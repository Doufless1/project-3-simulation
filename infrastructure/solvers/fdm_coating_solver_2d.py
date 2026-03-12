"""
2D Finite Difference Coating Solver - Stage 2 Laser Treatment.

Specialized 2D FDM solver for laser treatment of HVOF coatings.
Differs from the general-purpose FDMHeatSolver3D in that it:

  1. Operates on a 2D coating grid with spatially varying k (pore-aware)
  2. Uses Beer-Lambert volumetric absorption: Q ~ exp(-mu*y)
  3. Tracks cumulative melt history per cell (melted |= T > T_melt)
  4. Handles coating + substrate as distinct material regions

Governing equation:
    rho*cp * dT/dt = d/dx(k*dT/dx) + d/dy(k*dT/dy) + Q(x,y,t)

Heat source (Gaussian beam scanning at speed v):
    Q(x,y,t) = (2*alpha*P / pi*r^2) * exp(-2*(x - v*t)^2 / r^2) * exp(-mu*y)

Numerical scheme: Forward-Time Central-Space (FTCS) explicit method.

Performance: all static arrays (harmonic-mean k, rho*cp) are precomputed
outside the time loop. Only T, Q, and melt tracking are updated per step.
"""

import sys
import time as time_module

import numpy as np

from domain.coating_entities import CoatingConfig, CoatingGrid


class FDMCoatingSolver2D:
    """
    2D transient heat solver for laser treatment of HVOF coatings.

    Handles spatially varying thermal conductivity and tracks
    cumulative melting for porosity closure analysis.
    """

    def __init__(self, absorption_coeff: float = 1e4):
        """
        Args:
            absorption_coeff: Optical absorption coefficient mu [1/m].
                Controls Beer-Lambert penetration depth.
                Typical for WC coatings: ~1e4 (penetration depth ~100 um).
        """
        self._mu = absorption_coeff

    def solve(
        self,
        coating_grid: CoatingGrid,
        config: CoatingConfig,
        laser_power: float,
        beam_radius: float,
        scan_speed: float,
        absorptivity: float,
        coating_rho: float,
        coating_cp: float,
        t_melt: float,
        t_ambient: float = 25.0,
        verbose: bool = True,
    ) -> dict:
        """
        Run the 2D laser treatment simulation.

        Args:
            coating_grid: Pre-generated coating microstructure.
            config: Coating configuration (substrate properties, etc).
            laser_power: Laser power P [W].
            beam_radius: Beam radius r [m].
            scan_speed: Scan speed v [m/s].
            absorptivity: Surface absorptivity alpha (0-1).
            coating_rho: Coating density rho [kg/m3].
            coating_cp: Coating specific heat cp [J/kg.K].
            t_melt: Melting temperature of the coating matrix [C].
            t_ambient: Ambient/initial temperature [C].
            verbose: Print progress updates.

        Returns:
            dict with keys:
                'temperature': final 2D temperature field
                'melted': cumulative melt mask (bool array)
                'duration_s': wall-clock time
        """
        start = time_module.perf_counter()

        ny, nx = coating_grid.shape
        dx = coating_grid.dx
        dy = coating_grid.dy

        # --- Build property arrays (spatially varying) ---
        rho = np.full((ny, nx), coating_rho, dtype=np.float64)
        cp = np.full((ny, nx), coating_cp, dtype=np.float64)

        # Substrate region has different properties
        cr = coating_grid.coating_rows
        rho[cr:, :] = config.substrate_rho
        cp[cr:, :] = config.substrate_cp

        # Thermal conductivity from the coating grid (already includes pores)
        k = coating_grid.k_map.copy()

        # --- Precompute static arrays (moved out of time loop) ---
        # Harmonic mean conductivities at cell interfaces (STATIC)
        k_xp = 2.0 * k[1:-1, 1:-1] * k[1:-1, 2:] / (k[1:-1, 1:-1] + k[1:-1, 2:] + 1e-30)
        k_xm = 2.0 * k[1:-1, 1:-1] * k[1:-1, :-2] / (k[1:-1, 1:-1] + k[1:-1, :-2] + 1e-30)
        k_yp = 2.0 * k[1:-1, 1:-1] * k[2:, 1:-1] / (k[1:-1, 1:-1] + k[2:, 1:-1] + 1e-30)
        k_ym = 2.0 * k[1:-1, 1:-1] * k[:-2, 1:-1] / (k[1:-1, 1:-1] + k[:-2, 1:-1] + 1e-30)

        # rho*cp for interior (STATIC)
        rho_cp_inv = 1.0 / (rho[1:-1, 1:-1] * cp[1:-1, 1:-1])

        # Precompute diffusion coefficients
        kx_p_dx2 = k_xp / dx**2
        kx_m_dx2 = k_xm / dx**2
        ky_p_dy2 = k_yp / dy**2
        ky_m_dy2 = k_ym / dy**2

        # --- CFL Stability ---
        kappa = k / (rho * cp)
        kappa_max = np.max(kappa)
        dt_max = 1.0 / (2.0 * kappa_max * (1.0 / dx**2 + 1.0 / dy**2))
        dt = dt_max * 0.4  # 40% safety margin

        # --- Simulation time ---
        scan_length = nx * dx
        total_time = scan_length / scan_speed
        n_steps = int(np.ceil(total_time / dt))

        # Cap steps for very long runs
        max_steps = 500_000
        if n_steps > max_steps:
            dt = total_time / max_steps
            n_steps = max_steps
            # Recompute since dt changed
            kx_p_dx2_dt = kx_p_dx2 * dt
            kx_m_dx2_dt = kx_m_dx2 * dt
            ky_p_dy2_dt = ky_p_dy2 * dt
            ky_m_dy2_dt = ky_m_dy2 * dt
        else:
            kx_p_dx2_dt = kx_p_dx2 * dt
            kx_m_dx2_dt = kx_m_dx2 * dt
            ky_p_dy2_dt = ky_p_dy2 * dt
            ky_m_dy2_dt = ky_m_dy2 * dt

        if verbose:
            print(f"    Grid: {ny}x{nx}, dt={dt:.2e}s, "
                  f"n_steps={n_steps}, scan_time={total_time:.4f}s")
            sys.stdout.flush()

        # --- Initialize ---
        T = np.full((ny, nx), t_ambient, dtype=np.float64)
        melted = np.zeros((ny, nx), dtype=bool)

        # Coordinate arrays
        x_coords = np.arange(nx) * dx
        y_coords = np.arange(ny) * dy

        # Beer-Lambert depth factor (precomputed)
        depth_factor = np.exp(-self._mu * y_coords)  # shape (ny,)

        # Gaussian prefactor, including mu for volumetric source [W/m3]
        # Q_vol = mu * (2*alpha*P / pi*r^2) * exp(-2*(x-vt)^2/r^2) * exp(-mu*y)
        gauss_prefactor = self._mu * 2.0 * absorptivity * laser_power / (np.pi * beam_radius**2)

        # Precompute dt * rho_cp_inv for source term
        dt_rho_cp_inv = dt * rho_cp_inv

        # --- Time-stepping loop (optimized) ---
        progress_interval = max(n_steps // 10, 1)

        for step in range(n_steps):
            t = step * dt
            laser_x = scan_speed * t

            # Gaussian lateral profile
            gauss_x = np.exp(-2.0 * (x_coords - laser_x)**2 / beam_radius**2)

            # Volumetric heat source Q(x,y)
            Q_interior = gauss_prefactor * np.outer(
                depth_factor[1:-1], gauss_x[1:-1]
            )

            # FDM diffusion (all precomputed coefficients)
            dT_x = (kx_p_dx2_dt * (T[1:-1, 2:] - T[1:-1, 1:-1])
                     - kx_m_dx2_dt * (T[1:-1, 1:-1] - T[1:-1, :-2]))
            dT_y = (ky_p_dy2_dt * (T[2:, 1:-1] - T[1:-1, 1:-1])
                     - ky_m_dy2_dt * (T[1:-1, 1:-1] - T[:-2, 1:-1]))

            T[1:-1, 1:-1] += (dT_x + dT_y) * rho_cp_inv + Q_interior * dt_rho_cp_inv

            # Boundary conditions (in-place)
            T[0, :] = T[1, :]          # Top: adiabatic
            T[-1, :] = t_ambient       # Bottom: fixed
            T[:, 0] = T[:, 1]          # Left: adiabatic
            T[:, -1] = T[:, -2]        # Right: adiabatic

            # Cumulative melt tracking
            melted |= (T > t_melt)

            # Progress
            if verbose and (step % progress_interval == 0) and step > 0:
                pct = step / n_steps * 100
                peak = np.max(T)
                print(f"      {pct:5.0f}% | step {step}/{n_steps} | "
                      f"peak T = {peak:.0f} C")
                sys.stdout.flush()

        elapsed = time_module.perf_counter() - start

        if verbose:
            print(f"    Done in {elapsed:.1f}s | "
                  f"Peak T = {np.max(T):.0f} C | "
                  f"Melted cells = {np.sum(melted)}")
            sys.stdout.flush()

        return {
            "temperature": T,
            "melted": melted,
            "duration_s": elapsed,
        }
