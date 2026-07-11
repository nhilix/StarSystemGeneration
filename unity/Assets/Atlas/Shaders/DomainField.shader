// The domains lens as a per-pixel field over the port registry
// (unity-atlas-design.md "The camera"): each port contributes an
// owner-colored falloff scaled by its service radius; overlaps mix and a
// second claim brightens (contested light). Additive over the starfield.
Shader "StarGen/DomainField"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0.30
        _Contested ("Contested boost", Float) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // xy = world position on the plane, z = glow radius (world), w unused
            float4 _Ports[512];
            // rgb = owner color, a unused
            float4 _PortColors[512];
            int _PortCount;
            float _Intensity;
            float _Contested;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xy;
                float3 sum = 0;
                float wsum = 0;
                float wmax = 0;
                float wsecond = 0;
                for (int k = 0; k < _PortCount; k++)
                {
                    float2 d = p - _Ports[k].xy;
                    float x = saturate(1.0 - length(d) / _Ports[k].z);
                    float w = x * x;
                    sum += _PortColors[k].rgb * w;
                    wsum += w;
                    if (w > wmax) { wsecond = wmax; wmax = w; }
                    else if (w > wsecond) { wsecond = w; }
                }
                if (wsum <= 0.0001) return half4(0, 0, 0, 0);
                float3 mixColor = sum / wsum;
                float glow = saturate(wmax) * _Intensity
                           + saturate(wsecond * 2.0) * _Contested;
                return half4(mixColor * glow, 1);
            }
            ENDHLSL
        }
    }
}
