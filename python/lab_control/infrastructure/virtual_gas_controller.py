"""
Virtual Gas Controller — Simulates argon atmosphere control.

CIA Triad:
  - Integrity: Flow rates validated by domain entity.
  - Availability: Simulated purge sequence with O2 decay model.
"""

import math

from lab_control.domain.entities import GasSystem
from lab_control.domain.ports import IGasController


# Purge model constants
CHAMBER_VOLUME_L = 32.0      # 400×400×200 mm = 32 L
ATMOSPHERIC_O2_PPM = 209500  # ~20.95%


class VirtualGasController(IGasController):
    """
    Simulated shielding gas controller.

    Includes a simplified O2 dilution model for purge simulation.
    """

    def __init__(self):
        self._purge_elapsed_s: float = 0.0

    def set_flow(self, gas: GasSystem, flow_l_min: float) -> None:
        """Set gas flow, validation by entity."""
        gas.set_flow(flow_l_min)

    def start_purge(self, gas: GasSystem) -> None:
        """Begin purge at 15 L/min."""
        gas.start_purge(15.0)
        self._purge_elapsed_s = 0.0

    def stop(self, gas: GasSystem) -> None:
        """Close gas supply."""
        gas.stop()
        self._purge_elapsed_s = 0.0

    def read_o2(self, gas: GasSystem) -> float:
        """
        Simulate O2 level during purge.

        Uses exponential dilution model:
          O2(t) = O2_initial × exp(-flow_rate × t / V_chamber)
        """
        flow = gas.current_flow.value_l_per_min
        if flow <= 0:
            gas.chamber_o2_ppm = ATMOSPHERIC_O2_PPM
            return gas.chamber_o2_ppm

        flow_l_s = flow / 60.0
        gas.chamber_o2_ppm = ATMOSPHERIC_O2_PPM * math.exp(
            -flow_l_s * self._purge_elapsed_s / CHAMBER_VOLUME_L
        )
        return gas.chamber_o2_ppm

    def advance_time(self, delta_s: float) -> None:
        """Advance simulation clock for purge model."""
        self._purge_elapsed_s += delta_s
