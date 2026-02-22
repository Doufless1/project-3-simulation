
from dash import Input, Output, State, callback, ctx, no_update
from shared.design_tokens import APP_DESCRIPTION_STYLE

def register_navigation_callbacks(app):
    pass

# ---- Mobile Hamburger Toggle ----
@callback(
    Output("mobile-panel", "className"),
    Input("hamburger-btn", "n_clicks"),
    Input("mobile-close-btn", "n_clicks"),
    State("mobile-panel", "className"),
    prevent_initial_call=True,
)
def toggle_mobile_panel(open_clicks, close_clicks, current_class):
    trigger = ctx.triggered_id
    if trigger == "hamburger-btn":
        return "sim-left-panel panel-visible"
    elif trigger == "mobile-close-btn":
        return "sim-left-panel panel-hidden"
    return current_class


# ---- R6: App Description Banner Dismiss ----
@callback(
    Output("sim-description-banner", "style"),
    Input("dismiss-sim-desc-btn", "n_clicks"),
    prevent_initial_call=True,
)
def dismiss_sim_description(n_clicks):
    if n_clicks:
        return {**APP_DESCRIPTION_STYLE, "margin": "0 24px", "display": "none"}
    return no_update
