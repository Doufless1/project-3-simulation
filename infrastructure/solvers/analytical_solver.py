"""
Analytical Heat Solver — Closed-form solution for semi-infinite solid.

Implements IHeatSolver using the analytical formula:
    T(z,t) = T_amb + (Q / (ρ·c_p·√(π·κ·t))) · exp(-z²/(4·κ·t))

Use case: Quick validation and comparison against the FDM solver.
Limitation: Only handles 1D depth profile at the point of peak fluence.
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
from domain.ports import IHeatSolver, ILaserSource


class AnalyticalHeatSolver(IHeatSolver):
    """
    Analytical solution for instantaneous surface heating.

    Suitable for fast validation of the FDM solver.
    """

    def __init__(self, laser_source: ILaserSource):
        self._laser_source = laser_source

    @property
    def name(self) -> str:
        return "Analytical-1D"

    def solve(
        self,
        material: Material,
        laser: LaserBeam,
        trajectory: Trajectory,
        grid_size: tuple = (0.010, 0.010, 0.002),
        resolution: float = 50e-6,
    ) -> SimulationResult:
        """
        Compute analytical temperature profile T(z) at center.

        Simplification: Uses total deposited fluence as an
        instantaneous heat pulse.
        """
        start_time = time_module.perf_counter()

        lz = grid_size[2]
        nz = max(int(lz / resolution), 10)
        z_coords = np.linspace(0, lz, nz)

        kappa = material.thermal_diffusivity
        rho = material.density
        cp = material.specific_heat

        # Estimate total fluence at center from trajectory
        total_fluence = self._estimate_center_fluence(
            material, laser, trajectory
        )

        # Absorbed fluence
        q_absorbed = material.absorption * total_fluence  # J/m²

        # Effective pulse duration
        pulse_duration = trajectory.duration
        if pulse_duration <= 0:
            pulse_duration = 1e-6

        # T(z) at end of pulse
        t_eval = pulse_duration
        denominator = rho * cp * math.sqrt(math.pi * kappa * t_eval)

        if denominator <= 0:
            temperature_z = np.full(nz, material.t_ambient)
        else:
            temperature_z = material.t_ambient + (
                q_absorbed / denominator
            ) * np.exp(-z_coords ** 2 / (4 * kappa * t_eval))

        peak_temp = float(np.max(temperature_z))

        # Find melt depth
        melt_idx = np.where(temperature_z > material.t_melt)[0]
        melt_depth = float(z_coords[melt_idx[-1]]) if len(melt_idx) > 0 else None

        vap_idx = np.where(temperature_z > material.t_vaporization)[0]
        vap_depth = float(z_coords[vap_idx[-1]]) if len(vap_idx) > 0 else None

        elapsed = time_module.perf_counter() - start_time

        return SimulationResult(
            temperature_field=temperature_z.tolist(),
            fluence_map=[],
            x_coords=[],
            y_coords=[],
            z_coords=z_coords.tolist(),
            time_coords=[0.0, pulse_duration],
            peak_temperature_celsius=peak_temp,
            peak_fluence_j_per_m2=float(q_absorbed),
            melt_depth_m=melt_depth,
            vaporization_depth_m=vap_depth,
            total_energy_j=0.0,
            material_name=material.name,
            solver_name=self.name,
            duration_seconds=elapsed,
        )

    def _estimate_center_fluence(
        self, material: Material, laser: LaserBeam, trajectory: Trajectory
    ) -> float:
        """Integrate intensity at grid center over the trajectory."""
        cx = (max(trajectory.x) + min(trajectory.x)) / 2
        cy = (max(trajectory.y) + min(trajectory.y)) / 2

        total_fluence = 0.0
        for i in range(len(trajectory.time) - 1):
            dt_seg = trajectory.time[i + 1] - trajectory.time[i]
            if dt_seg <= 0:
                continue

            # Compute spot radius at defocus distance
            z_dist = trajectory.z[i]
            z_r = laser.rayleigh_range
            w_z = laser.spot_radius * math.sqrt(1 + (z_dist / z_r) ** 2)

            # Gaussian intensity at center
            r_sq = (cx - trajectory.x[i]) ** 2 + (cy - trajectory.y[i]) ** 2
            i_peak = 2 * laser.power / (math.pi * w_z ** 2)
            intensity = i_peak * math.exp(-2 * r_sq / w_z ** 2)

            total_fluence += intensity * dt_seg

        return total_fluence
