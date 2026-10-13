"""
Domain Exceptions — Typed error hierarchy.

CIA Triad:
  - Integrity: Invalid data is rejected with precise error types.
  - Availability: Errors are caught and handled gracefully.
"""


class LabControlError(Exception):
    """Base exception for all lab control errors."""


# ── Table Errors ──────────────────────────────────────────────────────

class TableLimitError(LabControlError):
    """Motion would exceed travel limits (Integrity)."""


class TableBusyError(LabControlError):
    """Table is already executing a move (Availability)."""


class TableNotHomedError(LabControlError):
    """Table must be homed before executing moves (Integrity)."""


# ── Laser Errors ──────────────────────────────────────────────────────

class LaserPowerError(LabControlError):
    """Requested power is outside safe range (Integrity)."""


class LaserInterlockError(LabControlError):
    """Cannot fire laser — safety interlock is open (Confidentiality)."""


# ── Gas System Errors ─────────────────────────────────────────────────

class GasFlowError(LabControlError):
    """Flow rate outside valid range (Integrity)."""


class GasSupplyError(LabControlError):
    """Gas supply pressure too low (Availability)."""


# ── Safety Errors ─────────────────────────────────────────────────────

class SafetyViolationError(LabControlError):
    """A safety condition has been violated (all CIA pillars)."""


class EmergencyStopError(LabControlError):
    """Emergency stop has been triggered (Availability)."""


# ── Cost Errors ───────────────────────────────────────────────────────

class InvalidComponentError(LabControlError):
    """Component not found in catalog (Integrity)."""
