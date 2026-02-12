"""
Modern Web-Based 3D Simulation GUI — Dash + Plotly.

A sleek, browser-based simulation lab with:
- Interactive 3D temperature fields (WebGL — orbit/zoom/pan)
- Material selector with ULTRA-C-Ta-WH-3849
- Laser parameter controls (power, spot radius, beam profile)
- Motion controls (speed, pattern)
- Real-time simulation results
- Modern dark theme with glassmorphism

Usage:
    python run_gui.py
    → Opens http://localhost:8050 in your browser
"""

import os
import sys
import json
import webbrowser
import threading
import traceback

import numpy as np
import plotly.graph_objects as go
from dash import Dash, html, dcc, callback, Input, Output, State, no_update
import dash_bootstrap_components as dbc

# Add project root
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from domain.entities import Material, LaserBeam
from infrastructure.laser.gaussian_source import GaussianLaserSource
from infrastructure.laser.tophat_source import TopHatLaserSource
from infrastructure.motion.raster_generator import RasterGenerator
from infrastructure.motion.spiral_generator import SpiralGenerator
from infrastructure.motion.linear_generator import LinearGenerator
from infrastructure.solvers.fdm_heat_solver_3d import FDMHeatSolver3D
from infrastructure.solvers.analytical_solver import AnalyticalHeatSolver
from infrastructure.logging.audit_logger import AuditLogger
from application.simulation_use_case import SimulationUseCase


# ============================================================================
# Material Library
# ============================================================================

MATERIALS = {
    "ultra_c": {
        "label": "ULTRA-C-Ta-WH-3849",
        "obj": Material(
            name="ULTRA-C-Ta-WH-3849",
            absorption=0.70, thermal_conductivity=60.0,
            density=9939.0, specific_heat=386.0,
            t_ambient=20.0, t_melt=3376.0, t_vaporization=4800.0,
        ),
        "composition": "C 36.2% · Ta 22.3% · W 19.4% · Cr 17.5% · Nb 4.4% · Hf 0.2%",
    },
    "ss316l": {
        "label": "316L Stainless Steel",
        "obj": Material(
            name="316L Stainless Steel",
            absorption=0.35, thermal_conductivity=15.0,
            density=8000.0, specific_heat=500.0,
            t_ambient=20.0, t_melt=1400.0, t_vaporization=2800.0,
        ),
        "composition": "Fe 65% · Cr 17% · Ni 12% · Mo 2.5%",
    },
    "wc_co": {
        "label": "WC-12Co (HVOF)",
        "obj": Material(
            name="WC-12Co (HVOF Coating)",
            absorption=0.55, thermal_conductivity=80.0,
            density=14500.0, specific_heat=240.0,
            t_ambient=20.0, t_melt=2870.0, t_vaporization=6000.0,
        ),
        "composition": "WC 88% · Co 12%",
    },
}


# ============================================================================
# Plotly Dark Theme Template
# ============================================================================

PLOT_TEMPLATE = dict(
    layout=go.Layout(
        paper_bgcolor="rgba(0,0,0,0)",
        plot_bgcolor="rgba(10,10,25,0.85)",
        font=dict(color="#c0c8e0", family="Inter, system-ui, sans-serif"),
        margin=dict(l=50, r=30, t=50, b=50),
        coloraxis=dict(colorbar=dict(
            tickfont=dict(color="#c0c8e0"),
            title=dict(font=dict(color="#c0c8e0")),
        )),
    )
)


# ============================================================================
# CSS Styles (inline for zero dependencies)
# ============================================================================

CARD_STYLE = {
    "background": "rgba(20, 25, 45, 0.85)",
    "backdropFilter": "blur(12px)",
    "border": "1px solid rgba(100, 140, 255, 0.12)",
    "borderRadius": "14px",
    "padding": "20px",
    "marginBottom": "16px",
    "boxShadow": "0 4px 30px rgba(0,0,0,0.3)",
}

SECTION_TITLE_STYLE = {
    "color": "#64b5f6",
    "fontSize": "13px",
    "fontWeight": "700",
    "textTransform": "uppercase",
    "letterSpacing": "1.5px",
    "marginBottom": "14px",
}

SLIDER_LABEL_STYLE = {
    "color": "#8898c0",
    "fontSize": "12px",
    "fontWeight": "500",
    "marginBottom": "2px",
    "marginTop": "10px",
}

VALUE_STYLE = {
    "color": "#e0e8ff",
    "fontSize": "22px",
    "fontWeight": "700",
    "fontFamily": "JetBrains Mono, Consolas, monospace",
}

RESULT_CARD_STYLE = {
    **CARD_STYLE,
    "background": "rgba(20, 30, 55, 0.9)",
    "border": "1px solid rgba(100, 200, 255, 0.15)",
}

METRIC_LABEL = {
    "color": "#7088b0",
    "fontSize": "11px",
    "fontWeight": "600",
    "textTransform": "uppercase",
    "letterSpacing": "0.8px",
}

METRIC_VALUE = {
    "color": "#e0f0ff",
    "fontSize": "20px",
    "fontWeight": "700",
    "fontFamily": "JetBrains Mono, Consolas, monospace",
}


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


# ============================================================================
# Dash App
# ============================================================================

app = Dash(
    __name__,
    external_stylesheets=[
        dbc.themes.DARKLY,
        "https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;600;700&display=swap",
    ],
    title="Laser-HVOF 3D Simulation Lab",
    suppress_callback_exceptions=True,
)


# ============================================================================
# Layout
# ============================================================================

app.layout = html.Div(
    style={
        "background": "linear-gradient(135deg, #0a0e1a 0%, #0d1529 50%, #0a1020 100%)",
        "minHeight": "100vh",
        "fontFamily": "Inter, system-ui, -apple-system, sans-serif",
    },
    children=[
        # ---- Header ----
        html.Div(
            style={
                "padding": "20px 32px",
                "display": "flex",
                "alignItems": "center",
                "justifyContent": "space-between",
                "borderBottom": "1px solid rgba(100,140,255,0.08)",
            },
            children=[
                html.Div([
                    html.H1("Laser-HVOF 3D Simulation", style={
                        "color": "#e0e8ff",
                        "fontSize": "24px",
                        "fontWeight": "700",
                        "margin": "0",
                        "letterSpacing": "-0.5px",
                    }),
                    html.P("Interactive simulation lab  ·  Clean Architecture", style={
                        "color": "#506888",
                        "fontSize": "12px",
                        "margin": "4px 0 0 0",
                        "letterSpacing": "0.5px",
                    }),
                ]),
                html.Div(
                    id="status-badge",
                    children="Ready",
                    style={
                        "background": "rgba(80,200,100,0.15)",
                        "color": "#50c864",
                        "padding": "6px 16px",
                        "borderRadius": "20px",
                        "fontSize": "12px",
                        "fontWeight": "600",
                    },
                ),
            ],
        ),

        # ---- Main Grid ----
        html.Div(
            style={
                "display": "grid",
                "gridTemplateColumns": "320px 1fr",
                "gap": "20px",
                "padding": "20px 24px",
                "maxHeight": "calc(100vh - 90px)",
                "overflow": "hidden",
            },
            children=[
                # ==== Left Panel: Controls ====
                html.Div(
                    style={
                        "overflowY": "auto",
                        "maxHeight": "calc(100vh - 130px)",
                        "paddingRight": "8px",
                    },
                    children=[
                        # Material Card
                        html.Div(style=CARD_STYLE, children=[
                            html.Div("Material", style=SECTION_TITLE_STYLE),
                            dcc.Dropdown(
                                id="material-select",
                                options=[
                                    {"label": v["label"], "value": k}
                                    for k, v in MATERIALS.items()
                                ],
                                value="ultra_c",
                                clearable=False,
                                style={"marginBottom": "10px"},
                                className="dash-dropdown-dark",
                            ),
                            html.Div(id="material-info", style={
                                "color": "#607898",
                                "fontSize": "11px",
                                "lineHeight": "1.6",
                                "padding": "8px 12px",
                                "background": "rgba(255,255,255,0.03)",
                                "borderRadius": "8px",
                            }),
                        ]),

                        # Laser Card
                        html.Div(style=CARD_STYLE, children=[
                            html.Div("Laser", style=SECTION_TITLE_STYLE),

                            html.Div("Power", style=SLIDER_LABEL_STYLE),
                            html.Div(id="power-value", children="500 W", style=VALUE_STYLE),
                            dcc.Slider(
                                id="power-slider", min=50, max=5000, step=50, value=500,
                                marks={50: "50", 1000: "1k", 2500: "2.5k", 5000: "5k"},
                                tooltip={"placement": "bottom"},
                            ),

                            html.Div("Spot Radius", style=SLIDER_LABEL_STYLE),
                            html.Div(id="spot-value", children="50 µm", style=VALUE_STYLE),
                            dcc.Slider(
                                id="spot-slider", min=10, max=500, step=5, value=50,
                                marks={10: "10", 100: "100", 250: "250", 500: "500"},
                                tooltip={"placement": "bottom"},
                            ),

                            html.Div("Beam Profile", style=SLIDER_LABEL_STYLE),
                            dbc.RadioItems(
                                id="beam-select",
                                options=[
                                    {"label": "Gaussian", "value": "gaussian"},
                                    {"label": "Top-Hat", "value": "tophat"},
                                ],
                                value="gaussian",
                                inline=True,
                                className="mt-1",
                            ),
                        ]),

                        # Motion Card
                        html.Div(style=CARD_STYLE, children=[
                            html.Div("Motion", style=SECTION_TITLE_STYLE),

                            html.Div("Scan Speed", style=SLIDER_LABEL_STYLE),
                            html.Div(id="speed-value", children="100 mm/s", style=VALUE_STYLE),
                            dcc.Slider(
                                id="speed-slider", min=10, max=1000, step=10, value=100,
                                marks={10: "10", 250: "250", 500: "500", 1000: "1000"},
                                tooltip={"placement": "bottom"},
                            ),

                            html.Div("Pattern", style=SLIDER_LABEL_STYLE),
                            dbc.RadioItems(
                                id="motion-select",
                                options=[
                                    {"label": "Raster", "value": "raster"},
                                    {"label": "Spiral", "value": "spiral"},
                                    {"label": "Linear", "value": "linear"},
                                ],
                                value="raster",
                                inline=True,
                                className="mt-1",
                            ),
                        ]),

                        # Solver Card
                        html.Div(style=CARD_STYLE, children=[
                            html.Div("Solver", style=SECTION_TITLE_STYLE),

                            dbc.RadioItems(
                                id="solver-select",
                                options=[
                                    {"label": "3D FDM (Full)", "value": "fdm3d"},
                                    {"label": "Analytical (Fast)", "value": "analytical"},
                                ],
                                value="fdm3d",
                                className="mt-1",
                            ),

                            html.Div("Resolution", style={**SLIDER_LABEL_STYLE, "marginTop": "12px"}),
                            html.Div(id="res-value", children="200 µm", style=VALUE_STYLE),
                            dcc.Slider(
                                id="res-slider", min=100, max=500, step=25, value=200,
                                marks={100: "100", 200: "200", 350: "350", 500: "500"},
                                tooltip={"placement": "bottom"},
                            ),
                        ]),

                        # Run Button
                        html.Button(
                            "▶  Run Simulation",
                            id="run-btn",
                            n_clicks=0,
                            style={
                                "width": "100%",
                                "padding": "14px",
                                "background": "linear-gradient(135deg, #4361ee, #3a0ca3)",
                                "color": "white",
                                "border": "none",
                                "borderRadius": "12px",
                                "fontSize": "15px",
                                "fontWeight": "700",
                                "cursor": "pointer",
                                "letterSpacing": "0.5px",
                                "boxShadow": "0 4px 20px rgba(67,97,238,0.35)",
                                "transition": "all 0.2s ease",
                                "marginBottom": "16px",
                            },
                        ),

                        # Results Card
                        html.Div(id="results-card", style=RESULT_CARD_STYLE, children=[
                            html.Div("Results", style=SECTION_TITLE_STYLE),
                            html.Div(
                                "Run a simulation to see results.",
                                style={"color": "#506080", "fontSize": "13px"},
                            ),
                        ]),
                    ],
                ),

                # ==== Right Panel: Visualization ====
                html.Div(
                    style={
                        "display": "flex",
                        "flexDirection": "column",
                        "maxHeight": "calc(100vh - 130px)",
                    },
                    children=[
                        dbc.Tabs(
                            id="viz-tabs",
                            active_tab="tab-3d",
                            className="mb-0",
                            children=[
                                dbc.Tab(label="3D Surface", tab_id="tab-3d"),
                                dbc.Tab(label="XZ Section", tab_id="tab-xz"),
                                dbc.Tab(label="YZ Section", tab_id="tab-yz"),
                                dbc.Tab(label="Fluence Map", tab_id="tab-fluence"),
                                dbc.Tab(label="Depth Profile", tab_id="tab-depth"),
                            ],
                        ),
                        html.Div(
                            id="viz-content",
                            style={
                                **CARD_STYLE,
                                "flex": "1",
                                "marginTop": "0",
                                "borderTopLeftRadius": "0",
                                "borderTopRightRadius": "0",
                                "overflow": "hidden",
                                "padding": "8px",
                            },
                            children=[
                                dcc.Graph(
                                    id="main-graph",
                                    figure=empty_3d_figure(),
                                    style={"height": "100%"},
                                    config={
                                        "displayModeBar": True,
                                        "scrollZoom": True,
                                        "displaylogo": False,
                                    },
                                ),
                            ],
                        ),
                    ],
                ),
            ],
        ),

        # Hidden store for simulation data
        dcc.Store(id="sim-data-store"),
        dcc.Loading(
            id="loading-overlay",
            type="circle",
            color="#4361ee",
            children=[html.Div(id="loading-target")],
            style={
                "position": "fixed",
                "top": "50%",
                "left": "50%",
                "transform": "translate(-50%,-50%)",
                "zIndex": "9999",
            },
        ),
    ],
)


# ============================================================================
# Callbacks
# ============================================================================

@callback(
    Output("power-value", "children"),
    Input("power-slider", "value"),
)
def update_power_label(val):
    return f"{val:,.0f} W"


@callback(
    Output("spot-value", "children"),
    Input("spot-slider", "value"),
)
def update_spot_label(val):
    return f"{val} µm"


@callback(
    Output("speed-value", "children"),
    Input("speed-slider", "value"),
)
def update_speed_label(val):
    return f"{val} mm/s"


@callback(
    Output("res-value", "children"),
    Input("res-slider", "value"),
)
def update_res_label(val):
    return f"{val} µm"


@callback(
    Output("material-info", "children"),
    Input("material-select", "value"),
)
def update_material_info(mat_key):
    m = MATERIALS[mat_key]
    obj = m["obj"]
    return html.Div([
        html.Div(m["composition"], style={"marginBottom": "6px", "color": "#7088b0"}),
        html.Span(f"k={obj.thermal_conductivity} W/mK", style={"marginRight": "12px"}),
        html.Span(f"ρ={obj.density} kg/m³"),
        html.Br(),
        html.Span(f"T_melt={obj.t_melt}°C", style={"marginRight": "12px"}),
        html.Span(f"α={obj.absorption}"),
    ])


# ---- Run Simulation ----
@callback(
    Output("sim-data-store", "data"),
    Output("loading-target", "children"),
    Output("status-badge", "children"),
    Output("status-badge", "style"),
    Input("run-btn", "n_clicks"),
    State("material-select", "value"),
    State("power-slider", "value"),
    State("spot-slider", "value"),
    State("beam-select", "value"),
    State("speed-slider", "value"),
    State("motion-select", "value"),
    State("solver-select", "value"),
    State("res-slider", "value"),
    prevent_initial_call=True,
)
def run_simulation(n_clicks, mat_key, power, spot, beam, speed, motion, solver_type, resolution):
    if not n_clicks:
        return no_update, no_update, no_update, no_update

    try:
        material = MATERIALS[mat_key]["obj"]

        laser_source = GaussianLaserSource() if beam == "gaussian" else TopHatLaserSource()

        if solver_type == "fdm3d":
            solver = FDMHeatSolver3D(laser_source=laser_source)
        else:
            solver = AnalyticalHeatSolver(laser_source=laser_source)

        motion_generators = {
            "raster": RasterGenerator(),
            "spiral": SpiralGenerator(),
            "linear": LinearGenerator(),
        }

        os.makedirs("output", exist_ok=True)
        audit = AuditLogger(log_dir="output")

        use_case = SimulationUseCase(
            heat_solver=solver,
            motion_generator=motion_generators[motion],
            audit_logger=audit,
        )

        laser = LaserBeam(
            power=power, wavelength=1.064e-6,
            spot_radius=spot * 1e-6, focal_length=0.1,
        )

        speed_ms = speed / 1000.0
        grid_lx, grid_ly, grid_lz = 0.010, 0.010, 0.002

        if motion == "raster":
            motion_params = {
                "x_start": 0.0, "x_end": grid_lx,
                "y_start": 0.0, "y_end": grid_ly,
                "z_focus": 0.05, "line_spacing": 0.5e-3,
                "scan_speed": speed_ms,
            }
        elif motion == "spiral":
            motion_params = {
                "center_x": grid_lx / 2, "center_y": grid_ly / 2,
                "z_focus": 0.05, "inner_radius": 0.5e-3,
                "outer_radius": 4.0e-3, "n_revolutions": 5,
                "scan_speed": speed_ms,
            }
        else:
            motion_params = {
                "x_start": 0.0, "y_start": grid_ly / 2,
                "x_end": grid_lx, "y_end": grid_ly / 2,
                "z_focus": 0.05, "scan_speed": speed_ms,
            }

        res_m = resolution * 1e-6
        result = use_case.execute(
            material=material, laser=laser,
            motion_params=motion_params,
            grid_size=(grid_lx, grid_ly, grid_lz),
            resolution=res_m,
        )

        # Serialize for storage (convert numpy arrays)
        data = {
            "temperature_field": result.temperature_field,
            "fluence_map": result.fluence_map,
            "x_coords": result.x_coords,
            "y_coords": result.y_coords,
            "z_coords": result.z_coords,
            "peak_temperature_celsius": result.peak_temperature_celsius,
            "peak_fluence_j_per_m2": result.peak_fluence_j_per_m2,
            "melt_depth_m": result.melt_depth_m,
            "vaporization_depth_m": result.vaporization_depth_m,
            "total_energy_j": result.total_energy_j,
            "material_name": result.material_name,
            "solver_name": result.solver_name,
            "duration_seconds": result.duration_seconds,
            "t_melt": material.t_melt,
            "t_vaporization": material.t_vaporization,
        }

        badge_style = {
            "background": "rgba(80,200,100,0.15)",
            "color": "#50c864",
            "padding": "6px 16px",
            "borderRadius": "20px",
            "fontSize": "12px",
            "fontWeight": "600",
        }

        return data, "", f"✓ Done ({result.duration_seconds:.1f}s)", badge_style

    except Exception as exc:
        badge_style = {
            "background": "rgba(255,100,100,0.15)",
            "color": "#ff6464",
            "padding": "6px 16px",
            "borderRadius": "20px",
            "fontSize": "12px",
            "fontWeight": "600",
        }
        return no_update, "", f"Error: {str(exc)[:60]}", badge_style


# ---- Update Results Card ----
@callback(
    Output("results-card", "children"),
    Input("sim-data-store", "data"),
)
def update_results(data):
    if not data:
        return [
            html.Div("Results", style=SECTION_TITLE_STYLE),
            html.Div("Run a simulation to see results.", style={"color": "#506080", "fontSize": "13px"}),
        ]

    def metric(label, value):
        return html.Div(
            style={"marginBottom": "10px"},
            children=[
                html.Div(label, style=METRIC_LABEL),
                html.Div(value, style=METRIC_VALUE),
            ],
        )

    items = [html.Div("Results", style=SECTION_TITLE_STYLE)]
    items.append(metric("Peak Temperature", f"{data['peak_temperature_celsius']:.1f} °C"))
    items.append(metric("Peak Fluence", f"{data['peak_fluence_j_per_m2']:.2e} J/m²"))
    items.append(metric("Total Energy", f"{data['total_energy_j']:.4f} J"))

    if data.get("melt_depth_m") is not None:
        items.append(metric("Melt Depth", f"{data['melt_depth_m']*1e6:.1f} µm"))
    else:
        items.append(metric("Melt Depth", "None"))

    if data.get("vaporization_depth_m") is not None:
        items.append(metric("Vaporization Depth", f"{data['vaporization_depth_m']*1e6:.1f} µm"))

    items.append(metric("Solver Time", f"{data['duration_seconds']:.2f} s"))

    items.append(html.Div(
        style={"marginTop": "12px", "padding": "8px 12px", "background": "rgba(255,255,255,0.03)", "borderRadius": "8px"},
        children=[
            html.Span(data["material_name"], style={"color": "#7088b0", "fontSize": "11px"}),
            html.Span(" · ", style={"color": "#354060"}),
            html.Span(data["solver_name"], style={"color": "#607898", "fontSize": "11px"}),
        ],
    ))

    return items


# ---- Update Visualization ----
@callback(
    Output("main-graph", "figure"),
    Input("viz-tabs", "active_tab"),
    Input("sim-data-store", "data"),
)
def update_graph(active_tab, data):
    if not data:
        if active_tab == "tab-3d":
            return empty_3d_figure()
        return empty_2d_figure("Run simulation to see results")

    t_field = np.array(data["temperature_field"])
    x_mm = np.array(data["x_coords"]) * 1000
    y_mm = np.array(data["y_coords"]) * 1000
    z_mm = np.array(data["z_coords"]) * 1000

    if active_tab == "tab-3d":
        return build_3d_surface(t_field, x_mm, y_mm, data)
    elif active_tab == "tab-xz":
        return build_xz_section(t_field, x_mm, z_mm, data)
    elif active_tab == "tab-yz":
        return build_yz_section(t_field, y_mm, z_mm, data)
    elif active_tab == "tab-fluence":
        return build_fluence_map(data, x_mm, y_mm)
    elif active_tab == "tab-depth":
        return build_depth_profile(t_field, data)
    return empty_2d_figure()


# ============================================================================
# Plot Builders
# ============================================================================

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


# ============================================================================
# Entry Point
# ============================================================================

if __name__ == "__main__":
    port = 8050
    print(f"\n  Laser-HVOF 3D Simulation Lab")
    print(f"  -> Opening at http://localhost:{port}\n")
    threading.Timer(1.5, lambda: webbrowser.open(f"http://localhost:{port}")).start()
    app.run(debug=False, port=port)
