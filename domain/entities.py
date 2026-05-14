"""
Domain Entities — Core business objects of the simulation.

Entities encapsulate validation and core domain logic.
They depend ONLY on value_objects and exceptions from this layer.

Design Patterns:
- SRP: Each entity validates and manages only its own state.
- OCP: Properties are computed, new derived properties can be added
       via subclassing without modifying existing code.
"""

import math
from dataclasses import dataclass, field
from typing import Optional, List

from domain.exceptions import (
    InvalidMaterialError,
    InvalidLaserConfigError,
    InvalidTrajectoryError,
)


# ============================================================================
# Material Entity
# ============================================================================

@dataclass
class Material:
    """
    Thermal and optical properties of a target material.

    Validates all physical constraints on construction.
    """

    name: str

    # Optical
    absorption: float  # Fraction absorbed (0.0–1.0)

    # Thermal
    thermal_conductivity: float   # k [W/(m·K)]
    density: float                # ρ [kg/m³]
    specific_heat: float          # c_p [J/(kg·K)]

    # Phase change temperatures [°C]
    t_ambient: float = 20.0
    t_melt: float = 1400.0
    t_vaporization: float = 2800.0

    def __post_init__(self):
        self._validate()

    def _validate(self):
        """Enforce physical constraints (Integrity — CIA Triad)."""
        if not (0.0 < self.absorption <= 1.0):
            raise InvalidMaterialError(
                f"Absorption must be in (0, 1], got {self.absorption}"
            )
        if self.thermal_conductivity <= 0:
            raise InvalidMaterialError("Thermal conductivity must be positive.")
        if self.density <= 0:
            raise InvalidMaterialError("Density must be positive.")
        if self.specific_heat <= 0:
            raise InvalidMaterialError("Specific heat must be positive.")
        if self.t_melt >= self.t_vaporization:
            raise InvalidMaterialError(
                f"Melt temp ({self.t_melt}°C) must be below "
                f"vaporization temp ({self.t_vaporization}°C)."
            )

    @property
    def thermal_diffusivity(self) -> float:
        """κ = k / (ρ · c_p) [m²/s]."""
        return self.thermal_conductivity / (self.density * self.specific_heat)


# ============================================================================
# Laser Beam Entity
# ============================================================================

@dataclass
class LaserBeam:
    """
    Laser beam configuration with physics-derived properties.

    Validates parameters on construction.
    """

    power: float            # [W]
    wavelength: float       # [m]
    spot_radius: float      # w0 at focus [m]
    focal_length: float     # [m]

    def __post_init__(self):
        self._validate()

    def _validate(self):
        """Enforce physical constraints."""
        if self.power <= 0:
            raise InvalidLaserConfigError("Power must be positive.")
        if self.wavelength <= 0:
            raise InvalidLaserConfigError("Wavelength must be positive.")
        if self.spot_radius <= 0:
            raise InvalidLaserConfigError("Spot radius must be positive.")
        if self.focal_length <= 0:
            raise InvalidLaserConfigError("Focal length must be positive.")

    @property
    def rayleigh_range(self) -> float:
        """z_R = π · w0² / λ [m]."""
        return math.pi * self.spot_radius ** 2 / self.wavelength

    @property
    def peak_intensity(self) -> float:
        """I_0 = 2P / (π · w0²) [W/m²]."""
        return 2 * self.power / (math.pi * self.spot_radius ** 2)

    @property
    def divergence_angle(self) -> float:
        """θ = λ / (π · w0) [rad]."""
        return self.wavelength / (math.pi * self.spot_radius)


# ============================================================================
# Trajectory Entity
# ============================================================================

@dataclass
class Trajectory:
    """
    Motion path data for laser scanning.

    Stores parallel arrays of time, x, y, z coordinates.
    Validates consistency on construction.
    """

    time: list = field(default_factory=list)
    x: list = field(default_factory=list)
    y: list = field(default_factory=list)
    z: list = field(default_factory=list)

    def __post_init__(self):
        self._validate()

    def _validate(self):
        lengths = {len(self.time), len(self.x), len(self.y), len(self.z)}
        if len(lengths) != 1:
            raise InvalidTrajectoryError(
                "All trajectory arrays must have the same length."
            )
        if len(self.time) < 2:
            raise InvalidTrajectoryError(
                "Trajectory must have at least 2 points."
            )

    @property
    def n_points(self) -> int:
        return len(self.time)

    @property
    def duration(self) -> float:
        return self.time[-1] - self.time[0]


# ============================================================================
# Simulation Result Entity
# ============================================================================

@dataclass
class SimulationResult:
    """
    Immutable container for simulation output.

    Stores the 3D temperature field, fluence map, and derived metrics.
    """

    # Raw data (stored as nested lists for domain purity — no numpy dependency)
    temperature_field: list = field(default_factory=list)
    fluence_map: list = field(default_factory=list)
    x_coords: list = field(default_factory=list)
    y_coords: list = field(default_factory=list)
    z_coords: list = field(default_factory=list)
    time_coords: list = field(default_factory=list)

    # Derived metrics
    peak_temperature_celsius: float = 0.0
    peak_fluence_j_per_m2: float = 0.0
    melt_depth_m: Optional[float] = None
    vaporization_depth_m: Optional[float] = None
    total_energy_j: float = 0.0

    # Metadata
    material_name: str = ""
    solver_name: str = ""
    duration_seconds: float = 0.0
