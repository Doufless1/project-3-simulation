"""
Gaussian Laser Source — TEM00 beam profile.

Implements ILaserSource using the standard Gaussian intensity formula:
    I(x,y) = (2P / πw²) · exp(-2r²/w²)

Single Responsibility: Only computes Gaussian intensity distribution.
"""

import numpy as np

from domain.ports import ILaserSource


class GaussianLaserSource(ILaserSource):
    """Strategy: Gaussian (TEM00) beam intensity profile."""

    def compute_intensity(
        self,
        x_grid: np.ndarray,
        y_grid: np.ndarray,
        center_x: float,
        center_y: float,
        spot_radius: float,
        power: float,
    ) -> np.ndarray:
        """
        Compute 2D Gaussian intensity distribution.

        I(x,y) = (2P / πw²) · exp(-2r²/w²)
        """
        r_squared = (x_grid - center_x) ** 2 + (y_grid - center_y) ** 2
        peak_intensity = 2.0 * power / (np.pi * spot_radius ** 2)
        return peak_intensity * np.exp(-2.0 * r_squared / spot_radius ** 2)
