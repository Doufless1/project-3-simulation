"""
Virtual Safety Monitor — Simulates interlock status reading.

STRIDE:
  - Spoofing: Safety state is read-only; mutations go through entity methods.
  - Tampering: E-stop is latching — requires explicit reset.
  - Repudiation: All safety events should be logged via audit port.
"""

from lab_control.domain.entities import SafetySystem
from lab_control.domain.ports import ISafetyMonitor


class VirtualSafetyMonitor(ISafetyMonitor):
    """
    Simulated safety monitor.

    In a real system, this would read physical switch states
    via GPIO or industrial I/O modules.
    """

    def check_all(self, safety: SafetySystem) -> dict:
        """Return full safety status as a dictionary."""
        return safety.status_summary

    def trigger_e_stop(self, safety: SafetySystem) -> None:
        """
        Trigger emergency stop.

        Latching: system stays stopped until manual reset.
        """
        safety.press_e_stop()

    def reset_e_stop(self, safety: SafetySystem) -> None:
        """Reset emergency stop — simulates manual key-turn reset."""
        safety.reset_e_stop()
