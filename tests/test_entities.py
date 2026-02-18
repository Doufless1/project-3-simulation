"""
Tests for Domain Entities — Validation and edge cases.

Tests cover:
- Material validation (positive values, melt < vaporization)
- LaserBeam validation (positive params)
- Trajectory validation (consistent lengths)
- Value objects (immutability, normalization)
"""

import unittest
import os

from domain.entities import Material, LaserBeam, Trajectory
from domain.value_objects import Position3D, Temperature, Composition
from domain.exceptions import (
    InvalidMaterialError,
    InvalidLaserConfigError,
    InvalidTrajectoryError,
)


class TestMaterial(unittest.TestCase):
    """Test Material entity validation."""

    def test_valid_material_creation(self):
        """Valid material should be created without errors."""
        mat = Material(
            name="Test Steel",
            absorption=0.35,
            thermal_conductivity=15.0,
            density=8000.0,
            specific_heat=500.0,
        )
        self.assertEqual(mat.name, "Test Steel")
        self.assertAlmostEqual(mat.thermal_diffusivity, 15.0 / (8000.0 * 500.0))

    def test_invalid_absorption_raises(self):
        """Absorption outside (0, 1] should raise."""
        with self.assertRaises(InvalidMaterialError):
            Material("Bad", absorption=1.5, thermal_conductivity=10,
                     density=7000, specific_heat=400)

    def test_zero_absorption_raises(self):
        """Zero absorption should raise."""
        with self.assertRaises(InvalidMaterialError):
            Material("Bad", absorption=0.0, thermal_conductivity=10,
                     density=7000, specific_heat=400)

    def test_melt_above_vaporization_raises(self):
        """Melt temp above vaporization should raise."""
        with self.assertRaises(InvalidMaterialError):
            Material("Bad", absorption=0.5, thermal_conductivity=10,
                     density=7000, specific_heat=400,
                     t_melt=3000, t_vaporization=2000)

    def test_negative_density_raises(self):
        """Negative density should raise."""
        with self.assertRaises(InvalidMaterialError):
            Material("Bad", absorption=0.5, thermal_conductivity=10,
                     density=-100, specific_heat=400)


class TestLaserBeam(unittest.TestCase):
    """Test LaserBeam entity validation."""

    def test_valid_laser(self):
        """Valid laser should have correct derived properties."""
        laser = LaserBeam(power=500, wavelength=1.064e-6,
                          spot_radius=50e-6, focal_length=0.1)
        self.assertGreater(laser.peak_intensity, 0)
        self.assertGreater(laser.rayleigh_range, 0)

    def test_zero_power_raises(self):
        """Zero power should raise."""
        with self.assertRaises(InvalidLaserConfigError):
            LaserBeam(power=0, wavelength=1e-6,
                      spot_radius=50e-6, focal_length=0.1)

    def test_negative_wavelength_raises(self):
        """Negative wavelength should raise."""
        with self.assertRaises(InvalidLaserConfigError):
            LaserBeam(power=500, wavelength=-1e-6,
                      spot_radius=50e-6, focal_length=0.1)


class TestTrajectory(unittest.TestCase):
    """Test Trajectory entity validation."""

    def test_valid_trajectory(self):
        """Valid trajectory should work."""
        traj = Trajectory(
            time=[0.0, 0.1], x=[0.0, 0.01], y=[0.0, 0.0], z=[0.05, 0.05]
        )
        self.assertEqual(traj.n_points, 2)

    def test_mismatched_lengths_raises(self):
        """Mismatched array lengths should raise."""
        with self.assertRaises(InvalidTrajectoryError):
            Trajectory(time=[0, 1], x=[0], y=[0, 0], z=[0, 0])

    def test_single_point_raises(self):
        """Single-point trajectory should raise."""
        with self.assertRaises(InvalidTrajectoryError):
            Trajectory(time=[0], x=[0], y=[0], z=[0])


class TestValueObjects(unittest.TestCase):
    """Test immutable value objects."""

    def test_position3d_distance(self):
        """Distance calculation should be correct."""
        p1 = Position3D(0, 0, 0)
        p2 = Position3D(3, 4, 0)
        self.assertAlmostEqual(p1.distance_to(p2), 5.0)

    def test_temperature_below_absolute_zero(self):
        """Below absolute zero should raise."""
        with self.assertRaises(ValueError):
            Temperature(-300)

    def test_temperature_kelvin_conversion(self):
        """Kelvin conversion should be correct."""
        t = Temperature(100)
        self.assertAlmostEqual(t.kelvin, 373.15)

    def test_composition_normalization(self):
        """Non-normalized composition should auto-normalize."""
        comp = Composition(elements={"Fe": 50, "Cr": 30, "Ni": 20})
        total = sum(comp.elements.values())
        self.assertAlmostEqual(total, 1.0, places=5)

    def test_composition_negative_fraction_raises(self):
        """Negative fraction should raise."""
        with self.assertRaises(ValueError):
            Composition(elements={"Fe": -0.5, "Cr": 1.5})

    def test_position3d_immutable(self):
        """Position3D should be frozen."""
        p = Position3D(1.0, 2.0, 3.0)
        with self.assertRaises(AttributeError):
            p.x = 5.0


if __name__ == "__main__":
    unittest.main()
