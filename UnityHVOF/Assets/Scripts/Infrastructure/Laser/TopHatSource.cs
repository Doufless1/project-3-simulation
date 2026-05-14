// ============================================================================
// Top-Hat Laser Source — Implements ILaserSource.
//
// Pattern: Strategy — interchangeable with GaussianSource.
// Computes uniform intensity within the spot radius, zero outside.
//
// Port of: infrastructure/laser/tophat_source.py
// ============================================================================

using System;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Laser
{
    /// <summary>Top-hat (flat-top) beam profile intensity distribution.</summary>
    public sealed class TopHatSource : ILaserSource
    {
        public double[,] ComputeIntensity(
            double[,] xGrid, double[,] yGrid,
            double centerX, double centerY,
            double spotRadius, double power)
        {
            int nx = xGrid.GetLength(0);
            int ny = xGrid.GetLength(1);
            var intensity = new double[nx, ny];

            double uniformIntensity = power / (Math.PI * spotRadius * spotRadius);
            double r2Max = spotRadius * spotRadius;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    double dx = xGrid[i, j] - centerX;
                    double dy = yGrid[i, j] - centerY;
                    double r2 = dx * dx + dy * dy;
                    intensity[i, j] = r2 <= r2Max ? uniformIntensity : 0.0;
                }
            }

            return intensity;
        }
    }
}
