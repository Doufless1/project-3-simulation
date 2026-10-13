"""
Smoke Tests — 3D lab figure builder.

Coverage: The Plotly 3D lab scene builds for both the idle and the
          fully-active (firing, gas flowing, interlocks locked) states.
"""

import unittest

import plotly.graph_objects as go

from lab_control.presentation.visualizations import build_3d_lab_figure


class TestBuild3DLabFigure(unittest.TestCase):

    def test_idle_scene_builds(self):
        fig = build_3d_lab_figure()
        self.assertIsInstance(fig, go.Figure)
        self.assertGreater(len(fig.data), 0)

    def test_active_scene_builds(self):
        fig = build_3d_lab_figure(
            pos_x_mm=150.0,
            pos_y_mm=120.0,
            laser_active=True,
            laser_power_pct=30.0,
            gas_flowing=True,
            gas_flow_rate=15.0,
            door_locked=True,
            chamber_locked=True,
        )
        self.assertIsInstance(fig, go.Figure)
        self.assertGreater(len(fig.data), 0)


if __name__ == "__main__":
    unittest.main()
