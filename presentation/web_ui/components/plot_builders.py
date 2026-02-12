
import numpy as np
import plotly.graph_objects as go

def empty_3d_figure():
    """Create an empty 3D placeholder."""
    fig = go.Figure(data=[go.Surface(z=[[0]])])
    fig.update_layout(
        scene=dict(
            xaxis=dict(title="X [mm]", color="#607090", gridcolor="#1a2040"),
            yaxis=dict(title="Y [mm]", color="#607090", gridcolor="#1a2040"),
            zaxis=dict(title="T [°C]", color="#607090", gridcolor="#1a2040"),
            bgcolor="rgba(8,10,22,0.95)",
        ),
        paper_bgcolor="rgba(0,0,0,0)",
        margin=dict(l=0, r=0, t=40, b=0),
        font=dict(color="#8898c0"),
        title=dict(text="Run simulation to see results", font=dict(size=14, color="#506080")),
    )
    return fig


def empty_2d_figure(title=""):
    fig = go.Figure()
    fig.update_layout(
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#8898c0"),
        margin=dict(l=50, r=30, t=50, b=50),
        title=dict(text=title, font=dict(size=14, color="#506080")),
    )
    return fig


def build_3d_surface(t_field, x_mm, y_mm, data):
    """Interactive 3D surface temperature plot."""
    if t_field.ndim != 3:
        return empty_3d_figure()

    surface = t_field[:, :, 0]

    fig = go.Figure(data=[
        go.Surface(
            x=x_mm, y=y_mm, z=surface.T,
            colorscale="Inferno",
            colorbar=dict(
                title=dict(text="T [°C]", font=dict(color="#8898c0")),
                tickfont=dict(color="#8898c0"),
            ),
            hovertemplate=(
                "X: %{x:.2f} mm<br>"
                "Y: %{y:.2f} mm<br>"
                "T: %{z:.1f} °C<extra></extra>"
            ),
        )
    ])

    fig.update_layout(
        scene=dict(
            xaxis=dict(title="X [mm]", color="#607090", gridcolor="#1a2040",
                       backgroundcolor="rgba(8,10,22,0.95)"),
            yaxis=dict(title="Y [mm]", color="#607090", gridcolor="#1a2040",
                       backgroundcolor="rgba(8,10,22,0.95)"),
            zaxis=dict(title="Temperature [°C]", color="#607090", gridcolor="#1a2040",
                       backgroundcolor="rgba(8,10,22,0.95)"),
            bgcolor="rgba(8,10,22,0.95)",
            camera=dict(eye=dict(x=1.5, y=1.5, z=1.2)),
        ),
        paper_bgcolor="rgba(0,0,0,0)",
        margin=dict(l=0, r=0, t=50, b=0),
        title=dict(
            text=f"3D Surface Temperature — {data['material_name']}",
            font=dict(color="#8898c0", size=14),
        ),
    )
    return fig


def build_xz_section(t_field, x_mm, z_mm, data):
    """XZ temperature cross-section."""
    if t_field.ndim != 3:
        return empty_2d_figure("No 3D data")

    mid_y = t_field.shape[1] // 2
    cross = t_field[:, mid_y, :].T

    fig = go.Figure(data=[
        go.Heatmap(
            x=x_mm, y=z_mm, z=cross,
            colorscale="Hot", reversescale=False,
            colorbar=dict(title=dict(text="T [°C]", font=dict(color="#8898c0")),
                          tickfont=dict(color="#8898c0")),
            hovertemplate="X: %{x:.2f} mm<br>Z: %{y:.3f} mm<br>T: %{z:.1f} °C<extra></extra>",
        )
    ])

    fig.update_layout(
        xaxis=dict(title="X [mm]", color="#607090", gridcolor="#1a2040"),
        yaxis=dict(title="Depth Z [mm]", color="#607090", gridcolor="#1a2040", autorange="reversed"),
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#8898c0"),
        margin=dict(l=60, r=30, t=50, b=50),
        title=dict(text="XZ Cross-Section (mid-Y)", font=dict(size=14, color="#8898c0")),
    )
    return fig


def build_yz_section(t_field, y_mm, z_mm, data):
    """YZ temperature cross-section."""
    if t_field.ndim != 3:
        return empty_2d_figure("No 3D data")

    mid_x = t_field.shape[0] // 2
    cross = t_field[mid_x, :, :].T

    fig = go.Figure(data=[
        go.Heatmap(
            x=y_mm, y=z_mm, z=cross,
            colorscale="Hot", reversescale=False,
            colorbar=dict(title=dict(text="T [°C]", font=dict(color="#8898c0")),
                          tickfont=dict(color="#8898c0")),
            hovertemplate="Y: %{x:.2f} mm<br>Z: %{y:.3f} mm<br>T: %{z:.1f} °C<extra></extra>",
        )
    ])

    fig.update_layout(
        xaxis=dict(title="Y [mm]", color="#607090", gridcolor="#1a2040"),
        yaxis=dict(title="Depth Z [mm]", color="#607090", gridcolor="#1a2040", autorange="reversed"),
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#8898c0"),
        margin=dict(l=60, r=30, t=50, b=50),
        title=dict(text="YZ Cross-Section (mid-X)", font=dict(size=14, color="#8898c0")),
    )
    return fig


def build_fluence_map(data, x_mm, y_mm):
    """2D fluence heatmap."""
    fluence = np.array(data["fluence_map"])
    if fluence.ndim != 2 or fluence.size == 0:
        return empty_2d_figure("No fluence data")

    fig = go.Figure(data=[
        go.Heatmap(
            x=x_mm, y=y_mm, z=fluence.T,
            colorscale="Inferno",
            colorbar=dict(title=dict(text="J/m²", font=dict(color="#8898c0")),
                          tickfont=dict(color="#8898c0")),
            hovertemplate="X: %{x:.2f} mm<br>Y: %{y:.2f} mm<br>Fluence: %{z:.2e} J/m²<extra></extra>",
        ),
    ])

    fig.update_layout(
        xaxis=dict(title="X [mm]", color="#607090", gridcolor="#1a2040",
                   scaleanchor="y", scaleratio=1),
        yaxis=dict(title="Y [mm]", color="#607090", gridcolor="#1a2040"),
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#8898c0"),
        margin=dict(l=60, r=30, t=50, b=50),
        title=dict(text="Cumulative Fluence Map", font=dict(size=14, color="#8898c0")),
    )
    return fig


def build_depth_profile(t_field, data):
    """Temperature vs depth at hottest surface point."""
    if t_field.ndim != 3:
        return empty_2d_figure("No 3D data")

    z_um = np.array(data["z_coords"]) * 1e6
    surface = t_field[:, :, 0]
    hot_idx = np.unravel_index(np.argmax(surface), surface.shape)
    profile = t_field[hot_idx[0], hot_idx[1], :]

    fig = go.Figure()

    # Temperature curve
    fig.add_trace(go.Scatter(
        x=z_um.tolist(), y=profile.tolist(),
        mode="lines",
        line=dict(color="#ff6b6b", width=3),
        fill="tozeroy",
        fillcolor="rgba(255,107,107,0.08)",
        name="T(z)",
        hovertemplate="Depth: %{x:.0f} µm<br>T: %{y:.1f} °C<extra></extra>",
    ))

    # Melt line
    t_melt = data.get("t_melt", 0)
    if t_melt and max(profile) > t_melt * 0.5:
        fig.add_hline(
            y=t_melt, line_dash="dash", line_color="#ff9f43",
            annotation_text=f"T_melt = {t_melt}°C",
            annotation_font_color="#ff9f43",
            annotation_font_size=11,
        )

    # Vaporization line
    t_vap = data.get("t_vaporization", 0)
    if t_vap and max(profile) > t_vap * 0.5:
        fig.add_hline(
            y=t_vap, line_dash="dot", line_color="#ee5a24",
            annotation_text=f"T_vap = {t_vap}°C",
            annotation_font_color="#ee5a24",
            annotation_font_size=11,
        )

    fig.update_layout(
        xaxis=dict(title="Depth [µm]", color="#607090", gridcolor="#1a2040"),
        yaxis=dict(title="Temperature [°C]", color="#607090", gridcolor="#1a2040"),
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#8898c0"),
        margin=dict(l=60, r=30, t=50, b=50),
        title=dict(
            text="Depth Temperature Profile (Hottest Point)",
            font=dict(size=14, color="#8898c0"),
        ),
        showlegend=False,
    )
    return fig
