"""
Coating Analysis — Post-processing for HVOF laser treatment simulation.

Extracts experimental-equivalent measurements from the simulation output:
  - Porosity reduction (pores sealed in melt zone)
  - Melt depth
  - Heat-affected zone (HAZ) depth
  - Hardness prediction (empirical model)
  - Substrate melting check

All measurements are designed to be directly comparable to published
experimental data from the 2024 WC-NiCr study.
"""

import numpy as np

from domain.coating_entities import CoatingConfig, CoatingGrid, LaserTreatmentResult


def measure_porosity_change(
    grid: CoatingGrid,
    melted_mask: np.ndarray,
) -> tuple:
    """
    Calculate porosity before and after laser treatment.

    Pores inside the melted zone are considered "sealed" (densified).

    Args:
        grid: Original coating grid with pore locations.
        melted_mask: Boolean mask of cells that reached T > T_melt.

    Returns:
        (porosity_before, porosity_after, reduction_percent)
    """
    coating = grid.grid[:grid.coating_rows, :]
    melted_coating = melted_mask[:grid.coating_rows, :]
    total_cells = coating.size

    # Pores before treatment
    pores_before = np.sum(coating == 0)
    porosity_before = pores_before / total_cells

    # Pores that were inside the melted zone are now sealed
    pores_sealed = np.sum((coating == 0) & melted_coating)
    pores_after = pores_before - pores_sealed
    porosity_after = pores_after / total_cells

    # Reduction percentage
    if porosity_before > 0:
        reduction_pct = (1.0 - porosity_after / porosity_before) * 100.0
    else:
        reduction_pct = 0.0

    return float(porosity_before), float(porosity_after), float(reduction_pct)


def measure_melt_depth(
    melted_mask: np.ndarray,
    dy: float,
) -> float:
    """
    Measure the maximum melt depth in micrometers.

    Args:
        melted_mask: Boolean mask of melted cells.
        dy: Cell height [m].

    Returns:
        Melt depth in µm.
    """
    # For each column, find the deepest melted row
    melted_per_col = np.any(melted_mask, axis=1)  # Which rows have any melting
    if not np.any(melted_per_col):
        return 0.0

    deepest_row = np.max(np.where(melted_per_col)[0])
    return float(deepest_row * dy * 1e6)  # Convert to µm


def measure_haz_depth(
    temperature_field: np.ndarray,
    melted_mask: np.ndarray,
    dy: float,
    coating_rows: int = None,
    t_haz_threshold: float = 200.0,
) -> float:
    """
    Measure the heat-affected zone depth (below the melt zone).

    HAZ = region where T > threshold but not melted, within coating only.

    Args:
        temperature_field: Final 2D temperature field.
        melted_mask: Boolean mask of melted cells.
        dy: Cell height [m].
        coating_rows: If set, only measure HAZ within the coating region.
        t_haz_threshold: Temperature threshold for HAZ. Default 200 C.

    Returns:
        HAZ depth in um.
    """
    # Limit to coating region if specified
    if coating_rows is not None:
        T_region = temperature_field[:coating_rows, :]
        melt_region = melted_mask[:coating_rows, :]
    else:
        T_region = temperature_field
        melt_region = melted_mask

    haz_mask = (T_region > t_haz_threshold) & (~melt_region)

    haz_per_row = np.any(haz_mask, axis=1)
    if not np.any(haz_per_row):
        return 0.0

    deepest_haz = np.max(np.where(haz_per_row)[0])

    # HAZ depth is measured from the bottom of the melt zone
    melted_per_row = np.any(melt_region, axis=1)
    if np.any(melted_per_row):
        deepest_melt = np.max(np.where(melted_per_row)[0])
        haz_depth = max(0, deepest_haz - deepest_melt)
    else:
        haz_depth = deepest_haz

    return float(haz_depth * dy * 1e6)  # Convert to um


def predict_hardness(
    hv_before: float,
    porosity_before: float,
    porosity_after: float,
    beta: float = 0.3,
) -> float:
    """
    Empirical hardness prediction based on porosity reduction.

    HV_after = HV_before × (1 + β · Δϕ/ϕ₀)

    Calibrated from: ~72% porosity reduction → ~21% hardness increase.

    Args:
        hv_before: As-sprayed hardness [HV].
        porosity_before: Porosity before treatment (fraction).
        porosity_after: Porosity after treatment (fraction).
        beta: Calibration constant (default 0.3).

    Returns:
        Predicted post-treatment hardness [HV].
    """
    if porosity_before <= 0:
        return hv_before

    delta_phi = porosity_before - porosity_after
    hv_after = hv_before * (1.0 + beta * delta_phi / porosity_before)

    return float(hv_after)


def check_substrate_melting(
    temperature_field: np.ndarray,
    coating_rows: int,
    t_melt_substrate: float,
) -> tuple:
    """
    Check whether the substrate exceeded its melting temperature.

    Args:
        temperature_field: Final 2D temperature field [°C].
        coating_rows: Number of rows in the coating layer.
        t_melt_substrate: Substrate melting point [°C].

    Returns:
        (substrate_melted: bool, max_substrate_temp: float)
    """
    substrate_region = temperature_field[coating_rows:, :]

    if substrate_region.size == 0:
        return False, 0.0

    max_temp = float(np.max(substrate_region))
    return max_temp > t_melt_substrate, max_temp


def build_treatment_result(
    coating_grid: CoatingGrid,
    config: CoatingConfig,
    solver_output: dict,
    t_melt: float,
    laser_power: float,
    scan_speed: float,
    beam_radius: float,
) -> LaserTreatmentResult:
    """
    Assemble all post-processing measurements into a LaserTreatmentResult.

    This is the main entry point for post-processing.

    Args:
        coating_grid: Original coating microstructure.
        config: Coating configuration.
        solver_output: Dict from FDMCoatingSolver2D.solve().
        t_melt: Coating matrix melting temperature [°C].
        laser_power: Laser power used [W].
        scan_speed: Scan speed used [m/s].
        beam_radius: Beam radius used [m].

    Returns:
        LaserTreatmentResult with all metrics.
    """
    T = solver_output["temperature"]
    melted = solver_output["melted"]

    # Porosity analysis
    por_before, por_after, por_reduction = measure_porosity_change(
        coating_grid, melted
    )

    # Melt depth
    melt_depth = measure_melt_depth(melted, coating_grid.dy)

    # HAZ depth
    haz_depth = measure_haz_depth(T, melted, coating_grid.dy,
                                   coating_rows=coating_grid.coating_rows)

    # Hardness prediction
    hv_after = predict_hardness(
        config.hardness_hv_before, por_before, por_after
    )

    # Substrate check
    sub_melted, max_sub_temp = check_substrate_melting(
        T, coating_grid.coating_rows, config.substrate_t_melt
    )

    return LaserTreatmentResult(
        temperature_field=T,
        peak_temperature_c=float(np.max(T)),
        melted_mask=melted,
        melt_depth_um=melt_depth,
        porosity_before=por_before,
        porosity_after=por_after,
        porosity_reduction_pct=por_reduction,
        haz_depth_um=haz_depth,
        hardness_hv_before=config.hardness_hv_before,
        hardness_hv_after=hv_after,
        substrate_melted=sub_melted,
        max_substrate_temp_c=max_sub_temp,
        laser_power_w=laser_power,
        scan_speed_m_s=scan_speed,
        beam_radius_m=beam_radius,
        duration_seconds=solver_output["duration_s"],
    )
