using UnityEngine;
using HVOFSim.Domain.Entities;
using Material = UnityEngine.Material;

namespace HVOFSim.Presentation.VFX
{
    [RequireComponent(typeof(Renderer))]
    public class HeatmapRenderer : MonoBehaviour
    {
        private Material _heatmapMat;
        private Texture2D _heatmapTex;

        public void Initialize(Material mat)
        {
            _heatmapMat = mat;
            _heatmapTex = new Texture2D(256, 256, TextureFormat.RFloat, false);
            _heatmapTex.wrapMode = TextureWrapMode.Clamp;
            _heatmapTex.filterMode = FilterMode.Bilinear;
            _heatmapMat.SetTexture("_HeatmapTex", _heatmapTex);
            
            // Initial clear State
            ClearHeatmap();
        }

        public void UpdateFromSimulation(SimulationResult result)
        {
            if (_heatmapMat == null || _heatmapTex == null) return;
            if (result.TemperatureField == null) return;

            int nx = result.TemperatureField.GetLength(0);
            int ny = result.TemperatureField.GetLength(1);

            // Resize if needed
            if (_heatmapTex.width != nx || _heatmapTex.height != ny)
            {
                _heatmapTex.Reinitialize(nx, ny);
            }

            // Temperature is mostly 2D cross section. We will map X to U and Y to V.
            var colors = new Unity.Collections.NativeArray<Color>(nx * ny, Unity.Collections.Allocator.Temp);
            float maxTemp = 25f;

            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float temp = (float)result.TemperatureField[x, y, 0]; // z=0 is top surface
                    if(temp > maxTemp) maxTemp = temp;
                    // Y in simulation is often depth (0 is top), in Unity texture we reverse V
                    int texY = ny - 1 - y;
                    colors[texY * nx + x] = new Color(temp, 0, 0, 1f); 
                }
            }

            _heatmapTex.SetPixelData(colors, 0);
            _heatmapTex.Apply();

            _heatmapMat.SetFloat("_MaxTemp", Mathf.Max(100f, maxTemp));
            
            colors.Dispose();
        }

        public void ClearHeatmap()
        {
            if (_heatmapTex == null) return;
            var colors = new Unity.Collections.NativeArray<Color>(_heatmapTex.width * _heatmapTex.height, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(25f, 0, 0, 1f);
            _heatmapTex.SetPixelData(colors, 0);
            _heatmapTex.Apply();
            colors.Dispose();
        }
    }
}
