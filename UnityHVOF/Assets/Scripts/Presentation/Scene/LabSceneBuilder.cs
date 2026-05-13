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

        // Enclosure door (interactive open/close)
        private Transform _enclosureDoorPivot;
        private bool _doorIsOpen;
        private Coroutine _doorRoutine;

        // Materials
        private Material _chamberMat;
        private Material _tableMat;
        private Material _laserMat;
        private Material _tankMat;
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

            // Initial equipment creation based on defaults. SwapChamber()
            // already creates the XY table internally if one is missing
            // (see CreateChamberModel) — calling CreateTable() here too would
            // produce a duplicate stack of tables.
            SwapChamber(ChamberType.StandardAluminium, animate: false);
            SwapLaserType(LaserType.YtterbiumFiber, animate: false);
            SwapGasTanks(GasType.Argon, animate: false);

            // CreateSafetyEnclosure();  // Removed — chamber handles its own enclosure
            CreateLighting();
            // CreateMonitorPanel(); // Removed — monitor screen and table
            CreateHazardMarkings();
            CreateCableTrays();
            CreateEquipmentLabels();
            // CreateEStopButton();  // Removed — IPG cabinet has integrated E-Stop
            CreateFumeExtractor();
            // Ceiling fluorescent panels are now owned by LabRoomGeometry
            // (the room shell builder) to keep architecture in one place.
            SetupPostProcessing();

            // After every sub-builder runs, rearrange/scale equipment so it
            // actually fits inside the real 2.76 x 6.02 m room polygon.
            ArrangeEquipmentForRoom();
        }

        // ── Equipment arrangement for the real room ──────────────────────
        //
        // Room is a rectangle 1.21 (X) x 5.00 (Z) x 2.76 (Y) m built by
        // LabRoomGeometry. All equipment is aggressively compressed to fit.
        //
        // Stations arranged along the long Z axis:
        //   - Desk + Monitor (Command_Center)  : near the front  (z = +1.80 m)
        //   - Chamber + Table + Laser          : middle          (z = -1.00 m)
        //   - Gas tanks                        : along back wall (z = -1.55 m)
        //
        // Hazard strips / cable trays / labels from the old 15 m lab have
        // hard-coded world coordinates that do not apply here and are disabled.
        private void ArrangeEquipmentForRoom()
        {
            // All equipment at the BACK of the 5 m corridor so the user
            // walks down and opens the enclosure door to load samples.
            // Room Z range: ~+2.50 (front/entrance) to ~-2.50 (back wall).
            const float backZ = 2.10f;

            if (_chamberRoot != null)
            {
                _chamberRoot.transform.localScale    = new Vector3(0.25f, 0.40f, 0.25f);
                _chamberRoot.transform.localPosition = new Vector3(0f, 0f, backZ);
                // Rotate 180° so the door faces -Z (toward the user approaching from the entrance)
                _chamberRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            if (_tableRoot != null)
            {
                _tableRoot.transform.localScale    = new Vector3(0.25f, 0.30f, 0.25f);
                _tableRoot.transform.localPosition = new Vector3(0f, 0f, backZ);
            }
            if (_laserUnitRoot != null)
            {
                _laserUnitRoot.transform.localScale    = new Vector3(0.25f, 0.45f, 0.25f);
                _laserUnitRoot.transform.localPosition = new Vector3(0f, 0f, backZ);
            }

            if (_gasSystemRoot != null)
            {
                _gasSystemRoot.transform.localScale    = new Vector3(0.25f, 0.40f, 0.25f);
                _gasSystemRoot.transform.localPosition = new Vector3(-0.68f, 0f, backZ + 0.55f);
            }

            // Move fume extractor next to the enclosure
            var fumeExt = transform.Find("BOFA_FumeExtractor");
            if (fumeExt != null)
            {
                fumeExt.localPosition = new Vector3(0.45f, 1.15f, backZ);
            }

            /*
            var commandCenter = transform.Find("Command_Center");
            if (commandCenter != null)
            {
                commandCenter.localScale    = new Vector3(0.40f, 0.55f, 0.40f);
                commandCenter.localPosition = new Vector3(0f, 0f, 1.80f);
                // Rotate 180 so the monitor faces the chamber / back of room.
                commandCenter.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            */

            var estop = transform.Find("EStopButton");
            if (estop != null)
            {
                estop.localPosition = new Vector3(-0.30f, 0.60f, 1.70f);
                estop.localScale    = Vector3.one;
            }

            // 4) Hide / destroy objects authored for the old 15 m room whose
            //    hard-coded world coordinates make no sense here. Safer than
            //    trying to re-derive their positions.
            DisableByName("CableTray");
            DisableByName("CableRiser");
            DisableByName("GasCables");
            DisableByName("WarningPulse");
            foreach (Transform t in transform)
            {
                if (t.name == "HazardStrip") t.gameObject.SetActive(false);
                // Equipment labels were placed at absolute world coords for the
                // old room; they float awkwardly in the new one. Disable until
                // a follow-up gives them proper local anchors on equipment.
                if (t.name.StartsWith("Label_")) t.gameObject.SetActive(false);
            }
        }

        private void DisableByName(string goName)
        {
            var t = transform.Find(goName);
            if (t != null) t.gameObject.SetActive(false);
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

        // Anchor the moving CNC platform under the laser head. The chamber and
        // laser are positioned at world z = ChamberAnchorZ in ArrangeEquipmentForRoom;
        // the runtime motion (X/Y within 0–300 mm) is added on top.
        private const float ChamberAnchorZ = 2.10f;

        public void UpdateTablePosition(XYTable table)
        {
            if (Table == null || table == null) return;
            // Map domain X/Y (0-300mm) to world space. Center of table (150,150)
            // should sit directly under the laser head at (0, y, ChamberAnchorZ).
            float xOffset = (float)((table.Position.XMm - 150.0) * 0.001 * Scale);
            float zOffset = (float)((table.Position.YMm - 150.0) * 0.001 * Scale);
            var target = new Vector3(-xOffset, Table.transform.position.y, ChamberAnchorZ - zOffset);
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

        // ── Interactive Enclosure Door ───────────────────────────────

        /// &lt;summary&gt;Click the enclosure door to open / close it.&lt;/summary&gt;
        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && _enclosureDoorPivot != null)
            {
                var ray = UnityEngine.Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                {
                    // Check if the hit object is a child of the door pivot
                    if (hit.transform.IsChildOf(_enclosureDoorPivot))
                        ToggleEnclosureDoor();
                }
            }
        }

        public void ToggleEnclosureDoor()
        {
            if (_enclosureDoorPivot == null) return;
            if (_doorRoutine != null) StopCoroutine(_doorRoutine);
            _doorIsOpen = !_doorIsOpen;
            _doorRoutine = StartCoroutine(AnimateDoor(_doorIsOpen ? -90f : 0f));
        }

        private IEnumerator AnimateDoor(float targetAngle)
        {
            Quaternion start = _enclosureDoorPivot.localRotation;
            Quaternion end   = Quaternion.Euler(0f, targetAngle, 0f);
            float t = 0f;
            const float duration = 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f); // ease-out cubic
                _enclosureDoorPivot.localRotation = Quaternion.Slerp(start, end, ease);
                yield return null;
            }
            _enclosureDoorPivot.localRotation = end;
            _doorRoutine = null;
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

            // Floor and wall materials are now owned by RoomMaterials (photo-matched
            // cream/white palette) and applied via LabRoomGeometry — see
            // CreateFloorAndWalls().

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
            // Delegate the empty room shell (floor, walls, ceiling, windows,
            // blinds, doors, skirting, fluorescent panels) to LabRoomGeometry
            // so LabSceneBuilder stays focused on HVOF equipment.
            LabRoomGeometry.Build(transform, LabRoomSpec.FromSketch(), RoomMaterials.PhotoMatch());
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
            var pgo = new GameObject($"DIY_Laser_Enclosure");
            pgo.transform.SetParent(_chamberRoot.transform, false);
            
            // Counteract ArrangeEquipmentForRoom's (0.25, 0.40, 0.25) scale.
            // We apply a 2x multiplier here because a true 46cm box on the floor 
            // looks too small next to industrial lab equipment.
            float targetScale = 2.0f;
            pgo.transform.localScale = new Vector3(targetScale / 0.25f, targetScale / 0.40f, targetScale / 0.25f);
            
            // Raise it so it sits on a workbench (0.85m tall in world space)
            // Local Y offset = world_Y / final_Y_scale = 0.85 / targetScale
            pgo.transform.localPosition = new Vector3(0, 0.85f / targetScale, 0);
            
            // Create a simple workbench underneath it
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Enclosure_Workbench";
            bench.transform.SetParent(pgo.transform);
            // Bench is 1.2 x 0.85 x 1.2 in world space -> local scale divided by targetScale
            bench.transform.localScale = new Vector3(1.2f / targetScale, 0.85f / targetScale, 1.2f / targetScale);
            bench.transform.localPosition = new Vector3(0, -0.85f / targetScale / 2f, 0);
            
            var benchMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            benchMat.color = new Color(0.15f, 0.15f, 0.16f);
            benchMat.SetFloat("_Metallic", 0.5f);
            benchMat.SetFloat("_Smoothness", 0.2f);
            bench.GetComponent<Renderer>().material = benchMat;

            Chamber = pgo;

            if (type == ChamberType.OpenAir) return; // Nothing to draw

            const float S = 0.1f; // 1 Blender unit = 10 cm = 0.1 meters in Unity

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            var mAlu = new Material(shader) { color = new Color(0.78f, 0.78f, 0.80f) };
            mAlu.SetFloat("_Metallic", 0.95f); mAlu.SetFloat("_Smoothness", 0.70f);

            var mBlkSteel = new Material(shader) { color = new Color(0.04f, 0.04f, 0.04f) };
            mBlkSteel.SetFloat("_Metallic", 0.40f); mBlkSteel.SetFloat("_Smoothness", 0.45f);

            var mBlkPlast = new Material(shader) { color = new Color(0.05f, 0.05f, 0.05f) };
            mBlkPlast.SetFloat("_Metallic", 0.10f); mBlkPlast.SetFloat("_Smoothness", 0.30f);

            var mYellow = new Material(shader) { color = new Color(0.97f, 0.85f, 0.05f) };
            mYellow.SetFloat("_Metallic", 0.0f); mYellow.SetFloat("_Smoothness", 0.45f);

            var mRedGlow = new Material(shader) { color = new Color(1.0f, 0.05f, 0.05f) };
            mRedGlow.EnableKeyword("_EMISSION");
            mRedGlow.SetColor("_EmissionColor", new Color(1.0f, 0.05f, 0.05f) * 6.0f);

            var mTxt = new Material(shader) { color = new Color(0.02f, 0.02f, 0.02f) };
            
            var mSteel = new Material(shader) { color = new Color(0.65f, 0.65f, 0.67f) };
            mSteel.SetFloat("_Metallic", 1.0f); mSteel.SetFloat("_Smoothness", 0.70f);

            var mHandle = new Material(shader) { color = new Color(0.20f, 0.20f, 0.22f) };
            mHandle.SetFloat("_Metallic", 0.85f); mHandle.SetFloat("_Smoothness", 0.70f);

            System.Func<string, Vector3, Vector3, Material, Transform, GameObject> AddBox = (name, center, size, mat, parent) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent);
                go.transform.localScale = new Vector3(size.x, size.z, size.y) * S;
                // Map Blender (X, Y, Z) -> Unity (X, Z, Y)
                go.transform.localPosition = new Vector3(center.x, center.z, center.y) * S;
                go.GetComponent<Renderer>().material = mat;
                return go;
            };

            System.Func<string, Vector3, float, float, char, Material, Transform, GameObject> AddCyl = (name, center, radius, length, axis, mat, parent) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(parent);
                if (axis == 'X')
                {
                    go.transform.localScale = new Vector3(radius * 2 * S, length / 2 * S, radius * 2 * S);
                    go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                else if (axis == 'Y')
                {
                    go.transform.localScale = new Vector3(radius * 2 * S, length / 2 * S, radius * 2 * S);
                    go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                else // Z
                {
                    go.transform.localScale = new Vector3(radius * 2 * S, length / 2 * S, radius * 2 * S);
                }
                go.transform.localPosition = new Vector3(center.x, center.z, center.y) * S;
                go.GetComponent<Renderer>().material = mat;
                return go;
            };

            System.Action<string, string, float, Vector3, Material, TextAlignmentOptions, Transform> AddText = (name, text, size, loc, mat, align, parent) =>
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent);
                go.transform.localPosition = new Vector3(loc.x, loc.z, loc.y) * S;
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = size; 
                tmp.color = mat.color;
                tmp.fontStyle = FontStyles.Bold;
                
                var rect = tmp.rectTransform;
                if (align == TextAlignmentOptions.BottomLeft)
                {
                    rect.pivot = new Vector2(0, 0);
                    tmp.alignment = TextAlignmentOptions.BottomLeft;
                }
                rect.sizeDelta = new Vector2(5f, 5f);
            };

            float EXT = 4.6f;
            float EXT_T = 0.30f;
            float PANEL_T = 0.02f;
            float HALF = EXT / 2f;
            float POST_OFF = HALF - EXT_T / 2f;
            float INNER = EXT - 2 * EXT_T;

            Transform root = pgo.transform;

            // POSTS
            float[] sxArr = { -1, 1, 1, -1 };
            float[] syArr = { -1, -1, 1, 1 };
            for (int i = 0; i < 4; i++)
            {
                AddBox($"Post_{i}", new Vector3(sxArr[i]*POST_OFF, syArr[i]*POST_OFF, EXT/2f), new Vector3(EXT_T, EXT_T, EXT), mAlu, root);
            }
            float[] signArr = { -1, 1 };
            foreach (float sy in signArr)
                AddBox($"TopX_{sy}", new Vector3(0, sy*POST_OFF, EXT - EXT_T/2f), new Vector3(EXT - 2*EXT_T, EXT_T, EXT_T), mAlu, root);
            foreach (float sx in signArr)
                AddBox($"TopY_{sx}", new Vector3(sx*POST_OFF, 0, EXT - EXT_T/2f), new Vector3(EXT_T, EXT - 2*EXT_T, EXT_T), mAlu, root);
            foreach (float sy in signArr)
                AddBox($"BotX_{sy}", new Vector3(0, sy*POST_OFF, EXT_T/2f), new Vector3(EXT - 2*EXT_T, EXT_T, EXT_T), mAlu, root);
            foreach (float sx in signArr)
                AddBox($"BotY_{sx}", new Vector3(sx*POST_OFF, 0, EXT_T/2f), new Vector3(EXT_T, EXT - 2*EXT_T, EXT_T), mAlu, root);

            float back_y = -HALF + EXT_T + PANEL_T/2f;
            float left_x = -HALF + EXT_T + PANEL_T/2f;
            float right_x = HALF - EXT_T - PANEL_T/2f;
            float top_z = EXT - EXT_T - PANEL_T/2f;
            float bot_z = EXT_T + PANEL_T/2f;

            AddBox("Panel_Back", new Vector3(0, back_y, EXT/2f), new Vector3(INNER, PANEL_T, INNER), mBlkSteel, root);
            AddBox("Panel_Left", new Vector3(left_x, 0, EXT/2f), new Vector3(PANEL_T, INNER, INNER), mBlkSteel, root);
            AddBox("Panel_Right", new Vector3(right_x, 0, EXT/2f), new Vector3(PANEL_T, INNER, INNER), mBlkSteel, root);
            AddBox("Panel_Top", new Vector3(0, 0, top_z), new Vector3(INNER, INNER, PANEL_T), mBlkSteel, root);
            AddBox("Panel_Bottom", new Vector3(0, 0, bot_z), new Vector3(INNER, INNER, PANEL_T), mBlkSteel, root);

            // DOOR — fills the entire front face, opens outward
            // The front face is at Blender +Y = Unity +Z.
            // Hinge on the left edge (Blender -X side).
            float DOOR_W = 32f;   // full width
            float DOOR_H = 20f;   // full height  
            float DOOR_T = PANEL_T;
            float frontZ = (HALF - EXT_T) * S;   // front face Z in Unity local
            float hingeX = (-HALF + EXT_T) * S;   // left edge X in Unity local
            float midY   = (EXT / 2f) * S;        // center height Y in Unity local

            var doorRoot = new GameObject("DoorPivot");
            doorRoot.transform.SetParent(root);
            doorRoot.transform.localPosition = new Vector3(hingeX, midY, frontZ);
            
            // Door panel: extends from hinge along +X, centered on Y, thin along Z
            var doorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorGo.name = "Door";
            doorGo.transform.SetParent(doorRoot.transform);
            doorGo.transform.localScale = new Vector3(DOOR_W * S, DOOR_H * S, DOOR_T * S);
            doorGo.transform.localPosition = new Vector3(DOOR_W / 2f * S, 0f, 0f);
            doorGo.GetComponent<Renderer>().material = mBlkSteel;

            // Handle bar on the right side of the door (far from hinge)
            float handleX = (DOOR_W - 0.40f) * S;
            var handleBar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handleBar.name = "Handle_Bar";
            handleBar.transform.SetParent(doorRoot.transform);
            handleBar.transform.localScale = new Vector3(0.04f * S, 0.40f * S, 0.04f * S);
            handleBar.transform.localPosition = new Vector3(handleX, 0f, DOOR_T * S);
            handleBar.GetComponent<Renderer>().material = mHandle;

            // Handle standoffs (top + bottom)
            foreach (float hOff in new[] { 0.35f, -0.35f })
            {
                var standoff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                standoff.name = $"Handle_SO_{hOff}";
                standoff.transform.SetParent(doorRoot.transform);
                standoff.transform.localScale = new Vector3(0.04f * S, 0.08f * S, 0.04f * S);
                standoff.transform.localPosition = new Vector3(handleX, hOff * S, DOOR_T / 2f * S);
                standoff.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                standoff.GetComponent<Renderer>().material = mHandle;
            }



            // Hinges on the frame
            foreach (float hz in new[] { 0.20f, 0.80f })
            {
                var hinge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hinge.name = "Hinge";
                hinge.transform.SetParent(root);
                hinge.transform.localScale = new Vector3(0.05f * S, 0.09f * S, 0.05f * S);
                hinge.transform.localPosition = new Vector3(hingeX, hz * EXT * S, frontZ + 0.003f);
                hinge.GetComponent<Renderer>().material = mSteel;
            }

            // Start closed; clicks toggle open/close (swings outward = -90°)
            doorRoot.transform.localRotation = Quaternion.identity;
            _enclosureDoorPivot = doorRoot.transform;

            float LED_X = HALF - 0.50f, LED_Y = HALF - 0.50f;
            AddCyl("LED_Base", new Vector3(LED_X, LED_Y, EXT + 0.08f), 0.10f, 0.16f, 'Z', mBlkPlast, root);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "LED_Dome";
            dome.transform.SetParent(root);
            dome.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f) * S;
            dome.transform.localPosition = new Vector3(LED_X, EXT + 0.16f, LED_Y) * S; // Y is front -> Z, Z is UP -> Y
            dome.GetComponent<Renderer>().material = mRedGlow;

            float SW_X = -HALF + EXT_T + 0.15f;
            float SW_Y = HALF - EXT_T - 0.10f;
            float SW_Z = EXT - EXT_T - 0.20f;
            AddBox("Interlock_Body", new Vector3(SW_X, SW_Y, SW_Z), new Vector3(0.20f, 0.10f, 0.20f), mBlkPlast, root);
            AddBox("Interlock_Lever", new Vector3(SW_X + 0.13f, SW_Y, SW_Z), new Vector3(0.06f, 0.02f, 0.04f), mSteel, root);
            AddCyl("Interlock_Wire", new Vector3(SW_X, SW_Y - 0.05f, SW_Z - 0.10f), 0.015f, 0.20f, 'Z', mBlkPlast, root);

            // Detailed XY positioning stage (translated from Blender BOM)
            float xyBaseZ = bot_z + PANEL_T / 2f + 0.02f;
            CreateXYTable(root, xyBaseZ, S);

            float beam_top_z = EXT - EXT_T - PANEL_T;
            AddCyl("BeamHead", new Vector3(0, 0, beam_top_z - 0.50f), 0.18f, 1.00f, 'Z', mAlu, root);
            AddCyl("BeamLens", new Vector3(0, 0, beam_top_z - 1.05f), 0.10f, 0.10f, 'Z', mSteel, root);

            SafetyEnclosure = pgo;

            // Optional: override the old chamber LED binding
            _chamberLED = dome.AddComponent<Light>();
            _chamberLED.type = LightType.Point;
            _chamberLED.color = Color.red;
            _chamberLED.intensity = 8f;
            _chamberLED.range = 2f;
            _doorLED = _chamberLED; // Map both to the same dome for simplicity
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

        // ── Detailed XY Positioning Stage ─────────────────────────────
        // Translated from the Blender BOM script.  1 BU = 10 cm.
        // Components: 4×2020 extrusions, 2×MGN12 rail+carriage per axis,
        //   2×T8 lead screws, 4×KP08 pillow blocks, 2×NEMA 17 motors,
        //   2×jaw couplings, 4×limit switches, 80×80 mm sample plate.
        private void CreateXYTable(Transform parent, float baseZ, float S)
        {
            var xyRoot = new GameObject("XY_PositioningTable");
            xyRoot.transform.SetParent(parent);
            xyRoot.transform.localPosition = new Vector3(0, baseZ * S, 0);
            xyRoot.transform.localScale = Vector3.one;
            Transform rt = xyRoot.transform;

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            // ── Materials ──
            var mBlkAno   = new Material(shader) { color = new Color(0.06f, 0.06f, 0.07f) };
            mBlkAno.SetFloat("_Metallic", 0.85f); mBlkAno.SetFloat("_Smoothness", 0.50f);
            var mAlu      = new Material(shader) { color = new Color(0.78f, 0.78f, 0.80f) };
            mAlu.SetFloat("_Metallic", 0.95f); mAlu.SetFloat("_Smoothness", 0.70f);
            var mAluD     = new Material(shader) { color = new Color(0.55f, 0.55f, 0.58f) };
            mAluD.SetFloat("_Metallic", 0.95f); mAluD.SetFloat("_Smoothness", 0.55f);
            var mStl      = new Material(shader) { color = new Color(0.84f, 0.84f, 0.86f) };
            mStl.SetFloat("_Metallic", 1.0f); mStl.SetFloat("_Smoothness", 0.82f);
            var mDStl     = new Material(shader) { color = new Color(0.28f, 0.28f, 0.30f) };
            mDStl.SetFloat("_Metallic", 0.90f); mDStl.SetFloat("_Smoothness", 0.60f);
            var mBrass    = new Material(shader) { color = new Color(0.82f, 0.65f, 0.30f) };
            mBrass.SetFloat("_Metallic", 0.85f); mBrass.SetFloat("_Smoothness", 0.65f);
            var mPlast    = new Material(shader) { color = new Color(0.05f, 0.05f, 0.05f) };
            mPlast.SetFloat("_Metallic", 0.10f); mPlast.SetFloat("_Smoothness", 0.35f);
            var mRed      = new Material(shader) { color = new Color(0.78f, 0.10f, 0.10f) };
            mRed.SetFloat("_Metallic", 0.10f); mRed.SetFloat("_Smoothness", 0.55f);
            var mBolt     = new Material(shader) { color = new Color(0.40f, 0.40f, 0.42f) };
            mBolt.SetFloat("_Metallic", 0.90f); mBolt.SetFloat("_Smoothness", 0.60f);
            var mCable    = new Material(shader) { color = new Color(0.18f, 0.18f, 0.20f) };

            // ── Helpers (Blender XYZ where Z=up → Unity XZY) ──
            System.Func<string, Vector3, Vector3, Material, GameObject> B = (n, c, sz, m) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n; go.transform.SetParent(rt);
                go.transform.localScale    = new Vector3(sz.x, sz.z, sz.y) * S;
                go.transform.localPosition = new Vector3(c.x, c.z, c.y) * S;
                go.GetComponent<Renderer>().material = m;
                return go;
            };
            System.Func<string, Vector3, float, float, char, Material, GameObject> C = (n, c, rad, len, ax, m) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = n; go.transform.SetParent(rt);
                go.transform.localScale = new Vector3(rad * 2f * S, len / 2f * S, rad * 2f * S);
                if (ax == 'X') go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                else if (ax == 'Y') go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                go.transform.localPosition = new Vector3(c.x, c.z, c.y) * S;
                go.GetComponent<Renderer>().material = m;
                return go;
            };

            // ── Dimensions (Blender units, 1 BU = 10 cm) ──
            // Sized for 50×50 mm samples: 360 mm base, 300 mm travel.
            const float ET = 0.20f;                         // 2020 cross-section
            const float BW = 3.60f, BD = 3.60f;             // base frame (360 mm)
            const float RW = 0.12f, RH = 0.08f;             // MGN12 rail
            const float CL = 0.27f, CW = 0.27f, CH = 0.10f; // carriage
            const float LR = 0.04f, LL = 3.00f;             // T8 lead screw (300 mm)
            const float KW = 0.30f, KL = 0.40f, KH = 0.30f; // KP08
            const float NS = 0.42f, NL = 0.48f;             // NEMA 17
            const float CR = 0.075f, CpL = 0.25f;           // coupling
            const float SW = 1.00f, SH = 0.05f;             // sample plate (100 mm)
            const float XPT = 0.05f;                         // X plate thickness

            // Z anchors
            float ETopZ  = ET;                    // 0.20
            float RCtrZ  = ETopZ + RH / 2f;      // 0.24
            float CTopZ  = ETopZ + CH;            // 0.30
            float XPlZ   = CTopZ + XPT / 2f;     // 0.325
            float LXZ    = 0.35f;                 // lead-screw axis

            float YBarLen = BD - 2f * ET;         // 3.20
            float RXY1 = -BD / 2f + ET / 2f;     // -1.70  (front rail Y)
            float RXY2 = +BD / 2f - ET / 2f;     // +1.70  (back  rail Y)
            float XCX  = 0.30f;                   // X-carriage offset

            // ════════════════ BASE FRAME ════════════════
            B("Ext_X_Front", new Vector3(0, -BD/2f+ET/2f, ET/2f), new Vector3(BW, ET, ET), mBlkAno);
            B("Ext_X_Back",  new Vector3(0, +BD/2f-ET/2f, ET/2f), new Vector3(BW, ET, ET), mBlkAno);
            B("Ext_Y_Left",  new Vector3(-BW/2f+ET/2f, 0, ET/2f), new Vector3(ET, YBarLen, ET), mBlkAno);
            B("Ext_Y_Right", new Vector3(+BW/2f-ET/2f, 0, ET/2f), new Vector3(ET, YBarLen, ET), mBlkAno);
            // T-slot grooves
            for (int si = 0; si < 2; si++)
            {
                float sy = si == 0 ? -1f : 1f;
                B($"Slot_Top_{si}", new Vector3(0, sy*(BD/2f-ET/2f), ET+0.0005f), new Vector3(BW*0.97f, 0.05f, 0.005f), mDStl);
            }

            // ════════════════ X-AXIS ════════════════
            // Rails
            for (int i = 0; i < 2; i++)
            {
                float ry = i == 0 ? RXY1 : RXY2;
                B($"X_Rail_{i}",     new Vector3(0, ry, RCtrZ), new Vector3(LL, RW, RH), mStl);
                B($"X_Carriage_{i}", new Vector3(XCX, ry, ETopZ+CH/2f), new Vector3(CL, CW, CH), mDStl);
            }
            // X-carriage plate
            B("X_Plate", new Vector3(XCX, 0, XPlZ), new Vector3(1.80f, 3.80f, XPT), mAlu);
            // Lead screw + nut
            C("X_LeadScrew", new Vector3(0, 0, LXZ), LR, LL, 'X', mStl);
            C("X_LeadNut",   new Vector3(XCX, 0, LXZ), LR*1.7f, 0.20f, 'X', mBrass);
            B("X_NutBracket", new Vector3(XCX, 0, (LXZ+LR*1.7f+XPlZ-XPT/2f)/2f+0.005f),
                new Vector3(0.18f, 0.30f, Mathf.Max(0.04f, XPlZ-XPT/2f-LXZ-LR*1.7f)), mAluD);
            // KP08 pillow blocks
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                float cx = sx * (LL / 2f + 0.05f);
                B($"X_KP08_{i}",       new Vector3(cx, 0, LXZ), new Vector3(KL, KW, KH), mDStl);
                C($"X_KP08_Brg_{i}",   new Vector3(cx, 0, LXZ), LR*1.55f, KL*0.55f, 'X', mStl);
                B($"X_KP08_Flange_{i}",new Vector3(cx, 0, ETopZ+0.01f), new Vector3(KL*1.1f, KW*1.2f, 0.02f), mAluD);
            }
            // NEMA 17 + mount + coupling
            float NXX  = LL/2f+0.05f+CpL+NL/2f;  // 1.54
            float CpX  = LL/2f+0.05f+CpL/2f;     // 1.175
            B("X_NEMA17",     new Vector3(NXX, 0, LXZ), new Vector3(NL, NS, NS), mDStl);
            B("X_NEMA_Band",  new Vector3(NXX, 0, LXZ), new Vector3(NL*0.30f, NS*1.001f, NS*1.001f), mPlast);
            B("X_MotorMount", new Vector3(NXX-NL/2f-0.025f, 0, LXZ), new Vector3(0.04f, NS*1.2f, NS*1.2f), mAlu);
            C("X_NEMA_Shaft", new Vector3(LL/2f+0.05f+CpL*0.25f, 0, LXZ), 0.025f, CpL*0.7f, 'X', mStl);
            C("X_Coupling",   new Vector3(CpX, 0, LXZ), CR, CpL, 'X', mDStl);
            // Motor mount bolts
            foreach (float bsy in new[]{-1f,1f})
                foreach (float bsz in new[]{-1f,1f})
                    C($"X_MBolt_{bsy}_{bsz}", new Vector3(NXX-NL/2f-0.05f, bsy*NS*0.42f, LXZ+bsz*NS*0.42f), 0.025f, 0.04f, 'X', mBolt);
            C("X_MotorCable", new Vector3(NXX+NL/2f+0.04f, NS*0.30f, LXZ-0.10f), 0.025f, 0.40f, 'Z', mCable);
            // X limit switches
            for (int i = 0; i < 2; i++)
            {
                float lsx = (i == 0 ? -1f : 1f) * (LL / 2f - 0.10f);
                B($"X_Limit_{i}",      new Vector3(lsx, RXY2+0.13f, ETopZ+0.05f), new Vector3(0.20f, 0.10f, 0.10f), mPlast);
                B($"X_Limit_Lever_{i}",new Vector3(lsx, RXY2+0.20f, ETopZ+0.07f), new Vector3(0.10f, 0.02f, 0.005f), mStl);
                C($"X_Limit_Tip_{i}",  new Vector3(lsx+0.04f, RXY2+0.21f, ETopZ+0.07f), 0.012f, 0.012f, 'Z', mRed);
            }

            // ════════════════ Y-AXIS (on X-plate) ════════════════
            float YBZ   = XPlZ + XPT / 2f;        // top of X plate
            float YRCZ  = YBZ + RH / 2f;
            float YCTZ  = YBZ + CH;
            float LYZ   = YBZ + 0.15f;
            float YRL   = 2.50f;                   // Y rail length (250 mm)
            float YRX1  = XCX - 0.45f;
            float YRX2  = XCX + 0.45f;
            float YCY   = -0.20f;                  // Y carriage offset
            float YLSL  = 2.20f;                   // Y lead-screw length (220 mm)

            // Rails + carriages
            for (int i = 0; i < 2; i++)
            {
                float rx = i == 0 ? YRX1 : YRX2;
                B($"Y_Rail_{i}",     new Vector3(rx, 0, YRCZ), new Vector3(RW, YRL, RH), mStl);
                B($"Y_Carriage_{i}", new Vector3(rx, YCY, YBZ+CH/2f), new Vector3(CW, CL, CH), mDStl);
            }
            // Y plate
            float YPT = 0.05f;
            float YPZ = YCTZ + YPT / 2f;
            B("Y_Plate", new Vector3(XCX, YCY, YPZ), new Vector3(1.40f, 1.20f, YPT), mAlu);
            // Lead screw + nut
            C("Y_LeadScrew", new Vector3(XCX, 0, LYZ), LR, YLSL, 'Y', mStl);
            C("Y_LeadNut",   new Vector3(XCX, YCY, LYZ), LR*1.7f, 0.20f, 'Y', mBrass);
            B("Y_NutBracket", new Vector3(XCX, YCY, (LYZ+LR*1.7f+YPZ-YPT/2f)/2f+0.005f),
                new Vector3(0.30f, 0.18f, Mathf.Max(0.04f, YPZ-YPT/2f-LYZ-LR*1.7f)), mAluD);
            // KP08
            for (int i = 0; i < 2; i++)
            {
                float sy = (i == 0 ? -1f : 1f) * (YLSL / 2f + 0.05f);
                B($"Y_KP08_{i}",       new Vector3(XCX, sy, LYZ), new Vector3(KW, KL, KH), mDStl);
                C($"Y_KP08_Brg_{i}",   new Vector3(XCX, sy, LYZ), LR*1.55f, KL*0.55f, 'Y', mStl);
                B($"Y_KP08_Flange_{i}",new Vector3(XCX, sy, YBZ+0.01f), new Vector3(KW*1.2f, KL*1.1f, 0.02f), mAluD);
            }
            // NEMA 17 + mount + coupling
            float NYY = YLSL/2f+0.05f+CpL+NL/2f;
            float CpY = YLSL/2f+0.05f+CpL/2f;
            B("Y_NEMA17",     new Vector3(XCX, NYY, LYZ), new Vector3(NS, NL, NS), mDStl);
            B("Y_NEMA_Band",  new Vector3(XCX, NYY, LYZ), new Vector3(NS*1.001f, NL*0.30f, NS*1.001f), mPlast);
            B("Y_MotorMount", new Vector3(XCX, NYY-NL/2f-0.025f, LYZ), new Vector3(NS*1.2f, 0.04f, NS*1.2f), mAlu);
            C("Y_NEMA_Shaft", new Vector3(XCX, YLSL/2f+0.05f+CpL*0.25f, LYZ), 0.025f, CpL*0.7f, 'Y', mStl);
            C("Y_Coupling",   new Vector3(XCX, CpY, LYZ), CR, CpL, 'Y', mDStl);
            foreach (float bsx in new[]{-1f,1f})
                foreach (float bsz in new[]{-1f,1f})
                    C($"Y_MBolt_{bsx}_{bsz}", new Vector3(XCX+bsx*NS*0.42f, NYY-NL/2f-0.05f, LYZ+bsz*NS*0.42f), 0.025f, 0.04f, 'Y', mBolt);
            C("Y_MotorCable", new Vector3(XCX+NS*0.30f, NYY+NL/2f+0.04f, LYZ-0.10f), 0.025f, 0.40f, 'Z', mCable);
            // Y limit switches
            for (int i = 0; i < 2; i++)
            {
                float lsy = (i == 0 ? -1f : 1f) * (YLSL / 2f - 0.10f);
                B($"Y_Limit_{i}",      new Vector3(YRX2+0.13f, lsy, YBZ+0.05f), new Vector3(0.10f, 0.20f, 0.10f), mPlast);
                B($"Y_Limit_Lever_{i}",new Vector3(YRX2+0.20f, lsy, YBZ+0.07f), new Vector3(0.02f, 0.10f, 0.005f), mStl);
                C($"Y_Limit_Tip_{i}",  new Vector3(YRX2+0.21f, lsy+0.04f, YBZ+0.07f), 0.012f, 0.012f, 'Z', mRed);
            }

            // ════════════════ SAMPLE PLATE ════════════════
            float SPZ = YPZ + YPT / 2f + SH / 2f;
            Table = B("SamplePlate", new Vector3(XCX, YCY, SPZ), new Vector3(SW, SW, SH), mAlu);
            // M4 hole pattern (3×3 minus center)
            for (int hx = -1; hx <= 1; hx++)
                for (int hy = -1; hy <= 1; hy++)
                {
                    if (hx == 0 && hy == 0) continue;
                    C($"SampleHole_{hx}_{hy}",
                        new Vector3(XCX+hx*SW*0.35f, YCY+hy*SW*0.35f, SPZ+SH/2f-0.003f),
                        0.022f, 0.012f, 'Z', mPlast);
                }
            // Center crosshair (laser target)
            C("SampleTarget", new Vector3(XCX, YCY, SPZ+SH/2f+0.0005f), 0.06f, 0.001f, 'Z', mRed);

            // Workpiece on sample plate
            Workpiece = B("Workpiece_50x50", new Vector3(XCX, YCY, SPZ+SH/2f+0.01f), new Vector3(0.50f, 0.50f, 0.02f), mStl);

            // Table-homed LED on X motor
            _tableHomedLED = CreateLEDIndicator(rt, new Vector3(NXX * S, (LXZ + NS * 0.55f) * S, 0), new Color(1f, 0.5f, 0f));
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
            // ── IPG YLR-1000 — exact datasheet dimensions (6U rack) ─────
            // Real: 448 × 497 × 266 mm, < 50 kg.
            // ArrangeEquipmentForRoom() applies (0.25x, 0.45y, 0.25z).
            // Local values = real_mm / 1000 / scale_factor.
            //   W = 0.448 / 0.25 = 1.792   D = 0.497 / 0.25 = 1.988   H = 0.266 / 0.45 = 0.591

            const float W = 1.792f, D = 1.988f, H = 0.591f;
            float LEFT = -W / 2f, RIGHT = W / 2f;
            float TOP = H;

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            // — Materials (matching reference photo palette) —
            var matBlue = new Material(shader) { color = new Color(0.02f, 0.05f, 0.30f) };
            matBlue.SetFloat("_Metallic", 0.85f); matBlue.SetFloat("_Smoothness", 0.65f);

            var matGray = new Material(shader) { color = new Color(0.78f, 0.78f, 0.80f) };
            matGray.SetFloat("_Metallic", 0.70f); matGray.SetFloat("_Smoothness", 0.55f);

            var matBlack = new Material(shader) { color = new Color(0.02f, 0.02f, 0.02f) };
            matBlack.SetFloat("_Metallic", 0.20f); matBlack.SetFloat("_Smoothness", 0.35f);

            var matGreen = new Material(shader) { color = new Color(0.05f, 0.65f, 0.10f) };
            matGreen.SetFloat("_Metallic", 0.10f); matGreen.SetFloat("_Smoothness", 0.60f);

            var matRed = new Material(shader) { color = new Color(0.75f, 0.05f, 0.05f) };
            matRed.SetFloat("_Metallic", 0.10f); matRed.SetFloat("_Smoothness", 0.60f);

            var matLCD = new Material(shader) { color = new Color(0.05f, 0.10f, 0.20f) };
            matLCD.EnableKeyword("_EMISSION");
            matLCD.SetColor("_EmissionColor", new Color(0.05f, 0.10f, 0.20f) * 0.3f);
            matLCD.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            var matMetal = new Material(shader) { color = new Color(0.55f, 0.55f, 0.58f) };
            matMetal.SetFloat("_Metallic", 1.0f); matMetal.SetFloat("_Smoothness", 0.70f);

            // — 1. Main chassis —
            var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "LaserCabinet";
            chassis.transform.SetParent(parent);
            chassis.transform.localScale = new Vector3(W, H, D);
            chassis.transform.localPosition = new Vector3(-1.5f, 1.74f + H / 2f, -5f);
            chassis.GetComponent<Renderer>().material = matBlue;

            // — 2. Faceplate (flush against chassis front) —
            float FP_T = 0.025f;
            float FP_W = W * 0.99f, FP_H = H * 0.99f;
            var faceplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceplate.name = "IPG_Faceplate";
            faceplate.transform.SetParent(parent);
            faceplate.transform.localScale = new Vector3(FP_W, FP_H, FP_T);
            faceplate.transform.localPosition = chassis.transform.localPosition
                + new Vector3(0, 0, -D / 2f - FP_T / 2f);
            faceplate.GetComponent<Renderer>().material = matGray;

            float fpFrontZ = faceplate.transform.localPosition.z - FP_T / 2f;

            // — 3. Left control panel cluster —
            float CP_W = W * 0.22f, CP_H = H * 0.85f, CP_T = 0.018f;
            float CP_X = chassis.transform.localPosition.x + LEFT + 0.08f + CP_W / 2f;
            float CP_Z_pos = chassis.transform.localPosition.y; // centred vertically

            var controlPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            controlPanel.name = "IPG_ControlPanel";
            controlPanel.transform.SetParent(parent);
            controlPanel.transform.localScale = new Vector3(CP_W, CP_H, CP_T);
            controlPanel.transform.localPosition = new Vector3(CP_X, CP_Z_pos, fpFrontZ - CP_T / 2f + 0.001f);
            controlPanel.GetComponent<Renderer>().material = matBlack;

            float cpFrontZ = fpFrontZ - CP_T;

            // LCD screen
            float LCD_W = CP_W * 0.55f, LCD_H = CP_H * 0.50f, LCD_T = 0.006f;
            float LCD_X = CP_X + CP_W * 0.20f;
            float LCD_Y = CP_Z_pos + CP_H * 0.05f;
            var lcd = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lcd.name = "IPG_LCD";
            lcd.transform.SetParent(parent);
            lcd.transform.localScale = new Vector3(LCD_W, LCD_H, LCD_T);
            lcd.transform.localPosition = new Vector3(LCD_X, LCD_Y, cpFrontZ - LCD_T / 2f);
            lcd.GetComponent<Renderer>().material = matLCD;

            // Button column X
            float colX = CP_X - CP_W * 0.30f;

            // Green button (cylinder oriented along Z)
            float GR_R = 0.055f, GR_L = 0.03f;
            float GR_Y = CP_Z_pos + CP_H * 0.30f;
            var greenBtn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            greenBtn.name = "IPG_GreenButton";
            greenBtn.transform.SetParent(parent);
            greenBtn.transform.localScale = new Vector3(GR_R * 2f, GR_L / 2f, GR_R * 2f);
            greenBtn.transform.localPosition = new Vector3(colX, GR_Y, cpFrontZ - GR_L / 2f);
            greenBtn.transform.localRotation = Quaternion.Euler(90, 0, 0);
            greenBtn.GetComponent<Renderer>().material = matGreen;

            // Red E-Stop button (base + mushroom cap)
            float RD_R = 0.075f, RD_L = 0.035f;
            float RD_Y = CP_Z_pos + CP_H * 0.02f;
            var redBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            redBase.name = "IPG_RedButtonBase";
            redBase.transform.SetParent(parent);
            redBase.transform.localScale = new Vector3(RD_R * 2f, RD_L / 2f, RD_R * 2f);
            redBase.transform.localPosition = new Vector3(colX, RD_Y, cpFrontZ - RD_L / 2f);
            redBase.transform.localRotation = Quaternion.Euler(90, 0, 0);
            redBase.GetComponent<Renderer>().material = matRed;

            float RD_TOP_R = RD_R * 0.70f, RD_TOP_L = 0.02f;
            var redCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            redCap.name = "IPG_RedButtonCap";
            redCap.transform.SetParent(parent);
            redCap.transform.localScale = new Vector3(RD_TOP_R * 2f, RD_TOP_L, RD_TOP_R * 2f);
            redCap.transform.localPosition = new Vector3(colX, RD_Y, cpFrontZ - RD_L - RD_TOP_L / 2f);
            var redCapMat = new Material(matRed);
            redCapMat.EnableKeyword("_EMISSION");
            redCapMat.SetColor("_EmissionColor", new Color(1.5f, 0.1f, 0.05f));
            redCap.GetComponent<Renderer>().material = redCapMat;

            // Key switch
            float KS_R = 0.03f, KS_L = 0.025f;
            float KS_Y = CP_Z_pos - CP_H * 0.32f;
            var keyBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            keyBody.name = "IPG_KeySwitch";
            keyBody.transform.SetParent(parent);
            keyBody.transform.localScale = new Vector3(KS_R * 2f, KS_L / 2f, KS_R * 2f);
            keyBody.transform.localPosition = new Vector3(colX, KS_Y, cpFrontZ - KS_L / 2f);
            keyBody.transform.localRotation = Quaternion.Euler(90, 0, 0);
            keyBody.GetComponent<Renderer>().material = matMetal;

            var keyBlade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyBlade.name = "IPG_KeyBlade";
            keyBlade.transform.SetParent(parent);
            keyBlade.transform.localScale = new Vector3(0.005f, 0.02f, 0.06f);
            keyBlade.transform.localPosition = new Vector3(colX, KS_Y - 0.02f, cpFrontZ - KS_L - 0.03f);
            keyBlade.GetComponent<Renderer>().material = matMetal;

            // — 4. Right ventilation grille —
            float VENT_W = W * 0.55f, VENT_H = H * 0.70f, VENT_T = 0.015f;
            float VENT_X = chassis.transform.localPosition.x + RIGHT - 0.08f - VENT_W / 2f;
            float VENT_Y = chassis.transform.localPosition.y; // centred

            var ventBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ventBack.name = "IPG_VentBack";
            ventBack.transform.SetParent(parent);
            ventBack.transform.localScale = new Vector3(VENT_W, VENT_H, VENT_T);
            ventBack.transform.localPosition = new Vector3(VENT_X, VENT_Y, fpFrontZ - VENT_T / 2f + 0.001f);
            ventBack.GetComponent<Renderer>().material = matBlack;

            float ventFrontZ = fpFrontZ - VENT_T;

            // 14 horizontal slats
            int NUM_SLATS = 14;
            float slatThickness = VENT_H / (NUM_SLATS * 2.2f);
            float slatGap = (VENT_H - NUM_SLATS * slatThickness) / (NUM_SLATS + 1);
            float topSlatY = VENT_Y + VENT_H / 2f - slatGap - slatThickness / 2f;
            for (int i = 0; i < NUM_SLATS; i++)
            {
                float y = topSlatY - i * (slatThickness + slatGap);
                var slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = $"IPG_VentSlat_{i:D2}";
                slat.transform.SetParent(parent);
                slat.transform.localScale = new Vector3(VENT_W * 0.97f, slatThickness, 0.006f);
                slat.transform.localPosition = new Vector3(VENT_X, y, ventFrontZ - 0.003f);
                slat.GetComponent<Renderer>().material = matGray;
            }

            // — 5. Top hazard labels —
            for (int i = 0; i < 3; i++)
            {
                var label = GameObject.CreatePrimitive(PrimitiveType.Cube);
                label.name = $"IPG_Label_{i}";
                label.transform.SetParent(parent);
                label.transform.localScale = new Vector3(0.17f, 0.003f, 0.13f);
                label.transform.localPosition = chassis.transform.localPosition
                    + new Vector3(-W * 0.18f + i * 0.23f, TOP / 2f + 0.002f, -D * 0.30f);
                label.GetComponent<Renderer>().material = _hazardMat;
            }

            // — 6. Command Desk with PC & Monitor (next to cabinet) ——
            float DESK_TOP_Y = 1.70f;
            float DESK_T_thick = 0.08f;
            float DESK_W_size = 2.0f;
            float DESK_D_size = 4.0f;
            float DESK_X_pos = -1.5f;
            float DESK_Z_pos = chassis.transform.localPosition.z - 3.5f;

            var matDeskTop = new Material(shader) { color = new Color(0.25f, 0.22f, 0.20f) };
            matDeskTop.SetFloat("_Metallic", 0.15f); matDeskTop.SetFloat("_Smoothness", 0.45f);
            var matDeskLeg = new Material(shader) { color = new Color(0.18f, 0.18f, 0.20f) };
            matDeskLeg.SetFloat("_Metallic", 0.80f); matDeskLeg.SetFloat("_Smoothness", 0.50f);

            // Desk top surface
            var deskTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deskTop.name = "CommandDesk_Top";
            deskTop.transform.SetParent(parent);
            deskTop.transform.localScale = new Vector3(DESK_W_size, DESK_T_thick, DESK_D_size);
            deskTop.transform.localPosition = new Vector3(DESK_X_pos, DESK_TOP_Y, DESK_Z_pos);
            deskTop.GetComponent<Renderer>().material = matDeskTop;

            // Desk legs (4 corners)
            float dLegH = DESK_TOP_Y - DESK_T_thick / 2f;
            float dHalfW = DESK_W_size / 2f - 0.15f;
            float dHalfD = DESK_D_size / 2f - 0.15f;
            for (int li = 0; li < 4; li++)
            {
                float lx = (li % 2 == 0 ? -1 : 1) * dHalfW + DESK_X_pos;
                float lz = (li < 2 ? -1 : 1) * dHalfD + DESK_Z_pos;
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"CommandDesk_Leg_{li}";
                leg.transform.SetParent(parent);
                leg.transform.localScale = new Vector3(0.12f, dLegH, 0.12f);
                leg.transform.localPosition = new Vector3(lx, dLegH / 2f, lz);
                leg.GetComponent<Renderer>().material = matDeskLeg;
            }

            // PC Tower (on the floor next to the desk)
            float pcX = DESK_X_pos + 1.2f;
            float pcH = 1.10f;
            var pcCase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pcCase.name = "PC_Tower";
            pcCase.transform.SetParent(parent);
            pcCase.transform.localScale = new Vector3(0.80f, pcH, 1.60f);
            pcCase.transform.localPosition = new Vector3(pcX, pcH / 2f, DESK_Z_pos);
            pcCase.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            pcCase.GetComponent<Renderer>().material = matBlack;

            // PC front panel accent
            var pcFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pcFront.name = "PC_FrontPanel";
            pcFront.transform.SetParent(parent);
            pcFront.transform.localScale = new Vector3(0.72f, 0.33f, 0.02f);
            pcFront.transform.localPosition = new Vector3(pcX + 0.81f, pcH * 0.75f, DESK_Z_pos);
            pcFront.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            pcFront.GetComponent<Renderer>().material = matGray;

            // PC power LED
            var pcLed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pcLed.name = "PC_PowerLED";
            pcLed.transform.SetParent(parent);
            pcLed.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            pcLed.transform.localPosition = new Vector3(pcX + 0.82f, pcH * 0.82f, DESK_Z_pos - 0.24f);
            var pcLedMat = new Material(shader);
            pcLedMat.color = new Color(0.1f, 0.8f, 0.2f);
            pcLedMat.EnableKeyword("_EMISSION");
            pcLedMat.SetColor("_EmissionColor", new Color(0.2f, 2.0f, 0.4f));
            pcLed.GetComponent<Renderer>().material = pcLedMat;

            // Monitor (center of desk — widescreen 16:9)
            float monX = DESK_X_pos;
            float monStandH = 0.35f;
            float monW = 1.80f, monH = 0.50f, monT = 0.04f;

            // Monitor stand base
            var monBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            monBase.name = "Monitor_StandBase";
            monBase.transform.SetParent(parent);
            monBase.transform.localScale = new Vector3(0.45f, 0.03f, 0.45f);
            monBase.transform.localPosition = new Vector3(monX, DESK_TOP_Y + DESK_T_thick / 2f + 0.03f, DESK_Z_pos);
            monBase.GetComponent<Renderer>().material = matBlack;

            // Monitor stand neck
            var monNeck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            monNeck.name = "Monitor_StandNeck";
            monNeck.transform.SetParent(parent);
            monNeck.transform.localScale = new Vector3(0.08f, monStandH / 2f, 0.08f);
            monNeck.transform.localPosition = new Vector3(monX, DESK_TOP_Y + DESK_T_thick / 2f + 0.06f + monStandH / 2f, DESK_Z_pos);
            monNeck.GetComponent<Renderer>().material = matBlack;

            // Monitor bezel (behind screen for depth)
            float monScreenY = DESK_TOP_Y + DESK_T_thick / 2f + 0.06f + monStandH + monH / 2f;
            var monBezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monBezel.name = "Monitor_Bezel";
            monBezel.transform.SetParent(parent);
            monBezel.transform.localScale = new Vector3(monW + 0.08f, monH + 0.08f, monT + 0.02f);
            monBezel.transform.localPosition = new Vector3(monX - 0.015f, monScreenY, DESK_Z_pos);
            monBezel.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            monBezel.GetComponent<Renderer>().material = matBlack;

            // Monitor screen (emissive)
            var monScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monScreen.name = "Monitor_Screen";
            monScreen.transform.SetParent(parent);
            monScreen.transform.localScale = new Vector3(monW, monH, monT);
            monScreen.transform.localPosition = new Vector3(monX + 0.01f, monScreenY, DESK_Z_pos);
            monScreen.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            var monScreenMat = new Material(shader);
            monScreenMat.color = new Color(0.02f, 0.04f, 0.08f);
            monScreenMat.EnableKeyword("_EMISSION");
            monScreenMat.SetColor("_EmissionColor", new Color(0.08f, 0.15f, 0.30f) * 0.5f);
            monScreenMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            monScreen.GetComponent<Renderer>().material = monScreenMat;

            // Keyboard
            var keyboard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyboard.name = "Keyboard";
            keyboard.transform.SetParent(parent);
            keyboard.transform.localScale = new Vector3(1.40f, 0.05f, 0.50f);
            keyboard.transform.localPosition = new Vector3(monX + 1.0f, DESK_TOP_Y + DESK_T_thick / 2f + 0.03f, DESK_Z_pos);
            keyboard.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            keyboard.GetComponent<Renderer>().material = matBlack;

            // Mouse
            var mouse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouse.name = "Mouse";
            mouse.transform.SetParent(parent);
            mouse.transform.localScale = new Vector3(0.22f, 0.04f, 0.35f);
            mouse.transform.localPosition = new Vector3(monX + 0.8f, DESK_TOP_Y + DESK_T_thick / 2f + 0.025f, DESK_Z_pos + 1.0f);
            mouse.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            mouse.GetComponent<Renderer>().material = matBlack;

            // — Processing head (replaces old gantry + cube head) —
            CreateProcessingHead(parent);
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

        // ── Processing Head — translated from Blender script ─────────
        // Vertical stack of aluminum positioning modules + camera + beam delivery.
        // Mounted above the workpiece; nozzle points down. Horizontal arms
        // connect back toward the IPG cabinet at x = -1.5.
        private void CreateProcessingHead(Transform parent)
        {
            const float S = 0.30f; // Blender-to-local scale

            LaserGantry = new GameObject("LaserGantry");
            LaserGantry.transform.SetParent(parent);
            LaserGantry.transform.localPosition = new Vector3(0, 2.0f, 0);
            Transform root = LaserGantry.transform;

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            // — Materials —
            var matAlu = new Material(shader) { color = new Color(0.78f, 0.78f, 0.80f) };
            matAlu.SetFloat("_Metallic", 0.95f); matAlu.SetFloat("_Smoothness", 0.68f);

            var matSteel = new Material(shader) { color = new Color(0.88f, 0.88f, 0.90f) };
            matSteel.SetFloat("_Metallic", 1.0f); matSteel.SetFloat("_Smoothness", 0.82f);

            var matDarkAlu = new Material(shader) { color = new Color(0.55f, 0.55f, 0.58f) };
            matDarkAlu.SetFloat("_Metallic", 0.95f); matDarkAlu.SetFloat("_Smoothness", 0.55f);

            var matBlackH = new Material(shader) { color = new Color(0.04f, 0.04f, 0.04f) };
            matBlackH.SetFloat("_Metallic", 0.30f); matBlackH.SetFloat("_Smoothness", 0.45f);

            var matBolt = new Material(shader) { color = new Color(0.30f, 0.30f, 0.32f) };
            matBolt.SetFloat("_Metallic", 0.90f); matBolt.SetFloat("_Smoothness", 0.60f);

            var matBlueTag = new Material(shader) { color = new Color(0.10f, 0.40f, 0.85f) };

            var matLens = new Material(shader) { color = new Color(0.05f, 0.08f, 0.15f) };
            matLens.SetFloat("_Smoothness", 0.95f);

            // — Helper: create box under gantry root —
            System.Func<string, Vector3, Vector3, Material, GameObject> Box = (n, pos, size, mat) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n;
                go.transform.SetParent(root);
                go.transform.localScale = size;
                go.transform.localPosition = pos;
                go.GetComponent<Renderer>().material = mat;
                return go;
            };

            // — Helper: create cylinder under gantry root —
            System.Func<string, Vector3, float, float, char, Material, int, GameObject> Cyl =
                (n, pos, radius, length, axis, mat, verts) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = n;
                go.transform.SetParent(root);
                // Cylinder default is Y-axis; rotate for X or Z
                if (axis == 'X')
                {
                    go.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);
                    go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                else if (axis == 'Z')
                {
                    go.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);
                    go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                else // Y (default)
                {
                    go.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);
                }
                go.transform.localPosition = pos;
                go.GetComponent<Renderer>().material = mat;
                return go;
            };

            // Blender→Unity coord mapping (with S scale):
            //   bX → -X (arms toward cabinet), bY → Z, bZ → Y (up)
            System.Func<float, float, float, Vector3> P = (bx, by, bz) =>
                new Vector3(-bx * S, bz * S, by * S);
            System.Func<float, float> R = (v) => v * S;

            // ── BASE MOUNTING BRACKET ──
            Box("Base_Plate", P(0, 0, 0.05f), new Vector3(R(1.8f), R(0.10f), R(1.6f)), matAlu);
            Box("Base_Step",  P(0, 0.10f, 0.18f), new Vector3(R(1.5f), R(0.16f), R(0.9f)), matAlu);
            Box("Base_Top",   P(0, 0.05f, 0.32f), new Vector3(R(1.2f), R(0.12f), R(0.7f)), matAlu);
            Box("ID_Label",   P(0.30f, -0.55f, 0.15f), new Vector3(R(0.55f), R(0.18f), R(0.02f)), matBlueTag);

            // ── VERTICAL POSITIONING MODULES ──
            // Each module = body + two side wings + bolt heads
            System.Action<string, float, float, float, float> AddModule = (name, zC, bx, by, bz) =>
            {
                Box($"{name}_Body", P(0, 0, zC), new Vector3(R(bx), R(bz), R(by)), matAlu);
                float wt = 0.10f;
                Box($"{name}_WingL", P(bx / 2f + wt / 2f, 0, zC),
                    new Vector3(R(wt), R(bz - 0.05f), R(by + 0.25f)), matDarkAlu);
                Box($"{name}_WingR", P(-bx / 2f - wt / 2f, 0, zC),
                    new Vector3(R(wt), R(bz - 0.05f), R(by + 0.25f)), matDarkAlu);
                // Bolt heads on front face
                foreach (float dx in new[] { -bx * 0.30f, bx * 0.30f })
                    foreach (float dz in new[] { -bz * 0.32f, bz * 0.32f })
                    {
                        var bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        bolt.name = $"{name}_Bolt";
                        bolt.transform.SetParent(root);
                        bolt.transform.localScale = new Vector3(R(0.09f), R(0.012f), R(0.09f));
                        bolt.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        bolt.transform.localPosition = P(dx, -by / 2f - 0.012f, zC + dz);
                        bolt.GetComponent<Renderer>().material = matBolt;
                    }
            };

            AddModule("Mod1", 0.95f, 1.05f, 1.05f, 1.10f);
            AddModule("Mod2", 2.20f, 1.10f, 1.10f, 1.20f);
            AddModule("Mod3", 3.20f, 0.95f, 0.95f, 0.65f);

            // Spacer plates between modules
            Box("Spacer_1_2", P(0, 0, 1.55f), new Vector3(R(1.15f), R(0.08f), R(1.20f)), matDarkAlu);
            Box("Spacer_2_3", P(0, 0, 2.85f), new Vector3(R(1.10f), R(0.08f), R(1.10f)), matDarkAlu);
            Box("TopMount",   P(0, 0, 3.62f), new Vector3(R(0.95f), R(0.10f), R(0.95f)), matDarkAlu);

            // ── CAMERA / SIGHTING MODULE ──
            Box("CameraBody",  P(0, 0.05f, 4.00f), new Vector3(R(0.75f), R(0.65f), R(0.95f)), matBlackH);
            Box("CameraElbow", P(-0.20f, 0.05f, 4.40f), new Vector3(R(0.42f), R(0.25f), R(0.55f)), matBlackH);
            Cyl("LensBarrel",  P(-0.20f, 0.05f, 4.70f), R(0.16f), R(0.35f), 'Y', matSteel, 48);
            Cyl("LensGlass",   P(-0.20f, 0.05f, 4.88f), R(0.14f), R(0.04f), 'Y', matLens, 48);
            Cyl("LensGrip",    P(-0.20f, 0.05f, 4.78f), R(0.165f), R(0.06f), 'Y', matDarkAlu, 48);

            // ── LOWER BEAM DELIVERY ARM (toward cabinet, -X) ──
            float loLen = R(1.10f), loZ = 0.95f, loXStart = 0.55f;
            Cyl("LowerCyl_Body",  P(loXStart + 1.10f / 2f, 0, loZ), R(0.22f), loLen, 'X', matSteel, 48);
            Cyl("LowerCyl_Ring",  P(loXStart + 0.05f, 0, loZ), R(0.255f), R(0.10f), 'X', matDarkAlu, 48);
            Cyl("LowerCyl_End",   P(loXStart + 1.10f + 0.20f, 0, loZ), R(0.12f), R(0.06f), 'X', matSteel, 48);

            // ── UPPER BEAM DELIVERY ARM (toward cabinet, -X) ──
            float hiLen = R(1.40f), hiZ = 2.20f, hiXStart = 0.60f;
            Cyl("UpperCyl_Body",   P(hiXStart + 1.40f / 2f, 0, hiZ), R(0.32f), hiLen, 'X', matSteel, 48);
            Cyl("UpperCyl_Collar", P(hiXStart + 0.08f, 0, hiZ), R(0.36f), R(0.16f), 'X', matDarkAlu, 48);

            // Stepped nozzle at end of upper arm
            Cyl("Nozzle_Step1", P(hiXStart + 1.40f + 0.10f, 0, hiZ), R(0.24f), R(0.18f), 'X', matSteel, 48);
            Cyl("Nozzle_Step2", P(hiXStart + 1.40f + 0.28f, 0, hiZ), R(0.16f), R(0.20f), 'X', matSteel, 48);
            Cyl("Nozzle_Tip",   P(hiXStart + 1.40f + 0.42f, 0, hiZ), R(0.08f), R(0.10f), 'X', matSteel, 48);

            // ── DOWNWARD EXIT NOZZLE (LaserHead — beam exits here) ──
            LaserHead = new GameObject("LaserHead");
            LaserHead.transform.SetParent(root);
            LaserHead.transform.localPosition = P(0, 0, -0.20f); // below base

            var exitNozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exitNozzle.name = "ExitNozzle";
            exitNozzle.transform.SetParent(LaserHead.transform);
            exitNozzle.transform.localScale = new Vector3(R(0.16f), R(0.30f), R(0.16f));
            exitNozzle.transform.localPosition = Vector3.zero;
            exitNozzle.GetComponent<Renderer>().material = matSteel;

            var nozzleTip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nozzleTip.name = "ExitNozzleTip";
            nozzleTip.transform.SetParent(LaserHead.transform);
            nozzleTip.transform.localScale = new Vector3(R(0.08f), R(0.12f), R(0.08f));
            nozzleTip.transform.localPosition = new Vector3(0, -R(0.20f), 0);
            var tipMat = new Material(matSteel);
            tipMat.color = new Color(0.80f, 0.80f, 0.85f);
            nozzleTip.GetComponent<Renderer>().material = tipMat;

            // Emissive accent stripe (state-reactive)
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.transform.SetParent(LaserHead.transform);
            stripe.transform.localScale = new Vector3(R(0.20f), 0.02f, R(0.20f));
            stripe.transform.localPosition = new Vector3(0, R(0.12f), 0);
            _laserStripeMat = new Material(_emissiveOrangeMat);
            _laserStripeMat.SetColor("_EmissionColor", Color.black);
            stripe.GetComponent<Renderer>().material = _laserStripeMat;
            _laserStripeRenderer = stripe.GetComponent<Renderer>();

            // LED indicator
            _laserActiveLED = CreateLEDIndicator(root, P(0, -0.55f, 0.50f), new Color(0.1f, 1f, 0.3f));
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

        // Monitor panel removed from scene
        private void CreateMonitorPanel() { }

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
            // Minimized lighting — realistic dim storage/lab feel.
            RenderSettings.ambientIntensity = 0.18f;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.08f);
            RenderSettings.ambientMode = AmbientMode.Flat;

            var dirGO = new GameObject("DirectionalFill");
            dirGO.transform.SetParent(transform);
            dirGO.transform.rotation = Quaternion.Euler(50, -30, 0);
            var dir = dirGO.AddComponent<Light>();
            dir.type = LightType.Directional;
            dir.color = new Color(0.95f, 0.9f, 0.85f);
            dir.intensity = 0.2f;
            dir.shadows = LightShadows.Soft;

            var spotGO = new GameObject("ChamberSpotlight");
            spotGO.transform.SetParent(Chamber.transform);
            spotGO.transform.localPosition = new Vector3(0, 2.7f, 0);
            spotGO.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var spot = spotGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.9f, 0.95f, 1f);
            spot.intensity = 6f;
            spot.range = 5f;
            spot.spotAngle = 60f;
            spot.shadows = LightShadows.Soft;

            // Decorative LED/monitor glows kept but dimmer.
            CreatePointLight("LEDStrip1", new Vector3(-0.8f, 0.05f, 0), new Color(0.1f, 0.4f, 1f), 1.5f, 1.4f);
            CreatePointLight("LEDStrip2", new Vector3(0.8f, 0.05f, 0), new Color(0.1f, 0.4f, 1f), 1.5f, 1.4f);
            CreatePointLight("LEDStrip3", new Vector3(0f, 0.05f, -0.5f), new Color(0.05f, 0.3f, 0.9f), 1.0f, 1.2f);

            CreatePointLight("MonitorGlow", new Vector3(0, 1.5f, -3.0f), new Color(0.15f, 0.3f, 0.9f), 1.0f, 1.8f);

            // Red warning light (near E-Stop area on desk)
            var warnGO = new GameObject("WarningPulse");
            warnGO.transform.SetParent(transform);
            warnGO.transform.position = new Vector3(-1.0f, 1.1f, -3.5f);
            _warningPulseLight = warnGO.AddComponent<Light>();
            _warningPulseLight.type = LightType.Point;
            _warningPulseLight.color = new Color(1f, 0.1f, 0.05f);
            _warningPulseLight.intensity = 0f;
            _warningPulseLight.range = 2f;

            CreatePointLight("GasAccent", new Vector3(2.75f, 0.3f, -2f), new Color(0.1f, 0.8f, 0.3f), 0.6f, 1.4f);

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

        // E-Stop removed — IPG cabinet has integrated E-Stop on control panel.
        private void CreateEStopButton() { }

        private void CreateFumeExtractor()
        {
            var root = new GameObject("BOFA_FumeExtractor");
            root.transform.SetParent(transform);
            
            // Place on the bench next to the enclosure
            root.transform.localPosition = new Vector3(0.5f, 0.85f, -1.0f);
            
            float S = 0.1f; // 1 Blender unit = 10 cm

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            var mWhite = new Material(shader) { color = new Color(0.94f, 0.94f, 0.93f) };
            mWhite.SetFloat("_Metallic", 0.10f); mWhite.SetFloat("_Smoothness", 0.55f);

            var mOffWhite = new Material(shader) { color = new Color(0.86f, 0.86f, 0.84f) };
            mOffWhite.SetFloat("_Metallic", 0.05f); mOffWhite.SetFloat("_Smoothness", 0.45f);

            var mDarkPanel = new Material(shader) { color = new Color(0.10f, 0.12f, 0.14f) };
            mDarkPanel.SetFloat("_Metallic", 0.20f); mDarkPanel.SetFloat("_Smoothness", 0.50f);

            var mGreenTrim = new Material(shader) { color = new Color(0.20f, 0.55f, 0.25f) };
            mGreenTrim.SetFloat("_Metallic", 0.10f); mGreenTrim.SetFloat("_Smoothness", 0.55f);

            var mBlackPlast = new Material(shader) { color = new Color(0.06f, 0.06f, 0.06f) };
            mBlackPlast.SetFloat("_Metallic", 0.10f); mBlackPlast.SetFloat("_Smoothness", 0.35f);

            var mDarkSteel = new Material(shader) { color = new Color(0.30f, 0.30f, 0.33f) };
            mDarkSteel.SetFloat("_Metallic", 0.95f); mDarkSteel.SetFloat("_Smoothness", 0.60f);

            var mYellow = new Material(shader) { color = new Color(0.97f, 0.85f, 0.05f) };
            mYellow.SetFloat("_Metallic", 0.0f); mYellow.SetFloat("_Smoothness", 0.45f);

            var mGreenLbl = new Material(shader) { color = new Color(0.10f, 0.55f, 0.20f) };
            mGreenLbl.SetFloat("_Metallic", 0.0f); mGreenLbl.SetFloat("_Smoothness", 0.45f);

            var mRedGlow = new Material(shader) { color = new Color(0.85f, 0.10f, 0.10f) };
            mRedGlow.EnableKeyword("_EMISSION"); mRedGlow.SetColor("_EmissionColor", mRedGlow.color * 0.6f);

            var mLedR = new Material(shader) { color = new Color(1.00f, 0.10f, 0.10f) };
            mLedR.EnableKeyword("_EMISSION"); mLedR.SetColor("_EmissionColor", mLedR.color * 2.5f);

            var mLedG = new Material(shader) { color = new Color(0.10f, 1.00f, 0.10f) };
            mLedG.EnableKeyword("_EMISSION"); mLedG.SetColor("_EmissionColor", mLedG.color * 2.5f);

            var mTxtWhite = new Material(shader) { color = new Color(0.97f, 0.97f, 0.97f) };
            var mTxtBlack = new Material(shader) { color = new Color(0.04f, 0.04f, 0.04f) };
            var mLabelBg = new Material(shader) { color = new Color(0.95f, 0.95f, 0.93f) };
            var mCable = new Material(shader) { color = new Color(0.50f, 0.50f, 0.52f) };

            System.Func<string, Vector3, Vector3, Material, GameObject> AddBox = (name, center, size, mat) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(root.transform);
                go.transform.localScale = new Vector3(size.x, size.z, size.y) * S;
                go.transform.localPosition = new Vector3(center.x, center.z, center.y) * S;
                go.GetComponent<Renderer>().material = mat;
                return go;
            };

            System.Func<string, Vector3, float, float, char, Material, GameObject> AddCyl = (name, center, radius, length, axis, mat) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(root.transform);
                if (axis == 'X')
                {
                    go.transform.localScale = new Vector3(radius * 2 * S, length / 2 * S, radius * 2 * S);
                    go.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                else if (axis == 'Y')
                {
                    go.transform.localScale = new Vector3(radius * 2 * S, length / 2 * S, radius * 2 * S);
                    go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                else // Z
                {
                    go.transform.localScale = new Vector3(radius * 2 * S, length / 2 * S, radius * 2 * S);
                }
                go.transform.localPosition = new Vector3(center.x, center.z, center.y) * S;
                go.GetComponent<Renderer>().material = mat;
                return go;
            };

            System.Action<string, string, float, Vector3, Material, TMPro.TextAlignmentOptions, bool> AddText = (name, text, size, loc, mat, align, back) =>
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform);
                go.transform.localPosition = new Vector3(loc.x, loc.z, loc.y) * S;
                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = size; 
                tmp.color = mat.color;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                
                var rect = tmp.rectTransform;
                if (align == TMPro.TextAlignmentOptions.BottomLeft)
                {
                    rect.pivot = new Vector2(0, 0);
                    tmp.alignment = TMPro.TextAlignmentOptions.BottomLeft;
                }
                else if (align == TMPro.TextAlignmentOptions.Bottom)
                {
                    rect.pivot = new Vector2(0.5f, 0);
                    tmp.alignment = TMPro.TextAlignmentOptions.Bottom;
                }
                rect.sizeDelta = new Vector2(5f, 5f);
                
                if (back)
                    go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            };

            float LB_W = 3.00f, LB_D = 3.40f, LB_H = 2.70f;
            float UB_W = 3.20f, UB_D = 3.60f, UB_H = 2.30f;

            float LB_FRONT_Y = -LB_D/2f;
            float UB_FRONT_Y = -UB_D/2f;
            float LB_BACK_Y = LB_D/2f;
            float UB_BACK_Y = UB_D/2f;
            float TOTAL_H = LB_H + UB_H;

            AddBox("LowerBase", new Vector3(0, 0, LB_H/2f), new Vector3(LB_W, LB_D, LB_H), mWhite);
            AddBox("UpperHood", new Vector3(0, 0, LB_H + UB_H/2f), new Vector3(UB_W, UB_D, UB_H), mWhite);
            AddBox("SeamLine", new Vector3(0, 0, LB_H + 0.001f), new Vector3(UB_W*1.001f, UB_D*1.001f, 0.005f), mOffWhite);

            AddBox("TopInset", new Vector3(0, 0, TOTAL_H + 0.001f), new Vector3(UB_W*0.55f, UB_D*0.55f, 0.02f), mOffWhite);
            float EX_R = 0.18f, EX_LEN = 0.45f;
            AddCyl("ExhaustOutlet", new Vector3(0, 0, TOTAL_H + EX_LEN/2f), EX_R, EX_LEN, 'Z', mOffWhite);
            AddCyl("ExhaustCap", new Vector3(0, 0, TOTAL_H + EX_LEN + 0.015f), EX_R*0.85f, 0.03f, 'Z', mBlackPlast);

            float PANEL_W = LB_W * 0.92f, PANEL_H = 0.46f, PANEL_T = 0.005f;
            float PANEL_Y = LB_FRONT_Y - PANEL_T/2f - 0.001f;
            float PANEL_Z = 0.45f;
            float TEXT_Y = PANEL_Y - PANEL_T/2f - 0.005f;

            AddBox("BrandPanel", new Vector3(0, PANEL_Y, PANEL_Z), new Vector3(PANEL_W, PANEL_T, PANEL_H), mDarkPanel);
            AddBox("GreenStripe", new Vector3(0, PANEL_Y - 0.001f, PANEL_Z + PANEL_H/2f - 0.012f), new Vector3(PANEL_W, 0.003f, 0.022f), mGreenTrim);

            AddText("BOFA_Logo", "BOFA", 0.18f, new Vector3(-PANEL_W*0.10f, TEXT_Y, PANEL_Z - 0.07f), mTxtWhite, TMPro.TextAlignmentOptions.Bottom, false);
            AddText("Tagline_1", "THE WORLD LEADER IN", 0.045f, new Vector3(PANEL_W*0.06f, TEXT_Y, PANEL_Z + 0.04f), mTxtWhite, TMPro.TextAlignmentOptions.BottomLeft, false);
            AddText("Tagline_2", "FUME EXTRACTION TECHNOLOGY", 0.045f, new Vector3(PANEL_W*0.06f, TEXT_Y, PANEL_Z - 0.03f), mTxtWhite, TMPro.TextAlignmentOptions.BottomLeft, false);

            float LED_X = -PANEL_W*0.42f;
            Material[] ledMats = { mLedR, mLedG, mLedG };
            for (int i = 0; i < 3; i++)
            {
                AddCyl($"LED_{i}", new Vector3(LED_X, TEXT_Y - 0.002f, PANEL_Z + 0.12f - i*0.10f), 0.022f, 0.012f, 'Y', ledMats[i]);
            }

            float LX = UB_W/2f + 0.005f;
            float LZ = LB_H;
            AddBox("Latch_UpperPlate", new Vector3(LX, 0.0f, LZ + 0.10f), new Vector3(0.025f, 0.10f, 0.18f), mDarkSteel);
            AddBox("Latch_LowerHook", new Vector3(LX, 0.0f, LZ - 0.10f), new Vector3(0.025f, 0.06f, 0.10f), mDarkSteel);
            AddBox("Latch_Cam", new Vector3(LX + 0.025f, 0.0f, LZ + 0.04f), new Vector3(0.05f, 0.05f, 0.08f), mDarkSteel);

            AddBox("WarningSticker", new Vector3(UB_W/2f + 0.003f, -0.50f, LB_H + 0.20f), new Vector3(0.005f, 0.18f, 0.18f), mYellow);

            float LBL_Y = LB_BACK_Y + 0.003f;
            AddBox("ProductLabel", new Vector3(-0.30f, LBL_Y, LB_H * 0.55f), new Vector3(0.80f, 0.005f, 0.55f), mLabelBg);
            
            float[] lines = { LB_H*0.55f + 0.18f, LB_H*0.55f + 0.10f, LB_H*0.55f + 0.02f, LB_H*0.55f - 0.06f, LB_H*0.55f - 0.14f };
            for (int i = 0; i < lines.Length; i++)
            {
                AddBox($"LabelLine_{i}", new Vector3(-0.30f, LBL_Y + 0.001f, lines[i]), new Vector3(0.55f, 0.001f, 0.005f), mTxtBlack);
            }
            
            AddBox("GreenSticker", new Vector3(-LB_W/2f + 0.22f, LBL_Y, LB_H + UB_H*0.40f), new Vector3(0.30f, 0.005f, 0.30f), mGreenLbl);

            float INLET_X = LB_W*0.30f;
            float INLET_Y = LB_BACK_Y + 0.04f;
            float INLET_Z = 0.30f;
            AddBox("PowerInlet_Body", new Vector3(INLET_X, INLET_Y, INLET_Z), new Vector3(0.40f, 0.08f, 0.35f), mBlackPlast);
            AddBox("PowerSwitch", new Vector3(INLET_X - 0.18f, INLET_Y + 0.001f, INLET_Z + 0.10f), new Vector3(0.10f, 0.005f, 0.07f), mRedGlow);
            AddBox("IEC_Recess", new Vector3(INLET_X + 0.05f, INLET_Y + 0.005f, INLET_Z), new Vector3(0.16f, 0.02f, 0.13f), mDarkSteel);

            float GX = -LB_W/2f - 0.04f;
            float GY = LB_D/2f - 0.30f;
            AddCyl("CableGland", new Vector3(GX, GY, 0.20f), 0.04f, 0.10f, 'X', mBlackPlast);
            AddCyl("Cable_Down", new Vector3(GX - 0.10f, GY, 0.05f), 0.025f, 0.30f, 'Z', mCable);
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
