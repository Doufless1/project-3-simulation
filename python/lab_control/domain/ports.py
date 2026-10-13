"""
Domain Ports — Abstract interfaces (ABCs) for lab control infrastructure.

Dependency Inversion Principle:
  Application and Domain layers depend ONLY on these abstractions.
  Infrastructure provides concrete implementations.

Interface Segregation Principle:
  Each port has a single, focused responsibility.
"""

from abc import ABC, abstractmethod
from typing import List, Optional

from lab_control.domain.entities import (
    XYTable,
    LaserUnit,
    GasSystem,
    SafetySystem,
)
from lab_control.domain.value_objects import (
    Position2D,
    GCodeLine,
    ScanRecipe,
    EquipmentSpec,
)


# ── Table Controller Port ─────────────────────────────────────────────

class ITableController(ABC):
    """
    Strategy: Interface for X-Y table motion control.

    Implementations: VirtualTableController (simulation),
                     GrblTableController (real hardware — future).
    """

    @abstractmethod
    def home(self, table: XYTable) -> None:
        """Home both axes."""

    @abstractmethod
    def move_to(self, table: XYTable, target: Position2D) -> None:
        """Move to absolute position."""

    @abstractmethod
    def execute_scan(
        self, table: XYTable, recipe: ScanRecipe
    ) -> List[Position2D]:
        """
        Execute a full scan pattern and return the path taken.

        Returns:
            List of Position2D points traced during the scan.
        """

    @abstractmethod
    def generate_gcode(self, recipe: ScanRecipe) -> List[GCodeLine]:
        """
        Generate G-code from a scan recipe.

        Returns:
            List of GCodeLine instructions.
        """


# ── Laser Controller Port ─────────────────────────────────────────────

class ILaserController(ABC):
    """Strategy: Interface for laser control."""

    @abstractmethod
    def set_power(self, laser: LaserUnit, power_w: float) -> None:
        """Set laser output power."""

    @abstractmethod
    def arm(self, laser: LaserUnit, safety: SafetySystem) -> None:
        """Arm the laser (enable emission on trigger)."""

    @abstractmethod
    def fire(self, laser: LaserUnit, safety: SafetySystem) -> None:
        """Start laser emission."""

    @abstractmethod
    def stop(self, laser: LaserUnit) -> None:
        """Stop laser emission."""


# ── Gas Controller Port ───────────────────────────────────────────────

class IGasController(ABC):
    """Strategy: Interface for shielding gas control."""

    @abstractmethod
    def set_flow(self, gas: GasSystem, flow_l_min: float) -> None:
        """Set gas flow rate."""

    @abstractmethod
    def start_purge(self, gas: GasSystem) -> None:
        """Begin chamber purge sequence."""

    @abstractmethod
    def stop(self, gas: GasSystem) -> None:
        """Close gas supply."""

    @abstractmethod
    def read_o2(self, gas: GasSystem) -> float:
        """Read current O2 level in ppm."""


# ── Safety Monitor Port ──────────────────────────────────────────────

class ISafetyMonitor(ABC):
    """Strategy: Interface for safety system monitoring."""

    @abstractmethod
    def check_all(self, safety: SafetySystem) -> dict:
        """Return full safety status dictionary."""

    @abstractmethod
    def trigger_e_stop(self, safety: SafetySystem) -> None:
        """Trigger emergency stop."""

    @abstractmethod
    def reset_e_stop(self, safety: SafetySystem) -> None:
        """Reset emergency stop (manual action required)."""


# ── Cost Calculator Port ─────────────────────────────────────────────

class ICostCalculator(ABC):
    """Repository: Interface for equipment cost estimation."""

    @abstractmethod
    def get_catalog(self) -> List[EquipmentSpec]:
        """Return all available equipment."""

    @abstractmethod
    def calculate_total(
        self, selected_items: List[EquipmentSpec]
    ) -> float:
        """Calculate total cost of selected equipment."""

    @abstractmethod
    def get_by_category(self, category: str) -> List[EquipmentSpec]:
        """Filter equipment by category."""


# ── Audit Logger Port (STRIDE: Repudiation Defense) ──────────────────

class IAuditLogger(ABC):
    """
    STRIDE Mitigation: Prevents repudiation by recording
    all lab operations with timestamps and parameters.
    """

    @abstractmethod
    def log_action(
        self, subsystem: str, action: str, details: dict
    ) -> str:
        """
        Log a lab action.

        Returns:
            Unique event ID for correlation.
        """

    @abstractmethod
    def log_safety_event(
        self, event_type: str, details: dict
    ) -> str:
        """Log a safety-critical event (higher priority)."""

    @abstractmethod
    def get_recent_logs(self, count: int = 50) -> List[dict]:
        """Retrieve recent log entries."""
