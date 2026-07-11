// The domains lens as a per-pixel field over the port registry.
// Semantics (K1 second eyeball): a polity's territory is the UNION of its
// ports' service areas — per-polity max field, uniform flat fill, so same-
// polity overlaps merge invisibly. Each combined region wears a crisp
// border outline; where two DIFFERENT polities' regions intersect, the
// Venn overlap is shaded by their relationship (war/tension/warm/neutral,
// baked into _RelationTex by the read model) with both outlines drawn
// through it.
Shader "StarGen/DomainField"
{
    Properties
    {
        _FillIntensity ("Fill intensity", Float) = 0.16
        _OverlapIntensity ("Overlap intensity", Float) = 0.30
        _BorderIntensity ("Border intensity", Float) = 0.55
        _BorderPx ("Border width (px)", Float) = 1.6
        _RelationTex ("Relation matrix", 2D) = "black" {}
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
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #define MAX_PORTS 512
            #define MAX_SLOTS 32

            // xy = world position, z = service radius (world), w = polity slot
            float4 _Ports[MAX_PORTS];
            float4 _SlotColors[MAX_SLOTS];
            int _PortCount;
            CBUFFER_START(UnityPerMaterial)
                float _FillIntensity;
                float _OverlapIntensity;
                float _BorderIntensity;
                float _BorderPx;
            CBUFFER_END
            TEXTURE2D(_RelationTex);
            SAMPLER(sampler_RelationTex);

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

            // Border band just inside a field's zero edge, ~_BorderPx wide
            // on screen regardless of zoom (fwidth-scaled).
            float BorderMask(float field)
            {
                float e = fwidth(field) * _BorderPx;
                return step(0.0001, field) * (1.0 - smoothstep(e, e * 2.0, field));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xy;

                float fields[MAX_SLOTS];
                [unroll(MAX_SLOTS)]
                for (int s = 0; s < MAX_SLOTS; s++) fields[s] = 0.0;

                for (int k = 0; k < _PortCount; k++)
                {
                    float2 d = p - _Ports[k].xy;
                    float f = saturate(1.0 - length(d) / _Ports[k].z);
                    int slot = (int)_Ports[k].w;
                    fields[slot] = max(fields[slot], f);
                }

                // Top two polities at this pixel.
                float f1 = 0.0, f2 = 0.0;
                int i1 = 0, i2 = 0;
                for (int s2 = 0; s2 < MAX_SLOTS; s2++)
                {
                    float f = fields[s2];
                    if (f > f1) { f2 = f1; i2 = i1; f1 = f; i1 = s2; }
                    else if (f > f2) { f2 = f; i2 = s2; }
                }
                if (f1 <= 0.0001) return half4(0, 0, 0, 0);

                float3 c1 = _SlotColors[i1].rgb;
                float3 c2 = _SlotColors[i2].rgb;
                bool overlap = f2 > 0.0001;

                float3 fill;
                if (overlap)
                {
                    float2 uv = float2((i1 + 0.5) / MAX_SLOTS,
                                       (i2 + 0.5) / MAX_SLOTS);
                    float3 rel = SAMPLE_TEXTURE2D(_RelationTex,
                        sampler_RelationTex, uv).rgb;
                    fill = lerp((c1 + c2) * 0.5, rel, 0.65) * _OverlapIntensity;
                }
                else
                {
                    fill = c1 * _FillIntensity;
                }

                // Outlines: the union edge of each involved polity — drawn
                // through the overlap too, for the Venn read.
                float3 border = c1 * (BorderMask(f1) * _BorderIntensity);
                if (overlap)
                    border += c2 * (BorderMask(f2) * _BorderIntensity);

                // Colors and intensities are composed in sRGB (the design
                // artifact's space); linearize once for the linear buffer.
                return half4(SRGBToLinear(fill + border), 1);
            }
            ENDHLSL
        }
    }
}
