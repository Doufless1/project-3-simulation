"""
Tests for the HVOF Coating + Laser Treatment Simulation.

Covers:
  - Coating microstructure generation (Stage 1)
  - Porosity measurement and closure (Post-processing)
  - Hardness prediction
  - Integration: two-stage simulation
"""

import unittest
import sys
import os

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import numpy as np

from domain.entities import Material
from domain.coating_entities import CoatingConfig, CoatingGrid
from infrastructure.coating.coating_generator import HVOFCoatingGenerator
from infrastructure.coating.coating_analysis import (
    measure_porosity_change,
    measure_melt_depth,
    measure_haz_depth,
    predict_hardness,
    check_substrate_melting,
)


# ============================================================================
# Test Material
# ============================================================================

WC_NICR = Material(
    name="WC-NiCr (Test)",
    absorption=0.4,
    thermal_conductivity=12.0,
    density=13500.0,
    specific_heat=350.0,
    t_melt=1350.0,
    t_vaporization=2800.0,
    t_ambient=25.0,
)


class TestCoatingGeneration(unittest.TestCase):
    """Tests for HVOFCoatingGenerator (Stage 1)."""

    def setUp(self):
        self.gen = HVOFCoatingGenerator()
        self.config = CoatingConfig(
            coating_thickness_m=300e-6,
            coating_width_m=500e-6,
            target_porosity=0.03,
            splat_thickness_m=7.5e-6,
        )

    def test_grid_shape(self):
        """Grid should have correct dimensions."""
        res = 5e-6
        grid = self.gen.generate(self.config, coating_k=12.0, resolution=res, seed=42)

        expected_nx = int(self.config.coating_width_m / res)
        expected_coating_rows = int(self.config.coating_thickness_m / res)
        expected_substrate_rows = int(self.config.substrate_thickness_m / res)

        self.assertEqual(grid.nx, expected_nx)
        self.assertEqual(grid.coating_rows, expected_coating_rows)
        self.assertEqual(grid.substrate_rows, expected_substrate_rows)
        self.assertEqual(grid.ny, expected_coating_rows + expected_substrate_rows)

    def test_porosity_within_target(self):
        """Generated porosity should be close to the target."""
        grid = self.gen.generate(self.config, coating_k=12.0, resolution=5e-6, seed=42)
        actual = grid.porosity
        self.assertAlmostEqual(actual, self.config.target_porosity, delta=0.005)

    def test_substrate_is_solid(self):
        """Substrate region should have no pores."""
        grid = self.gen.generate(self.config, coating_k=12.0, resolution=5e-6, seed=42)
        substrate = grid.grid[grid.coating_rows:, :]
        self.assertTrue(np.all(substrate == 1))

    def test_conductivity_map(self):
        """Pore cells should have low k, solid cells should have coating k."""
        grid = self.gen.generate(self.config, coating_k=12.0, resolution=5e-6, seed=42)
        coating_region = grid.grid[:grid.coating_rows, :]
        k_coating = grid.k_map[:grid.coating_rows, :]

        # Solid cells
        solid_k = k_coating[coating_region == 1]
        self.assertTrue(np.all(solid_k == 12.0))

        # Pore cells
        pore_k = k_coating[coating_region == 0]
        if len(pore_k) > 0:
            self.assertTrue(np.all(pore_k < 1.0))  # Air conductivity

    def test_splat_boundary_pores(self):
        """More pores should be at splat boundary rows."""
        grid = self.gen.generate(self.config, coating_k=12.0, resolution=5e-6, seed=42)
        coating = grid.grid[:grid.coating_rows, :]
        rows_per_splat = max(int(self.config.splat_thickness_m / 5e-6), 1)

        boundary_pores = 0
        interior_pores = 0
        boundary_cells = 0
        interior_cells = 0

        for row in range(grid.coating_rows):
            pores_in_row = np.sum(coating[row, :] == 0)
            if row % rows_per_splat == 0:
                boundary_pores += pores_in_row
                boundary_cells += grid.nx
            else:
                interior_pores += pores_in_row
                interior_cells += grid.nx

        # Boundary pore density should be higher than interior
        if boundary_cells > 0 and interior_cells > 0:
            boundary_density = boundary_pores / boundary_cells
            interior_density = interior_pores / interior_cells
            self.assertGreater(boundary_density, interior_density)

    def test_reproducibility(self):
        """Same seed should produce identical grids."""
        g1 = self.gen.generate(self.config, coating_k=12.0, resolution=5e-6, seed=123)
        g2 = self.gen.generate(self.config, coating_k=12.0, resolution=5e-6, seed=123)
        np.testing.assert_array_equal(g1.grid, g2.grid)


class TestCoatingAnalysis(unittest.TestCase):
    """Tests for post-processing analysis functions."""

    def _make_grid(self, porosity=0.05):
        """Create a simple test coating grid."""
        ny_coat, nx = 60, 100
        ny_sub = 40
        ny = ny_coat + ny_sub
        grid = np.ones((ny, nx), dtype=np.float64)

        # Add pores
        n_pores = int(porosity * ny_coat * nx)
        np.random.seed(99)
        idx = np.random.choice(ny_coat * nx, n_pores, replace=False)
        flat = grid[:ny_coat, :].flatten()
        flat[idx] = 0
        grid[:ny_coat, :] = flat.reshape(ny_coat, nx)

        k_map = np.where(grid == 1, 12.0, 0.025)
        k_map[ny_coat:, :] = 50.0  # Substrate

        return CoatingGrid(
            grid=grid, k_map=k_map, dx=5e-6, dy=5e-6,
            coating_rows=ny_coat, substrate_rows=ny_sub,
        )

    def test_porosity_reduction_when_melted(self):
        """Porosity should decrease when pores are in the melt zone."""
        cg = self._make_grid(porosity=0.05)
        # Simulate melting in the top 20 rows
        melted = np.zeros_like(cg.grid, dtype=bool)
        melted[:20, :] = True

        before, after, reduction = measure_porosity_change(cg, melted)
        self.assertGreater(before, 0)
        self.assertLess(after, before)
        self.assertGreater(reduction, 0)

    def test_no_porosity_change_without_melting(self):
        """No melting → no porosity change."""
        cg = self._make_grid(porosity=0.05)
        melted = np.zeros_like(cg.grid, dtype=bool)

        before, after, reduction = measure_porosity_change(cg, melted)
        self.assertAlmostEqual(before, after)
        self.assertAlmostEqual(reduction, 0.0)

    def test_melt_depth(self):
        """Melt depth should reflect deepest melted row."""
        melted = np.zeros((100, 200), dtype=bool)
        melted[:30, 50:150] = True  # Melted top 30 rows
        dy = 5e-6

        depth = measure_melt_depth(melted, dy)
        expected = 29 * dy * 1e6  # 0-indexed, deepest row = 29
        self.assertAlmostEqual(depth, expected, places=1)

    def test_zero_melt_depth(self):
        """No melting → zero melt depth."""
        melted = np.zeros((100, 200), dtype=bool)
        self.assertEqual(measure_melt_depth(melted, 5e-6), 0.0)

    def test_haz_depth(self):
        """HAZ should exist below the melt zone."""
        T = np.full((100, 200), 25.0)
        T[:30, :] = 1500.0   # Melted zone
        T[30:50, :] = 400.0  # HAZ (hot but not melted)
        T[50:60, :] = 100.0  # Below HAZ threshold

        melted = T > 1350.0
        haz = measure_haz_depth(T, melted, dy=5e-6, t_haz_threshold=200.0)
        self.assertGreater(haz, 0)

    def test_hardness_increases_with_densification(self):
        """Hardness should increase when porosity decreases."""
        hv_before = 1000.0
        hv_after = predict_hardness(hv_before, 0.03, 0.01, beta=0.3)
        self.assertGreater(hv_after, hv_before)

    def test_hardness_unchanged_without_densification(self):
        """No porosity change → no hardness change."""
        hv = predict_hardness(1000.0, 0.03, 0.03, beta=0.3)
        self.assertAlmostEqual(hv, 1000.0)

    def test_substrate_melting_detection(self):
        """Should detect when substrate exceeds its melting point."""
        T = np.full((100, 200), 25.0)
        coating_rows = 60

        # Substrate stays cool
        melted, max_t = check_substrate_melting(T, coating_rows, 1400.0)
        self.assertFalse(melted)

        # Substrate overheats
        T[80:, :] = 1500.0
        melted, max_t = check_substrate_melting(T, coating_rows, 1400.0)
        self.assertTrue(melted)
        self.assertGreater(max_t, 1400.0)


if __name__ == "__main__":
    unittest.main()
