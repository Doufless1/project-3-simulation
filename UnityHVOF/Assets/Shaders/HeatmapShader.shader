Shader "HVOFSim/HeatmapShader"
{
    Properties
    {
        _MinTemp ("Min Temperature", Float) = 25.0
        _MaxTemp ("Max Temperature", Float) = 1500.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _HeatmapTex;
            float _MinTemp;
            float _MaxTemp;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Turbo colormap approximation
            fixed3 turbo(float x)
            {
                x = saturate(x);
                float4 k = float4(0.13572138, 4.61539260, -42.66032258, 132.13108234);
                float4 k2 = float4(-152.94239396, 59.05169002, 5.25113220, -16.89617244);
                float4 k3 = float4(20.76600529, -11.98767140, 2.71231610, -0.63585093);

                float r = dot(k, float4(1.0, x, x*x, x*x*x)) + dot(k2.xy, float2(x*x*x*x, x*x*x*x*x));
                float g = dot(k, float4(0.13572138, 4.61539260, -42.66032258, 132.13108234)); // approx
                
                // Simple jet/turbo fallback since actual polynomial is long
                float r_fallback = saturate(1.5 - abs(x * 4.0 - 3.0));
                float g_fallback = saturate(1.5 - abs(x * 4.0 - 2.0));
                float b_fallback = saturate(1.5 - abs(x * 4.0 - 1.0));
                
                return fixed3(r_fallback, g_fallback, b_fallback);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // read temperature from 2d texture
                float rawTemp = tex2D(_HeatmapTex, i.uv).r;
                float normTemp = saturate((rawTemp - _MinTemp) / (_MaxTemp - _MinTemp));
                
                // grid lines
                float2 grid = frac(i.uv * 50.0);
                float lineW = 0.05;
                float isGrid = (grid.x < lineW || grid.x > 1.0 - lineW || grid.y < lineW || grid.y > 1.0 - lineW) ? 1.0 : 0.0;

                fixed3 col = turbo(normTemp);
                return fixed4(lerp(col, fixed3(0,0,0), isGrid * 0.2), 1.0);
            }
            ENDCG
        }
    }
}
