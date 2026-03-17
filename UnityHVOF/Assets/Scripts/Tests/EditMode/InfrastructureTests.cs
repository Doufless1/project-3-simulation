// ============================================================================
// EditMode Tests — Infrastructure Layer
//
// Tests concrete implementations: laser sources, motion generators,
// coating generator/analysis, virtual hardware controllers, audit logger.
//
// Mirrors: tests/test_infrastructure.py
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Exceptions;
using HVOFSim.Domain.ValueObjects;
using HVOFSim.Infrastructure.Laser;
using HVOFSim.Infrastructure.Motion;
using HVOFSim.Infrastructure.Solvers;
using HVOFSim.Infrastructure.Coating;
using HVOFSim.Infrastructure.VirtualHardware;
using HVOFSim.Infrastructure.Logging;
using HVOFSim.Infrastructure.Persistence;

namespace HVOFSim.Tests.EditMode
{
    // ══════════════════════════════════════════════════════════════════
    // LASER SOURCE TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class GaussianSourceTests
    {
        [Test]
        public void PeakIntensityAtCenter_MatchesTheory()
        {
            var source = new GaussianSource();
            double w = 0.001;
            double P = 500;
            var xGrid = new double[1, 1] { { 0.0 } };
            var yGrid = new double[1, 1] { { 0.0 } };

            var intensity = source.ComputeIntensity(xGrid, yGrid, 0, 0, w, P);

            double expectedPeak = 2 * P / (Math.PI * w * w);
            Assert.AreEqual(expectedPeak, intensity[0, 0], expectedPeak * 1e-10);
        }

        [Test]
        public void IntensityDecays_WithDistance()
        {
            var source = new GaussianSource();
            double w = 0.001;
            var xGrid = new double[1, 2] { { 0.0, 0.002 } };
            var yGrid = new double[1, 2] { { 0.0, 0.0 } };

            var intensity = source.ComputeIntensity(xGrid, yGrid, 0, 0, w, 500);

            Assert.Greater(intensity[0, 0], intensity[0, 1],
                "Intensity should decay away from center");
        }
    }

    [TestFixture]
    public class TopHatSourceTests
    {
        [Test]
        public void UniformInsideSpot_ZeroOutside()
        {
            var source = new TopHatSource();
            double w = 0.001;
            var xGrid = new double[1, 2] { { 0.0, 0.005 } };
            var yGrid = new double[1, 2] { { 0.0, 0.0 } };

            var intensity = source.ComputeIntensity(xGrid, yGrid, 0, 0, w, 500);

            Assert.Greater(intensity[0, 0], 0, "Inside spot should have intensity");
            Assert.AreEqual(0.0, intensity[0, 1], "Outside spot should be zero");
        }
    }

    [TestFixture]
    public class LaserFactoryTests
    {
        [Test]
        public void CreateGaussian_ReturnsGaussianSource()
        {
            var source = LaserFactory.Create("gaussian");
            Assert.IsInstanceOf<GaussianSource>(source);
        }

        [Test]
        public void CreateTopHat_ReturnsTopHatSource()
        {
            var source = LaserFactory.Create("tophat");
            Assert.IsInstanceOf<TopHatSource>(source);
        }

        [Test]
        public void UnknownProfile_Throws()
        {
            Assert.Throws<ArgumentException>(() => LaserFactory.Create("unknown"));
        }

        [Test]
        public void CaseInsensitive_Works()
        {
            var source = LaserFactory.Create("GAUSSIAN");
            Assert.IsInstanceOf<GaussianSource>(source);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // MOTION GENERATOR TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class RasterGeneratorTests
    {
        [Test]
        public void Generate_ProducesValidTrajectory()
        {
            var gen = new RasterGenerator();
            var traj = gen.Generate(new Dictionary<string, object>
            {
                ["width_m"] = 0.01,
                ["height_m"] = 0.01,
                ["speed_m_s"] = 0.005,
                ["line_spacing_m"] = 0.001
            });

            Assert.GreaterOrEqual(traj.NPoints, 2);
            Assert.Greater(traj.Duration, 0);
        }

        [Test]
        public void RasterCoversFullWidth()
        {
            var gen = new RasterGenerator();
            var traj = gen.Generate(new Dictionary<string, object>
            {
                ["width_m"] = 0.01,
                ["height_m"] = 0.005,
                ["speed_m_s"] = 0.005,
                ["line_spacing_m"] = 0.001
            });

            double maxX = 0;
            foreach (double x in traj.X)
                if (x > maxX) maxX = x;

            Assert.AreEqual(0.01, maxX, 1e-10, "Raster should cover full width");
        }
    }

    [TestFixture]
    public class SpiralGeneratorTests
    {
        [Test]
        public void Generate_ProducesValidTrajectory()
        {
            var gen = new SpiralGenerator();
            var traj = gen.Generate(new Dictionary<string, object>
            {
                ["radius_m"] = 0.005,
                ["spacing_m"] = 0.001,
                ["speed_m_s"] = 0.005
            });

            Assert.GreaterOrEqual(traj.NPoints, 2);
            Assert.Greater(traj.Duration, 0);
        }
    }

    [TestFixture]
    public class LinearGeneratorTests
    {
        [Test]
        public void Generate_StartsAtOrigin()
        {
            var gen = new LinearGenerator();
            var traj = gen.Generate(new Dictionary<string, object>
            {
                ["length_m"] = 0.01,
                ["speed_m_s"] = 0.005,
                ["n_points"] = 50
            });

            Assert.AreEqual(0.0, traj.X[0], 1e-10);
            Assert.AreEqual(50, traj.NPoints);
        }

        [Test]
        public void Generate_EndsAtLength()
        {
            var gen = new LinearGenerator();
            var traj = gen.Generate(new Dictionary<string, object>
            {
                ["length_m"] = 0.01,
                ["speed_m_s"] = 0.005,
                ["n_points"] = 50
            });

            Assert.AreEqual(0.01, traj.X[traj.NPoints - 1], 1e-10);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // SOLVER TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class FDMHeatSolver3DTests
    {
        [Test]
        public void GridTooLarge_ThrowsDoSProtection()
        {
            var solver = new FDMHeatSolver3D(new GaussianSource());
            var mat = new Material("Test", 0.5, 45, 14800, 300);
            var laser = new LaserBeam(500, 1.07e-6, 0.5e-3, 0.15);
            var traj = new Trajectory(
                new[] { 0.0, 0.01 }, new[] { 0.0, 0.01 },
                new[] { 0.0, 0.0 }, new[] { 0.0, 0.0 });

            // Very small resolution → too many cells
            Assert.Throws<GridTooLargeException>(() =>
                solver.Solve(mat, laser, traj, new[] { 1.0, 1.0, 1.0 }, 1e-6));
        }

        [Test]
        public void SmallGrid_ProducesValidResult()
        {
            var solver = new FDMHeatSolver3D(new GaussianSource());
            var mat = new Material("WC-NiCr", 0.75, 45, 14800, 300);
            var laser = new LaserBeam(500, 1.07e-6, 0.5e-3, 0.15);
            var traj = new Trajectory(
                new[] { 0.0, 0.001 },
                new[] { 0.005, 0.005 },
                new[] { 0.005, 0.005 },
                new[] { 0.0, 0.0 });

            var result = solver.Solve(mat, laser, traj,
                new[] { 0.003, 0.003, 0.001 }, 0.001);

            Assert.Greater(result.PeakTemperatureCelsius, mat.TAmbient,
                "Peak temperature should exceed ambient");
            Assert.AreEqual("FDM-3D-FTCS", result.SolverName);
            Assert.Greater(result.DurationSeconds, 0);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // COATING TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class CoatingGeneratorTests
    {
        [Test]
        public void Generate_ProducesGrid_WithCorrectDimensions()
        {
            var gen = new HVOFCoatingGenerator();
            var config = new CoatingConfig(
                coatingThicknessM: 100e-6,
                coatingWidthM: 0.5e-3,
                substrateThicknessM: 50e-6);

            var grid = gen.Generate(config, 45.0, 10e-6, seed: 42);

            Assert.Greater(grid.Ny, 0);
            Assert.Greater(grid.Nx, 0);
            Assert.AreEqual(grid.CoatingRows + grid.SubstrateRows, grid.Ny);
        }

        [Test]
        public void Generate_PorosityNearTarget()
        {
            var gen = new HVOFCoatingGenerator();
            var config = new CoatingConfig(targetPorosity: 0.05);
            var grid = gen.Generate(config, 45.0, 2e-6, seed: 42);

            Assert.AreEqual(0.05, grid.Porosity, 0.02,
                "Porosity should be near target (±2%)");
        }

        [Test]
        public void Generate_DeterministicWithSeed()
        {
            var gen = new HVOFCoatingGenerator();
            var config = new CoatingConfig();

            var grid1 = gen.Generate(config, 45.0, 5e-6, seed: 42);
            var grid2 = gen.Generate(config, 45.0, 5e-6, seed: 42);

            Assert.AreEqual(grid1.Porosity, grid2.Porosity, 1e-10,
                "Same seed should produce identical results");
        }
    }

    [TestFixture]
    public class CoatingAnalysisTests
    {
        [Test]
        public void MeasurePorosityChange_NoMelting_NoChange()
        {
            var grid = new CoatingGrid(
                new int[,] { { 1, 0, 1 }, { 1, 1, 0 } },
                new double[,] { { 45, 0.025, 45 }, { 45, 45, 0.025 } },
                1e-6, 1e-6, 2, 0);

            var melted = new bool[2, 3]; // Nothing melted

            var (before, after, reduction) = CoatingAnalysis.MeasurePorosityChange(grid, melted);
            Assert.AreEqual(before, after, 1e-10, "No melting → no porosity change");
            Assert.AreEqual(0.0, reduction, 1e-10);
        }

        [Test]
        public void MeasurePorosityChange_FullMelt_SealsPores()
        {
            var grid = new CoatingGrid(
                new int[,] { { 1, 0, 1 }, { 1, 1, 0 } },
                new double[,] { { 45, 0.025, 45 }, { 45, 45, 0.025 } },
                1e-6, 1e-6, 2, 0);

            // Everything melted
            var melted = new bool[,] { { true, true, true }, { true, true, true } };

            var (before, after, reduction) = CoatingAnalysis.MeasurePorosityChange(grid, melted);
            Assert.AreEqual(0.0, after, 1e-10, "Full melt should seal all pores");
            Assert.AreEqual(100.0, reduction, 1e-10);
        }

        [Test]
        public void PredictHardness_WithReduction_Increases()
        {
            double hv = CoatingAnalysis.PredictHardness(1000, 0.05, 0.01);
            Assert.Greater(hv, 1000, "Porosity reduction should increase hardness");
        }

        [Test]
        public void PredictHardness_NoPorosity_ReturnsOriginal()
        {
            double hv = CoatingAnalysis.PredictHardness(1000, 0.0, 0.0);
            Assert.AreEqual(1000, hv);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // VIRTUAL HARDWARE TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class VirtualTableControllerTests
    {
        [Test]
        public void Home_SetsTableHomed()
        {
            var ctrl = new VirtualTableController();
            var table = new XYTable();
            ctrl.Home(table);
            Assert.IsTrue(table.IsHomed);
        }

        [Test]
        public void ExecuteScan_ProducesPath()
        {
            var ctrl = new VirtualTableController();
            var table = new XYTable();
            ctrl.Home(table);
            var recipe = new ScanRecipe("raster", 10, 500, 1, 30, 50, 50);
            var path = ctrl.ExecuteScan(table, recipe);
            Assert.Greater(path.Count, 0, "Scan should produce waypoints");
        }

        [Test]
        public void GenerateGCode_IncludesHomeAndMove()
        {
            var ctrl = new VirtualTableController();
            var recipe = new ScanRecipe("raster", 10, 500, 1, 30, 50, 50);
            var gcode = ctrl.GenerateGCode(recipe);
            Assert.Greater(gcode.Count, 2, "G-code should include home + moves");
            Assert.IsTrue(gcode[0].Code.StartsWith("G28"), "First line should be home");
        }
    }

    [TestFixture]
    public class VirtualGasControllerTests
    {
        [Test]
        public void ReadO2_DecaysDuringPurge()
        {
            var ctrl = new VirtualGasController();
            var gas = new GasSystem();
            gas.StartPurge();

            double initialO2 = gas.ChamberO2Ppm;
            ctrl.ReadO2(gas);

            Assert.Less(gas.ChamberO2Ppm, initialO2, "O2 should decrease during purge");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // AUDIT LOGGER TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class AuditLoggerTests
    {
        [Test]
        public void LogAction_ReturnsUniqueId()
        {
            var logger = new AuditLogger();
            string id1 = logger.LogAction("TABLE", "HOME", null);
            string id2 = logger.LogAction("LASER", "FIRE", null);
            Assert.AreNotEqual(id1, id2);
        }

        [Test]
        public void GetRecentLogs_ReturnsCorrectCount()
        {
            var logger = new AuditLogger();
            logger.LogAction("TABLE", "HOME", null);
            logger.LogAction("LASER", "FIRE", null);
            logger.LogAction("GAS", "PURGE", null);

            var logs = logger.GetRecentLogs(2);
            Assert.AreEqual(2, logs.Count);
        }

        [Test]
        public void LogSimulationStart_CreatesEntry()
        {
            var logger = new AuditLogger();
            string runId = logger.LogSimulationStart("WC-NiCr", "FDM-3D", null);
            Assert.IsNotNull(runId);
            Assert.IsNotEmpty(runId);

            var logs = logger.GetRecentLogs(1);
            Assert.AreEqual("SIM_START", logs[0]["type"]);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // EQUIPMENT CATALOG TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class EquipmentCatalogTests
    {
        [Test]
        public void GetCatalog_ReturnsItems()
        {
            var catalog = new EquipmentCatalog();
            var items = catalog.GetCatalog();
            Assert.Greater(items.Count, 0);
        }

        [Test]
        public void GetByCategory_FiltersCorrectly()
        {
            var catalog = new EquipmentCatalog();
            var lasers = catalog.GetByCategory("laser");
            Assert.Greater(lasers.Count, 0);
            foreach (var item in lasers)
                Assert.AreEqual("laser", item.Category);
        }

        [Test]
        public void CalculateTotal_SumsCorrectly()
        {
            var catalog = new EquipmentCatalog();
            var items = catalog.GetCatalog();
            double total = catalog.CalculateTotal(items);
            Assert.Greater(total, 0);
        }
    }
}
