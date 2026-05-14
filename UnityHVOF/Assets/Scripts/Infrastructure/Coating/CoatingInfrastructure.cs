// ============================================================================
// HVOF Coating Microstructure Generator + Analysis — Stage 1 + Post-processing.
//
// Generator: Creates a 2D grid representing an as-sprayed HVOF coating with
//   stochastic pore seeding at splat boundaries.
//
// Analysis: Post-processing measurements for porosity, melt depth, HAZ,
//   hardness prediction, and substrate melting check.
//
// Port of: infrastructure/coating/coating_generator.py (115 lines)
//          infrastructure/coating/coating_analysis.py (255 lines)
// ============================================================================

using System;
using HVOFSim.Domain.Entities;

namespace HVOFSim.Infrastructure.Coating
{
    /// <summary>Thermal conductivity of air in pores [W/m·K].</summary>
    internal static class CoatingConstants
    {
        public const double KAir = 0.025;
    }

    // ══════════════════════════════════════════════════════════════════
    // GENERATOR
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a physically-representative HVOF coating microstructure.
    /// The grid has coating on top and substrate on the bottom.
    /// y=0 is the coating surface (top), y increases downward.
    /// </summary>
    public sealed class HVOFCoatingGenerator : HVOFSim.Domain.Ports.ICoatingGenerator
    {
        public CoatingGrid Generate(
            CoatingConfig config,
            double coatingK,
            double resolution = 2e-6,
            int? seed = null)
        {
            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            double dx = resolution, dy = resolution;

            // Grid dimensions
            int nx = Math.Max((int)(config.CoatingWidthM / dx), 10);
            int coatingRows = Math.Max((int)(config.CoatingThicknessM / dy), 5);
            int substrateRows = Math.Max((int)(config.SubstrateThicknessM / dy), 5);
            int ny = coatingRows + substrateRows;

            // Initialize: all solid
            var grid = new int[ny, nx];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    grid[y, x] = 1;

            // --- Seed pores in the coating region ---
            int coatingCells = coatingRows * nx;
            int numPores = (int)(config.TargetPorosity * coatingCells);
            numPores = Math.Min(numPores, coatingCells - 1);

            // Build probability weights per row (higher at splat boundaries)
            var weights = new double[coatingRows];
            int rowsPerSplat = Math.Max((int)(config.SplatThicknessM / dy), 1);

            double totalWeight = 0;
            for (int row = 0; row < coatingRows; row++)
            {
                weights[row] = (row % rowsPerSplat == 0)
                    ? config.BoundaryPoreFactor
                    : 1.0;
                totalWeight += weights[row] * nx;
            }

            // Sample pore locations using weighted random selection
            int poresPlaced = 0;
            int maxAttempts = numPores * 10;
            int attempts = 0;

            while (poresPlaced < numPores && attempts < maxAttempts)
            {
                // Pick a row weighted by boundary probability
                double r = rng.NextDouble() * totalWeight;
                double cumulative = 0;
                int selectedRow = 0;
                for (int row = 0; row < coatingRows; row++)
                {
                    cumulative += weights[row] * nx;
                    if (r <= cumulative)
                    {
                        selectedRow = row;
                        break;
                    }
                }

                int selectedCol = rng.Next(nx);

                if (grid[selectedRow, selectedCol] == 1) // Not already a pore
                {
                    grid[selectedRow, selectedCol] = 0;
                    poresPlaced++;
                }
                attempts++;
            }

            // --- Build thermal conductivity map ---
            var kMap = new double[ny, nx];
            for (int y = 0; y < coatingRows; y++)
                for (int x = 0; x < nx; x++)
                    kMap[y, x] = grid[y, x] == 1 ? coatingK : CoatingConstants.KAir;

            for (int y = coatingRows; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    kMap[y, x] = config.SubstrateK;

            return new CoatingGrid(grid, kMap, dx, dy, coatingRows, substrateRows);
        }
    }

}
