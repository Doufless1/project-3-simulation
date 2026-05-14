// ============================================================================
// 3D Finite Difference Method Heat Solver.
//
// Implements IHeatSolver for full 3D transient heat conduction:
//     ∂T/∂t = κ (∂²T/∂x² + ∂²T/∂y² + ∂²T/∂z²)
//
// Numerical scheme: Forward-Time Central-Space (FTCS) explicit method.
//
// CIA Triad:
//   - Integrity: Automatic CFL stability check before solving.
//   - Availability: Grid size limit to prevent memory exhaustion (DoS).
//
// DRY: Beam propagation logic is a static helper, not duplicated.
//
// Port of: infrastructure/solvers/fdm_heat_solver_3d.py (324 lines)
// ============================================================================

using System;
using System.Diagnostics;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Exceptions;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Solvers
{
    /// <summary>
    /// 3D transient heat solver using the explicit FTCS finite difference method.
    /// Solves: ∂T/∂t = κ · ∇²T with surface heat flux from laser.
    /// </summary>
    public sealed class FDMHeatSolver3D : IHeatSolver
    {
        /// <summary>Safety limit (Availability — DoS prevention).</summary>
        private const int MaxGridCells = 5_000_000;

        private readonly ILaserSource _laserSource;
        private readonly string _bottomBc;

        /// <summary>
        /// Create a 3D FDM heat solver.
        /// </summary>
        /// <param name="laserSource">Injected beam profile strategy (DIP).</param>
        /// <param name="bottomBc">Bottom boundary condition: "adiabatic" or "fixed".</param>
        public FDMHeatSolver3D(ILaserSource laserSource, string bottomBc = "adiabatic")
        {
            _laserSource = laserSource;
            _bottomBc = bottomBc;
        }

        public string Name => "FDM-3D-FTCS";

        public SimulationResult Solve(
            Material material,
            LaserBeam laser,
            Trajectory trajectory,
            double[] gridSize = null,
            double resolution = 100e-6)
        {
            gridSize ??= new[] { 0.010, 0.010, 0.002 };
            var stopwatch = Stopwatch.StartNew();

            // --- Grid Setup ---
            double lx = gridSize[0], ly = gridSize[1], lz = gridSize[2];
            double dx = resolution, dy = resolution, dz = resolution;

            int nx = Math.Max((int)(lx / dx), 3);
            int ny = Math.Max((int)(ly / dy), 3);
            int nz = Math.Max((int)(lz / dz), 3);

            int totalCells = nx * ny * nz;
            CheckGridSize(totalCells);

            double kappa = material.ThermalDiffusivity;

            // --- CFL Stability Check ---
            double dtMax = dx * dx / (6.0 * kappa); // 3D stability limit
            double dtTrajectory = ComputeTrajectoryDt(trajectory);
            double dt = Math.Min(dtMax * 0.8, dtTrajectory); // 80% safety margin

            if (dt <= 0)
                throw new SolverInstabilityException(
                    $"Invalid time step dt={dt:E2}s. Check material diffusivity.");

            // --- Initialize Temperature Field ---
            var tField = new double[nx, ny, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                        tField[i, j, k] = material.TAmbient;

            // Coordinate arrays
            double[] xCoords = Linspace(0, lx, nx);
            double[] yCoords = Linspace(0, ly, ny);
            double[] zCoords = Linspace(0, lz, nz);

            // 2D meshgrid for surface (z=0)
            var xMesh = new double[nx, ny];
            var yMesh = new double[nx, ny];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    xMesh[i, j] = xCoords[i];
                    yMesh[i, j] = yCoords[j];
                }

            // Cumulative fluence map
            var fluenceMap = new double[nx, ny];

            // --- Time-Stepping Loop ---
            int nSteps = trajectory.NPoints;
            long totalSubsteps = 0;
            const long MaxTotalSubsteps = 200_000; // Overall budget to prevent runaway

            for (int stepIdx = 0; stepIdx < nSteps - 1; stepIdx++)
            {
                double segDt = trajectory.Time[stepIdx + 1] - trajectory.Time[stepIdx];
                if (segDt <= 0) continue;

                int nSub = Math.Max((int)Math.Ceiling(segDt / dt), 1);
                
                // Prevent freeze on extreme per-segment substep counts
                if (nSub > 5000)
                {
                    throw new SolverInstabilityException(
                        $"Simulation parameters require {nSub} substeps per segment " +
                        $"(limit: 5000). Increase resolution, spot size, or scan speed.");
                }

                totalSubsteps += nSub;
                if (totalSubsteps > MaxTotalSubsteps)
                {
                    throw new SolverInstabilityException(
                        $"Total substeps ({totalSubsteps}) exceed budget ({MaxTotalSubsteps}). " +
                        $"The scan area is too large for the current resolution. " +
                        $"Try a smaller scan area or increase spot size / speed.");
                }

                double actualDt = segDt / nSub;

                double rxA = kappa * actualDt / (dx * dx);
                double ryA = kappa * actualDt / (dy * dy);
                double rzA = kappa * actualDt / (dz * dz);

                double beamX = trajectory.X[stepIdx];
                double beamY = trajectory.Y[stepIdx];
                double beamZDistance = trajectory.Z[stepIdx];

                // Compute spot radius at this defocus distance
                double wZ = ComputeSpotRadiusAtZ(beamZDistance, laser);

                // Compute surface intensity distribution
                var intensity2D = _laserSource.ComputeIntensity(
                    xMesh, yMesh, beamX, beamY, wZ, laser.Power);

                // Apply material absorption
                var absorbedIntensity = new double[nx, ny];
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                    {
                        absorbedIntensity[i, j] = material.Absorption * intensity2D[i, j];
                        fluenceMap[i, j] += absorbedIntensity[i, j] * segDt;
                    }

                // Surface heat flux rate
                var surfaceFluxRate = new double[nx, ny];
                double rhoTimeCpTimesDz = material.Density * material.SpecificHeat * dz;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                        surfaceFluxRate[i, j] = absorbedIntensity[i, j] / rhoTimeCpTimesDz;

                for (int sub = 0; sub < nSub; sub++)
                {
                    tField = DiffusionStep(
                        tField, nx, ny, nz,
                        rxA, ryA, rzA,
                        surfaceFluxRate, actualDt,
                        material.TAmbient);
                }
            }

            // --- Extract Results ---
            stopwatch.Stop();
            double elapsed = stopwatch.Elapsed.TotalSeconds;

            double peakTemp = double.MinValue;
            double peakFluence = double.MinValue;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (fluenceMap[i, j] > peakFluence) peakFluence = fluenceMap[i, j];
                    for (int k = 0; k < nz; k++)
                        if (tField[i, j, k] > peakTemp) peakTemp = tField[i, j, k];
                }

            double? meltDepth = FindThresholdDepth(tField, nx, ny, nz, zCoords, material.TMelt);
            double? vapDepth = FindThresholdDepth(tField, nx, ny, nz, zCoords, material.TVaporization);

            double totalEnergy = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    totalEnergy += fluenceMap[i, j];
            totalEnergy *= dx * dy;

            return new SimulationResult(
                temperatureField: tField,
                fluenceMap: fluenceMap,
                xCoords: xCoords,
                yCoords: yCoords,
                zCoords: zCoords,
                timeCoords: trajectory.Time,
                peakTemperatureCelsius: peakTemp,
                peakFluenceJPerM2: peakFluence,
                meltDepthM: meltDepth,
                vaporizationDepthM: vapDepth,
                totalEnergyJ: totalEnergy,
                materialName: material.Name,
                solverName: Name,
                durationSeconds: elapsed);
        }

        // ===== Private Methods (SRP: each handles one sub-task) =====

        private double[,,] DiffusionStep(
            double[,,] tField, int nx, int ny, int nz,
            double rx, double ry, double rz,
            double[,] surfaceSource, double dt, double tAmbient)
        {
            var tNew = (double[,,])tField.Clone();

            // Interior points: 3D Laplacian
            for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < ny - 1; j++)
                    for (int k = 1; k < nz - 1; k++)
                    {
                        tNew[i, j, k] = tField[i, j, k]
                            + rx * (tField[i + 1, j, k] + tField[i - 1, j, k] - 2 * tField[i, j, k])
                            + ry * (tField[i, j + 1, k] + tField[i, j - 1, k] - 2 * tField[i, j, k])
                            + rz * (tField[i, j, k + 1] + tField[i, j, k - 1] - 2 * tField[i, j, k]);
                    }

            // Top surface (z=0): Apply laser heat flux
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    tNew[i, j, 0] += surfaceSource[i, j] * dt;

            // Diffusion at surface using ghost node = z=1
            for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < ny - 1; j++)
                    tNew[i, j, 0] += rz * (tField[i, j, 1] - tField[i, j, 0]);

            // Lateral (x-y) diffusion at the surface — the interior loop
            // starts at k=1 so the surface was missing this contribution,
            // causing heat to pile up at the irradiated cell instead of
            // spreading laterally which under-predicts the heated zone width.
            for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < ny - 1; j++)
                    tNew[i, j, 0] += rx * (tField[i + 1, j, 0] + tField[i - 1, j, 0] - 2 * tField[i, j, 0])
                                   + ry * (tField[i, j + 1, 0] + tField[i, j - 1, 0] - 2 * tField[i, j, 0]);

            // Bottom boundary
            if (_bottomBc == "adiabatic")
            {
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                        tNew[i, j, nz - 1] = tNew[i, j, nz - 2];
            }
            else
            {
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                        tNew[i, j, nz - 1] = tAmbient;
            }

            // Lateral boundaries: Adiabatic (mirror)
            for (int j = 0; j < ny; j++)
                for (int k = 0; k < nz; k++)
                {
                    tNew[0, j, k] = tNew[1, j, k];
                    tNew[nx - 1, j, k] = tNew[nx - 2, j, k];
                }
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    tNew[i, 0, k] = tNew[i, 1, k];
                    tNew[i, ny - 1, k] = tNew[i, ny - 2, k];
                }

            return tNew;
        }

        /// <summary>Gaussian beam propagation: w(z) = w0 · √(1 + (z/z_R)²). DRY helper.</summary>
        private static double ComputeSpotRadiusAtZ(double z, LaserBeam laser)
        {
            double zR = laser.RayleighRange;
            return laser.SpotRadius * Math.Sqrt(1 + (z / zR) * (z / zR));
        }

        /// <summary>DoS prevention: reject grids that would exhaust memory.</summary>
        private static void CheckGridSize(int totalCells)
        {
            if (totalCells > MaxGridCells)
                throw new GridTooLargeException(totalCells, MaxGridCells);
        }

        /// <summary>Compute the smallest time step from the trajectory.</summary>
        private static double ComputeTrajectoryDt(Trajectory trajectory)
        {
            double minDt = double.MaxValue;
            for (int i = 0; i < trajectory.NPoints - 1; i++)
            {
                double segDt = trajectory.Time[i + 1] - trajectory.Time[i];
                if (segDt > 0 && segDt < minDt)
                    minDt = segDt;
            }
            return minDt < double.MaxValue ? minDt : 1e-4;
        }

        /// <summary>Find maximum depth where temperature exceeds threshold.</summary>
        private static double? FindThresholdDepth(
            double[,,] tField, int nx, int ny, int nz,
            double[] zCoords, double threshold)
        {
            int deepestK = -1;
            for (int k = 0; k < nz; k++)
            {
                double maxAtZ = double.MinValue;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                        if (tField[i, j, k] > maxAtZ)
                            maxAtZ = tField[i, j, k];

                if (maxAtZ > threshold)
                    deepestK = k;
            }
            return deepestK >= 0 ? (double?)zCoords[deepestK] : null;
        }

        /// <summary>Generate evenly spaced array (like numpy.linspace).</summary>
        private static double[] Linspace(double start, double end, int count)
        {
            var result = new double[count];
            if (count == 1) { result[0] = start; return result; }
            double step = (end - start) / (count - 1);
            for (int i = 0; i < count; i++)
                result[i] = start + i * step;
            return result;
        }
    }
}
