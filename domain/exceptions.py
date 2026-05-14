"""
Domain Exceptions — Custom error types for the simulation engine.

Each exception has a single responsibility:
- InvalidMaterialError: Material property validation failures
- SolverInstabilityError: Numerical solver exceeded stability limits
- GridTooLargeError: Requested grid exceeds safe memory bounds
- InvalidTrajectoryError: Motion path validation failures
- InvalidLaserConfigError: Laser parameter validation failures
"""


class DomainError(Exception):
    """Base exception for all domain errors."""


class InvalidMaterialError(DomainError):
    """Raised when material properties fail validation."""


class SolverInstabilityError(DomainError):
    """Raised when the numerical solver exceeds stability criteria."""


class GridTooLargeError(DomainError):
    """Raised when the requested simulation grid exceeds safe limits."""

    def __init__(self, requested_cells: int, max_cells: int):
        self.requested_cells = requested_cells
        self.max_cells = max_cells
        super().__init__(
            f"Grid too large: {requested_cells:,} cells requested, "
            f"max allowed is {max_cells:,}."
        )


class InvalidTrajectoryError(DomainError):
    """Raised when trajectory arrays are inconsistent."""


class InvalidLaserConfigError(DomainError):
    """Raised when laser parameters fail validation."""
