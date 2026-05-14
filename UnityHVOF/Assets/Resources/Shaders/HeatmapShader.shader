Shader "HVOFSim/HeatmapShader"
{
    Properties
    {
        _HeatmapTex ("Temperature Map (RFloat)", 2D) = "white" {}
        _MinTemp ("Minimum Temperature (C)", Float) = 20.0
        _MaxTemp ("Maximum Temperature (C)", Float) = 1500.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_HeatmapTex);
            SAMPLER(sampler_HeatmapTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _HeatmapTex_ST;
                float _MinTemp;
                float _MaxTemp;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _HeatmapTex_ST.xy + _HeatmapTex_ST.zw;
                return output;
            }

            // Function to generate inferno/magma style heatmap color
            half3 GetHeatmapColor(float t)
            {
                t = saturate(t);
                
                // Color stops: Dark Blue -> Cyan -> Green -> Yellow -> Red -> White
                half3 c0 = half3(0.0, 0.0, 0.5);   
                half3 c1 = half3(0.0, 0.5, 1.0);   
                half3 c2 = half3(0.0, 1.0, 0.0);   
                half3 c3 = half3(1.0, 1.0, 0.0);   
                half3 c4 = half3(1.0, 0.0, 0.0);   
                half3 c5 = half3(1.0, 1.0, 1.0);   

                float step = 0.2;
                
                if (t < step)
                    return lerp(c0, c1, t / step);
                else if (t < 2.0 * step)
                    return lerp(c1, c2, (t - step) / step);
                else if (t < 3.0 * step)
                    return lerp(c2, c3, (t - 2.0 * step) / step);
                else if (t < 4.0 * step)
                    return lerp(c3, c4, (t - 3.0 * step) / step);
                else
                    return lerp(c4, c5, (t - 4.0 * step) / step);
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Sample the single channel R float texture
                float rawTemp = SAMPLE_TEXTURE2D(_HeatmapTex, sampler_HeatmapTex, input.uv).r;
                
                // Normalize T between min and max
                float normalizedTemp = (rawTemp - _MinTemp) / max(1.0, (_MaxTemp - _MinTemp));
                
                // If it's near ambient, render base metal color
                if (normalizedTemp <= 0.01) {
                    return half4(0.3, 0.3, 0.35, 1.0); 
                }
                
                half3 heatColor = GetHeatmapColor(normalizedTemp);
                return half4(heatColor, 1.0);
            }
            ENDHLSL
        }
    }
}
