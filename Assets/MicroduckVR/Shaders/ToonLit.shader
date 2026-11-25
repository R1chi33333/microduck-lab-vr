Shader "Microduck/ToonLit"
{
    // Comic toon shading (port of toon.js): the main light's N.L times its shadow is cut into hard bands
    // (lit / mid / shadow / deep); shadows lean towards violet instead of going grey; a thin rim band.
    // No screen-space halftone or hatching: those shimmer per eye in VR.
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _T1 ("Lit / mid threshold", Range(0, 1)) = 0.62
        _T2 ("Mid / shadow threshold", Range(0, 1)) = 0.22
        _T3 ("Deep shadow threshold", Range(0, 1)) = 0.06
        _MidMul ("Mid band brightness", Range(0, 1)) = 0.86
        _ShadowMul ("Shadow brightness", Range(0, 1)) = 0.62
        _DeepMul ("Deep shadow brightness", Range(0, 1)) = 0.48
        _ShadowTint ("Shadow tint (violet lean)", Color) = (0.78, 0.68, 1.0, 1)
        _TintAmount ("Shadow tint amount", Range(0, 1)) = 0.55
        _RimColor ("Rim Color", Color) = (1.0, 0.478, 0.851, 1)
        _RimT ("Rim threshold", Range(0, 1)) = 0.62
        _RimAmount ("Rim amount", Range(0, 1)) = 0.0
        _Unlit ("Unlit (flat colour)", Range(0, 1)) = 0
        // used by the Lit passes reused below
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _T1, _T2, _T3, _MidMul, _ShadowMul, _DeepMul;
                float4 _ShadowTint; float _TintAmount;
                float4 _RimColor; float _RimT, _RimAmount;
                float _Unlit;
                float _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                if (_Unlit > 0.5) return half4(baseCol, 1);

                float3 n = normalize(i.normalWS);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float lit = saturate(dot(n, L.direction)) * L.shadowAttenuation;

                float band = lit >= _T1 ? 1.0 : (lit >= _T2 ? _MidMul : (lit >= _T3 ? _ShadowMul : _DeepMul));
                float shade = 1.0 - band;                                   // 0 in the lit band
                float3 tint = lerp(float3(1, 1, 1), _ShadowTint.rgb, saturate(shade * 2.2) * _TintAmount);
                float3 col = baseCol * band * tint;

                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float fres = 1.0 - saturate(dot(n, v));
                col = lerp(col, _RimColor.rgb, step(_RimT, fres) * _RimAmount * step(_T1, lit));

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack Off
}
