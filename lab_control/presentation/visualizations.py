"""
Lab Visualizations Module — Smooth State-Reactive 3D.

Provides 3D visualizations for the Lab Control Dashboard.
Optimized for smooth rotation/panning and clean state transitions:
  - Low-poly cylinders (8 segments) for responsive WebGL
  - Fixed particle positions (no randomness) for consistency
  - Soft color palette for state changes (no harsh flashing)
  - uirevision preserves camera between updates
  - Transition duration for smooth figure swaps

Static geometry cached at module level; dynamic elements update per call.
"""

import plotly.graph_objects as go
import numpy as np

# ── Palette (softer tones for smooth transitions) ────────────────────
COLORS = {
    "bg": "#0a0e17",
    "floor": "#1a2332",
    "table": "#8899aa",
    "table_legs": "#475569",
    "stage": "#3b82f6",
    "chamber_idle": "#f5a623",
    "chamber_laser": "#e85d4a",
    "chamber_purge": "#5cb8a5",
    "laser_body": "#c43e3e",
    "laser_beam_core": "#ffffff",
    "beam_glow": "rgba(255,100,100,0.3)",
    "spark": "#ffcc44",
    "gas_tank": "#2d9d78",
    "gas_flow": "#4ae0b5",
    "purge_swirl": "#7af0d0",
    "pipe": "#3a4556",
    "pc_body": "#1f2937",
    "pc_screen": "#7c5cbf",
    "extract": "#5a6a7a",
    "extract_on": "#38a5c4",
    "cage_safe": "rgba(16,185,129,0.07)",
    "cage_warn": "rgba(239,68,68,0.12)",
    "cage_estop": "rgba(239,68,68,0.22)",
    "text": "#f0f4f8",
    "fiber": "#d4a846",
    "valve": "#71787f",
}

# ── Lighting presets ─────────────────────────────────────────────────
_LIT_METAL = dict(ambient=0.6, diffuse=0.8, specular=0.2, roughness=0.5, fresnel=0.2)
_LIT_SHINY = dict(ambient=0.6, diffuse=0.6, specular=0.8, roughness=0.4)
_LIT_MATTE = dict(ambient=0.5, diffuse=0.5, specular=0.1, roughness=0.8)
_LIT_GLASS = dict(ambient=0.4, diffuse=0.1, specular=0.8, roughness=0.0, fresnel=0.8)
_LIT_EMISSIVE = dict(ambient=0.9, diffuse=0.1, specular=1.0)


# ── Geometry Helpers ─────────────────────────────────────────────────

def _box(name, x0, y0, z0, dx, dy, dz, color,
         opacity=1.0, showlegend=True, lighting=None):
    """Return a Mesh3d box trace (not added to fig — for batching)."""
    x = [x0, x0+dx, x0+dx, x0, x0, x0+dx, x0+dx, x0]
    y = [y0, y0, y0+dy, y0+dy, y0, y0, y0+dy, y0+dy]
    z = [z0, z0, z0, z0, z0+dz, z0+dz, z0+dz, z0+dz]
    return go.Mesh3d(
        x=x, y=y, z=z,
        i=[0,0,0,0,4,4,2,2,0,0,1,1],
        j=[1,2,4,5,5,6,3,6,1,3,2,6],
        k=[2,3,5,6,6,7,6,7,4,7,5,5],
        color=color, opacity=opacity,
        name=name, showlegend=showlegend,
        lightposition=dict(x=10, y=10, z=100),
        lighting=lighting or _LIT_METAL,
        hovertemplate=f"<b>{name}</b><extra></extra>",
    )


def _cylinder(name, cx, cy, z0, z1, r, color,
              n=8, opacity=1.0, showlegend=True, lighting=None):
    """Return a Mesh3d cylinder trace (low-poly, 8 segments default)."""
    θ = np.linspace(0, 2 * np.pi, n, endpoint=False)
    cos, sin = np.cos(θ), np.sin(θ)
    # Vertices: bottom ring, top ring, center bottom, center top
    x = np.concatenate([cx + r*cos, cx + r*cos, [cx, cx]])
    y = np.concatenate([cy + r*sin, cy + r*sin, [cy, cy]])
    z = np.concatenate([np.full(n, z0), np.full(n, z1), [z0, z1]])
    bc, tc = 2*n, 2*n + 1
    ii, jj, kk = [], [], []
    for i in range(n):
        nx = (i + 1) % n
        # Side quads (2 triangles)
        ii += [i, i, nx]; jj += [nx, i+n, nx+n]; kk += [i+n, nx+n, i+n]
        # Caps
        ii += [bc, tc]; jj += [i, i+n]; kk += [nx, nx+n]
    return go.Mesh3d(
        x=x, y=y, z=z, i=ii, j=jj, k=kk,
        color=color, opacity=opacity,
        name=name, showlegend=showlegend,
        lightposition=dict(x=10, y=10, z=100),
        lighting=lighting or _LIT_METAL,
        hovertemplate=f"<b>{name}</b><extra></extra>",
    )


# ── Key lab coordinates ──────────────────────────────────────────────
TX, TY, TZ = 1.5, 1.5, 0.8
TW, TD, TH = 1.5, 1.0, 0.15
XY_Z = TZ + TH
HEAD_X, HEAD_Y = TX + 0.75, TY + 0.5
HEAD_Z = XY_Z + 0.1 + 0.3 + 0.4  # above chamber
T1X, T1Y = 0.50, 4.00  # Tank 1
T2X, T2Y = 0.90, 4.00  # Tank 2
TANK_R, TANK_H = 0.13, 1.4

# Pre-computed gas flow path (8 fixed waypoints, no randomness)
_FLOW_PATH = np.array([
    [T1X, T1Y - TANK_R, TANK_H * 0.8],
    [T1X, 3.5, TANK_H * 0.8],
    [T1X + 0.3, 3.2, TANK_H * 0.7],
    [TX, 2.8, XY_Z + 0.5],
    [TX + 0.2, 2.4, XY_Z + 0.4],
    [TX + 0.4, 2.1, XY_Z + 0.3],
    [TX + 0.6, 1.9, XY_Z + 0.2],
    [TX + 0.75, TY + 0.5, XY_Z + 0.15],
])


# ── Layout (shared, constant) ───────────────────────────────────────
_LAYOUT = dict(
    scene=dict(
        xaxis=dict(title="", range=[0, 6], showgrid=False,
                   zeroline=False, showbackground=False, showticklabels=False),
        yaxis=dict(title="", range=[0, 5], showgrid=False,
                   zeroline=False, showbackground=False, showticklabels=False),
        zaxis=dict(title="", range=[0, 2.5], showgrid=False,
                   zeroline=False, showbackground=False, showticklabels=False),
        aspectratio=dict(x=1.2, y=1, z=0.5),
        camera=dict(
            eye=dict(x=1.5, y=-1.5, z=0.8),
            center=dict(x=0, y=0, z=-0.2),
        ),
        bgcolor=COLORS["bg"],
    ),
    paper_bgcolor=COLORS["bg"],
    plot_bgcolor=COLORS["bg"],
    margin=dict(l=0, r=0, t=0, b=0),
    showlegend=True,
    legend=dict(
        x=0.01, y=0.99,
        bgcolor="rgba(10,14,23,0.7)",
        bordercolor="rgba(255,255,255,0.1)",
        borderwidth=1,
        font=dict(color=COLORS["text"], size=10),
    ),
    uirevision="lab-3d",  # Preserves camera angle between updates
)


def _build_static_traces():
    """Pre-build all static lab geometry (called once at import)."""
    traces = []

    # 1. Floor
    x = np.linspace(0, 6, 2)
    y = np.linspace(0, 5, 2)
    X, Y = np.meshgrid(x, y)
    traces.append(go.Surface(
        x=X, y=Y, z=np.zeros_like(X) - 0.05,
        colorscale=[[0, COLORS["floor"]], [1, COLORS["floor"]]],
        showscale=False,
        lighting=dict(ambient=0.8, diffuse=0.8, specular=0.1, roughness=0.8),
        hoverinfo="none", name="Floor",
    ))

    # 2. Optical Table
    traces.append(_box("Optical Table", TX, TY, TZ, TW, TD, TH,
                       COLORS["table"],
                       lighting=dict(ambient=0.5, diffuse=0.5, specular=0.6, roughness=0.3)))

    # Table legs (4 low-poly cylinders)
    for lx, ly in [(TX+0.1, TY+0.1), (TX+TW-0.1, TY+0.1),
                   (TX+0.1, TY+TD-0.1), (TX+TW-0.1, TY+TD-0.1)]:
        traces.append(_cylinder("Leg", lx, ly, 0, TZ, 0.04,
                                COLORS["table_legs"], n=6, showlegend=False))

    # 3. X-Y Stages
    traces.append(_box("X-Y Stages", TX+0.2, TY+0.1, XY_Z, 1.1, 0.8, 0.1,
                       COLORS["stage"], lighting=_LIT_SHINY))

    # 4. Laser Head (cylinder + lens ring)
    traces.append(_cylinder("Laser Head", HEAD_X, HEAD_Y,
                            HEAD_Z, HEAD_Z + 0.25, 0.08,
                            COLORS["laser_body"], n=8, lighting=_LIT_MATTE))
    traces.append(_cylinder("Lens", HEAD_X, HEAD_Y,
                            HEAD_Z - 0.02, HEAD_Z + 0.02, 0.10,
                            COLORS["pipe"], n=8, showlegend=False, lighting=_LIT_SHINY))

    # 5. Laser Source Rack
    traces.append(_box("Laser Source", 0.4, 0.4, 0, 0.7, 0.7, 1.5,
                       COLORS["laser_body"]))

    # Fiber cable (rack → head)
    traces.append(go.Scatter3d(
        x=[0.75, 0.75, HEAD_X, HEAD_X],
        y=[0.75, 0.75, HEAD_Y, HEAD_Y],
        z=[1.5, 2.1, 2.1, HEAD_Z + 0.25],
        mode="lines",
        line=dict(color=COLORS["fiber"], width=3),
        name="Fiber Cable", hoverinfo="none", showlegend=False,
    ))

    # 6. Gas Tanks (cylinders + valve caps)
    for label, cx, cy in [("Argon Tank 1", T1X, T1Y),
                          ("Argon Tank 2", T2X, T2Y)]:
        traces.append(_cylinder(label, cx, cy, 0, TANK_H, TANK_R,
                                COLORS["gas_tank"], n=8))
        traces.append(_cylinder("Valve", cx, cy, TANK_H, TANK_H + 0.08, 0.05,
                                COLORS["valve"], n=6, showlegend=False))

    # Gas piping (fixed path)
    traces.append(go.Scatter3d(
        x=_FLOW_PATH[:, 0], y=_FLOW_PATH[:, 1], z=_FLOW_PATH[:, 2],
        mode="lines",
        line=dict(color=COLORS["pipe"], width=3),
        name="Gas Pipe", hoverinfo="none", showlegend=False,
    ))

    # 7. Control Station
    traces.append(_box("Control Desk", 4.5, 1.0, 0, 1.2, 0.8, 0.8,
                       COLORS["table_legs"]))
    traces.append(_box("Monitor", 4.8, 1.1, 0.8, 0.6, 0.05, 0.4,
                       COLORS["pc_screen"], lighting=_LIT_EMISSIVE))
    traces.append(_box("PC Tower", 5.4, 1.1, 0, 0.2, 0.5, 0.5,
                       COLORS["pc_body"], showlegend=False))

    # 8. Extraction Unit (cylinder)
    traces.append(_cylinder("Extraction", 4.9, 4.4, 0, 2.0, 0.35,
                            COLORS["extract"], n=8))

    # Duct line
    traces.append(go.Scatter3d(
        x=[TX+TW+0.2, TX+TW+0.5, 4.9, 4.9],
        y=[TY+0.5, TY+0.5, 4.0, 4.4],
        z=[XY_Z+0.6, 1.8, 1.8, 2.0],
        mode="lines",
        line=dict(color=COLORS["extract"], width=2),
        name="Duct", hoverinfo="none", showlegend=False,
    ))

    return traces


# ── Module-level cache ───────────────────────────────────────────────
_STATIC_TRACES = tuple(_build_static_traces())


def build_3d_lab_figure(
    pos_x_mm=0.0,
    pos_y_mm=0.0,
    laser_active=False,
    laser_power_pct=0.0,
    gas_flowing=False,
    gas_flow_rate=0.0,
    gas_purging=False,
    door_locked=False,
    chamber_locked=False,
    e_stop=False,
):
    """
    Build 3D lab figure with smooth state-reactive visualization.

    Static geometry is cached; dynamic elements added per call:
      - Processing chamber (position + color)
      - Safety enclosure (color = state)
      - Gas flow dots along fixed pipe path
      - Purge swirl inside chamber
      - Laser beam (core + glow + spark)
      - E-stop corner markers
    """
    fig = go.Figure()
    fig.add_traces(_STATIC_TRACES)

    # ── Safety Enclosure (color reflects state) ──────────────────────
    all_safe = door_locked and chamber_locked and not e_stop
    if e_stop:
        cage_color, cage_op = COLORS["cage_estop"], 0.20
    elif all_safe:
        cage_color, cage_op = COLORS["cage_safe"], 0.06
    else:
        cage_color, cage_op = COLORS["cage_warn"], 0.10

    fig.add_trace(go.Mesh3d(
        x=[TX-0.2, TX+TW+0.2, TX+TW+0.2, TX-0.2, TX-0.2, TX+TW+0.2, TX+TW+0.2, TX-0.2],
        y=[TY-0.2, TY-0.2, TY+TD+0.2, TY+TD+0.2, TY-0.2, TY-0.2, TY+TD+0.2, TY+TD+0.2],
        z=[0,0,0,0, 2.2,2.2,2.2,2.2],
        i=[0,0,0,0,4,4,2,2,0,0,1,1],
        j=[1,2,4,5,5,6,3,6,1,3,2,6],
        k=[2,3,5,6,6,7,6,7,4,7,5,5],
        color=cage_color, opacity=cage_op,
        name="Safety Enclosure", showlegend=True,
        lighting=_LIT_GLASS,
        hovertemplate="<b>Safety Enclosure</b><extra></extra>",
    ))

    # ── Processing Chamber (moves with X-Y) ──────────────────────────
    cx = TX + 0.5 + pos_x_mm / 1000.0
    cy = TY + 0.3 + pos_y_mm / 1000.0
    cz = XY_Z + 0.1

    if laser_active:
        c_color = COLORS["chamber_laser"]
    elif gas_purging:
        c_color = COLORS["chamber_purge"]
    else:
        c_color = COLORS["chamber_idle"]

    fig.add_trace(_box("Processing Chamber", cx, cy, cz, 0.5, 0.4, 0.3,
                       c_color,
                       lighting=dict(ambient=0.6, diffuse=0.8, specular=0.9, roughness=0.2)))

    # ── Gas Flow (dots along fixed pipe path) ────────────────────────
    if gas_flowing or gas_purging:
        flow_color = COLORS["purge_swirl"] if gas_purging else COLORS["gas_flow"]
        # Scale number of dots with flow rate (min 4, max 10)
        n_dots = min(10, max(4, int(gas_flow_rate / 3)))
        # Evenly spaced along the pre-computed path
        t = np.linspace(0, len(_FLOW_PATH) - 1, n_dots)
        idxs = t.astype(int)
        fracs = t - idxs
        idxs_next = np.minimum(idxs + 1, len(_FLOW_PATH) - 1)
        # Interpolate positions along path
        pts = _FLOW_PATH[idxs] * (1 - fracs[:, None]) + _FLOW_PATH[idxs_next] * fracs[:, None]

        fig.add_trace(go.Scatter3d(
            x=pts[:, 0], y=pts[:, 1], z=pts[:, 2],
            mode="markers",
            marker=dict(size=4, color=flow_color, opacity=0.75, symbol="circle"),
            name="Gas Flow",
            hovertemplate=f"Argon {gas_flow_rate:.1f} L/min<extra></extra>",
        ))

        # Purge: gentle spiral inside chamber
        if gas_purging:
            n_s = 8
            a = np.linspace(0, 3 * np.pi, n_s)
            fig.add_trace(go.Scatter3d(
                x=cx + 0.25 + 0.10 * np.cos(a),
                y=cy + 0.20 + 0.10 * np.sin(a),
                z=cz + np.linspace(0.03, 0.27, n_s),
                mode="lines+markers",
                line=dict(color=COLORS["purge_swirl"], width=2),
                marker=dict(size=3, color=COLORS["purge_swirl"], opacity=0.6),
                name="Purge",
                hovertemplate="Purging chamber<extra></extra>",
            ))

    # ── Laser Beam ───────────────────────────────────────────────────
    if laser_active:
        beam_top = HEAD_Z
        beam_bot = cz + 0.3
        w = max(3, laser_power_pct / 12)

        # Glow (wide, transparent)
        fig.add_trace(go.Scatter3d(
            x=[HEAD_X, HEAD_X], y=[HEAD_Y, HEAD_Y], z=[beam_top, beam_bot],
            mode="lines",
            line=dict(color=COLORS["beam_glow"], width=w + 6),
            name="Beam Glow", showlegend=False, hoverinfo="none",
        ))
        # Core (bright, thin)
        fig.add_trace(go.Scatter3d(
            x=[HEAD_X, HEAD_X], y=[HEAD_Y, HEAD_Y], z=[beam_top, beam_bot],
            mode="lines",
            line=dict(color=COLORS["laser_beam_core"], width=w),
            name="Laser Beam", hoverinfo="none",
        ))
        # Spark cluster at impact
        fig.add_trace(go.Scatter3d(
            x=[HEAD_X, HEAD_X-0.015, HEAD_X+0.015, HEAD_X+0.01, HEAD_X-0.01],
            y=[HEAD_Y, HEAD_Y-0.015, HEAD_Y+0.015, HEAD_Y+0.01, HEAD_Y-0.01],
            z=[beam_bot, beam_bot+0.02, beam_bot+0.025, beam_bot+0.035, beam_bot+0.015],
            mode="markers",
            marker=dict(size=[8, 4, 4, 3, 3],
                        color=[COLORS["spark"], "#ff9933", "#ff7733", "#ff6622", "#ffaa44"],
                        opacity=0.85),
            name="Spark", hoverinfo="none",
        ))

    # ── E-Stop Markers ───────────────────────────────────────────────
    if e_stop:
        ex = [TX-0.15, TX+TW+0.15, TX+TW/2]
        ey = [TY+TD/2, TY+TD/2, TY-0.15]
        ez = [2.0, 2.0, 2.0]
        fig.add_trace(go.Scatter3d(
            x=ex, y=ey, z=ez,
            mode="markers+text",
            marker=dict(size=10, color="#ef4444", symbol="x", opacity=0.85),
            text=["E-STOP", "", ""],
            textposition="top center",
            textfont=dict(size=12, color="#ef4444"),
            name="E-STOP",
            hovertemplate="EMERGENCY STOP ACTIVE<extra></extra>",
        ))

    # ── Apply layout ─────────────────────────────────────────────────
    fig.update_layout(**_LAYOUT)
    return fig
