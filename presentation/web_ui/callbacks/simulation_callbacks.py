
import os
import sys
import numpy as np
from dash import Input, Output, State, html, callback, no_update

# Import project root for domain/infrastructure access
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../")))

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

from ..services.material_service import MATERIALS
from ..constants import (
    SECTION_TITLE_STYLE, METRIC_LABEL, METRIC_VALUE,
    WARNING_STYLE, DANGER_STYLE, SUCCESS_HINT_STYLE, INTERPRETATION_STYLE,
)
from ..components.plot_builders import (
    empty_3d_figure, empty_2d_figure, build_3d_surface,
    build_xz_section, build_yz_section, build_fluence_map, build_depth_profile
)


# ---- Slider Label Callbacks ----
@callback(Output("power-value", "children"), Input("power-slider", "value"))
def update_power_label(val):
    return f"{val:,.0f} W"

@callback(Output("spot-value", "children"), Input("spot-slider", "value"))
def update_spot_label(val):
    return f"{val} µm"

@callback(Output("speed-value", "children"), Input("speed-slider", "value"))
def update_speed_label(val):
    return f"{val} mm/s"

@callback(Output("res-value", "children"), Input("res-slider", "value"))
def update_res_label(val):
    return f"{val} µm"


# ---- Run Simulation ----
@callback(
    Output("sim-data-store", "data"),
    Output("loading-target", "children"),
    Output("status-badge", "children"),
    Output("status-badge", "style"),
    Output("run-btn", "className"),
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
        return no_update, no_update, no_update, no_update, no_update

    try:
        if mat_key not in MATERIALS:
             return no_update, "", "Error: Material not found", {
                 "background": "#ff6464", "color": "white",
                 "padding": "6px 16px", "borderRadius": "20px",
                 "fontSize": "12px", "fontWeight": "600",
             }, ""

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

        # Serialize for storage
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

        # Remove pulse after first successful run
        return data, "", f"Done ({result.duration_seconds:.1f}s)", badge_style, ""

    except Exception as exc:
        badge_style = {
            "background": "rgba(255,100,100,0.15)",
            "color": "#ff6464",
            "padding": "6px 16px",
            "borderRadius": "20px",
            "fontSize": "12px",
            "fontWeight": "600",
        }
        return no_update, "", f"Error: {str(exc)[:60]}", badge_style, ""


# ---- Helper: CSS icon element ----
def _icon(icon_class, symbol):
    """Create a small CSS-styled icon circle."""
    return html.Span(symbol, className=icon_class)


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

    def metric(label, value, interpretation=None):
        children = [
            html.Div(label, style=METRIC_LABEL),
            html.Div(value, style=METRIC_VALUE),
        ]
        if interpretation:
            children.append(html.Div(interpretation, style=INTERPRETATION_STYLE))
        return html.Div(style={"marginBottom": "10px"}, children=children)

    items = [html.Div("Results", style=SECTION_TITLE_STYLE)]

    peak_t = data["peak_temperature_celsius"]
    t_melt = data.get("t_melt", 0)
    t_vap = data.get("t_vaporization", 0)

    # Peak Temperature with context
    if t_melt and peak_t > t_melt:
        temp_hint = f"Exceeds melting point ({t_melt:.0f} C)"
    elif t_melt:
        temp_hint = f"{peak_t / t_melt * 100:.0f}% of melting point ({t_melt:.0f} C)"
    else:
        temp_hint = None
    items.append(metric("Peak Temperature", f"{peak_t:.1f} C", temp_hint))

    items.append(metric(
        "Peak Fluence",
        f"{data['peak_fluence_j_per_m2']:.2e} J/m2",
        "Energy density at the most exposed point",
    ))

    items.append(metric(
        "Total Energy",
        f"{data['total_energy_j']:.4f} J",
        "Total energy delivered to the surface",
    ))

    if data.get("melt_depth_m") is not None:
        md = data["melt_depth_m"] * 1e6
        items.append(metric("Melt Depth", f"{md:.1f} um", "Depth at which melting occurs"))
    else:
        items.append(metric("Melt Depth", "None", "No melting detected at this configuration"))

    if data.get("vaporization_depth_m") is not None:
        vd = data["vaporization_depth_m"] * 1e6
        items.append(metric("Vaporization Depth", f"{vd:.1f} um"))

    items.append(metric("Solver Time", f"{data['duration_seconds']:.2f} s"))

    # ---- Warnings / Assessments ----
    if t_vap and peak_t > t_vap:
        items.append(html.Div(style=DANGER_STYLE, children=[
            _icon("icon-danger", "!"),
            html.Span("Vaporization risk — temperature exceeds vaporization point. Consider reducing power or increasing scan speed."),
        ]))
    elif t_melt and peak_t > t_melt:
        items.append(html.Div(style=WARNING_STYLE, children=[
            _icon("icon-warning", "!"),
            html.Span(f"Material will melt — peak temperature exceeds T_melt ({t_melt:.0f} C). Melt depth shown above."),
        ]))
    elif t_melt and peak_t > t_melt * 0.8:
        items.append(html.Div(style=WARNING_STYLE, children=[
            _icon("icon-warning", "~"),
            html.Span(f"Approaching melt threshold — {peak_t / t_melt * 100:.0f}% of melting point."),
        ]))
    else:
        items.append(html.Div(style=SUCCESS_HINT_STYLE, children=[
            _icon("icon-success", "ok"),
            html.Span("Safe operating range — no melting or vaporization detected."),
        ]))

    # Summary assessment
    if peak_t < 200:
        assessment = "Minimal thermal effect. Consider increasing power."
    elif t_melt and peak_t < t_melt * 0.5:
        assessment = "Gentle treatment — surface heating without structural changes."
    elif t_melt and peak_t < t_melt:
        assessment = "Moderate treatment — significant heating, potential for hardening."
    elif t_vap and peak_t < t_vap:
        assessment = "Aggressive treatment — melting occurs, suitable for surface modification."
    else:
        assessment = "Extreme treatment — vaporization detected, may cause material removal."

    items.append(html.Div(
        style={
            "marginTop": "12px",
            "padding": "10px 12px",
            "background": "rgba(255,255,255,0.03)",
            "borderRadius": "8px",
        },
        children=[
            html.Div("Assessment", style={**METRIC_LABEL, "marginBottom": "4px"}),
            html.Div(assessment, style={"color": "#a0b8e0", "fontSize": "12px", "lineHeight": "1.5"}),
            html.Div(
                style={"marginTop": "8px"},
                children=[
                    html.Span(data["material_name"], style={"color": "#7088b0", "fontSize": "11px"}),
                    html.Span(" · ", style={"color": "#354060"}),
                    html.Span(data["solver_name"], style={"color": "#607898", "fontSize": "11px"}),
                ],
            ),
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
