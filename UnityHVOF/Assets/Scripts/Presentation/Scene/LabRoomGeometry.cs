// ============================================================================
// LabRoomGeometry — Builds the empty laboratory room shell from a LabRoomSpec.
//
// Responsibility (SRP):
//   * Only room architecture (floor, walls, ceiling, windows, blinds, doors,
//     door hardware, window handle, skirting, ceiling light panels).
//   * Does NOT place or modify HVOF equipment.
//
// Coordinate system:
//   * Unit = 1 meter.
//   * Origin (0, 0, 0) is at the approximate center of the room floor, so the
//     existing HVOF equipment (which is built around the origin) stays in
//     place.
//   * +X = right (long window band wall).
//   * -X = left (diagonal wall, second blinds band).
//   * +Z = front (entrance with the 126 cm doorway clearance).
//   * -Z = back (127 cm wall with interior door).
//   * +Y = up, ceiling at spec.CeilingHeightCm / 100.
//
// Floor plan reconstruction (from the sketch, all cm):
//   P0 = ( +CENTER_W/2,  +RIGHT_L/2 )        front-right
//   P1 = ( +CENTER_W/2,  -RIGHT_L/2 )        back-right  (right wall = 602)
//   P2 = ( +CENTER_W/2 - BACK_W, -RIGHT_L/2) back-left of back wall (back = 127)
//   P3 = ( -CENTER_W/2,  zDiag )             end of 500 diagonal
//   P4 = ( -CENTER_W/2,  +RIGHT_L/2 )        front-left
//   back to P0                               (front wall = 276, with doorway)
// ============================================================================

using UnityEngine;
using TMPro;
using Material = UnityEngine.Material;

namespace HVOFSim.Presentation.Scene
{
    public static class LabRoomGeometry
    {
        public static GameObject Build(Transform parent, LabRoomSpec spec, RoomMaterials mats)
        {
            var root = new GameObject("LabRoom");
            root.transform.SetParent(parent, false);

            var g = new Geometry(spec);
            BuildFloor(root.transform, g, mats);
            BuildCeiling(root.transform, g, mats);
            BuildShell(root.transform, g, spec, mats);
            BuildCeilingFluorescents(root.transform, g, mats);

            return root;
        }

        // ── Geometry helpers ────────────────────────────────────────────

        private sealed class Geometry
        {
            public readonly float CeilingH;
            public readonly float CenterW;   // main section width (X)   = 2.76 m
            public readonly float RightL;    // right wall length (Z)    = 6.02 m
            public readonly float BackW;     // back wall width          = 1.27 m
            public readonly float Diagonal;  // diagonal wall length     = 5.00 m
            public readonly float DoorClr;   // front-wall doorway open  = 1.26 m

            // Polygon corners (meters, floor plan)
            public readonly Vector2 P0, P1, P2, P3, P4;

            public Geometry(LabRoomSpec s)
            {
                float m = LabRoomSpec.CmToM;
                CeilingH = s.CeilingHeightCm * m;
                CenterW  = s.CenterDepthCm   * m;
                RightL   = s.RightWallLengthCm * m;
                BackW    = s.BackWallWidthCm * m;
                Diagonal = s.LeftDiagonalCm  * m;
                DoorClr  = s.EntryDoorClearanceCm * m;

                float halfX = CenterW * 0.5f;
                float halfZ = RightL  * 0.5f;

                P0 = new Vector2(+halfX, +halfZ);
                P1 = new Vector2(+halfX, -halfZ);
                P2 = new Vector2(+halfX - BackW, -halfZ);

                // Diagonal from P2 to P3 on left wall; compute Z-component of 5.00 m
                // diagonal given X-component = CenterW - BackW.
                float dx = (CenterW - BackW);
                float dz = Mathf.Sqrt(Mathf.Max(0f, Diagonal * Diagonal - dx * dx));
                P3 = new Vector2(-halfX, -halfZ + dz);
                P4 = new Vector2(-halfX, +halfZ);
            }
        }

        // ── Floor / ceiling as polygon meshes ───────────────────────────

        private static void BuildFloor(Transform parent, Geometry g, RoomMaterials mats)
        {
            var poly = new[] { g.P0, g.P1, g.P2, g.P3, g.P4 };
            var go = CreatePolygon("Floor", poly, y: 0f, up: true, mats.Floor);
            go.transform.SetParent(parent, false);

            BuildSkirting(parent, g, mats);
        }

        private static void BuildCeiling(Transform parent, Geometry g, RoomMaterials mats)
        {
            var poly = new[] { g.P0, g.P1, g.P2, g.P3, g.P4 };
            var go = CreatePolygon("Ceiling", poly, y: g.CeilingH, up: false, mats.Ceiling);
            go.transform.SetParent(parent, false);
        }

        private static GameObject CreatePolygon(string name, Vector2[] poly, float y, bool up, Material mat)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var mesh = new Mesh { name = name + "_Mesh" };
            var verts = new Vector3[poly.Length];
            for (int i = 0; i < poly.Length; i++)
                verts[i] = new Vector3(poly[i].x, y, poly[i].y);

            // Fan triangulation (all vertices listed in order; polygon is convex-enough
            // for our floor plan: a single reflex corner at the diagonal is still fan-safe
            // from P0).
            int triCount = poly.Length - 2;
            var tris = new int[triCount * 3];
            for (int i = 0; i < triCount; i++)
            {
                int baseIdx = i * 3;
                tris[baseIdx] = 0;
                if (up)
                {
                    tris[baseIdx + 1] = i + 1;
                    tris[baseIdx + 2] = i + 2;
                }
                else
                {
                    tris[baseIdx + 1] = i + 2;
                    tris[baseIdx + 2] = i + 1;
                }
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            mr.sharedMaterial = mat;
            return go;
        }

        // ── Shell walls with openings & windows ─────────────────────────

        private static void BuildShell(Transform parent, Geometry g, LabRoomSpec spec, RoomMaterials mats)
        {
            float h = g.CeilingH;
            const float thick = 0.08f;    // 8 cm drywall

            // Photo-matched rectangular storage room:
            //   - Right wall (P0 → P1)  : floor-to-ceiling glass curtain wall.
            //   - Back wall  (P1 → P2)  : solid (no door).
            //   - Left wall  (P2 → P3)  : solid + "VG5 / Laboratori 2" sign.
            //   - Front wall (P4 → P0)  : solid with ONE door aligned to the
            //                              LEFT side (the door you see first
            //                              when walking in).
            //   - Segment P3 → P4 is degenerate for a rectangle (P3 == P4) and
            //     is skipped intentionally.

            // Right wall — full floor-to-ceiling glass curtain wall.
            BuildGlassCurtainWall(
                parent: parent,
                name: "RightWall",
                a: g.P0, b: g.P1,
                wallHeight: h,
                wallThickness: thick,
                glazingLength: spec.RightWindowLengthCm * LabRoomSpec.CmToM,
                mats: mats,
                includeDoorPanel: false);

            // Front wall is now solid — the door lives at the opposite end.
            BuildSolidWall(parent, "FrontWall", g.P4, g.P0, h, thick, mats.Wall);

            // Left wall splits into:
            //   1) A short solid segment near the back-left corner with a
            //      second entry door (around the corner from the back door).
            //   2) The remainder of the left wall stays solid and carries the
            //      VG5 / Laboratori 2 sign.
            float leftDoorSegLen = spec.LeftWallDoorSegmentLengthCm * LabRoomSpec.CmToM;
            // P2 = back-left (-Z), P3 = front-left (+Z). Door segment hugs P2.
            Vector2 leftDoorEnd = LerpSeg(g.P2, g.P3, leftDoorSegLen);

            BuildWallWithDoor(
                parent: parent,
                name: "LeftBackWall",
                a: g.P2, b: leftDoorEnd,
                wallHeight: h,
                wallThickness: thick,
                doorWidth:  spec.EntryDoorClearanceCm * LabRoomSpec.CmToM,
                doorHeight: spec.DoorLeafHeightCm * LabRoomSpec.CmToM,
                mats: mats,
                spec: spec,
                doorStyle: DoorStyle.Entry,
                leftMarginMeters: spec.LeftWallDoorBackMarginCm * LabRoomSpec.CmToM);

            BuildSolidWall(parent, "LeftWall", leftDoorEnd, g.P3, h, thick, mats.Wall);
            // BuildSignPlate(parent, leftDoorEnd, g.P3, spec, mats); // Removed duplicate legacy sign

            // Back wall — single entry door. Anchored to the -X side of the
            // wall so that when the in-room camera looks at the back wall,
            // the door reads on the RIGHT (matches the photo where the door
            // is hard against the right side of the back wall).
            // a = P2 (back-left, -X), b = P1 (back-right, +X); leftMarginMeters
            // is therefore measured from the -X end.
            BuildWallWithDoor(
                parent: parent,
                name: "BackWall",
                a: g.P2, b: g.P1,
                wallHeight: h,
                wallThickness: thick,
                doorWidth:  spec.EntryDoorClearanceCm * LabRoomSpec.CmToM,
                doorHeight: spec.DoorLeafHeightCm * LabRoomSpec.CmToM,
                mats: mats,
                spec: spec,
                doorStyle: DoorStyle.Entry,
                leftMarginMeters: spec.FrontDoorLeftMarginCm * LabRoomSpec.CmToM);
        }

        // ── Wall primitives ─────────────────────────────────────────────

        private static void BuildSolidWall(Transform parent, string name, Vector2 a, Vector2 b,
                                           float height, float thick, Material mat)
        {
            PlaceWallSegment(parent, name, a, b, fromY: 0f, toY: height, thick, mat);
        }

        // ── Left-wall "VG5 / Laboratori 2" room sign ────────────────────
        //
        // Mounts a small plate proud of the interior face of the left wall,
        // near the front (entrance) side of the room. Replicates the black
        // vertical stripe visible in the reference photograph.
        private static void BuildSignPlate(Transform parent, Vector2 a, Vector2 b,
                                           LabRoomSpec spec, RoomMaterials mats)
        {
            float segLen = Vector2.Distance(a, b);
            if (segLen < 0.1f) return;

            // Position the sign near the front edge (the `b` end of the left
            // wall, since BuildShell passes a = P2 (back) → b = P3 (front)).
            float distFromFront = spec.SignDistanceFromFrontCm * LabRoomSpec.CmToM;
            float offsetAlong = Mathf.Clamp(segLen - distFromFront, 0.05f, segLen - 0.05f);
            Vector2 mid = LerpSeg(a, b, offsetAlong);
            float yaw = YawFromSegment(a, b);
            float signY = spec.SignHeightFromFloorCm * LabRoomSpec.CmToM;

            var root = new GameObject("LeftWallSign");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(mid.x, signY, mid.y);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            float w = spec.SignWidthCm  * LabRoomSpec.CmToM;
            float hh = spec.SignHeightCm * LabRoomSpec.CmToM;
            // Interior side of the wall (room side) is at local -Z when the
            // yaw is taken from (a→b) direction for a left wall (wall normal
            // then points toward +X room interior along local +Z negative).
            // Empirically we offset a few cm into the room for visibility.
            float proud = 0.015f;

            // Backing plate (brushed aluminum face)
            AddChildCube(root.transform, "Plate", new Vector3(0f, 0f, -proud),
                         new Vector3(w, hh, 0.02f), mats.BrushedMetal);

            // Black vertical stripe on the left edge of the plate
            float stripeW = Mathf.Min(0.05f, w * 0.18f);
            AddChildCube(root.transform, "Stripe",
                         new Vector3(-w * 0.5f + stripeW * 0.5f, 0f, -proud - 0.003f),
                         new Vector3(stripeW, hh, 0.022f), mats.CurtainFrame);

            // TextMeshPro labels on the plate
            AddSignText(root.transform, "SignPrimary", spec.SignPrimaryText,
                        new Vector3(stripeW * 0.8f, hh * 0.22f, -proud - 0.012f),
                        new Vector2(w - stripeW - 0.01f, hh * 0.45f),
                        fontSize: 0.55f, color: new Color(0.1f, 0.2f, 0.45f));

            AddSignText(root.transform, "SignSecondary", spec.SignSecondaryText,
                        new Vector3(stripeW * 0.8f, -hh * 0.28f, -proud - 0.012f),
                        new Vector2(w - stripeW - 0.01f, hh * 0.4f),
                        fontSize: 0.4f, color: new Color(0.1f, 0.2f, 0.45f));
        }

        private static void AddSignText(Transform parent, string name, string text,
                                        Vector3 localPos, Vector2 size, float fontSize, Color color)
        {
            var tmpGO = new GameObject(name);
            tmpGO.transform.SetParent(parent, false);
            tmpGO.transform.localPosition = localPos;
            // Face the room (rotate 180° around Y so text reads correctly from
            // the interior side of the left wall).
            tmpGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var tmp = tmpGO.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = color;
            tmp.fontSize = 4f;
            tmp.rectTransform.sizeDelta = new Vector2(size.x / fontSize * 10f, size.y / fontSize * 10f);
            tmpGO.transform.localScale = new Vector3(fontSize * 0.1f, fontSize * 0.1f, 1f);
        }

        private static void BuildWallWithDoor(Transform parent, string name,
                                              Vector2 a, Vector2 b,
                                              float wallHeight, float wallThickness,
                                              float doorWidth, float doorHeight,
                                              RoomMaterials mats, LabRoomSpec spec,
                                              DoorStyle doorStyle,
                                              float leftMarginMeters = -1f)
        {
            float segLen = Vector2.Distance(a, b);
            float opening = Mathf.Min(doorWidth, segLen - 0.05f);
            // When leftMarginMeters < 0 the opening is centered (default legacy
            // behaviour). When >= 0 it is anchored that many meters from the
            // `a` end, but never allowed to overflow the wall.
            float leftMargin = leftMarginMeters < 0f
                ? (segLen - opening) * 0.5f
                : Mathf.Clamp(leftMarginMeters, 0f, segLen - opening);

            // Split points along the wall
            Vector2 leftSplit  = LerpSeg(a, b, leftMargin);
            Vector2 rightSplit = LerpSeg(a, b, leftMargin + opening);

            // Side jamb walls (floor → ceiling)
            PlaceWallSegment(parent, name + "_LeftJamb",  a, leftSplit,  0f, wallHeight, wallThickness, mats.Wall);
            PlaceWallSegment(parent, name + "_RightJamb", rightSplit, b, 0f, wallHeight, wallThickness, mats.Wall);

            // Header above the opening
            PlaceWallSegment(parent, name + "_Header", leftSplit, rightSplit, doorHeight, wallHeight, wallThickness, mats.Wall);

            // Door leaf + frame + hardware
            BuildDoor(parent, name + "_Door", leftSplit, rightSplit, doorHeight, wallThickness, mats, spec, doorStyle);
        }

        private static void BuildWallWithWindow(Transform parent, string name,
                                                Vector2 a, Vector2 b,
                                                float wallHeight, float wallThickness,
                                                float windowLength,
                                                float sillY, float headY,
                                                float frameThickness,
                                                RoomMaterials mats, LabRoomSpec spec,
                                                bool placeHandle)
        {
            float segLen = Vector2.Distance(a, b);
            float opening = Mathf.Min(windowLength, segLen - 0.05f);
            float leftMargin  = (segLen - opening) * 0.5f;
            float rightMargin = leftMargin;

            Vector2 leftSplit  = LerpSeg(a, b, leftMargin);
            Vector2 rightSplit = LerpSeg(a, b, leftMargin + opening);

            // Side jambs full height
            PlaceWallSegment(parent, name + "_LeftJamb",  a, leftSplit,  0f, wallHeight, wallThickness, mats.Wall);
            PlaceWallSegment(parent, name + "_RightJamb", rightSplit, b, 0f, wallHeight, wallThickness, mats.Wall);

            // Apron (below sill) and header (above head)
            PlaceWallSegment(parent, name + "_Apron",  leftSplit, rightSplit, 0f, sillY, wallThickness, mats.Wall);
            PlaceWallSegment(parent, name + "_Header", leftSplit, rightSplit, headY, wallHeight, wallThickness, mats.Wall);

            // Window: frame + glass + blinds
            BuildWindow(parent, name + "_Window", leftSplit, rightSplit, sillY, headY,
                        wallThickness, frameThickness, mats, spec, placeHandle);
        }

        // ── Floor-to-ceiling glass curtain wall (photo reference) ────────
        //
        // Replaces the old sill+header window band with tall aluminum-framed
        // glass panels that run from floor to ceiling. Divided by vertical
        // mullions every ~1.3 m. Optionally one panel is a sliding glass door
        // with a tall vertical pull handle.
        private static void BuildGlassCurtainWall(Transform parent, string name,
                                                   Vector2 a, Vector2 b,
                                                   float wallHeight, float wallThickness,
                                                   float glazingLength,
                                                   RoomMaterials mats,
                                                   bool includeDoorPanel)
        {
            float segLen = Vector2.Distance(a, b);
            float opening = Mathf.Min(glazingLength, segLen - 0.05f);
            float margin  = (segLen - opening) * 0.5f;

            Vector2 leftSplit  = LerpSeg(a, b, margin);
            Vector2 rightSplit = LerpSeg(a, b, margin + opening);

            // Side jambs stay as solid wall on each side of the glazing.
            PlaceWallSegment(parent, name + "_LeftJamb",  a, leftSplit,  0f, wallHeight, wallThickness, mats.Wall);
            PlaceWallSegment(parent, name + "_RightJamb", rightSplit, b, 0f, wallHeight, wallThickness, mats.Wall);

            // Root transform for the glazed panel (sits flush with wall plane).
            var root = new GameObject(name + "_Glazing");
            root.transform.SetParent(parent, false);
            Vector2 mid = (leftSplit + rightSplit) * 0.5f;
            root.transform.position = new Vector3(mid.x, wallHeight * 0.5f, mid.y);
            root.transform.rotation = Quaternion.Euler(0f, YawFromSegment(leftSplit, rightSplit) +270f, 0f);

            float length  = opening;
            float height  = wallHeight;

            // Thin perimeter frame: top rail, bottom rail, left & right posts.
            float rail = 0.06f;                    // 6 cm rails / mullions
            float depth = wallThickness * 1.1f;    // proud of wall by a hair
            AddChildCube(root.transform, "TopRail",    new Vector3(0f,  height * 0.5f - rail * 0.5f, 0f), new Vector3(length, rail, depth), mats.CurtainFrame);
            AddChildCube(root.transform, "BottomRail", new Vector3(0f, -height * 0.5f + rail * 0.5f, 0f), new Vector3(length, rail, depth), mats.CurtainFrame);
            AddChildCube(root.transform, "LeftPost",   new Vector3(-length * 0.5f + rail * 0.5f, 0f, 0f), new Vector3(rail, height, depth), mats.CurtainFrame);
            AddChildCube(root.transform, "RightPost",  new Vector3(+length * 0.5f - rail * 0.5f, 0f, 0f), new Vector3(rail, height, depth), mats.CurtainFrame);

            // Vertical mullions every ~1.3 m → 2–4 panes on typical walls.
            int panes = Mathf.Max(2, Mathf.RoundToInt(length / 1.3f));
            float paneWidth = length / panes;
            for (int i = 1; i < panes; i++)
            {
                float x = -length * 0.5f + paneWidth * i;
                AddChildCube(root.transform, $"Mullion_{i}",
                             new Vector3(x, 0f, 0f),
                             new Vector3(rail * 0.7f, height - rail * 2f, depth * 0.9f),
                             mats.CurtainFrame);
            }

            // Glass pane (single sheet behind the mullions — visually correct
            // because the mullions are in front and the frame outlines each pane).
            AddChildCube(root.transform, "GlassPane",
                         Vector3.zero,
                         new Vector3(length - rail * 2f, height - rail * 2f, 0.012f),
                         mats.Glass);

            // Optional door panel with a tall vertical pull handle.
            if (includeDoorPanel)
            {
                // Pick the pane nearest the room's center along the wall to act
                // as the door.
                int doorPaneIndex = panes / 2;
                float doorX = -length * 0.5f + paneWidth * (doorPaneIndex + 0.5f);

                // A single slim frame around the door pane to read as a leaf.
                AddChildCube(root.transform, "DoorPaneFrame_L",
                             new Vector3(doorX - paneWidth * 0.5f + rail * 0.45f, 0f, depth * 0.15f),
                             new Vector3(rail * 0.9f, height - rail * 2f, depth * 0.6f),
                             mats.CurtainFrame);
                AddChildCube(root.transform, "DoorPaneFrame_R",
                             new Vector3(doorX + paneWidth * 0.5f - rail * 0.45f, 0f, depth * 0.15f),
                             new Vector3(rail * 0.9f, height - rail * 2f, depth * 0.6f),
                             mats.CurtainFrame);

                // Tall vertical stainless-steel pull handle on the room side.
                float handleHeight = Mathf.Min(1.4f, height * 0.6f);
                float handleX = doorX + paneWidth * 0.25f;
                var handleRoot = new GameObject("GlassDoorHandle");
                handleRoot.transform.SetParent(root.transform, false);
                handleRoot.transform.localPosition = new Vector3(handleX, 0f, -depth * 0.6f);

                AddChildCube(handleRoot.transform, "Bar",
                             Vector3.zero,
                             new Vector3(0.03f, handleHeight, 0.03f),
                             mats.BrushedMetal);
                AddChildCube(handleRoot.transform, "MountTop",
                             new Vector3(0f,  handleHeight * 0.5f - 0.02f, 0.04f),
                             new Vector3(0.025f, 0.025f, 0.08f),
                             mats.BrushedMetal);
                AddChildCube(handleRoot.transform, "MountBottom",
                             new Vector3(0f, -handleHeight * 0.5f + 0.02f, 0.04f),
                             new Vector3(0.025f, 0.025f, 0.08f),
                             mats.BrushedMetal);
            }
        }

        private static void PlaceWallSegment(Transform parent, string name,
                                             Vector2 a, Vector2 b,
                                             float fromY, float toY,
                                             float thickness, Material mat)
        {
            float length = Vector2.Distance(a, b);
            if (length < 0.001f || toY - fromY < 0.001f) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);

            Vector2 mid = (a + b) * 0.5f;
            float midY = (fromY + toY) * 0.5f;
            float height = toY - fromY;

            go.transform.position = new Vector3(mid.x, midY, mid.y);
            go.transform.rotation = Quaternion.Euler(0f, YawFromSegment(a, b) - 90f, 0f);
            go.transform.localScale = new Vector3(length, height, thickness);

            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // ── Window, blinds, handle ──────────────────────────────────────

        private static void BuildWindow(Transform parent, string name,
                                        Vector2 a, Vector2 b,
                                        float sillY, float headY,
                                        float wallThickness, float frameThickness,
                                        RoomMaterials mats, LabRoomSpec spec,
                                        bool placeHandle)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Vector2 mid = (a + b) * 0.5f;
            float height = headY - sillY;
            float length = Vector2.Distance(a, b);
            root.transform.position = new Vector3(mid.x, (sillY + headY) * 0.5f, mid.y);
            root.transform.rotation = Quaternion.Euler(0f, YawFromSegment(a, b), 0f);

            // Frame (4 pieces: top, bottom, left, right)
            float ft = frameThickness;
            float fd = wallThickness * 1.1f;     // slightly proud of wall
            AddChildCube(root.transform, "FrameTop",    new Vector3(0f,  height * 0.5f - ft * 0.5f, 0f), new Vector3(length, ft, fd), mats.WindowFrame);
            AddChildCube(root.transform, "FrameBottom", new Vector3(0f, -height * 0.5f + ft * 0.5f, 0f), new Vector3(length, ft, fd), mats.WindowFrame);
            AddChildCube(root.transform, "FrameLeft",   new Vector3(-length * 0.5f + ft * 0.5f, 0f, 0f), new Vector3(ft, height, fd), mats.WindowFrame);
            AddChildCube(root.transform, "FrameRight",  new Vector3(+length * 0.5f - ft * 0.5f, 0f, 0f), new Vector3(ft, height, fd), mats.WindowFrame);

            // Mullion (vertical divider every ~1.5 m) for realism
            int mullions = Mathf.Max(0, Mathf.FloorToInt(length / 1.5f) - 1);
            if (mullions > 0)
            {
                float stepX = length / (mullions + 1);
                for (int i = 1; i <= mullions; i++)
                {
                    float x = -length * 0.5f + stepX * i;
                    AddChildCube(root.transform, $"Mullion{i}", new Vector3(x, 0f, 0f), new Vector3(ft * 0.6f, height, fd * 0.9f), mats.WindowFrame);
                }
            }

            // Glass pane
            AddChildCube(root.transform, "Glass", Vector3.zero,
                         new Vector3(length - ft * 2f, height - ft * 2f, 0.01f), mats.Glass);

            // Blinds (horizontal slats in front of the glass, room-side)
            BuildBlinds(root.transform, length - ft * 2f, height - ft * 2f, spec, mats);

            // Crank handle — only on right wall
            if (placeHandle)
            {
                BuildWindowHandle(root.transform, length, height, spec, mats);
            }
        }

        private static void BuildBlinds(Transform parent, float width, float height,
                                        LabRoomSpec spec, RoomMaterials mats)
        {
            var root = new GameObject("Blinds");
            root.transform.SetParent(parent, false);
            // Blinds sit just inside the window (on the room-facing side).
            root.transform.localPosition = new Vector3(0f, 0f, -0.04f);

            int slats = Mathf.Max(6, spec.BlindSlatCount);
            float slatHeight = 0.04f;       // 4 cm visual thickness
            float slatSpacing = height / slats;
            float tilt = spec.BlindSlatTiltDeg;

            for (int i = 0; i < slats; i++)
            {
                float y = height * 0.5f - slatSpacing * (i + 0.5f);
                var slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = $"Slat_{i:00}";
                slat.transform.SetParent(root.transform, false);
                slat.transform.localPosition = new Vector3(0f, y, 0f);
                slat.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
                slat.transform.localScale = new Vector3(width, slatHeight, 0.025f);
                slat.GetComponent<Renderer>().sharedMaterial = mats.BlindSlat;
                DestroyColliderAtRuntime(slat);
            }

            // Tilt/lift cords on each side (simple thin cylinders)
            float cordX = width * 0.5f - 0.02f;
            for (int side = -1; side <= 1; side += 2)
            {
                var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cord.name = "Cord_" + (side < 0 ? "L" : "R");
                cord.transform.SetParent(root.transform, false);
                cord.transform.localPosition = new Vector3(cordX * side, 0f, -0.01f);
                cord.transform.localScale = new Vector3(0.005f, height * 0.5f, 0.005f);
                cord.GetComponent<Renderer>().sharedMaterial = mats.BlindSlat;
                DestroyColliderAtRuntime(cord);
            }
        }

        private static void BuildWindowHandle(Transform parent, float windowLen, float windowHeight,
                                              LabRoomSpec spec, RoomMaterials mats)
        {
            // Handle sits 20 cm from the inner (back-most) edge of the window, at 110 cm
            // above floor. Because the window Y-center is at (sill+head)/2, convert the
            // absolute height to the local Y offset inside the window root.
            float handleOffsetFromInnerEdge = spec.HandleInnerOffsetCm * LabRoomSpec.CmToM;
            float handleLocalX = -windowLen * 0.5f + handleOffsetFromInnerEdge;

            float absY = spec.HandleHeightCm * LabRoomSpec.CmToM;
            float winCenterY = (spec.WindowSillHeightCm + (spec.CeilingHeightCm - spec.WindowHeadClearCm)) * 0.5f * LabRoomSpec.CmToM;
            float handleLocalY = absY - winCenterY;

            var handleRoot = new GameObject("CrankHandle_C");
            handleRoot.transform.SetParent(parent, false);
            handleRoot.transform.localPosition = new Vector3(handleLocalX, handleLocalY, -0.06f);

            // Base mount (small disc against the frame)
            var mount = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mount.name = "HandleMount";
            mount.transform.SetParent(handleRoot.transform, false);
            mount.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mount.transform.localScale = new Vector3(0.04f, 0.01f, 0.04f);
            mount.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(mount);

            // Short stem out from the frame
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "HandleStem";
            stem.transform.SetParent(handleRoot.transform, false);
            stem.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            stem.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            stem.transform.localScale = new Vector3(0.012f, 0.04f, 0.012f);
            stem.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(stem);

            // Lever (L-shape crank handle)
            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "HandleLever";
            lever.transform.SetParent(handleRoot.transform, false);
            lever.transform.localPosition = new Vector3(0.035f, -0.01f, -0.08f);
            lever.transform.localScale = new Vector3(0.08f, 0.018f, 0.018f);
            lever.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(lever);

            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "HandleKnob";
            knob.transform.SetParent(handleRoot.transform, false);
            knob.transform.localPosition = new Vector3(0.075f, -0.01f, -0.08f);
            knob.transform.localScale = new Vector3(0.025f, 0.025f, 0.025f);
            knob.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(knob);
        }

        // ── Doors ───────────────────────────────────────────────────────

        private enum DoorStyle { Entry, Interior }

        private static void BuildDoor(Transform parent, string name,
                                      Vector2 a, Vector2 b,
                                      float doorHeight, float wallThickness,
                                      RoomMaterials mats, LabRoomSpec spec,
                                      DoorStyle style)
        {
            float width = Vector2.Distance(a, b);
            if (width < 0.01f) return;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Vector2 mid = (a + b) * 0.5f;
            root.transform.position = new Vector3(mid.x, doorHeight * 0.5f, mid.y);
            root.transform.rotation = Quaternion.Euler(0f, YawFromSegment(a, b) - 90f, 0f);

            // Frame (left, right, top) slightly inset
            float ft = 0.06f;
            float fd = wallThickness * 1.25f;
            AddChildCube(root.transform, "FrameTop",   new Vector3(0f,  doorHeight * 0.5f - ft * 0.5f, 0f), new Vector3(width, ft, fd), mats.DoorFrame);
            // Entry-door side frames: slim jambs (8 cm) instead of the default
            // thickness, so they look like real door casings and do not clip
            // into the leaf in narrow walls.
            float frameLeftX = style == DoorStyle.Entry ? 0.08f : ft;
            AddChildCube(root.transform, "FrameLeft",  new Vector3(-width * 0.5f + frameLeftX * 0.5f, 0f, 0f),     new Vector3(frameLeftX, doorHeight, fd), mats.DoorFrame);
            float frameRightX = style == DoorStyle.Entry ? 0.08f : ft;
            AddChildCube(root.transform, "FrameRight", new Vector3(+width * 0.5f - frameRightX * 0.5f, 0f, 0f),     new Vector3(frameRightX, doorHeight, fd), mats.DoorFrame);

            // Leaf — width slightly less than opening to clear frame. We use a
            // `leafPivot` (unit scale) so attached hardware (handles, hinges,
            // keyhole) keeps world-space dimensions regardless of leaf size.
            float leafW = Mathf.Min(spec.DoorLeafWidthCm * LabRoomSpec.CmToM, width - ft * 2f - 0.01f);
            float leafH = doorHeight - ft - 0.01f;
            float leafT = spec.DoorLeafThicknessCm * LabRoomSpec.CmToM;

            var leafPivot = new GameObject("LeafPivot");
            leafPivot.transform.SetParent(root.transform, false);
            leafPivot.transform.localPosition = new Vector3(0f, -ft * 0.5f, 0f);

            AddChildCube(leafPivot.transform, "LeafVisual", Vector3.zero,
                         new Vector3(leafW, leafH, leafT), mats.DoorLeaf);

            // Lever handle (two, one per side, at 105 cm above floor).
            float handleY = -leafH * 0.5f + 1.05f;
            float handleX = leafW * 0.5f - 0.07f;
            BuildDoorHandle(leafPivot.transform, new Vector3(handleX, handleY, +leafT * 0.5f + 0.015f), mats);
            BuildDoorHandle(leafPivot.transform, new Vector3(handleX, handleY, -leafT * 0.5f - 0.015f), mats, mirrorZ: true);

            // Keyhole escutcheon (entry door only; photo 1 shows it)
            if (style == DoorStyle.Entry)
            {
                BuildKeyhole(leafPivot.transform, new Vector3(handleX, handleY - 0.12f, +leafT * 0.5f + 0.008f), mats);
                BuildKeyhole(leafPivot.transform, new Vector3(handleX, handleY - 0.12f, -leafT * 0.5f - 0.008f), mats);
            }

            // Hinges on the side opposite the handle
            for (int i = 0; i < 3; i++)
            {
                float hy = -leafH * 0.5f + 0.25f + i * ((leafH - 0.5f) * 0.5f);
                AddChildCube(leafPivot.transform, $"Hinge_{i}",
                             new Vector3(-leafW * 0.5f + 0.005f, hy, 0f),
                             new Vector3(0.02f, 0.08f, leafT + 0.01f),
                             mats.BrushedMetal);
            }
        }

        private static void BuildDoorHandle(Transform parent, Vector3 localPos, RoomMaterials mats, bool mirrorZ = false)
        {
            var root = new GameObject("Handle");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            // zDir is the unit vector pointing away from the leaf on whichever
            // side we're building. Hardware is assembled along that direction.
            float zDir = mirrorZ ? -1f : +1f;

            var rose = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rose.name = "Rosette";
            rose.transform.SetParent(root.transform, false);
            rose.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rose.transform.localScale = new Vector3(0.055f, 0.008f, 0.055f);
            rose.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(rose);

            var spindle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spindle.name = "Spindle";
            spindle.transform.SetParent(root.transform, false);
            spindle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spindle.transform.localPosition = new Vector3(0f, 0f, 0.02f * zDir);
            spindle.transform.localScale = new Vector3(0.015f, 0.02f, 0.015f);
            spindle.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(spindle);

            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "Lever";
            lever.transform.SetParent(root.transform, false);
            lever.transform.localPosition = new Vector3(-0.055f, 0f, 0.04f * zDir);
            lever.transform.localScale = new Vector3(0.11f, 0.018f, 0.02f);
            lever.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(lever);

            var leverTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leverTip.name = "LeverTip";
            leverTip.transform.SetParent(root.transform, false);
            leverTip.transform.localPosition = new Vector3(-0.11f, 0f, 0.04f * zDir);
            leverTip.transform.localScale = new Vector3(0.022f, 0.022f, 0.022f);
            leverTip.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(leverTip);
        }

        private static void BuildKeyhole(Transform parent, Vector3 localPos, RoomMaterials mats)
        {
            var esc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            esc.name = "Keyhole";
            esc.transform.SetParent(parent, false);
            esc.transform.localPosition = localPos;
            esc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            esc.transform.localScale = new Vector3(0.035f, 0.005f, 0.035f);
            esc.GetComponent<Renderer>().sharedMaterial = mats.BrushedMetal;
            DestroyColliderAtRuntime(esc);
        }

        // ── Skirting ────────────────────────────────────────────────────

        private static void BuildSkirting(Transform parent, Geometry g, RoomMaterials mats)
        {
            var root = new GameObject("Skirting");
            root.transform.SetParent(parent, false);

            var pts = new[] { g.P0, g.P1, g.P2, g.P3, g.P4, g.P0 };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                PlaceWallSegment(root.transform, $"Skirt_{i}", pts[i], pts[i + 1], 0f, 0.08f, 0.015f, mats.Skirting);
            }
        }

        // ── Ceiling fluorescents (room-owned, minimal) ──────────────────

        private static void BuildCeilingFluorescents(Transform parent, Geometry g, RoomMaterials mats)
        {
            var root = new GameObject("CeilingFluorescents");
            root.transform.SetParent(parent, false);

            var panelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            panelMat.color = new Color(0.92f, 0.92f, 0.95f);
            panelMat.EnableKeyword("_EMISSION");
            panelMat.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.65f));
            panelMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            float y = g.CeilingH - 0.05f;
            float[] zs = { -g.RightL * 0.33f, 0f, +g.RightL * 0.33f };
            foreach (var z in zs)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "FluorescentPanel";
                panel.transform.SetParent(root.transform, false);
                panel.transform.position = new Vector3(0f, y, z);
                panel.transform.localScale = new Vector3(0.6f, 0.05f, 1.2f);
                panel.GetComponent<Renderer>().sharedMaterial = panelMat;
                DestroyColliderAtRuntime(panel);

                var lightGO = new GameObject("CeilingLightSrc");
                lightGO.transform.SetParent(panel.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, -0.4f / panel.transform.localScale.y, 0f);
                var lt = lightGO.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.98f, 0.98f, 1f);
                lt.intensity = 1.1f;
                lt.range = 3.5f;
            }
        }

        // ── Utilities ───────────────────────────────────────────────────

        private static Vector2 LerpSeg(Vector2 a, Vector2 b, float distanceFromA)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1e-5f) return a;
            return a + (b - a) * (distanceFromA / len);
        }

        private static float YawFromSegment(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        private static void AddChildCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = mat;
            DestroyColliderAtRuntime(cube);
        }

        private static void DestroyColliderAtRuntime(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying) Object.DestroyImmediate(col);
            else Object.Destroy(col);
#else
            Object.Destroy(col);
#endif
        }
    }
}
