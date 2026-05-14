
from dash import Input, Output, State, callback, ctx


# ---- Preset Definitions ----
PRESETS = {
    "preset-gentle": {
        "power": 200,
        "spot": 100,
        "beam": "gaussian",
        "speed": 200,
        "motion": "raster",
        "solver": "fdm3d",
        "resolution": 200,
    },
    "preset-standard": {
        "power": 500,
        "spot": 50,
        "beam": "gaussian",
        "speed": 100,
        "motion": "raster",
        "solver": "fdm3d",
        "resolution": 200,
    },
    "preset-deep": {
        "power": 2000,
        "spot": 30,
        "beam": "tophat",
        "speed": 50,
        "motion": "linear",
        "solver": "fdm3d",
        "resolution": 150,
    },
    "preset-wide": {
        "power": 1000,
        "spot": 250,
        "beam": "gaussian",
        "speed": 300,
        "motion": "spiral",
        "solver": "fdm3d",
        "resolution": 250,
    },
}


@callback(
    Output("power-slider", "value"),
    Output("spot-slider", "value"),
    Output("beam-select", "value"),
    Output("speed-slider", "value"),
    Output("motion-select", "value"),
    Output("solver-select", "value"),
    Output("res-slider", "value"),
    Input("preset-gentle", "n_clicks"),
    Input("preset-standard", "n_clicks"),
    Input("preset-deep", "n_clicks"),
    Input("preset-wide", "n_clicks"),
    prevent_initial_call=True,
)
def apply_preset(gentle, standard, deep, wide):
    """Apply a preset configuration to all sliders."""
    trigger = ctx.triggered_id
    if trigger not in PRESETS:
        from dash import no_update
        return (no_update,) * 7

    p = PRESETS[trigger]
    return (
        p["power"],
        p["spot"],
        p["beam"],
        p["speed"],
        p["motion"],
        p["solver"],
        p["resolution"],
    )


# ---- Tab Description Updates ----
TAB_DESCRIPTIONS = {
    "tab-3d": "Interactive 3D surface temperature view. Rotate, zoom and pan to explore.",
    "tab-xz": "Cross-section view cutting through the XZ plane at the mid-Y position. Shows depth penetration.",
    "tab-yz": "Cross-section view cutting through the YZ plane at the mid-X position. Shows lateral heat spread.",
    "tab-fluence": "Cumulative energy density map showing how much laser energy each surface point received.",
    "tab-depth": "Temperature vs depth at the hottest surface point. Shows how deep the heat penetrates.",
}


@callback(
    Output("tab-description", "children"),
    Input("viz-tabs", "active_tab"),
)
def update_tab_description(active_tab):
    return TAB_DESCRIPTIONS.get(active_tab, "")


# ---- Banner Dismiss ----
@callback(
    Output("getting-started-banner", "className"),
    Input("dismiss-banner-btn", "n_clicks"),
    prevent_initial_call=True,
)
def dismiss_banner(n_clicks):
    if n_clicks:
        return "getting-started-banner banner-hidden"
    return "getting-started-banner"
