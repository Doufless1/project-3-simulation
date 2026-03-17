// ============================================================================
// 2D Finite Difference Coating Solver — Stage 2 Laser Treatment.
//
// Specialized 2D FDM solver for laser treatment of HVOF coatings.
// Differs from FDMHeatSolver3D:
//   1. Operates on a 2D coating grid with spatially varying k (pore-aware)
//   2. Uses Beer-Lambert volumetric absorption: Q ~ exp(-mu*y)
//   3. Tracks cumulative melt history per cell
//   4. Handles coating + substrate as distinct material regions
//
// Governing equation:
//     rho*cp * dT/dt = d/dx(k*dT/dx) + d/dy(k*dT/dy) + Q(x,y,t)
//
// Port of: infrastructure/solvers/fdm_coating_solver_2d.py (222 lines)
// ============================================================================

using System;
using System.Diagnostics;
using HVOFSim.Domain.Entities;

namespace HVOFSim.Infrastructure.Solvers
{
    /// <summary>
    /// 2D transient heat solver for laser treatment of HVOF coatings.
    /// Handles spatially varying thermal conductivity and tracks
    /// cumulative melting for porosity closure analysis.
    /// </summary>
    public sealed class FDMCoatingSolver2D : HVOFSim.Domain.Ports.ICoatingSolver
    {
        private readonly double _mu;

        /// <summary>
        /// Create a 2D coating solver.
        /// </summary>
        /// <param name="absorptionCoeff">Optical absorption coefficient mu [1/m].
        /// Controls Beer-Lambert penetration depth. Typical for WC coatings: ~1e4.</param>
        public FDMCoatingSolver2D(double absorptionCoeff = 1e4)
        {
            _mu = absorptionCoeff;
        }

        /// <summary>
        /// Run the 2D laser treatment simulation.
        /// </summary>
        /// <returns>Dictionary-like result with temperature field, melted mask, and duration.</returns>
        public CoatingSolverOutput Solve(
            CoatingGrid coatingGrid,
            CoatingConfig config,
            double laserPower,
            double beamRadius,
            double scanSpeed,
            double absorptivity,
            double coatingRho,
            double coatingCp,
            double tMelt,
            double tAmbient = 25.0)
        {
            var stopwatch = Stopwatch.StartNew();

            int ny = coatingGrid.Ny;
            int nx = coatingGrid.Nx;
            double dx = coatingGrid.Dx;
            double dy = coatingGrid.Dy;

            // --- Build property arrays (spatially varying) ---
            var rho = new double[ny, nx];
            var cp = new double[ny, nx];
            int cr = coatingGrid.CoatingRows;

            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    if (y < cr)
                    {
                        rho[y, x] = coatingRho;
                        cp[y, x] = coatingCp;
                    }
                    else
                    {
                        rho[y, x] = config.SubstrateRho;
                        cp[y, x] = config.SubstrateCp;
                    }
                }

            // Copy k map
            var k = new double[ny, nx];
            Array.Copy(coatingGrid.KMap, k, ny * nx);

            // --- CFL Stability ---
            double kappaMax = 0;
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    double kp = k[y, x] / (rho[y, x] * cp[y, x]);
                    if (kp > kappaMax) kappaMax = kp;
                }

            double dtMax = 1.0 / (2.0 * kappaMax * (1.0 / (dx * dx) + 1.0 / (dy * dy)));
            double dt = dtMax * 0.4; // 40% safety margin

            // --- Simulation time ---
            double scanLength = nx * dx;
            double totalTime = scanLength / scanSpeed;
            int nSteps = (int)Math.Ceiling(totalTime / dt);

            int maxSteps = 500_000;
            if (nSteps > maxSteps)
            {
                dt = totalTime / maxSteps;
                nSteps = maxSteps;
            }

            // --- Initialize ---
            var T = new double[ny, nx];
            var melted = new bool[ny, nx];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    T[y, x] = tAmbient;

            // Coordinate arrays
            var xCoords = new double[nx];
            var yCoords = new double[ny];
            for (int x = 0; x < nx; x++) xCoords[x] = x * dx;
            for (int y = 0; y < ny; y++) yCoords[y] = y * dy;

            // Beer-Lambert depth factor (precomputed)
            var depthFactor = new double[ny];
            for (int y = 0; y < ny; y++)
                depthFactor[y] = Math.Exp(-_mu * yCoords[y]);

            // Gaussian prefactor
            double gaussPrefactor = _mu * 2.0 * absorptivity * laserPower
                                    / (Math.PI * beamRadius * beamRadius);

            // --- Precompute harmonic mean conductivities ---
            int inyH = ny - 2, inxH = nx - 2;
            var kxpDt = new double[inyH, inxH];
            var kxmDt = new double[inyH, inxH];
            var kypDt = new double[inyH, inxH];
            var kymDt = new double[inyH, inxH];
            var rhoCpInv = new double[inyH, inxH];

            for (int y = 0; y < inyH; y++)
                for (int x = 0; x < inxH; x++)
                {
                    int yi = y + 1, xi = x + 1;
                    double kc = k[yi, xi];

                    double kXp = 2.0 * kc * k[yi, xi + 1] / (kc + k[yi, xi + 1] + 1e-30);
                    double kXm = 2.0 * kc * k[yi, xi - 1] / (kc + k[yi, xi - 1] + 1e-30);
                    double kYp = 2.0 * kc * k[yi + 1, xi] / (kc + k[yi + 1, xi] + 1e-30);
                    double kYm = 2.0 * kc * k[yi - 1, xi] / (kc + k[yi - 1, xi] + 1e-30);

                    kxpDt[y, x] = kXp / (dx * dx) * dt;
                    kxmDt[y, x] = kXm / (dx * dx) * dt;
                    kypDt[y, x] = kYp / (dy * dy) * dt;
                    kymDt[y, x] = kYm / (dy * dy) * dt;

                    rhoCpInv[y, x] = 1.0 / (rho[yi, xi] * cp[yi, xi]);
                }

            // --- Time-stepping loop ---
            for (int step = 0; step < nSteps; step++)
            {
                double t = step * dt;
                double laserX = scanSpeed * t;

                // Volumetric heat source for interior
                for (int y = 0; y < inyH; y++)
                {
                    int yi = y + 1;
                    for (int x = 0; x < inxH; x++)
                    {
                        int xi = x + 1;

                        // Gaussian lateral profile
                        double dxL = xCoords[xi] - laserX;
                        double gaussX = Math.Exp(-2.0 * dxL * dxL / (beamRadius * beamRadius));
                        double Q = gaussPrefactor * depthFactor[yi] * gaussX;

                        // FDM diffusion
                        double dTx = kxpDt[y, x] * (T[yi, xi + 1] - T[yi, xi])
                                   - kxmDt[y, x] * (T[yi, xi] - T[yi, xi - 1]);
                        double dTy = kypDt[y, x] * (T[yi + 1, xi] - T[yi, xi])
                                   - kymDt[y, x] * (T[yi, xi] - T[yi - 1, xi]);

                        T[yi, xi] += (dTx + dTy) * rhoCpInv[y, x]
                                   + Q * dt * rhoCpInv[y, x];
                    }
                }

                // Boundary conditions
                for (int x = 0; x < nx; x++)
                {
                    T[0, x] = T[1, x];           // Top: adiabatic
                    T[ny - 1, x] = tAmbient;     // Bottom: fixed
                }
                for (int y = 0; y < ny; y++)
                {
                    T[y, 0] = T[y, 1];           // Left: adiabatic
                    T[y, nx - 1] = T[y, nx - 2]; // Right: adiabatic
                }

                // Cumulative melt tracking
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                        if (T[y, x] > tMelt)
                            melted[y, x] = true;
            }

            stopwatch.Stop();

            return new CoatingSolverOutput(T, melted, stopwatch.Elapsed.TotalSeconds);
        }
    }
}
