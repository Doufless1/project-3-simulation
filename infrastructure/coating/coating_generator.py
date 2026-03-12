"""
HVOF Coating Microstructure Generator — Stage 1.

Generates a 2D grid representing an as-sprayed HVOF coating with:
  - Stochastic pore seeding to target porosity
  - Preferential pore placement at splat boundaries (lamellar structure)
  - Substrate layer below the coating
  - Spatially varying thermal conductivity map

Physics basis:
  - Real HVOF coatings are built from flattened splats (~5-10 µm thick)
  - Pores concentrate at inter-splat boundaries due to incomplete bonding
  - Typical as-sprayed porosity: 2.5-5% vol for WC-based coatings
"""

import numpy as np

from domain.coating_entities import CoatingConfig, CoatingGrid


# Thermal conductivity of air in pores [W/m·K]
K_AIR = 0.025


class HVOFCoatingGenerator:
    """
    Generates a physically-representative HVOF coating microstructure.

    The grid has coating on top and substrate on the bottom.
    y=0 is the coating surface (top), y increases downward.
    """

    def generate(
        self,
        config: CoatingConfig,
        coating_k: float,
        resolution: float = 2e-6,
        seed: int = None,
    ) -> CoatingGrid:
        """
        Generate a 2D coating + substrate grid.

        Args:
            config: Coating configuration parameters.
            coating_k: Thermal conductivity of solid coating material [W/m·K].
            resolution: Grid cell size dx = dy [m]. Default 2 µm.
            seed: Random seed for reproducibility.

        Returns:
            CoatingGrid with solid/pore grid and conductivity map.
        """
        if seed is not None:
            np.random.seed(seed)

        dx = dy = resolution

        # Grid dimensions
        nx = max(int(config.coating_width_m / dx), 10)
        coating_rows = max(int(config.coating_thickness_m / dy), 5)
        substrate_rows = max(int(config.substrate_thickness_m / dy), 5)
        ny = coating_rows + substrate_rows

        # Initialize: all solid
        grid = np.ones((ny, nx), dtype=np.float64)

        # --- Seed pores in the coating region ---
        coating_cells = coating_rows * nx
        num_pores = int(config.target_porosity * coating_cells)

        # Build probability map: higher at splat boundaries
        prob_map = np.ones(coating_rows, dtype=np.float64)
        rows_per_splat = max(int(config.splat_thickness_m / dy), 1)

        for row in range(coating_rows):
            if row % rows_per_splat == 0:
                # This row is a splat boundary
                prob_map[row] = config.boundary_pore_factor

        # Normalize to probability distribution
        # Expand to full 2D
        prob_2d = np.repeat(prob_map[:, np.newaxis], nx, axis=1)
        prob_flat = prob_2d.flatten()
        prob_flat /= prob_flat.sum()

        # Sample pore locations (without replacement)
        num_pores = min(num_pores, coating_cells - 1)  # Safety
        pore_indices = np.random.choice(
            coating_cells, size=num_pores, replace=False, p=prob_flat
        )

        # Place pores in the coating region
        coating_region = grid[:coating_rows, :].flatten()
        coating_region[pore_indices] = 0
        grid[:coating_rows, :] = coating_region.reshape(coating_rows, nx)

        # --- Build thermal conductivity map ---
        k_map = np.full((ny, nx), coating_k, dtype=np.float64)

        # Pores have air conductivity
        k_map[:coating_rows, :] = np.where(
            grid[:coating_rows, :] == 1, coating_k, K_AIR
        )

        # Substrate region
        k_map[coating_rows:, :] = config.substrate_k

        return CoatingGrid(
            grid=grid,
            k_map=k_map,
            dx=dx,
            dy=dy,
            coating_rows=coating_rows,
            substrate_rows=substrate_rows,
        )
