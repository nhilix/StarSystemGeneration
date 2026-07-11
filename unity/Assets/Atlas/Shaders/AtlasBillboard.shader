// Camera-facing point sprites for stars and port dots: the quad's center
// rides the vertex position, the corner offset + sizing ride TEXCOORD0
// (xy = corner in [-0.5,0.5], z = world size or 0, w = pixel size). The
// final size is the larger of the world size and the pixel size at that
// depth, capped in pixels — points stay point-like across the whole zoom
// continuum but remain localized in space.
Shader "StarGen/AtlasBillboard"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _MaxPx ("Max pixel size", Float) = 64
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
            float _MaxPx;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 corner     : TEXCOORD0;   // xy corner, z world size, w px size
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
                // World units spanned by one pixel at this depth.
                float pxWorld = 2.0 * depth / (UNITY_MATRIX_P._m11 * _ScreenParams.y);
                float size = max(input.corner.z, input.corner.w * pxWorld);
                size = min(size, _MaxPx * pxWorld);
                centerVS.xy += input.corner.xy * size;
                o.positionHCS = mul(UNITY_MATRIX_P, float4(centerVS, 1.0));
                o.uv = input.corner.xy + 0.5;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return tex * input.color;
            }
            ENDHLSL
        }
    }
}
