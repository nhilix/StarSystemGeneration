// AtlasBillboard's sibling for the authored glyph atlas: identical
// camera-facing sizing (corner + world/px sizes on TEXCOORD0), plus a
// per-vertex UV rect on TEXCOORD1 selecting the glyph cell, and a _Tint
// the LOD fade rides. Kept separate from StarGen/AtlasBillboard so the
// K1-validated point-sprite path stays untouched.
Shader "StarGen/AtlasGlyph"
{
    Properties
    {
        _MainTex ("Glyph atlas", 2D) = "white" {}
        _MaxPx ("Max pixel size", Float) = 56
        _Tint ("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _MaxPx;
                half4 _Tint;
            CBUFFER_END
            // Explicit view globals (CameraRig / capture tooling) — the
            // built-in _ScreenParams/P proved unreliable in batch renders.
            float _AtlasFocalY;      // 1 / tan(fov/2)
            float _AtlasViewportPx;  // render-target height in pixels

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 corner     : TEXCOORD0;   // xy corner, z world size, w px size
                float4 rect       : TEXCOORD1;   // glyph UV rect (xMin,yMin,xMax,yMax)
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 centerWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centerVS = TransformWorldToView(centerWS);
                float depth = max(0.01, -centerVS.z);
                float pxWorld = 2.0 * depth
                    / (max(0.01, _AtlasFocalY) * max(1.0, _AtlasViewportPx));
                float size = max(input.corner.z, input.corner.w * pxWorld);
                size = min(size, _MaxPx * pxWorld);
                centerVS.xy += input.corner.xy * size;
                o.positionHCS = mul(UNITY_MATRIX_P, float4(centerVS, 1.0));
                o.uv = lerp(input.rect.xy, input.rect.zw, input.corner.xy + 0.5);
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return tex * input.color * _Tint;
            }
            ENDHLSL
        }
    }
}
