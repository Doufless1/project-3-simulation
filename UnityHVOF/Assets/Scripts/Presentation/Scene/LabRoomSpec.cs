// ============================================================================
// LabRoomSpec — All real-world dimensions of the physical laboratory room,
// measured on site and reconstructed from the user's sketch.
//
// All values are stored in CENTIMETERS (as measured). The geometry builder
// multiplies by CmToM at build time to produce Unity world-space meters.
//
// Single responsibility: hold room dimensions. No Unity API is used here so
// this type is trivially testable and can be mutated independently from the
// scene builder.
// ============================================================================

namespace HVOFSim.Presentation.Scene
{
    public sealed class LabRoomSpec
    {
        // ── Unit conversion ──────────────────────────────────────────────
        public const float CmToM = 0.01f;

        // ── Primary envelope (floor plan, in cm) ─────────────────────────
        // Back wall width (the narrow end of the room, behind the interior door).
        public float BackWallWidthCm { get; set; } = 127f;

        // Main center section depth (front-to-back at the widest part).
        public float CenterDepthCm { get; set; } = 276f;

        // Right side long wall (the wall carrying the long window band).
        public float RightWallLengthCm { get; set; } = 602f;

        // Right side extension at the top (back-right jog).
        public float RightTopExtensionCm { get; set; } = 88f;

        // Left diagonal wall length.
        public float LeftDiagonalCm { get; set; } = 500f;

        // Ceiling height (vertical).
        public float CeilingHeightCm { get; set; } = 276f;

        // ── Fixtures / niches ────────────────────────────────────────────
        // Entry alcove (the 207 x 24 isolated rectangle on the left near entrance).
        public float EntryAlcoveLengthCm { get; set; } = 207f;
        public float EntryAlcoveDepthCm  { get; set; } = 24f;
        public float EntryInnerOffsetCm  { get; set; } = 48f;

        // Radiator / small component, center-left (100 x 22).
        public float RadiatorLengthCm { get; set; } = 100f;
        public float RadiatorDepthCm  { get; set; } = 22f;
        public float RadiatorOffsetCm { get; set; } = 8f;

        // Angled feature bottom-right (151 x 14).
        public float AngledFeatureLengthCm { get; set; } = 151f;
        public float AngledFeatureDepthCm  { get; set; } = 14f;

        // ── Doorway and doors ────────────────────────────────────────────
        // Entry doorway clearance on the front wall (126 cm opening).
        public float EntryDoorClearanceCm { get; set; } = 126f;

        // Door leaves (standard residential/office door).
        public float DoorLeafWidthCm  { get; set; } = 90f;
        public float DoorLeafHeightCm { get; set; } = 210f;
        public float DoorLeafThicknessCm { get; set; } = 4f;

        // ── Right wall window band (the 40 x 590 narrow strip) ──────────
        // 590 cm long window band running along the right wall.
        public float RightWindowLengthCm { get; set; } = 590f;
        // Sill (bottom) height of the window.
        public float WindowSillHeightCm  { get; set; } = 80f;
        // Top of the window below ceiling.
        public float WindowHeadClearCm   { get; set; } = 56f;  // 276 - 220 = 56
        // Frame thickness.
        public float WindowFrameThickCm  { get; set; } = 5f;

        // Window crank handle ("C") position on right wall: 20 cm from inner
        // (back-most) edge of the window band.
        public float HandleInnerOffsetCm { get; set; } = 20f;
        public float HandleHeightCm      { get; set; } = 110f;

        // ── Blinds ───────────────────────────────────────────────────────
        public int   BlindSlatCount  { get; set; } = 32;
        public float BlindSlatTiltDeg { get; set; } = 20f;

        // ── Factory ──────────────────────────────────────────────────────
        public static LabRoomSpec FromSketch() => new LabRoomSpec();
    }
}
