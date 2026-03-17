// ============================================================================
// Domain Entities — Core business objects of the simulation.
//
// Entities encapsulate validation and core domain logic.
// They depend ONLY on ValueObjects and Exceptions from this layer.
//
// Design Patterns:
//   - SRP: Each entity validates and manages only its own state.
//   - OCP: Computed properties can be extended via inheritance.
//
// Port of: domain/entities.py
// ============================================================================

using System;
using System.Collections.Generic;
using HVOFSim.Domain.Exceptions;

namespace HVOFSim.Domain.Entities
{
    // ── Material Entity ──────────────────────────────────────────────

    /// <summary>
    /// Thermal and optical properties of a target material.
    /// Validates all physical constraints on construction (Integrity — CIA Triad).
    /// </summary>
    public sealed class Material
    {
        public string Name { get; }

        // Optical
        public double Absorption { get; }

        // Thermal
        public double ThermalConductivity { get; }
        public double Density { get; }
        public double SpecificHeat { get; }

        // Phase change temperatures [°C]
        public double TAmbient { get; }
        public double TMelt { get; }
        public double TVaporization { get; }

        public Material(
            string name,
            double absorption,
            double thermalConductivity,
            double density,
            double specificHeat,
            double tAmbient = 20.0,
            double tMelt = 1400.0,
            double tVaporization = 2800.0)
        {
            Name = name;
            Absorption = absorption;
            ThermalConductivity = thermalConductivity;
            Density = density;
            SpecificHeat = specificHeat;
            TAmbient = tAmbient;
            TMelt = tMelt;
            TVaporization = tVaporization;

            Validate();
        }

        /// <summary>κ = k / (ρ · c_p) [m²/s].</summary>
        public double ThermalDiffusivity =>
            ThermalConductivity / (Density * SpecificHeat);

        private void Validate()
        {
            if (Absorption <= 0.0 || Absorption > 1.0)
                throw new InvalidMaterialException(
                    $"Absorption must be in (0, 1], got {Absorption}");
            if (ThermalConductivity <= 0)
                throw new InvalidMaterialException("Thermal conductivity must be positive.");
            if (Density <= 0)
                throw new InvalidMaterialException("Density must be positive.");
            if (SpecificHeat <= 0)
                throw new InvalidMaterialException("Specific heat must be positive.");
            if (TMelt >= TVaporization)
                throw new InvalidMaterialException(
                    $"Melt temp ({TMelt}°C) must be below vaporization temp ({TVaporization}°C).");
        }
    }

    // ── Laser Beam Entity ────────────────────────────────────────────

    /// <summary>
    /// Laser beam configuration with physics-derived properties.
    /// Validates parameters on construction.
    /// </summary>
    public sealed class LaserBeam
    {
        public double Power { get; }
        public double Wavelength { get; }
        public double SpotRadius { get; }
        public double FocalLength { get; }

        public LaserBeam(double power, double wavelength, double spotRadius, double focalLength)
        {
            Power = power;
            Wavelength = wavelength;
            SpotRadius = spotRadius;
            FocalLength = focalLength;

            Validate();
        }

        /// <summary>z_R = π · w0² / λ [m].</summary>
        public double RayleighRange =>
            Math.PI * SpotRadius * SpotRadius / Wavelength;

        /// <summary>I_0 = 2P / (π · w0²) [W/m²].</summary>
        public double PeakIntensity =>
            2.0 * Power / (Math.PI * SpotRadius * SpotRadius);

        /// <summary>θ = λ / (π · w0) [rad].</summary>
        public double DivergenceAngle =>
            Wavelength / (Math.PI * SpotRadius);

        private void Validate()
        {
            if (Power <= 0)
                throw new InvalidLaserConfigException("Power must be positive.");
            if (Wavelength <= 0)
                throw new InvalidLaserConfigException("Wavelength must be positive.");
            if (SpotRadius <= 0)
                throw new InvalidLaserConfigException("Spot radius must be positive.");
            if (FocalLength <= 0)
                throw new InvalidLaserConfigException("Focal length must be positive.");
        }
    }

    // ── Trajectory Entity ────────────────────────────────────────────

    /// <summary>
    /// Motion path data for laser scanning.
    /// Stores arrays of time, x, y, z coordinates.
    /// Validates consistency on construction.
    /// </summary>
    public sealed class Trajectory
    {
        public double[] Time { get; }
        public double[] X { get; }
        public double[] Y { get; }
        public double[] Z { get; }

        public Trajectory(double[] time, double[] x, double[] y, double[] z)
        {
            Time = time ?? throw new ArgumentNullException(nameof(time));
            X = x ?? throw new ArgumentNullException(nameof(x));
            Y = y ?? throw new ArgumentNullException(nameof(y));
            Z = z ?? throw new ArgumentNullException(nameof(z));

            Validate();
        }

        public int NPoints => Time.Length;

        public double Duration => Time[Time.Length - 1] - Time[0];

        private void Validate()
        {
            if (Time.Length != X.Length || Time.Length != Y.Length || Time.Length != Z.Length)
                throw new InvalidTrajectoryException(
                    "All trajectory arrays must have the same length.");
            if (Time.Length < 2)
                throw new InvalidTrajectoryException(
                    "Trajectory must have at least 2 points.");
        }
    }

    // ── Simulation Result Entity ─────────────────────────────────────

    /// <summary>
    /// Immutable container for simulation output.
    /// Stores the 3D temperature field, fluence map, and derived metrics.
    /// </summary>
    public sealed class SimulationResult
    {
        // Raw data
        public double[,,] TemperatureField { get; }
        public double[,] FluenceMap { get; }
        public double[] XCoords { get; }
        public double[] YCoords { get; }
        public double[] ZCoords { get; }
        public double[] TimeCoords { get; }

        // Derived metrics
        public double PeakTemperatureCelsius { get; }
        public double PeakFluenceJPerM2 { get; }
        public double? MeltDepthM { get; }
        public double? VaporizationDepthM { get; }
        public double TotalEnergyJ { get; }

        // Metadata
        public string MaterialName { get; }
        public string SolverName { get; }
        public double DurationSeconds { get; }

        public SimulationResult(
            double[,,] temperatureField,
            double[,] fluenceMap,
            double[] xCoords,
            double[] yCoords,
            double[] zCoords,
            double[] timeCoords,
            double peakTemperatureCelsius,
            double peakFluenceJPerM2,
            double? meltDepthM,
            double? vaporizationDepthM,
            double totalEnergyJ,
            string materialName,
            string solverName,
            double durationSeconds)
        {
            TemperatureField = temperatureField;
            FluenceMap = fluenceMap;
            XCoords = xCoords;
            YCoords = yCoords;
            ZCoords = zCoords;
            TimeCoords = timeCoords;
            PeakTemperatureCelsius = peakTemperatureCelsius;
            PeakFluenceJPerM2 = peakFluenceJPerM2;
            MeltDepthM = meltDepthM;
            VaporizationDepthM = vaporizationDepthM;
            TotalEnergyJ = totalEnergyJ;
            MaterialName = materialName;
            SolverName = solverName;
            DurationSeconds = durationSeconds;
        }
    }
}
