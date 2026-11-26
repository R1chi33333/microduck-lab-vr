Shader "Microduck/ComicFX"
{
    // Comic skate effects (port of skatefx.js): smoke puffs, sparks, glints, bolts and speed streaks, all from one
    // hand-drawn atlas (4 x 2 cells), drawn as camera-facing quads out of one dynamic mesh (ComicFX.cs).
    // Every vertex of a quad carries the particle's centre; the vertex shader spreads the corners out in view space,
    // so each eye gets its own billboard in single pass instanced stereo. Bolts and streaks turn to lie along their
    // direction as seen on screen and stretch along it.
    // Alpha-clipped and opaque, with depth and normals passes: the full-screen ink pass then sees the puffs as
    // surfaces (it outlines their silhouettes and never draws the edges of things behind them over a puff).
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Cutoff;
        CBUFFER_END

        // uv0: corner x, corner y (-0.5..0.5), rotation (rad), atlas cell; uv1: direction * stretch (world), size (m)
        struct Attributes { float3 positionOS : POSITION; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings FxVert(Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            ZERO_INITIALIZE(Varyings, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 ws = TransformObjectToWorld(v.positionOS);
            float3 mv = TransformWorldToView(ws);               // this eye's view space
            float ang = v.uv0.z, st = 1.0;
            float3 dir = v.uv1.xyz; float dl = length(dir);
            if (dl > 1e-4)
            {
                // the direction's angle on screen: project a point a little way along it
                float3 m2 = TransformWorldToView(ws + dir / dl * 0.05);
                float2 d = m2.xy / -m2.z - mv.xy / -mv.z;
                if (dot(d, d) > 1e-12) ang += atan2(d.y, d.x);
                st = max(1.0, dl);
            }
            float2 c = v.uv0.xy * float2(st, 1.0) * v.uv1.w;
            float s, co; sincos(ang, s, co);
            mv.xy += float2(co * c.x - s * c.y, s * c.x + co * c.y);
            o.positionCS = TransformWViewToHClip(mv);
            // atlas: cell 0..3 on the top row, 4..7 on the bottom row
            float cell = v.uv0.w, cx = cell - 4.0 * floor(cell / 4.0), cy = floor(cell / 4.0);
            float2 cu = v.uv0.xy + 0.5;
            o.uv = float2((cx + cu.x) * 0.25, (1.0 - cy + cu.y) * 0.5);
            o.normalWS = UNITY_MATRIX_V[2].xyz;                 // facing this eye (view space +z, in world space)
            return o;
        }

        half4 FxSample(Varyings i)
        {
            half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            clip(t.a - _Cutoff);
            return t;
        }
        ENDHLSL

        Pass
        {
            Name "ComicFX"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(FxSample(i).rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag (Varyings i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); FxSample(i); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag (Varyings i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); FxSample(i); return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
