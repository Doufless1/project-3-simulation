"""
Lab Visualizations Module — Enhanced State-Reactive 3D.

Provides 3D visualizations for the Lab Control Dashboard.
Shows visual feedback for every lab action:
  - Gas flow: animated particle trail from tanks to chamber
  - Purging: swirling particles inside chamber
  - Laser beam: core beam + glow + impact spark
  - Chamber moves with X-Y table
  - Safety enclosure changes color (green=locked, red=open)
  - Extraction fan active when gas flows

Static geometry is cached at module level; only dynamic elements
are updated per callback (~18 static + ~5 dynamic traces max).
"""

import plotly.graph_objects as go
import numpy as np

# ── Palette ──────────────────────────────────────────────────────────
COLORS = {
    "bg": "#0a0e17",
    "floor": "#1e293b",
    "grid": "#334155",
    "table": "#94a3b8",
    "table_legs": "#475569",
    "stage": "#3b82f6",
    "chamber": "#f59e0b",
    "chamber_hot": "#ef4444",
    "laser_body": "#dc2626",
    "laser_beam": "#ef4444",
    "core_beam": "#ffffff",
    "beam_glow": "#ff6b6b",
    "gas": "#10b981",
    "gas_flow": "#34d399",
    "gas_purge": "#6ee7b7",
    "pc_body": "#1f2937",
    "pc_screen": "#8b5cf6",
    "extract": "#64748b",
    "extract_active": "#06b6d4",
    "cage_locked": "#10b981",
    "cage_open": "#ef4444",
    "cage_glass": "#06b6d4",
    "text": "#f8fafc",
    "hazard": "#eab308",
    "pipe": "#374151",
    "door_locked": "#10b981",
    "door_open": "#ef4444",
}

# ── Lighting presets ─────────────────────────────────────────────────
_LIT_METAL = dict(ambient=0.6, diffuse=0.8, specular=0.2, roughness=0.5, fresnel=0.2)
_LIT_SHINY = dict(ambient=0.6, diffuse=0.6, specular=0.8, roughness=0.4)
_LIT_MATTE = dict(ambient=0.5, diffuse=0.5, specular=0.1, roughness=0.8)
_LIT_GLASS = dict(ambient=0.5, diffuse=0.1, specular=1.0, roughness=0.0, fresnel=1.0)
_LIT_EMISSIVE = dict(ambient=0.9, diffuse=0.1, specular=1.0)
_LIT_GLOW = dict(ambient=1.0, diffuse=0.0, specular=0.0)


# ── Geometry Helpers ─────────────────────────────────────────────────

def _add_box(fig, name, x0, y0, z0, dx, dy, dz, color,
             opacity=1.0, showlegend=True, lighting=None):
    """Add a 3D box as a single Mesh3d trace."""
    x = [x0, x0+dx, x0+dx, x0,    x0, x0+dx, x0+dx, x0]
    y = [y0, y0,    y0+dy, y0+dy,  y0, y0,    y0+dy, y0+dy]
    z = [z0, z0,    z0,    z0,     z0+dz, z0+dz, z0+dz, z0+dz]
    i = [0,0,0,0,4,4,2,2,0,0,1,1]
    j = [1,2,4,5,5,6,3,6,1,3,2,6]
    k = [2,3,5,6,6,7,6,7,4,7,5,5]
    fig.add_trace(go.Mesh3d(
        x=x, y=y, z=z, i=i, j=j, k=k,
        color=color, opacity=opacity,
        name=name, showlegend=showlegend,
        lightposition=dict(x=10, y=10, z=100),
        lighting=lighting or _LIT_METAL,
        hovertemplate=f"<b>{name}</b><extra></extra>",
    ))


def _add_cylinder(fig, name, cx, cy, z_bot, z_top, radius, color,
                  res=16, opacity=1.0, showlegend=True, lighting=None):
    """Add a cylinder as a single Mesh3d trace (circles + sides)."""
    theta = np.linspace(0, 2 * np.pi, res, endpoint=False)
    cos_t = np.cos(theta)
    sin_t = np.sin(theta)

    # Bottom and top circle vertices
    xb = cx + radius * cos_t
    yb = cy + radius * sin_t
    zb = np.full(res, z_bot)
    xt = cx + radius * cos_t
    yt = cy + radius * sin_t
    zt = np.full(res, z_top)

    # Center points for caps
    x_all = np.concatenate([xb, xt, [cx, cx]])
    y_all = np.concatenate([yb, yt, [cy, cy]])
    z_all = np.concatenate([zb, zt, [z_bot, z_top]])
    bc = 2 * res      # bottom center index
    tc = 2 * res + 1   # top center index

    ii, jj, kk = [], [], []
    for idx in range(res):
        nxt = (idx + 1) % res
        # Side faces (two triangles per quad)
        ii += [idx, idx]
        jj += [nxt, idx + res]
        kk += [idx + res, nxt + res]
        ii += [nxt]
        jj += [nxt + res]
        kk += [idx + res]
        # Bottom cap
        ii.append(bc)
        jj.append(idx)
        kk.append(nxt)
        # Top cap
        ii.append(tc)
        jj.append(idx + res)
        kk.append(nxt + res)

    fig.add_trace(go.Mesh3d(
        x=x_all, y=y_all, z=z_all,
        i=ii, j=jj, k=kk,
        color=color, opacity=opacity,
        name=name, showlegend=showlegend,
        lightposition=dict(x=10, y=10, z=100),
        lighting=lighting or _LIT_METAL,
        hovertemplate=f"<b>{name}</b><extra></extra>",
    ))


# ── Shared layout config ────────────────────────────────────────────
_LAYOUT = dict(
    scene=dict(
        xaxis=dict(title="", range=[0, 6], showgrid=False,
                   zeroline=False, showbackground=False),
        yaxis=dict(title="", range=[0, 5], showgrid=False,
                   zeroline=False, showbackground=False),
        zaxis=dict(title="", range=[0, 2.5], showgrid=False,
                   zeroline=False, showbackground=False),
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
        x=0.02, y=0.98,
        bgcolor="rgba(0,0,0,0)",
        font=dict(color=COLORS["text"], size=10),
    ),
    uirevision="constant",
)


# ── Key lab coordinates (shared between static and dynamic) ──────────
TX, TY, TZ = 1.5, 1.5, 0.8     # Optical table origin
TW, TD, TH = 1.5, 1.0, 0.15    # Table width, depth, height
XY_Z = TZ + TH                   # Top of table
HEAD_X = TX + 0.75               # Laser head position
HEAD_Y = TY + 0.5
# Gas tank positions
TANK1_CX, TANK1_CY = 0.50, 4.00
TANK2_CX, TANK2_CY = 0.90, 4.00
TANK_R = 0.13
TANK_H = 1.4


def _build_static_traces():
    """Pre-build all static lab geometry (called once at import)."""
    fig = go.Figure()

    # 1. Floor — single surface
    x = np.linspace(0, 6, 2)
    y = np.linspace(0, 5, 2)
    X, Y = np.meshgrid(x, y)
    Z = np.zeros_like(X) - 0.05
    fig.add_trace(go.Surface(
        x=X, y=Y, z=Z,
        colorscale=[[0, COLORS["floor"]], [1, COLORS["floor"]]],
        showscale=False,
        lighting=dict(ambient=0.8, diffuse=0.8, specular=0.1, roughness=0.8),
        hoverinfo="none", name="Floor",
    ))

    # 2. Optical Table (brushed metal look)
    _add_box(fig, "Optical Table", TX, TY, TZ, TW, TD, TH,
             COLORS["table"],
             lighting=dict(ambient=0.5, diffuse=0.5, specular=0.6, roughness=0.3))

    # Table legs — 4 cylinders
    for lx, ly in [
        (TX + 0.1, TY + 0.1),
        (TX + TW - 0.1, TY + 0.1),
        (TX + 0.1, TY + TD - 0.1),
        (TX + TW - 0.1, TY + TD - 0.1),
    ]:
        _add_cylinder(fig, "Leg", lx, ly, 0, TZ, 0.04,
                      COLORS["table_legs"], res=8, showlegend=False)

    # 3. X-Y Stages base
    _add_box(fig, "X-Y Stages", TX + 0.2, TY + 0.1, XY_Z, 1.1, 0.8, 0.1,
             COLORS["stage"], lighting=_LIT_SHINY)

    # 4. Laser Head (fixed position, cylindrical)
    head_z = XY_Z + 0.1 + 0.3 + 0.4
    _add_cylinder(fig, "Laser Head",
                  HEAD_X, HEAD_Y, head_z, head_z + 0.25, 0.08,
                  COLORS["laser_body"], res=12, lighting=_LIT_MATTE)
    # Lens ring on laser head
    _add_cylinder(fig, "Lens", HEAD_X, HEAD_Y, head_z - 0.02, head_z + 0.02, 0.1,
                  "#374151", res=12, showlegend=False, lighting=_LIT_SHINY)

    # 5. Laser Source Rack
    _add_box(fig, "Laser Source", 0.4, 0.4, 0, 0.7, 0.7, 1.5,
             COLORS["laser_body"])
    # Fiber cable from rack to head (a thin line)
    fig.add_trace(go.Scatter3d(
        x=[0.75, 0.75, HEAD_X, HEAD_X],
        y=[0.75, 0.75, HEAD_Y, HEAD_Y],
        z=[1.5, 2.1, 2.1, head_z + 0.25],
        mode="lines",
        line=dict(color="#fbbf24", width=3),
        name="Fiber Cable", hoverinfo="none", showlegend=False,
    ))

    # 6. Gas Tanks — proper cylinders with valve tops
    for label, cx, cy in [("Argon Tank 1", TANK1_CX, TANK1_CY),
                          ("Argon Tank 2", TANK2_CX, TANK2_CY)]:
        _add_cylinder(fig, label, cx, cy, 0, TANK_H, TANK_R,
                      COLORS["gas"], res=12)
        # Valve dome on top
        _add_cylinder(fig, "Valve", cx, cy, TANK_H, TANK_H + 0.08, 0.05,
                      "#6b7280", res=8, showlegend=False)

    # Gas piping from tanks to table area (static pipe path)
    pipe_y = 3.5
    fig.add_trace(go.Scatter3d(
        x=[TANK1_CX, TANK1_CX, TX + 0.25, TX + 0.25],
        y=[TANK1_CY - TANK_R, pipe_y, pipe_y, TY + TD],
        z=[TANK_H * 0.8, TANK_H * 0.8, TANK_H * 0.8, XY_Z + 0.15],
        mode="lines",
        line=dict(color=COLORS["pipe"], width=4),
        name="Gas Pipe", hoverinfo="none", showlegend=False,
    ))

    # 7. Control Station
    _add_box(fig, "Control Desk", 4.5, 1.0, 0, 1.2, 0.8, 0.8,
             COLORS["table_legs"])
    # Monitor as thin box with emissive screen
    _add_box(fig, "PC Monitor", 4.8, 1.1, 0.8, 0.6, 0.05, 0.4,
             COLORS["pc_screen"], lighting=_LIT_EMISSIVE)
    _add_box(fig, "PC Tower", 5.4, 1.1, 0, 0.2, 0.5, 0.5,
             COLORS["pc_body"])

    # 8. Extraction Unit (tall cylinder)
    _add_cylinder(fig, "Extraction Unit", 4.9, 4.4, 0, 2.0, 0.35,
                  COLORS["extract"], res=12)
    # Extraction duct from table area to unit
    fig.add_trace(go.Scatter3d(
        x=[TX + TW + 0.2, TX + TW + 0.5, 4.9, 4.9],
        y=[TY + 0.5, TY + 0.5, 4.0, 4.4],
        z=[XY_Z + 0.6, 1.8, 1.8, 2.0],
        mode="lines",
        line=dict(color=COLORS["extract"], width=3),
        name="Duct", hoverinfo="none", showlegend=False,
    ))

    return fig.data


# ── Module-level cache (built once) ──────────────────────────────────
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
    Build 3D lab figure with full state-reactive visualization.

    Static geometry is cached; dynamic elements change per call:
      - Processing chamber moves with X-Y table
      - Laser beam + glow + spark when firing
      - Gas flow particles from tanks to chamber
      - Purge swirl inside chamber
      - Safety enclosure color = green (locked) / red (open)
      - E-stop flashes the enclosure red
    """
    fig = go.Figure()
    fig.add_traces(_STATIC_TRACES)

    # ── Dynamic: Safety Enclosure (color changes with state) ─────────
    all_locked = door_locked and chamber_locked and not e_stop
    if e_stop:
        cage_color = COLORS["cage_open"]
        cage_opacity = 0.25
    elif all_locked:
        cage_color = COLORS["cage_locked"]
        cage_opacity = 0.08
    else:
        cage_color = COLORS["cage_open"]
        cage_opacity = 0.12

    _add_box(fig, "Safety Enclosure",
             TX - 0.2, TY - 0.2, 0, TW + 0.4, TD + 0.4, 2.2,
             cage_color, opacity=cage_opacity, lighting=_LIT_GLASS)

    # ── Dynamic: Processing Chamber (moves with X-Y table) ───────────
    cham_x = TX + 0.5 + pos_x_mm / 1000.0
    cham_y = TY + 0.3 + pos_y_mm / 1000.0
    cham_z = XY_Z + 0.1

    # Chamber color reflects state
    if laser_active:
        cham_color = COLORS["chamber_hot"]
    elif gas_purging:
        cham_color = COLORS["gas_purge"]
    else:
        cham_color = COLORS["chamber"]

    _add_box(fig, "Processing Chamber",
             cham_x, cham_y, cham_z, 0.5, 0.4, 0.3,
             cham_color,
             lighting=dict(ambient=0.6, diffuse=0.8, specular=0.9, roughness=0.2))

    # ── Dynamic: Gas Flow Visualization ──────────────────────────────
    if gas_flowing or gas_purging:
        # Flow intensity (more particles for higher flow)
        n_particles = int(6 + gas_flow_rate * 0.8)
        np.random.seed(42)  # Deterministic positions for consistency

        # Particles along pipe from tank to chamber
        t_param = np.linspace(0.05, 0.95, n_particles)
        # Path: tank valve → pipe bend → chamber inlet
        pipe_xs = TANK1_CX + t_param * (cham_x + 0.1 - TANK1_CX)
        pipe_ys = TANK1_CY - TANK_R + t_param * (cham_y + 0.2 - (TANK1_CY - TANK_R))
        pipe_zs = TANK_H * 0.8 + t_param * (cham_z + 0.15 - TANK_H * 0.8)
        # Add slight randomness for natural look
        pipe_xs += np.random.uniform(-0.03, 0.03, n_particles)
        pipe_ys += np.random.uniform(-0.03, 0.03, n_particles)

        flow_color = COLORS["gas_purge"] if gas_purging else COLORS["gas_flow"]
        particle_size = 4 if gas_purging else 3

        fig.add_trace(go.Scatter3d(
            x=pipe_xs, y=pipe_ys, z=pipe_zs,
            mode="markers",
            marker=dict(
                size=particle_size,
                color=flow_color,
                opacity=0.7,
                symbol="circle",
            ),
            name="Gas Flow",
            hovertemplate="Ar flow<extra></extra>",
        ))

        # Purge: swirling particles inside the chamber
        if gas_purging:
            n_swirl = 12
            angles = np.linspace(0, 4 * np.pi, n_swirl)
            r_swirl = 0.12
            sx = cham_x + 0.25 + r_swirl * np.cos(angles)
            sy = cham_y + 0.2 + r_swirl * np.sin(angles)
            sz = cham_z + 0.05 + np.linspace(0.02, 0.25, n_swirl)

            fig.add_trace(go.Scatter3d(
                x=sx, y=sy, z=sz,
                mode="markers+lines",
                marker=dict(size=3, color=COLORS["gas_purge"], opacity=0.6),
                line=dict(color=COLORS["gas_purge"], width=2),
                name="Purge Swirl",
                hovertemplate="Purging O₂<extra></extra>",
            ))

    # ── Dynamic: Laser Beam ──────────────────────────────────────────
    if laser_active:
        head_z = cham_z + 0.7
        beam_bottom = cham_z + 0.3

        # Outer glow beam (wider, dimmer)
        beam_width = max(3, laser_power_pct / 10)
        fig.add_trace(go.Scatter3d(
            x=[HEAD_X, HEAD_X],
            y=[HEAD_Y, HEAD_Y],
            z=[head_z, beam_bottom],
            mode="lines",
            line=dict(color=COLORS["beam_glow"], width=beam_width + 4),
            name="Beam Glow", hoverinfo="none",
            showlegend=False, opacity=0.3,
        ))

        # Core beam (bright, thin)
        fig.add_trace(go.Scatter3d(
            x=[HEAD_X, HEAD_X],
            y=[HEAD_Y, HEAD_Y],
            z=[head_z, beam_bottom],
            mode="lines",
            line=dict(color=COLORS["core_beam"], width=beam_width),
            name="Laser Beam", hoverinfo="none",
        ))

        # Impact zone — spark + heat glow
        fig.add_trace(go.Scatter3d(
            x=[HEAD_X, HEAD_X - 0.02, HEAD_X + 0.02, HEAD_X, HEAD_X],
            y=[HEAD_Y, HEAD_Y - 0.02, HEAD_Y + 0.02, HEAD_Y + 0.01, HEAD_Y - 0.01],
            z=[beam_bottom, beam_bottom + 0.02, beam_bottom + 0.03,
               beam_bottom + 0.04, beam_bottom + 0.02],
            mode="markers",
            marker=dict(
                size=[10, 5, 5, 4, 4],
                color=["#ffff00", "#ff8c00", "#ff6347", "#ff4500", "#ffa500"],
                opacity=0.9,
                symbol="diamond",
            ),
            name="Plasma Spark", hoverinfo="none",
        ))

    # ── Dynamic: E-Stop Warning ──────────────────────────────────────
    if e_stop:
        # Big red warning markers at the four corners of the enclosure
        corners_x = [TX - 0.2, TX + TW + 0.2, TX - 0.2, TX + TW + 0.2]
        corners_y = [TY - 0.2, TY - 0.2, TY + TD + 0.2, TY + TD + 0.2]
        corners_z = [2.2, 2.2, 2.2, 2.2]
        fig.add_trace(go.Scatter3d(
            x=corners_x, y=corners_y, z=corners_z,
            mode="markers+text",
            marker=dict(size=12, color="#ef4444", symbol="x", opacity=0.9),
            text=["⚠", "⚠", "⚠", "⚠"],
            textposition="top center",
            textfont=dict(size=14, color="#ef4444"),
            name="E-STOP", hovertemplate="EMERGENCY STOP<extra></extra>",
        ))

    # ── Apply layout ─────────────────────────────────────────────────
    fig.update_layout(**_LAYOUT)
    return fig
