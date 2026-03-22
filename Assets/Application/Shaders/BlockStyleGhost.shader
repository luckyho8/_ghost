Shader "Custom/BlockStyleGhost"
{
    Properties
    {
        _BaseColor    ("Base Color",    Color)              = (1, 0.3, 0.3, 0.25)
        _Color        ("Color",         Color)              = (1, 0.3, 0.3, 0.25)
        _Alpha        ("Alpha",         Range(0, 1))        = 0.25
        _CornerRadius ("Corner Radius", Range(0.01, 0.45)) = 0.18
        _TopHighlight ("Top Highlight", Range(0, 1))       = 0.45
        _SideDarken   ("Side Darken",   Range(0, 0.9))     = 0.35
        _BorderWidth  ("Border Width",  Range(0.0, 0.25))  = 0.08
        _BorderDarken ("Border Darken", Range(0, 1))       = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
                float  _Alpha;
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

            float RoundedRectSDF(float2 uv, float r)
            {
                float2 d = abs(uv - 0.5) - (0.5 - r);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float3 posWS  = IN.positionWS;

                float isTop  = step(0.7, normal.y);
                float isSide = 1.0 - isTop;

                float2 faceUV = frac(posWS.xz + 0.5);
                float sdf = RoundedRectSDF(faceUV, _CornerRadius);

                float clipVal = lerp(1.0, -sdf, isTop);
                clip(clipVal - 0.001);

                float3 col = _BaseColor.rgb;

                // Top highlight
                float2 hlUV = faceUV - float2(0.5, 0.65);
                float  hl   = saturate(1.0 - length(hlUV * float2(1.4, 1.0)) * 2.2);
                hl = hl * hl * _TopHighlight * isTop;
                col += hl;

                // Border
                float borderMask = (1.0 - saturate((-sdf) / max(_BorderWidth, 0.001))) * isTop;
                col *= (1.0 - _BorderDarken * borderMask);

                // Side darken
                col -= col * _SideDarken * isSide;

                return float4(saturate(col), _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
