"""
Raster Motion Generator — Zig-zag scan path.

Implements IMotionGenerator for raster (boustrophedon) scanning.
Single Responsibility: Only generates raster trajectories.
"""

import numpy as np

from domain.entities import Trajectory
from domain.ports import IMotionGenerator


class RasterGenerator(IMotionGenerator):
    """Generate a raster (zig-zag) scan trajectory."""

    def generate(self, **params) -> Trajectory:
        """
        Generate raster path.

        Required params:
            x_start, x_end, y_start, y_end: Scan area bounds [m]
            z_focus: Constant Z height [m]
            line_spacing: Gap between scan lines [m]
            scan_speed: Linear speed [m/s]
            dt: Time step [s] (default 1e-4)
        """
        x_start = params["x_start"]
        x_end = params["x_end"]
        y_start = params["y_start"]
        y_end = params["y_end"]
        z_focus = params["z_focus"]
        line_spacing = params["line_spacing"]
        scan_speed = params["scan_speed"]
        dt = params.get("dt", 1e-4)

        n_lines = int(np.ceil((y_end - y_start) / line_spacing)) + 1
        y_positions = np.linspace(y_start, y_end, n_lines)

        x_list, y_list, t_list = [], [], []
        current_time = 0.0

        for i, y_pos in enumerate(y_positions):
            x_line_start = x_start if i % 2 == 0 else x_end
            x_line_end = x_end if i % 2 == 0 else x_start

            line_length = abs(x_end - x_start)
            line_time = line_length / scan_speed
            n_points = max(int(line_time / dt), 2)

            x_list.append(np.linspace(x_line_start, x_line_end, n_points))
            y_list.append(np.full(n_points, y_pos))
            t_list.append(np.linspace(current_time, current_time + line_time, n_points))
            current_time += line_time + dt

        return Trajectory(
            time=np.concatenate(t_list).tolist(),
            x=np.concatenate(x_list).tolist(),
            y=np.concatenate(y_list).tolist(),
            z=[z_focus] * sum(len(a) for a in x_list),
        )
