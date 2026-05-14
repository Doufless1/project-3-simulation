"""
Landing Page — Unified entry point for the HVOF Laser Treatment Platform.

Provides a clear "front door" explaining what the platform offers and
linking to both applications (Lab Control + Simulation Engine).

Usage:
    python landing.py
"""

import threading
import webbrowser
from dash import Dash, html, dcc

from shared.design_tokens import (
    COLORS, FONT_FAMILY, FONT_MONO, CARD_STYLE, BANNER_STYLE,
)

def _tag(text):
    """Factory: renders a small tag/chip."""
    return html.Span(
        text,
        style={
            "fontSize": "11px",
            "fontWeight": "500",
            "color": COLORS["muted"],
            "backgroundColor": f"rgba(59, 130, 246, 0.08)",
            "border": f"1px solid rgba(59, 130, 246, 0.12)",
            "borderRadius": "6px",
            "padding": "3px 8px",
            "whiteSpace": "nowrap",
        },
    )


app = Dash(
    __name__,
    title="HVOF Laser Treatment Platform",
    suppress_callback_exceptions=True,
)

app.index_string = '''
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    {%metas%}
    <title>{%title%}</title>
    {%favicon%}
    {%css%}
    <style>
        * { box-sizing: border-box; }
        body { margin: 0; }
        *:focus-visible {
            outline: 2px solid #3b82f6 !important;
            outline-offset: 2px !important;
        }
        a { text-decoration: none; }
        .app-card {
            transition: transform 0.2s ease, border-color 0.2s ease, box-shadow 0.2s ease;
        }
        .app-card:hover {
            transform: translateY(-4px);
            border-color: rgba(59, 130, 246, 0.4) !important;
            box-shadow: 0 8px 30px rgba(59, 130, 246, 0.15) !important;
        }
        .app-card:active {
            transform: scale(0.98);
        }
        @media (prefers-reduced-motion: reduce) {
            *, *::before, *::after {
                animation-duration: 0.01ms !important;
                transition-duration: 0.01ms !important;
            }
        }
        @media (max-width: 768px) {
            .cards-grid {
                grid-template-columns: 1fr !important;
            }
        }
    </style>
</head>
<body>
    {%app_entry%}
    <footer>{%config%}{%scripts%}{%renderer%}</footer>
</body>
</html>
'''

# ── Layout ──────────────────────────────────────────────────────────

app.layout = html.Div(
    style={
        "backgroundColor": COLORS["bg"],
        "minHeight": "100vh",
        "color": COLORS["text"],
        "fontFamily": FONT_FAMILY,
        "display": "flex",
        "flexDirection": "column",
        "alignItems": "center",
        "padding": "48px 24px",
    },
    children=[
        # ── Hero Section ──
        html.Div(
            style={
                "textAlign": "center",
                "maxWidth": "700px",
                "marginBottom": "48px",
            },
            children=[
                html.H1(
                    "HVOF Laser Treatment Platform",
                    style={
                        "fontSize": "36px",
                        "fontWeight": "800",
                        "margin": "0 0 12px",
                        "background": f"linear-gradient(135deg, {COLORS['primary']}, {COLORS['accent']})",
                        "WebkitBackgroundClip": "text",
                        "WebkitTextFillColor": "transparent",
                        "lineHeight": "1.2",
                    },
                ),
                html.P(
                    "A simulation platform for High-Velocity Oxy-Fuel laser treatment. "
                    "Control virtual lab hardware or run physics-based thermal simulations.",
                    style={
                        "fontSize": "16px",
                        "color": COLORS["muted"],
                        "lineHeight": "1.6",
                        "margin": "0",
                    },
                ),
            ],
        ),

        # ── App Cards Grid ──
        html.Div(
            className="cards-grid",
            style={
                "display": "grid",
                "gridTemplateColumns": "1fr 1fr",
                "gap": "24px",
                "maxWidth": "900px",
                "width": "100%",
            },
            children=[
                # Card 1: Lab Control Dashboard
                html.A(
                    href="http://localhost:8051",
                    target="_blank",
                    rel="noopener noreferrer",
                    className="app-card",
                    style={
                        **CARD_STYLE,
                        "padding": "32px",
                        "display": "flex",
                        "flexDirection": "column",
                        "gap": "16px",
                        "cursor": "pointer",
                        "marginBottom": "0",
                    },
                    children=[
                        html.Div(
                            style={
                                "width": "48px", "height": "48px",
                                "borderRadius": "12px",
                                "backgroundColor": f"rgba(59, 130, 246, 0.12)",
                                "display": "flex", "alignItems": "center",
                                "justifyContent": "center",
                                "fontSize": "22px",
                            },
                            children=html.Span(
                                "\u2699",
                                style={"filter": "none", "color": COLORS["primary"]},
                            ),
                        ),
                        html.Div([
                            html.H2("Lab Control Dashboard", style={
                                "margin": "0 0 8px",
                                "fontSize": "20px",
                                "fontWeight": "700",
                                "color": COLORS["text"],
                            }),
                            html.P(
                                "Virtual digital twin of a laser treatment laboratory. "
                                "Control the motorized X-Y table, shielding gas system, "
                                "and 1 kW fiber laser with real-time safety interlocks.",
                                style={
                                    "margin": "0 0 12px",
                                    "fontSize": "13px",
                                    "color": COLORS["muted"],
                                    "lineHeight": "1.6",
                                },
                            ),
                        ]),
                        html.Div(
                            style={"display": "flex", "flexWrap": "wrap", "gap": "6px"},
                            children=[
                                _tag("X-Y Table"), _tag("Laser Control"),
                                _tag("Safety Interlocks"), _tag("3D View"),
                                _tag("Cost Calculator"),
                            ],
                        ),
                        html.Div(
                            style={
                                "marginTop": "auto",
                                "paddingTop": "16px",
                                "borderTop": f"1px solid {COLORS['card_border']}",
                                "display": "flex",
                                "alignItems": "center",
                                "justifyContent": "space-between",
                            },
                            children=[
                                html.Span("Port 8051", style={
                                    "fontSize": "12px",
                                    "color": COLORS["muted"],
                                    "fontFamily": FONT_MONO,
                                }),
                                html.Span("Open \u2192", style={
                                    "fontSize": "13px",
                                    "fontWeight": "600",
                                    "color": COLORS["primary"],
                                }),
                            ],
                        ),
                    ],
                ),

                # Card 2: Simulation Engine
                html.A(
                    href="http://localhost:8050",
                    target="_blank",
                    rel="noopener noreferrer",
                    className="app-card",
                    style={
                        **CARD_STYLE,
                        "padding": "32px",
                        "display": "flex",
                        "flexDirection": "column",
                        "gap": "16px",
                        "cursor": "pointer",
                        "marginBottom": "0",
                    },
                    children=[
                        html.Div(
                            style={
                                "width": "48px", "height": "48px",
                                "borderRadius": "12px",
                                "backgroundColor": f"rgba(139, 92, 246, 0.12)",
                                "display": "flex", "alignItems": "center",
                                "justifyContent": "center",
                                "fontSize": "22px",
                            },
                            children=html.Span(
                                "\u2604",
                                style={"filter": "none", "color": COLORS["accent"]},
                            ),
                        ),
                        html.Div([
                            html.H2("3D Simulation Engine", style={
                                "margin": "0 0 8px",
                                "fontSize": "20px",
                                "fontWeight": "700",
                                "color": COLORS["text"],
                            }),
                            html.P(
                                "Physics-based thermal modeling of laser treatment on various materials. "
                                "Configure laser power, beam profile, and scan motion to predict "
                                "temperature fields, melt depth, and energy distribution.",
                                style={
                                    "margin": "0 0 12px",
                                    "fontSize": "13px",
                                    "color": COLORS["muted"],
                                    "lineHeight": "1.6",
                                },
                            ),
                        ]),
                        html.Div(
                            style={"display": "flex", "flexWrap": "wrap", "gap": "6px"},
                            children=[
                                _tag("Material Database"), _tag("Thermal Solver"),
                                _tag("3D Visualization"), _tag("Custom Materials"),
                                _tag("Presets"),
                            ],
                        ),
                        html.Div(
                            style={
                                "marginTop": "auto",
                                "paddingTop": "16px",
                                "borderTop": f"1px solid {COLORS['card_border']}",
                                "display": "flex",
                                "alignItems": "center",
                                "justifyContent": "space-between",
                            },
                            children=[
                                html.Span("Port 8050", style={
                                    "fontSize": "12px",
                                    "color": COLORS["muted"],
                                    "fontFamily": FONT_MONO,
                                }),
                                html.Span("Open \u2192", style={
                                    "fontSize": "13px",
                                    "fontWeight": "600",
                                    "color": COLORS["accent"],
                                }),
                            ],
                        ),
                    ],
                ),
            ],
        ),

        # ── Architecture Note ──
        html.Div(
            style={
                "maxWidth": "700px",
                "textAlign": "center",
                "marginTop": "48px",
                "padding": "20px",
                "borderTop": f"1px solid {COLORS['card_border']}",
            },
            children=[
                html.P([
                    html.Span("Built with ", style={"color": COLORS["muted"]}),
                    html.Span("Clean Architecture", style={"color": COLORS["primary"], "fontWeight": "600"}),
                    html.Span(" \u00b7 ", style={"color": COLORS["card_border"]}),
                    html.Span("STRIDE Security", style={"color": COLORS["success"], "fontWeight": "600"}),
                    html.Span(" \u00b7 ", style={"color": COLORS["card_border"]}),
                    html.Span("Plotly Dash", style={"color": COLORS["accent"], "fontWeight": "600"}),
                ], style={"fontSize": "13px", "margin": "0"}),
            ],
        ),
    ],
)


if __name__ == "__main__":
    port = 8049
    print(f"\n  HVOF Laser Treatment Platform — Landing Page")
    print(f"  -> http://localhost:{port}\n")
    threading.Timer(1.5, lambda: webbrowser.open(f"http://localhost:{port}")).start()
    app.run(debug=False, port=port)
