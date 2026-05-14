"""
Lab Control Use Case — Orchestrates the entire virtual lab.

Dependency Injection: All controllers injected via constructor.
Single Responsibility: Coordinates subsystems, delegates details.
Audit: Every action is logged for STRIDE repudiation defense.
"""

from typing import List, Optional

from lab_control.domain.entities import (
    XYTable,
    LaserUnit,
    GasSystem,
    SafetySystem,
    TableState,
    LaserState,
)
from lab_control.domain.value_objects import Position2D, GCodeLine, ScanRecipe
from lab_control.domain.ports import (
    ITableController,
    ILaserController,
    IGasController,
    ISafetyMonitor,
    IAuditLogger,
)
from lab_control.domain.exceptions import (
    SafetyViolationError,
    EmergencyStopError,
)


class LabControlUseCase:
    """
    Application service: coordinates all lab subsystems.

    Injected Dependencies (DI):
      - table_ctrl: ITableController
      - laser_ctrl: ILaserController
      - gas_ctrl:   IGasController
      - safety_mon: ISafetyMonitor
      - audit:      IAuditLogger

    This is the ONLY class the presentation layer talks to.
    """

    def __init__(
        self,
        table: XYTable,
        laser: LaserUnit,
        gas: GasSystem,
        safety: SafetySystem,
        table_ctrl: ITableController,
        laser_ctrl: ILaserController,
        gas_ctrl: IGasController,
        safety_mon: ISafetyMonitor,
        audit: IAuditLogger,
    ):
        self._table = table
        self._laser = laser
        self._gas = gas
        self._safety = safety
        self._table_ctrl = table_ctrl
        self._laser_ctrl = laser_ctrl
        self._gas_ctrl = gas_ctrl
        self._safety_mon = safety_mon
        self._audit = audit

    # ── Table Operations ──────────────────────────────────────────────

    def home_table(self) -> dict:
        """Home X-Y table and log the action."""
        self._table_ctrl.home(self._table)
        self._audit.log_action("TABLE", "HOME", {
            "position": f"({self._table.position.x_mm}, "
                        f"{self._table.position.y_mm})"
        })
        return {"status": "homed", "position": (0.0, 0.0)}

    def jog_table(self, x_mm: float, y_mm: float) -> dict:
        """Move table to absolute position."""
        target = Position2D(x_mm, y_mm)
        self._table_ctrl.move_to(self._table, target)
        self._audit.log_action("TABLE", "JOG", {
            "target_x": x_mm, "target_y": y_mm
        })
        return {
            "status": "moved",
            "position": (self._table.position.x_mm,
                         self._table.position.y_mm),
        }

    def run_scan(self, recipe: ScanRecipe) -> dict:
        """Execute full scan pattern and return path + stats."""
        self._ensure_safe_for_operation()
        path = self._table_ctrl.execute_scan(self._table, recipe)
        self._audit.log_action("TABLE", "SCAN_COMPLETE", {
            "pattern": recipe.pattern,
            "points": len(path),
            "speed": recipe.speed_mm_s,
        })
        return {
            "status": "scan_complete",
            "pattern": recipe.pattern,
            "total_points": len(path),
            "path": path,
        }

    def generate_gcode(self, recipe: ScanRecipe) -> List[GCodeLine]:
        """Generate G-code from recipe without moving."""
        gcode = self._table_ctrl.generate_gcode(recipe)
        self._audit.log_action("TABLE", "GCODE_GENERATED", {
            "pattern": recipe.pattern,
            "lines": len(gcode),
        })
        return gcode

    # ── Laser Operations ──────────────────────────────────────────────

    def set_laser_power(self, power_w: float) -> dict:
        """Set laser power."""
        self._laser_ctrl.set_power(self._laser, power_w)
        self._audit.log_action("LASER", "SET_POWER", {
            "power_w": power_w,
            "pct": self._laser.power_pct,
        })
        return {"power_w": power_w, "pct": self._laser.power_pct}

    def arm_laser(self) -> dict:
        """Arm laser — requires all interlocks locked."""
        self._laser_ctrl.arm(self._laser, self._safety)
        self._safety.activate_warning()
        self._audit.log_action("LASER", "ARMED", {
            "power_w": self._laser.current_power_w
        })
        return {"state": self._laser.state.name}

    def fire_laser(self) -> dict:
        """Fire laser — requires armed state + interlocks."""
        self._laser_ctrl.fire(self._laser, self._safety)
        self._audit.log_action("LASER", "FIRING", {
            "power_w": self._laser.current_power_w
        })
        return {"state": self._laser.state.name}

    def stop_laser(self) -> dict:
        """Stop laser emission."""
        self._laser_ctrl.stop(self._laser)
        self._safety.deactivate_warning()
        self._audit.log_action("LASER", "STOPPED", {})
        return {"state": self._laser.state.name}

    # ── Gas Operations ────────────────────────────────────────────────

    def set_gas_flow(self, flow_l_min: float) -> dict:
        """Set argon flow rate."""
        self._gas_ctrl.set_flow(self._gas, flow_l_min)
        self._audit.log_action("GAS", "SET_FLOW", {
            "flow_l_min": flow_l_min
        })
        return {
            "flow_l_min": flow_l_min,
            "state": self._gas.state.name,
        }

    def start_purge(self) -> dict:
        """Start chamber purge sequence."""
        self._gas_ctrl.start_purge(self._gas)
        self._audit.log_action("GAS", "PURGE_STARTED", {})
        return {"state": self._gas.state.name}

    def read_o2(self) -> float:
        """Read chamber O2 level."""
        return self._gas_ctrl.read_o2(self._gas)

    # ── Safety Operations ─────────────────────────────────────────────

    def get_safety_status(self) -> dict:
        """Get full safety status."""
        return self._safety_mon.check_all(self._safety)

    def set_door_interlock(self, locked: bool) -> dict:
        """Set door interlock state."""
        if locked:
            self._safety.lock_door()
        else:
            self._safety.unlock_door()
            self._auto_stop_laser_if_firing()
        self._audit.log_safety_event("DOOR_INTERLOCK", {
            "locked": locked
        })
        return self._safety_mon.check_all(self._safety)

    def set_chamber_interlock(self, locked: bool) -> dict:
        """Set chamber interlock state."""
        if locked:
            self._safety.lock_chamber()
        else:
            self._safety.unlock_chamber()
            self._auto_stop_laser_if_firing()
        self._audit.log_safety_event("CHAMBER_INTERLOCK", {
            "locked": locked
        })
        return self._safety_mon.check_all(self._safety)

    def trigger_e_stop(self) -> dict:
        """Trigger emergency stop — stops everything."""
        self._safety_mon.trigger_e_stop(self._safety)
        self._laser.stop()
        self._laser.turn_off()
        self._gas.stop()
        self._table.state = TableState.ERROR
        self._audit.log_safety_event("E_STOP_TRIGGERED", {})
        return self._safety_mon.check_all(self._safety)

    def reset_e_stop(self) -> dict:
        """Reset emergency stop."""
        self._safety_mon.reset_e_stop(self._safety)
        self._table.state = TableState.IDLE
        self._audit.log_safety_event("E_STOP_RESET", {})
        return self._safety_mon.check_all(self._safety)

    # ── Full System Status ────────────────────────────────────────────

    def get_full_status(self) -> dict:
        """Return a snapshot of the entire lab state."""
        return {
            "table": {
                "state": self._table.state.name,
                "position_x": self._table.position.x_mm,
                "position_y": self._table.position.y_mm,
                "is_homed": self._table.is_homed,
            },
            "laser": {
                "state": self._laser.state.name,
                "power_w": self._laser.current_power_w,
                "power_pct": self._laser.power_pct,
            },
            "gas": {
                "state": self._gas.state.name,
                "flow_l_min": self._gas.current_flow.value_l_per_min,
                "o2_ppm": self._gas.chamber_o2_ppm,
                "supply_pct": self._gas.supply_level_pct,
                "atmosphere_safe": self._gas.is_atmosphere_safe,
            },
            "safety": self._safety.status_summary,
        }

    def get_audit_logs(self, count: int = 50) -> list:
        """Retrieve recent audit logs."""
        return self._audit.get_recent_logs(count)

    # ── Entity Accessors (read-only) ──────────────────────────────────

    @property
    def table(self) -> XYTable:
        return self._table

    @property
    def laser(self) -> LaserUnit:
        return self._laser

    @property
    def gas(self) -> GasSystem:
        return self._gas

    @property
    def safety(self) -> SafetySystem:
        return self._safety

    # ── Private ───────────────────────────────────────────────────────

    def _ensure_safe_for_operation(self) -> None:
        """Check all safety systems before dangerous operations."""
        if self._safety.e_stop_pressed:
            raise EmergencyStopError("E-Stop is pressed.")
        if not self._safety.all_interlocks_locked:
            raise SafetyViolationError(
                "Not all interlocks are locked."
            )

    def _auto_stop_laser_if_firing(self) -> None:
        """Safety: auto-stop laser if an interlock opens while firing."""
        if self._laser.state == LaserState.FIRING:
            self._laser.stop()
            self._audit.log_safety_event("AUTO_STOP_LASER", {
                "reason": "Interlock opened during firing"
            })
