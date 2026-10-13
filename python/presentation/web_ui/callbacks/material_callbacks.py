
from dash import Input, Output, State, html, callback, no_update
from ..services.material_service import (
    MATERIALS, estimate_thermal_props, format_composition_str, build_material_options
)
from domain.entities import Material

COMP_ELEMENTS = ["W", "C", "Ta", "Cr", "Fe", "Ni", "Co", "Mo", "Ti", "Nb", "Hf", "V"]


@callback(
    Output("material-info", "children"),
    Input("material-select", "value"),
)
def update_material_info(mat_key):
    if not mat_key or mat_key not in MATERIALS:
        return "Select a material"
    m = MATERIALS[mat_key]
    obj = m["obj"]
    children = []

    # Composition
    children.append(html.Div(
        m["composition"],
        style={"marginBottom": "8px", "color": "#8898c0", "fontWeight": "500"},
    ))

    # Category badge
    cat = m.get("category", "unknown")
    badge_color = "#4361ee" if cat == "discovered" else "#50c864"
    if cat == "custom":
        badge_color = "#e056fd"
        
    children.append(html.Span(
        cat.upper(),
        style={
            "display": "inline-block",
            "background": f"rgba({badge_color[1:][:2]},{badge_color[3:][:2]},{badge_color[5:]},0.15)" if len(badge_color) == 7 else "rgba(67,97,238,0.15)",
            "color": badge_color,
            "padding": "2px 8px",
            "borderRadius": "4px",
            "fontSize": "9px",
            "fontWeight": "700",
            "letterSpacing": "1px",
            "marginBottom": "8px",
            "marginRight": "8px",
        },
    ))

    # Score/Hardness for discovered
    if cat == "discovered":
        score = m.get("score", 0)
        hardness = m.get("hardness", 0)
        if score:
            children.append(html.Span(
                f"Score: {score:,.0f}",
                style={"color": "#ff9f43", "fontSize": "10px", "fontWeight": "600", "marginRight": "10px"},
            ))
        if hardness:
            children.append(html.Span(
                f"Hardness: {hardness:,.0f} HV",
                style={"color": "#ff6b6b", "fontSize": "10px", "fontWeight": "600"},
            ))
        children.append(html.Br())

    # Thermal properties table
    prop_style = {"color": "#607898", "fontSize": "10px", "marginRight": "10px"}
    children.append(html.Div(style={"marginTop": "6px"}, children=[
        html.Span(f"k={obj.thermal_conductivity} W/mK", style=prop_style),
        html.Span(f"rho={obj.density:.0f} kg/m3", style=prop_style),
        html.Span(f"cp={obj.specific_heat:.0f} J/kgK", style=prop_style),
        html.Br(),
        html.Span(f"T_melt={obj.t_melt:.0f} C", style=prop_style),
        html.Span(f"T_vap={obj.t_vaporization:.0f} C", style=prop_style),
        html.Span(f"abs={obj.absorption:.2f}", style=prop_style),
    ]))

    return html.Div(children)


@callback(
    Output("custom-mat-modal", "is_open"),
    Input("open-custom-modal", "n_clicks"),
    State("custom-mat-modal", "is_open"),
    prevent_initial_call=True,
)
def toggle_custom_modal(n, is_open):
    return not is_open


@callback(
    Output("material-select", "options"),
    Output("material-select", "value"),
    Output("custom-mat-modal", "is_open", allow_duplicate=True),
    Output("custom-mat-preview", "children"),
    Input("create-custom-btn", "n_clicks"),
    [State(f"comp-{e}", "value") for e in COMP_ELEMENTS],
    State("custom-name", "value"),
    State("custom-tmelt", "value"),
    State("custom-tvap", "value"),
    State("custom-density", "value"),
    State("custom-k", "value"),
    prevent_initial_call=True,
)
def create_custom_material(n_clicks, *args):
    """Create a new material from user-specified composition and properties."""
    if not n_clicks:
        return no_update, no_update, no_update, no_update

    # Parse inputs: first 12 are element compositions
    comp_values = args[:len(COMP_ELEMENTS)]
    name = args[len(COMP_ELEMENTS)] or "Custom-Material"
    t_melt_override = args[len(COMP_ELEMENTS) + 1] or 0
    t_vap_override = args[len(COMP_ELEMENTS) + 2] or 0
    density_override = args[len(COMP_ELEMENTS) + 3] or 0
    k_override = args[len(COMP_ELEMENTS) + 4] or 0

    # Build composition dict — only include non-zero entries
    composition = {}
    for elem, val in zip(COMP_ELEMENTS, comp_values):
        v = float(val or 0)
        if v > 0:
            composition[elem] = v / 100.0  # Convert % to fraction

    if not composition:
        return no_update, no_update, no_update, "Please enter at least one element percentage."

    # Normalize to sum to 1.0
    total = sum(composition.values())
    composition = {k: v / total for k, v in composition.items()}

    # Estimate properties
    est = estimate_thermal_props(composition)

    # Apply overrides
    k_val = float(k_override) if k_override else est["k"]
    density = float(density_override) if density_override else est["rho"]
    t_melt = float(t_melt_override) if t_melt_override else 2500.0
    t_vap = float(t_vap_override) if t_vap_override else t_melt + 1500.0

    if t_vap <= t_melt:
        t_vap = t_melt + 500.0

    # Create key
    key = f"custom_{name.lower().replace('-', '_').replace(' ', '_')}_{len(MATERIALS)}"

    # Add to MATERIALS
    MATERIALS[key] = {
        "label": name,
        "category": "custom",
        "score": 0,
        "hardness": 0,
        "obj": Material(
            name=name,
            absorption=est["abs"],
            thermal_conductivity=k_val,
            density=density,
            specific_heat=est["cp"],
            t_ambient=20.0,
            t_melt=t_melt,
            t_vaporization=t_vap,
        ),
        "composition": format_composition_str(composition),
    }

    # Rebuild options with new custom material
    new_options = build_material_options(MATERIALS)

    preview_text = f"Created: {name}  |  {format_composition_str(composition)}  |  k={k_val} W/mK, rho={density:.0f} kg/m3, T_melt={t_melt:.0f} C"

    return new_options, key, False, preview_text
