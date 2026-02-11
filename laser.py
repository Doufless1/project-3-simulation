"""
Laser Configuration Module for Laser Processing Simulation.

Provides laser parameters and Gaussian intensity calculations.
This is the interface expected by process_model.py.

Author: Agent 2 (Stub Implementation)
"""

import numpy as np
from dataclasses import dataclass
from typing import Optional


@dataclass
class LaserConfig:
    """
    Laser beam configuration parameters.
    
    Attributes:
        power: Laser output power [W]
        wavelength: Laser wavelength [m] (e.g., 1.064e-6 for Nd:YAG)
        base_spot_radius: Spot radius at focus (w0) [m]
        focal_length: Focal length of focusing optic [m]
        mode: Beam mode ('TEM00' for Gaussian, 'tophat' for flat-top)
    """
    power: float
    wavelength: float
    base_spot_radius: float
    focal_length: float
    mode: str = 'TEM00'
    
    def __post_init__(self):
        """Validate parameters."""
        if self.power <= 0:
            raise ValueError("Power must be positive")
        if self.wavelength <= 0:
            raise ValueError("Wavelength must be positive")
        if self.base_spot_radius <= 0:
            raise ValueError("Spot radius must be positive")
    
    @property
    def rayleigh_range(self) -> float:
        """
        Rayleigh range z_R [m].
        
        Distance from focus where beam radius increases by sqrt(2).
        z_R = π * w0² / λ
        """
        return np.pi * self.base_spot_radius**2 / self.wavelength
    
    @property
    def peak_intensity(self) -> float:
        """
        Peak intensity at focus [W/m²].
        
        I_0 = 2P / (π * w0²) for Gaussian beam
        """
        return 2 * self.power / (np.pi * self.base_spot_radius**2)
    
    @property
    def divergence_angle(self) -> float:
        """
        Far-field divergence half-angle [rad].
        
        θ = λ / (π * w0)
        """
        return self.wavelength / (np.pi * self.base_spot_radius)


def compute_spot_radius(z: float, laser: LaserConfig) -> float:
    """
    Compute beam spot radius at distance z from focus.
    
    Implements Gaussian beam propagation:
    w(z) = w0 * sqrt(1 + (z/z_R)²)
    
    Args:
        z: Distance from focal plane [m] (can be negative)
        laser: LaserConfig with beam parameters
        
    Returns:
        Spot radius at distance z [m]
    """
    z_R = laser.rayleigh_range
    return laser.base_spot_radius * np.sqrt(1 + (z / z_R)**2)


def compute_spot_radius_array(z_arr: np.ndarray, laser: LaserConfig) -> np.ndarray:
    """
    Vectorized computation of spot radius for array of z values.
    
    Args:
        z_arr: Array of distances from focal plane [m]
        laser: LaserConfig with beam parameters
        
    Returns:
        Array of spot radii [m]
    """
    z_R = laser.rayleigh_range
    return laser.base_spot_radius * np.sqrt(1 + (z_arr / z_R)**2)


def gaussian_intensity(
    x: np.ndarray,
    y: np.ndarray,
    center_x: float,
    center_y: float,
    spot_radius: float,
    power: float
) -> np.ndarray:
    """
    Calculate 2D Gaussian intensity distribution.
    
    I(x,y) = (2P / πw²) * exp(-2r²/w²)
    
    where r² = (x - x0)² + (y - y0)²
    
    Args:
        x: X-coordinates [m] (can be 2D meshgrid)
        y: Y-coordinates [m] (can be 2D meshgrid)
        center_x: Beam center X position [m]
        center_y: Beam center Y position [m]
        spot_radius: Current spot radius w [m]
        power: Laser power [W]
        
    Returns:
        Intensity array [W/m²]
    """
    # Compute radial distance squared
    r_squared = (x - center_x)**2 + (y - center_y)**2
    
    # Peak intensity
    I_peak = 2 * power / (np.pi * spot_radius**2)
    
    # Gaussian profile
    return I_peak * np.exp(-2 * r_squared / spot_radius**2)


def tophat_intensity(
    x: np.ndarray,
    y: np.ndarray,
    center_x: float,
    center_y: float,
    spot_radius: float,
    power: float
) -> np.ndarray:
    """
    Calculate flat-top (top-hat) intensity distribution.
    
    I = P / (π * w²) inside the spot, 0 outside
    
    Args:
        x: X-coordinates [m]
        y: Y-coordinates [m]
        center_x: Beam center X position [m]
        center_y: Beam center Y position [m]
        spot_radius: Spot radius [m]
        power: Laser power [W]
        
    Returns:
        Intensity array [W/m²]
    """
    r_squared = (x - center_x)**2 + (y - center_y)**2
    
    # Uniform intensity inside spot
    I_uniform = power / (np.pi * spot_radius**2)
    
    # Apply circular mask
    mask = r_squared <= spot_radius**2
    return np.where(mask, I_uniform, 0.0)


def intensity_at_point(
    x: float,
    y: float,
    center_x: float,
    center_y: float,
    z: float,
    laser: LaserConfig
) -> float:
    """
    Compute intensity at a single point considering defocus.
    
    Args:
        x, y: Point coordinates [m]
        center_x, center_y: Beam center position [m]
        z: Distance from focal plane [m]
        laser: Laser configuration
        
    Returns:
        Intensity at point [W/m²]
    """
    # Get defocused spot size
    w = compute_spot_radius(z, laser)
    
    # Compute intensity based on beam mode
    if laser.mode == 'TEM00':
        r_sq = (x - center_x)**2 + (y - center_y)**2
        I_peak = 2 * laser.power / (np.pi * w**2)
        return I_peak * np.exp(-2 * r_sq / w**2)
    elif laser.mode == 'tophat':
        r_sq = (x - center_x)**2 + (y - center_y)**2
        if r_sq <= w**2:
            return laser.power / (np.pi * w**2)
        return 0.0
    else:
        raise ValueError(f"Unknown beam mode: {laser.mode}")


# Common laser presets
def nd_yag_laser(power: float, spot_radius: float = 50e-6) -> LaserConfig:
    """Create Nd:YAG laser configuration (1064 nm)."""
    return LaserConfig(
        power=power,
        wavelength=1.064e-6,
        base_spot_radius=spot_radius,
        focal_length=0.1  # 100 mm typical
    )


def co2_laser(power: float, spot_radius: float = 100e-6) -> LaserConfig:
    """Create CO2 laser configuration (10.6 µm)."""
    return LaserConfig(
        power=power,
        wavelength=10.6e-6,
        base_spot_radius=spot_radius,
        focal_length=0.127  # 5 inch typical
    )


def fiber_laser(power: float, spot_radius: float = 25e-6) -> LaserConfig:
    """Create fiber laser configuration (1070 nm)."""
    return LaserConfig(
        power=power,
        wavelength=1.07e-6,
        base_spot_radius=spot_radius,
        focal_length=0.16  # 160 mm typical
    )
