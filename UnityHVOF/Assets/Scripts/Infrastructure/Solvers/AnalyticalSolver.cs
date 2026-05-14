// ============================================================================
// Analytical Heat Solver — Closed-form solution for semi-infinite solid.
//
// Implements IHeatSolver using the analytical formula:
//     T(z,t) = T_amb + (Q / (rho*c_p*sqrt(pi*kappa*t))) * exp(-z^2/(4*kappa*t))
//
// Use case: Quick validation and comparison against the FDM solver.
// Limitation: Only handles 1D depth profile at the point of peak fluence.
//
// Port of: infrastructure/solvers/analytical_solver.py
// ============================================================================

using System;
using System.Diagnostics;
using System.Collections.Generic;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Solvers
{
    /// <summary>
    /// Analytical solution for instantaneous surface heating.
    /// Suitable for fast validation of the FDM solver.
    /// </summary>
    public sealed class AnalyticalSolver : IHeatSolver
    {
        private readonly ILaserSource _laserSource;

        public AnalyticalSolver(ILaserSource laserSource)
        {
            _laserSource = laserSource ?? throw new ArgumentNullException(nameof(laserSource));
        }

        public string Name => "Analytical-1D";

        public SimulationResult Solve(
            Material material,
            LaserBeam laser,
            Trajectory trajectory,
            double[] gridSize,
            double resolution)
        {
            Stopwatch sw = Stopwatch.StartNew();

            double lz = gridSize[2];
            int nz = Math.Max((int)(lz / resolution), 10);
            double[] zCoords = new double[nz];
            for (int i = 0; i < nz; i++)
                zCoords[i] = i * lz / (nz - 1);

            double kappa = material.ThermalDiffusivity;
            double rho = material.Density;
            double cp = material.SpecificHeat;

            // Estimate total fluence at center from trajectory
            double totalFluence = EstimateCenterFluence(laser, trajectory);

            // Absorbed fluence
            double qAbsorbed = material.Absorption * totalFluence; // J/m^2

            // Effective pulse duration
            double pulseDuration = trajectory.Duration;
            if (pulseDuration <= 0) pulseDuration = 1e-6;

            // T(z) at end of pulse
            double tEval = pulseDuration;
            double denominator = rho * cp * Math.Sqrt(Math.PI * kappa * tEval);

            double[] temperatureZ = new double[nz];
            double peakTemp = material.TAmbient;
            double? meltDepth = null;
            double? vapDepth = null;

            if (denominator <= 0)
            {
                for (int i = 0; i < nz; i++) temperatureZ[i] = material.TAmbient;
            }
            else
            {
                for (int i = 0; i < nz; i++)
                {
                    double z = zCoords[i];
                    double exponent = -(z * z) / (4 * kappa * tEval);
                    temperatureZ[i] = material.TAmbient + (qAbsorbed / denominator) * Math.Exp(exponent);

                    if (temperatureZ[i] > peakTemp) peakTemp = temperatureZ[i];
                }
            }

            // Find melt depth and vaporization depth (deepest index that exceeds temp)
            for (int i = nz - 1; i >= 0; i--)
            {
                if (temperatureZ[i] > material.TMelt && !meltDepth.HasValue)
                {
                    meltDepth = zCoords[i];
                }
                if (temperatureZ[i] > material.TVaporization && !vapDepth.HasValue)
                {
                    vapDepth = zCoords[i];
                }
                
                if (meltDepth.HasValue && vapDepth.HasValue) break;
            }

            sw.Stop();

            return new SimulationResult(
                temperatureField: ConvertTo3DArray(temperatureZ),
                fluenceMap: new double[0,0], // Analytical doesn't generate 2D map
                xCoords: new double[0],
                yCoords: new double[0],
                zCoords: zCoords,
                timeCoords: new double[] { 0.0, pulseDuration },
                peakTemperatureCelsius: peakTemp,
                peakFluenceJPerM2: qAbsorbed,
                meltDepthM: meltDepth,
                vaporizationDepthM: vapDepth,
                totalEnergyJ: 0.0,
                materialName: material.Name,
                solverName: Name,
                durationSeconds: sw.Elapsed.TotalSeconds
            );
        }

        private double EstimateCenterFluence(LaserBeam laser, Trajectory trajectory)
        {
            if (trajectory.NPoints < 2) return 0;

            double minX = trajectory.X[0], maxX = trajectory.X[0];
            double minY = trajectory.Y[0], maxY = trajectory.Y[0];
            for (int i = 1; i < trajectory.NPoints; i++)
            {
                if (trajectory.X[i] < minX) minX = trajectory.X[i];
                if (trajectory.X[i] > maxX) maxX = trajectory.X[i];
                if (trajectory.Y[i] < minY) minY = trajectory.Y[i];
                if (trajectory.Y[i] > maxY) maxY = trajectory.Y[i];
            }

            double cx = (maxX + minX) / 2.0;
            double cy = (maxY + minY) / 2.0;

            double totalFluence = 0.0;
            for (int i = 0; i < trajectory.NPoints - 1; i++)
            {
                double dtSeg = trajectory.Time[i + 1] - trajectory.Time[i];
                if (dtSeg <= 0) continue;

                double zDist = trajectory.Z[i];
                double zR = laser.RayleighRange;
                double wZ = laser.SpotRadius * Math.Sqrt(1 + (zDist / zR) * (zDist / zR));

                double dx = cx - trajectory.X[i];
                double dy = cy - trajectory.Y[i];
                double rSq = dx * dx + dy * dy;

                double iPeak = 2 * laser.Power / (Math.PI * wZ * wZ);
                double intensity = iPeak * Math.Exp(-2 * rSq / (wZ * wZ));

                totalFluence += intensity * dtSeg;
            }

            return totalFluence;
        }

        private double[,,] ConvertTo3DArray(double[] array1D)
        {
            int nz = array1D.Length;
            double[,,] result = new double[1, 1, nz];
            for (int i = 0; i < nz; i++)
                result[0, 0, i] = array1D[i];
            return result;
        }
    }
}
