Shader "Microduck/ComicOutline"
{
    // Full-screen ink outline (used by URP's Full Screen Pass renderer feature, XR single pass instanced safe).
    // Edges come from depth and normals. The depth test uses the Laplacian of 1/z, which is zero on any
    // plane in screen space, so floors and walls seen at grazing angles never turn into black bands.
    Properties
    {
        _InkColor ("Ink", Color) = (0.106, 0.078, 0.188, 1)
        _LineWidth ("Line width (px at 1000 px tall)", Range(0.5, 6)) = 2.7
        _DepthThreshold ("Depth edge threshold", Range(0.001, 1)) = 0.06
        _NormalThreshold ("Normal edge threshold", Range(0.01, 1)) = 0.45
        _FadeStart ("Fade start (m)", Float) = 6
        _FadeEnd ("Fade end (m)", Float) = 14
        _GrassInkNear ("Grass clump ink: full until (m)", Float) = 0.9
        _GrassInkFar ("Grass clump ink: gone by (m)", Float) = 2.6
        _GrassStep ("Grass clump depth step (m)", Float) = 0.012
        _FarThin ("Thinner line on far things (0 off)", Range(0, 0.8)) = 0
        _ThinNear ("Thinner from (m)", Float) = 0.6
        _ThinFar ("Thinnest by (m)", Float) = 2.0
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
            float _LineWidth, _DepthThreshold, _NormalThreshold, _FadeStart, _FadeEnd, _GrassInkNear, _GrassInkFar, _GrassStep, _FarThin, _ThinNear, _ThinFar;
            float _InkCamScale;      // set while a mirror draws its reflection: the line weight for that picture (0 = as usual)

            // a grass tuft's silhouette: the neighbour is something else (another tuft, the soil, an object) and lies behind it
            float TuftEdge(float3 nc, float zc, float3 nn, float zn) { return (zn - zc > _GrassStep && (abs(nn.y - nc.y) + abs(nn.z - nc.z) > 0.012 || dot(nn, nn) >= 0.25)) ? 1.0 : 0.0; }

            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // the width is in pixels of a 1000 px tall image, so headset eyes and screenshots get the same weight of line
                float zc = EyeDepth(uv);
                // (filming, at several samples a pixel: a finer pen on things further off, so a duck across the room is drawn, not blotted)
                float2 px = _LineWidth * (_InkCamScale > 0.001 ? _InkCamScale : 1.0) * (1.0 - _FarThin * smoothstep(_ThinNear, _ThinFar, zc)) * max(_ScaledScreenParams.y / 1000.0, 0.6) / _ScaledScreenParams.xy;

                float zl = EyeDepth(uv - float2(px.x, 0)), zr = EyeDepth(uv + float2(px.x, 0));
                float zd = EyeDepth(uv - float2(0, px.y)), zu = EyeDepth(uv + float2(0, px.y));

                float3 nc = SampleSceneNormals(uv);
                float3 nl = SampleSceneNormals(uv - float2(px.x, 0)), nr = SampleSceneNormals(uv + float2(px.x, 0));
                float3 nd = SampleSceneNormals(uv - float2(0, px.y)), nu = SampleSceneNormals(uv + float2(0, px.y));
                // lawn (grass blades and their soil) writes a zero normal: it is never inked itself, and blades in
                // front of something do not ink that thing either. Things standing in front of the lawn keep their line.
                bool gc = dot(nc, nc) < 0.25;
                bool gl = dot(nl, nl) < 0.25, gr = dot(nr, nr) < 0.25, gd = dot(nd, nd) < 0.25, gu = dot(nu, nu) < 0.25;
                if (gl && zl < zc) zl = zc;
                if (gr && zr < zc) zr = zc;
                if (gd && zd < zc) zd = zc;
                if (gu && zu < zc) zu = zc;

                float ic = 1.0 / zc;
                float lap = abs(4.0 * ic - (1.0 / zl + 1.0 / zr + 1.0 / zd + 1.0 / zu)) / ic;
                float eDepth = step(_DepthThreshold, lap);

                float minDot = min(min(gl ? 1.0 : dot(nc, nl), gr ? 1.0 : dot(nc, nr)), min(gd ? 1.0 : dot(nc, nd), gu ? 1.0 : dot(nc, nu)));
                float eNormal = step(_NormalThreshold, 1.0 - minDot);

                float edge = max(eDepth, eNormal);
                if (gc)
                {
                    edge = 0.0;
                    if (nc.x > 0.12)
                    {
                        edge = max(max(TuftEdge(nc, zc, nl, zl), TuftEdge(nc, zc, nr, zr)), max(TuftEdge(nc, zc, nd, zd), TuftEdge(nc, zc, nu, zu)));
                        edge *= 1.0 - smoothstep(_GrassInkNear, _GrassInkFar, zc);
                    }
                }
                float fade = 1.0 - saturate((zc - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                return half4(lerp(col.rgb, _InkColor.rgb, edge * fade * _InkColor.a), col.a);
            }
            ENDHLSL
        }
    }
}
