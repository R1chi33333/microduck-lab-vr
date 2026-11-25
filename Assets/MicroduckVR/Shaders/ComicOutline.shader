Shader "Microduck/ComicOutline"
{
    // Full-screen ink outline (used by URP's Full Screen Pass renderer feature, XR single pass instanced safe).
    // Edges come from depth and normals. The depth test uses the Laplacian of 1/z, which is zero on any
    // plane in screen space, so floors and walls seen at grazing angles never turn into black bands.
    Properties
    {
        _InkColor ("Ink", Color) = (0.106, 0.078, 0.188, 1)
        _LineWidth ("Line width (px)", Range(0.5, 4)) = 1.6
        _DepthThreshold ("Depth edge threshold", Range(0.001, 1)) = 0.06
        _NormalThreshold ("Normal edge threshold", Range(0.01, 1)) = 0.45
        _FadeStart ("Fade start (m)", Float) = 6
        _FadeEnd ("Fade end (m)", Float) = 14
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "ComicOutline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _InkColor;
            float _LineWidth, _DepthThreshold, _NormalThreshold, _FadeStart, _FadeEnd;

            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 px = _LineWidth / _ScaledScreenParams.xy;

                float zc = EyeDepth(uv);
                float zl = EyeDepth(uv - float2(px.x, 0)), zr = EyeDepth(uv + float2(px.x, 0));
                float zd = EyeDepth(uv - float2(0, px.y)), zu = EyeDepth(uv + float2(0, px.y));
                float ic = 1.0 / zc;
                float lap = abs(4.0 * ic - (1.0 / zl + 1.0 / zr + 1.0 / zd + 1.0 / zu)) / ic;
                float eDepth = step(_DepthThreshold, lap);

                float3 nc = SampleSceneNormals(uv);
                float3 nl = SampleSceneNormals(uv - float2(px.x, 0)), nr = SampleSceneNormals(uv + float2(px.x, 0));
                float3 nd = SampleSceneNormals(uv - float2(0, px.y)), nu = SampleSceneNormals(uv + float2(0, px.y));
                float minDot = min(min(dot(nc, nl), dot(nc, nr)), min(dot(nc, nd), dot(nc, nu)));
                float eNormal = step(_NormalThreshold, 1.0 - minDot);

                float edge = max(eDepth, eNormal);
                float fade = 1.0 - saturate((zc - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                return half4(lerp(col.rgb, _InkColor.rgb, edge * fade * _InkColor.a), col.a);
            }
            ENDHLSL
        }
    }
}
