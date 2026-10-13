"""
Top-Hat Laser Source — Flat-top beam profile.

Implements ILaserSource using uniform intensity inside the spot:
    I = P / (πw²) inside the spot, 0 outside.

Single Responsibility: Only computes top-hat intensity distribution.
"""

import numpy as np

from domain.ports import ILaserSource


class TopHatLaserSource(ILaserSource):
    """Strategy: Flat-top (top-hat) beam intensity profile."""

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
        Compute flat-top intensity: uniform inside spot, zero outside.

        I = P / (πw²) for r ≤ w, else 0
        """
        r_squared = (x_grid - center_x) ** 2 + (y_grid - center_y) ** 2
        uniform_intensity = power / (np.pi * spot_radius ** 2)
        mask = r_squared <= spot_radius ** 2
        return np.where(mask, uniform_intensity, 0.0)
