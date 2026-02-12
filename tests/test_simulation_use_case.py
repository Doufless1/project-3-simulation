"""
Tests for the Simulation Use Case — End-to-end integration test.

Tests the full pipeline: motion generation → heat solving → result.
Uses real Infrastructure implementations (not mocks) for integration confidence.
"""

import unittest
import sys
import os

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from domain.entities import Material, LaserBeam

from infrastructure.laser.gaussian_source import GaussianLaserSource
from infrastructure.laser.laser_factory import LaserFactory
from infrastructure.motion.raster_generator import RasterGenerator
from infrastructure.motion.linear_generator import LinearGenerator
from infrastructure.solvers.fdm_heat_solver_3d import FDMHeatSolver3D
from infrastructure.solvers.analytical_solver import AnalyticalHeatSolver
from infrastructure.logging.audit_logger import AuditLogger

from application.simulation_use_case import SimulationUseCase


class TestSimulationUseCase(unittest.TestCase):
    """Integration test for the simulation pipeline."""

    def setUp(self):
        """Set up test fixtures."""
        self.material = Material(
            name="Test Steel",
            absorption=0.35,
            thermal_conductivity=15.0,
            density=8000.0,
            specific_heat=500.0,
            t_melt=1400.0,
            t_vaporization=2800.0,
        )
        self.laser = LaserFactory.create_nd_yag(power=200)
        self.output_dir = os.path.join(os.path.dirname(__file__), "test_output")
        os.makedirs(self.output_dir, exist_ok=True)

    def test_fdm3d_raster_simulation(self):
        """Full simulation with FDM3D solver and raster motion."""
        laser_source = GaussianLaserSource()
        solver = FDMHeatSolver3D(laser_source=laser_source)
        motion_gen = RasterGenerator()
        audit = AuditLogger(log_dir=self.output_dir)

        use_case = SimulationUseCase(
            heat_solver=solver,
            motion_generator=motion_gen,
            audit_logger=audit,
        )

        motion_params = {
            "x_start": 0.0, "x_end": 0.005,
            "y_start": 0.0, "y_end": 0.005,
            "z_focus": 0.05,
            "line_spacing": 1e-3,
            "scan_speed": 0.5,
        }

        result = use_case.execute(
            material=self.material,
            laser=self.laser,
            motion_params=motion_params,
            grid_size=(0.005, 0.005, 0.001),
            resolution=500e-6,  # Coarse for speed
        )

        # Verify result structure
        self.assertGreater(result.peak_temperature_celsius, 20.0)
        self.assertGreater(result.peak_fluence_j_per_m2, 0)
        self.assertEqual(result.material_name, "Test Steel")
        self.assertEqual(result.solver_name, "FDM-3D-FTCS")
        self.assertGreater(result.duration_seconds, 0)

    def test_analytical_linear_simulation(self):
        """Full simulation with Analytical solver and linear motion."""
        laser_source = GaussianLaserSource()
        solver = AnalyticalHeatSolver(laser_source=laser_source)
        motion_gen = LinearGenerator()
        audit = AuditLogger(log_dir=self.output_dir)

        use_case = SimulationUseCase(
            heat_solver=solver,
            motion_generator=motion_gen,
            audit_logger=audit,
        )

        motion_params = {
            "x_start": 0.0, "y_start": 0.0025,
            "x_end": 0.005, "y_end": 0.0025,
            "z_focus": 0.05,
            "scan_speed": 0.5,
        }

        result = use_case.execute(
            material=self.material,
            laser=self.laser,
            motion_params=motion_params,
            grid_size=(0.005, 0.005, 0.001),
            resolution=100e-6,
        )

        self.assertGreater(result.peak_temperature_celsius, 20.0)
        self.assertEqual(result.solver_name, "Analytical-1D")

    def test_laser_factory_creates_valid_beams(self):
        """Factory should create valid LaserBeam entities."""
        nd_yag = LaserFactory.create_nd_yag(500)
        co2 = LaserFactory.create_co2(1000)
        fiber = LaserFactory.create_fiber(200)

        self.assertEqual(nd_yag.power, 500)
        self.assertEqual(co2.wavelength, 10.6e-6)
        self.assertEqual(fiber.spot_radius, 25e-6)


if __name__ == "__main__":
    unittest.main()
