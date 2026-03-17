// ============================================================================
// Coating Domain Entities — HVOF microstructure and laser treatment results.
//
// Entities for the two-stage simulation:
//   Stage 1: CoatingConfig + CoatingGrid (HVOF microstructure)
//   Stage 2: LaserTreatmentResult (post-laser analysis)
//
// Design: Same patterns as existing entities — constructor validation.
// Port of: domain/coating_entities.py
// ============================================================================

using System;

namespace HVOFSim.Domain.Entities
{
    // ── Coating Configuration ────────────────────────────────────────

    /// <summary>
    /// Configuration for generating an HVOF coating microstructure.
    /// All dimensions in SI units (meters).
    /// </summary>
    public sealed class CoatingConfig
    {
        // Coating geometry
        public double CoatingThicknessM { get; }
        public double CoatingWidthM { get; }

        // Porosity parameters
        public double TargetPorosity { get; }
        public double PoreSizeM { get; }

        // Lamellar structure
        public double SplatThicknessM { get; }
        public double BoundaryPoreFactor { get; }

        // Substrate
        public double SubstrateThicknessM { get; }
        public double SubstrateK { get; }
        public double SubstrateRho { get; }
        public double SubstrateCp { get; }
        public double SubstrateTMelt { get; }

        // Hardness (as-sprayed)
        public double HardnessHvBefore { get; }

        public CoatingConfig(
            double coatingThicknessM = 300e-6,
            double coatingWidthM = 1e-3,
            double targetPorosity = 0.03,
            double poreSizeM = 0.5e-6,
            double splatThicknessM = 7.5e-6,
            double boundaryPoreFactor = 3.0,
            double substrateThicknessM = 200e-6,
            double substrateK = 50.0,
            double substrateRho = 7850.0,
            double substrateCp = 500.0,
            double substrateTMelt = 1400.0,
            double hardnessHvBefore = 1000.0)
        {
            CoatingThicknessM = coatingThicknessM;
            CoatingWidthM = coatingWidthM;
            TargetPorosity = targetPorosity;
            PoreSizeM = poreSizeM;
            SplatThicknessM = splatThicknessM;
            BoundaryPoreFactor = boundaryPoreFactor;
            SubstrateThicknessM = substrateThicknessM;
            SubstrateK = substrateK;
            SubstrateRho = substrateRho;
            SubstrateCp = substrateCp;
            SubstrateTMelt = substrateTMelt;
            HardnessHvBefore = hardnessHvBefore;

            Validate();
        }

        private void Validate()
        {
            if (TargetPorosity <= 0.0 || TargetPorosity >= 1.0)
                throw new ArgumentException(
                    $"Target porosity must be in (0, 1), got {TargetPorosity}");
            if (CoatingThicknessM <= 0)
                throw new ArgumentException("Coating thickness must be positive.");
            if (SplatThicknessM <= 0)
                throw new ArgumentException("Splat thickness must be positive.");
        }
    }

    // ── Coating Grid (Stage 1 output) ────────────────────────────────

    /// <summary>
    /// 2D grid representing the HVOF coating microstructure.
    /// Grid: 2D array, shape (ny, nx). 1 = solid, 0 = pore.
    /// KMap: 2D array, same shape. Thermal conductivity at each cell [W/m·K].
    /// </summary>
    public sealed class CoatingGrid
    {
        public int[,] Grid { get; }
        public double[,] KMap { get; }
        public double Dx { get; }
        public double Dy { get; }
        public int CoatingRows { get; }
        public int SubstrateRows { get; }

        public CoatingGrid(
            int[,] grid, double[,] kMap,
            double dx, double dy,
            int coatingRows, int substrateRows)
        {
            Grid = grid;
            KMap = kMap;
            Dx = dx;
            Dy = dy;
            CoatingRows = coatingRows;
            SubstrateRows = substrateRows;
        }

        public int Ny => Grid.GetLength(0);
        public int Nx => Grid.GetLength(1);

        /// <summary>Current porosity of the coating region only.</summary>
        public double Porosity
        {
            get
            {
                int totalCells = 0;
                int poreCells = 0;
                for (int y = 0; y < CoatingRows; y++)
                {
                    for (int x = 0; x < Nx; x++)
                    {
                        totalCells++;
                        if (Grid[y, x] == 0) poreCells++;
                    }
                }
                return totalCells > 0 ? (double)poreCells / totalCells : 0.0;
            }
        }

        /// <summary>Number of pore cells in the coating region.</summary>
        public int PoreCount
        {
            get
            {
                int count = 0;
                for (int y = 0; y < CoatingRows; y++)
                    for (int x = 0; x < Nx; x++)
                        if (Grid[y, x] == 0) count++;
                return count;
            }
        }
    }

    // ── Laser Treatment Result (Stage 2 output) ──────────────────────

    /// <summary>
    /// Complete results from the two-stage HVOF + laser simulation.
    /// Contains all metrics that a real experiment would measure.
    /// </summary>
    public sealed class LaserTreatmentResult
    {
        // Temperature data
        public double[,] TemperatureField { get; }
        public double PeakTemperatureC { get; }

        // Melt analysis
        public bool[,] MeltedMask { get; }
        public double MeltDepthUm { get; }

        // Porosity analysis
        public double PorosityBefore { get; }
        public double PorosityAfter { get; }
        public double PorosityReductionPct { get; }

        // Heat-affected zone
        public double HazDepthUm { get; }

        // Hardness prediction
        public double HardnessHvBefore { get; }
        public double HardnessHvAfter { get; }

        // Safety check
        public bool SubstrateMelted { get; }
        public double MaxSubstrateTempC { get; }

        // Simulation metadata
        public double LaserPowerW { get; }
        public double ScanSpeedMS { get; }
        public double BeamRadiusM { get; }
        public double DurationSeconds { get; }

        public LaserTreatmentResult(
            double[,] temperatureField,
            double peakTemperatureC,
            bool[,] meltedMask,
            double meltDepthUm,
            double porosityBefore,
            double porosityAfter,
            double porosityReductionPct,
            double hazDepthUm,
            double hardnessHvBefore,
            double hardnessHvAfter,
            bool substrateMelted,
            double maxSubstrateTempC,
            double laserPowerW,
            double scanSpeedMS,
            double beamRadiusM,
            double durationSeconds)
        {
            TemperatureField = temperatureField;
            PeakTemperatureC = peakTemperatureC;
            MeltedMask = meltedMask;
            MeltDepthUm = meltDepthUm;
            PorosityBefore = porosityBefore;
            PorosityAfter = porosityAfter;
            PorosityReductionPct = porosityReductionPct;
            HazDepthUm = hazDepthUm;
            HardnessHvBefore = hardnessHvBefore;
            HardnessHvAfter = hardnessHvAfter;
            SubstrateMelted = substrateMelted;
            MaxSubstrateTempC = maxSubstrateTempC;
            LaserPowerW = laserPowerW;
            ScanSpeedMS = scanSpeedMS;
            BeamRadiusM = beamRadiusM;
            DurationSeconds = durationSeconds;
        }
    }

    // ── Coating Solver Output ────────────────────────────────────────

    /// <summary>Output container for the 2D coating solver.</summary>
    public sealed class CoatingSolverOutput
    {
        public double[,] Temperature { get; }
        public bool[,] Melted { get; }
        public double DurationS { get; }

        public CoatingSolverOutput(double[,] temperature, bool[,] melted, double durationS)
        {
            Temperature = temperature;
            Melted = melted;
            DurationS = durationS;
        }
    }

    // ── Post-processing Domain Service ───────────────────────────────

    /// <summary>Post-processing measurements for HVOF laser treatment.</summary>
    public static class CoatingAnalysis
    {
        /// <summary>Calculate porosity before and after laser treatment.</summary>
        public static (double before, double after, double reductionPct) MeasurePorosityChange(
            CoatingGrid grid, bool[,] meltedMask)
        {
            int totalCells = 0, poresBefore = 0, poresSealed = 0;

            for (int y = 0; y < grid.CoatingRows; y++)
                for (int x = 0; x < grid.Nx; x++)
                {
                    totalCells++;
                    bool isPore = grid.Grid[y, x] == 0;
                    if (isPore) poresBefore++;
                    if (isPore && meltedMask[y, x]) poresSealed++;
                }

            double porosityBefore = totalCells > 0 ? (double)poresBefore / totalCells : 0;
            int poresAfter = poresBefore - poresSealed;
            double porosityAfter = totalCells > 0 ? (double)poresAfter / totalCells : 0;
            double reductionPct = porosityBefore > 0
                ? (1.0 - porosityAfter / porosityBefore) * 100.0
                : 0.0;

            return (porosityBefore, porosityAfter, reductionPct);
        }

        /// <summary>Measure maximum melt depth in micrometers.</summary>
        public static double MeasureMeltDepth(bool[,] meltedMask, double dy)
        {
            int ny = meltedMask.GetLength(0);
            int nx = meltedMask.GetLength(1);
            int deepestRow = -1;

            for (int y = 0; y < ny; y++)
            {
                bool anyMelted = false;
                for (int x = 0; x < nx; x++)
                    if (meltedMask[y, x]) { anyMelted = true; break; }
                if (anyMelted) deepestRow = y;
            }

            return deepestRow >= 0 ? deepestRow * dy * 1e6 : 0.0;
        }

        /// <summary>Measure HAZ depth below the melt zone.</summary>
        public static double MeasureHazDepth(
            double[,] temperatureField, bool[,] meltedMask,
            double dy, int coatingRows, double tHazThreshold = 200.0)
        {
            int ny = Math.Min(temperatureField.GetLength(0), coatingRows);
            int nx = temperatureField.GetLength(1);

            int deepestHaz = -1, deepestMelt = -1;

            for (int y = 0; y < ny; y++)
            {
                bool anyHaz = false, anyMelt = false;
                for (int x = 0; x < nx; x++)
                {
                    if (meltedMask[y, x]) anyMelt = true;
                    if (temperatureField[y, x] > tHazThreshold && !meltedMask[y, x])
                        anyHaz = true;
                }
                if (anyHaz) deepestHaz = y;
                if (anyMelt) deepestMelt = y;
            }

            if (deepestHaz < 0) return 0.0;
            int hazDepth = deepestMelt >= 0 ? Math.Max(0, deepestHaz - deepestMelt) : deepestHaz;
            return hazDepth * dy * 1e6;
        }

        /// <summary>
        /// Empirical hardness prediction based on porosity reduction.
        /// HV_after = HV_before × (1 + β · Δϕ/ϕ₀)
        /// </summary>
        public static double PredictHardness(
            double hvBefore, double porosityBefore, double porosityAfter, double beta = 0.3)
        {
            if (porosityBefore <= 0) return hvBefore;
            double deltaPhi = porosityBefore - porosityAfter;
            return hvBefore * (1.0 + beta * deltaPhi / porosityBefore);
        }

        /// <summary>Check whether the substrate exceeded its melting temperature.</summary>
        public static (bool melted, double maxTemp) CheckSubstrateMelting(
            double[,] temperatureField, int coatingRows, double tMeltSubstrate)
        {
            int ny = temperatureField.GetLength(0);
            int nx = temperatureField.GetLength(1);
            double maxTemp = 0;

            for (int y = coatingRows; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    if (temperatureField[y, x] > maxTemp)
                        maxTemp = temperatureField[y, x];

            return (maxTemp > tMeltSubstrate, maxTemp);
        }

        /// <summary>Assemble all post-processing measurements into a result.</summary>
        public static LaserTreatmentResult BuildTreatmentResult(
            CoatingGrid coatingGrid,
            CoatingConfig config,
            CoatingSolverOutput solverOutput,
            double tMelt,
            double laserPower,
            double scanSpeed,
            double beamRadius)
        {
            var (porBefore, porAfter, porReduction) =
                MeasurePorosityChange(coatingGrid, solverOutput.Melted);

            double meltDepth = MeasureMeltDepth(solverOutput.Melted, coatingGrid.Dy);

            double hazDepth = MeasureHazDepth(
                solverOutput.Temperature, solverOutput.Melted,
                coatingGrid.Dy, coatingGrid.CoatingRows);

            double hvAfter = PredictHardness(config.HardnessHvBefore, porBefore, porAfter);

            var (subMelted, maxSubTemp) = CheckSubstrateMelting(
                solverOutput.Temperature, coatingGrid.CoatingRows, config.SubstrateTMelt);

            // Find peak temperature
            int ny = solverOutput.Temperature.GetLength(0);
            int nx = solverOutput.Temperature.GetLength(1);
            double peakTemp = double.MinValue;
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    if (solverOutput.Temperature[y, x] > peakTemp)
                        peakTemp = solverOutput.Temperature[y, x];

            return new LaserTreatmentResult(
                temperatureField: solverOutput.Temperature,
                peakTemperatureC: peakTemp,
                meltedMask: solverOutput.Melted,
                meltDepthUm: meltDepth,
                porosityBefore: porBefore,
                porosityAfter: porAfter,
                porosityReductionPct: porReduction,
                hazDepthUm: hazDepth,
                hardnessHvBefore: config.HardnessHvBefore,
                hardnessHvAfter: hvAfter,
                substrateMelted: subMelted,
                maxSubstrateTempC: maxSubTemp,
                laserPowerW: laserPower,
                scanSpeedMS: scanSpeed,
                beamRadiusM: beamRadius,
                durationSeconds: solverOutput.DurationS);
        }
    }
}
