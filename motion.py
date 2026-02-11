"""
Motion Control Module for Laser Processing Simulation.

Provides trajectory generation for X-Y-Z motion stages.
This is the interface expected by process_model.py.

Author: Agent 1 (Stub Implementation)
"""

import numpy as np
from dataclasses import dataclass
from typing import Optional


@dataclass
class MotionPath:
    """
    Container for motion trajectory data.
    
    Attributes:
        time: Array of time points [s]
        x: X-coordinates along path [m]
        y: Y-coordinates along path [m]
        z: Z-coordinates (focus distance) along path [m]
    """
    time: np.ndarray
    x: np.ndarray
    y: np.ndarray
    z: np.ndarray
    
    def __post_init__(self):
        """Validate that all arrays have the same length."""
        lengths = [len(self.time), len(self.x), len(self.y), len(self.z)]
        if len(set(lengths)) != 1:
            raise ValueError("All trajectory arrays must have the same length")
    
    @property
    def n_points(self) -> int:
        """Number of points in trajectory."""
        return len(self.time)
    
    @property
    def duration(self) -> float:
        """Total duration of motion [s]."""
        return float(self.time[-1] - self.time[0])
    
    @property
    def dt(self) -> np.ndarray:
        """Time step array [s]."""
        return np.diff(self.time, prepend=self.time[0])


def raster_path(
    x_start: float,
    x_end: float,
    y_start: float,
    y_end: float,
    z_focus: float,
    line_spacing: float,
    scan_speed: float,
    dt: float = 1e-4
) -> MotionPath:
    """
    Generate a raster (zig-zag) scan path.
    
    Args:
        x_start: Starting X position [m]
        x_end: Ending X position [m]
        y_start: Starting Y position [m]
        y_end: Ending Y position [m]
        z_focus: Constant Z (focus) height [m]
        line_spacing: Spacing between scan lines [m]
        scan_speed: Linear scan speed [m/s]
        dt: Time step for sampling [s]
        
    Returns:
        MotionPath with the raster trajectory
    """
    # Calculate number of lines
    n_lines = int(np.ceil((y_end - y_start) / line_spacing)) + 1
    y_positions = np.linspace(y_start, y_end, n_lines)
    
    # Build path point by point
    x_list, y_list, t_list = [], [], []
    current_time = 0.0
    
    for i, y_pos in enumerate(y_positions):
        # Alternate direction for each line
        if i % 2 == 0:
            x_line_start, x_line_end = x_start, x_end
        else:
            x_line_start, x_line_end = x_end, x_start
        
        # Time for this line
        line_length = abs(x_end - x_start)
        line_time = line_length / scan_speed
        n_points = max(int(line_time / dt), 2)
        
        # Sample points along line
        x_points = np.linspace(x_line_start, x_line_end, n_points)
        t_points = np.linspace(current_time, current_time + line_time, n_points)
        
        x_list.append(x_points)
        y_list.append(np.full(n_points, y_pos))
        t_list.append(t_points)
        
        current_time = t_points[-1] + dt  # Small gap between lines
    
    # Concatenate all segments
    x_arr = np.concatenate(x_list)
    y_arr = np.concatenate(y_list)
    t_arr = np.concatenate(t_list)
    z_arr = np.full_like(x_arr, z_focus)
    
    return MotionPath(time=t_arr, x=x_arr, y=y_arr, z=z_arr)


def spiral_path(
    center_x: float,
    center_y: float,
    z_focus: float,
    inner_radius: float,
    outer_radius: float,
    n_revolutions: float,
    scan_speed: float,
    dt: float = 1e-4
) -> MotionPath:
    """
    Generate an outward spiral scan path.
    
    Args:
        center_x: Center X position [m]
        center_y: Center Y position [m]
        z_focus: Constant Z (focus) height [m]
        inner_radius: Starting radius [m]
        outer_radius: Ending radius [m]
        n_revolutions: Number of spiral turns
        scan_speed: Linear scan speed [m/s]
        dt: Time step for sampling [s]
        
    Returns:
        MotionPath with the spiral trajectory
    """
    # Estimate total arc length for timing
    avg_radius = (inner_radius + outer_radius) / 2
    total_angle = n_revolutions * 2 * np.pi
    arc_length = avg_radius * total_angle
    total_time = arc_length / scan_speed
    
    n_points = max(int(total_time / dt), 100)
    t_arr = np.linspace(0, total_time, n_points)
    theta = np.linspace(0, total_angle, n_points)
    radius = np.linspace(inner_radius, outer_radius, n_points)
    
    x_arr = center_x + radius * np.cos(theta)
    y_arr = center_y + radius * np.sin(theta)
    z_arr = np.full_like(x_arr, z_focus)
    
    return MotionPath(time=t_arr, x=x_arr, y=y_arr, z=z_arr)


def linear_path(
    x_start: float,
    y_start: float,
    x_end: float,
    y_end: float,
    z_focus: float,
    scan_speed: float,
    dt: float = 1e-4
) -> MotionPath:
    """
    Generate a simple linear path.
    
    Args:
        x_start, y_start: Starting position [m]
        x_end, y_end: Ending position [m]
        z_focus: Constant Z height [m]
        scan_speed: Linear scan speed [m/s]
        dt: Time step [s]
        
    Returns:
        MotionPath with linear trajectory
    """
    distance = np.sqrt((x_end - x_start)**2 + (y_end - y_start)**2)
    total_time = distance / scan_speed
    n_points = max(int(total_time / dt), 2)
    
    t_arr = np.linspace(0, total_time, n_points)
    x_arr = np.linspace(x_start, x_end, n_points)
    y_arr = np.linspace(y_start, y_end, n_points)
    z_arr = np.full_like(x_arr, z_focus)
    
    return MotionPath(time=t_arr, x=x_arr, y=y_arr, z=z_arr)


def variable_z_path(
    base_path: MotionPath,
    z_profile: np.ndarray
) -> MotionPath:
    """
    Apply a variable Z profile to an existing path.
    
    Args:
        base_path: Existing MotionPath to modify
        z_profile: New Z values (must match path length)
        
    Returns:
        New MotionPath with variable Z
    """
    if len(z_profile) != base_path.n_points:
        raise ValueError("Z profile length must match path length")
    
    return MotionPath(
        time=base_path.time.copy(),
        x=base_path.x.copy(),
        y=base_path.y.copy(),
        z=z_profile.copy()
    )
