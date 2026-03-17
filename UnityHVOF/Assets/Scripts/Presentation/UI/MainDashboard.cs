// ============================================================================
// Main Dashboard UI — Aligned with Python Dash dashboard layout.
//
// Structure matches lab_control/presentation/dashboard.py:
//   - Header with gradient title + app description
//   - Workflow stepper (4 steps)
//   - Tab bar: X-Y Table | Laser & Gas | Safety | Cost
//   - Bottom status bar with live state
//   - Activity log in Safety tab
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.ValueObjects;
using HVOFSim.Domain.Ports;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HVOFSim.Presentation.UI
{
    public sealed class MainDashboard : MonoBehaviour
    {
        private Canvas _canvas;

        // Live displays
        private TextMeshProUGUI _statusBarText;
        private TextMeshProUGUI _monitorText;
        private TextMeshProUGUI _laserStateText;
        private TextMeshProUGUI _laserPowerValText;
        private TextMeshProUGUI _laserPowerPctText;
        private TextMeshProUGUI _laserWlText;
        private TextMeshProUGUI _gasStateText;
        private TextMeshProUGUI _gasO2Text;
        private TextMeshProUGUI _gasSupplyText;
        private TextMeshProUGUI _gasSafeText;
        private TextMeshProUGUI _safetyDoorText;
        private TextMeshProUGUI _safetyChamberText;
        private TextMeshProUGUI _safetyEstopText;
        private TextMeshProUGUI _safetyWarningText;
        private TextMeshProUGUI _safetyAllClearText;
        private TextMeshProUGUI _posXText;
        private TextMeshProUGUI _posYText;
        private TextMeshProUGUI _tableStateText;
        private TextMeshProUGUI _scanPointsText;
        private TextMeshProUGUI _powerLabel;
        private TextMeshProUGUI _simResultText;
        private TextMeshProUGUI _activityLogText;

        // Stepper step circles
        private Image _step1Bg, _step2Bg, _step3Bg, _step4Bg;

        // Controls
        private Button _armButton;
        private Button _fireButton;
        private float _gasSliderValue = 0f;

        // Scan Parameters
        public float ScanSpeedMmS { get; private set; } = 12f;
        public float ScanWidthMm { get; private set; } = 50f;
        public float ScanHeightMm { get; private set; } = 50f;
        public float SpotSizeMm { get; private set; } = 2.5f;
        public float OverlapPct { get; private set; } = 50f;

        // Equipment Selection State
        public LaserType SelectedLaserType { get; private set; } = LaserType.YtterbiumFiber;
        public BeamProfile SelectedBeamProfile { get; private set; } = BeamProfile.Gaussian;
        public GasType SelectedGasType { get; private set; } = GasType.Argon;
        public ChamberType SelectedChamberType { get; private set; } = ChamberType.StandardAluminium;
        public HVOFSim.Domain.Entities.Material SelectedMaterial { get; private set; }

        private List<string> _consoleLogs = new List<string>();
        private ICostCalculator _costCalculator;

        // Setup tab dynamic UI elements
        private TextMeshProUGUI _laserInfoText;
        private TextMeshProUGUI _gasWarningText;
        private TextMeshProUGUI _chamberInfoText;
        private TextMeshProUGUI _materialInfoText;
        private Slider _gasFlowSlider;
        private TextMeshProUGUI _gasFlowLabel;
        private GameObject _gasControlsPanel;
        private TextMeshProUGUI _derivedValuesText;
        private Image[] _laserCardBgs = new Image[4];
        private Image[] _gasCardBgs = new Image[5];
        private Image[] _chamberCardBgs = new Image[5];
        private Image[] _materialCardBgs = new Image[7];

        // Callbacks — existing
        public Action OnRunSimulationClicked;
        public Action OnHomeClicked;
        public Action OnArmClicked;
        public Action OnFireClicked;
        public Action OnStopClicked;
        public Action OnEStopClicked;
        public Action OnPurgeClicked;
        public Action<float> OnPowerChanged;
        public Action OnLockDoorClicked;
        public Action OnUnlockDoorClicked;
        public Action OnLockChamberClicked;
        public Action OnUnlockChamberClicked;
        public Action OnResetEStopClicked;
        public Action<float> OnSetGasFlowClicked;
        public Action OnStopGasClicked;
        public Action OnResetLabClicked;

        // Callbacks — new equipment switching
        public Action<LaserType> OnLaserTypeChanged;
        public Action<BeamProfile> OnBeamProfileChanged;
        public Action<GasType> OnGasTypeChanged;
        public Action<ChamberType> OnChamberTypeChanged;
        public Action<ChamberSpec> OnCustomChamberChanged;
        public Action<HVOFSim.Domain.Entities.Material> OnMaterialChanged;

        private bool _isScanComplete = false;

        // ── Design Tokens (exact match: shared/design_tokens.py) ──
        static readonly Color COL_BG       = Hex("#0a0e17");
        static readonly Color COL_CARD     = Hex("#111827");
        static readonly Color COL_BORDER   = Hex("#1e293b");
        static readonly Color COL_PRIMARY  = Hex("#3b82f6");
        static readonly Color COL_ACCENT   = Hex("#8b5cf6");
        static readonly Color COL_SUCCESS  = Hex("#10b981");
        static readonly Color COL_DANGER   = Hex("#ef4444");
        static readonly Color COL_WARNING  = Hex("#f59e0b");
        static readonly Color COL_TEXT     = Hex("#e2e8f0");
        static readonly Color COL_MUTED    = Hex("#a0aec0");

        // Tab system (5 tabs: Setup | X-Y Table | Laser & Gas | Safety | Cost)
        private const int TAB_COUNT = 5;
        private GameObject[] _tabPanels = new GameObject[TAB_COUNT];
        private Button[] _tabButtons = new Button[TAB_COUNT];
        private Image[] _tabBtnImages = new Image[TAB_COUNT];

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public void Initialize(ICostCalculator costCalc = null)
        {
            _costCalculator = costCalc;
            // Initialize default material
            var defaultPreset = MaterialPresetLibrary.GetAll()[2]; // WC-12Co
            SelectedMaterial = defaultPreset.ToDomainMaterial();

            CreateCanvas();
            CreateHeader();
            CreateWorkflowStepper();
            CreateTopTabBar();
            _tabPanels[0] = CreateSetupTab();
            _tabPanels[1] = CreateXYTableTab();
            _tabPanels[2] = CreateLaserGasTab();
            _tabPanels[3] = CreateSafetyTab();
            _tabPanels[4] = CreateCostTab();
            ShowTab(0);
            CreateBottomStatusBar();
            UpdateDerivedValues();
            LogToMonitor("SYSTEM INITIALIZED. Awaiting commands...");
        }

        public void UpdateDisplay(XYTable table, LaserUnit laser, GasSystem gas, SafetySystem safety)
        {
            // Status bar
            if (_statusBarText != null)
            {
                string safeCol = safety.AllInterlocksLocked ? "#10B981" : "#EF4444";
                string safeTxt = safety.AllInterlocksLocked ? "ALL CLEAR" : "NOT READY";
                _statusBarText.text =
                    $"Table: {table.State} X={table.Position.XMm:F1} Y={table.Position.YMm:F1}   " +
                    $"Laser: {laser.State} {laser.CurrentPowerW:F0}W   " +
                    $"Gas: {gas.State} {gas.CurrentFlow.ValueLPerMin:F1} L/min   " +
                    $"Safety: <color={safeCol}>{safeTxt}</color>";
            }

            // X-Y indicators
            if (_posXText != null) _posXText.text = $"<color=#3b82f6>{table.Position.XMm:F2} mm</color>";
            if (_posYText != null) _posYText.text = $"<color=#3b82f6>{table.Position.YMm:F2} mm</color>";
            if (_tableStateText != null) _tableStateText.text = $"<color=#e2e8f0>{table.State}</color>";

            // Laser indicators
            if (_laserStateText != null)
            {
                string lc = laser.State == LaserState.Firing ? "#EF4444" : laser.State == LaserState.Armed ? "#F59E0B" : "#10B981";
                _laserStateText.text = $"<color={lc}>{laser.State}</color>";
            }
            if (_laserPowerValText != null) _laserPowerValText.text = $"<color=#3b82f6>{laser.CurrentPowerW:F0} W</color>";
            if (_laserPowerPctText != null) _laserPowerPctText.text = $"<color=#8b5cf6>{laser.PowerPct:F0}%</color>";
            if (_laserWlText != null) _laserWlText.text = $"<color=#e2e8f0>{laser.WavelengthNm:F0} nm</color>";

            // Gas indicators
            if (_gasStateText != null) _gasStateText.text = $"<color=#e2e8f0>{gas.State}</color>";
            if (_gasO2Text != null)
            {
                string o2c = gas.IsAtmosphereSafe ? "#10B981" : "#EF4444";
                _gasO2Text.text = $"<color={o2c}>{gas.ChamberO2Ppm:F0}</color>";
            }
            if (_gasSupplyText != null) _gasSupplyText.text = $"<color=#e2e8f0>{gas.SupplyLevelPct:F0}%</color>";
            if (_gasSafeText != null)
            {
                _gasSafeText.text = gas.IsAtmosphereSafe
                    ? "<color=#10B981>SAFE</color>"
                    : "<color=#EF4444>UNSAFE</color>";
            }

            // Safety indicators
            if (_safetyDoorText != null)
            {
                string dc = safety.DoorInterlock == InterlockStatus.Locked ? "#10B981" : "#EF4444";
                _safetyDoorText.text = $"<color={dc}>{safety.DoorInterlock}</color>";
            }
            if (_safetyChamberText != null)
            {
                string cc = safety.ChamberInterlock == InterlockStatus.Locked ? "#10B981" : "#EF4444";
                _safetyChamberText.text = $"<color={cc}>{safety.ChamberInterlock}</color>";
            }
            if (_safetyEstopText != null)
            {
                string ec = safety.EStopPressed ? "#EF4444" : "#10B981";
                _safetyEstopText.text = $"<color={ec}>{(safety.EStopPressed ? "PRESSED" : "OK")}</color>";
            }
            if (_safetyWarningText != null) _safetyWarningText.text = safety.LaserWarningActive ? "<color=#F59E0B>ACTIVE</color>" : "<color=#a0aec0>Off</color>";
            if (_safetyAllClearText != null)
            {
                _safetyAllClearText.text = safety.AllInterlocksLocked
                    ? "<color=#10B981>ALL CLEAR</color>"
                    : "<color=#EF4444>NOT READY</color>";
            }

            // Stepper colors
            bool doorLocked = safety.DoorInterlock == InterlockStatus.Locked && safety.ChamberInterlock == InterlockStatus.Locked;
            if (_step1Bg != null) _step1Bg.color = doorLocked ? COL_SUCCESS : COL_BORDER;
            if (_step2Bg != null) _step2Bg.color = gas.State == GasState.Purging || gas.State == GasState.Flowing ? COL_SUCCESS : (doorLocked ? COL_PRIMARY : COL_BORDER);
            if (_step3Bg != null) _step3Bg.color = laser.State == LaserState.Firing ? COL_SUCCESS : (laser.State == LaserState.Armed ? COL_WARNING : COL_BORDER);
            if (_step4Bg != null) _step4Bg.color = (table.State == TableState.Scanning || _isScanComplete) ? COL_SUCCESS : COL_BORDER;

            // Button interactivity
            if (_armButton != null) _armButton.interactable = safety.AllInterlocksLocked && laser.State != LaserState.Armed && laser.State != LaserState.Firing;
            if (_fireButton != null) _fireButton.interactable = laser.State == LaserState.Armed;
        }

        public void ShowSimulationResults(SimulationResult result)
        {
            _isScanComplete = true; // Mark scan as finished to turn step 4 green
            if (_simResultText != null)
                _simResultText.text =
                    $"<color=#10B981><b>Simulation Complete</b></color>\n\n" +
                    $"<b>Solver:</b> {result.SolverName}\n" +
                    $"<b>Peak Temp:</b> {result.PeakTemperatureCelsius:F1} C\n" +
                    $"<b>Peak Fluence:</b> {result.PeakFluenceJPerM2:F2} J/m2\n" +
                    $"<b>Melt Depth:</b> {(result.MeltDepthM.HasValue ? $"{result.MeltDepthM.Value * 1e6:F1} um" : "None")}\n" +
                    $"<b>Total Energy:</b> {result.TotalEnergyJ:F4} J\n" +
                    $"<b>Compute Time:</b> {result.DurationSeconds:F3} s";
            LogToMonitor($"Simulation finished. Peak Temp: {result.PeakTemperatureCelsius:F1} C");
        }

        public void LogToMonitor(string message)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            _consoleLogs.Add($"[{timestamp}] {message}");
            if (_consoleLogs.Count > 100) _consoleLogs.RemoveAt(0);
            if (_activityLogText != null)
            {
                _activityLogText.text = string.Join("\n", _consoleLogs.AsEnumerable().Reverse().Take(30).Reverse());
            }
            Debug.Log("[Dashboard] " + message);
        }

        // ═══════════════════════════════════════════════════════════════
        //                     UI CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("ModernDashboardCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            var monitor = GameObject.Find("DashboardMonitorBase");
            if (monitor != null)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                var rect = canvasGO.GetComponent<RectTransform>();
                rect.SetParent(monitor.transform, false);
                rect.localScale = new Vector3(0.95f / 1920f, 0.95f / 1080f, 1f);
                rect.sizeDelta = new Vector3(1920, 1080);
                rect.localPosition = new Vector3(0, 0, -0.51f);
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        private void CreateHeader()
        {
            var header = CreateUIPanel("Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 80), new Vector2(0, -40), COL_BG);
            var vlg = header.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 8, 4);
            vlg.spacing = 2;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;

            // Title row
            var titleRow = CreateRow(header);
            var title = MkLabel(titleRow, "<b>HVOF Laser Lab</b>", 26, COL_PRIMARY);
            var subtitle = MkLabel(titleRow, "Virtual Control Dashboard", 26, new Color(COL_TEXT.r, COL_TEXT.g, COL_TEXT.b, 0.8f));
            subtitle.fontStyle = FontStyles.Normal;

            // Subtitle line and App Description port from Python Dashboard
            MkLabel(header, "Virtual digital twin of a laser treatment lab. Control the X-Y table, gas system, and laser in a safe simulation environment.", 13, COL_MUTED);
            var archLabel = MkLabel(header, "Design Patterns: Clean Architecture – CIA Triad – STRIDE", 11, new Color(COL_PRIMARY.r, COL_PRIMARY.g, COL_PRIMARY.b, 0.7f));
            archLabel.fontStyle = FontStyles.Italic;
        }

        private void CreateWorkflowStepper()
        {
            var stepper = CreateUIPanel("Stepper", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 36), new Vector2(0, -82), COL_BG);
            var hlg = stepper.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 24, 4, 4);
            hlg.spacing = 6;
            hlg.childControlWidth = false; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;

            _step1Bg = CreateStepChip(stepper, "1", "Lock Safety");
            MkLabel(stepper, "→", 16, COL_BORDER).GetComponent<LayoutElement>().preferredWidth = 20;
            _step2Bg = CreateStepChip(stepper, "2", "Start Gas");
            MkLabel(stepper, "→", 16, COL_BORDER).GetComponent<LayoutElement>().preferredWidth = 20;
            _step3Bg = CreateStepChip(stepper, "3", "Fire Laser");
            MkLabel(stepper, "→", 16, COL_BORDER).GetComponent<LayoutElement>().preferredWidth = 20;
            _step4Bg = CreateStepChip(stepper, "4", "Run Scan");
        }

        private Image CreateStepChip(GameObject parent, string num, string label)
        {
            var chip = new GameObject("Step" + num);
            chip.transform.SetParent(parent.transform, false);
            var chipImg = chip.AddComponent<Image>();
            chipImg.color = new Color(COL_PRIMARY.r, COL_PRIMARY.g, COL_PRIMARY.b, 0.06f);
            var le = chip.AddComponent<LayoutElement>();
            le.preferredWidth = 130; le.preferredHeight = 28;
            var chlg = chip.AddComponent<HorizontalLayoutGroup>();
            chlg.padding = new RectOffset(8, 12, 2, 2);
            chlg.spacing = 8;
            chlg.childControlWidth = false; chlg.childForceExpandWidth = false;
            chlg.childAlignment = TextAnchor.MiddleLeft;

            // Circle
            var circle = new GameObject("Circle");
            circle.transform.SetParent(chip.transform, false);
            var circleImg = circle.AddComponent<Image>();
            circleImg.color = COL_BORDER;
            var cle = circle.AddComponent<LayoutElement>();
            cle.preferredWidth = 22; cle.preferredHeight = 22;
            MkLabel(circle, $"<b>{num}</b>", 12, Color.white, TextAlignmentOptions.Center);

            MkLabel(chip, $"<b>{label}</b>", 12, COL_MUTED);

            return circleImg;
        }

        private void CreateTopTabBar()
        {
            var tabContainer = CreateUIPanel("TabBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 48), new Vector2(0, -120), COL_CARD);
            var hlg = tabContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 1; hlg.childControlWidth = true; hlg.childForceExpandWidth = true;

            string[] names = { "SET Setup", "X-Y Table", "Laser & Gas", "Safety", "Cost" };
            for (int i = 0; i < TAB_COUNT; i++)
            {
                int idx = i;
                var btn = CreateStyledButton(tabContainer, names[i], COL_CARD, COL_TEXT, () => ShowTab(idx), 48);
                _tabButtons[i] = btn;
                _tabBtnImages[i] = btn.GetComponent<Image>();
            }
        }

        private void ShowTab(int index)
        {
            for (int i = 0; i < TAB_COUNT; i++)
            {
                if (_tabPanels[i] != null) _tabPanels[i].SetActive(i == index);
                if (_tabBtnImages[i] != null) _tabBtnImages[i].color = (i == index) ? COL_PRIMARY : COL_CARD;
            }
        }

        // ─── TAB: Setup (Equipment Configuration) ────────────────────

        private GameObject CreateSetupTab()
        {
            var panel = CreateTabPanel("SetupTab");
            var content = CreateScrollContent(panel);

            // Intro
            var introCard = CreateCard(content, "");
            MkLabel(introCard, "Configure your laser type, shielding gas, processing chamber, and target material before running the simulation. Changes update the 3D lab scene in real time.", 13, COL_MUTED);

            // ── Laser Type Picker ──────────────────────────────────────
            var laserCard = CreateCard(content, "Laser Type");
            MkLabel(laserCard, "Select the laser source. Each type has different wavelength, power range, and beam quality.", 11, COL_MUTED);

            var laserGrid = CreateRow(laserCard);
            var laserTypes = new[] { LaserType.YtterbiumFiber, LaserType.CO2, LaserType.NdYAG, LaserType.Diode };
            for (int i = 0; i < laserTypes.Length; i++)
            {
                int idx = i;
                var lt = laserTypes[i];
                var spec = LaserTypeSpecs.GetSpec(lt);
                var cardGO = new GameObject("LaserCard");
                cardGO.transform.SetParent(laserGrid.transform, false);
                var cardImg = cardGO.AddComponent<Image>();
                cardImg.color = (lt == SelectedLaserType) ? COL_PRIMARY : COL_CARD;
                _laserCardBgs[idx] = cardImg;
                var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 8, 8);
                vlg.spacing = 3;
                vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
                cardGO.AddComponent<LayoutElement>().flexibleWidth = 1;

                MkLabel(cardGO, $"<b>{spec.ShortName}</b>", 13, Color.white);
                MkLabel(cardGO, $"{spec.WavelengthNm} nm", 11, COL_MUTED);
                MkLabel(cardGO, $"{spec.MinPowerW}–{spec.MaxPowerW/1000f:F0}kW", 10, COL_MUTED);
                MkLabel(cardGO, $"M²={spec.BeamQualityM2:F1}", 10, COL_ACCENT);

                var btn = cardGO.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    SelectLaserType(lt, idx);
                });
            }

            // Beam Profile Selector
            var profileRow = CreateRow(laserCard);
            MkLabel(profileRow, "<b>BEAM PROFILE</b>", 11, COL_MUTED).GetComponent<LayoutElement>().preferredWidth = 120;
            CreateStyledButton(profileRow, "(*) Gaussian (TEM00)", 
                SelectedBeamProfile == BeamProfile.Gaussian ? COL_PRIMARY : COL_CARD, 
                Color.white, () => { SelectBeamProfile(BeamProfile.Gaussian); }, 28);
            CreateStyledButton(profileRow, "(=) Top-Hat (Flat)", 
                SelectedBeamProfile == BeamProfile.TopHat ? COL_PRIMARY : COL_CARD, 
                Color.white, () => { SelectBeamProfile(BeamProfile.TopHat); }, 28);

            // Laser Info Card
            _laserInfoText = MkLabel(laserCard, "", 11, COL_MUTED);
            _laserInfoText.enableWordWrapping = true;
            UpdateLaserInfoText();

            // ── Gas Mode Selector ─────────────────────────────────────
            var gasCard = CreateCard(content, "Gas Configuration");
            MkLabel(gasCard, "Select shielding gas or disable gas for open-air processing.", 11, COL_MUTED);

            var gasGrid = CreateRow(gasCard);
            var gasTypes = new[] { GasType.None, GasType.Argon, GasType.Nitrogen, GasType.Helium, GasType.ArgonHydrogenMix };
            string[] gasLabels = { "No Gas", "Argon", "N₂", "He", "Ar+H₂" };
            for (int i = 0; i < gasTypes.Length; i++)
            {
                int idx = i;
                var gt = gasTypes[i];
                var cardGO = new GameObject("GasCard");
                cardGO.transform.SetParent(gasGrid.transform, false);
                var cardImg = cardGO.AddComponent<Image>();
                cardImg.color = (gt == SelectedGasType) ? COL_PRIMARY : COL_CARD;
                _gasCardBgs[idx] = cardImg;
                var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 6, 6);
                vlg.spacing = 2;
                vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
                cardGO.AddComponent<LayoutElement>().flexibleWidth = 1;

                var spec = GasTypeSpecs.GetSpec(gt);
                MkLabel(cardGO, $"<b>{gasLabels[idx]}</b>", 12, Color.white);
                if (spec.MaxFlowLMin > 0)
                    MkLabel(cardGO, $"0–{spec.MaxFlowLMin} L/min", 10, COL_MUTED);
                else
                    MkLabel(cardGO, "No flow", 10, COL_WARNING);

                var btn = cardGO.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    SelectGasType(gt, idx);
                });
            }

            _gasWarningText = MkLabel(gasCard, "", 11, COL_WARNING);
            _gasWarningText.enableWordWrapping = true;
            UpdateGasWarningText();

            // ── Chamber Selector ──────────────────────────────────────
            var chamberCard = CreateCard(content, "Processing Chamber");
            MkLabel(chamberCard, "Select the processing enclosure. Affects travel limits and gas availability.", 11, COL_MUTED);

            var chamberGrid = CreateRow(chamberCard);
            var chamberTypes = new[] { ChamberType.StandardAluminium, ChamberType.LargeSteel, ChamberType.OpenAir, ChamberType.Vacuum, ChamberType.Custom };
            string[] chamberLabels = { "Standard Al", "Large Steel", "Open Air", "Vacuum", "Custom" };
            for (int i = 0; i < chamberTypes.Length; i++)
            {
                int idx = i;
                var ct = chamberTypes[i];
                var spec = ChamberSpecs.GetSpec(ct);
                var cardGO = new GameObject("ChamberCard");
                cardGO.transform.SetParent(chamberGrid.transform, false);
                var cardImg = cardGO.AddComponent<Image>();
                cardImg.color = (ct == SelectedChamberType) ? COL_PRIMARY : COL_CARD;
                _chamberCardBgs[idx] = cardImg;
                var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 6, 6);
                vlg.spacing = 2;
                vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
                cardGO.AddComponent<LayoutElement>().flexibleWidth = 1;

                MkLabel(cardGO, $"<b>{chamberLabels[idx]}</b>", 12, Color.white);
                if (spec.TravelXMm > 0)
                    MkLabel(cardGO, $"{spec.TravelXMm:F0}×{spec.TravelYMm:F0} mm", 10, COL_MUTED);
                else
                    MkLabel(cardGO, "Unlimited", 10, COL_MUTED);

                var btn = cardGO.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    SelectChamberType(ct, idx);
                });
            }

            _chamberInfoText = MkLabel(chamberCard, "", 11, COL_MUTED);
            _chamberInfoText.enableWordWrapping = true;
            UpdateChamberInfoText();

            // ── Material Library ──────────────────────────────────────
            var matCard = CreateCard(content, "Material Library");
            MkLabel(matCard, "Select a pre-loaded coating material or create a custom one. All properties can be edited.", 11, COL_MUTED);

            var matGrid = CreateRow(matCard);
            var presets = MaterialPresetLibrary.GetAll();
            for (int i = 0; i < presets.Count; i++)
            {
                int idx = i;
                var preset = presets[i];
                var cardGO = new GameObject("MatCard");
                cardGO.transform.SetParent(matGrid.transform, false);
                var cardImg = cardGO.AddComponent<Image>();
                bool isSelected = (SelectedMaterial != null && SelectedMaterial.Name == preset.Name);
                cardImg.color = isSelected ? COL_PRIMARY : COL_CARD;
                _materialCardBgs[idx] = cardImg;
                var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 6, 6);
                vlg.spacing = 2;
                vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
                cardGO.AddComponent<LayoutElement>().flexibleWidth = 1;

                // Color swatch
                var swatchGO = new GameObject("Swatch");
                swatchGO.transform.SetParent(cardGO.transform, false);
                var swatchImg = swatchGO.AddComponent<Image>();
                Color swatchCol; ColorUtility.TryParseHtmlString(preset.ColorHex, out swatchCol);
                swatchImg.color = swatchCol;
                swatchGO.AddComponent<LayoutElement>().preferredHeight = 6;

                MkLabel(cardGO, $"<b>{preset.Name}</b>", 10, Color.white);
                MkLabel(cardGO, $"Tₘ={preset.TMelt:F0}°C", 9, COL_MUTED);
                MkLabel(cardGO, $"{preset.HardnessRange}", 9, COL_ACCENT);

                var btn = cardGO.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    SelectMaterial(preset, idx);
                });
            }

            _materialInfoText = MkLabel(matCard, "", 11, COL_MUTED);
            _materialInfoText.enableWordWrapping = true;
            UpdateMaterialInfoText();

            // ── Derived Values Display ────────────────────────────────
            var derivedCard = CreateCard(content, "Derived Values (live)");
            _derivedValuesText = MkLabel(derivedCard, "", 12, COL_TEXT);
            _derivedValuesText.enableWordWrapping = true;

            return panel;
        }

        // ── Setup Tab Selection Handlers ─────────────────────────────

        private void SelectLaserType(LaserType type, int cardIndex)
        {
            SelectedLaserType = type;
            var spec = LaserTypeSpecs.GetSpec(type);
            SelectedBeamProfile = spec.DefaultProfile;
            for (int i = 0; i < _laserCardBgs.Length; i++)
                if (_laserCardBgs[i] != null) _laserCardBgs[i].color = (i == cardIndex) ? COL_PRIMARY : COL_CARD;
            UpdateLaserInfoText();
            UpdateDerivedValues();
            LogToMonitor($"Laser type changed to {spec.Name} ({spec.WavelengthNm} nm)");
            OnLaserTypeChanged?.Invoke(type);
        }

        private void SelectBeamProfile(BeamProfile profile)
        {
            SelectedBeamProfile = profile;
            LogToMonitor($"Beam profile changed to {profile}");
            OnBeamProfileChanged?.Invoke(profile);
        }

        private void SelectGasType(GasType type, int cardIndex)
        {
            SelectedGasType = type;
            for (int i = 0; i < _gasCardBgs.Length; i++)
                if (_gasCardBgs[i] != null) _gasCardBgs[i].color = (i == cardIndex) ? COL_PRIMARY : COL_CARD;
            UpdateGasWarningText();
            var spec = GasTypeSpecs.GetSpec(type);
            LogToMonitor($"Gas mode changed to {spec.DisplayName}");
            OnGasTypeChanged?.Invoke(type);
        }

        private void SelectChamberType(ChamberType type, int cardIndex)
        {
            SelectedChamberType = type;
            for (int i = 0; i < _chamberCardBgs.Length; i++)
                if (_chamberCardBgs[i] != null) _chamberCardBgs[i].color = (i == cardIndex) ? COL_PRIMARY : COL_CARD;
            UpdateChamberInfoText();

            var spec = ChamberSpecs.GetSpec(type);
            // Force gas to None when Open Air is selected
            if (!spec.SupportsGas && SelectedGasType != GasType.None)
            {
                SelectGasType(GasType.None, 0);
                LogToMonitor("Gas forced to 'No Gas' — chamber does not support gas");
            }
            LogToMonitor($"Chamber changed to {spec.Name}");
            OnChamberTypeChanged?.Invoke(type);
        }

        private void SelectMaterial(MaterialPreset preset, int cardIndex)
        {
            try
            {
                SelectedMaterial = preset.ToDomainMaterial();
                for (int i = 0; i < _materialCardBgs.Length; i++)
                    if (_materialCardBgs[i] != null) _materialCardBgs[i].color = (i == cardIndex) ? COL_PRIMARY : COL_CARD;
                UpdateMaterialInfoText();
                UpdateDerivedValues();
                LogToMonitor($"Material selected: {preset.Name}");
                OnMaterialChanged?.Invoke(SelectedMaterial);
            }
            catch (Exception e) { LogToMonitor($"<color=#FF5555>Material error: {e.Message}</color>"); }
        }

        // ── Setup Tab Info Updaters ──────────────────────────────────

        private void UpdateLaserInfoText()
        {
            if (_laserInfoText == null) return;
            var spec = LaserTypeSpecs.GetSpec(SelectedLaserType);
            _laserInfoText.text = $"<color=#8b5cf6><b>{spec.Name}</b></color>  |  wavelen={spec.WavelengthNm} nm  |  M2={spec.BeamQualityM2:F1}  |  Eff={spec.EfficiencyPct}%\n" +
                $"<color=#10b981>[OK] {spec.Pros}</color>\n" +
                $"<color=#ef4444>[FAIL] {spec.Cons}</color>\n" +
                $"Best for: {spec.BestFor}";
        }

        private void UpdateGasWarningText()
        {
            if (_gasWarningText == null) return;
            var spec = GasTypeSpecs.GetSpec(SelectedGasType);
            _gasWarningText.text = string.IsNullOrEmpty(spec.WarningNote) ? "" : spec.WarningNote;
        }

        private void UpdateChamberInfoText()
        {
            if (_chamberInfoText == null) return;
            var spec = ChamberSpecs.GetSpec(SelectedChamberType);
            _chamberInfoText.text = $"<b>{spec.Name}</b>  |  {spec.MaterialName}  |  Travel: {spec.TravelXMm:F0}×{spec.TravelYMm:F0} mm\n{spec.Description}";
        }

        private void UpdateMaterialInfoText()
        {
            if (_materialInfoText == null) return;
            if (SelectedMaterial == null) { _materialInfoText.text = "No material selected"; return; }
            var m = SelectedMaterial;
            _materialInfoText.text = $"<b>{m.Name}</b>  |  abs={m.Absorption:F2}  k={m.ThermalConductivity} W/(m-K)  rho={m.Density} kg/m3  cp={m.SpecificHeat} J/(kg-K)\n" +
                $"T_melt={m.TMelt:F0} C  T_vap={m.TVaporization:F0} C  kappa={m.ThermalDiffusivity:E2} m2/s";
        }

        private void UpdateDerivedValues()
        {
            if (_derivedValuesText == null) return;
            var spec = LaserTypeSpecs.GetSpec(SelectedLaserType);
            double spotRadiusM = (SpotSizeMm / 1000.0) / 2.0;
            double power = 500; // Default display power
            double I0 = 2.0 * power / (System.Math.PI * spotRadiusM * spotRadiusM);
            double interactionTime = (SpotSizeMm / 1000.0) / (ScanSpeedMmS / 1000.0);
            double lineSpacing = SpotSizeMm * (1.0 - OverlapPct / 100.0);
            double zR = System.Math.PI * spotRadiusM * spotRadiusM / spec.WavelengthM;

            _derivedValuesText.text =
                $"<b>Power Density:</b> I0 = 2P/(pi*w0^2) = <color=#3b82f6>{I0:E2} W/m2</color>  (at 500 W)\n" +
                $"<b>Interaction Time:</b> t = d/v = <color=#3b82f6>{interactionTime * 1000:F2} ms</color>\n" +
                $"<b>Line Spacing:</b> s = d*(1-overlap) = <color=#3b82f6>{lineSpacing:F2} mm</color>\n" +
                $"<b>Rayleigh Range:</b> z_R = pi*w0^2/lambda = <color=#3b82f6>{zR * 1000:F2} mm</color>\n" +
                $"<b>Diffusivity:</b> kappa = k/(rho*cp) = <color=#3b82f6>{(SelectedMaterial != null ? SelectedMaterial.ThermalDiffusivity.ToString("E2") : "—")} m2/s</color>";
        }

        // ─── TAB: X-Y Table ──────────────────────────────────────────

        private GameObject CreateXYTableTab()
        {
            var panel = CreateTabPanel("XYTab");
            var content = CreateScrollContent(panel);

            // Intro
            var introCard = CreateCard(content, "");
            MkLabel(introCard, "Control the motorized positioning table that moves HVOF-coated samples beneath the laser beam. Set scan parameters, preview the path, and export G-Code for the CNC motion controller.", 13, COL_MUTED);

            // Columns
            var cols = CreateRow(content);
            var leftCol = CreateColumn(cols, 1f);
            var rightCol = CreateColumn(cols, 2f);

            // Position Control
            var posCard = CreateCard(leftCol, "Position Control");
            var btnRow = CreateRow(posCard);
            CreateStyledButton(btnRow, "Home", COL_PRIMARY, Color.white, () => { LogToMonitor("Command: Home Table"); OnHomeClicked?.Invoke(); });
            CreateStyledButton(btnRow, "Run Scan", COL_SUCCESS, Color.white, () => { LogToMonitor("Starting Scan..."); OnRunSimulationClicked?.Invoke(); });
            CreateStyledButton(btnRow, "Reset Lab", COL_DANGER, Color.white, () => { LogToMonitor("Resetting Lab State..."); _isScanComplete = false; if(_simResultText != null) _simResultText.text = "Click 'Run Scan' to visualize"; OnResetLabClicked?.Invoke(); });
            CreateStyledButton(btnRow, "G-Code", COL_ACCENT, Color.white, () => { LogToMonitor("Exported G-Code"); });
            MkLabel(posCard, "Home = reset origin. Run Scan = runs laser path & digital twin simulation. Reset Lab = stops all & clears state.", 11, COL_MUTED);

            // Scan Parameters
            var paramCard = CreateCard(leftCol, "Scan Parameters");
            string[] patterns = { "Raster (back-and-forth)", "Spiral (center out)", "Linear (single pass)" };
            int patIdx = 0;
            var patLabel = MkLabel(paramCard, "<b>Raster (back-and-forth)</b>", 13, Color.white);
            CreateInfoRow(paramCard, "PATTERN", "Raster = rows. Spiral = circular. Linear = single pass.");
            CreateStyledButton(paramCard, "Change Pattern", COL_CARD, COL_PRIMARY, () => { patIdx = (patIdx + 1) % patterns.Length; patLabel.text = $"<b>{patterns[patIdx]}</b>"; }, 28);

            CreateInfoRow(paramCard, "SPEED (MM/S)", "Table speed. Slower = hotter. Typical: 5-50 mm/s.");
            var speedVal = MkLabel(paramCard, "<b>12</b>", 13, Color.white);
            CreateSlider(paramCard, 1, 50, v => { float val = Mathf.Round(v); speedVal.text = $"<b>{val:F0}</b>"; ScanSpeedMmS = val; }).value = 12;

            CreateInfoRow(paramCard, "SCAN WIDTH (MM)", "Horizontal scan extent on sample.");
            var wVal = MkLabel(paramCard, "<b>50</b>", 13, Color.white);
            CreateSlider(paramCard, 10, 200, v => { float val = Mathf.Round(v); wVal.text = $"<b>{val:F0}</b>"; ScanWidthMm = val; }).value = 50;

            CreateInfoRow(paramCard, "SCAN HEIGHT (MM)", "Vertical scan extent on sample.");
            var hVal = MkLabel(paramCard, "<b>50</b>", 13, Color.white);
            CreateSlider(paramCard, 10, 200, v => { float val = Mathf.Round(v); hVal.text = $"<b>{val:F0}</b>"; ScanHeightMm = val; }).value = 50;

            CreateInfoRow(paramCard, "SPOT SIZE (MM)", "Beam diameter. Smaller = concentrated.");
            var spVal = MkLabel(paramCard, "<b>2.5</b>", 13, Color.white);
            var spotSlider = CreateSlider(paramCard, 0.5f, 5f, v => { 
                float val = Mathf.Round(v * 10f) / 10f; 
                spVal.text = $"<b>{val:F1}</b>"; 
                SpotSizeMm = val; 
            });
            spotSlider.value = 2.5f;

            CreateInfoRow(paramCard, "OVERLAP (%)", "Pass overlap. 50% typical.");
            var ovVal = MkLabel(paramCard, "<b>50</b>", 13, Color.white);
            CreateSlider(paramCard, 0, 90, v => { float val = Mathf.Round(v); ovVal.text = $"<b>{val:F0}</b>"; OverlapPct = val; }).value = 50;

            // Scan Path Preview
            var prevCard = CreateCard(rightCol, "Scan Path Preview");
            var bbox = CreateBlackBox(prevCard, 350);
            _simResultText = MkLabel(bbox, "Click 'Run Scan' to visualize", 14, COL_MUTED, TextAlignmentOptions.Center);
            var simRect = _simResultText.GetComponent<RectTransform>();
            simRect.anchorMin = Vector2.zero; simRect.anchorMax = Vector2.one;
            simRect.sizeDelta = Vector2.zero; simRect.anchoredPosition = Vector2.zero;

            // Indicators row (Moves from right col to its own block, or stays in right col depending on original structure)
            var indCard = CreateCard(rightCol, "");
            var indRow = CreateRow(indCard);
            _posXText = CreateIndicator(indRow, "Position X", "0.00 mm");
            _posYText = CreateIndicator(indRow, "Position Y", "0.00 mm");
            _tableStateText = CreateIndicator(indRow, "State", "Idle");
            _scanPointsText = CreateIndicator(indRow, "Points", "0");
            // G-Code block moved to bottom (spanning full width)
            var gcCard = CreateCard(content, "Generated G-Code");
            MkLabel(gcCard, "G-Code is the standard language for CNC machines. Copy this output and send it to a motion controller.", 11, COL_MUTED);
            var gcBox = CreateBlackBox(gcCard, 80);
            MkLabel(gcBox, " Click 'Export G-Code' to generate...", 13, COL_MUTED);

            return panel;
        }

        // ─── TAB: Laser & Gas ────────────────────────────────────────

        private GameObject CreateLaserGasTab()
        {
            var panel = CreateTabPanel("LaserGasTab");
            var content = CreateScrollContent(panel);

            // Intro — dynamic based on selected laser type
            var introCard = CreateCard(content, "");
            var laserSpec = LaserTypeSpecs.GetSpec(SelectedLaserType);
            MkLabel(introCard, $"Control the {laserSpec.Name}. Workflow: Set Power → Arm → Fire. The laser will not fire unless all safety interlocks are locked.", 13, COL_MUTED);

            var cols = CreateRow(content);
            var leftCol = CreateColumn(cols, 1f);
            var rightCol = CreateColumn(cols, 1f);

            // Laser Control
            var laserCard = CreateCard(leftCol, "Laser Control");
            CreateInfoRow(laserCard, "POWER (W)", $"Drag slider to set laser power. Range: {laserSpec.MinPowerW}–{laserSpec.MaxPowerW} W for {laserSpec.ShortName}.");
            _powerLabel = MkLabel(laserCard, "Power: 0 W", 16, Color.white);
            float maxPowerSlider = Mathf.Min((float)laserSpec.MaxPowerW, 5000f); // Cap slider at 5kW for usability
            CreateSlider(laserCard, 0, maxPowerSlider, v => { _powerLabel.text = $"Power: {v:F0} W"; OnPowerChanged?.Invoke(v); });

            var lBtnRow = CreateRow(laserCard);
            CreateStyledButton(lBtnRow, "Set Power", COL_PRIMARY, Color.white, () => LogToMonitor("Power set"));
            _armButton = CreateStyledButton(lBtnRow, "Arm", COL_WARNING, Color.black, () => { LogToMonitor("Command: Arm Laser"); OnArmClicked?.Invoke(); });
            _fireButton = CreateStyledButton(lBtnRow, "Fire", COL_DANGER, Color.white, () => { LogToMonitor("Command: FIRE LASER"); OnFireClicked?.Invoke(); });
            CreateStyledButton(lBtnRow, "Stop", COL_PRIMARY, Color.white, () => { LogToMonitor("Command: Stop Laser"); OnStopClicked?.Invoke(); });
            MkLabel(laserCard, "Set Power = apply slider. Arm = prepare (requires interlocks). Fire = emit beam. Stop = cut emission.", 11, COL_MUTED);

            // Pre-run warning
            var warnCard = CreateCard(leftCol, "");
            var warnText = MkLabel(warnCard, "", 12, COL_WARNING);
            warnText.enableWordWrapping = true;
            // Will be populated dynamically via UpdateDisplay

            // Laser Status
            var lStatCard = CreateCard(leftCol, "Laser Status");
            var lIndRow = CreateRow(lStatCard);
            _laserStateText = CreateIndicator(lIndRow, "State", "Off");
            _laserPowerValText = CreateIndicator(lIndRow, "Power (W)", "0");
            _laserPowerPctText = CreateIndicator(lIndRow, "Power (%)", "0%");
            _laserWlText = CreateIndicator(lIndRow, "Wavelength", $"{laserSpec.WavelengthNm} nm");

            // Gas Controls — conditionally visible
            _gasControlsPanel = new GameObject("GasControls");
            _gasControlsPanel.transform.SetParent(rightCol.transform, false);
            var gasVLG = _gasControlsPanel.AddComponent<VerticalLayoutGroup>();
            gasVLG.spacing = 16; gasVLG.childControlWidth = true; gasVLG.childForceExpandWidth = true;
            _gasControlsPanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var gasSpec = GasTypeSpecs.GetSpec(SelectedGasType);
            var gasCard = CreateCard(_gasControlsPanel, $"Shielding Gas ({gasSpec.DisplayName})");
            MkLabel(gasCard, $"{gasSpec.Description}. Purge the chamber before firing to flush out oxygen.", 11, COL_MUTED);

            CreateInfoRow(gasCard, "FLOW RATE (L/MIN)", $"Flow rate. {gasSpec.RecommendedPurgeFlowLMin:F0} L/min recommended for purging.");
            _gasFlowLabel = MkLabel(gasCard, "<b>0</b>", 13, Color.white);
            float gasMax = gasSpec.MaxFlowLMin > 0 ? (float)gasSpec.MaxFlowLMin : 20f;
            _gasFlowSlider = CreateSlider(gasCard, 0, gasMax, v => { _gasFlowLabel.text = $"<b>{v:F1}</b>"; _gasSliderValue = v; });
            _gasFlowSlider.value = 0;

            var gBtnRow = CreateRow(gasCard);
            CreateStyledButton(gBtnRow, "Set Flow", COL_PRIMARY, Color.white, () => { LogToMonitor($"Gas flow set to {_gasSliderValue:F1} L/min"); OnSetGasFlowClicked?.Invoke(_gasSliderValue); });
            CreateStyledButton(gBtnRow, "Purge", COL_WARNING, Color.white, () => { LogToMonitor($"Command: Purge {gasSpec.DisplayName}"); OnPurgeClicked?.Invoke(); });
            CreateStyledButton(gBtnRow, "Stop Gas", COL_PRIMARY, Color.white, () => { LogToMonitor("Command: Stop Gas"); OnStopGasClicked?.Invoke(); });

            // Gas indicators
            var gIndCard = CreateCard(_gasControlsPanel, "");
            var gIndRow = CreateRow(gIndCard);
            _gasStateText = CreateIndicator(gIndRow, "Gas State", "Closed");
            _gasO2Text = CreateIndicator(gIndRow, "O₂ (ppm)", "209500");
            _gasSupplyText = CreateIndicator(gIndRow, "Supply (%)", "100%");
            _gasSafeText = CreateIndicator(gIndRow, "Atmosphere", "UNSAFE");

            // Hide gas controls if No Gas mode
            _gasControlsPanel.SetActive(SelectedGasType != GasType.None);
            if (SelectedGasType == GasType.None)
            {
                var noGasCard = CreateCard(rightCol, "");
                MkLabel(noGasCard, "<color=#F59E0B><b>[!] No Gas Mode</b></color>\nProcessing in open air. Oxidation will occur.", 14, COL_WARNING).enableWordWrapping = true;
            }

            return panel;
        }

        // ─── TAB: Safety ─────────────────────────────────────────────

        private GameObject CreateSafetyTab()
        {
            var panel = CreateTabPanel("SafetyTab");
            var content = CreateScrollContent(panel);

            var introCard = CreateCard(content, "");
            MkLabel(introCard, "Safety interlocks prevent the laser from firing when the lab is not secure. Both door and chamber must be LOCKED before arming. E-Stop immediately cuts all power.", 13, COL_MUTED);

            // Interlocks
            var cols = CreateRow(content);
            var leftCol = CreateColumn(cols, 1f);
            var rightCol = CreateColumn(cols, 1f);

            var intCard = CreateCard(leftCol, "Safety Interlocks");
            var doorRow = CreateRow(intCard);
            CreateStyledButton(doorRow, "Lock Door", COL_SUCCESS, Color.white, () => { LogToMonitor("Safety: Door LOCKED"); OnLockDoorClicked?.Invoke(); });
            CreateStyledButton(doorRow, "Unlock Door", COL_WARNING, Color.white, () => { LogToMonitor("Safety: Door UNLOCKED"); OnUnlockDoorClicked?.Invoke(); });
            MkLabel(intCard, "Door interlock: prevents laser when lab door is open.", 11, COL_MUTED);

            var chamRow = CreateRow(intCard);
            CreateStyledButton(chamRow, "Lock Chamber", COL_SUCCESS, Color.white, () => { LogToMonitor("Safety: Chamber LOCKED"); OnLockChamberClicked?.Invoke(); });
            CreateStyledButton(chamRow, "Unlock Chamber", COL_WARNING, Color.white, () => { LogToMonitor("Safety: Chamber UNLOCKED"); OnUnlockChamberClicked?.Invoke(); });
            MkLabel(intCard, "Chamber interlock: prevents laser when processing chamber lid is open.", 11, COL_MUTED);

            CreateStyledButton(intCard, "E-STOP", COL_DANGER, Color.white, () => { LogToMonitor("<color=#FF0000><b>EMERGENCY STOP!</b></color>"); OnEStopClicked?.Invoke(); }, 50);
            CreateStyledButton(intCard, "Reset E-Stop", COL_CARD, Color.white, () => { LogToMonitor("Safety: E-Stop reset"); OnResetEStopClicked?.Invoke(); });
            MkLabel(intCard, "E-Stop: immediately shuts all systems. Must be reset to resume.", 11, COL_MUTED);

            // Safety indicators
            var sIndCard = CreateCard(rightCol, "Status");
            _safetyDoorText = CreateIndicator(sIndCard, "Door", "Unlocked");
            _safetyChamberText = CreateIndicator(sIndCard, "Chamber", "Unlocked");
            _safetyEstopText = CreateIndicator(sIndCard, "E-Stop", "OK");
            _safetyWarningText = CreateIndicator(sIndCard, "Warning Light", "Off");
            _safetyAllClearText = CreateIndicator(sIndCard, "System", "NOT READY");

            // Activity Log
            var logCard = CreateCard(content, "Activity Log");
            MkLabel(logCard, "All actions are recorded for traceability.", 11, COL_MUTED);
            var logBox = CreateBlackBox(logCard, 200);
            _activityLogText = MkLabel(logBox, "", 11, COL_MUTED);
            _activityLogText.enableWordWrapping = true;
            var lr = _activityLogText.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.sizeDelta = new Vector2(-16, -8); lr.anchoredPosition = Vector2.zero;

            return panel;
        }

        // ─── TAB: Cost ───────────────────────────────────────────────

        private GameObject CreateCostTab()
        {
            var panel = CreateTabPanel("CostTab");
            var content = CreateScrollContent(panel);

            var introCard = CreateCard(content, "");
            MkLabel(introCard, "Bill of materials for building this laser treatment laboratory. Shows every piece of equipment needed with real-world European market pricing.", 13, COL_MUTED);

            if (_costCalculator != null)
            {
                var catalog = _costCalculator.GetCatalog();
                double total = _costCalculator.CalculateTotal(catalog);
                var categories = catalog.Select(e => e.Category).Distinct().ToList();

                // Summary card
                var sumCard = CreateCard(content, "Budget Summary");
                var sumRow = CreateRow(sumCard);
                CreateIndicator(sumRow, "TOTAL EQUIPMENT", $"<color=#3b82f6>€{total:N0}</color>", 28);
                CreateIndicator(sumRow, "CATEGORIES", $"<color=#8b5cf6>{categories.Count}</color>", 28);
                CreateIndicator(sumRow, "LINE ITEMS", $"<color=#10b981>{catalog.Count}</color>", 28);

                // Equipment table per category
                var tableCard = CreateCard(content, "Equipment Catalog");
                // Header
                var hdrRow = CreateRow(tableCard);
                MkLabel(hdrRow, "<b>Equipment</b>", 12, COL_PRIMARY);
                MkLabel(hdrRow, "<b>Description</b>", 12, COL_PRIMARY);
                MkLabel(hdrRow, "<b>Qty</b>", 12, COL_PRIMARY, TextAlignmentOptions.Center);
                MkLabel(hdrRow, "<b>Unit Cost</b>", 12, COL_PRIMARY, TextAlignmentOptions.Right);
                MkLabel(hdrRow, "<b>Total</b>", 12, COL_PRIMARY, TextAlignmentOptions.Right);
                CreateDivider(tableCard);

                foreach (var cat in categories)
                {
                    var items = _costCalculator.GetByCategory(cat);
                    foreach (var item in items)
                    {
                        var row = CreateRow(tableCard);
                        MkLabel(row, item.Name, 12, COL_TEXT);
                        MkLabel(row, item.Description, 11, COL_MUTED);
                        MkLabel(row, item.Quantity.ToString(), 12, COL_TEXT, TextAlignmentOptions.Center);
                        MkLabel(row, $"€{item.UnitCostEur:N0}", 12, COL_TEXT, TextAlignmentOptions.Right);
                        MkLabel(row, $"€{item.TotalCostEur:N0}", 12, COL_PRIMARY, TextAlignmentOptions.Right);
                    }
                    double catTotal = items.Sum(i => i.TotalCostEur);
                    var subRow = CreateRow(tableCard);
                    MkLabel(subRow, $"<b>Subtotal: {cat.ToUpper()}</b>", 12, COL_MUTED);
                    MkLabel(subRow, "", 12, COL_MUTED);
                    MkLabel(subRow, "", 12, COL_MUTED);
                    MkLabel(subRow, "", 12, COL_MUTED);
                    MkLabel(subRow, $"<b>€{catTotal:N0}</b>", 12, COL_WARNING, TextAlignmentOptions.Right);
                    CreateDivider(tableCard);
                }
                // Grand total
                var totalRow = CreateRow(tableCard);
                MkLabel(totalRow, "<b>GRAND TOTAL</b>", 15, COL_TEXT);
                MkLabel(totalRow, "", 12, COL_TEXT);
                MkLabel(totalRow, "", 12, COL_TEXT);
                MkLabel(totalRow, "", 12, COL_TEXT);
                MkLabel(totalRow, $"<b>€{total:N0}</b>", 16, COL_SUCCESS, TextAlignmentOptions.Right);
            }
            else
            {
                // Fallback if no catalog injected
                MkLabel(content, "Equipment catalog not available. Inject ICostCalculator via Initialize().", 14, COL_MUTED);
            }

            return panel;
        }

        private void CreateBottomStatusBar()
        {
            var bar = CreateUIPanel("StatusBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 36), new Vector2(0, 18), COL_CARD);
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 24, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            _statusBarText = MkLabel(bar, "Table: Idle   Laser: Off   Gas: Closed   Safety: <color=#EF4444>Not Ready</color>", 12, COL_MUTED);
        }

        // ═══════════════════════════════════════════════════════════════
        //                     UI UTILITIES
        // ═══════════════════════════════════════════════════════════════

        private GameObject CreateTabPanel(string name)
        {
            return CreateUIPanel(name, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -204), new Vector2(0, -84), COL_BG);
        }

        private GameObject CreateScrollContent(GameObject panel)
        {
            var scrollGo = new GameObject("ScrollRect");
            scrollGo.transform.SetParent(panel.transform, false);
            var srRect = scrollGo.AddComponent<RectTransform>();
            srRect.anchorMin = Vector2.zero; srRect.anchorMax = Vector2.one;
            srRect.sizeDelta = Vector2.zero; srRect.anchoredPosition = Vector2.zero;

            var vpGo = new GameObject("Viewport");
            vpGo.transform.SetParent(scrollGo.transform, false);
            var vpRect = vpGo.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero; vpRect.anchoredPosition = Vector2.zero;
            vpGo.AddComponent<Image>().color = COL_BG;
            vpGo.AddComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(vpGo.transform, false);
            var cRect = contentGo.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1); cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0.5f, 1); cRect.sizeDelta = Vector2.zero;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.AddComponent<ScrollRect>();
            sr.content = cRect; sr.viewport = vpRect;
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 50f;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 16;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            return contentGo;
        }

        private GameObject CreateUIPanel(string name, Vector2 min, Vector2 max, Vector2 size, Vector2 pos, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max;
            rect.sizeDelta = size; rect.anchoredPosition = pos;
            go.AddComponent<Image>().color = c;
            return go;
        }

        private GameObject CreateCard(GameObject parent, string titleStr, int minH = 0)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = COL_CARD;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.spacing = 10;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            if (minH > 0) { var le = go.AddComponent<LayoutElement>(); le.minHeight = minH; }
            if (!string.IsNullOrEmpty(titleStr)) { MkLabel(go, $"<b>{titleStr}</b>", 16, Color.white); CreateDivider(go); }
            return go;
        }

        private GameObject CreateRow(GameObject parent)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(parent.transform, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childControlWidth = true; hlg.childForceExpandWidth = true;
            hlg.childControlHeight = false; hlg.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private GameObject CreateColumn(GameObject parent, float flex)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(parent.transform, false);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.AddComponent<LayoutElement>().flexibleWidth = flex;
            return go;
        }

        private void CreateDivider(GameObject parent)
        {
            var go = new GameObject("Div");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
            go.AddComponent<Image>().color = COL_BORDER;
            go.AddComponent<LayoutElement>().preferredHeight = 1;
        }

        private GameObject CreateBlackBox(GameObject parent, int height)
        {
            var go = new GameObject("BlackBox");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = COL_BG;
            go.AddComponent<LayoutElement>().minHeight = height;
            return go;
        }

        private TextMeshProUGUI CreateIndicator(GameObject parent, string label, string defaultVal, int valSize = 18)
        {
            var col = new GameObject("Ind");
            col.transform.SetParent(parent.transform, false);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            col.AddComponent<LayoutElement>().flexibleWidth = 1;
            MkLabel(col, label.ToUpper(), 11, COL_MUTED);
            return MkLabel(col, $"<b>{defaultVal}</b>", valSize, COL_TEXT);
        }

        private TextMeshProUGUI MkLabel(GameObject parent, string txt, int size, Color c, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = txt; tmp.fontSize = size; tmp.color = c; tmp.alignment = align;
            tmp.richText = true; tmp.enableWordWrapping = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 8;
            return tmp;
        }

        private Button CreateStyledButton(GameObject parent, string txt, Color bg, Color textCol, System.Action onClick, float h = 35)
        {
            var go = new GameObject("Btn");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, h);
            go.AddComponent<Image>().color = bg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(new UnityEngine.Events.UnityAction(onClick));
            var colors = btn.colors;
            colors.highlightedColor = new Color(Mathf.Min(bg.r + 0.15f, 1), Mathf.Min(bg.g + 0.15f, 1), Mathf.Min(bg.b + 0.15f, 1));
            colors.pressedColor = new Color(Mathf.Max(bg.r - 0.15f, 0), Mathf.Max(bg.g - 0.15f, 0), Mathf.Max(bg.b - 0.15f, 0));
            btn.colors = colors;
            var label = MkLabel(go, $"<b>{txt}</b>", 13, textCol, TextAlignmentOptions.Center);
            var lr = label.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.sizeDelta = Vector2.zero; lr.anchoredPosition = Vector2.zero;
            go.AddComponent<LayoutElement>().minHeight = h;
            return btn;
        }

        private Slider CreateSlider(GameObject parent, float min, float max, System.Action<float> onChanged)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 20);
            go.AddComponent<LayoutElement>().minHeight = 20;

            var bg = new GameObject("Bg");
            bg.transform.SetParent(go.transform, false);
            var bgR = bg.AddComponent<RectTransform>();
            bgR.anchorMin = Vector2.zero; bgR.anchorMax = Vector2.one; bgR.sizeDelta = Vector2.zero;
            bg.AddComponent<Image>().color = COL_BORDER;

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(go.transform, false);
            var faR = fillArea.AddComponent<RectTransform>();
            faR.anchorMin = Vector2.zero; faR.anchorMax = Vector2.one; faR.sizeDelta = new Vector2(-10, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fR = fill.AddComponent<RectTransform>();
            fR.anchorMin = Vector2.zero; fR.anchorMax = Vector2.one; fR.sizeDelta = Vector2.zero;
            fill.AddComponent<Image>().color = COL_PRIMARY;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fR;
            slider.minValue = min; slider.maxValue = max;
            slider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(onChanged));
            return slider;
        }

        private void CreateInfoRow(GameObject parent, string label, string tooltip)
        {
            var row = new GameObject("Info");
            row.transform.SetParent(parent.transform, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childControlWidth = false; hlg.childForceExpandWidth = false;
            row.AddComponent<LayoutElement>().minHeight = 20;

            var lbl = MkLabel(row, $"<b>{label}</b>", 11, COL_MUTED);
            lbl.GetComponent<LayoutElement>().preferredWidth = 180;

            // Info toggle
            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(parent.transform, false);
            var tipTmp = tipGo.AddComponent<TextMeshProUGUI>();
            tipTmp.text = $"<i><color=#8b5cf6>{tooltip}</color></i>";
            tipTmp.fontSize = 10; tipTmp.color = COL_MUTED; tipTmp.richText = true;
            tipTmp.enableWordWrapping = true;
            tipGo.AddComponent<LayoutElement>().minHeight = 24;
            tipGo.SetActive(false);

            var iBtnGo = new GameObject("iBtn");
            iBtnGo.transform.SetParent(row.transform, false);
            iBtnGo.AddComponent<RectTransform>().sizeDelta = new Vector2(20, 20);
            iBtnGo.AddComponent<Image>().color = new Color(COL_ACCENT.r, COL_ACCENT.g, COL_ACCENT.b, 0.15f);
            var iBtn = iBtnGo.AddComponent<Button>();
            iBtnGo.AddComponent<LayoutElement>().preferredWidth = 20;
            var iBtnLbl = new GameObject("L");
            iBtnLbl.transform.SetParent(iBtnGo.transform, false);
            var ibR = iBtnLbl.AddComponent<RectTransform>();
            ibR.anchorMin = Vector2.zero; ibR.anchorMax = Vector2.one; ibR.sizeDelta = Vector2.zero;
            var ibTmp = iBtnLbl.AddComponent<TextMeshProUGUI>();
            ibTmp.text = "i"; ibTmp.fontSize = 11; ibTmp.color = COL_ACCENT;
            ibTmp.alignment = TextAlignmentOptions.Center;
            iBtn.onClick.AddListener(() => tipGo.SetActive(!tipGo.activeSelf));
        }
    }
}
