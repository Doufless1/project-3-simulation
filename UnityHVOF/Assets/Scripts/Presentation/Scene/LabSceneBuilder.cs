// ============================================================================
// Lab Scene Builder — Premium 3D lab with cinematic lighting & post-processing.
//
// Visual upgrade features:
//   - URP Post-Processing: Bloom, ACES tonemapping, vignette, color grading
//   - 10+ lights: spots, LED strips, status indicators, ceiling panels
//   - Emissive materials: laser glow, monitor edges, floor grid, LED dots
//   - Industrial detail: hazard tape, cable trays, labels, E-Stop button
//   - Reflective epoxy floor with subtle grid lines
//   - Live LED indicators that change with system state
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.ValueObjects;
using HVOFSim.Presentation.VFX;
using System.Collections;
using TMPro;
using Material = UnityEngine.Material;

namespace HVOFSim.Presentation.Scene
{
    public sealed class LabSceneBuilder : MonoBehaviour
    {
        [Header("Scene References (auto-created)")]
        public GameObject Chamber;
        public GameObject Table;
        public GameObject LaserHead;
        public GameObject LaserGantry;
        public GameObject GasTank1;
        public GameObject GasTank2;
        public GameObject SafetyEnclosure;
        public GameObject Workpiece;
        public GameObject MonitorScreen;
        public Light MainLight;
        public Light LaserImpactLight;

        // LED indicators (state-reactive)
        private Light _doorLED;
        private Light _chamberLED;
        private Light _laserActiveLED;
        private Light _tableHomedLED;
        private Light _warningPulseLight;
        private Renderer _laserStripeRenderer;
        private Material _laserStripeMat;
        private float _warningPulsePhase;

        public Transform WorkpieceTransform => Workpiece != null ? Workpiece.transform : _tableRoot.transform;
        public LaserBeamVFX BeamVFX { get; private set; }
        public GasFlowVFX GasVFX { get; private set; }

        private GameObject _chamberRoot;
        private GameObject _laserUnitRoot;
        private GameObject _gasSystemRoot;
        private GameObject _tableRoot;
        private Volume _postProcessVolume;
        private Bloom _bloomEffect;
        private Coroutine _swapRoutine;

        // Materials
        private Material _chamberMat;
        private Material _tableMat;
        private Material _laserMat;
        private Material _tankMat;
        private Material _floorMat;
        private Material _wallMat;
        private Material _enclosureMat;
        private Material _workpieceMat;
        private Material _hazardMat;
        private Material _emissiveBlueMat;
        private Material _emissiveOrangeMat;
        private Material _cableMat;
        private Material _labelBgMat;

        private const float Scale = 0.1f;

        public void BuildScene()
        {
            CreateMaterials();
            CreateFloorAndWalls();

            // Build equipment roots
            _tableRoot = new GameObject("XYTableSystem");
            _tableRoot.transform.SetParent(transform);
            
            _chamberRoot = new GameObject("ChamberSystem");
            _chamberRoot.transform.SetParent(transform);
            
            _laserUnitRoot = new GameObject("LaserSystem");
            _laserUnitRoot.transform.SetParent(transform);
            
            _gasSystemRoot = new GameObject("GasSystem");
            _gasSystemRoot.transform.SetParent(transform);

            // Initial equipment creation based on defaults
            SwapChamber(ChamberType.StandardAluminium, animate: false);
            CreateTable();
            SwapLaserType(LaserType.YtterbiumFiber, animate: false);
            SwapGasTanks(GasType.Argon, animate: false);

            CreateSafetyEnclosure(); // Part of standard setup, safe to call
            CreateLighting();
            CreateMonitorPanel();
            CreateHazardMarkings();
            CreateCableTrays();
            CreateEquipmentLabels();
            CreateEStopButton();
            CreateCeilingPanels();
            SetupPostProcessing();
        }

        public void UpdateChamberState(InterlockStatus chamberInterlock, GasState gasState)
        {
            if (_chamberMat == null) return;
            Color targetColor;
            if (chamberInterlock == InterlockStatus.Unlocked)
                targetColor = new Color(0.8f, 0.2f, 0.2f, 0.15f);
            else if (gasState == GasState.Purging)
                targetColor = new Color(0.8f, 0.6f, 0.1f, 0.15f);
            else
                targetColor = new Color(0.2f, 0.8f, 0.3f, 0.15f);
            _chamberMat.color = Color.Lerp(_chamberMat.color, targetColor, Time.deltaTime * 3f);

            // LED indicators
            if (_doorLED != null)
            {
                bool doorOk = chamberInterlock == InterlockStatus.Locked;
                _doorLED.color = doorOk ? new Color(0.1f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
                _doorLED.intensity = doorOk ? 3f : 5f;
            }
            if (_chamberLED != null)
            {
                bool chamOk = chamberInterlock == InterlockStatus.Locked;
                _chamberLED.color = chamOk ? new Color(0.1f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
                _chamberLED.intensity = chamOk ? 3f : 5f;
            }

            // Warning pulse when unsafe
            if (_warningPulseLight != null)
            {
                bool unsafe_ = chamberInterlock == InterlockStatus.Unlocked;
                if (unsafe_)
                {
                    _warningPulsePhase += Time.deltaTime * 4f;
                    _warningPulseLight.intensity = 2f + Mathf.Sin(_warningPulsePhase) * 2f;
                }
                else
                {
                    _warningPulseLight.intensity = 0f;
                }
            }
        }

        public void UpdateTablePosition(XYTable table)
        {
            if (Table == null || table == null) return;
            // Map domain X/Y (0-300mm) to world space. Center of table (150,150) should be at LaserHead (0, y, 0.1).
            float xOffset = (float)((table.Position.XMm - 150.0) * 0.001 * Scale);
            float zOffset = (float)((table.Position.YMm - 150.0) * 0.001 * Scale);
            var target = new Vector3(-xOffset, Table.transform.position.y, 0.1f - zOffset);
            Table.transform.position = Vector3.Lerp(Table.transform.position, target, Time.deltaTime * 5f);

            if (_tableHomedLED != null)
            {
                _tableHomedLED.color = table.IsHomed ? new Color(0.1f, 1f, 0.3f) : new Color(1f, 0.5f, 0.1f);
                _tableHomedLED.intensity = table.IsHomed ? 3f : 1f;
            }
        }

        // Called from CompositionRoot Update to sync laser LED
        public void UpdateLaserLED(LaserState state)
        {
            if (_laserActiveLED != null)
            {
                if (state == LaserState.Firing)
                { _laserActiveLED.color = new Color(1f, 0.3f, 0.05f); _laserActiveLED.intensity = 8f; }
                else if (state == LaserState.Armed)
                { _laserActiveLED.color = new Color(1f, 0.7f, 0.1f); _laserActiveLED.intensity = 4f; }
                else
                { _laserActiveLED.color = new Color(0.1f, 1f, 0.3f); _laserActiveLED.intensity = 1f; }
            }
            // Emissive stripe on laser head
            if (_laserStripeMat != null)
            {
                Color emC = state == LaserState.Firing ? new Color(3f, 1f, 0.2f) :
                            state == LaserState.Armed ? new Color(2f, 1.4f, 0.2f) :
                            Color.black;
                _laserStripeMat.SetColor("_EmissionColor", emC);
            }
        }

        // ── Creation Methods ─────────────────────────────────────────

        private void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            _chamberMat = new Material(shader) { color = new Color(0.5f, 0.5f, 0.6f, 0.15f) };
            SetTransparent(_chamberMat);

            _tableMat = new Material(shader) { color = new Color(0.3f, 0.3f, 0.35f) };
            _tableMat.SetFloat("_Metallic", 0.85f);
            _tableMat.SetFloat("_Smoothness", 0.65f);

            _laserMat = new Material(shader) { color = new Color(0.18f, 0.18f, 0.22f) };
            _laserMat.SetFloat("_Metallic", 0.92f);
            _laserMat.SetFloat("_Smoothness", 0.75f);

            _tankMat = new Material(shader) { color = new Color(0.1f, 0.4f, 0.7f) };
            _tankMat.SetFloat("_Metallic", 0.6f);
            _tankMat.SetFloat("_Smoothness", 0.5f);

            // Reflective epoxy floor
            _floorMat = new Material(shader) { color = new Color(0.12f, 0.12f, 0.14f) };
            _floorMat.SetFloat("_Metallic", 0.15f);
            _floorMat.SetFloat("_Smoothness", 0.85f);

            _wallMat = new Material(shader) { color = new Color(0.22f, 0.22f, 0.25f) };
            _wallMat.SetFloat("_Smoothness", 0.3f);

            _enclosureMat = new Material(shader) { color = new Color(0.1f, 0.2f, 0.35f, 0.2f) };
            SetTransparent(_enclosureMat);
            _enclosureMat.SetFloat("_Metallic", 0.9f);
            _enclosureMat.SetFloat("_Smoothness", 0.85f);

            _workpieceMat = new Material(shader) { color = new Color(0.6f, 0.55f, 0.5f) };
            _workpieceMat.SetFloat("_Metallic", 0.7f);
            _workpieceMat.SetFloat("_Smoothness", 0.4f);

            // Hazard tape: yellow/black
            _hazardMat = new Material(shader) { color = new Color(0.95f, 0.8f, 0.1f) };

            // Emissive blue
            _emissiveBlueMat = new Material(shader) { color = new Color(0.05f, 0.1f, 0.2f) };
            _emissiveBlueMat.EnableKeyword("_EMISSION");
            _emissiveBlueMat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 1.5f));
            _emissiveBlueMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // Emissive orange (for laser)
            _emissiveOrangeMat = new Material(shader) { color = new Color(0.15f, 0.08f, 0.02f) };
            _emissiveOrangeMat.EnableKeyword("_EMISSION");
            _emissiveOrangeMat.SetColor("_EmissionColor", new Color(2f, 0.8f, 0.1f));
            _emissiveOrangeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // Cable
            _cableMat = new Material(shader) { color = new Color(0.08f, 0.08f, 0.08f) };
            _cableMat.SetFloat("_Smoothness", 0.6f);

            // Label background
            _labelBgMat = new Material(shader) { color = new Color(0.15f, 0.15f, 0.2f) };
        }

        private void CreateFloorAndWalls()
        {
            // Main floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(transform);
            floor.transform.localScale = new Vector3(15f, 0.1f, 15f);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.GetComponent<Renderer>().material = _floorMat;

            // Subtle floor grid lines (emissive)
            for (int i = -7; i <= 7; i++)
            {
                var lineX = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lineX.name = "GridX";
                lineX.transform.SetParent(transform);
                lineX.transform.localScale = new Vector3(15f, 0.005f, 0.02f);
                lineX.transform.localPosition = new Vector3(0, 0.002f, i * 1f);
                lineX.GetComponent<Renderer>().material = _emissiveBlueMat;

                var lineZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lineZ.name = "GridZ";
                lineZ.transform.SetParent(transform);
                lineZ.transform.localScale = new Vector3(0.02f, 0.005f, 15f);
                lineZ.transform.localPosition = new Vector3(i * 1f, 0.002f, 0);
                lineZ.GetComponent<Renderer>().material = _emissiveBlueMat;
            }

            // Back wall
            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "BackWall";
            backWall.transform.SetParent(transform);
            backWall.transform.localScale = new Vector3(15f, 6f, 0.15f);
            backWall.transform.localPosition = new Vector3(0, 3f, -7.5f);
            backWall.GetComponent<Renderer>().material = _wallMat;

            // Left wall
            var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.SetParent(transform);
            leftWall.transform.localScale = new Vector3(0.15f, 6f, 15f);
            leftWall.transform.localPosition = new Vector3(-7.5f, 3f, 0f);
            leftWall.GetComponent<Renderer>().material = _wallMat;

            // Right wall
            var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.SetParent(transform);
            rightWall.transform.localScale = new Vector3(0.15f, 6f, 15f);
            rightWall.transform.localPosition = new Vector3(7.5f, 3f, 0f);
            rightWall.GetComponent<Renderer>().material = _wallMat;

            // Ceiling
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(transform);
            ceiling.transform.localScale = new Vector3(15f, 0.1f, 15f);
            ceiling.transform.localPosition = new Vector3(0, 6f, 0);
            ceiling.GetComponent<Renderer>().material = _wallMat;

            // Front wall (transparent glass so camera can see inside)
            var frontWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontWall.name = "FrontWall";
            frontWall.transform.SetParent(transform);
            frontWall.transform.localScale = new Vector3(15f, 6f, 0.15f);
            frontWall.transform.localPosition = new Vector3(0, 3f, 7.5f);
            
            var glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glassMat.color = new Color(0.1f, 0.15f, 0.2f, 0.15f);
            SetTransparent(glassMat);
            frontWall.GetComponent<Renderer>().material = glassMat;
        }

        // ── Equipment Swapping Methods ───────────────────────────────

        public void SwapLaserType(LaserType type, bool animate = true)
        {
            if (animate && _laserUnitRoot.transform.childCount > 0)
            {
                var oldObj = _laserUnitRoot.transform.GetChild(0).gameObject;
                StartCoroutine(SmoothTransition(oldObj, () => CreateLaserModel(type)));
            }
            else
            {
                foreach (Transform child in _laserUnitRoot.transform) Destroy(child.gameObject);
                CreateLaserModel(type);
            }
        }

        public void SwapGasTanks(GasType type, bool animate = true)
        {
            if (animate && _gasSystemRoot.transform.childCount > 0)
            {
                var oldObj = _gasSystemRoot.transform.GetChild(0).gameObject;
                StartCoroutine(SmoothTransition(oldObj, () => CreateGasTanksModel(type)));
            }
            else
            {
                foreach (Transform child in _gasSystemRoot.transform) Destroy(child.gameObject);
                CreateGasTanksModel(type);
            }
        }

        public void SwapChamber(ChamberType type, bool animate = true)
        {
            if (animate && _chamberRoot.transform.childCount > 0)
            {
                var oldObj = _chamberRoot.transform.GetChild(0).gameObject;
                StartCoroutine(SmoothTransition(oldObj, () => CreateChamberModel(type)));
            }
            else
            {
                foreach (Transform child in _chamberRoot.transform) Destroy(child.gameObject);
                CreateChamberModel(type);
            }
        }

        private IEnumerator SmoothTransition(GameObject oldObj, System.Action createNewObj)
        {
            // Ease out old object (scale down)
            float t = 0;
            Vector3 origScale = oldObj.transform.localScale;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float eval = 1f - (t / 0.2f); // 1 to 0
                oldObj.transform.localScale = origScale * eval;
                yield return null;
            }
            Destroy(oldObj);

            // Create new object
            createNewObj();
            if (_laserUnitRoot.transform.childCount == 0 && _gasSystemRoot.transform.childCount == 0 && _chamberRoot.transform.childCount == 0)
                yield break; // Edge case safety

            // Find whichever root we just populated
            Transform newChild = null;
            if (_laserUnitRoot.transform.childCount > 0 && oldObj.transform.parent == _laserUnitRoot.transform)
                newChild = _laserUnitRoot.transform.GetChild(0);
            else if (_gasSystemRoot.transform.childCount > 0 && oldObj.transform.parent == _gasSystemRoot.transform)
                newChild = _gasSystemRoot.transform.GetChild(0);
            else if (_chamberRoot.transform.childCount > 0 && oldObj.transform.parent == _chamberRoot.transform)
                newChild = _chamberRoot.transform.GetChild(0);

            if (newChild != null)
            {
                Vector3 targetScale = newChild.localScale;
                newChild.localScale = Vector3.zero;
                // Ease in new object
                t = 0;
                while (t < 0.3f)
                {
                    t += Time.deltaTime;
                    // Ease out cubic
                    float r = t / 0.3f;
                    float eval = 1f - Mathf.Pow(1f - r, 3f);
                    newChild.localScale = targetScale * eval;
                    yield return null;
                }
                newChild.localScale = targetScale;
            }
        }

        // ── Chamber Generation ───────────────────────────────────────

        private void CreateChamberModel(ChamberType type)
        {
            var pgo = new GameObject($"Chamber_{type}");
            pgo.transform.SetParent(_chamberRoot.transform, false);
            Chamber = pgo;

            if (type == ChamberType.OpenAir) return; // Nothing to draw

            var spec = ChamberSpecs.GetSpec(type);
            
            // Map dimensions to Unity scale (assuming 1 unit = 1 meter approx)
            Vector3 size = new Vector3((float)spec.LengthMm / 500f * 3.2f, (float)spec.HeightMm / 400f * 2.8f, (float)spec.WidthMm / 500f * 3.0f);
            
            var baseCab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCab.transform.SetParent(pgo.transform);
            baseCab.transform.localScale = new Vector3(size.x, 0.8f, size.z);
            baseCab.transform.localPosition = new Vector3(0, 0.4f, 0);
            baseCab.GetComponent<Renderer>().material = _chamberMat;

            // Simplified walls for new chamber sizes
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.transform.SetParent(pgo.transform);
            back.transform.localScale = new Vector3(size.x, size.y, 0.1f);
            back.transform.localPosition = new Vector3(0, 0.8f + size.y / 2f, size.z / 2f - 0.05f);
            
            var wallMat = new Material(_chamberMat);
            if (spec.MaterialName.Contains("Steel"))
                wallMat.color = new Color(0.4f, 0.45f, 0.5f, 0.6f); // Greyish steel tint
            else if (spec.IsVacuum)
                wallMat.color = new Color(0.2f, 0.3f, 0.6f, 0.8f); // Darker blue vacuum rated
            else
                wallMat.color = new Color(0.5f, 0.5f, 0.6f, 0.15f); // Transparent Al
                
            back.GetComponent<Renderer>().material = wallMat;

            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.transform.SetParent(pgo.transform);
            left.transform.localScale = new Vector3(0.1f, size.y, size.z);
            left.transform.localPosition = new Vector3(-size.x / 2f + 0.05f, 0.8f + size.y / 2f, 0);
            left.GetComponent<Renderer>().material = wallMat;

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.transform.SetParent(pgo.transform);
            right.transform.localScale = new Vector3(0.1f, size.y, size.z);
            right.transform.localPosition = new Vector3(size.x / 2f - 0.05f, 0.8f + size.y / 2f, 0);
            right.GetComponent<Renderer>().material = wallMat;

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.SetParent(pgo.transform);
            roof.transform.localScale = new Vector3(size.x, 0.1f, size.z);
            roof.transform.localPosition = new Vector3(0, 0.8f + size.y, 0);
            roof.GetComponent<Renderer>().material = wallMat;

            // Optional Vacuum Pump geometry
            if (spec.IsVacuum)
            {
                var pump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pump.transform.SetParent(pgo.transform);
                pump.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                pump.transform.localPosition = new Vector3(size.x / 2f + 0.5f, 0.4f, size.z / 2f - 0.5f);
                pump.GetComponent<Renderer>().material.color = new Color(0.2f, 0.3f, 0.5f);

                var hose = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hose.transform.SetParent(pgo.transform);
                hose.transform.localScale = new Vector3(0.5f, 0.1f, 0.1f);
                hose.transform.localPosition = new Vector3(size.x / 2f + 0.2f, 0.6f, size.z / 2f - 0.5f);
                hose.GetComponent<Renderer>().material = _cableMat;
            }

            // Keep the safety enclosure bounds logic if needed for the table inside
            SafetyEnclosure = new GameObject("SafetyEnclosure_Proxy");
            SafetyEnclosure.transform.SetParent(pgo.transform);
            SafetyEnclosure.transform.localPosition = new Vector3(0, 0.8f + size.y / 2f, 0);

            // Re-bind LED indicators to relative positions
            _doorLED = CreateLEDIndicator(pgo.transform, new Vector3(-size.x/2f + 0.1f, 0.8f + size.y - 0.2f, -size.z/2f + 0.2f), Color.red);
            _chamberLED = CreateLEDIndicator(pgo.transform, new Vector3(size.x/2f - 0.1f, 0.8f + size.y - 0.2f, -size.z/2f + 0.2f), Color.red);
            
            // Create Table inside chamber if it doesn't exist yet
            if (_tableRoot.transform.childCount == 0) CreateTable();
        }

        private void CreateFramePillar(Vector3 pos)
        {
            // Main pillar
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.transform.SetParent(Chamber.transform);
            pillar.transform.localScale = new Vector3(0.1f, 2.0f, 0.1f);
            pillar.transform.localPosition = pos;
            pillar.GetComponent<Renderer>().material = _laserMat;

            // Emissive edge strip along pillar
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.transform.SetParent(Chamber.transform);
            strip.transform.localScale = new Vector3(0.03f, 2.01f, 0.03f);
            strip.transform.localPosition = pos + new Vector3(-0.045f, 0, -0.045f);
            strip.GetComponent<Renderer>().material = _emissiveBlueMat;
        }

        private void CreateTable()
        {
            var tableGO = new GameObject("CNC_Table_Geometry");
            tableGO.transform.SetParent(_tableRoot.transform);

            var machineBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            machineBase.transform.SetParent(tableGO.transform);
            machineBase.transform.localScale = new Vector3(2.4f, 0.6f, 1.8f);
            machineBase.transform.localPosition = new Vector3(0, 0.51f, 0); // Raised slightly to prevent z-fighting with chamber base
            var baseMat = new Material(_tableMat);
            baseMat.color = new Color(0.08f, 0.08f, 0.09f); // Darker base
            baseMat.SetFloat("_Metallic", 0.7f);
            baseMat.SetFloat("_Smoothness", 0.5f);
            machineBase.GetComponent<Renderer>().material = baseMat;

            // Guide rails (polished aluminum)
            var railMat = new Material(_tableMat);
            railMat.color = new Color(0.8f, 0.85f, 0.9f);
            railMat.SetFloat("_Metallic", 0.95f);
            railMat.SetFloat("_Smoothness", 0.8f);

            var rail1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rail1.transform.SetParent(tableGO.transform);
            rail1.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
            rail1.transform.localPosition = new Vector3(-0.6f, 0.85f, 0);
            rail1.transform.localRotation = Quaternion.Euler(90, 0, 0);
            rail1.GetComponent<Renderer>().material = railMat;

            var rail2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rail2.transform.SetParent(tableGO.transform);
            rail2.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
            rail2.transform.localPosition = new Vector3(0.6f, 0.85f, 0);
            rail2.transform.localRotation = Quaternion.Euler(90, 0, 0);
            rail2.GetComponent<Renderer>().material = railMat;

            // Stepper motor block
            var motor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            motor.transform.SetParent(tableGO.transform);
            motor.transform.localScale = new Vector3(0.2f, 0.2f, 0.3f);
            motor.transform.localPosition = new Vector3(0, 0.85f, 0.95f);
            motor.GetComponent<Renderer>().material = _laserMat;

            // Table homed LED on motor
            _tableHomedLED = CreateLEDIndicator(tableGO.transform, new Vector3(0, 0.98f, 0.95f), new Color(1f, 0.5f, 0f));

            // Moving platform (luxurious machined metal)
            Table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Table.name = "TablePlatform";
            Table.transform.SetParent(tableGO.transform);
            Table.transform.localScale = new Vector3(1.6f, 0.1f, 1.2f);
            Table.transform.localPosition = new Vector3(0, 0.95f, 0);
            var platMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            platMat.color = new Color(0.65f, 0.68f, 0.75f); // Bright brushed steel
            platMat.SetFloat("_Metallic", 0.95f);
            platMat.SetFloat("_Smoothness", 0.85f);
            Table.GetComponent<Renderer>().material = platMat;

            // T-Slots on table
            var slotMat = new Material(_laserMat);
            slotMat.color = new Color(0.3f, 0.3f, 0.35f);
            for (int i = -4; i <= 4; i++)
            {
                var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slot.transform.SetParent(Table.transform);
                slot.transform.localScale = new Vector3(0.02f, 1.1f, 1.05f);
                slot.transform.localPosition = new Vector3(i * 0.15f, 0.01f, 0);
                slot.GetComponent<Renderer>().material = slotMat;
            }
            CreateWorkpiece(); // Ensure workpiece exists on table
        }

        // ── Laser Model Generation ───────────────────────────────────

        private void CreateLaserModel(LaserType type)
        {
            var pgo = new GameObject($"Laser_{type}");
            pgo.transform.SetParent(_laserUnitRoot.transform, false);

            switch (type)
            {
                case LaserType.YtterbiumFiber:
                    CreateStandardLaserUnit(pgo.transform);
                    break;
                case LaserType.CO2:
                    CreateCO2LaserUnit(pgo.transform);
                    break;
                case LaserType.NdYAG:
                    CreateNdYAGLaserUnit(pgo.transform);
                    break;
                case LaserType.Diode:
                    CreateDiodeLaserUnit(pgo.transform);
                    break;
            }

            // Create VFX components on the laser head
            Transform head = pgo.transform.Find("LaserGantry/LaserHead") ?? pgo.transform.GetChild(0).Find("LaserHead") ?? pgo.transform;
            if (head == pgo.transform && LaserHead != null) head = LaserHead.transform;

            var beamGO = new GameObject("BeamVFX");
            beamGO.transform.SetParent(head);
            BeamVFX = beamGO.AddComponent<LaserBeamVFX>();

            var ltGO = new GameObject("ImpactLight");
            ltGO.transform.SetParent(beamGO.transform);
            LaserImpactLight = ltGO.AddComponent<Light>();
            LaserImpactLight.type = LightType.Point;
            LaserImpactLight.color = new Color(1f, 0.8f, 0.4f);
            LaserImpactLight.intensity = 0f;
            LaserImpactLight.range = 3f;
            LaserImpactLight.shadows = LightShadows.Soft;

            BeamVFX.Initialize(head, WorkpieceTransform, LaserImpactLight);
            if (Workpiece != null) BeamVFX.UpdateBeamColors(type);
        }

        private void CreateStandardLaserUnit(Transform parent)
        {
            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "LaserCabinet";
            cab.transform.SetParent(parent);
            cab.transform.localScale = new Vector3(0.6f, 1.2f, 0.8f);
            cab.transform.position = new Vector3(-1.5f, 0.6f, 0f);
            cab.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.25f);

            CreateGantryAndHead(parent, new Vector3(0.1f, 0.15f, 0.1f));
        }

        private void CreateCO2LaserUnit(Transform parent)
        {
            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "LaserCabinet";
            cab.transform.SetParent(parent);
            cab.transform.localScale = new Vector3(1.2f, 1.8f, 2.0f);
            cab.transform.position = new Vector3(-2.2f, 0.9f, 0.5f);
            cab.GetComponent<Renderer>().material.color = new Color(0.1f, 0.1f, 0.15f);

            CreateGantryAndHead(parent, new Vector3(0.25f, 0.4f, 0.25f)); // Bulkier head
            
            // Mirror delivery arm (articulated tube rather than fiber)
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arm.transform.SetParent(parent);
            arm.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
            arm.transform.position = new Vector3(-1.0f, 2.2f, 0f); // Bridge to gantry
            arm.transform.rotation = Quaternion.Euler(0, 0, 90f);
            arm.GetComponent<Renderer>().material.color = new Color(0.6f, 0.6f, 0.65f);
        }

        private void CreateNdYAGLaserUnit(Transform parent)
        {
            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "LaserCabinet";
            cab.transform.SetParent(parent);
            cab.transform.localScale = new Vector3(0.6f, 1.2f, 1.5f);
            cab.transform.position = new Vector3(-1.5f, 0.6f, 0.2f);
            cab.GetComponent<Renderer>().material.color = new Color(0.85f, 0.85f, 0.85f); // Medical/scientific white

            CreateGantryAndHead(parent, new Vector3(0.15f, 0.2f, 0.15f));
        }

        private void CreateDiodeLaserUnit(Transform parent)
        {
            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "LaserCabinet";
            cab.transform.SetParent(parent);
            cab.transform.localScale = new Vector3(0.4f, 0.6f, 0.6f);
            cab.transform.position = new Vector3(-1.2f, 0.3f, 0f);
            cab.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f);

            CreateGantryAndHead(parent, new Vector3(0.3f, 0.2f, 0.15f)); // Wide profile head
            
            // Copper heatsink fins on top of head
            if (LaserHead != null)
            {
                var fins = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fins.transform.SetParent(LaserHead.transform);
                fins.transform.localPosition = new Vector3(0, 0.2f, 0);
                fins.transform.localScale = new Vector3(0.9f, 0.3f, 0.8f);
                fins.GetComponent<Renderer>().material.color = new Color(0.7f, 0.4f, 0.2f);
            }
        }

        private void CreateGantryAndHead(Transform parent, Vector3 headScale)
        {
            LaserGantry = new GameObject("LaserGantry");
            LaserGantry.transform.SetParent(parent);
            LaserGantry.transform.localPosition = new Vector3(0, 2.2f, 0);

            var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bridge.transform.SetParent(LaserGantry.transform);
            bridge.transform.localScale = new Vector3(3.0f, 0.2f, 0.2f);
            bridge.GetComponent<Renderer>().material = _laserMat;

            var zAxis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zAxis.transform.SetParent(LaserGantry.transform);
            zAxis.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f);
            zAxis.transform.localPosition = new Vector3(0, -0.4f, 0.1f);
            zAxis.GetComponent<Renderer>().material = _tableMat;

            LaserHead = new GameObject("LaserHead");
            LaserHead.transform.SetParent(LaserGantry.transform);
            LaserHead.transform.localPosition = new Vector3(0, -0.8f, 0.1f);

            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.transform.SetParent(LaserHead.transform);
            housing.transform.localScale = headScale;
            housing.GetComponent<Renderer>().material = _laserMat;

            // Emissive accent stripe (changes color with laser state)
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.transform.SetParent(LaserHead.transform);
            stripe.transform.localScale = new Vector3(headScale.x * 1.1f, 0.05f, headScale.z * 1.1f);
            stripe.transform.localPosition = new Vector3(0, -headScale.y * 0.2f, 0);
            _laserStripeMat = new Material(_emissiveOrangeMat);
            _laserStripeMat.SetColor("_EmissionColor", Color.black); // start off
            stripe.GetComponent<Renderer>().material = _laserStripeMat;
            _laserStripeRenderer = stripe.GetComponent<Renderer>();

            // Nozzle
            var nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nozzle.transform.SetParent(LaserHead.transform);
            nozzle.transform.localScale = new Vector3(0.05f, 0.08f, 0.05f);
            nozzle.transform.localPosition = new Vector3(0, -headScale.y/2f - 0.04f, 0);
            var nozzleMat = new Material(_tableMat);
            nozzleMat.color = new Color(0.8f, 0.8f, 0.85f);
            nozzleMat.SetFloat("_Metallic", 0.95f);
            nozzle.GetComponent<Renderer>().material = nozzleMat;

            // Laser active LED on gantry
            _laserActiveLED = CreateLEDIndicator(LaserGantry.transform, new Vector3(0.2f, 0.05f, 0), new Color(0.1f, 1f, 0.3f));
        }

        // ── Gas Tank Generation ───────────────────────────────────────

        private void CreateGasTanksModel(GasType type)
        {
            var pgo = new GameObject($"Gas_{type}");
            pgo.transform.SetParent(_gasSystemRoot.transform, false);

            if (type == GasType.None) return; // Open air, no tanks

            var spec = GasTypeSpecs.GetSpec(type);
            Color tankColor;
            ColorUtility.TryParseHtmlString(spec.CylinderColorHex, out tankColor);

            GasTank1 = CreateDetailedTank(pgo.transform, "Tank_1", new Vector3(2.5f, 0f, -2f), tankColor);
            GasTank2 = CreateDetailedTank(pgo.transform, "Tank_2", new Vector3(3.0f, 0f, -2f), tankColor);

            // Create VFX components at nozzle location (near laser head usually)
            var gasGO = new GameObject("GasFlowVFX");
            gasGO.transform.SetParent(pgo.transform);
            GasVFX = gasGO.AddComponent<GasFlowVFX>();
            GasVFX.Initialize(new Vector3(0, 0.45f, 0.05f)); // Slightly offset from laser head nominal pos
            GasVFX.UpdateGasColor(type);
        }

        private GameObject CreateDetailedTank(Transform parent, string name, Vector3 pos, Color tankColor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.localPosition = pos;

            var tankBodyMat = new Material(_tankMat);
            tankBodyMat.color = tankColor;
            tankBodyMat.SetFloat("_Metallic", 0.7f);
            tankBodyMat.SetFloat("_Smoothness", 0.55f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(root.transform);
            body.transform.localScale = new Vector3(0.5f, 1.0f, 0.5f);
            body.transform.localPosition = new Vector3(0, 1.0f, 0);
            body.GetComponent<Renderer>().material = tankBodyMat;

            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.transform.SetParent(root.transform);
            top.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            top.transform.localPosition = new Vector3(0, 2.0f, 0);
            top.GetComponent<Renderer>().material = tankBodyMat;

            // Brass valve
            var brassMat = new Material(_tableMat);
            brassMat.color = new Color(0.8f, 0.7f, 0.3f);
            brassMat.SetFloat("_Metallic", 0.9f);
            brassMat.SetFloat("_Smoothness", 0.7f);

            var valveBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            valveBase.transform.SetParent(root.transform);
            valveBase.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
            valveBase.transform.localPosition = new Vector3(0, 2.3f, 0);
            valveBase.GetComponent<Renderer>().material = brassMat;

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.transform.SetParent(root.transform);
            handle.transform.localScale = new Vector3(0.2f, 0.02f, 0.2f);
            handle.transform.localPosition = new Vector3(0, 2.45f, 0);
            handle.GetComponent<Renderer>().material = _cableMat;

            // Pressure gauge (emissive green dot)
            var gauge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gauge.transform.SetParent(root.transform);
            gauge.transform.localScale = new Vector3(0.08f, 0.08f, 0.02f);
            gauge.transform.localPosition = new Vector3(0.26f, 1.8f, 0);
            var gaugeMat = new Material(_emissiveBlueMat);
            gaugeMat.color = new Color(0.05f, 0.15f, 0.05f);
            gaugeMat.SetColor("_EmissionColor", new Color(0.2f, 1.2f, 0.3f));
            gauge.GetComponent<Renderer>().material = gaugeMat;

            return root;
        }

        private void CreateSafetyEnclosure() { /* handled inside CreateChamber */ }

        private void CreateMonitorPanel()
        {
            var deskRoot = new GameObject("Command_Center");
            deskRoot.transform.SetParent(transform);
            deskRoot.transform.localPosition = new Vector3(0, 0, -3.5f);

            var deskLegMat = new Material(_tableMat);
            deskLegMat.color = new Color(0.06f, 0.06f, 0.08f);

            var deskLegs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deskLegs.transform.SetParent(deskRoot.transform);
            deskLegs.transform.localScale = new Vector3(2.4f, 0.9f, 0.6f);
            deskLegs.transform.localPosition = new Vector3(0, 0.45f, 0);
            deskLegs.GetComponent<Renderer>().material = deskLegMat;

            var deskTopMat = new Material(_tableMat);
            deskTopMat.color = new Color(0.1f, 0.1f, 0.13f);
            deskTopMat.SetFloat("_Metallic", 0.5f);
            deskTopMat.SetFloat("_Smoothness", 0.7f);

            var deskTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deskTop.transform.SetParent(deskRoot.transform);
            deskTop.transform.localScale = new Vector3(2.6f, 0.1f, 1.0f);
            deskTop.transform.localPosition = new Vector3(0, 0.95f, 0);
            deskTop.GetComponent<Renderer>().material = deskTopMat;

            // Monitor with emissive bezel
            MonitorScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MonitorScreen.name = "DashboardMonitorBase";
            MonitorScreen.transform.SetParent(deskRoot.transform);
            MonitorScreen.transform.localScale = new Vector3(2.4f, 1.35f, 0.05f);
            MonitorScreen.transform.localPosition = new Vector3(0, 1.6f, 0.2f);

            var screenMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            screenMat.color = new Color(0.02f, 0.02f, 0.03f);
            screenMat.SetFloat("_Metallic", 0.6f);
            screenMat.SetFloat("_Smoothness", 0.8f);
            MonitorScreen.GetComponent<Renderer>().material = screenMat;

            // Monitor edge bezel (emissive blue trim)
            var bezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bezel.transform.SetParent(deskRoot.transform);
            bezel.transform.localScale = new Vector3(2.48f, 1.40f, 0.03f);
            bezel.transform.localPosition = new Vector3(0, 1.6f, 0.22f);
            bezel.GetComponent<Renderer>().material = _emissiveBlueMat;
        }

        private void CreateWorkpiece()
        {
            Workpiece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Workpiece.name = "Workpiece";
            Workpiece.transform.SetParent(Table.transform);
            Workpiece.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
            Workpiece.transform.localPosition = new Vector3(0, 0.1f, 0);
            Workpiece.GetComponent<Renderer>().material = _workpieceMat;
        }

        private void CreateLighting()
        {
            RenderSettings.ambientIntensity = 0.35f;
            RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.1f);
            RenderSettings.ambientMode = AmbientMode.Flat;

            // Main directional fill light (warm)
            var dirGO = new GameObject("DirectionalFill");
            dirGO.transform.SetParent(transform);
            dirGO.transform.rotation = Quaternion.Euler(50, -30, 0);
            var dir = dirGO.AddComponent<Light>();
            dir.type = LightType.Directional;
            dir.color = new Color(0.95f, 0.9f, 0.85f);
            dir.intensity = 0.6f;
            dir.shadows = LightShadows.Soft;

            // Chamber spotlight
            var spotGO = new GameObject("ChamberSpotlight");
            spotGO.transform.SetParent(Chamber.transform);
            spotGO.transform.localPosition = new Vector3(0, 2.7f, 0);
            spotGO.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var spot = spotGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.9f, 0.95f, 1f);
            spot.intensity = 20f;
            spot.range = 8f;
            spot.spotAngle = 75f;
            spot.shadows = LightShadows.Soft;

            // Blue LED under-table strip lights
            CreatePointLight("LEDStrip1", new Vector3(-0.8f, 0.05f, 0), new Color(0.1f, 0.4f, 1f), 6f, 2.5f);
            CreatePointLight("LEDStrip2", new Vector3(0.8f, 0.05f, 0), new Color(0.1f, 0.4f, 1f), 6f, 2.5f);
            CreatePointLight("LEDStrip3", new Vector3(0f, 0.05f, -0.5f), new Color(0.05f, 0.3f, 0.9f), 4f, 2f);

            // Monitor glow (blue backlight)
            CreatePointLight("MonitorGlow", new Vector3(0, 1.5f, -3.0f), new Color(0.15f, 0.3f, 0.9f), 4f, 3f);

            // Red warning light (near E-Stop area on desk)
            var warnGO = new GameObject("WarningPulse");
            warnGO.transform.SetParent(transform);
            warnGO.transform.position = new Vector3(-1.0f, 1.1f, -3.5f);
            _warningPulseLight = warnGO.AddComponent<Light>();
            _warningPulseLight.type = LightType.Point;
            _warningPulseLight.color = new Color(1f, 0.1f, 0.05f);
            _warningPulseLight.intensity = 0f;
            _warningPulseLight.range = 2f;

            // Gas area green accent
            CreatePointLight("GasAccent", new Vector3(2.75f, 0.3f, -2f), new Color(0.1f, 0.8f, 0.3f), 2f, 2f);

            // Laser impact point light
            var impactGO = new GameObject("LaserImpactLight");
            impactGO.transform.SetParent(LaserHead.transform);
            impactGO.transform.localPosition = new Vector3(0, -0.4f, 0);
            LaserImpactLight = impactGO.AddComponent<Light>();
            LaserImpactLight.type = LightType.Point;
            LaserImpactLight.color = new Color(1f, 0.5f, 0.1f);
            LaserImpactLight.intensity = 0f;
            LaserImpactLight.range = 2f;
        }

        private void CreateHazardMarkings()
        {
            // Yellow/black hazard strips around chamber base
            float y = 0.01f;
            float halfW = 1.7f;
            float halfD = 1.6f;

            CreateHazardStrip(new Vector3(0, y, -halfD), new Vector3(halfW * 2, 0.01f, 0.15f));
            CreateHazardStrip(new Vector3(0, y, halfD), new Vector3(halfW * 2, 0.01f, 0.15f));
            CreateHazardStrip(new Vector3(-halfW, y, 0), new Vector3(0.15f, 0.01f, halfD * 2));
            CreateHazardStrip(new Vector3(halfW, y, 0), new Vector3(0.15f, 0.01f, halfD * 2));
        }

        private void CreateHazardStrip(Vector3 pos, Vector3 scale)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "HazardStrip";
            strip.transform.SetParent(transform);
            strip.transform.localScale = scale;
            strip.transform.localPosition = pos;
            strip.GetComponent<Renderer>().material = _hazardMat;
        }

        private void CreateCableTrays()
        {
            // Cable tray from desk to chamber (black conduit)
            var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "CableTray";
            tray.transform.SetParent(transform);
            tray.transform.localScale = new Vector3(0.2f, 0.08f, 3.0f);
            tray.transform.localPosition = new Vector3(-1.2f, 0.04f, -1.75f);
            tray.GetComponent<Renderer>().material = _cableMat;

            // Vertical riser
            var riser = GameObject.CreatePrimitive(PrimitiveType.Cube);
            riser.name = "CableRiser";
            riser.transform.SetParent(transform);
            riser.transform.localScale = new Vector3(0.15f, 2.2f, 0.1f);
            riser.transform.localPosition = new Vector3(-1.2f, 1.1f, -0.3f);
            riser.GetComponent<Renderer>().material = _cableMat;

            // Gas lines from tanks to chamber (using LineRenderer for curved cables)
            var gasLineGO = new GameObject("GasCables");
            gasLineGO.transform.SetParent(transform);
            var line = gasLineGO.AddComponent<LineRenderer>();
            line.positionCount = 4;
            line.startWidth = 0.03f;
            line.endWidth = 0.03f;
            line.useWorldSpace = true;
            
            // Connect Tank 1 and Tank 2 to the safety chamber
            line.SetPosition(0, new Vector3(3.0f, 2.3f, -2.0f)); // Tank 2 valve
            line.SetPosition(1, new Vector3(2.5f, 2.3f, -2.0f)); // Tank 1 valve
            line.SetPosition(2, new Vector3(1.8f, 1.2f, -1.0f)); // Midway sag
            line.SetPosition(3, new Vector3(1.5f, 1.0f, 0.5f));  // Chamber inlet
            
            var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lineMat.color = new Color(0.1f, 0.2f, 0.15f); // Dark industrial green hose
            line.material = lineMat;
        }

        private void CreateEquipmentLabels()
        {
            CreateTextLabel("IPG YLR-1000", new Vector3(0, 2.5f, -1.3f), 0.15f);
            CreateTextLabel("ARGON", new Vector3(2.75f, 2.7f, -2f), 0.12f);
            CreateTextLabel("X-Y STAGE", new Vector3(0, 0.3f, -1.0f), 0.1f);
            CreateTextLabel("[!] CLASS 4 LASER", new Vector3(0, 2.95f, -1.3f), 0.1f, new Color(1f, 0.3f, 0.1f));
        }

        private void CreateTextLabel(string text, Vector3 worldPos, float size, Color? color = null)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(transform);
            go.transform.position = worldPos;

            // Background plate
            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bg.transform.SetParent(go.transform);
            bg.transform.localScale = new Vector3(text.Length * size * 0.55f, size * 1.2f, 0.02f);
            bg.transform.localPosition = Vector3.zero;
            bg.GetComponent<Renderer>().material = _labelBgMat;

            var tmpGO = new GameObject("Text");
            tmpGO.transform.SetParent(go.transform);
            tmpGO.transform.localPosition = new Vector3(0, 0, -0.015f);
            
            var tmp = tmpGO.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 4; // Keep base font size small for 3D space
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? new Color(0.8f, 0.85f, 0.9f);
            
            // Allow wrapping and set the rect size to be larger than the text
            tmp.rectTransform.sizeDelta = new Vector2(text.Length, 2f);
            
            // Adjust the actual scale of the text container to fit the background size parameter
            tmpGO.transform.localScale = new Vector3(size * 0.6f, size * 0.6f, 1f);
        }

        private void CreateEStopButton()
        {
            var estopRoot = new GameObject("EStopButton");
            estopRoot.transform.SetParent(transform);
            estopRoot.transform.position = new Vector3(-1.0f, 1.02f, -3.5f);

            // Base plate (yellow)
            var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePlate.transform.SetParent(estopRoot.transform);
            basePlate.transform.localScale = new Vector3(0.15f, 0.02f, 0.15f);
            basePlate.transform.localPosition = Vector3.zero;
            basePlate.GetComponent<Renderer>().material = _hazardMat;

            // Red mushroom cap
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.transform.SetParent(estopRoot.transform);
            cap.transform.localScale = new Vector3(0.12f, 0.06f, 0.12f);
            cap.transform.localPosition = new Vector3(0, 0.04f, 0);
            var capMat = new Material(_emissiveOrangeMat);
            capMat.color = new Color(0.8f, 0.05f, 0.02f);
            capMat.SetColor("_EmissionColor", new Color(1.5f, 0.1f, 0.05f));
            cap.GetComponent<Renderer>().material = capMat;
        }

        private void CreateCeilingPanels()
        {
            // Emissive fluorescent panels on ceiling
            var panelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            panelMat.color = new Color(0.9f, 0.9f, 0.95f);
            panelMat.EnableKeyword("_EMISSION");
            panelMat.SetColor("_EmissionColor", new Color(1.5f, 1.5f, 1.6f));
            panelMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            CreateCeilingPanel(panelMat, new Vector3(-2f, 5.9f, -1f));
            CreateCeilingPanel(panelMat, new Vector3(2f, 5.9f, -1f));
            CreateCeilingPanel(panelMat, new Vector3(0f, 5.9f, -3f));
        }

        private void CreateCeilingPanel(Material mat, Vector3 pos)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "CeilingLight";
            panel.transform.SetParent(transform);
            panel.transform.localScale = new Vector3(1.5f, 0.05f, 0.5f);
            panel.transform.localPosition = pos;
            panel.GetComponent<Renderer>().material = mat;

            // Paired point light to actually illuminate
            var lightGO = new GameObject("CeilingLightSrc");
            lightGO.transform.SetParent(panel.transform);
            lightGO.transform.localPosition = new Vector3(0, -0.5f, 0);
            var lt = lightGO.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(0.95f, 0.95f, 1f);
            lt.intensity = 3f;
            lt.range = 5f;
        }

        private void SetupPostProcessing()
        {
            // Create runtime Volume for cinematic post-processing
            var volGO = new GameObject("PostProcessVolume");
            volGO.transform.SetParent(transform);
            _postProcessVolume = volGO.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
            _postProcessVolume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _postProcessVolume.profile = profile;

            // Bloom — dynamic based on laser power (adjusted externally)
            _bloomEffect = profile.Add<Bloom>();
            _bloomEffect.active = true;
            _bloomEffect.threshold.Override(0.8f);
            _bloomEffect.intensity.Override(1.0f);
            _bloomEffect.scatter.Override(0.7f);
            _bloomEffect.highQualityFiltering.Override(true);

            // Screen Space Ambient Occlusion — gives depth to chamber corners and rails
            // Optional/expensive, so keep it subtle
            // Removed direct SSAO instantiation to avoid assembly reference issues since it's 
            // part of the Universal scriptable render pipeline package, not UnityEngine.Rendering.Universal.
            // We rely on the project's default renderer features for SSAO if enabled.

            // Depth of Field — focuses on the workpiece
            var dof = profile.Add<DepthOfField>();
            dof.active = true;
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(1.5f);
            dof.gaussianEnd.Override(4f);
            dof.gaussianMaxRadius.Override(1f);

            // ACES Tonemapping — cinematic color grading
            var tone = profile.Add<Tonemapping>();
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            // Vignette — draws focus
            var vig = profile.Add<Vignette>();
            vig.active = true;
            vig.intensity.Override(0.4f);
            vig.smoothness.Override(0.5f);

            // Color Adjustments — richer, more contrast
            var colorAdj = profile.Add<ColorAdjustments>();
            colorAdj.active = true;
            colorAdj.contrast.Override(18f);
            colorAdj.saturation.Override(12f);
            colorAdj.postExposure.Override(0.1f);
        }

        // Public method to dynamically scale bloom with laser power
        public void UpdateDynamicBloom(double powerPct)
        {
            if (_bloomEffect != null)
            {
                // Ramp bloom intensity from 1.0 (idle) up to 3.5 (full power)
                _bloomEffect.intensity.Override(1.0f + (float)(powerPct / 100.0) * 2.5f);
            }
        }

        // ── Utility Methods ──────────────────────────────────────────

        private Light CreateLEDIndicator(Transform parent, Vector3 localPos, Color color)
        {
            var go = new GameObject("LED");
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;

            // Emissive dot
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.SetParent(go.transform);
            dot.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            dot.transform.localPosition = Vector3.zero;
            var dotMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            dotMat.color = color;
            dotMat.EnableKeyword("_EMISSION");
            dotMat.SetColor("_EmissionColor", color * 3f);
            dotMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            dot.GetComponent<Renderer>().material = dotMat;

            // Tiny point light
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = color;
            lt.intensity = 3f;
            lt.range = 0.5f;
            return lt;
        }

        private void CreatePointLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = pos;
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = color;
            lt.intensity = intensity;
            lt.range = range;
        }

        private static void SetTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
