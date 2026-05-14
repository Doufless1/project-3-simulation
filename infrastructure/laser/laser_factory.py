"""
Laser Factory — Creates pre-configured LaserBeam entities.

Design Pattern: Factory Method
DRY: Centralizes laser preset definitions instead of repeating
     wavelength/focal_length values throughout the codebase.
"""

from domain.entities import LaserBeam


class LaserFactory:
    """Factory for creating common laser presets."""

    @staticmethod
    def create_nd_yag(power: float, spot_radius: float = 50e-6) -> LaserBeam:
        """Create Nd:YAG laser (1064 nm)."""
        return LaserBeam(
            power=power,
            wavelength=1.064e-6,
            spot_radius=spot_radius,
            focal_length=0.1,
        )

    @staticmethod
    def create_co2(power: float, spot_radius: float = 100e-6) -> LaserBeam:
        """Create CO2 laser (10.6 µm)."""
        return LaserBeam(
            power=power,
            wavelength=10.6e-6,
            spot_radius=spot_radius,
            focal_length=0.127,
        )

    @staticmethod
    def create_fiber(power: float, spot_radius: float = 25e-6) -> LaserBeam:
        """Create fiber laser (1070 nm)."""
        return LaserBeam(
            power=power,
            wavelength=1.07e-6,
            spot_radius=spot_radius,
            focal_length=0.16,
        )
