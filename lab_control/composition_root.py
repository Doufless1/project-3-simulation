"""
Composition Root — Wires all dependencies and creates the use case.

This is the ONLY place where concrete classes are imported.
All other layers depend ONLY on abstractions (ports).

Pattern: Dependency Injection via Constructor.
"""

from lab_control.domain.entities import (
    XYTable,
    LaserUnit,
    GasSystem,
    SafetySystem,
)
from lab_control.infrastructure.virtual_table_controller import (
    VirtualTableController,
)
from lab_control.infrastructure.virtual_laser_controller import (
    VirtualLaserController,
)
from lab_control.infrastructure.virtual_gas_controller import (
    VirtualGasController,
)
from lab_control.infrastructure.virtual_safety_monitor import (
    VirtualSafetyMonitor,
)
from lab_control.infrastructure.equipment_catalog import (
    EquipmentCostCatalog,
)
from lab_control.infrastructure.audit_logger import InMemoryAuditLogger
from lab_control.application.lab_control_use_case import LabControlUseCase


def create_lab_control() -> LabControlUseCase:
    """
    Factory function: assembles the entire lab from concrete parts.

    Returns a fully wired LabControlUseCase ready for use.
    """
    return LabControlUseCase(
        table=XYTable(),
        laser=LaserUnit(),
        gas=GasSystem(),
        safety=SafetySystem(),
        table_ctrl=VirtualTableController(),
        laser_ctrl=VirtualLaserController(),
        gas_ctrl=VirtualGasController(),
        safety_mon=VirtualSafetyMonitor(),
        audit=InMemoryAuditLogger(),
    )


def create_cost_calculator() -> EquipmentCostCatalog:
    """Factory function: creates cost calculator with default catalog."""
    return EquipmentCostCatalog()
