"""
Figure Renderer — regenerates the solver figures used in the docs.

Runs the two-stage HVOF laser-remelting model (coating generator + 2D FDM
solver) on the WC-NiCr reference case from run_hvof_validation.py and saves
a before / during / after cross-section of the coating.

Usage (from python/):
    python tools/render_figures.py
Output:
    ../docs/media/images/coating-remelting-290w.png
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ROOT)

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.colors import ListedColormap

from application.hvof_laser_use_case import HVOFLaserUseCase
from infrastructure.coating.coating_generator import HVOFCoatingGenerator
from run_hvof_validation import COATING_CONFIG, WC_NICR

OUT_DIR = os.path.join(os.path.dirname(ROOT), "docs", "media", "images")

# Same recipe as the passing validation case (290 W row).
POWER_W = 290.0
BEAM_RADIUS_M = 0.001
SCAN_SPEED_M_S = 0.015
RESOLUTION_M = 10e-6
SEED = 42


def _material_map(grid, coating_rows, sealed=None):
    """0 = pore, 1 = coating, 2 = substrate, 3 = sealed pore."""
    img = np.where(grid == 0, 0, 1).astype(int)
    img[coating_rows:, :] = np.where(grid[coating_rows:, :] == 0, 0, 2)
    if sealed is not None:
        img[sealed] = 3
    return img


def render_coating_remelting():
    coating = HVOFCoatingGenerator().generate(
        config=COATING_CONFIG,
        coating_k=WC_NICR.thermal_conductivity,
        resolution=RESOLUTION_M,
        seed=SEED,
    )
    result = HVOFLaserUseCase().execute(
        material=WC_NICR,
        coating_config=COATING_CONFIG,
        laser_power=POWER_W,
        beam_radius=BEAM_RADIUS_M,
        scan_speed=SCAN_SPEED_M_S,
        resolution=RESOLUTION_M,
        seed=SEED,
    )

    rows = coating.coating_rows
    melted = np.asarray(result.melted_mask, dtype=bool)
    sealed = (coating.grid == 0) & melted
    sealed[rows:, :] = False

    ny, nx = coating.grid.shape
    extent = [0, nx * coating.dx * 1e3, ny * coating.dy * 1e6, 0]  # mm × µm

    cmap = ListedColormap(["#111827", "#9ca3af", "#cbd5e1", "#22c55e"])
    fig, axes = plt.subplots(3, 1, figsize=(10, 9.5), constrained_layout=True)

    axes[0].imshow(_material_map(coating.grid, rows), cmap=cmap, vmin=0, vmax=3,
                   extent=extent, aspect="auto", interpolation="nearest")
    axes[0].set_title(
        f"As-sprayed HVOF coating — porosity {result.porosity_before * 100:.1f} %",
        loc="left", fontweight="bold")

    t_img = axes[1].imshow(result.temperature_field, cmap="inferno", extent=extent,
                           aspect="auto", interpolation="bilinear")
    axes[1].contour(np.linspace(extent[0], extent[1], nx),
                    np.linspace(extent[3], extent[2], ny),
                    melted.astype(float), levels=[0.5], colors="#38bdf8",
                    linewidths=1.6)
    axes[1].set_title(
        f"End of a {POWER_W:.0f} W pass — peak {result.peak_temperature_c:.0f} °C; "
        f"blue = cells above T_melt (melt depth {result.melt_depth_um:.0f} µm)",
        loc="left", fontweight="bold")
    fig.colorbar(t_img, ax=axes[1], label="Temperature [°C]", pad=0.01)

    axes[2].imshow(_material_map(coating.grid, rows, sealed), cmap=cmap, vmin=0,
                   vmax=3, extent=extent, aspect="auto", interpolation="nearest")
    axes[2].set_title(
        f"After remelting — porosity {result.porosity_after * 100:.1f} % "
        f"({result.porosity_reduction_pct:.0f} % reduction, sealed pores in green), "
        f"substrate {'MELTED' if result.substrate_melted else 'intact'}",
        loc="left", fontweight="bold")

    interface_um = rows * coating.dy * 1e6
    for ax in axes:
        ax.axhline(interface_um, color="#f59e0b", lw=1, ls="--")
        ax.set_ylabel("Depth [µm]")
    for ax in (axes[0], axes[2]):
        ax.text(0.01, interface_um - 12, "HVOF COATING (WC-NiCr)",
                color="#1f2937", fontsize=9, fontweight="bold")
        ax.text(0.01, interface_um + 45, "SUBSTRATE (steel)",
                color="#475569", fontsize=9, fontweight="bold")
    axes[2].set_xlabel("Width [mm]")

    fig.suptitle("WC-NiCr HVOF coating — laser remelting cross-section "
                 "(2D FDM model)", fontsize=13, fontweight="bold")

    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "coating-remelting-290w.png")
    fig.savefig(out, dpi=130)
    plt.close(fig)
    print(f"Saved {out}")
    return out


if __name__ == "__main__":
    render_coating_remelting()
