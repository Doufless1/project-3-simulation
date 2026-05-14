"""
HVOF Laser Treatment Use Case — Two-stage simulation orchestrator.

Chains the full pipeline:
  Stage 1: Generate HVOF coating microstructure
  Stage 2: Simulate laser treatment (2D FDM)
  Post-processing: Measure porosity, melt depth, HAZ, hardness

Also provides parameter_sweep() for validation against published studies.
"""

from domain.entities import Material
from domain.coating_entities import CoatingConfig, LaserTreatmentResult

from infrastructure.coating.coating_generator import HVOFCoatingGenerator
from infrastructure.solvers.fdm_coating_solver_2d import FDMCoatingSolver2D
from infrastructure.coating.coating_analysis import build_treatment_result


class HVOFLaserUseCase:
    """
    Use case: Execute a two-stage HVOF coating + laser treatment simulation.

    Stage 1 → Stage 2 → Post-processing → LaserTreatmentResult
    """

    def __init__(
        self,
        coating_generator: HVOFCoatingGenerator = None,
        solver: FDMCoatingSolver2D = None,
    ):
        self._gen = coating_generator or HVOFCoatingGenerator()
        self._solver = solver or FDMCoatingSolver2D()

    def execute(
        self,
        material: Material,
        coating_config: CoatingConfig = None,
        laser_power: float = 500.0,
        beam_radius: float = 0.001,
        scan_speed: float = 0.010,
        resolution: float = 2e-6,
        seed: int = 42,
    ) -> LaserTreatmentResult:
        """
        Run the full two-stage simulation.

        Args:
            material: Coating material properties (WC-NiCr).
            coating_config: HVOF coating configuration. Uses defaults if None.
            laser_power: Laser power [W].
            beam_radius: Beam radius [m].
            scan_speed: Scan speed [m/s].
            resolution: Grid cell size [m]. Default 2 µm.
            seed: Random seed for reproducible microstructure.

        Returns:
            LaserTreatmentResult with all metrics.
        """
        config = coating_config or CoatingConfig()

        # --- Stage 1: Generate coating microstructure ---
        coating_grid = self._gen.generate(
            config=config,
            coating_k=material.thermal_conductivity,
            resolution=resolution,
            seed=seed,
        )

        # --- Stage 2: Laser treatment simulation ---
        solver_output = self._solver.solve(
            coating_grid=coating_grid,
            config=config,
            laser_power=laser_power,
            beam_radius=beam_radius,
            scan_speed=scan_speed,
            absorptivity=material.absorption,
            coating_rho=material.density,
            coating_cp=material.specific_heat,
            t_melt=material.t_melt,
            t_ambient=material.t_ambient,
        )

        # --- Post-processing ---
        result = build_treatment_result(
            coating_grid=coating_grid,
            config=config,
            solver_output=solver_output,
            t_melt=material.t_melt,
            laser_power=laser_power,
            scan_speed=scan_speed,
            beam_radius=beam_radius,
        )

        return result

    def parameter_sweep(
        self,
        material: Material,
        power_levels: list = None,
        coating_config: CoatingConfig = None,
        beam_radius: float = 0.001,
        scan_speed: float = 0.010,
        resolution: float = 2e-6,
        seed: int = 42,
    ) -> list:
        """
        Run simulations at multiple power levels for validation.

        Args:
            material: Coating material.
            power_levels: List of laser powers [W]. Default [200, 350, 500].
            coating_config: Coating config. Uses defaults if None.
            beam_radius: Beam radius [m].
            scan_speed: Scan speed [m/s].
            resolution: Grid cell size [m].
            seed: Random seed.

        Returns:
            List of LaserTreatmentResult, one per power level.
        """
        if power_levels is None:
            power_levels = [200.0, 350.0, 500.0]

        results = []
        for power in power_levels:
            result = self.execute(
                material=material,
                coating_config=coating_config,
                laser_power=power,
                beam_radius=beam_radius,
                scan_speed=scan_speed,
                resolution=resolution,
                seed=seed,  # Same microstructure for fair comparison
            )
            results.append(result)

        return results
