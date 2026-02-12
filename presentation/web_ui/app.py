
import dash_bootstrap_components as dbc
from dash import Dash

from .constants import APP_INDEX_STRING
from .components.layout_factory import get_layout

# Initialize Dash App
app = Dash(
    __name__,
    external_stylesheets=[
        dbc.themes.DARKLY,
        "https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;600;700&display=swap",
    ],
    title="Laser-HVOF 3D Simulation Lab",
    suppress_callback_exceptions=True,
)

# Apply CSS injection
app.index_string = APP_INDEX_STRING

# Set Layout
app.layout = get_layout()

# Register Callbacks (Importing them is sufficient as they use @callback)
# We must import AFTER app creation so @callback finds the app instance if needed,
# though Dash's global callback registry usually works fine.
from .callbacks import (
    navigation_callbacks,
    material_callbacks,
    simulation_callbacks
)

# Expose server for WSGI
server = app.server
