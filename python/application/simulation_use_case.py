"""
Simulation Use Case — Orchestrates a complete laser-HVOF simulation.

This is the Application Layer entry point. It:
1. Accepts injected dependencies (DIP — no concrete classes referenced).
2. Coordinates Domain entities and Infrastructure implementations.
3. Logs all runs via IAuditLogger (STRIDE: Repudiation defense).

Single Responsibility: Orchestration only — no physics or I/O logic here.
"""

from domain.entities import Material, LaserBeam, SimulationResult
from domain.ports import IHeatSolver, IMotionGenerator, IAuditLogger


class SimulationUseCase:
    """
    Use case: Execute a laser treatment simulation.

    Dependencies are injected via constructor (Dependency Inversion).
    This class has NO knowledge of which solver, motion generator,
    or logger is being used.
    """

    def __init__(
        self,
        heat_solver: IHeatSolver,
        motion_generator: IMotionGenerator,
        audit_logger: IAuditLogger,
    ):
        self._solver = heat_solver
        self._motion_gen = motion_generator
        self._audit = audit_logger

    def execute(
        self,
        material: Material,
        laser: LaserBeam,
        motion_params: dict,
        grid_size: tuple = (0.010, 0.010, 0.002),
        resolution: float = 100e-6,
    ) -> SimulationResult:
        """
        Execute the full simulation pipeline.

        Steps:
            1. Generate trajectory from motion parameters.
            2. Log simulation start (STRIDE: audit trail).
            3. Run thermal solver.
            4. Log simulation end with result summary.

        Args:
            material: Target material properties.
            laser: Laser beam configuration.
            motion_params: Parameters for the motion generator.
            grid_size: (Lx, Ly, Lz) domain dimensions [m].
            resolution: Spatial resolution [m].

        Returns:
            SimulationResult with full 3D thermal data.
        """
        # Step 1: Generate trajectory
        trajectory = self._motion_gen.generate(**motion_params)

        # Step 2: Audit — log start
        sim_params = {
            "material": material.name,
            "laser_power_w": laser.power,
            "laser_wavelength_m": laser.wavelength,
            "grid_size_m": list(grid_size),
            "resolution_m": resolution,
            "trajectory_points": trajectory.n_points,
        }
        run_id = self._audit.log_simulation_start(
            material.name, self._solver.name, sim_params
        )

        try:
            # Step 3: Solve
            result = self._solver.solve(
                material=material,
                laser=laser,
                trajectory=trajectory,
                grid_size=grid_size,
                resolution=resolution,
            )

            # Step 4: Audit — log end
            result_summary = {
                "peak_temp_c": result.peak_temperature_celsius,
                "peak_fluence_j_m2": result.peak_fluence_j_per_m2,
                "melt_depth_m": result.melt_depth_m,
                "duration_s": result.duration_seconds,
            }
            self._audit.log_simulation_end(run_id, result_summary)

            return result

        except Exception as exc:
            self._audit.log_error(run_id, str(exc))
            raise
