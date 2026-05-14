// ============================================================================
// Domain Ports — Abstract interfaces for both simulation and lab control.
//
// Dependency Inversion Principle:
//   Application and Domain layers depend ONLY on these abstractions.
//   Infrastructure provides concrete implementations.
//
// Interface Segregation Principle:
//   Each port has a single, focused responsibility.
//
// Port of: domain/ports.py + lab_control/domain/ports.py
// ============================================================================

using System.Collections.Generic;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.ValueObjects;

namespace HVOFSim.Domain.Ports
{
    // ════════════════════════════════════════════════════════════════
    // SIMULATION PORTS
    // ════════════════════════════════════════════════════════════════

    // ── Heat Solver Port (Strategy Pattern) ──────────────────────────

    /// <summary>
    /// Abstract interface for thermal solvers.
    /// Pattern: Strategy — swap solvers without changing use case code.
    /// </summary>
    public interface IHeatSolver
    {
        /// <summary>Human-readable solver name for audit logging.</summary>
        string Name { get; }

        /// <summary>
        /// Run the thermal simulation and return results.
        /// </summary>
        SimulationResult Solve(
            Material material,
            LaserBeam laser,
            Trajectory trajectory,
            double[] gridSize,
            double resolution);
    }

    // ── Laser Source Port (Strategy Pattern) ──────────────────────────

    /// <summary>
    /// Abstract interface for beam intensity calculation.
    /// Pattern: Strategy — different beam profiles are interchangeable.
    /// </summary>
    public interface ILaserSource
    {
        /// <summary>
        /// Compute 2D intensity distribution on the surface grid.
        /// </summary>
        /// <param name="xGrid">X-coordinate mesh [m].</param>
        /// <param name="yGrid">Y-coordinate mesh [m].</param>
        /// <param name="centerX">Beam center X position [m].</param>
        /// <param name="centerY">Beam center Y position [m].</param>
        /// <param name="spotRadius">Current spot radius w [m].</param>
        /// <param name="power">Laser power [W].</param>
        /// <returns>2D intensity array [W/m²].</returns>
        double[,] ComputeIntensity(
            double[,] xGrid, double[,] yGrid,
            double centerX, double centerY,
            double spotRadius, double power);
    }

    // ── Coating Generator & Solver Ports ─────────────────────────────
    
    public interface ICoatingGenerator
    {
        CoatingGrid Generate(CoatingConfig config, double coatingK, double resolution = 2e-6, int? seed = null);
    }

    public interface ICoatingSolver
    {
        CoatingSolverOutput Solve(
            CoatingGrid coatingGrid,
            CoatingConfig config,
            double laserPower,
            double beamRadius,
            double scanSpeed,
            double absorptivity,
            double coatingRho,
            double coatingCp,
            double tMelt,
            double tAmbient = 25.0);
    }

    // ── Motion Generator Port (Strategy Pattern) ─────────────────────

    /// <summary>
    /// Abstract interface for trajectory generation.
    /// </summary>
    public interface IMotionGenerator
    {
        /// <summary>Generate a motion trajectory from parameters.</summary>
        Trajectory Generate(Dictionary<string, object> parameters);
    }

    // ── Material Repository Port (Repository Pattern) ────────────────

    /// <summary>
    /// Abstract interface for material persistence.
    /// Pattern: Repository — abstracts storage mechanism.
    /// </summary>
    public interface IMaterialRepository
    {
        void Save(Dictionary<string, object> materialData);
        List<Dictionary<string, object>> LoadAll();
        List<Dictionary<string, object>> FindBest(int n = 5);
    }

    // ── Simulation Audit Logger Port (STRIDE: Repudiation Defense) ───

    /// <summary>
    /// Prevents repudiation by recording all simulation runs
    /// with timestamps and parameters.
    /// </summary>
    public interface ISimulationAuditLogger
    {
        /// <summary>Log the start of a simulation run. Returns unique run ID.</summary>
        string LogSimulationStart(string materialName, string solverName, Dictionary<string, object> parameters);

        /// <summary>Log the completion of a simulation run.</summary>
        void LogSimulationEnd(string runId, Dictionary<string, object> resultSummary);

        /// <summary>Log an error during a simulation run.</summary>
        void LogError(string runId, string error);
    }

    // ════════════════════════════════════════════════════════════════
    // LAB CONTROL PORTS
    // ════════════════════════════════════════════════════════════════

    // ── Table Controller Port ────────────────────────────────────────

    /// <summary>
    /// Strategy: Interface for X-Y table motion control.
    /// Implementations: VirtualTableController (simulation),
    ///                  GrblTableController (real hardware — future).
    /// </summary>
    public interface ITableController
    {
        void Home(XYTable table);
        void MoveTo(XYTable table, Position2D target);
        List<Position2D> ExecuteScan(XYTable table, ScanRecipe recipe);
        List<GCodeLine> GenerateGCode(ScanRecipe recipe);
    }

    // ── Laser Controller Port ────────────────────────────────────────

    /// <summary>Strategy: Interface for laser control.</summary>
    public interface ILaserController
    {
        void SetPower(LaserUnit laser, double powerW);
        void Arm(LaserUnit laser, SafetySystem safety);
        void Fire(LaserUnit laser, SafetySystem safety);
        void Stop(LaserUnit laser);
    }

    // ── Gas Controller Port ──────────────────────────────────────────

    /// <summary>Strategy: Interface for shielding gas control.</summary>
    public interface IGasController
    {
        void SetFlow(GasSystem gas, double flowLMin);
        void StartPurge(GasSystem gas);
        void Stop(GasSystem gas);
        double ReadO2(GasSystem gas);
    }

    // ── Safety Monitor Port ──────────────────────────────────────────

    /// <summary>Strategy: Interface for safety system monitoring.</summary>
    public interface ISafetyMonitor
    {
        SafetyStatusSummary CheckAll(SafetySystem safety);
        void TriggerEStop(SafetySystem safety);
        void ResetEStop(SafetySystem safety);
    }

    // ── Cost Calculator Port ─────────────────────────────────────────

    /// <summary>Repository: Interface for equipment cost estimation.</summary>
    public interface ICostCalculator
    {
        List<EquipmentSpec> GetCatalog();
        double CalculateTotal(List<EquipmentSpec> selectedItems);
        List<EquipmentSpec> GetByCategory(string category);
    }

    // ── Lab Audit Logger Port (STRIDE: Repudiation Defense) ──────────

    /// <summary>
    /// Prevents repudiation by recording all lab operations
    /// with timestamps and parameters.
    /// </summary>
    public interface ILabAuditLogger
    {
        /// <summary>Log a lab action. Returns unique event ID.</summary>
        string LogAction(string subsystem, string action, Dictionary<string, object> details);

        /// <summary>Log a safety-critical event (higher priority).</summary>
        string LogSafetyEvent(string eventType, Dictionary<string, object> details);

        /// <summary>Retrieve recent log entries.</summary>
        List<Dictionary<string, object>> GetRecentLogs(int count = 50);
    }
}
