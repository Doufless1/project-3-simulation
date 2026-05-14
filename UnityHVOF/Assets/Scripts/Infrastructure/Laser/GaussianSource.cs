// ============================================================================
// Gaussian Laser Source — Implements ILaserSource.
//
// Pattern: Strategy — interchangeable with TopHatSource.
// Computes: I(x,y) = (2P / πw²) · exp(-2((x-cx)² + (y-cy)²) / w²)
//
// Port of: infrastructure/laser/gaussian_source.py
// ============================================================================

using System;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Laser
{
    /// <summary>Gaussian (TEM00) beam profile intensity distribution.</summary>
    public sealed class GaussianSource : ILaserSource
    {
        public double[,] ComputeIntensity(
            double[,] xGrid, double[,] yGrid,
            double centerX, double centerY,
            double spotRadius, double power)
        {
            int nx = xGrid.GetLength(0);
            int ny = xGrid.GetLength(1);
            var intensity = new double[nx, ny];

            double prefactor = 2.0 * power / (Math.PI * spotRadius * spotRadius);
            double invW2 = -2.0 / (spotRadius * spotRadius);

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    double dx = xGrid[i, j] - centerX;
                    double dy = yGrid[i, j] - centerY;
                    double r2 = dx * dx + dy * dy;
                    intensity[i, j] = prefactor * Math.Exp(invW2 * r2);
                }
            }

            return intensity;
        }
    }
}
