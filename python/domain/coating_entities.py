"""
Coating Domain Entities — HVOF microstructure and laser treatment results.

Entities for the two-stage simulation:
  Stage 1: CoatingConfig + CoatingGrid (HVOF microstructure)
  Stage 2: LaserTreatmentResult (post-laser analysis)

Design: Same patterns as existing entities — dataclass + validation.
"""

from dataclasses import dataclass, field
from typing import Optional, List

import numpy as np


# ============================================================================
# Coating Configuration
# ============================================================================

@dataclass
class CoatingConfig:
    """
    Configuration for generating an HVOF coating microstructure.

    All dimensions in SI units (meters).
    """

    # Coating geometry
    coating_thickness_m: float = 300e-6      # 300 µm standard HVOF
    coating_width_m: float = 1e-3            # 1 mm lateral extent

    # Porosity parameters
    target_porosity: float = 0.03            # 3% vol (from 2024 study)
    pore_size_m: float = 0.5e-6             # Dominant pore size 0.1–1.0 µm

    # Lamellar structure
    splat_thickness_m: float = 7.5e-6       # 5–10 µm between splat boundaries
    boundary_pore_factor: float = 3.0        # Pores 3× more likely at boundaries

    # Substrate
    substrate_thickness_m: float = 200e-6   # Substrate layer below coating
    substrate_k: float = 50.0               # Steel thermal conductivity [W/m·K]
    substrate_rho: float = 7850.0           # Steel density [kg/m³]
    substrate_cp: float = 500.0             # Steel specific heat [J/kg·K]
    substrate_t_melt: float = 1400.0        # Steel melting point [°C]

    # Hardness (as-sprayed)
    hardness_hv_before: float = 1000.0      # 900–1100 HV0.3

    def __post_init__(self):
        self._validate()

    def _validate(self):
        if not (0.0 < self.target_porosity < 1.0):
            raise ValueError(
                f"Target porosity must be in (0, 1), got {self.target_porosity}"
            )
        if self.coating_thickness_m <= 0:
            raise ValueError("Coating thickness must be positive.")
        if self.splat_thickness_m <= 0:
            raise ValueError("Splat thickness must be positive.")


# ============================================================================
# Coating Grid (Stage 1 output)
# ============================================================================

@dataclass
class CoatingGrid:
    """
    2D grid representing the HVOF coating microstructure.

    grid: 2D array, shape (ny, nx). 1 = solid, 0 = pore.
    k_map: 2D array, same shape. Thermal conductivity at each cell [W/m·K].
    """

    grid: np.ndarray                         # (ny, nx): 1=solid, 0=pore
    k_map: np.ndarray                        # (ny, nx): thermal conductivity
    dx: float                                # Cell width [m]
    dy: float                                # Cell height [m]
    coating_rows: int                        # Number of rows that are coating
    substrate_rows: int                      # Number of rows that are substrate

    @property
    def shape(self):
        return self.grid.shape

    @property
    def ny(self) -> int:
        return self.grid.shape[0]

    @property
    def nx(self) -> int:
        return self.grid.shape[1]

    @property
    def porosity(self) -> float:
        """Current porosity of the coating region only."""
        coating = self.grid[:self.coating_rows, :]
        total_cells = coating.size
        pore_cells = np.sum(coating == 0)
        return float(pore_cells / total_cells) if total_cells > 0 else 0.0

    @property
    def pore_count(self) -> int:
        return int(np.sum(self.grid[:self.coating_rows, :] == 0))


# ============================================================================
# Laser Treatment Result (Stage 2 output)
# ============================================================================

@dataclass
class LaserTreatmentResult:
    """
    Complete results from the two-stage HVOF + laser simulation.

    Contains all metrics that a real experiment would measure.
    """

    # Temperature data
    temperature_field: np.ndarray            # Final 2D temperature field
    peak_temperature_c: float = 0.0          # Max surface temperature [°C]

    # Melt analysis
    melted_mask: np.ndarray = field(default_factory=lambda: np.array([]))
    melt_depth_um: float = 0.0               # Max melt depth [µm]

    # Porosity analysis
    porosity_before: float = 0.0             # As-sprayed porosity
    porosity_after: float = 0.0              # Post-laser porosity
    porosity_reduction_pct: float = 0.0      # Reduction percentage

    # Heat-affected zone
    haz_depth_um: float = 0.0                # HAZ depth [µm]

    # Hardness prediction
    hardness_hv_before: float = 0.0          # As-sprayed [HV]
    hardness_hv_after: float = 0.0           # Post-treatment [HV]

    # Safety check
    substrate_melted: bool = False           # True if substrate T > T_melt
    max_substrate_temp_c: float = 0.0        # Peak temperature in substrate

    # Simulation metadata
    laser_power_w: float = 0.0
    scan_speed_m_s: float = 0.0
    beam_radius_m: float = 0.0
    duration_seconds: float = 0.0
