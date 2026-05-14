"""
Value Objects — Immutable, self-validating data containers.

CIA Triad:
  - Integrity: Every value is validated on construction.
  - Confidentiality: Frozen dataclasses prevent post-creation tampering.
"""

from dataclasses import dataclass
from typing import List


# ── 2D Position ───────────────────────────────────────────────────────

@dataclass(frozen=True)
class Position2D:
    """Immutable X-Y coordinate in millimetres."""

    x_mm: float
    y_mm: float

    def __post_init__(self):
        if not isinstance(self.x_mm, (int, float)):
            raise TypeError(f"x_mm must be numeric, got {type(self.x_mm)}")
        if not isinstance(self.y_mm, (int, float)):
            raise TypeError(f"y_mm must be numeric, got {type(self.y_mm)}")

    def distance_to(self, other: "Position2D") -> float:
        """Euclidean distance to another 2D point [mm]."""
        return ((self.x_mm - other.x_mm) ** 2
                + (self.y_mm - other.y_mm) ** 2) ** 0.5


# ── Speed ─────────────────────────────────────────────────────────────

@dataclass(frozen=True)
class Speed:
    """Validated scan speed in mm/s."""

    value_mm_per_s: float

    def __post_init__(self):
        if self.value_mm_per_s < 0:
            raise ValueError(
                f"Speed cannot be negative: {self.value_mm_per_s} mm/s"
            )

    @property
    def is_zero(self) -> bool:
        return self.value_mm_per_s == 0.0


# ── Flow Rate ─────────────────────────────────────────────────────────

@dataclass(frozen=True)
class FlowRate:
    """Validated gas flow rate in L/min."""

    value_l_per_min: float

    def __post_init__(self):
        if self.value_l_per_min < 0:
            raise ValueError(
                f"Flow rate cannot be negative: {self.value_l_per_min} L/min"
            )


# ── G-Code Instruction ───────────────────────────────────────────────

@dataclass(frozen=True)
class GCodeLine:
    """Single validated G-code instruction."""

    code: str
    line_number: int

    def __post_init__(self):
        if not self.code.strip():
            raise ValueError("G-code line cannot be empty.")
        if self.line_number < 0:
            raise ValueError("Line number must be non-negative.")

    def __str__(self) -> str:
        return f"N{self.line_number} {self.code}"


# ── Scan Recipe ───────────────────────────────────────────────────────

@dataclass(frozen=True)
class ScanRecipe:
    """
    Immutable scan parameters recipe.

    Integrity: All values validated on construction.
    """

    pattern: str          # "raster", "spiral", "linear"
    speed_mm_s: float     # Scan speed
    power_w: float        # Laser power
    spot_mm: float        # Spot diameter
    overlap_pct: float    # Overlap percentage (0–100)
    width_mm: float       # Scan area width
    height_mm: float      # Scan area height

    def __post_init__(self):
        valid_patterns = ("raster", "spiral", "linear")
        if self.pattern not in valid_patterns:
            raise ValueError(
                f"Unknown pattern '{self.pattern}', "
                f"must be one of {valid_patterns}"
            )
        if self.speed_mm_s <= 0:
            raise ValueError("Speed must be positive.")
        if self.power_w <= 0:
            raise ValueError("Power must be positive.")
        if self.spot_mm <= 0:
            raise ValueError("Spot diameter must be positive.")
        if not (0 <= self.overlap_pct <= 99):
            raise ValueError("Overlap must be 0–99%.")
        if self.width_mm <= 0 or self.height_mm <= 0:
            raise ValueError("Scan area dimensions must be positive.")

    @property
    def line_spacing_mm(self) -> float:
        """Distance between scan lines based on spot and overlap."""
        return self.spot_mm * (1.0 - self.overlap_pct / 100.0)


# ── Equipment Spec ────────────────────────────────────────────────────

@dataclass(frozen=True)
class EquipmentSpec:
    """Specification for a single piece of lab equipment."""

    category: str          # e.g. "laser", "stage", "chamber"
    name: str              # e.g. "IPG YLR-1000"
    description: str       # Short description
    unit_cost_eur: float   # Unit cost in euros
    quantity: int = 1      # Number needed

    def __post_init__(self):
        if self.unit_cost_eur < 0:
            raise ValueError("Cost cannot be negative.")
        if self.quantity < 1:
            raise ValueError("Quantity must be at least 1.")

    @property
    def total_cost_eur(self) -> float:
        return self.unit_cost_eur * self.quantity
