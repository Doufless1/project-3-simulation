"""
3D Finite Difference Method Heat Solver.

Implements IHeatSolver for full 3D transient heat conduction:
    ∂T/∂t = κ (∂²T/∂x² + ∂²T/∂y² + ∂²T/∂z²)

Numerical scheme: Forward-Time Central-Space (FTCS) explicit method.

CIA Triad:
- Integrity: Automatic CFL stability check before solving.
- Availability: Grid size limit to prevent memory exhaustion (DoS).

DRY: Beam propagation logic imported from a shared helper,
     not duplicated from the laser module.
"""

import math
import time as time_module

import numpy as np

from domain.entities import (
    Material,
    LaserBeam,
    SimulationResult,
    Trajectory,
)
from domain.exceptions import GridTooLargeError, SolverInstabilityError
from domain.ports import IHeatSolver, ILaserSource


# ============================================================================
# Shared Helper: Beam Propagation (DRY — used by solver only)
# ============================================================================

def compute_spot_radius_at_z(z: float, laser: LaserBeam) -> float:
    """
    Gaussian beam propagation: w(z) = w0 · √(1 + (z/z_R)²).

    Extracted as a pure function to avoid duplication (DRY).
    """
    z_r = laser.rayleigh_range
    return laser.spot_radius * math.sqrt(1 + (z / z_r) ** 2)


# ============================================================================
# Constants
# ============================================================================

MAX_GRID_CELLS = 5_000_000  # Safety limit (Availability — DoS prevention)


# ============================================================================
# 3D FDM Solver
# ============================================================================

class FDMHeatSolver3D(IHeatSolver):
    """
    3D transient heat solver using the explicit FTCS finite difference method.

    Solves: ∂T/∂t = κ · ∇²T  with surface heat flux from laser.

    Boundary Conditions:
      - Top surface (z=0): Neumann BC (laser heat flux during scan)
      - Bottom (z=max): Adiabatic (∂T/∂z = 0) or Dirichlet (T_ambient)
      - Lateral boundaries: Adiabatic (∂T/∂x = 0, ∂T/∂y = 0)
    """

    def __init__(
        self,
        laser_source: ILaserSource,
        bottom_bc: str = "adiabatic",
    ):
        """
        Args:
            laser_source: Injected beam profile strategy (DIP).
            bottom_bc: Bottom boundary condition ('adiabatic' or 'fixed').
        """
        self._laser_source = laser_source
        self._bottom_bc = bottom_bc

    @property
    def name(self) -> str:
        return "FDM-3D-FTCS"

    def solve(
        self,
        material: Material,
        laser: LaserBeam,
        trajectory: Trajectory,
        grid_size: tuple = (0.010, 0.010, 0.002),
        resolution: float = 100e-6,
    ) -> SimulationResult:
        """
        Run the full 3D heat simulation.

        Args:
            material: Target material properties.
            laser: Laser beam configuration.
            trajectory: Motion path (time, x, y, z arrays).
            grid_size: (Lx, Ly, Lz) domain size in meters.
                       Default: 10mm × 10mm × 2mm.
            resolution: Spatial step Δx = Δy = Δz [m].
                        Default: 100 µm.

        Returns:
            SimulationResult with 3D temperature snapshot and metrics.
        """
        start_time = time_module.perf_counter()

        # --- Grid Setup ---
        lx, ly, lz = grid_size
        dx = resolution
        dy = resolution
        dz = resolution

        nx = max(int(lx / dx), 3)
        ny = max(int(ly / dy), 3)
        nz = max(int(lz / dz), 3)

        total_cells = nx * ny * nz
        self._check_grid_size(total_cells)

        kappa = material.thermal_diffusivity

        # --- CFL Stability Check ---
        dt_max = dx ** 2 / (6.0 * kappa)  # 3D stability limit
        dt_trajectory = self._compute_trajectory_dt(trajectory)
        dt = min(dt_max * 0.8, dt_trajectory)  # 80% safety margin

        if dt <= 0:
            raise SolverInstabilityError(
                f"Invalid time step dt={dt:.2e}s. Check material diffusivity."
            )

        # --- Initialize Temperature Field ---
        t_field = np.full((nx, ny, nz), material.t_ambient, dtype=np.float64)

        # Coordinate arrays
        x_coords = np.linspace(0, lx, nx)
        y_coords = np.linspace(0, ly, ny)
        z_coords = np.linspace(0, lz, nz)

        # 2D meshgrid for surface (z=0)
        x_mesh, y_mesh = np.meshgrid(x_coords, y_coords, indexing="ij")

        # --- Cumulative Fluence Map ---
        fluence_map = np.zeros((nx, ny), dtype=np.float64)

        # --- Precompute Coefficients ---
        rx = kappa * dt / dx ** 2
        ry = kappa * dt / dy ** 2
        rz = kappa * dt / dz ** 2

        # --- Time-Stepping Loop ---
        n_steps = trajectory.n_points
        traj_time = np.array(trajectory.time)
        traj_x = np.array(trajectory.x)
        traj_y = np.array(trajectory.y)
        traj_z = np.array(trajectory.z)

        for step_idx in range(n_steps - 1):
            # Determine sub-steps for this trajectory segment
            seg_dt = traj_time[step_idx + 1] - traj_time[step_idx]
            if seg_dt <= 0:
                continue

            n_sub = max(int(np.ceil(seg_dt / dt)), 1)
            actual_dt = seg_dt / n_sub

            # Recalculate coefficients for actual_dt
            rx_a = kappa * actual_dt / dx ** 2
            ry_a = kappa * actual_dt / dy ** 2
            rz_a = kappa * actual_dt / dz ** 2

            # Beam position (interpolate within segment)
            beam_x = traj_x[step_idx]
            beam_y = traj_y[step_idx]
            beam_z_distance = traj_z[step_idx]

            # Compute spot radius at this defocus distance
            w_z = compute_spot_radius_at_z(beam_z_distance, laser)

            # Compute surface intensity distribution
            intensity_2d = self._laser_source.compute_intensity(
                x_mesh, y_mesh, beam_x, beam_y, w_z, laser.power
            )

            # Apply material absorption
            absorbed_intensity = material.absorption * intensity_2d

            # Accumulate fluence
            fluence_map += absorbed_intensity * seg_dt

            # Surface heat flux: q = α · I(x,y) [W/m²]
            # Neumann BC at z=0: dT/dz|_{z=0} = -q/k
            surface_flux_temperature_rate = (
                absorbed_intensity / (material.density * material.specific_heat * dz)
            )

            for _ in range(n_sub):
                t_field = self._diffusion_step(
                    t_field, rx_a, ry_a, rz_a,
                    surface_flux_temperature_rate, actual_dt,
                    material.t_ambient,
                )

        # --- Extract Results ---
        elapsed = time_module.perf_counter() - start_time

        peak_temp = float(np.max(t_field))
        peak_fluence = float(np.max(fluence_map))

        melt_depth = self._find_threshold_depth(
            t_field, z_coords, material.t_melt
        )
        vap_depth = self._find_threshold_depth(
            t_field, z_coords, material.t_vaporization
        )

        total_energy = float(np.sum(fluence_map) * dx * dy)

        return SimulationResult(
            temperature_field=t_field.tolist(),
            fluence_map=fluence_map.tolist(),
            x_coords=x_coords.tolist(),
            y_coords=y_coords.tolist(),
            z_coords=z_coords.tolist(),
            time_coords=list(trajectory.time),
            peak_temperature_celsius=peak_temp,
            peak_fluence_j_per_m2=peak_fluence,
            melt_depth_m=melt_depth,
            vaporization_depth_m=vap_depth,
            total_energy_j=total_energy,
            material_name=material.name,
            solver_name=self.name,
            duration_seconds=elapsed,
        )

    # ===== Private Methods (SRP: each handles one sub-task) =====

    def _diffusion_step(
        self,
        t_field: np.ndarray,
        rx: float,
        ry: float,
        rz: float,
        surface_source: np.ndarray,
        dt: float,
        t_ambient: float,
    ) -> np.ndarray:
        """
        Single FTCS diffusion step in 3D.

        Uses numpy slicing for vectorized computation.
        """
        nx, ny, nz = t_field.shape
        t_new = t_field.copy()

        # Interior points: 3D Laplacian
        t_new[1:-1, 1:-1, 1:-1] = t_field[1:-1, 1:-1, 1:-1] + (
            rx * (t_field[2:, 1:-1, 1:-1] + t_field[:-2, 1:-1, 1:-1]
                  - 2 * t_field[1:-1, 1:-1, 1:-1])
            + ry * (t_field[1:-1, 2:, 1:-1] + t_field[1:-1, :-2, 1:-1]
                    - 2 * t_field[1:-1, 1:-1, 1:-1])
            + rz * (t_field[1:-1, 1:-1, 2:] + t_field[1:-1, 1:-1, :-2]
                    - 2 * t_field[1:-1, 1:-1, 1:-1])
        )

        # --- Boundary Conditions ---

        # Top surface (z=0): Apply laser heat flux
        t_new[:, :, 0] += surface_source * dt

        # Also apply diffusion at surface (z=0) using ghost node = z=1
        t_new[1:-1, 1:-1, 0] += rz * (
            t_field[1:-1, 1:-1, 1] - t_field[1:-1, 1:-1, 0]
        )

        # Bottom boundary (z = nz-1)
        if self._bottom_bc == "adiabatic":
            t_new[:, :, -1] = t_new[:, :, -2]  # Zero gradient
        else:
            t_new[:, :, -1] = t_ambient  # Fixed temperature

        # Lateral boundaries: Adiabatic (mirror)
        t_new[0, :, :] = t_new[1, :, :]
        t_new[-1, :, :] = t_new[-2, :, :]
        t_new[:, 0, :] = t_new[:, 1, :]
        t_new[:, -1, :] = t_new[:, -2, :]

        return t_new

    @staticmethod
    def _check_grid_size(total_cells: int) -> None:
        """DoS prevention: reject grids that would exhaust memory."""
        if total_cells > MAX_GRID_CELLS:
            raise GridTooLargeError(total_cells, MAX_GRID_CELLS)

    @staticmethod
    def _compute_trajectory_dt(trajectory: Trajectory) -> float:
        """Compute the smallest time step from the trajectory."""
        times = trajectory.time
        min_dt = float("inf")
        for i in range(len(times) - 1):
            seg_dt = times[i + 1] - times[i]
            if seg_dt > 0:
                min_dt = min(min_dt, seg_dt)
        return min_dt if min_dt != float("inf") else 1e-4

    @staticmethod
    def _find_threshold_depth(
        t_field: np.ndarray, z_coords: np.ndarray, threshold: float
    ):
        """Find maximum depth where temperature exceeds threshold."""
        # Find along z-axis (axis=2), check max temperature at each z
        max_temp_per_z = np.max(t_field, axis=(0, 1))

        # Find deepest z where temp > threshold
        exceeds = np.where(max_temp_per_z > threshold)[0]
        if len(exceeds) == 0:
            return None
        return float(z_coords[exceeds[-1]])
