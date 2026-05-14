"""
Virtual Laser Controller — Simulates laser power control.

CIA Triad:
  - Confidentiality: Laser state changes gated by safety interlocks.
  - Integrity: Power ranges validated by domain entity.
"""

from lab_control.domain.entities import LaserUnit, SafetySystem
from lab_control.domain.ports import ILaserController


class VirtualLaserController(ILaserController):
    """
    Simulated laser controller.

    Delegates all validation to the LaserUnit entity,
    keeping this class thin (SRP).
    """

    def set_power(self, laser: LaserUnit, power_w: float) -> None:
        """Set laser power — delegates validation to entity."""
        laser.set_power(power_w)

    def arm(self, laser: LaserUnit, safety: SafetySystem) -> None:
        """Arm laser — entity checks interlocks."""
        laser.arm(safety)

    def fire(self, laser: LaserUnit, safety: SafetySystem) -> None:
        """Fire laser — entity enforces all safety checks."""
        laser.fire(safety)

    def stop(self, laser: LaserUnit) -> None:
        """Immediately stop laser emission."""
        laser.stop()
