
from dash import Input, Output, State, callback, ctx

def register_navigation_callbacks(app):
    # We can use @callback if we import it, but cleaner to use app.callback if passed, 
    # or just use @callback and ensure app is created before importing? 
    # Actually, Dash's @callback global decorator works if app is initialized. 
    # But to be safe and modular, let's use the explicit app.callback or just definitions.
    # The standard Dash pattern with multiple files often uses the global @callback 
    # if the app is imported, or a function that takes the app.
    
    # Since we are not importing 'app' here to avoid circular imports, 
    # we will use the @callback decorator from dash, which works 
    # as long as the callback is imported *after* app creation in the main file.
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
