"""
Domain Entities — Core business objects with validation and state.

Entities encapsulate validation and core domain logic.
They depend ONLY on value_objects and exceptions from this layer.

Design Patterns:
  - SRP: Each entity manages only its own state.
  - OCP: Computed properties can be extended via subclassing.

CIA Triad:
  - Integrity: All state changes validated.
  - Availability: State machine prevents invalid transitions.
"""

from dataclasses import dataclass, field
from enum import Enum, auto
from typing import List, Optional, Tuple

from lab_control.domain.exceptions import (
    TableLimitError,
    TableBusyError,
    TableNotHomedError,
    LaserPowerError,
    LaserInterlockError,
    GasFlowError,
    SafetyViolationError,
)
from lab_control.domain.value_objects import Position2D, FlowRate


# ── Enums ─────────────────────────────────────────────────────────────

class TableState(Enum):
    """X-Y table finite state machine."""
    IDLE = auto()
    HOMING = auto()
    MOVING = auto()
    SCANNING = auto()
    ERROR = auto()


class LaserState(Enum):
    """Laser finite state machine."""
    OFF = auto()
    STANDBY = auto()
    ARMED = auto()
    FIRING = auto()
    ERROR = auto()


class GasState(Enum):
    """Gas system finite state machine."""
    CLOSED = auto()
    PURGING = auto()
    FLOWING = auto()
    ERROR = auto()


class InterlockStatus(Enum):
    """Safety interlock status."""
    LOCKED = auto()      # Door closed, safe to operate
    UNLOCKED = auto()    # Door open, laser disabled


# ── X-Y Table Entity ─────────────────────────────────────────────────

@dataclass
class XYTable:
    """
    Motorized X-Y positioning table.

    Validates travel limits and enforces state machine transitions.
    """

    travel_x_mm: float = 300.0
    travel_y_mm: float = 300.0
    resolution_um: float = 1.0
    max_speed_mm_s: float = 250.0

    # Internal state — not constructor args
    position: Position2D = field(default_factory=lambda: Position2D(0.0, 0.0))
    state: TableState = field(default=TableState.IDLE)
    is_homed: bool = field(default=False)

    def validate_position(self, target: Position2D) -> None:
        """Integrity: Reject moves outside travel range."""
        if target.x_mm < 0 or target.x_mm > self.travel_x_mm:
            raise TableLimitError(
                f"X={target.x_mm:.2f} mm outside range [0, {self.travel_x_mm}]"
            )
        if target.y_mm < 0 or target.y_mm > self.travel_y_mm:
            raise TableLimitError(
                f"Y={target.y_mm:.2f} mm outside range [0, {self.travel_y_mm}]"
            )

    def validate_ready(self) -> None:
        """Integrity: Table must be homed and idle before moving."""
        if not self.is_homed:
            raise TableNotHomedError("Table must be homed first.")
        if self.state not in (TableState.IDLE, TableState.SCANNING):
            raise TableBusyError(
                f"Cannot move while in state: {self.state.name}"
            )

    def move_to(self, target: Position2D) -> None:
        """Move to target, enforcing all safety constraints."""
        self.validate_ready()
        self.validate_position(target)
        self.state = TableState.MOVING
        self.position = target
        self.state = TableState.IDLE

    def home(self) -> None:
        """Home both axes to origin (0, 0)."""
        self.state = TableState.HOMING
        self.position = Position2D(0.0, 0.0)
        self.is_homed = True
        self.state = TableState.IDLE

    @property
    def progress_x_pct(self) -> float:
        """Current X position as percentage of travel."""
        return (self.position.x_mm / self.travel_x_mm) * 100.0

    @property
    def progress_y_pct(self) -> float:
        """Current Y position as percentage of travel."""
        return (self.position.y_mm / self.travel_y_mm) * 100.0


# ── Laser Unit Entity ─────────────────────────────────────────────────

@dataclass
class LaserUnit:
    """
    Fiber laser source with power control.

    STRIDE: LaserInterlockError prevents firing when safety is compromised.
    """

    max_power_w: float = 1000.0
    wavelength_nm: float = 1070.0
    min_power_w: float = 50.0

    # Internal state
    current_power_w: float = field(default=0.0)
    state: LaserState = field(default=LaserState.OFF)

    def set_power(self, power_w: float) -> None:
        """Integrity: Validate power then apply."""
        if power_w < 0 or power_w > self.max_power_w:
            raise LaserPowerError(
                f"Power {power_w} W outside range [0, {self.max_power_w}]"
            )
        self.current_power_w = power_w

    def arm(self, interlock: "SafetySystem") -> None:
        """STRIDE: Only arm if all interlocks are satisfied."""
        if not interlock.all_interlocks_locked:
            raise LaserInterlockError(
                "Cannot arm laser — safety interlocks are open."
            )
        self.state = LaserState.ARMED

    def fire(self, interlock: "SafetySystem") -> None:
        """Fire the laser (set state to FIRING)."""
        if self.state != LaserState.ARMED:
            raise LaserInterlockError("Laser must be armed before firing.")
        if not interlock.all_interlocks_locked:
            raise LaserInterlockError(
                "Interlocks opened — firing aborted."
            )
        if self.current_power_w < self.min_power_w:
            raise LaserPowerError(
                f"Power {self.current_power_w} W below minimum {self.min_power_w} W."
            )
        self.state = LaserState.FIRING

    def stop(self) -> None:
        """Immediately stop the laser."""
        self.state = LaserState.STANDBY
        self.current_power_w = 0.0

    def turn_off(self) -> None:
        """Fully power down."""
        self.current_power_w = 0.0
        self.state = LaserState.OFF

    @property
    def power_pct(self) -> float:
        """Current power as percentage of maximum."""
        if self.max_power_w <= 0:
            return 0.0
        return (self.current_power_w / self.max_power_w) * 100.0


# ── Gas System Entity ─────────────────────────────────────────────────

@dataclass
class GasSystem:
    """
    Controlled atmosphere system (Argon shielding gas).

    CIA: Validates flow range, monitors supply pressure.
    """

    max_flow_l_min: float = 20.0
    min_flow_l_min: float = 0.0

    # Internal state
    current_flow: FlowRate = field(
        default_factory=lambda: FlowRate(0.0)
    )
    state: GasState = field(default=GasState.CLOSED)
    supply_pressure_bar: float = field(default=200.0)
    chamber_o2_ppm: float = field(default=209500.0)  # Atmospheric O2

    def set_flow(self, flow_l_min: float) -> None:
        """Integrity: Validate then set gas flow."""
        if flow_l_min < self.min_flow_l_min:
            raise GasFlowError(
                f"Flow {flow_l_min} L/min below minimum {self.min_flow_l_min}"
            )
        if flow_l_min > self.max_flow_l_min:
            raise GasFlowError(
                f"Flow {flow_l_min} L/min above maximum {self.max_flow_l_min}"
            )
        self.current_flow = FlowRate(flow_l_min)
        self.state = GasState.FLOWING if flow_l_min > 0 else GasState.CLOSED

    def start_purge(self, flow_l_min: float = 15.0) -> None:
        """Begin chamber purge at specified flow rate."""
        self.set_flow(flow_l_min)
        self.state = GasState.PURGING

    def stop(self) -> None:
        """Close gas supply."""
        self.current_flow = FlowRate(0.0)
        self.state = GasState.CLOSED

    @property
    def is_atmosphere_safe(self) -> bool:
        """Check if O2 level is below 100 ppm threshold."""
        return self.chamber_o2_ppm < 100.0

    @property
    def supply_level_pct(self) -> float:
        """Remaining supply as percentage (200 bar = 100%)."""
        return min((self.supply_pressure_bar / 200.0) * 100.0, 100.0)


# ── Safety System Entity ─────────────────────────────────────────────

@dataclass
class SafetySystem:
    """
    Laboratory safety interlock system.

    STRIDE defense:
      - Spoofing: Interlock states are read-only from domain.
      - Tampering: State changes go through validated methods.
      - Repudiation: All changes are logged via audit port.
    """

    door_interlock: InterlockStatus = field(
        default=InterlockStatus.UNLOCKED
    )
    chamber_interlock: InterlockStatus = field(
        default=InterlockStatus.UNLOCKED
    )
    e_stop_pressed: bool = field(default=False)
    laser_warning_active: bool = field(default=False)

    @property
    def all_interlocks_locked(self) -> bool:
        """All conditions must be met for laser operation."""
        return (
            self.door_interlock == InterlockStatus.LOCKED
            and self.chamber_interlock == InterlockStatus.LOCKED
            and not self.e_stop_pressed
        )

    def lock_door(self) -> None:
        self.door_interlock = InterlockStatus.LOCKED

    def unlock_door(self) -> None:
        self.door_interlock = InterlockStatus.UNLOCKED

    def lock_chamber(self) -> None:
        self.chamber_interlock = InterlockStatus.LOCKED

    def unlock_chamber(self) -> None:
        self.chamber_interlock = InterlockStatus.UNLOCKED

    def press_e_stop(self) -> None:
        """Emergency stop — latching, requires manual reset."""
        self.e_stop_pressed = True

    def reset_e_stop(self) -> None:
        """Manual reset of emergency stop."""
        self.e_stop_pressed = False

    def activate_warning(self) -> None:
        self.laser_warning_active = True

    def deactivate_warning(self) -> None:
        self.laser_warning_active = False

    @property
    def status_summary(self) -> dict:
        """Return a dict summary of all safety states."""
        return {
            "door": self.door_interlock.name,
            "chamber": self.chamber_interlock.name,
            "e_stop": self.e_stop_pressed,
            "warning_light": self.laser_warning_active,
            "all_clear": self.all_interlocks_locked,
        }
