// ============================================================================
// EditMode Tests — Domain Layer
//
// Tests value objects, entities, and exceptions.
// Mirrors: tests/test_domain.py
//
// Coverage targets:
//   - CIA Integrity: All constructor validation paths tested
//   - STRIDE Spoofing/Tampering: State machine transition tests
//   - SRP: Each test class tests exactly one entity
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Exceptions;
using HVOFSim.Domain.ValueObjects;

namespace HVOFSim.Tests.EditMode
{
    // ══════════════════════════════════════════════════════════════════
    // VALUE OBJECTS TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class TemperatureTests
    {
        [Test]
        public void ValidTemperature_CreatesSuccessfully()
        {
            var t = new Temperature(100.0);
            Assert.AreEqual(100.0, t.ValueCelsius, 1e-10);
        }

        [Test]
        public void Temperature_KelvinConversion_IsCorrect()
        {
            var t = new Temperature(0.0);
            Assert.AreEqual(273.15, t.Kelvin, 1e-10);
        }

        [Test]
        public void Temperature_BelowAbsoluteZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => new Temperature(-274.0));
        }

        [Test]
        public void Temperature_Exceeds_ReturnsTrueAboveThreshold()
        {
            var t = new Temperature(1500.0);
            Assert.IsTrue(t.Exceeds(1400.0));
            Assert.IsFalse(t.Exceeds(1600.0));
        }
    }

    [TestFixture]
    public class Position2DTests
    {
        [Test]
        public void ValidPosition_CreatesSuccessfully()
        {
            var p = new Position2D(10.0, 20.0);
            Assert.AreEqual(10.0, p.XMm, 1e-10);
            Assert.AreEqual(20.0, p.YMm, 1e-10);
        }

        [Test]
        public void Position2D_NaN_Throws()
        {
            Assert.Throws<ArgumentException>(() => new Position2D(double.NaN, 0));
        }

        [Test]
        public void Position2D_Infinity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new Position2D(0, double.PositiveInfinity));
        }

        [Test]
        public void DistanceTo_KnownValues_IsCorrect()
        {
            var a = new Position2D(0, 0);
            var b = new Position2D(3, 4);
            Assert.AreEqual(5.0, a.DistanceTo(b), 1e-10);
        }
    }

    [TestFixture]
    public class SpeedTests
    {
        [Test]
        public void NegativeSpeed_Throws()
        {
            Assert.Throws<ArgumentException>(() => new Speed(-1.0));
        }

        [Test]
        public void ZeroSpeed_IsZero()
        {
            var s = new Speed(0.0);
            Assert.IsTrue(s.IsZero);
        }
    }

    [TestFixture]
    public class CompositionTests
    {
        [Test]
        public void ValidComposition_Normalizes()
        {
            var comp = new Composition(new Dictionary<string, double>
            {
                { "WC", 0.88 },
                { "Ni", 0.07 },
                { "Cr", 0.05 }
            });
            // Sum should be ~1.0 after normalization
            double sum = 0;
            foreach (var kvp in comp.Elements) sum += kvp.Value;
            Assert.AreEqual(1.0, sum, 0.01);
        }

        [Test]
        public void NegativeFraction_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new Composition(new Dictionary<string, double> { { "Fe", -0.1 } }));
        }

        [Test]
        public void DominantElements_FiltersBelowThreshold()
        {
            var comp = new Composition(new Dictionary<string, double>
            {
                { "WC", 0.88 },
                { "Ni", 0.07 },
                { "Cr", 0.05 }
            });
            var dominant = comp.DominantElements;
            Assert.AreEqual(1, dominant.Count); // Only WC > 10%
        }
    }

    [TestFixture]
    public class ScanRecipeTests
    {
        [Test]
        public void ValidRecipe_CreatesSuccessfully()
        {
            var recipe = new ScanRecipe("raster", 10, 500, 1, 30, 50, 50);
            Assert.AreEqual("raster", recipe.Pattern);
            Assert.AreEqual(0.7, recipe.LineSpacingMm, 1e-10);
        }

        [Test]
        public void InvalidPattern_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScanRecipe("zigzag", 10, 500, 1, 30, 50, 50));
        }

        [Test]
        public void NegativeSpeed_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScanRecipe("raster", -10, 500, 1, 30, 50, 50));
        }

        [Test]
        public void OverlapOutOfRange_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScanRecipe("raster", 10, 500, 1, 100, 50, 50));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // ENTITY TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class MaterialTests
    {
        [Test]
        public void ValidMaterial_CreatesSuccessfully()
        {
            var mat = new Material("WC-NiCr", 0.75, 45.0, 14800, 300, 20, 1400, 2800);
            Assert.AreEqual("WC-NiCr", mat.Name);
            Assert.AreEqual(0.75, mat.Absorption);
        }

        [Test]
        public void Material_ThermalDiffusivity_MatchesPython()
        {
            var mat = new Material("WC-NiCr", 0.75, 45.0, 14800, 300);
            double expected = 45.0 / (14800 * 300);
            Assert.AreEqual(expected, mat.ThermalDiffusivity, 1e-12);
        }

        [Test]
        public void InvalidAbsorption_ZeroOrBelow_Throws()
        {
            Assert.Throws<InvalidMaterialException>(() =>
                new Material("Bad", 0.0, 45, 14800, 300));
        }

        [Test]
        public void InvalidAbsorption_AboveOne_Throws()
        {
            Assert.Throws<InvalidMaterialException>(() =>
                new Material("Bad", 1.5, 45, 14800, 300));
        }

        [Test]
        public void NegativeConductivity_Throws()
        {
            Assert.Throws<InvalidMaterialException>(() =>
                new Material("Bad", 0.5, -1, 14800, 300));
        }

        [Test]
        public void MeltAboveVaporization_Throws()
        {
            Assert.Throws<InvalidMaterialException>(() =>
                new Material("Bad", 0.5, 45, 14800, 300, 20, 3000, 2800));
        }
    }

    [TestFixture]
    public class LaserBeamTests
    {
        [Test]
        public void ValidLaser_CreatesSuccessfully()
        {
            var laser = new LaserBeam(500, 1.07e-6, 0.5e-3, 0.15);
            Assert.AreEqual(500, laser.Power);
        }

        [Test]
        public void PeakIntensity_MatchesPython()
        {
            var laser = new LaserBeam(500, 1.07e-6, 0.5e-3, 0.15);
            double expected = 2 * 500 / (Math.PI * 0.5e-3 * 0.5e-3);
            Assert.AreEqual(expected, laser.PeakIntensity, expected * 1e-10);
        }

        [Test]
        public void RayleighRange_MatchesPython()
        {
            var laser = new LaserBeam(500, 1.07e-6, 0.5e-3, 0.15);
            double expected = Math.PI * 0.5e-3 * 0.5e-3 / 1.07e-6;
            Assert.AreEqual(expected, laser.RayleighRange, expected * 1e-10);
        }

        [Test]
        public void ZeroPower_Throws()
        {
            Assert.Throws<InvalidLaserConfigException>(() =>
                new LaserBeam(0, 1.07e-6, 0.5e-3, 0.15));
        }
    }

    [TestFixture]
    public class TrajectoryTests
    {
        [Test]
        public void ValidTrajectory_CreatesSuccessfully()
        {
            var traj = new Trajectory(
                new[] { 0.0, 1.0 },
                new[] { 0.0, 0.01 },
                new[] { 0.0, 0.0 },
                new[] { 0.0, 0.0 });
            Assert.AreEqual(2, traj.NPoints);
            Assert.AreEqual(1.0, traj.Duration);
        }

        [Test]
        public void MismatchedArrayLengths_Throws()
        {
            Assert.Throws<InvalidTrajectoryException>(() =>
                new Trajectory(
                    new[] { 0.0, 1.0 },
                    new[] { 0.0 },
                    new[] { 0.0, 0.0 },
                    new[] { 0.0, 0.0 }));
        }

        [Test]
        public void SinglePoint_Throws()
        {
            Assert.Throws<InvalidTrajectoryException>(() =>
                new Trajectory(
                    new[] { 0.0 },
                    new[] { 0.0 },
                    new[] { 0.0 },
                    new[] { 0.0 }));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // LAB ENTITY TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class XYTableTests
    {
        [Test]
        public void NewTable_IsIdleAndNotHomed()
        {
            var table = new XYTable();
            Assert.AreEqual(TableState.Idle, table.State);
            Assert.IsFalse(table.IsHomed);
        }

        [Test]
        public void Home_SetsHomedAndIdle()
        {
            var table = new XYTable();
            table.Home();
            Assert.IsTrue(table.IsHomed);
            Assert.AreEqual(TableState.Idle, table.State);
            Assert.AreEqual(0.0, table.Position.XMm);
        }

        [Test]
        public void MoveNotHomed_Throws()
        {
            var table = new XYTable();
            Assert.Throws<TableNotHomedException>(() =>
                table.MoveTo(new Position2D(10, 10)));
        }

        [Test]
        public void MoveOutsideLimits_Throws()
        {
            var table = new XYTable(travelXMm: 300, travelYMm: 300);
            table.Home();
            Assert.Throws<TableLimitException>(() =>
                table.MoveTo(new Position2D(350, 10)));
        }

        [Test]
        public void ValidMove_UpdatesPosition()
        {
            var table = new XYTable();
            table.Home();
            table.MoveTo(new Position2D(100, 150));
            Assert.AreEqual(100.0, table.Position.XMm);
            Assert.AreEqual(150.0, table.Position.YMm);
        }
    }

    [TestFixture]
    public class SafetySystemTests
    {
        [Test]
        public void NewSafety_InterlocksUnlocked()
        {
            var safety = new SafetySystem();
            Assert.IsFalse(safety.AllInterlocksLocked);
            Assert.AreEqual(InterlockStatus.Unlocked, safety.DoorInterlock);
            Assert.AreEqual(InterlockStatus.Unlocked, safety.ChamberInterlock);
        }

        [Test]
        public void AllLocked_ReturnsTrue()
        {
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            Assert.IsTrue(safety.AllInterlocksLocked);
        }

        [Test]
        public void EStop_PreventsAllClear()
        {
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            safety.PressEStop();
            Assert.IsFalse(safety.AllInterlocksLocked);
        }

        [Test]
        public void EStopReset_RestoresAllClear()
        {
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            safety.PressEStop();
            safety.ResetEStop();
            Assert.IsTrue(safety.AllInterlocksLocked);
        }

        [Test]
        public void StatusSummary_ReflectsState()
        {
            var safety = new SafetySystem();
            safety.LockDoor();
            var summary = safety.GetStatusSummary();
            Assert.AreEqual("Locked", summary.Door);
            Assert.AreEqual("Unlocked", summary.Chamber);
            Assert.IsFalse(summary.AllClear);
        }
    }

    [TestFixture]
    public class LaserUnitTests
    {
        [Test]
        public void NewLaser_IsOff()
        {
            var laser = new LaserUnit();
            Assert.AreEqual(LaserState.Off, laser.State);
            Assert.AreEqual(0.0, laser.CurrentPowerW);
        }

        [Test]
        public void SetPower_ValidRange_Succeeds()
        {
            var laser = new LaserUnit(maxPowerW: 1000);
            laser.SetPower(500);
            Assert.AreEqual(500, laser.CurrentPowerW);
            Assert.AreEqual(50.0, laser.PowerPct, 0.01);
        }

        [Test]
        public void SetPower_ExceedsMax_Throws()
        {
            var laser = new LaserUnit(maxPowerW: 1000);
            Assert.Throws<LaserPowerException>(() => laser.SetPower(1500));
        }

        [Test]
        public void ArmWithoutInterlocks_Throws()
        {
            var laser = new LaserUnit();
            var safety = new SafetySystem(); // Interlocks unlocked
            Assert.Throws<LaserInterlockException>(() => laser.Arm(safety));
        }

        [Test]
        public void ArmWithInterlocks_Succeeds()
        {
            var laser = new LaserUnit();
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            laser.Arm(safety);
            Assert.AreEqual(LaserState.Armed, laser.State);
        }

        [Test]
        public void FireWithoutArming_Throws()
        {
            var laser = new LaserUnit();
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            Assert.Throws<LaserInterlockException>(() => laser.Fire(safety));
        }

        [Test]
        public void FireBelowMinPower_Throws()
        {
            var laser = new LaserUnit(maxPowerW: 1000, minPowerW: 50);
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            laser.SetPower(10);
            laser.Arm(safety);
            Assert.Throws<LaserPowerException>(() => laser.Fire(safety));
        }

        [Test]
        public void FullFireCycle_Succeeds()
        {
            var laser = new LaserUnit(maxPowerW: 1000, minPowerW: 50);
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            laser.SetPower(500);
            laser.Arm(safety);
            laser.Fire(safety);
            Assert.AreEqual(LaserState.Firing, laser.State);
        }

        [Test]
        public void Stop_ResetsState()
        {
            var laser = new LaserUnit();
            var safety = new SafetySystem();
            safety.LockDoor();
            safety.LockChamber();
            laser.SetPower(500);
            laser.Arm(safety);
            laser.Fire(safety);
            laser.Stop();
            Assert.AreEqual(LaserState.Standby, laser.State);
            Assert.AreEqual(0.0, laser.CurrentPowerW);
        }
    }

    [TestFixture]
    public class GasSystemTests
    {
        [Test]
        public void NewGas_IsClosed()
        {
            var gas = new GasSystem();
            Assert.AreEqual(GasState.Closed, gas.State);
            Assert.AreEqual(0.0, gas.CurrentFlow.ValueLPerMin);
        }

        [Test]
        public void SetFlow_ValidRange_Succeeds()
        {
            var gas = new GasSystem(maxFlowLMin: 20);
            gas.SetFlow(10);
            Assert.AreEqual(10.0, gas.CurrentFlow.ValueLPerMin);
            Assert.AreEqual(GasState.Flowing, gas.State);
        }

        [Test]
        public void SetFlow_ExceedsMax_Throws()
        {
            var gas = new GasSystem(maxFlowLMin: 20);
            Assert.Throws<GasFlowException>(() => gas.SetFlow(25));
        }

        [Test]
        public void StartPurge_SetsState()
        {
            var gas = new GasSystem();
            gas.StartPurge(15);
            Assert.AreEqual(GasState.Purging, gas.State);
            Assert.AreEqual(15.0, gas.CurrentFlow.ValueLPerMin);
        }

        [Test]
        public void Stop_ResetsFlow()
        {
            var gas = new GasSystem();
            gas.SetFlow(10);
            gas.Stop();
            Assert.AreEqual(GasState.Closed, gas.State);
            Assert.AreEqual(0.0, gas.CurrentFlow.ValueLPerMin);
        }

        [Test]
        public void AtmosphereSafe_BelowThreshold()
        {
            var gas = new GasSystem();
            gas.ChamberO2Ppm = 50;
            Assert.IsTrue(gas.IsAtmosphereSafe);
        }

        [Test]
        public void AtmosphereUnsafe_AboveThreshold()
        {
            var gas = new GasSystem();
            Assert.IsFalse(gas.IsAtmosphereSafe); // Default: atmospheric O2
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // COATING ENTITY TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class CoatingConfigTests
    {
        [Test]
        public void DefaultConfig_IsValid()
        {
            var config = new CoatingConfig();
            Assert.AreEqual(300e-6, config.CoatingThicknessM, 1e-12);
            Assert.AreEqual(0.03, config.TargetPorosity, 1e-12);
        }

        [Test]
        public void InvalidPorosity_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new CoatingConfig(targetPorosity: 1.5));
        }

        [Test]
        public void ZeroPorosity_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new CoatingConfig(targetPorosity: 0.0));
        }
    }
}
