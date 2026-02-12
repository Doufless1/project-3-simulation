"""
Linear Motion Generator — Simple straight-line path.

Implements IMotionGenerator for linear scan trajectories.
Single Responsibility: Only generates linear paths.
"""

import numpy as np

from domain.entities import Trajectory
from domain.ports import IMotionGenerator


class LinearGenerator(IMotionGenerator):
    """Generate a simple linear scan trajectory."""

    def generate(self, **params) -> Trajectory:
        """
        Generate linear path.

        Required params:
            x_start, y_start: Starting position [m]
            x_end, y_end: Ending position [m]
            z_focus: Constant Z height [m]
            scan_speed: Linear speed [m/s]
            dt: Time step [s] (default 1e-4)
        """
        x_start = params["x_start"]
        y_start = params["y_start"]
        x_end = params["x_end"]
        y_end = params["y_end"]
        z_focus = params["z_focus"]
        scan_speed = params["scan_speed"]
        dt = params.get("dt", 1e-4)

        distance = np.sqrt((x_end - x_start) ** 2 + (y_end - y_start) ** 2)
        total_time = distance / scan_speed
        n_points = max(int(total_time / dt), 2)

        t_arr = np.linspace(0, total_time, n_points)
        x_arr = np.linspace(x_start, x_end, n_points)
        y_arr = np.linspace(y_start, y_end, n_points)

        return Trajectory(
            time=t_arr.tolist(),
            x=x_arr.tolist(),
            y=y_arr.tolist(),
            z=[z_focus] * n_points,
        )
