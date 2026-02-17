
from dash import html, dcc
import dash_bootstrap_components as dbc
from ..constants import (
    CARD_STYLE, SECTION_TITLE_STYLE, SLIDER_LABEL_STYLE, 
    VALUE_STYLE, RESULT_CARD_STYLE
)
from ..services.material_service import MATERIALS, build_material_options
from .plot_builders import empty_3d_figure

# Build options once
MATERIAL_OPTIONS = build_material_options(MATERIALS)

# Find default material
DEFAULT_MATERIAL = "ultra_c_ta_wh_3849"
if DEFAULT_MATERIAL not in MATERIALS:
    for k, v in MATERIALS.items():
        if v.get("category") == "discovered":
            DEFAULT_MATERIAL = k
            break
    else:
        DEFAULT_MATERIAL = next(iter(MATERIALS)) if MATERIALS else None


def get_layout():
    """Construct the main Dash layout."""
    return html.Div(
        style={
            "background": "linear-gradient(135deg, #0a0e1a 0%, #0d1529 50%, #0a1020 100%)",
            "minHeight": "100vh",
            "fontFamily": "Inter, system-ui, -apple-system, sans-serif",
        },
        children=[
            # ---- Header ----
            html.Div(
                className="sim-header",
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
                        style={"display": "flex", "alignItems": "center", "gap": "12px"},
                        children=[
                            html.Button(
                                "☰",
                                id="hamburger-btn",
                                className="hamburger-btn",
                                n_clicks=0,
                            ),
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
                ],
            ),

            # ---- Main Grid ----
            html.Div(
                className="sim-main-grid",
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
                        id="mobile-panel",
                        className="sim-left-panel panel-hidden",
                        style={
                            "overflowY": "auto",
                            "maxHeight": "calc(100vh - 130px)",
                            "paddingRight": "8px",
                        },
                        children=[
                            # Close button (visible only on mobile via CSS)
                            html.Button(
                                "✕",
                                id="mobile-close-btn",
                                className="mobile-close-btn",
                                n_clicks=0,
                            ),
                            # Material Card
                            html.Div(style=CARD_STYLE, children=[
                                html.Div([
                                    html.Span("Material", style=SECTION_TITLE_STYLE),
                                    html.Span(
                                        f"{len(MATERIALS)} loaded",
                                        style={"color": "#405070", "fontSize": "11px", "float": "right", "marginTop": "2px"},
                                    ),
                                ]),
                                dcc.Dropdown(
                                    id="material-select",
                                    options=MATERIAL_OPTIONS,
                                    value=DEFAULT_MATERIAL,
                                    clearable=False,
                                    searchable=True,
                                    placeholder="Search materials...",
                                    style={
                                        "marginBottom": "10px",
                                        "backgroundColor": "#1a2040",
                                        "color": "#c0d0f0",
                                        "border": "1px solid rgba(100,140,255,0.15)",
                                        "borderRadius": "8px",
                                    },
                                ),
                                html.Div(id="material-info", style={
                                    "color": "#607898",
                                    "fontSize": "11px",
                                    "lineHeight": "1.6",
                                    "padding": "8px 12px",
                                    "background": "rgba(255,255,255,0.03)",
                                    "borderRadius": "8px",
                                    "marginBottom": "10px",
                                }),
                                # Custom Material Button
                                html.Button(
                                    "+ Create Custom Material",
                                    id="open-custom-modal",
                                    n_clicks=0,
                                    style={
                                        "width": "100%",
                                        "padding": "8px",
                                        "background": "rgba(100,140,255,0.08)",
                                        "color": "#6488c0",
                                        "border": "1px dashed rgba(100,140,255,0.25)",
                                        "borderRadius": "8px",
                                        "fontSize": "12px",
                                        "cursor": "pointer",
                                    },
                                ),
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
                        className="sim-right-panel",
                        style={
                            "display": "flex",
                            "flexDirection": "column",
                            "maxHeight": "calc(100vh - 130px)",
                            "minHeight": "350px",
                        },
                        children=[
                            dbc.Tabs(
                                id="viz-tabs",
                                active_tab="tab-3d",
                                className="mb-0",
                                children=[
                                    dbc.Tab(label="3D", tab_id="tab-3d"),
                                    dbc.Tab(label="XZ", tab_id="tab-xz"),
                                    dbc.Tab(label="YZ", tab_id="tab-yz"),
                                    dbc.Tab(label="Fluence", tab_id="tab-fluence"),
                                    dbc.Tab(label="Depth", tab_id="tab-depth"),
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
                                    "padding": "4px",
                                    "minHeight": "300px",
                                },
                                children=[
                                    dcc.Graph(
                                        id="main-graph",
                                        figure=empty_3d_figure(),
                                        style={"height": "100%", "minHeight": "280px"},
                                        responsive=True,
                                        config={
                                            "displayModeBar": "hover",
                                            "scrollZoom": True,
                                            "displaylogo": False,
                                            "modeBarButtonsToRemove": ["lasso2d", "select2d"],
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

            # Custom Material Modal
            dbc.Modal(
                id="custom-mat-modal",
                is_open=False,
                centered=True,
                size="lg",
                children=[
                    dbc.ModalHeader(
                        dbc.ModalTitle("Create Custom Material"),
                        close_button=True,
                        style={"background": "#0d1529", "color": "#c0d0f0", "border": "none"},
                    ),
                    dbc.ModalBody(
                        style={"background": "#0d1529", "color": "#c0d0f0"},
                        children=[
                            dbc.Row([
                                dbc.Col([
                                    dbc.Label("Material Name", style={"color": "#8898c0", "fontSize": "12px"}),
                                    dbc.Input(id="custom-name", value="My-Custom-Material", type="text",
                                              style={"background": "#1a2040", "color": "#c0d0f0", "border": "1px solid rgba(100,140,255,0.15)"}),
                                ], width=12, className="mb-3"),
                            ]),
                            html.Div("Composition (% — will be normalized to 100%)", style={"color": "#64b5f6", "fontSize": "12px", "fontWeight": "600", "marginBottom": "10px"}),
                            dbc.Row([
                                dbc.Col([
                                    dbc.Label(elem, style={"color": "#7088b0", "fontSize": "11px"}),
                                    dbc.Input(id=f"comp-{elem}", type="number", value=default_val, min=0, max=100, step=0.1,
                                              style={"background": "#1a2040", "color": "#c0d0f0", "border": "1px solid rgba(100,140,255,0.1)", "fontSize": "12px"}),
                                ], width=3, className="mb-2")
                                for elem, default_val in [
                                    ("W", 30), ("C", 20), ("Ta", 10), ("Cr", 10),
                                    ("Fe", 0), ("Ni", 0), ("Co", 5), ("Mo", 0),
                                    ("Ti", 5), ("Nb", 0), ("Hf", 0), ("V", 0),
                                ]
                            ]),
                            html.Hr(style={"borderColor": "rgba(100,140,255,0.1)"}),
                            html.Div("Override Properties (leave 0 to auto-estimate)", style={"color": "#64b5f6", "fontSize": "12px", "fontWeight": "600", "marginBottom": "10px"}),
                            dbc.Row([
                                dbc.Col([
                                    dbc.Label("T_melt [C]", style={"color": "#7088b0", "fontSize": "11px"}),
                                    dbc.Input(id="custom-tmelt", type="number", value=0, min=0,
                                              style={"background": "#1a2040", "color": "#c0d0f0", "border": "1px solid rgba(100,140,255,0.1)", "fontSize": "12px"}),
                                ], width=3),
                                dbc.Col([
                                    dbc.Label("T_vap [C]", style={"color": "#7088b0", "fontSize": "11px"}),
                                    dbc.Input(id="custom-tvap", type="number", value=0, min=0,
                                              style={"background": "#1a2040", "color": "#c0d0f0", "border": "1px solid rgba(100,140,255,0.1)", "fontSize": "12px"}),
                                ], width=3),
                                dbc.Col([
                                    dbc.Label("Density [kg/m3]", style={"color": "#7088b0", "fontSize": "11px"}),
                                    dbc.Input(id="custom-density", type="number", value=0, min=0,
                                              style={"background": "#1a2040", "color": "#c0d0f0", "border": "1px solid rgba(100,140,255,0.1)", "fontSize": "12px"}),
                                ], width=3),
                                dbc.Col([
                                    dbc.Label("k [W/mK]", style={"color": "#7088b0", "fontSize": "11px"}),
                                    dbc.Input(id="custom-k", type="number", value=0, min=0,
                                              style={"background": "#1a2040", "color": "#c0d0f0", "border": "1px solid rgba(100,140,255,0.1)", "fontSize": "12px"}),
                                ], width=3),
                            ]),
                            html.Div(id="custom-mat-preview", style={"marginTop": "16px", "padding": "10px", "background": "rgba(255,255,255,0.03)", "borderRadius": "8px", "fontSize": "11px", "color": "#607898"}),
                        ],
                    ),
                    dbc.ModalFooter(
                        style={"background": "#0d1529", "border": "none"},
                        children=[
                            html.Button(
                                "Create & Select",
                                id="create-custom-btn",
                                n_clicks=0,
                                style={
                                    "padding": "10px 24px",
                                    "background": "linear-gradient(135deg, #4361ee, #3a0ca3)",
                                    "color": "white",
                                    "border": "none",
                                    "borderRadius": "8px",
                                    "fontWeight": "600",
                                    "cursor": "pointer",
                                },
                            ),
                        ],
                    ),
                ],
            ),

            # Store for custom materials
            dcc.Store(id="custom-materials-store", data={}),
        ]
    )
