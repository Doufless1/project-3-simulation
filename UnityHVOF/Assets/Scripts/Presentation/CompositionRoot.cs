// ============================================================================
// Composition Root — Pure DI entry point for the HVOF simulation.
//
// This is the ONLY place concrete classes are `new`-ed and wired together.
// No service locator, no framework — just manual `new` (Pure DI).
//
// Responsibilities:
//   1. Create domain entities (XYTable, LaserUnit, GasSystem, SafetySystem)
//   2. Create infrastructure implementations (solvers, controllers, loggers)
//   3. Create application use cases (inject ports)
//   4. Build 3D scene and UI
//   5. Wire UI callbacks → use cases
//   6. Drive Update loop: entity state → scene/VFX updates
// ============================================================================

using UnityEngine;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.ValueObjects;
using HVOFSim.Infrastructure.Laser;
using HVOFSim.Infrastructure.Logging;
using HVOFSim.Infrastructure.Motion;
using HVOFSim.Infrastructure.Solvers;
using HVOFSim.Infrastructure.VirtualHardware;
using HVOFSim.Infrastructure.Persistence;
using HVOFSim.Infrastructure.Coating;
using HVOFSim.Application;
using HVOFSim.Presentation.Camera;
using HVOFSim.Presentation.Scene;
using HVOFSim.Presentation.UI;
using HVOFSim.Presentation.VFX;
using HVOFSim.Domain.Ports;
using UnityEngine.EventSystems;
using Material = UnityEngine.Material;

namespace HVOFSim.Presentation
{
    /// <summary>
    /// MonoBehaviour entry point — bootstraps the entire simulation.
    /// Attach this to an empty GameObject in the scene.
    /// </summary>
    public sealed class CompositionRoot : MonoBehaviour
    {
        // ── Domain Entities ──────────────────────────────────────────
        private XYTable _table;
        private LaserUnit _laser;
        private GasSystem _gas;
        private SafetySystem _safety;

        // ── Infrastructure ───────────────────────────────────────────
        private VirtualTableController _tableCtrl;
        private VirtualLaserController _laserCtrl;
        private VirtualGasController _gasCtrl;
        private VirtualSafetyMonitor _safetyMonitor;
        private AuditLogger _auditLogger;
        private EquipmentCatalog _equipmentCatalog;

        // ── Application ──────────────────────────────────────────────
        private LabControlUseCase _labControl;
        private SimulationUseCase _simUseCase;
        private HVOFLaserUseCase _hvofUseCase;

        // ── Presentation ─────────────────────────────────────────────
        private LabSceneBuilder _sceneBuilder;
        private MainDashboard _dashboard;
        private LaserBeamVFX _laserVFX;
        private GasFlowVFX _gasVFX;
        private HeatmapRenderer _heatmapRenderer;
        private OrbitCamera _orbitCam;

        private void Awake()
        {
            BootstrapDomain();
            BootstrapInfrastructure();
            BootstrapApplication();
            BootstrapPresentation();
            WireCallbacks();

            Debug.Log("[CompositionRoot] HVOF Simulation bootstrapped successfully.");
        }

        private void Update()
        {
            // Drive real-time updates: entity state → visuals
            _sceneBuilder.UpdateTablePosition(_table);
            _sceneBuilder.UpdateChamberState(_safety.ChamberInterlock, _gas.State);
            _sceneBuilder.UpdateLaserLED(_laser.State);
            if (_laserVFX != null) _laserVFX.UpdateState(_laser.State, _laser.PowerPct);
            if (_gasVFX != null) _gasVFX.UpdateState(_gas.State, _gas.CurrentFlow.ValueLPerMin);

            // Read O2 during purge (simulated decay)
            if (_gas.State == GasState.Purging || _gas.State == GasState.Flowing)
                _gasCtrl.ReadO2(_gas);

            // Update dashboard HUD
            _dashboard.UpdateDisplay(_table, _laser, _gas, _safety);
        }

        // ═════════════════════════════════════════════════════════════
        // Bootstrap Methods
        // ═════════════════════════════════════════════════════════════

        private void BootstrapDomain()
        {
            // Initial domain entities use default equipment specs
            var chamberSpec = ChamberSpecs.GetSpec(ChamberType.StandardAluminium);
            var laserSpec = LaserTypeSpecs.GetSpec(LaserType.YtterbiumFiber);
            var gasSpec = GasTypeSpecs.GetSpec(GasType.Argon);

            _table = new XYTable(travelXMm: chamberSpec.TravelXMm, travelYMm: chamberSpec.TravelYMm);
            _laser = new LaserUnit(maxPowerW: laserSpec.MaxPowerW, wavelengthNm: laserSpec.WavelengthNm);
            _gas = new GasSystem(maxFlowLMin: gasSpec.MaxFlowLMin);
            _safety = new SafetySystem();
        }

        private void BootstrapInfrastructure()
        {
            _tableCtrl = new VirtualTableController();
            _laserCtrl = new VirtualLaserController();
            _gasCtrl = new VirtualGasController();
            _safetyMonitor = new VirtualSafetyMonitor();
            _auditLogger = new AuditLogger(
                System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "AuditLogs"));
            _equipmentCatalog = new EquipmentCatalog();
        }

        private void BootstrapApplication()
        {
            _labControl = new LabControlUseCase(
                _tableCtrl, _laserCtrl, _gasCtrl, _safetyMonitor, _auditLogger);

            ILaserSource laserSource = LaserFactory.Create("gaussian");
            IHeatSolver solver = new FDMHeatSolver3D(laserSource);
            IMotionGenerator motionGen = new RasterGenerator();

            _simUseCase = new SimulationUseCase(solver, motionGen, _auditLogger);
            _hvofUseCase = new HVOFLaserUseCase(
                new HVOFCoatingGenerator(),
                new FDMCoatingSolver2D(1e4)
            );
        }

        private void BootstrapPresentation()
        {
            // Scene builder
            var sceneGO = new GameObject("LabScene");
            _sceneBuilder = sceneGO.AddComponent<LabSceneBuilder>();
            _sceneBuilder.BuildScene();

            // Camera
            var mainCamGO = GameObject.Find("Main Camera");
            if (mainCamGO != null)
            {
                var cam = mainCamGO.GetComponent<UnityEngine.Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
                    cam.fieldOfView = 45;
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 100f;
                }
                
                if (mainCamGO.GetComponent<OrbitCamera>() == null)
                    _orbitCam = mainCamGO.AddComponent<OrbitCamera>();
                else
                    _orbitCam = mainCamGO.GetComponent<OrbitCamera>();
            }

            // VFX — use scene builder's own VFX components (created during SwapLaserType/SwapGasTanks)
            _laserVFX = _sceneBuilder.BeamVFX;
            _gasVFX = _sceneBuilder.GasVFX;

            // Heatmap
            _heatmapRenderer = _sceneBuilder.Workpiece.AddComponent<HeatmapRenderer>();
            var heatmapMat = new Material(Shader.Find("HVOFSim/HeatmapShader"));
            _sceneBuilder.Workpiece.GetComponent<Renderer>().material = heatmapMat;
            _heatmapRenderer.Initialize(heatmapMat);

            // EventSystem for UI Interaction
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();

            // Dashboard UI
            var uiGO = new GameObject("Dashboard");
            _dashboard = uiGO.AddComponent<MainDashboard>();
            _dashboard.Initialize(_equipmentCatalog);
            
            // Link Main Camera to Canvas for proper WorldSpace event routing
            var canvas = _dashboard.GetComponentInChildren<Canvas>();
            if (canvas != null && mainCamGO != null)
            {
                canvas.worldCamera = mainCamGO.GetComponent<UnityEngine.Camera>();
            }
        }

        private void WireCallbacks()
        {
            // Wire UI button callbacks → application use case methods
            _dashboard.OnRunSimulationClicked = () =>
            {
                try
                {
                    // Read live parameters from the Dashboard UI and Domain Entities
                    float widthM = _dashboard.ScanWidthMm / 1000f;
                    float heightM = _dashboard.ScanHeightMm / 1000f;
                    float speedMS = _dashboard.ScanSpeedMmS / 1000f;
                    float spotRadiusM = (_dashboard.SpotSizeMm / 1000f) / 2f;
                    float lineSpacingM = (_dashboard.SpotSizeMm / 1000f) * (1f - (_dashboard.OverlapPct / 100f));
                    
                    // Use the selected material from the dashboard if available
                    var material = _dashboard.SelectedMaterial ?? new HVOFSim.Domain.Entities.Material("WC-NiCr", 0.75, 45, 14800, 300);
                    var laserSpec = LaserTypeSpecs.GetSpec(_dashboard.SelectedLaserType);
                    var laser = new LaserBeam(_laser.CurrentPowerW, laserSpec.WavelengthM, spotRadiusM, 0.15);
                    
                    var motionParams = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["width_m"] = widthM,
                        ["height_m"] = heightM,
                        ["speed_m_s"] = speedMS,
                        ["line_spacing_m"] = lineSpacingM
                    };
                    
                    // Domain grid [x, y, z] resolution in meters
                    var result = _simUseCase.Execute(material, laser, motionParams, new[] {widthM, heightM, 0.002}, 500e-6);
                    _dashboard.ShowSimulationResults(result);
                    _heatmapRenderer.UpdateFromSimulation(result);
                }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnHomeClicked = () =>
            {
                try { _labControl.HomeTable(_table); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnPowerChanged = (power) =>
            {
                try { _labControl.SetLaserPower(_laser, power); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnArmClicked = () =>
            {
                try { _labControl.ArmLaser(_laser, _safety); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnFireClicked = () =>
            {
                try { _labControl.FireLaser(_laser, _safety); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnStopClicked = () =>
            {
                try { _labControl.StopLaser(_laser); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnPurgeClicked = () =>
            {
                try { _labControl.StartGasPurge(_gas); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnEStopClicked = () =>
            {
                try { _labControl.EmergencyStop(_table, _laser, _gas, _safety); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnLockDoorClicked = () =>
            {
                _safety.LockDoor();
                _auditLogger.LogSafetyEvent("DOOR_LOCK",
                    new System.Collections.Generic.Dictionary<string, object>());
                _dashboard.LogToMonitor("Safety: Door Interlock LOCKED");
            };

            _dashboard.OnLockChamberClicked = () =>
            {
                _safety.LockChamber();
                _auditLogger.LogSafetyEvent("CHAMBER_LOCK",
                    new System.Collections.Generic.Dictionary<string, object>());
                _dashboard.LogToMonitor("Safety: Chamber Interlock LOCKED");
            };

            _dashboard.OnUnlockDoorClicked = () =>
            {
                _safety.UnlockDoor();
                _auditLogger.LogSafetyEvent("DOOR_UNLOCK",
                    new System.Collections.Generic.Dictionary<string, object>());
                _dashboard.LogToMonitor("Safety: Door Interlock UNLOCKED");
            };

            _dashboard.OnUnlockChamberClicked = () =>
            {
                _safety.UnlockChamber();
                _auditLogger.LogSafetyEvent("CHAMBER_UNLOCK",
                    new System.Collections.Generic.Dictionary<string, object>());
                _dashboard.LogToMonitor("Safety: Chamber Interlock UNLOCKED");
            };

            _dashboard.OnSetGasFlowClicked = (flow) =>
            {
                try
                {
                    _gas.SetFlow(flow);
                    _auditLogger.LogAction("GAS", "SET_FLOW",
                        new System.Collections.Generic.Dictionary<string, object> { ["flow_lmin"] = flow });
                    _dashboard.LogToMonitor($"Gas flow set to {flow:F1} L/min");
                }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnStopGasClicked = () =>
            {
                try { _labControl.StopGas(_gas); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnResetEStopClicked = () =>
            {
                try { _labControl.ResetEStop(_safety); }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            _dashboard.OnResetLabClicked = () =>
            {
                try 
                { 
                    _labControl.StopLaser(_laser);
                    _labControl.StopGas(_gas);
                    _labControl.HomeTable(_table);
                    if (_safety.ChamberInterlock == InterlockStatus.Locked) _safety.UnlockChamber();
                    if (_safety.DoorInterlock == InterlockStatus.Locked) _safety.UnlockDoor();
                    _dashboard.LogToMonitor("Lab Reset Complete");
                }
                catch (System.Exception e) { _dashboard.LogToMonitor($"<color=#FF5555>Error: {e.Message}</color>"); }
            };

            // ── Equipment Change Callbacks ────────────────────────────

            _dashboard.OnLaserTypeChanged = (laserType) =>
            {
                var spec = LaserTypeSpecs.GetSpec(laserType);
                // Reconstruct laser with new specs
                _laser = new LaserUnit(maxPowerW: spec.MaxPowerW, wavelengthNm: spec.WavelengthNm);
                // Swap 3D model and VFX
                _sceneBuilder.SwapLaserType(laserType);
                // Re-bind VFX reference (new component created during swap)
                _laserVFX = _sceneBuilder.BeamVFX;
                _dashboard.LogToMonitor($"Laser reconfigured: {spec.Name} ({spec.WavelengthNm} nm, max {spec.MaxPowerW} W)");
            };

            _dashboard.OnGasTypeChanged = (gasType) =>
            {
                var spec = GasTypeSpecs.GetSpec(gasType);
                _gas = new GasSystem(maxFlowLMin: spec.MaxFlowLMin);
                _sceneBuilder.SwapGasTanks(gasType);
                _gasVFX = _sceneBuilder.GasVFX;
                _dashboard.LogToMonitor($"Gas system reconfigured: {spec.DisplayName}");
            };

            _dashboard.OnChamberTypeChanged = (chamberType) =>
            {
                var spec = ChamberSpecs.GetSpec(chamberType);
                _table = new XYTable(travelXMm: spec.TravelXMm, travelYMm: spec.TravelYMm);
                _sceneBuilder.SwapChamber(chamberType);
                // Also update scene builder's dynamic bloom
                _sceneBuilder.UpdateDynamicBloom(0);
                _dashboard.LogToMonitor($"Chamber reconfigured: {spec.Name} (travel: {spec.TravelXMm:F0}×{spec.TravelYMm:F0} mm)");
            };

            _dashboard.OnMaterialChanged = (material) =>
            {
                _dashboard.LogToMonitor($"Material loaded: {material.Name} (T_melt={material.TMelt:F0}°C)");
            };
        }
    }
}
