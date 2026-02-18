"""
Lab Control Dashboard — Dash web application.

Presentation layer: depends ONLY on the application use case.
No direct imports from infrastructure or domain internals.

Clean Architecture:
  - UI knows nothing about database, controllers, or domain details.
  - All interaction goes through LabControlUseCase.

STRIDE:
  - DoS: Callback throttling with prevent_initial_call.
  - Repudiation: All actions audit-logged via use case.
"""

import json
import math
import plotly.graph_objects as go
import dash
from dash import Dash, html, dcc, Input, Output, State, callback_context, no_update

from lab_control.composition_root import create_lab_control, create_cost_calculator
from lab_control.domain.value_objects import ScanRecipe
from lab_control.domain.exceptions import LabControlError
from lab_control.presentation.visualizations import build_3d_lab_figure

# ── Composition Root: Wire dependencies ──────────────────────────────

lab = create_lab_control()
cost_calc = create_cost_calculator()

# ── App Setup ────────────────────────────────────────────────────────

app = Dash(
    __name__,
    title="HVOF Laser Lab — Virtual Control Dashboard",
    suppress_callback_exceptions=True,
)

# ── Colour Palette ───────────────────────────────────────────────────

COLORS = {
    "bg": "#0a0e17",
    "card": "#111827",
    "card_border": "#1e293b",
    "primary": "#3b82f6",
    "success": "#10b981",
    "danger": "#ef4444",
    "warning": "#f59e0b",
    "text": "#e2e8f0",
    "muted": "#94a3b8",
    "accent": "#8b5cf6",
}

# ── Styles ───────────────────────────────────────────────────────────

CARD_STYLE = {
    "backgroundColor": COLORS["card"],
    "border": f"1px solid {COLORS['card_border']}",
    "borderRadius": "12px",
    "padding": "20px",
    "marginBottom": "16px",
}

LABEL_STYLE = {
    "color": COLORS["muted"],
    "fontSize": "12px",
    "textTransform": "uppercase",
    "letterSpacing": "1px",
    "marginBottom": "6px",
}

BTN_STYLE = {
    "padding": "10px 20px",
    "borderRadius": "8px",
    "border": "none",
    "cursor": "pointer",
    "fontWeight": "600",
    "fontSize": "13px",
    "marginRight": "8px",
    "marginBottom": "8px",
    "display": "inline-flex",
    "alignItems": "center",
    "gap": "8px",
    "transition": "opacity 0.2s",
}

SLIDER_MARKS_STYLE = {"color": COLORS["muted"], "fontSize": "11px"}


def _icon(name, size=16, color=None):
    """Factory: creates a NanoBanana style SVG icon."""
    style = {
        "width": f"{size}px",
        "height": f"{size}px",
        "verticalAlign": "middle",
    }
    # Filter hack for status colors if needed (simplified)
    if color == "success":
        style["filter"] = "invert(54%) sepia(66%) saturate(464%) hue-rotate(97deg) brightness(93%) contrast(92%)"
    elif color == "danger":
        style["filter"] = "invert(36%) sepia(74%) saturate(1915%) hue-rotate(338deg) brightness(98%) contrast(96%)"
    elif color == "warning":
        style["filter"] = "invert(76%) sepia(35%) saturate(5451%) hue-rotate(359deg) brightness(101%) contrast(94%)"
        
    return html.Img(src=f"/assets/icons/{name}.svg", style=style)


def _btn(text, btn_id, color_key="primary", icon_name=None):
    """Factory: creates a styled button (DRY)."""
    children = []
    if icon_name:
        children.append(_icon(icon_name))
    children.append(html.Span(text))
    
    return html.Button(
        children,
        id=btn_id,
        n_clicks=0,
        style={
            **BTN_STYLE,
            "backgroundColor": COLORS[color_key],
            "color": "#fff",
        },
    )


def _indicator(label, value_id, color="text"):
    """Factory: creates a label + value indicator (DRY)."""
    return html.Div([
        html.Div(label, style=LABEL_STYLE),
        html.Div(
            id=value_id,
            style={
                "color": COLORS[color],
                "fontSize": "20px",
                "fontWeight": "700",
            },
        ),
    ], style={"flex": "1", "minWidth": "120px"})


# ══════════════════════════════════════════════════════════════════════
#                             LAYOUT
# ══════════════════════════════════════════════════════════════════════

app.index_string = '''
<!DOCTYPE html>
<html>
    <head>
        {%metas%}
        <title>{%title%}</title>
        {%favicon%}
        {%css%}
        <style>
            *:focus-visible {
                outline: 2px solid #3b82f6 !important;
                outline-offset: 2px !important;
                border-radius: 4px;
            }
            button:focus-visible {
                outline: 2px solid #3b82f6 !important;
                outline-offset: 2px !important;
                box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.25) !important;
            }
        </style>
    </head>
    <body>
        {%app_entry%}
        <footer>
            {%config%}
            {%scripts%}
            {%renderer%}
        </footer>
    </body>
</html>
'''

app.layout = html.Div(
    style={
        "backgroundColor": COLORS["bg"],
        "minHeight": "100vh",
        "color": COLORS["text"],
        "fontFamily": "'Inter', 'Segoe UI', system-ui, sans-serif",
        "padding": "24px",
    },
    children=[
        # ── Header ───────────────────────────
        html.Div([
            html.Div([
                html.H1(
                    "HVOF Laser Lab",
                    style={
                        "margin": "0",
                        "fontSize": "26px",
                        "fontWeight": "800",
                        "background": f"linear-gradient(135deg, "
                                      f"{COLORS['primary']}, {COLORS['accent']})",
                        "WebkitBackgroundClip": "text",
                        "WebkitTextFillColor": "transparent",
                        "display": "flex",
                        "alignItems": "center",
                        "gap": "12px",
                    },
                ),
                html.Span("Virtual Control Dashboard", style={"fontSize": "26px", "fontWeight": "300", "opacity": "0.8"})
            ], style={"display": "flex", "alignItems": "center", "gap": "12px"}),
            html.P(
                "Laser Treatment Laboratory · Simulation & Control",
                style={
                    "margin": "4px 0 0",
                    "color": COLORS["muted"],
                    "fontSize": "13px",
                    "paddingLeft": "4px",
                },
            ),
        ], style={"marginBottom": "24px"}),

        # ── Tabs ─────────────────────────────
        dcc.Tabs(
            id="main-tabs",
            value="tab-xy",
            style={"marginBottom": "20px"},
            colors={
                "border": COLORS["card_border"],
                "primary": COLORS["primary"],
                "background": COLORS["card"],
            },
            children=[
                dcc.Tab(
                    label="X-Y Table", value="tab-xy",
                    style={"color": COLORS["text"], "backgroundColor": COLORS["card"], "padding": "12px 20px", "borderRadius": "8px 8px 0 0", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    selected_style={"color": "#fff", "backgroundColor": COLORS["primary"], "padding": "12px 20px", "borderRadius": "8px 8px 0 0", "fontWeight": "700", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    children=[_icon("tab-xy"), " X-Y Table"]
                ),
                dcc.Tab(
                    label="Laser", value="tab-laser",
                    style={"color": COLORS["text"], "backgroundColor": COLORS["card"], "padding": "12px 20px", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    selected_style={"color": "#fff", "backgroundColor": COLORS["primary"], "padding": "12px 20px", "fontWeight": "700", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    children=[_icon("tab-laser"), " Laser"]
                ),
                dcc.Tab(
                    label="Safety", value="tab-safety",
                    style={"color": COLORS["text"], "backgroundColor": COLORS["card"], "padding": "12px 20px", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    selected_style={"color": "#fff", "backgroundColor": COLORS["primary"], "padding": "12px 20px", "fontWeight": "700", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    children=[_icon("tab-safety"), " Safety"]
                ),
                dcc.Tab(
                    label="3D Lab", value="tab-3d",
                    style={"color": COLORS["text"], "backgroundColor": COLORS["card"], "padding": "12px 20px", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    selected_style={"color": "#fff", "backgroundColor": COLORS["primary"], "padding": "12px 20px", "fontWeight": "700", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    children=[_icon("tab-3d"), " 3D Lab"]
                ),
                dcc.Tab(
                    label="Cost", value="tab-cost",
                    style={"color": COLORS["text"], "backgroundColor": COLORS["card"], "padding": "12px 20px", "borderRadius": "0 8px 0 0", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    selected_style={"color": "#fff", "backgroundColor": COLORS["primary"], "padding": "12px 20px", "fontWeight": "700", "borderRadius": "0 8px 0 0", "display": "flex", "alignItems": "center", "justifyContent": "center", "gap": "8px"},
                    children=[_icon("tab-cost"), " Cost"]
                ),
            ],
        ),

        # ── Tab content ──────────────────────
        html.Div(id="tab-content"),

        # ── Hidden interval for auto-refresh ─
        dcc.Interval(id="refresh-interval", interval=2000, n_intervals=0),

        # ── Fire confirmation dialog ─
        dcc.ConfirmDialog(
            id="confirm-fire",
            message="⚠️ FIRE LASER?\n\nThis will activate the laser at the set power level.\nEnsure all safety interlocks are engaged and the chamber is clear.\n\nProceed?",
        ),

        # ── Status bar ───────────────────────
        html.Div(
            id="status-bar",
            style={
                "position": "fixed",
                "bottom": "0",
                "left": "0",
                "right": "0",
                "backgroundColor": COLORS["card"],
                "borderTop": f"1px solid {COLORS['card_border']}",
                "padding": "8px 24px",
                "display": "flex",
                "gap": "24px",
                "fontSize": "12px",
                "color": COLORS["muted"],
                "zIndex": "100",
            },
        ),
    ],
)


# ══════════════════════════════════════════════════════════════════════
#                          TAB PANELS
# ══════════════════════════════════════════════════════════════════════

def _build_xy_tab():
    """X-Y Table control panel with scan visualization."""
    return html.Div([
        # ── Controls Row ─────────────────
        html.Div([
            # Left: Controls
            html.Div([
                html.Div([
                    html.H3([_icon("tab-xy"), " Position Control"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
                    html.Div([
                        _btn("Home", "btn-home", "primary", "home"),
                        _btn("Run Scan", "btn-scan", "success", "play"),
                        _btn("Export G-Code", "btn-gcode", "accent", "save"),
                    ]),
                ], style=CARD_STYLE),

                html.Div([
                    html.H3([_icon("settings"), " Scan Parameters"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
                    html.Div("PATTERN", style=LABEL_STYLE),
                    dcc.Dropdown(
                        id="scan-pattern",
                        options=[
                            {"label": "Raster (serpentine)", "value": "raster"},
                            {"label": "Spiral (Archimedean)", "value": "spiral"},
                            {"label": "Linear (single pass)", "value": "linear"},
                        ],
                        value="raster",
                        style={"backgroundColor": COLORS["bg"], "color": COLORS["text"],
                               "borderRadius": "6px", "marginBottom": "12px"},
                    ),
                    html.Div([
                        html.Span("SPEED (mm/s)", style=LABEL_STYLE),
                        html.Abbr(" ℹ", title="Table traverse speed during laser scanning",
                                  style={"color": COLORS["accent"], "cursor": "help", "textDecoration": "none", "fontSize": "14px"}),
                    ], style={"display": "flex", "alignItems": "center"}),
                    dcc.Slider(
                        id="scan-speed", min=1, max=50, step=1, value=12,
                        marks={1: {"label": "1"}, 12: {"label": "12"},
                               25: {"label": "25"}, 50: {"label": "50", **SLIDER_MARKS_STYLE}},
                        tooltip={"placement": "bottom"},
                    ),
                    html.Div([
                        html.Span("SCAN WIDTH (mm)", style=LABEL_STYLE),
                        html.Abbr(" ℹ", title="Horizontal extent of the scan area on the workpiece",
                                  style={"color": COLORS["accent"], "cursor": "help", "textDecoration": "none", "fontSize": "14px"}),
                    ], style={"display": "flex", "alignItems": "center"}),
                    dcc.Slider(
                        id="scan-width", min=10, max=200, step=5, value=50,
                        marks={10: {"label": "10"}, 50: {"label": "50"},
                               100: {"label": "100"}, 200: {"label": "200"}},
                        tooltip={"placement": "bottom"},
                    ),
                    html.Div([
                        html.Span("SCAN HEIGHT (mm)", style=LABEL_STYLE),
                        html.Abbr(" ℹ", title="Vertical extent of the scan area on the workpiece",
                                  style={"color": COLORS["accent"], "cursor": "help", "textDecoration": "none", "fontSize": "14px"}),
                    ], style={"display": "flex", "alignItems": "center"}),
                    dcc.Slider(
                        id="scan-height", min=10, max=200, step=5, value=50,
                        marks={10: {"label": "10"}, 50: {"label": "50"},
                               100: {"label": "100"}, 200: {"label": "200"}},
                        tooltip={"placement": "bottom"},
                    ),
                    html.Div([
                        html.Span("SPOT SIZE (mm)", style=LABEL_STYLE),
                        html.Abbr(" ℹ", title="Laser beam diameter on the workpiece surface",
                                  style={"color": COLORS["accent"], "cursor": "help", "textDecoration": "none", "fontSize": "14px"}),
                    ], style={"display": "flex", "alignItems": "center"}),
                    dcc.Slider(
                        id="scan-spot", min=0.5, max=5, step=0.5, value=2.5,
                        marks={0.5: {"label": "0.5"}, 2.5: {"label": "2.5"}, 5: {"label": "5"}},
                        tooltip={"placement": "bottom"},
                    ),
                    html.Div([
                        html.Span("OVERLAP (%)", style=LABEL_STYLE),
                        html.Abbr(" ℹ", title="How much each laser pass overlaps the previous one (higher = denser coverage)",
                                  style={"color": COLORS["accent"], "cursor": "help", "textDecoration": "none", "fontSize": "14px"}),
                    ], style={"display": "flex", "alignItems": "center"}),
                    dcc.Slider(
                        id="scan-overlap", min=0, max=90, step=5, value=50,
                        marks={0: {"label": "0"}, 50: {"label": "50"}, 90: {"label": "90"}},
                        tooltip={"placement": "bottom"},
                    ),
                ], style=CARD_STYLE),
            ], style={"flex": "1", "minWidth": "280px"}),

            # Right: Visualization
            html.Div([
                html.Div([
                    html.H3([_icon("layers"), " 3D Laboratory Layout"], style={"margin": "0 0 8px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
                    dcc.Loading(
                        type="circle",
                        color=COLORS["primary"],
                        children=dcc.Graph(id="scan-plot", figure=_empty_scan_figure(), style={"height": "420px"}),
                    ),
                ], style=CARD_STYLE),
                html.Div([
                    html.Div(
                        style={"display": "flex", "flexWrap": "wrap", "gap": "12px"},
                        children=[
                            _indicator("Position X", "pos-x"),
                            _indicator("Position Y", "pos-y"),
                            _indicator("State", "table-state"),
                            _indicator("Points", "scan-points"),
                        ],
                    ),
                ], style=CARD_STYLE),
            ], style={"flex": "2", "minWidth": "400px"}),
        ], style={"display": "flex", "gap": "16px", "flexWrap": "wrap"}),

        # ── G-Code Output ────────────────
        html.Div([
            html.H3([_icon("file-text"), " Generated G-Code"], style={"margin": "0 0 8px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            dcc.Loading(
                type="circle",
                color=COLORS["primary"],
                children=html.Pre(
                    id="gcode-output",
                    style={
                        "backgroundColor": COLORS["bg"],
                        "padding": "16px",
                        "borderRadius": "8px",
                        "maxHeight": "200px",
                        "overflow": "auto",
                        "fontSize": "12px",
                        "fontFamily": "'Fira Code', 'Consolas', monospace",
                        "color": COLORS["muted"],
                        "border": f"1px solid {COLORS['card_border']}",
                    },
                    children="Click 'Export G-Code' to generate...",
                ),
            ),
        ], style=CARD_STYLE),
    ])


def _build_laser_tab():
    """Laser control panel."""
    return html.Div([
        html.Div([
            html.Div([
                html.H3([_icon("sliders"), " Laser Control"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
                html.Div("POWER (W)", style=LABEL_STYLE),
                dcc.Slider(
                    id="laser-power", min=0, max=1000, step=10, value=0,
                    marks={
                        0: {"label": "0"}, 200: {"label": "200"},
                        500: {"label": "500"}, 800: {"label": "800"},
                        1000: {"label": "1000"},
                    },
                    tooltip={"placement": "bottom"},
                ),
                html.Div(
                    style={"marginTop": "16px"},
                    children=[
                        _btn("Set Power", "btn-set-power", "primary", "power"),
                        _btn("Arm", "btn-arm", "warning", "target"),
                        _btn("Fire", "btn-fire", "danger", "zap"),
                        _btn("Stop", "btn-stop-laser", "primary", "stop"),
                    ],
                ),
            ], style=CARD_STYLE),

            html.Div([
                html.H3([_icon("activity"), " Laser Status"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
                html.Div(
                    style={"display": "flex", "flexWrap": "wrap", "gap": "12px"},
                    children=[
                        _indicator("State", "laser-state"),
                        _indicator("Power (W)", "laser-power-val"),
                        _indicator("Power (%)", "laser-power-pct"),
                        _indicator("Wavelength", "laser-wl"),
                    ],
                ),
                html.Div(
                    id="laser-bar",
                    style={
                        "marginTop": "16px",
                        "height": "16px",
                        "borderRadius": "8px",
                        "backgroundColor": COLORS["bg"],
                        "overflow": "hidden",
                    },
                    children=[
                        html.Div(
                            id="laser-bar-fill",
                            style={
                                "height": "100%",
                                "width": "0%",
                                "borderRadius": "8px",
                                "background": f"linear-gradient(90deg, "
                                               f"{COLORS['success']}, {COLORS['danger']})",
                                "transition": "width 0.3s ease",
                            },
                        ),
                    ],
                ),
            ], style=CARD_STYLE),
        ]),

        # ── Gas System ───────────────────
        html.Div([
            html.H3([_icon("wind"), " Shielding Gas (Argon)"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            html.Div([
                html.Div([
                    html.Div("FLOW RATE (L/min)", style=LABEL_STYLE),
                    dcc.Slider(
                        id="gas-flow", min=0, max=20, step=0.5, value=0,
                        marks={0: {"label": "0"}, 5: {"label": "5"},
                               10: {"label": "10"}, 15: {"label": "15"},
                               20: {"label": "20"}},
                        tooltip={"placement": "bottom"},
                    ),
                    html.Div([
                        _btn("Set Flow", "btn-set-gas", "primary", "wind"),
                        _btn("Purge", "btn-purge", "warning", "refresh-cw"),
                        _btn("Stop Gas", "btn-stop-gas", "primary", "slash"),
                    ], style={"marginTop": "12px"}),
                ], style={"flex": "1"}),
                html.Div([
                    html.Div(
                        style={"display": "flex", "flexWrap": "wrap", "gap": "12px"},
                        children=[
                            _indicator("Gas State", "gas-state"),
                            _indicator("O₂ (ppm)", "gas-o2"),
                            _indicator("Supply (%)", "gas-supply"),
                            _indicator("Atmosphere", "gas-safe"),
                        ],
                    ),
                ], style={"flex": "1"}),
            ], style={"display": "flex", "gap": "24px", "flexWrap": "wrap"}),
        ], style=CARD_STYLE),
    ])


def _build_safety_tab():
    """Safety & interlock status panel."""
    return html.Div([
        html.Div([
            html.H3([_icon("shield-lock"), " Safety Interlocks"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            html.Div([
                html.Div([
                    html.Div([
                        _btn("Lock Door", "btn-lock-door", "success", "lock"),
                        _btn("Unlock Door", "btn-unlock-door", "warning", "unlock"),
                    ]),
                    html.Div([
                        _btn("Lock Chamber", "btn-lock-chamber", "success", "lock"),
                        _btn("Unlock Chamber", "btn-unlock-chamber", "warning", "unlock"),
                    ], style={"marginTop": "8px"}),
                    html.Div([
                        _btn("E-STOP", "btn-estop", "danger", "octagon"),
                        _btn("Reset E-Stop", "btn-reset-estop", "primary", "refresh-cw"),
                    ], style={"marginTop": "8px"}),
                ], style={"flex": "1"}),
                html.Div([
                    html.Div(
                        style={"display": "flex", "flexWrap": "wrap", "gap": "12px"},
                        children=[
                            _indicator("Door", "safety-door"),
                            _indicator("Chamber", "safety-chamber"),
                            _indicator("E-Stop", "safety-estop"),
                            _indicator("Warning Light", "safety-warning"),
                            _indicator("System", "safety-all-clear"),
                        ],
                    ),
                ], style={"flex": "1"}),
            ], style={"display": "flex", "gap": "24px", "flexWrap": "wrap"}),
        ], style=CARD_STYLE),

        # ── Audit Log ────────────────────
        html.Div([
            html.H3([_icon("file-text"), " Audit Log (STRIDE: Repudiation Defense)"],
                     style={"margin": "0 0 12px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            html.Div(
                id="audit-log",
                style={
                    "backgroundColor": COLORS["bg"],
                    "padding": "12px",
                    "borderRadius": "8px",
                    "maxHeight": "300px",
                    "overflow": "auto",
                    "fontFamily": "'Fira Code', 'Consolas', monospace",
                    "fontSize": "11px",
                    "border": f"1px solid {COLORS['card_border']}",
                },
            ),
        ], style=CARD_STYLE),
    ])


def _build_3d_tab():
    """3D interactive lab layout viewer."""
    return html.Div([
        html.Div([
            html.H3([_icon("layers"), " 3D Laboratory Layout"], style={"margin": "0 0 8px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            html.P("Interactive 3D model — drag to rotate, scroll to zoom",
                    style={"color": COLORS["muted"], "fontSize": "12px", "margin": "0 0 8px"}),
            dcc.Graph(
                id="lab-3d",
                figure=build_3d_lab_figure(),
                style={"height": "550px"},
            ),
        ], style=CARD_STYLE),
    ])


def _build_cost_tab():
    """Equipment cost calculator."""
    catalog = cost_calc.get_catalog()
    categories = cost_calc.get_categories()
    total = cost_calc.calculate_total(catalog)

    category_sections = []
    for cat in categories:
        items = cost_calc.get_by_category(cat)
        cat_total = sum(i.total_cost_eur for i in items)
        rows = []
        for item in items:
            rows.append(html.Tr([
                html.Td(item.name, style={"padding": "8px", "color": COLORS["text"]}),
                html.Td(item.description, style={"padding": "8px", "color": COLORS["muted"],
                                                  "fontSize": "12px"}),
                html.Td(str(item.quantity), style={"padding": "8px", "textAlign": "center"}),
                html.Td(
                    f"€{item.unit_cost_eur:,.0f}",
                    style={"padding": "8px", "textAlign": "right"},
                ),
                html.Td(
                    f"€{item.total_cost_eur:,.0f}",
                    style={
                        "padding": "8px",
                        "textAlign": "right",
                        "fontWeight": "700",
                        "color": COLORS["primary"],
                    },
                ),
            ]))
            rows.append(html.Tr([
                html.Td(
                    colSpan=4,
                    children=f"Subtotal: {cat.upper()}",
                    style={
                        "padding": "8px",
                        "fontWeight": "600",
                        "borderTop": f"1px solid {COLORS['card_border']}",
                        "color": COLORS["muted"],
                    },
                ),
                html.Td(
                    f"€{cat_total:,.0f}",
                    style={
                        "padding": "8px",
                        "textAlign": "right",
                        "fontWeight": "700",
                        "color": COLORS["warning"],
                        "borderTop": f"1px solid {COLORS['card_border']}",
                    },
                ),
            ]))

        category_sections.append(
            html.Tbody(rows)
        )

    return html.Div([
        # ── Summary Card ─────────────────
        html.Div([
            html.H3([_icon("briefcase"), " Budget Summary"], style={"margin": "0 0 16px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            html.Div(
                style={"display": "flex", "gap": "24px", "flexWrap": "wrap"},
                children=[
                    html.Div([
                        html.Div("TOTAL EQUIPMENT", style=LABEL_STYLE),
                        html.Div(
                            f"€{total:,.0f}",
                            style={
                                "fontSize": "32px",
                                "fontWeight": "800",
                                "color": COLORS["primary"],
                            },
                        ),
                    ], style={"flex": "1"}),
                    html.Div([
                        html.Div("CATEGORIES", style=LABEL_STYLE),
                        html.Div(
                            str(len(categories)),
                            style={"fontSize": "32px", "fontWeight": "800", "color": COLORS["accent"]},
                        ),
                    ], style={"flex": "1"}),
                    html.Div([
                        html.Div("LINE ITEMS", style=LABEL_STYLE),
                        html.Div(
                            str(len(catalog)),
                            style={"fontSize": "32px", "fontWeight": "800", "color": COLORS["success"]},
                        ),
                    ], style={"flex": "1"}),
                ],
            ),
        ], style=CARD_STYLE),

        # ── Cost Breakdown Chart ─────────
        html.Div([
            html.H3([_icon("pie-chart"), " Cost by Category"], style={"margin": "0 0 8px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            dcc.Graph(
                id="cost-chart",
                figure=_build_cost_chart(),
                style={"height": "350px"},
            ),
        ], style=CARD_STYLE),

        # ── Detailed Table ───────────────
        html.Div([
            html.H3([_icon("list"), " Equipment Catalog"], style={"margin": "0 0 12px", "fontSize": "16px", "display": "flex", "alignItems": "center", "gap": "8px"}),
            html.Table(
                style={
                    "width": "100%",
                    "borderCollapse": "collapse",
                    "fontSize": "13px",
                },
                children=[
                    html.Thead(html.Tr([
                        html.Th("Equipment", style={"padding": "10px", "textAlign": "left",
                                                     "borderBottom": f"2px solid {COLORS['primary']}"}),
                        html.Th("Description", style={"padding": "10px", "textAlign": "left",
                                                       "borderBottom": f"2px solid {COLORS['primary']}"}),
                        html.Th("Qty", style={"padding": "10px", "textAlign": "center",
                                               "borderBottom": f"2px solid {COLORS['primary']}"}),
                        html.Th("Unit Cost", style={"padding": "10px", "textAlign": "right",
                                                     "borderBottom": f"2px solid {COLORS['primary']}"}),
                        html.Th("Total", style={"padding": "10px", "textAlign": "right",
                                                  "borderBottom": f"2px solid {COLORS['primary']}"}),
                    ])),
                    *category_sections,
                    html.Tfoot(html.Tr([
                        html.Td(
                            colSpan=4,
                            children="GRAND TOTAL",
                            style={
                                "padding": "12px",
                                "fontWeight": "800",
                                "fontSize": "15px",
                                "borderTop": f"3px solid {COLORS['primary']}",
                            },
                        ),
                        html.Td(
                            f"€{total:,.0f}",
                            style={
                                "padding": "12px",
                                "textAlign": "right",
                                "fontWeight": "800",
                                "fontSize": "18px",
                                "color": COLORS["success"],
                                "borderTop": f"3px solid {COLORS['primary']}",
                            },
                        ),
                    ])),
                ],
            ),
        ], style=CARD_STYLE),
    ])


# ══════════════════════════════════════════════════════════════════════
#                          3D LAB FIGURE
# ══════════════════════════════════════════════════════════════════════




# ══════════════════════════════════════════════════════════════════════
#                        COST CHART
# ══════════════════════════════════════════════════════════════════════

def _build_cost_chart():
    """Build a donut chart of costs by category."""
    categories = cost_calc.get_categories()
    labels = []
    values = []
    for cat in categories:
        items = cost_calc.get_by_category(cat)
        labels.append(cat.upper())
        values.append(sum(i.total_cost_eur for i in items))

    fig = go.Figure(go.Pie(
        labels=labels,
        values=values,
        hole=0.55,
        marker=dict(colors=[
            "#5b8fb9", "#7c9eb2", "#6ba89a", "#c4a35a",
            "#8e99a4", "#a3927d", "#6b9dad", "#b8956a",
            "#7ea685", "#8895b3",
        ]),
        textinfo="label+percent",
        textposition="outside",
        textfont=dict(size=11),
    ))
    fig.update_layout(
        paper_bgcolor=COLORS["bg"],
        plot_bgcolor=COLORS["bg"],
        font=dict(color=COLORS["text"], size=12),
        margin=dict(l=20, r=20, t=20, b=20),
        showlegend=False,
    )
    return fig


# ══════════════════════════════════════════════════════════════════════
#                          CALLBACKS
# ══════════════════════════════════════════════════════════════════════

@app.callback(
    Output("tab-content", "children"),
    Input("main-tabs", "value"),
)
def render_tab(tab):
    """Render the selected tab panel."""
    tab_builders = {
        "tab-xy": _build_xy_tab,
        "tab-laser": _build_laser_tab,
        "tab-safety": _build_safety_tab,
        "tab-3d": _build_3d_tab,
        "tab-cost": _build_cost_tab,
    }
    return tab_builders.get(tab, _build_xy_tab)()


# ── X-Y Table Callbacks ──────────────────────────────────────────────

@app.callback(
    [
        Output("scan-plot", "figure"),
        Output("pos-x", "children"),
        Output("pos-y", "children"),
        Output("table-state", "children"),
        Output("scan-points", "children"),
    ],
    [
        Input("btn-home", "n_clicks"),
        Input("btn-scan", "n_clicks"),
    ],
    [
        State("scan-pattern", "value"),
        State("scan-speed", "value"),
        State("scan-width", "value"),
        State("scan-height", "value"),
        State("scan-spot", "value"),
        State("scan-overlap", "value"),
    ],
    prevent_initial_call=True,
)
def handle_table_actions(
    home_clicks, scan_clicks,
    pattern, speed, width, height, spot, overlap,
):
    """Handle X-Y table button clicks."""
    triggered = callback_context.triggered[0]["prop_id"]
    scan_fig = _empty_scan_figure()
    path_len = 0
    error_msg = None

    try:
        if "btn-home" in triggered:
            lab.home_table()

        elif "btn-scan" in triggered:
            recipe = ScanRecipe(
                pattern=pattern,
                speed_mm_s=speed,
                power_w=800,
                spot_mm=spot,
                overlap_pct=overlap,
                width_mm=width,
                height_mm=height,
            )
            result = lab.run_scan(recipe)
            path = result["path"]
            path_len = len(path)
            xs = [p.x_mm for p in path]
            ys = [p.y_mm for p in path]
            scan_fig = _build_scan_figure(xs, ys, pattern)

    except LabControlError as e:
        error_msg = html.Div([_icon("alert-triangle", color="warning"), html.Span(f" {str(e)}")])
    except Exception as e:
        error_msg = html.Div([_icon("octagon", color="danger"), html.Span(f" Error: {str(e)}")])

    status = lab.get_full_status()
    state_display = error_msg if error_msg else status["table"]["state"]
    
    return (
        scan_fig,
        f"{status['table']['position_x']:.2f} mm",
        f"{status['table']['position_y']:.2f} mm",
        state_display,
        str(path_len),
    )


@app.callback(
    Output("gcode-output", "children"),
    Input("btn-gcode", "n_clicks"),
    [
        State("scan-pattern", "value"),
        State("scan-speed", "value"),
        State("scan-width", "value"),
        State("scan-height", "value"),
        State("scan-spot", "value"),
        State("scan-overlap", "value"),
    ],
    prevent_initial_call=True,
)
def generate_gcode(
    n_clicks, pattern, speed, width, height, spot, overlap,
):
    """Generate G-code and display it."""
    recipe = ScanRecipe(
        pattern=pattern,
        speed_mm_s=speed,
        power_w=800,
        spot_mm=spot,
        overlap_pct=overlap,
        width_mm=width,
        height_mm=height,
    )
    gcode = lab.generate_gcode(recipe)
    return "\n".join(str(line) for line in gcode)


# ── Laser Callbacks ──────────────────────────────────────────────────

# Fire button opens confirmation dialog instead of firing directly
@app.callback(
    Output("confirm-fire", "displayed"),
    Input("btn-fire", "n_clicks"),
    prevent_initial_call=True,
)
def show_fire_confirm(n_clicks):
    """Show a confirmation dialog before firing the laser."""
    return True


@app.callback(
    [
        Output("laser-state", "children"),
        Output("laser-power-val", "children"),
        Output("laser-power-pct", "children"),
        Output("laser-wl", "children"),
        Output("laser-bar-fill", "style"),
    ],
    [
        Input("btn-set-power", "n_clicks"),
        Input("btn-arm", "n_clicks"),
        Input("confirm-fire", "submit_n_clicks"),
        Input("btn-stop-laser", "n_clicks"),
    ],
    State("laser-power", "value"),
    prevent_initial_call=True,
)
def handle_laser_actions(set_clicks, arm_clicks, fire_confirmed, stop_clicks, power):
    """Handle laser button clicks. Fire only triggers after confirmation."""
    triggered = callback_context.triggered[0]["prop_id"]
    error_msg = None
    try:
        if "btn-set-power" in triggered:
            lab.set_laser_power(power)
        elif "btn-arm" in triggered:
            lab.arm_laser()
        elif "confirm-fire" in triggered:
            lab.fire_laser()
        elif "btn-stop-laser" in triggered:
            lab.stop_laser()
    except Exception as e:
        error_msg = html.Div([_icon("alert-triangle", color="warning"), html.Span(f" {str(e)}")])

    status = lab.get_full_status()
    ls = status["laser"]
    bar_style = {
        "height": "100%",
        "width": f"{ls['power_pct']:.0f}%",
        "borderRadius": "8px",
        "background": f"linear-gradient(90deg, {COLORS['success']}, {COLORS['danger']})",
        "transition": "width 0.3s ease",
    }
    
    if error_msg:
        state_display = error_msg
    else:
        # Icon mapping for laser state
        state = ls["state"]
        if state == "FIRING":
            icon = "zap"
            color = "danger"
        elif state == "ARMED":
            icon = "target"
            color = "warning"
        else: # OFF
            icon = "stop"
            color = "muted"
            
        state_display = html.Div([
            _icon(icon, color=color),
            html.Span(f" {state}")
        ], style={"display": "flex", "alignItems": "center", "gap": "6px", "color": COLORS.get(color, COLORS["text"])})
    
    return (
        state_display,
        f"{ls['power_w']:.0f} W",
        f"{ls['power_pct']:.1f}%",
        f"{lab.laser.wavelength_nm} nm",
        bar_style,
    )


# ── Gas Callbacks ────────────────────────────────────────────────────

@app.callback(
    [
        Output("gas-state", "children"),
        Output("gas-o2", "children"),
        Output("gas-supply", "children"),
        Output("gas-safe", "children"),
    ],
    [
        Input("btn-set-gas", "n_clicks"),
        Input("btn-purge", "n_clicks"),
        Input("btn-stop-gas", "n_clicks"),
    ],
    State("gas-flow", "value"),
    prevent_initial_call=True,
)
def handle_gas_actions(set_clicks, purge_clicks, stop_clicks, flow):
    """Handle gas system button clicks."""
    triggered = callback_context.triggered[0]["prop_id"]
    error_msg = None
    try:
        if "btn-set-gas" in triggered:
            lab.set_gas_flow(flow)
        elif "btn-purge" in triggered:
            lab.start_purge()
        elif "btn-stop-gas" in triggered:
            lab.set_gas_flow(0)
    except Exception as e:
        error_msg = html.Div([_icon("alert-triangle", color="warning"), html.Span(f" {str(e)}")])

    status = lab.get_full_status()
    gs = status["gas"]
    
    if gs["atmosphere_safe"]:
        safe_content = [_icon("shield-check", color="success"), html.Span(" SAFE", style={"color": COLORS["success"]})]
    else:
        safe_content = [_icon("shield-alert", color="danger"), html.Span(" O₂ HIGH", style={"color": COLORS["danger"]})]
    
    state_display = error_msg if error_msg else gs["state"]
    
    return (
        state_display,
        f"{gs['o2_ppm']:,.0f}",
        f"{gs['supply_pct']:.0f}%",
        html.Div(safe_content, style={"display": "flex", "alignItems": "center", "gap": "6px"}),
    )


# ── Safety Callbacks ─────────────────────────────────────────────────

@app.callback(
    [
        Output("safety-door", "children"),
        Output("safety-chamber", "children"),
        Output("safety-estop", "children"),
        Output("safety-warning", "children"),
        Output("safety-all-clear", "children"),
        Output("audit-log", "children"),
    ],
    [
        Input("btn-lock-door", "n_clicks"),
        Input("btn-unlock-door", "n_clicks"),
        Input("btn-lock-chamber", "n_clicks"),
        Input("btn-unlock-chamber", "n_clicks"),
        Input("btn-estop", "n_clicks"),
        Input("btn-reset-estop", "n_clicks"),
        Input("refresh-interval", "n_intervals"),
    ],

)
def handle_safety(ld, ud, lc, uc, es, re, interval):
    """Handle safety button clicks and refresh audit log."""
    triggered = callback_context.triggered[0]["prop_id"]
    try:
        if "btn-lock-door" in triggered:
            lab.set_door_interlock(True)
        elif "btn-unlock-door" in triggered:
            lab.set_door_interlock(False)
        elif "btn-lock-chamber" in triggered:
            lab.set_chamber_interlock(True)
        elif "btn-unlock-chamber" in triggered:
            lab.set_chamber_interlock(False)
        elif "btn-estop" in triggered:
            lab.trigger_e_stop()
        elif "btn-reset-estop" in triggered:
            lab.reset_e_stop()
    except Exception:
        # Safety audit logging catches these internally, 
        # but we catch here to verify dashboard robustness
        pass

    status = lab.get_full_status()
    ss = status["safety"]

    def _safety_badge(value, good_label, bad_label):
        # Helper to DRY up status badges with icons
        is_good = value in ("LOCKED", False, True)
        if isinstance(value, bool):  # e.g. e_stop=True is bad
            is_good = not value
            
        color = COLORS["success"] if is_good else COLORS["danger"]
        icon_name = "lock" if "LOCKED" in good_label else ("shield-check" if is_good else "shield-alert")
        if "OPEN" in bad_label: icon_name = "unlock" if not is_good else "lock"
        if "Active" in bad_label: icon_name = "alert-triangle"
        
        return html.Div([
            _icon(icon_name, color="success" if is_good else "danger"),
            html.Span(f" {good_label if is_good else bad_label}")
        ], style={"color": color, "fontWeight": "700", "display": "flex", "alignItems": "center", "gap": "6px"})

    door = _safety_badge(ss["door"] == "LOCKED", "LOCKED", "OPEN")
    chamber = _safety_badge(ss["chamber"] == "LOCKED", "LOCKED", "OPEN")
    
    # E-Stop is special
    if ss["e_stop"]:
        estop = html.Div([_icon("octagon", color="danger"), html.Span(" PRESSED")], 
                         style={"color": COLORS["danger"], "fontWeight": "700", "display": "flex", "alignItems": "center", "gap": "6px"})
    else:
        estop = html.Div([_icon("shield-check", color="success"), html.Span(" CLEAR")], 
                         style={"color": COLORS["success"], "fontWeight": "700", "display": "flex", "alignItems": "center", "gap": "6px"})
        
    # Warning Light
    if ss["warning_light"]:
        warning = html.Div([_icon("alert-triangle", color="warning"), html.Span(" ACTIVE")], 
                           style={"color": COLORS["warning"], "fontWeight": "700", "display": "flex", "alignItems": "center", "gap": "6px"})
    else:
        warning = html.Span("OFF", style={"color": COLORS["muted"]})

    # System Status
    if ss["all_clear"]:
        all_clear = html.Div([_icon("check-circle", color="success"), html.Span(" ALL CLEAR")], 
                             style={"color": COLORS["success"], "fontWeight": "700", "display": "flex", "alignItems": "center", "gap": "6px"})
    else:
        all_clear = html.Div([_icon("slash", color="danger"), html.Span(" NOT READY")], 
                             style={"color": COLORS["danger"], "fontWeight": "700", "display": "flex", "alignItems": "center", "gap": "6px"})

    # Audit log display
    logs = lab.get_audit_logs(30)
    log_entries = []
    for entry in reversed(logs):
        severity_color = COLORS["muted"]
        if entry["severity"] == "WARNING":
            severity_color = COLORS["warning"]
        log_entries.append(
            html.Div(
                f"[{entry['timestamp'][:19]}] "
                f"[{entry['subsystem']}] "
                f"{entry['action']} — "
                f"{json.dumps(entry['details'])}",
                style={"color": severity_color, "marginBottom": "2px"},
            )
        )

    return door, chamber, estop, warning, all_clear, log_entries


# ── Status Bar Callback ──────────────────────────────────────────────

@app.callback(
    Output("status-bar", "children"),
    Input("refresh-interval", "n_intervals"),
)
def update_status_bar(n):
    """Refresh the bottom status bar."""
    status = lab.get_full_status()
    t = status["table"]
    l = status["laser"]
    g = status["gas"]
    s = status["safety"]
    all_ok = "✅" if s["all_clear"] else "❌"
    return [
        html.Span(f"Table: {t['state']} | X={t['position_x']:.1f} Y={t['position_y']:.1f}"),
        html.Span(f"Laser: {l['state']} | {l['power_w']:.0f}W"),
        html.Span(f"Gas: {g['state']} | {g['flow_l_min']:.1f} L/min"),
        html.Span(f"Safety: {all_ok}"),
    ]


# ══════════════════════════════════════════════════════════════════════
#                    HELPER FIGURE BUILDERS
# ══════════════════════════════════════════════════════════════════════

def _empty_scan_figure():
    """Create an empty placeholder scan plot."""
    fig = go.Figure()
    fig.add_annotation(
        text="Click 'Run Scan' to visualize",
        xref="paper", yref="paper",
        x=0.5, y=0.5, showarrow=False,
        font=dict(size=14, color=COLORS["muted"]),
    )
    fig.update_layout(
        paper_bgcolor=COLORS["bg"],
        plot_bgcolor=COLORS["bg"],
        xaxis=dict(showgrid=False, zeroline=False, showticklabels=False),
        yaxis=dict(showgrid=False, zeroline=False, showticklabels=False),
        margin=dict(l=20, r=20, t=20, b=20),
    )
    return fig


def _build_scan_figure(xs, ys, pattern):
    """Build a 2D plot of the scan path."""
    fig = go.Figure()

    # Path line
    fig.add_trace(go.Scattergl(
        x=xs, y=ys,
        mode="lines",
        line=dict(color=COLORS["primary"], width=1),
        name="Scan Path",
        hovertemplate="X: %{x:.2f} mm<br>Y: %{y:.2f} mm<extra></extra>",
    ))

    # Start marker
    fig.add_trace(go.Scatter(
        x=[xs[0]], y=[ys[0]],
        mode="markers",
        marker=dict(size=10, color=COLORS["success"], symbol="circle"),
        name="Start",
    ))

    # End marker
    fig.add_trace(go.Scatter(
        x=[xs[-1]], y=[ys[-1]],
        mode="markers",
        marker=dict(size=10, color=COLORS["danger"], symbol="x"),
        name="End",
    ))

    fig.update_layout(
        paper_bgcolor=COLORS["bg"],
        plot_bgcolor=COLORS["bg"],
        font=dict(color=COLORS["text"]),
        xaxis=dict(
            title="X (mm)", gridcolor="#1e293b",
            color=COLORS["muted"], scaleanchor="y",
        ),
        yaxis=dict(
            title="Y (mm)", gridcolor="#1e293b",
            color=COLORS["muted"],
        ),
        margin=dict(l=50, r=20, t=20, b=50),
        legend=dict(
            bgcolor=COLORS["card"],
            bordercolor=COLORS["card_border"],
        ),
    )
    return fig



# ── 3D Visualisation Callback ────────────────────────────────────────

@app.callback(
    Output("lab-3d", "figure"),
    Input("refresh-interval", "n_intervals"),
    State("main-tabs", "value"),
)
def update_3d_view(n, tab):
    """Update the 3D view with current lab state."""
    # Performance optimization: only update if on the 3D tab
    if tab != "tab-3d":
        return dash.no_update
        
    status = lab.get_full_status()
    t = status["table"]
    l = status["laser"]
    
    # Check if laser is effectively "active" (firing)
    is_firing = l["state"] == "FIRING"
    
    return build_3d_lab_figure(
        pos_x_mm=t["position_x"],
        pos_y_mm=t["position_y"],
        laser_active=is_firing
    )


# ══════════════════════════════════════════════════════════════════════
#                          ENTRY POINT
# ══════════════════════════════════════════════════════════════════════

def run():
    """Start the dashboard server."""
    app.run(debug=False, port=8051)


if __name__ == "__main__":
    run()
