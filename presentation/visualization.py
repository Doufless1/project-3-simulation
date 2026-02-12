"""
Visualization Module — Matplotlib-based plotting for simulation results.

Single Responsibility: Only handles visualization, no simulation logic.

Generates:
- 3D surface temperature cross-sections
- 2D fluence heatmap
- Depth temperature profile
"""

import os

import numpy as np


def _get_pyplot():
    """Lazy import of matplotlib.pyplot with Agg backend."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    return plt


def plot_fluence_map(result, output_path: str = "fluence_map.png") -> str:
    """
    Plot 2D cumulative fluence heatmap.

    Args:
        result: SimulationResult with fluence_map data.
        output_path: File path for the saved figure.

    Returns:
        Path to the saved image.
    """
    plt = _get_pyplot()

    fluence = np.array(result.fluence_map)
    if fluence.ndim != 2 or fluence.size == 0:
        print("[Visualization] No fluence data to plot.")
        return ""

    x = np.array(result.x_coords) * 1000  # Convert to mm
    y = np.array(result.y_coords) * 1000

    fig, ax = plt.subplots(figsize=(8, 6))
    im = ax.pcolormesh(
        x, y, fluence.T,
        cmap="inferno",
        shading="auto",
    )
    cbar = fig.colorbar(im, ax=ax)
    cbar.set_label("Cumulative Fluence [J/m²]", fontsize=11)
    ax.set_xlabel("X [mm]", fontsize=11)
    ax.set_ylabel("Y [mm]", fontsize=11)
    ax.set_title("Surface Fluence Map", fontsize=13, fontweight="bold")
    ax.set_aspect("equal")
    fig.tight_layout()
    fig.savefig(output_path, dpi=150)
    plt.close(fig)
    print(f"[Visualization] Fluence map saved to: {output_path}")
    return output_path


def plot_temperature_cross_section(
    result,
    axis: str = "xz",
    output_path: str = "temperature_cross_section.png",
) -> str:
    """
    Plot a 2D temperature cross-section through the 3D field.

    Args:
        result: SimulationResult with temperature_field data.
        axis: Which plane to slice ('xz' or 'yz').
        output_path: File path for the saved figure.

    Returns:
        Path to the saved image.
    """
    plt = _get_pyplot()

    t_field = np.array(result.temperature_field)
    if t_field.ndim != 3 or t_field.size == 0:
        print("[Visualization] No 3D temperature data to plot.")
        return ""

    x_coords = np.array(result.x_coords) * 1000  # mm
    y_coords = np.array(result.y_coords) * 1000
    z_coords = np.array(result.z_coords) * 1000

    fig, ax = plt.subplots(figsize=(10, 5))

    if axis == "xz":
        mid_y = t_field.shape[1] // 2
        cross = t_field[:, mid_y, :].T  # shape (nz, nx)
        im = ax.pcolormesh(
            x_coords, z_coords, cross,
            cmap="hot",
            shading="auto",
        )
        ax.set_xlabel("X [mm]", fontsize=11)
        ax.set_ylabel("Depth Z [mm]", fontsize=11)
        ax.set_title("Temperature — XZ Cross-Section (mid-Y)", fontsize=13, fontweight="bold")
    else:  # yz
        mid_x = t_field.shape[0] // 2
        cross = t_field[mid_x, :, :].T  # shape (nz, ny)
        im = ax.pcolormesh(
            y_coords, z_coords, cross,
            cmap="hot",
            shading="auto",
        )
        ax.set_xlabel("Y [mm]", fontsize=11)
        ax.set_ylabel("Depth Z [mm]", fontsize=11)
        ax.set_title("Temperature — YZ Cross-Section (mid-X)", fontsize=13, fontweight="bold")

    ax.invert_yaxis()
    cbar = fig.colorbar(im, ax=ax)
    cbar.set_label("Temperature [°C]", fontsize=11)
    fig.tight_layout()
    fig.savefig(output_path, dpi=150)
    plt.close(fig)
    print(f"[Visualization] Cross-section saved to: {output_path}")
    return output_path


def plot_depth_profile(
    result,
    output_path: str = "depth_profile.png",
) -> str:
    """
    Plot temperature vs depth at the hottest surface point.

    Args:
        result: SimulationResult with 3D temperature field.
        output_path: File path for the saved figure.

    Returns:
        Path to the saved image.
    """
    plt = _get_pyplot()

    t_field = np.array(result.temperature_field)
    if t_field.ndim != 3 or t_field.size == 0:
        print("[Visualization] No 3D data for depth profile.")
        return ""

    z_coords = np.array(result.z_coords) * 1e6  # Convert to µm

    # Find hottest surface point
    surface_temps = t_field[:, :, 0]
    hot_idx = np.unravel_index(np.argmax(surface_temps), surface_temps.shape)
    depth_profile = t_field[hot_idx[0], hot_idx[1], :]

    fig, ax = plt.subplots(figsize=(7, 5))
    ax.plot(z_coords, depth_profile, "r-", linewidth=2, label="Temperature")

    # Add melt and vaporization lines if relevant
    if result.melt_depth_m is not None:
        t_melt = next(
            (t for z_um, t in zip(z_coords, depth_profile)
             if z_um >= result.melt_depth_m * 1e6),
            None,
        )
        ax.axhline(y=depth_profile[0] * 0.5, color="orange", linestyle="--",
                    alpha=0.7, label=f"Melt depth: {result.melt_depth_m*1e6:.0f} µm")

    ax.set_xlabel("Depth [µm]", fontsize=11)
    ax.set_ylabel("Temperature [°C]", fontsize=11)
    ax.set_title("Temperature vs Depth (Hottest Point)", fontsize=13, fontweight="bold")
    ax.legend(fontsize=10)
    ax.grid(True, alpha=0.3)
    fig.tight_layout()
    fig.savefig(output_path, dpi=150)
    plt.close(fig)
    print(f"[Visualization] Depth profile saved to: {output_path}")
    return output_path


def generate_all_plots(result, output_dir: str = ".") -> list:
    """
    Generate all standard visualization plots.

    Returns:
        List of saved file paths.
    """
    paths = []
    paths.append(plot_fluence_map(
        result, os.path.join(output_dir, "fluence_map.png")
    ))
    paths.append(plot_temperature_cross_section(
        result, "xz", os.path.join(output_dir, "temperature_xz.png")
    ))
    paths.append(plot_temperature_cross_section(
        result, "yz", os.path.join(output_dir, "temperature_yz.png")
    ))
    paths.append(plot_depth_profile(
        result, os.path.join(output_dir, "depth_profile.png")
    ))
    return [p for p in paths if p]
