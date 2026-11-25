Shader "Microduck/Vignette"
{
    // Tunnelling vignette for comfort: transparent in the middle, ink-coloured towards the edges.
    // Drawn on a quad parented to the XR camera. XR single pass instanced safe.
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0
        _Color ("Color", Color) = (0.106, 0.078, 0.188, 1)
        _Feather ("Feather", Range(0.02, 0.5)) = 0.18
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

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = length(i.uv - 0.5) * 2.0;              // 0 centre, 1 edge of the quad
                float inner = lerp(1.25, 0.42, _Strength);        // opening shrinks as strength rises
                float a = smoothstep(inner, inner + _Feather, r) * saturate(_Strength * 4.0);
                return half4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
