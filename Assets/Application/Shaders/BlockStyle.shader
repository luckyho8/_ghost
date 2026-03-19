Shader "Custom/BlockStyle"
{
    Properties
    {
        // MaterialPropertyBlock 호환 (_BaseColor / _Color 모두 지원)
        _BaseColor    ("Base Color",    Color)              = (1, 0.3, 0.3, 1)
        _Color        ("Color",         Color)              = (1, 0.3, 0.3, 1)
        _CornerRadius ("Corner Radius", Range(0.01, 0.45)) = 0.18
        _TopHighlight ("Top Highlight", Range(0, 1))       = 0.45
        _SideDarken   ("Side Darken",   Range(0, 0.9))     = 0.35
        _BorderWidth  ("Border Width",  Range(0.0, 0.25))  = 0.08
        _BorderDarken ("Border Darken", Range(0, 1))       = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Back

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Color;
                float  _CornerRadius;
                float  _TopHighlight;
                float  _SideDarken;
                float  _BorderWidth;
                float  _BorderDarken;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // 둥근 사각형 SDF: 음수=내부, 양수=외부
            float RoundedRectSDF(float2 uv, float r)
            {
                float2 d = abs(uv - 0.5) - (0.5 - r);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float3 posWS  = IN.positionWS;

                // 윗면 판별 (월드 normal.y >= 0.7)
                float isTop  = step(0.7, normal.y);
                float isSide = 1.0 - isTop;

                // 윗면 UV: 월드 XZ 좌표를 블록 1칸(1유닛) 단위로 0~1 매핑
                // +0.5 오프셋 → 블록 중심이 UV (0.5, 0.5)
                float2 faceUV = frac(posWS.xz + 0.5);

                // 둥근 모서리 SDF
                float sdf = RoundedRectSDF(faceUV, _CornerRadius);

                // 윗면만 모서리 클리핑, 옆면은 통과
                float clipVal = lerp(1.0, -sdf, isTop);
                clip(clipVal - 0.001);

                float3 col = _BaseColor.rgb;

                // ── 윗면 ────────────────────────────────
                // 하이라이트: 위쪽 중앙에 밝은 타원형 광택
                float2 hlUV = faceUV - float2(0.5, 0.65);
                float  hl   = saturate(1.0 - length(hlUV * float2(1.4, 1.0)) * 2.2);
                hl = hl * hl * _TopHighlight * isTop;
                col += hl;

                // 테두리: 둥근 모서리 안쪽 어두운 링
                float borderMask = (1.0 - saturate((-sdf) / max(_BorderWidth, 0.001))) * isTop;
                col *= (1.0 - _BorderDarken * borderMask);

                // ── 옆면 ────────────────────────────────
                col -= col * _SideDarken * isSide;

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
