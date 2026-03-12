"""
HVOF Laser Treatment Validation Script.

Runs the full two-stage simulation at multiple power levels and
compares outputs against published benchmarks from the 2024 WC-NiCr study.

Usage:
    cd "c:\\Users\\usuario\\Project 3 Simulation"
    python run_hvof_validation.py
"""

import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from domain.entities import Material
from domain.coating_entities import CoatingConfig
from application.hvof_laser_use_case import HVOFLaserUseCase


# ============================================================================
# WC-NiCr Material (from 2024 study)
# ============================================================================

WC_NICR = Material(
    name="WC-NiCr (HVOF Coating)",
    absorption=0.45,              # α ≈ 0.3–0.6 for WC at 1µm wavelength
    thermal_conductivity=12.0,   # k ≈ 10–15 W/m·K
    density=13500.0,             # ρ ≈ 13,000–14,500 kg/m³
    specific_heat=350.0,         # cp ≈ 300–400 J/kg·K
    t_melt=1350.0,               # NiCr matrix melting ~1300–1400°C
    t_vaporization=2800.0,
    t_ambient=25.0,
)

# ============================================================================
# Coating Configuration
# ============================================================================

COATING_CONFIG = CoatingConfig(
    coating_thickness_m=300e-6,   # 300 µm
    coating_width_m=1e-3,        # 1 mm
    target_porosity=0.03,        # 3%
    splat_thickness_m=7.5e-6,    # 5–10 µm
    hardness_hv_before=1000.0,   # 900–1100 HV0.3
)

# ============================================================================
# Benchmark targets (from published studies)
# ============================================================================

BENCHMARKS = {
    "porosity_reduction_pct": (30.0, 72.0),      # WC-NiCr 2024 study
    "melt_depth_um": (50.0, 150.0),               # NiCrBSi study, 300-500W
    "peak_temperature_c": (1400.0, 2000.0),        # Multiple studies, beam center
    "haz_depth_um": (100.0, 200.0),                # General laser studies
}

# ============================================================================
# Run validation
# ============================================================================

def main():
    print("=" * 70)
    print("  HVOF LASER TREATMENT SIMULATION - VALIDATION RUN")
    print("=" * 70)
    print()
    print(f"Material:      {WC_NICR.name}")
    print(f"k = {WC_NICR.thermal_conductivity} W/m.K, "
          f"rho = {WC_NICR.density} kg/m3, "
          f"cp = {WC_NICR.specific_heat} J/kg.K")
    print(f"T_melt = {WC_NICR.t_melt} C, alpha = {WC_NICR.absorption}")
    print(f"Target porosity: {COATING_CONFIG.target_porosity * 100:.1f}%")
    print()

    # Use coarser resolution for speed (10um instead of 2um)
    resolution = 10e-6

    use_case = HVOFLaserUseCase()
    power_levels = [200.0, 270.0, 290.0]

    print("Running parameter sweep...")
    print(f"  Powers: {power_levels} W")
    print(f"  Beam radius: 1.0 mm")
    print(f"  Scan speed: 10 mm/s")
    print(f"  Resolution: {resolution * 1e6:.1f} um")
    print()

    results = use_case.parameter_sweep(
        material=WC_NICR,
        power_levels=power_levels,
        coating_config=COATING_CONFIG,
        beam_radius=0.001,
        scan_speed=0.015,
        resolution=resolution,
        seed=42,
    )

    # ===== Print Results =====
    print("-" * 70)
    print(f"{'Power [W]':>10} | {'Peak T [C]':>12} | {'Melt [um]':>10} | "
          f"{'Porosity D%':>12} | {'HAZ [um]':>10} | {'HV after':>9} | "
          f"{'Sub Melt?':>9}")
    print("-" * 70)

    for power, result in zip(power_levels, results):
        print(
            f"{power:>10.0f} | "
            f"{result.peak_temperature_c:>12.1f} | "
            f"{result.melt_depth_um:>10.1f} | "
            f"{result.porosity_reduction_pct:>12.1f} | "
            f"{result.haz_depth_um:>10.1f} | "
            f"{result.hardness_hv_after:>9.0f} | "
            f"{'YES !!' if result.substrate_melted else 'NO ok':>9}"
        )

    print("-" * 70)
    print()

    # ===== Validation Against Benchmarks =====
    print("=" * 70)
    print("  VALIDATION AGAINST PUBLISHED BENCHMARKS")
    print("=" * 70)
    print()

    # Use the highest-power result (500W) for benchmark comparison
    best = results[-1]
    all_pass = True

    print()
    checks = [
        ("Porosity reduction [%]", best.porosity_reduction_pct, BENCHMARKS["porosity_reduction_pct"]),
        ("Melt depth [um]", best.melt_depth_um, BENCHMARKS["melt_depth_um"]),
        ("Peak surface temp [C]", best.peak_temperature_c, BENCHMARKS["peak_temperature_c"]),
        ("HAZ depth [um]", best.haz_depth_um, BENCHMARKS["haz_depth_um"]),
    ]

    for name, value, (lo, hi) in checks:
        passed = lo <= value <= hi
        status = "PASS" if passed else "FAIL"
        if not passed:
            all_pass = False
        print(f"  {name:<28s}  {value:>8.1f}  target [{lo:.0f}-{hi:.0f}]  {status}")

    # Substrate melting check
    sub_ok = not best.substrate_melted
    print(f"  {'No substrate melting':<28s}  {'YES' if sub_ok else 'NO':>8s}  "
          f"target [NO melting]            {'PASS' if sub_ok else 'FAIL'}")
    if not sub_ok:
        all_pass = False

    print()
    if all_pass:
        print("  >>> ALL BENCHMARKS PASSED - Simulation is validated! <<<")
    else:
        print("  ** Some benchmarks outside range - consider tuning parameters. **")
        print("    Tips: increase power or absorptivity for low porosity reduction,")
        print("    decrease power if substrate melts or melt depth too high.")

    print()
    print(f"Total simulation time: "
          f"{sum(r.duration_seconds for r in results):.1f}s")
    print("=" * 70)


if __name__ == "__main__":
    main()
