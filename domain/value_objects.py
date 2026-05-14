"""
Value Objects — Immutable, self-validating data containers.

These objects enforce Integrity (CIA Triad) by validating on construction
and being immutable (frozen=True) to prevent tampering.

- Position3D: A point in 3D space [meters]
- Temperature: A validated temperature value [°C]
- Composition: A normalized element-to-fraction mapping
"""

from dataclasses import dataclass, field
from typing import Dict


@dataclass(frozen=True)
class Position3D:
    """Immutable 3D coordinate in meters."""

    x: float
    y: float
    z: float

    def distance_to(self, other: "Position3D") -> float:
        """Euclidean distance to another point."""
        return (
            (self.x - other.x) ** 2
            + (self.y - other.y) ** 2
            + (self.z - other.z) ** 2
        ) ** 0.5


@dataclass(frozen=True)
class Temperature:
    """
    Validated temperature value in Celsius.

    Enforces physical lower bound (absolute zero = -273.15 °C).
    """

    value_celsius: float

    def __post_init__(self):
        if self.value_celsius < -273.15:
            raise ValueError(
                f"Temperature {self.value_celsius}°C is below absolute zero."
            )

    @property
    def kelvin(self) -> float:
        """Convert to Kelvin."""
        return self.value_celsius + 273.15

    def exceeds(self, threshold_celsius: float) -> bool:
        """Check if this temperature exceeds a threshold."""
        return self.value_celsius > threshold_celsius


@dataclass(frozen=True)
class Composition:
    """
    Normalized element-to-mass-fraction mapping.

    Integrity: Validates that fractions sum to ~1.0 and are non-negative.
    Immutable to prevent post-creation tampering.
    """

    elements: Dict[str, float] = field(default_factory=dict)

    def __post_init__(self):
        # Validate non-negative fractions
        for symbol, fraction in self.elements.items():
            if fraction < 0:
                raise ValueError(
                    f"Negative fraction {fraction} for element '{symbol}'."
                )

        # Normalize if not already ~1.0
        total = sum(self.elements.values())
        if total <= 0:
            raise ValueError("Composition must have at least one element.")

        if not (0.99 <= total <= 1.01):
            # Auto-normalize (use object.__setattr__ since frozen)
            normalized = {k: v / total for k, v in self.elements.items()}
            object.__setattr__(self, "elements", normalized)

    @property
    def dominant_elements(self) -> list:
        """Return elements with fraction > 10%, sorted descending."""
        return sorted(
            [(sym, frac) for sym, frac in self.elements.items() if frac > 0.10],
            key=lambda x: x[1],
            reverse=True,
        )
