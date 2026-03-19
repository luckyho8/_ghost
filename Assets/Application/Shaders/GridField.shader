Shader "Custom/GridField"
{
    Properties
    {
        _BgColor       ("Background Color",       Color)           = (0.10, 0.09, 0.20, 1)
        _CellColor     ("Cell Color",             Color)           = (0.13, 0.12, 0.26, 1)
        _LineColor     ("Grid Line Color",        Color)           = (0.20, 0.18, 0.36, 1)
        _GridSize      ("Grid Size (World Units)", Float)          = 1.0
        _LineThickness ("Line Thickness",         Range(0.01, 0.2)) = 0.04
        _CellRound     ("Cell Corner Softness",   Range(0, 0.5))   = 0.18
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _CellColor;
                float4 _LineColor;
                float  _GridSize;
                float  _LineThickness;
                float  _CellRound;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // 블록 중심이 정수 좌표에 있으므로 +0.5 오프셋 → 격자선이 블록 경계에 정렬
                float2 worldXZ = (IN.positionWS.xz + 0.5) / _GridSize;
                float2 cellUV  = frac(worldXZ);   // 각 셀 안에서 0~1

                // ── 격자선 마스크 (AA 포함) ──────────────────
                float2 df        = fwidth(worldXZ) * 1.5;
                float2 distEdge  = min(cellUV, 1.0 - cellUV);
                float2 lineMask  = 1.0 - smoothstep(_LineThickness - df, _LineThickness + df, distEdge);
                float  isLine    = saturate(lineMask.x + lineMask.y);

                // ── 셀 내부 밝기 (모서리 → 어두움) ──────────
                float2 d       = abs(cellUV - 0.5) - (0.5 - _CellRound);
                float  sdf     = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - _CellRound;
                float  cellIn  = saturate(-sdf / max(_CellRound, 0.001));

                float3 col = _BgColor.rgb;
                col = lerp(col, _CellColor.rgb, cellIn * (1.0 - isLine));
                col = lerp(col, _LineColor.rgb, isLine);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
