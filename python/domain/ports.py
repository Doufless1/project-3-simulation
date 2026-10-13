"""
Domain Ports — Abstract interfaces (ABCs) for the simulation engine.

These define the contracts that Infrastructure must fulfill.
Use cases depend ONLY on these abstractions (Dependency Inversion Principle).

Interfaces follow the Interface Segregation Principle:
each port has a single, focused responsibility.
"""

from abc import ABC, abstractmethod
from typing import List, Optional

from domain.entities import (
    Material,
    LaserBeam,
    Trajectory,
    SimulationResult,
)


# ============================================================================
# Heat Solver Port (Strategy Pattern)
# ============================================================================

class IHeatSolver(ABC):
    """
    Abstract interface for thermal solvers.

    Implementations: FDMHeatSolver3D, AnalyticalHeatSolver
    Pattern: Strategy — swap solvers without changing use case code.
    """

    @abstractmethod
    def solve(
        self,
        material: Material,
        laser: LaserBeam,
        trajectory: Trajectory,
        grid_size: tuple,
        resolution: float,
    ) -> SimulationResult:
        """
        Run the thermal simulation and return results.

        Args:
            material: Target material properties.
            laser: Laser beam configuration.
            trajectory: Motion path of the laser.
            grid_size: (width_m, height_m, depth_m) of sim domain.
            resolution: Grid cell size [m].

        Returns:
            SimulationResult with temperature field and metrics.
        """

    @property
    @abstractmethod
    def name(self) -> str:
        """Human-readable solver name for audit logging."""


# ============================================================================
# Laser Source Port (Strategy Pattern)
# ============================================================================

class ILaserSource(ABC):
    """
    Abstract interface for beam intensity calculation.

    Implementations: GaussianLaserSource, TopHatLaserSource
    Pattern: Strategy — different beam profiles are interchangeable.
    """

    @abstractmethod
    def compute_intensity(
        self,
        x_grid,
        y_grid,
        center_x: float,
        center_y: float,
        spot_radius: float,
        power: float,
    ):
        """
        Compute 2D intensity distribution on the surface grid.

        Args:
            x_grid: X-coordinate mesh (numpy array).
            y_grid: Y-coordinate mesh (numpy array).
            center_x: Beam center X position [m].
            center_y: Beam center Y position [m].
            spot_radius: Current spot radius w [m].
            power: Laser power [W].

        Returns:
            2D intensity array [W/m²].
        """


# ============================================================================
# Motion Generator Port (Strategy Pattern)
# ============================================================================

class IMotionGenerator(ABC):
    """
    Abstract interface for trajectory generation.

    Implementations: RasterGenerator, SpiralGenerator, LinearGenerator
    """

    @abstractmethod
    def generate(self, **params) -> Trajectory:
        """
        Generate a motion trajectory from parameters.

        Returns:
            Trajectory entity with time/x/y/z arrays.
        """


# ============================================================================
# Material Repository Port (Repository Pattern)
# ============================================================================

class IMaterialRepository(ABC):
    """
    Abstract interface for material persistence.

    Implementations: JsonMaterialRepository, MaterialsProjectAdapter
    Pattern: Repository — abstracts storage mechanism.
    """

    @abstractmethod
    def save(self, material_data: dict) -> None:
        """Persist a material record."""

    @abstractmethod
    def load_all(self) -> List[dict]:
        """Load all stored material records."""

    @abstractmethod
    def find_best(self, n: int = 5) -> List[dict]:
        """Return top N materials by fitness score."""


# ============================================================================
# Audit Logger Port (STRIDE: Repudiation Defense)
# ============================================================================

class IAuditLogger(ABC):
    """
    Abstract interface for simulation audit logging.

    STRIDE Mitigation: Prevents repudiation by recording
    all simulation runs with timestamps and parameters.
    """

    @abstractmethod
    def log_simulation_start(
        self, material_name: str, solver_name: str, params: dict
    ) -> str:
        """
        Log the start of a simulation run.

        Returns:
            A unique run ID for correlation.
        """

    @abstractmethod
    def log_simulation_end(
        self, run_id: str, result_summary: dict
    ) -> None:
        """Log the completion of a simulation run."""

    @abstractmethod
    def log_error(self, run_id: str, error: str) -> None:
        """Log an error during a simulation run."""
