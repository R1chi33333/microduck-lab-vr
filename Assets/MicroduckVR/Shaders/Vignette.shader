Shader "Microduck/Vignette"
{
    // Tunnelling vignette for comfort: transparent in the middle, ink-coloured towards the edges.
    // Drawn on a quad parented to the XR camera. XR single pass instanced safe.
    // Comic speed lines live in the same quad: static radial ink wedges in the outer ring only, locked to the view
    // (they never sweep across it, only their ink flickers a little), fading in with _SpeedLines.
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0
        _Color ("Color", Color) = (0.106, 0.078, 0.188, 1)
        _Feather ("Feather", Range(0.02, 0.5)) = 0.18
        _SpeedLines ("Speed lines", Range(0, 1)) = 0
        _LineCount ("Speed line sectors", Float) = 56
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZTest Always ZWrite Off Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Name "Vignette"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float4 _Color;
                float _Feather;
                float _SpeedLines;
                float _LineCount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Hash(float x) { return frac(sin(x * 12.9898 + 4.1414) * 43758.5453); }

            // radial ink wedges: each angular sector may hold one line, thin at its inner tip and fatter outwards
            float SpeedLines(float2 d, float r)
            {
                if (_SpeedLines < 0.002) return 0.0;
                float ang = atan2(d.y, d.x) / 6.2831853 + 0.5;            // 0..1 round the view
                float s = ang * _LineCount, id = floor(s), f = frac(s) - 0.5;
                float h1 = Hash(id), h2 = Hash(id + 17.3), h3 = Hash(id + 41.7);
                float present = step(h1, 0.25 + 0.6 * _SpeedLines);       // more lines the faster you go
                float start = lerp(0.5, 0.4, _SpeedLines) + h2 * 0.1;     // inner tip; the middle of the view stays clear
                float t = saturate((r - start) / 0.22);
                float halfW = lerp(0.03, 0.14 + 0.16 * h3, t);              // in sectors
                // antialias across the line: how many sectors one pixel spans here (continuous, unlike the angle itself)
                float aa = length(fwidth(d)) / max(r * 0.5, 1e-3) * _LineCount / 6.2831853 + 1e-4;
                float off = abs(f - (h3 - 0.5) * 0.4);
                float ink = (1.0 - smoothstep(halfW - aa, halfW + aa, off)) * smoothstep(start, start + 0.02, r);
                // a slight comic flicker, in steps (the lines never move)
                float flick = 0.8 + 0.2 * Hash(id * 3.1 + floor(_Time.y * 10.0));
                return present * ink * flick * saturate(_SpeedLines * 1.6);
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 d = i.uv - 0.5;
                float r = length(d) * 2.0;                       // 0 centre, 1 edge of the quad
                float inner = lerp(0.74, 0.24, _Strength);        // opening shrinks as strength rises (0.6 = the headset's own field of view)
                float a = smoothstep(inner, inner + _Feather, r) * saturate(_Strength * 4.0);
                a = max(a, SpeedLines(d, r));
                return half4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
