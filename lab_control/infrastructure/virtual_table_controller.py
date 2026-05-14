"""
Virtual Table Controller — Simulates X-Y table motion and generates G-code.

CIA Triad:
  - Integrity: All positions validated through domain entity.
  - Availability: Rate limiting prevents excessive scan point generation (DoS).

STRIDE:
  - DoS: Maximum path length enforced.
  - Tampering: Positions generated from validated ScanRecipe.
"""

import math
from typing import List

from lab_control.domain.entities import XYTable, TableState
from lab_control.domain.value_objects import Position2D, GCodeLine, ScanRecipe
from lab_control.domain.ports import ITableController
from lab_control.domain.exceptions import TableLimitError

# STRIDE: DoS prevention — limit scan complexity
MAX_SCAN_POINTS = 50_000


class VirtualTableController(ITableController):
    """
    Simulated X-Y table controller.

    Generates motion paths and G-code without real hardware.
    """

    def home(self, table: XYTable) -> None:
        """Simulate homing sequence."""
        table.home()

    def move_to(self, table: XYTable, target: Position2D) -> None:
        """Simulate point-to-point move."""
        table.move_to(target)

    def execute_scan(
        self, table: XYTable, recipe: ScanRecipe
    ) -> List[Position2D]:
        """
        Generate and execute a scan pattern.

        Strategy pattern: dispatches to the correct generator
        based on recipe.pattern.
        """
        table.validate_ready()
        generators = {
            "raster": self._generate_raster_path,
            "spiral": self._generate_spiral_path,
            "linear": self._generate_linear_path,
        }
        generator = generators[recipe.pattern]
        path = generator(recipe)
        self._validate_path_within_limits(table, path)
        table.state = TableState.SCANNING
         # No need to assign every intermediate point — the dataclass field
        # is overwritten 1600+ times with no observer between writes.
        # Only the final position matters for the post-scan state.
        if path:
            table.position = path[-1]
        table.state = TableState.IDLE
        return path

    def generate_gcode(self, recipe: ScanRecipe) -> List[GCodeLine]:
        """
        Convert a scan recipe into standard G-code.

        Output is compatible with GRBL / LinuxCNC controllers.
        """
        generators = {
            "raster": self._generate_raster_path,
            "spiral": self._generate_spiral_path,
            "linear": self._generate_linear_path,
        }
        path = generators[recipe.pattern](recipe)
        return self._path_to_gcode(path, recipe.speed_mm_s)

    # ── Private: Pattern Generators ───────────────────────────────────

    def _generate_raster_path(
        self, recipe: ScanRecipe
    ) -> List[Position2D]:
        """
        Bidirectional raster scan (serpentine).

        Scan lines run along X, stepping in Y by line_spacing.
        """
        points: List[Position2D] = []
        spacing = recipe.line_spacing_mm
        step_x = recipe.spot_mm * 0.5
        num_y_lines = max(1, int(recipe.height_mm / spacing))
        num_x_steps = max(1, int(recipe.width_mm / step_x))
        self._check_point_count(num_y_lines * num_x_steps)

        for y_idx in range(num_y_lines):
            y = y_idx * spacing
            x_range = range(num_x_steps)
            if y_idx % 2 == 1:
                x_range = reversed(x_range)
            for x_idx in x_range:
                x = x_idx * step_x
                points.append(Position2D(x, y))
        return points

    def _generate_spiral_path(
        self, recipe: ScanRecipe
    ) -> List[Position2D]:
        """
        Archimedean spiral from center of scan area.
        """
        points: List[Position2D] = []
        cx = recipe.width_mm / 2.0
        cy = recipe.height_mm / 2.0
        max_r = min(cx, cy)
        spacing = recipe.line_spacing_mm
        num_turns = max_r / spacing
        num_points = int(num_turns * 60)
        self._check_point_count(num_points)

        for i in range(num_points):
            theta = (i / 60.0) * 2 * math.pi
            r = (i / num_points) * max_r
            x = cx + r * math.cos(theta)
            y = cy + r * math.sin(theta)
            points.append(Position2D(x, y))
        return points

    def _generate_linear_path(
        self, recipe: ScanRecipe
    ) -> List[Position2D]:
        """
        Single-pass linear scan along X axis at mid-Y.
        """
        step_x = recipe.spot_mm * 0.5
        num_points = max(2, int(recipe.width_mm / step_x))
        self._check_point_count(num_points)
        mid_y = recipe.height_mm / 2.0
        return [
            Position2D(i * step_x, mid_y)
            for i in range(num_points)
        ]

    # ── Private: G-code Builder ───────────────────────────────────────

    def _path_to_gcode(
        self, path: List[Position2D], feed_mm_s: float
    ) -> List[GCodeLine]:
        """Convert a list of positions into valid G-code instructions."""
        feed_mm_min = feed_mm_s * 60.0
        lines: List[GCodeLine] = []

        lines.append(GCodeLine("G90", 0))            # Absolute mode
        lines.append(GCodeLine("G21", 1))            # Metric (mm)
        lines.append(GCodeLine("G28 X0 Y0", 2))     # Home
        lines.append(
            GCodeLine(f"F{feed_mm_min:.1f}", 3)      # Feed rate
        )
        lines.append(GCodeLine("M3 S100", 4))        # Laser ON

        for idx, pt in enumerate(path):
            lines.append(
                GCodeLine(
                    f"G1 X{pt.x_mm:.3f} Y{pt.y_mm:.3f}",
                    5 + idx,
                )
            )

        lines.append(GCodeLine("M5", 5 + len(path)))        # Laser OFF
        lines.append(GCodeLine("G28 X0 Y0", 6 + len(path))) # Home
        lines.append(GCodeLine("M2", 7 + len(path)))        # End program
        return lines

    # ── Private: Safety Checks ────────────────────────────────────────

    def _validate_path_within_limits(
        self, table: XYTable, path: List[Position2D]
    ) -> None:
        """Integrity: Every point in the path must be within travel."""
        for pt in path:
            table.validate_position(pt)

    def _check_point_count(self, count: int) -> None:
        """STRIDE DoS: Prevent excessive scan point generation."""
        if count > MAX_SCAN_POINTS:
            raise TableLimitError(
                f"Scan generates {count} points, "
                f"exceeding limit of {MAX_SCAN_POINTS}."
            )
