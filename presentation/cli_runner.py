"""
CLI Runner — Main entry point and Composition Root.

This is the Composition Root where all dependencies are wired together.
It is the ONLY place that knows about concrete implementations.

Clean Architecture Rule: Only the outermost layer (Presentation)
imports concrete Infrastructure classes.

Usage:
    python -m presentation.cli_runner
    python -m presentation.cli_runner --solver analytical
    python -m presentation.cli_runner --beam tophat
    python -m presentation.cli_runner --motion spiral
"""

import argparse
import sys

from domain.entities import Material, LaserBeam

# Infrastructure (concrete implementations — only imported HERE)
from infrastructure.laser.gaussian_source import GaussianLaserSource
from infrastructure.laser.tophat_source import TopHatLaserSource
from infrastructure.laser.laser_factory import LaserFactory
from infrastructure.motion.raster_generator import RasterGenerator
from infrastructure.motion.spiral_generator import SpiralGenerator
from infrastructure.motion.linear_generator import LinearGenerator
from infrastructure.solvers.fdm_heat_solver_3d import FDMHeatSolver3D
from infrastructure.solvers.analytical_solver import AnalyticalHeatSolver
from infrastructure.logging.audit_logger import AuditLogger

# Application (use case)
from application.simulation_use_case import SimulationUseCase

# Presentation
from presentation.visualization import generate_all_plots


# ============================================================================
# Default Materials (Factory-like presets — DRY)
# ============================================================================

SS316L = Material(
    name="316L Stainless Steel",
    absorption=0.35,
    thermal_conductivity=15.0,
    density=8000.0,
    specific_heat=500.0,
    t_ambient=20.0,
    t_melt=1400.0,
    t_vaporization=2800.0,
)

ULTRA_C = Material(
    name="ULTRA-C-Ta-WH-3849",
    absorption=0.70,
    thermal_conductivity=60.0,
    density=9939.0,
    specific_heat=386.0,
    t_ambient=20.0,
    t_melt=3376.0,
    t_vaporization=4800.0,
)

WC_CO = Material(
    name="WC-12Co (HVOF Coating)",
    absorption=0.55,
    thermal_conductivity=80.0,
    density=14500.0,
    specific_heat=240.0,
    t_ambient=20.0,
    t_melt=2870.0,
    t_vaporization=6000.0,
)

MATERIALS = {
    "ss316l": SS316L,
    "ultra_c": ULTRA_C,
    "wc_co": WC_CO,
}


# ============================================================================
# CLI Argument Parsing
# ============================================================================

def parse_arguments():
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="Laser-HVOF 3D Simulation Engine (Clean Architecture)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python -m presentation.cli_runner
  python -m presentation.cli_runner --material wc_co --solver fdm3d
  python -m presentation.cli_runner --beam tophat --motion spiral
        """,
    )
    parser.add_argument(
        "--material", choices=list(MATERIALS.keys()),
        default="ss316l", help="Material preset (default: ss316l)",
    )
    parser.add_argument(
        "--solver", choices=["fdm3d", "analytical"],
        default="fdm3d", help="Heat solver (default: fdm3d)",
    )
    parser.add_argument(
        "--beam", choices=["gaussian", "tophat"],
        default="gaussian", help="Beam profile (default: gaussian)",
    )
    parser.add_argument(
        "--motion", choices=["raster", "spiral", "linear"],
        default="raster", help="Motion pattern (default: raster)",
    )
    parser.add_argument(
        "--power", type=float, default=500.0,
        help="Laser power [W] (default: 500)",
    )
    parser.add_argument(
        "--speed", type=float, default=0.1,
        help="Scan speed [m/s] (default: 0.1)",
    )
    parser.add_argument(
        "--resolution", type=float, default=200e-6,
        help="Grid resolution [m] (default: 200e-6)",
    )
    parser.add_argument(
        "--output-dir", type=str, default="output",
        help="Output directory for plots (default: output)",
    )
    return parser.parse_args()


# ============================================================================
# Composition Root — Wire Dependencies
# ============================================================================

def build_simulation(args) -> tuple:
    """
    Wire all dependencies based on CLI arguments.

    This is the ONLY function that knows about concrete types.
    Returns (use_case, material, laser, motion_params, grid_size, resolution).
    """
    # --- Select beam profile strategy ---
    if args.beam == "gaussian":
        laser_source = GaussianLaserSource()
    else:
        laser_source = TopHatLaserSource()

    # --- Select heat solver strategy ---
    if args.solver == "fdm3d":
        solver = FDMHeatSolver3D(laser_source=laser_source)
    else:
        solver = AnalyticalHeatSolver(laser_source=laser_source)

    # --- Select motion generator strategy ---
    motion_generators = {
        "raster": RasterGenerator(),
        "spiral": SpiralGenerator(),
        "linear": LinearGenerator(),
    }
    motion_gen = motion_generators[args.motion]

    # --- Create audit logger ---
    audit = AuditLogger(log_dir=args.output_dir)

    # --- Wire use case ---
    use_case = SimulationUseCase(
        heat_solver=solver,
        motion_generator=motion_gen,
        audit_logger=audit,
    )

    # --- Select material ---
    material = MATERIALS[args.material]

    # --- Create laser ---
    laser = LaserFactory.create_nd_yag(power=args.power)

    # --- Motion parameters ---
    grid_lx, grid_ly, grid_lz = 0.010, 0.010, 0.002

    if args.motion == "raster":
        motion_params = {
            "x_start": 0.0, "x_end": grid_lx,
            "y_start": 0.0, "y_end": grid_ly,
            "z_focus": 0.05,
            "line_spacing": 0.5e-3,
            "scan_speed": args.speed,
        }
    elif args.motion == "spiral":
        motion_params = {
            "center_x": grid_lx / 2, "center_y": grid_ly / 2,
            "z_focus": 0.05,
            "inner_radius": 0.5e-3, "outer_radius": 4.0e-3,
            "n_revolutions": 5,
            "scan_speed": args.speed,
        }
    else:  # linear
        motion_params = {
            "x_start": 0.0, "y_start": grid_ly / 2,
            "x_end": grid_lx, "y_end": grid_ly / 2,
            "z_focus": 0.05,
            "scan_speed": args.speed,
        }

    return use_case, material, laser, motion_params, (grid_lx, grid_ly, grid_lz), args.resolution


# ============================================================================
# Main
# ============================================================================

def main():
    """Main entry point."""
    args = parse_arguments()

    # Ensure output directory exists
    import os
    os.makedirs(args.output_dir, exist_ok=True)

    print("=" * 65)
    print("  LASER-HVOF 3D SIMULATION ENGINE")
    print("  Clean Architecture · SOLID · CIA · STRIDE")
    print("=" * 65)

    # --- Build ---
    use_case, material, laser, motion_params, grid_size, resolution = (
        build_simulation(args)
    )

    print(f"\n  Material:   {material.name}")
    print(f"  Solver:     {args.solver}")
    print(f"  Beam:       {args.beam}")
    print(f"  Motion:     {args.motion}")
    print(f"  Power:      {laser.power} W")
    print(f"  Resolution: {resolution*1e6:.0f} µm")
    print(f"  Grid:       {grid_size[0]*1e3:.1f} × {grid_size[1]*1e3:.1f} × {grid_size[2]*1e3:.1f} mm")
    print()

    # --- Execute ---
    print("  Running simulation...")
    result = use_case.execute(
        material=material,
        laser=laser,
        motion_params=motion_params,
        grid_size=grid_size,
        resolution=resolution,
    )

    # --- Display Results ---
    print("\n" + "=" * 65)
    print("  RESULTS")
    print("=" * 65)
    print(f"  Peak Temperature:    {result.peak_temperature_celsius:.1f} °C")
    print(f"  Peak Fluence:        {result.peak_fluence_j_per_m2:.2e} J/m²")
    print(f"  Total Energy:        {result.total_energy_j:.4f} J")

    if result.melt_depth_m is not None:
        print(f"  Melt Depth:          {result.melt_depth_m*1e6:.1f} µm")
    else:
        print("  Melt Depth:          None (below melt point)")

    if result.vaporization_depth_m is not None:
        print(f"  Vaporization Depth:  {result.vaporization_depth_m*1e6:.1f} µm")

    print(f"  Solver Time:         {result.duration_seconds:.2f} s")
    print()

    # --- Generate Plots ---
    print("  Generating plots...")
    plots = generate_all_plots(result, output_dir=args.output_dir)
    print(f"  Generated {len(plots)} plot(s) in '{args.output_dir}/'")
    print()
    print("  Done.")


if __name__ == "__main__":
    main()
