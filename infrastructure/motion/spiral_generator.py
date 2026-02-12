"""
Spiral Motion Generator — Outward spiral scan path.

Implements IMotionGenerator for Archimedean spiral trajectories.
Single Responsibility: Only generates spiral paths.
"""

import numpy as np

from domain.entities import Trajectory
from domain.ports import IMotionGenerator


class SpiralGenerator(IMotionGenerator):
    """Generate an outward Archimedean spiral trajectory."""

    def generate(self, **params) -> Trajectory:
        """
        Generate spiral path.

        Required params:
            center_x, center_y: Spiral center [m]
            z_focus: Constant Z height [m]
            inner_radius, outer_radius: Radii bounds [m]
            n_revolutions: Number of full turns
            scan_speed: Tangential speed [m/s]
            dt: Time step [s] (default 1e-4)
        """
        center_x = params["center_x"]
        center_y = params["center_y"]
        z_focus = params["z_focus"]
        inner_radius = params["inner_radius"]
        outer_radius = params["outer_radius"]
        n_revolutions = params["n_revolutions"]
        scan_speed = params["scan_speed"]
        dt = params.get("dt", 1e-4)

        avg_radius = (inner_radius + outer_radius) / 2
        total_angle = n_revolutions * 2 * np.pi
        arc_length = avg_radius * total_angle
        total_time = arc_length / scan_speed

        n_points = max(int(total_time / dt), 100)
        t_arr = np.linspace(0, total_time, n_points)
        theta = np.linspace(0, total_angle, n_points)
        radius = np.linspace(inner_radius, outer_radius, n_points)

        return Trajectory(
            time=t_arr.tolist(),
            x=(center_x + radius * np.cos(theta)).tolist(),
            y=(center_y + radius * np.sin(theta)).tolist(),
            z=[z_focus] * n_points,
        )
