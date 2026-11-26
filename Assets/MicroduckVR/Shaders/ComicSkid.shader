Shader "Microduck/ComicSkid"
{
    // Skid marks (port of the skid ring buffer in skatefx.js): flat ink strokes on the floor with a rough, broken
    // edge, fading out over their life. One dynamic mesh holds all of them (ComicFX.cs); each vertex carries its
    // stroke's birth time, so nothing is touched on the CPU while they fade.
    // Transparent, no depth write and no depth or normals pass: the ink pass only sees the floor under them.
    Properties
    {
        _InkColor ("Ink", Color) = (0.106, 0.078, 0.188, 1)
        _FxTime ("Clock (s)", Float) = 0
        _Life ("Life (s)", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite Off Cull Off
        Offset -2, -2
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Name "ComicSkid"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _InkColor;
                float _FxTime, _Life;
            CBUFFER_END

            // uv0: along, across (0..1); uv1: birth time, seed
            struct Attributes { float3 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 data : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv0;
                float age = _FxTime - v.uv1.x;
                o.data = float2((1.0 - smoothstep(_Life * 0.55, _Life, age)) * step(0.0, age), v.uv1.y);
                return o;
            }

            float H(float x, float seed) { return frac(sin(x * 91.7 + seed * 13.1) * 43758.5); }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float e = abs(i.uv.y - 0.5) * 2.0;
                float rough = 0.72 + 0.28 * H(floor(i.uv.x * 6.0), i.data.y);
                if (e > rough || i.data.x < 0.01) discard;
                return half4(_InkColor.rgb, 0.72 * i.data.x);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
