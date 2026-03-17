// ============================================================================
// EditMode Tests — Application Layer Use Cases
//
// Tests orchestration logic: SimulationUseCase, HVOFLaserUseCase, LabControlUseCase.
// Uses mock/stub infrastructure to isolate use case logic.
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.ValueObjects;
using HVOFSim.Domain.Ports;
using HVOFSim.Application;

namespace HVOFSim.Tests.EditMode
{
    // ══════════════════════════════════════════════════════════════════
    // MOCKS
    // ══════════════════════════════════════════════════════════════════

    public class MockHeatSolver : IHeatSolver
    {
        public string Name => "MockSolver";
        public bool SolveCalled = false;

        public SimulationResult Solve(Material material, LaserBeam laser, Trajectory trajectory, double[] gridSize, double resolution)
        {
            SolveCalled = true;
            return new SimulationResult(
                temperatureField: new double[1, 1, 1],
                fluenceMap: new double[1, 1],
                xCoords: new double[] { 0 },
                yCoords: new double[] { 0 },
                zCoords: new double[] { 0 },
                timeCoords: new double[] { 0, 1 },
                peakTemperatureCelsius: 1000,
                peakFluenceJPerM2: 5000,
                meltDepthM: 0.001,
                vaporizationDepthM: null,
                totalEnergyJ: 100,
                materialName: material.Name,
                solverName: Name,
                durationSeconds: 0.1
            );
        }
    }

    public class MockMotionGenerator : IMotionGenerator
    {
        public bool GenerateCalled = false;
        public Trajectory Generate(Dictionary<string, object> parameters)
        {
            GenerateCalled = true;
            return new Trajectory(
                new[] { 0.0, 1.0 }, new[] { 0.0, 1.0 }, new[] { 0.0, 0.0 }, new[] { 0.0, 1.0 }
            );
        }
    }

    public class MockAuditLogger : ISimulationAuditLogger, ILabAuditLogger
    {
        public List<string> LoggedActions = new List<string>();

        public string LogSimulationStart(string materialName, string solverName, Dictionary<string, object> parameters)
        {
            LoggedActions.Add("SIM_START");
            return "RUN123";
        }

        public void LogSimulationEnd(string runId, Dictionary<string, object> resultSummary)
        {
            LoggedActions.Add("SIM_END");
        }

        public void LogError(string runId, string error)
        {
            LoggedActions.Add("SIM_ERROR");
        }

        public string LogAction(string component, string action, Dictionary<string, object> details)
        {
            LoggedActions.Add($"{component}_{action}");
            return "LOG123";
        }

        public string LogSafetyEvent(string eventType, Dictionary<string, object> details)
        {
            LoggedActions.Add($"SAFETY_{eventType}");
            return "LOG124";
        }

        public List<Dictionary<string, object>> GetRecentLogs(int count = 50)
        {
            return new List<Dictionary<string, object>>();
        }
    }

    public class MockTableController : ITableController
    {
        public void Home(XYTable table) { table.Home(); }
        public void MoveTo(XYTable table, Position2D target) { table.MoveTo(target); }
        public List<Position2D> ExecuteScan(XYTable table, ScanRecipe recipe) { return new List<Position2D>(); }
        public List<GCodeLine> GenerateGCode(ScanRecipe recipe) { return new List<GCodeLine>(); }
    }

    public class MockLaserController : ILaserController
    {
        public void SetPower(LaserUnit laser, double powerW) { laser.SetPower(powerW); }
        public void Arm(LaserUnit laser, SafetySystem safety) { laser.Arm(safety); }
        public void Fire(LaserUnit laser, SafetySystem safety) { laser.Fire(safety); }
        public void Stop(LaserUnit laser) { laser.Stop(); }
    }

    public class MockGasController : IGasController
    {
        public void SetFlow(GasSystem gas, double flowLMin) { gas.SetFlow(flowLMin); }
        public void StartPurge(GasSystem gas) { gas.StartPurge(); }
        public void Stop(GasSystem gas) { gas.Stop(); }
        public double ReadO2(GasSystem gas) { return gas.ChamberO2Ppm; }
    }

    public class MockSafetyMonitor : ISafetyMonitor
    {
        public SafetyStatusSummary CheckAll(SafetySystem safety) { return safety.GetStatusSummary(); }
        public void TriggerEStop(SafetySystem safety) { safety.PressEStop(); }
        public void ResetEStop(SafetySystem safety) { safety.ResetEStop(); }
    }

    // ══════════════════════════════════════════════════════════════════
    // USE CASE TESTS
    // ══════════════════════════════════════════════════════════════════

    [TestFixture]
    public class SimulationUseCaseTests
    {
        [Test]
        public void Execute_CallsSolverAndLogs()
        {
            var solver = new MockHeatSolver();
            var motion = new MockMotionGenerator();
            var audit = new MockAuditLogger();
            var useCase = new SimulationUseCase(solver, motion, audit);

            var material = new Material("Test", 0.5, 45, 8000, 400);
            var laser = new LaserBeam(500, 1e-6, 1e-3, 0.1);
            var result = useCase.Execute(material, laser, new Dictionary<string, object>());

            Assert.IsTrue(motion.GenerateCalled);
            Assert.IsTrue(solver.SolveCalled);
            Assert.Contains("SIM_START", audit.LoggedActions);
            Assert.Contains("SIM_END", audit.LoggedActions);
            Assert.AreEqual("MockSolver", result.SolverName);
        }
    }

    [TestFixture]
    public class LabControlUseCaseTests
    {
        [Test]
        public void ControlActions_UpdateEntitiesAndLog()
        {
            var table = new MockTableController();
            var laser = new MockLaserController();
            var gas = new MockGasController();
            var safety = new MockSafetyMonitor();
            var audit = new MockAuditLogger();
            
            var useCase = new LabControlUseCase(table, laser, gas, safety, audit);

            var t = new XYTable();
            var l = new LaserUnit();
            var g = new GasSystem();
            var s = new SafetySystem();

            // Home Table
            useCase.HomeTable(t);
            Assert.IsTrue(t.IsHomed);
            Assert.Contains("TABLE_HOME", audit.LoggedActions);

            // Set Laser Power
            useCase.SetLaserPower(l, 200);
            Assert.AreEqual(200, l.CurrentPowerW);
            Assert.Contains("LASER_SET_POWER", audit.LoggedActions);

            // E-Stop
            useCase.EmergencyStop(t, l, g, s);
            Assert.IsTrue(s.EStopPressed);
            Assert.AreEqual(LaserState.Standby, l.State); // stopped
            Assert.Contains("SAFETY_E_STOP", audit.LoggedActions);
        }
    }
}
