"""
Lab Visualizations Module.

Provides "NanoBanana" style 3D visualizations for the Lab Control Dashboard.
"""

import plotly.graph_objects as go
import math
import numpy as np

# NanoBanana Palette (Vibrant, Clean)
COLORS = {
    "bg": "#0a0e17",         # Match dashboard bg
    "floor": "#334155",      # Slate 700
    "grid": "#475569",       # Slate 600
    "table": "#94a3b8",      # Slate 400
    "table_legs": "#64748b", # Slate 500
    "stage": "#3b82f6",      # Blue 500
    "chamber": "#f59e0b",    # Amber 500
    "laser": "#ef4444",      # Red 500
    "gas": "#10b981",        # Emerald 500
    "pc": "#8b5cf6",         # Violet 500
    "extract": "#64748b",    # Slate 500
    "cage": "#06b6d4",       # Cyan 500 (transparent)
    "text": "#f8fafc",       # Slate 50
    "hazard": "#eab308",     # Yellow 500
}

def _add_box(fig, name, x0, y0, z0, dx, dy, dz, color, opacity=1.0, showlegend=True):
    """Helper: add a 3D box."""
    # Vertices
    x = [x0, x0+dx, x0+dx, x0,    x0, x0+dx, x0+dx, x0]
    y = [y0, y0,    y0+dy, y0+dy,  y0, y0,    y0+dy, y0+dy]
    z = [z0, z0,    z0,    z0,     z0+dz, z0+dz, z0+dz, z0+dz]
    
    # Triangles
    i = [0,0,0,0,4,4,2,2,0,0,1,1]
    j = [1,2,4,5,5,6,3,6,1,3,2,6]
    k = [2,3,5,6,6,7,6,7,4,7,5,5]
    
    fig.add_trace(go.Mesh3d(
        x=x, y=y, z=z,
        i=i, j=j, k=k,
        color=color, opacity=opacity,
        name=name, showlegend=showlegend,
        lightposition=dict(x=10, y=10, z=100),
        flatshading=True,
        hovertemplate=f"<b>{name}</b><br>Size: {dx:.1f}x{dy:.1f}x{dz:.1f}m<extra></extra>"
    ))

def _add_cylinder(fig, name, x0, y0, z0, r, h, color, opacity=1.0, res=20):
    """Helper: add a vertical cylinder."""
    theta = np.linspace(0, 2*np.pi, res)
    x = x0 + r * np.cos(theta)
    y = y0 + r * np.sin(theta)
    
    # Bottom cap
    z_bot = [z0] * res
    fig.add_trace(go.Mesh3d(
        x=np.concatenate(([x0], x)),
        y=np.concatenate(([y0], y)),
        z=np.concatenate(([z0], z_bot)),
        i=[0]*res, j=np.arange(1, res+1), k=np.append(np.arange(2, res+1), 1),
        color=color, opacity=opacity,
        name=name, showlegend=False,
    ))
    
    # Top cap
    z_top = [z0+h] * res
    fig.add_trace(go.Mesh3d(
        x=np.concatenate(([x0], x)),
        y=np.concatenate(([y0], y)),
        z=np.concatenate(([z0+h], z_top)),
        i=[0]*res, j=np.arange(1, res+1), k=np.append(np.arange(2, res+1), 1),
        color=color, opacity=opacity,
        name=name, showlegend=False,
    ))
    
    # Sides (Triangle Strip)
    # This is complex to mesh manually, simplified by using Surface for sides
    z_grid = np.array([[z0, z0+h]] * res)
    x_grid = np.array([x, x]).T
    y_grid = np.array([y, y]).T
    
    fig.add_trace(go.Surface(
        x=x_grid, y=y_grid, z=z_grid,
        colorscale=[[0, color], [1, color]],
        showscale=False, opacity=opacity,
        name=name, hoverinfo="name",
        contours={"x": {"show": False}, "y": {"show": False}, "z": {"show": False}}
    ))

def build_3d_lab_figure(pos_x_mm=0.0, pos_y_mm=0.0, laser_active=False):
    """
    Build standardized NanoBanana 3D Lab Layout.
    
    Args:
        pos_x_mm (float): X-axis position of the stage in mm.
        pos_y_mm (float): Y-axis position of the stage in mm.
        laser_active (bool): Whether the laser beam should be visible.
    """
    fig = go.Figure()

    # 1. Improved Room Floor (Grid)
    # 6m x 5m room
    _add_box(fig, "Lab Floor", 0, 0, -0.05, 6, 5, 0.05, COLORS["floor"], 1.0, False)
    
    # 2. Optical Table (Detailed with Legs)
    table_x, table_y, table_z = 1.5, 1.5, 0.8
    table_w, table_d, table_h = 1.5, 1.0, 0.15
    
    # Table Top
    _add_box(fig, "Optical Table", table_x, table_y, table_z, table_w, table_d, table_h, COLORS["table"])
    
    # Legs (Cylinders)
    leg_r = 0.05
    colors_legs = COLORS["table_legs"]
    _add_cylinder(fig, "Leg FL", table_x+0.1, table_y+0.1, 0, leg_r, table_z, colors_legs)
    _add_cylinder(fig, "Leg FR", table_x+table_w-0.1, table_y+0.1, 0, leg_r, table_z, colors_legs)
    _add_cylinder(fig, "Leg BL", table_x+0.1, table_y+table_d-0.1, 0, leg_r, table_z, colors_legs)
    _add_cylinder(fig, "Leg BR", table_x+table_w-0.1, table_y+table_d-0.1, 0, leg_r, table_z, colors_legs)

    # 3. Equipment on Table via NanoBanana stacking
    # X-Y Stage Base (Fixed relative to table)
    xy_z = table_z + table_h
    _add_box(fig, "X-Y Stages", table_x+0.2, table_y+0.1, xy_z, 1.1, 0.8, 0.1, COLORS["stage"])
    
    # Processing Chamber (MOVING PART)
    # Base position at 0,0 is offset by 0.3, 0.2 relative to stage base
    # Stage base is at table_x+0.2, table_y+0.1
    # Chamber starts centered-ish. Let's say 0,0 is at table_x+0.5, table_y+0.3
    
    cham_x_base = table_x + 0.5
    cham_y_base = table_y + 0.3
    
    # Convert mm to m
    dx_m = pos_x_mm / 1000.0
    dy_m = pos_y_mm / 1000.0
    
    current_cham_x = cham_x_base + dx_m
    current_cham_y = cham_y_base + dy_m
    
    cham_z = xy_z + 0.1
    _add_box(fig, "Processing Chamber", current_cham_x, current_cham_y, cham_z, 0.5, 0.4, 0.3, COLORS["chamber"])
    
    # Laser Head (Suspended/Mounted) - FIXED relative to table
    head_z = cham_z + 0.4
    # Ensure head is positioned nicely relative to the 'center' of travel
    head_x = table_x + 0.75 # roughly center of stage range
    head_y = table_y + 0.5
    
    _add_box(fig, "Laser Head", head_x - 0.1, head_y - 0.1, head_z, 0.2, 0.2, 0.2, COLORS["laser"])
    
    # Beam Path Indicator (Line) - VISIBLE ONLY IF ACTIVE
    if laser_active:
        fig.add_trace(go.Scatter3d(
            x=[head_x, head_x], 
            y=[head_y, head_y], 
            z=[head_z, cham_z+0.3], # Ends at top of chamber (approx)
            mode="lines",
            line=dict(color="#ff0000", width=8), # Brighter red, thicker
            name="LASER BEAM", hoverinfo="name"
        ))
        
        # Add a "spark" at the hit point
        fig.add_trace(go.Scatter3d(
            x=[head_x], y=[head_y], z=[cham_z+0.3],
            mode="markers",
            marker=dict(size=6, color="#ffff00", symbol="diamond"),
            name="Impact Point"
        ))

    # 4. Peripheral Equipment
    # Laser Source Rack
    _add_box(fig, "Laser Source Rack", 0.5, 0.5, 0, 0.6, 0.6, 1.5, COLORS["laser"])
    
    # Gas Tanks (Cylinders)
    _add_cylinder(fig, "Argon Tank 1", 0.5, 4.0, 0, 0.15, 1.4, COLORS["gas"])
    _add_cylinder(fig, "Argon Tank 2", 0.9, 4.0, 0, 0.15, 1.4, COLORS["gas"])
    
    # Control Station (Desk + PC)
    _add_box(fig, "Control Desk", 4.5, 1.0, 0, 1.2, 0.8, 0.8, COLORS["table_legs"])
    _add_box(fig, "PC Monitor", 4.8, 1.1, 0.8, 0.1, 0.6, 0.4, COLORS["pc"])
    _add_box(fig, "PC Tower", 5.4, 1.1, 0, 0.2, 0.5, 0.5, COLORS["pc"])
    
    # Fume Extraction (Overhead)
    _add_box(fig, "Extraction Unit", 4.5, 4.0, 0, 0.8, 0.8, 2.0, COLORS["extract"])

    # 5. Safety Enclosure (Transparent Cage)
    # Covering the table area
    _add_box(fig, "Safety Enclosure", table_x-0.2, table_y-0.2, 0, table_w+0.4, table_d+0.4, 2.2, COLORS["cage"], opacity=0.15)

    # 6. Annotations (Door, E-Stop)
    fig.add_trace(go.Scatter3d(
        x=[3, 1.5], y=[0, 4.5], z=[1.8, 1.5],
        mode="markers+text",
        text=["🚪 ENTER", "⚠ HAZARD AREA"],
        textposition="top center",
        marker=dict(size=5, color=COLORS["text"], opacity=0),
        name="Info Labels"
    ))
    
    # E-Stops
    fig.add_trace(go.Scatter3d(
        x=[table_x-0.2, 5.0], y=[table_y-0.2, 1.0], z=[1.2, 0.85],
        mode="markers",
        marker=dict(size=8, color="#ef4444", symbol="diamond"),
        name="E-Stop Button"
    ))

    # Layout Styling
    fig.update_layout(
        scene=dict(
            xaxis=dict(title="", range=[0, 6], showgrid=False, zeroline=False, showbackground=False),
            yaxis=dict(title="", range=[0, 5], showgrid=False, zeroline=False, showbackground=False),
            zaxis=dict(title="", range=[0, 2.5], showgrid=False, zeroline=False, showbackground=False),
            aspectratio=dict(x=1.2, y=1, z=0.5),
            camera=dict(
                eye=dict(x=1.5, y=-1.5, z=0.8),
                center=dict(x=0, y=0, z=-0.2),
            ),
            bgcolor=COLORS["bg"],
        ),
        paper_bgcolor=COLORS["bg"],
        plot_bgcolor=COLORS["bg"],
        margin=dict(l=0, r=0, t=0, b=0),
        showlegend=True,
        legend=dict(
            x=0.02, y=0.98,
            bgcolor="rgba(0,0,0,0)",
            font=dict(color=COLORS["text"], size=10)
        ),
        uirevision="constant", # Crucial! Keeps camera angle during updates
    )
    
    return fig
