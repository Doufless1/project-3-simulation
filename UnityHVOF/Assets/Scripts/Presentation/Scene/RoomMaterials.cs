// ============================================================================
// RoomMaterials — URP/Lit material palette matching the photographs of the
// physical room: cream painted walls, white doors, off-white speckled vinyl
// floor, aluminum slat blinds, tinted glass, brushed metal hardware.
//
// Single responsibility: own the room's visual finish. Kept separate from
// LabSceneBuilder so equipment paint and room paint evolve independently.
// ============================================================================

using UnityEngine;
using Material = UnityEngine.Material;

namespace HVOFSim.Presentation.Scene
{
    public sealed class RoomMaterials
    {
        public Material Wall         { get; private set; }
        public Material Ceiling      { get; private set; }
        public Material Floor        { get; private set; }
        public Material DoorLeaf     { get; private set; }
        public Material DoorFrame    { get; private set; }
        public Material WindowFrame  { get; private set; }
        public Material Glass        { get; private set; }
        public Material BlindSlat    { get; private set; }
        public Material BrushedMetal { get; private set; }
        public Material Skirting     { get; private set; }
        public Material CurtainFrame { get; private set; }

        public static RoomMaterials PhotoMatch()
        {
            var mats = new RoomMaterials();
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            mats.Wall = new Material(shader) { color = Hex("#EFEAE0") };
            mats.Wall.SetFloat("_Metallic", 0f);
            mats.Wall.SetFloat("_Smoothness", 0.18f);

            mats.Ceiling = new Material(shader) { color = Hex("#F3F1EC") };
            mats.Ceiling.SetFloat("_Metallic", 0f);
            mats.Ceiling.SetFloat("_Smoothness", 0.1f);

            mats.Floor = new Material(shader) { color = Hex("#D9D4CC") };
            mats.Floor.SetFloat("_Metallic", 0.0f);
            mats.Floor.SetFloat("_Smoothness", 0.35f);

            mats.DoorLeaf = new Material(shader) { color = Hex("#F4F2EE") };
            mats.DoorLeaf.SetFloat("_Metallic", 0f);
            mats.DoorLeaf.SetFloat("_Smoothness", 0.28f);

            mats.DoorFrame = new Material(shader) { color = Hex("#EDEAE3") };
            mats.DoorFrame.SetFloat("_Smoothness", 0.22f);

            mats.WindowFrame = new Material(shader) { color = Hex("#E8E4DC") };
            mats.WindowFrame.SetFloat("_Smoothness", 0.32f);

            mats.Glass = new Material(shader) { color = new Color(0.78f, 0.82f, 0.85f, 0.12f) };
            SetTransparent(mats.Glass);
            mats.Glass.SetFloat("_Metallic", 0f);
            mats.Glass.SetFloat("_Smoothness", 0.95f);

            mats.BlindSlat = new Material(shader) { color = Hex("#DCDCD6") };
            mats.BlindSlat.SetFloat("_Metallic", 0.35f);
            mats.BlindSlat.SetFloat("_Smoothness", 0.55f);

            mats.BrushedMetal = new Material(shader) { color = Hex("#B8B9BC") };
            mats.BrushedMetal.SetFloat("_Metallic", 0.9f);
            mats.BrushedMetal.SetFloat("_Smoothness", 0.6f);

            mats.Skirting = new Material(shader) { color = Hex("#C9C3B8") };
            mats.Skirting.SetFloat("_Smoothness", 0.25f);

            // Dark anodized-aluminum curtain-wall mullions (photo reference:
            // thin dark-grey vertical frames of the floor-to-ceiling glazing).
            mats.CurtainFrame = new Material(shader) { color = Hex("#3A3D40") };
            mats.CurtainFrame.SetFloat("_Metallic", 0.7f);
            mats.CurtainFrame.SetFloat("_Smoothness", 0.55f);

            return mats;
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta;
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
