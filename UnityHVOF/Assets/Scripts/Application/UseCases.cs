// ============================================================================
// Application Use Cases — Orchestration layer.
//
// SRP: Orchestration only — no physics, no I/O, no Unity.
// DIP: Depends only on Domain ports (interfaces), not concrete classes.
// STRIDE: All operations audited via IAuditLogger.
//
// Port of: application/simulation_use_case.py,
//          application/hvof_laser_use_case.py,
//          lab_control/application/lab_control_use_case.py
// ============================================================================

using System;
using System.Collections.Generic;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Application
{
    // ══════════════════════════════════════════════════════════════════
    // Simulation Use Case
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Execute a laser treatment simulation.
    /// Dependencies injected via constructor (DIP).
    /// </summary>
    public sealed class SimulationUseCase
    {
        private readonly IHeatSolver _solver;
        private readonly IMotionGenerator _motionGen;
        private readonly ISimulationAuditLogger _audit;

        public SimulationUseCase(
            IHeatSolver heatSolver,
            IMotionGenerator motionGenerator,
            ISimulationAuditLogger auditLogger)
        {
            _solver = heatSolver;
            _motionGen = motionGenerator;
            _audit = auditLogger;
        }

        /// <summary>
        /// Execute the full simulation pipeline.
        /// 1. Generate trajectory → 2. Audit start → 3. Solve → 4. Audit end.
        /// </summary>
        public SimulationResult Execute(
            Material material,
            LaserBeam laser,
            Dictionary<string, object> motionParams,
            double[] gridSize = null,
            double resolution = 100e-6)
        {
            gridSize ??= new[] { 0.010, 0.010, 0.002 };

            // Step 1: Generate trajectory
            Trajectory trajectory = _motionGen.Generate(motionParams);

            // Step 2: Audit — log start
            var simParams = new Dictionary<string, object>
            {
                ["material"] = material.Name,
                ["laser_power_w"] = laser.Power,
                ["laser_wavelength_m"] = laser.Wavelength,
                ["grid_size_m"] = gridSize,
                ["resolution_m"] = resolution,
                ["trajectory_points"] = trajectory.NPoints
            };
            string runId = _audit.LogSimulationStart(material.Name, _solver.Name, simParams);

            try
            {
                // Step 3: Solve
                SimulationResult result = _solver.Solve(
                    material, laser, trajectory, gridSize, resolution);

                // Step 4: Audit — log end
                var resultSummary = new Dictionary<string, object>
                {
                    ["peak_temp_c"] = result.PeakTemperatureCelsius,
                    ["peak_fluence_j_m2"] = result.PeakFluenceJPerM2,
                    ["melt_depth_m"] = result.MeltDepthM?.ToString() ?? "null",
                    ["duration_s"] = result.DurationSeconds
                };
                _audit.LogSimulationEnd(runId, resultSummary);

                return result;
            }
            catch (Exception exc)
            {
                _audit.LogError(runId, exc.Message);
                throw;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // HVOF Laser Use Case (Two-Stage)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Execute a two-stage HVOF coating + laser treatment simulation.
    /// Stage 1 → Stage 2 → Post-processing → LaserTreatmentResult.
    /// </summary>
    public sealed class HVOFLaserUseCase
    {
        private readonly ICoatingGenerator _gen;
        private readonly ICoatingSolver _solver;

        public HVOFLaserUseCase(
            ICoatingGenerator coatingGenerator,
            ICoatingSolver solver)
        {
            _gen = coatingGenerator ?? throw new ArgumentNullException(nameof(coatingGenerator));
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        }

        /// <summary>Run the full two-stage simulation.</summary>
        public LaserTreatmentResult Execute(
            Material material,
            CoatingConfig coatingConfig = null,
            double laserPower = 500.0,
            double beamRadius = 0.001,
            double scanSpeed = 0.010,
            double resolution = 2e-6,
            int seed = 42)
        {
            var config = coatingConfig ?? new CoatingConfig();

            // Stage 1: Generate coating microstructure
            CoatingGrid coatingGrid = _gen.Generate(
                config, material.ThermalConductivity, resolution, seed);

            // Stage 2: Laser treatment simulation
            CoatingSolverOutput solverOutput = _solver.Solve(
                coatingGrid, config,
                laserPower, beamRadius, scanSpeed,
                material.Absorption, material.Density, material.SpecificHeat,
                material.TMelt, material.TAmbient);

            // Post-processing
            return CoatingAnalysis.BuildTreatmentResult(
                coatingGrid, config, solverOutput,
                material.TMelt, laserPower, scanSpeed, beamRadius);
        }

        /// <summary>Run simulations at multiple power levels for validation.</summary>
        public List<LaserTreatmentResult> ParameterSweep(
            Material material,
            double[] powerLevels = null,
            CoatingConfig coatingConfig = null,
            double beamRadius = 0.001,
            double scanSpeed = 0.010,
            double resolution = 2e-6,
            int seed = 42)
        {
            powerLevels ??= new[] { 200.0, 350.0, 500.0 };
            var results = new List<LaserTreatmentResult>();

            foreach (double power in powerLevels)
            {
                results.Add(Execute(
                    material, coatingConfig, power,
                    beamRadius, scanSpeed, resolution, seed));
            }

            return results;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Lab Control Use Case
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orchestrates lab control operations: table, laser, gas, safety.
    /// All operations audited (STRIDE: repudiation defense).
    /// </summary>
    public sealed class LabControlUseCase
    {
        private readonly ITableController _table;
        private readonly ILaserController _laser;
        private readonly IGasController _gas;
        private readonly ISafetyMonitor _safety;
        private readonly ILabAuditLogger _audit;

        public LabControlUseCase(
            ITableController table,
            ILaserController laser,
            IGasController gas,
            ISafetyMonitor safety,
            ILabAuditLogger audit)
        {
            _table = table;
            _laser = laser;
            _gas = gas;
            _safety = safety;
            _audit = audit;
        }

        public void HomeTable(XYTable table)
        {
            _table.Home(table);
            _audit.LogAction("TABLE", "HOME", new Dictionary<string, object>());
        }

        public void SetLaserPower(LaserUnit laser, double powerW)
        {
            _laser.SetPower(laser, powerW);
            _audit.LogAction("LASER", "SET_POWER",
                new Dictionary<string, object> { ["power_w"] = powerW });
        }

        public void ArmLaser(LaserUnit laser, SafetySystem safety)
        {
            _laser.Arm(laser, safety);
            _audit.LogAction("LASER", "ARM", new Dictionary<string, object>());
        }

        public void FireLaser(LaserUnit laser, SafetySystem safety)
        {
            _laser.Fire(laser, safety);
            _audit.LogAction("LASER", "FIRE",
                new Dictionary<string, object> { ["power_w"] = laser.CurrentPowerW });
        }

        public void StopLaser(LaserUnit laser)
        {
            _laser.Stop(laser);
            _audit.LogAction("LASER", "STOP", new Dictionary<string, object>());
        }

        public void StartGasPurge(GasSystem gas)
        {
            _gas.StartPurge(gas);
            _audit.LogAction("GAS", "START_PURGE", new Dictionary<string, object>());
        }

        public void StopGas(GasSystem gas)
        {
            _gas.Stop(gas);
            _audit.LogAction("GAS", "STOP", new Dictionary<string, object>());
        }

        public void EmergencyStop(
            XYTable table, LaserUnit laser, GasSystem gas, SafetySystem safety)
        {
            _safety.TriggerEStop(safety);
            _laser.Stop(laser);
            _gas.Stop(gas);
            _audit.LogSafetyEvent("E_STOP",
                new Dictionary<string, object> { ["triggered_by"] = "USER" });
        }

        public void ResetEStop(SafetySystem safety)
        {
            _safety.ResetEStop(safety);
            _audit.LogSafetyEvent("E_STOP_RESET", new Dictionary<string, object>());
        }
    }
}
