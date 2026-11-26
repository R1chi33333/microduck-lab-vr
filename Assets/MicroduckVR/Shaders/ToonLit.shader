Shader "Microduck/ToonLit"
{
    // Comic toon shading (port of toon.js): the main light's N.L times its shadow is cut into hard bands
    // (lit / mid / shadow / deep); shadows lean towards violet instead of going grey.
    // Print texture, VR-safe: the Ben-Day dots (mid band, growing towards the terminator), the light dots at
    // the shadow edge and the cross-hatching (deep shadow and cast shadows) are printed on the SURFACE, in the
    // object's own metric space (triplanar), not on the screen, so both eyes see the same dots stuck to the
    // object and nothing swims when the head moves. Every pattern is antialiased with its own derivatives and
    // fades to its average tone once a cell drops under ~2 px, so distant surfaces never moire.
    // Also: a coloured rim band and a hard specular pop on curved surfaces (flat walls and floors never get
    // them, however grazing the view), a hemisphere fill from the scene ambient (cool sky above, warm bounce
    // below) and a contact darkening towards the floor.
    //
    // Global switches (Shader.SetGlobalFloat, see ComicLook): _ComicPatternsOff, _ComicFillOff,
    // _ComicContactOff (1 = off; unset = on), _ComicPatternScale (cell size multiplier; unset = 1),
    // _ComicFloorY (floor height for the contact term; unset = 0).
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
        [Header(Print texture)]
        _Halftone ("Halftone / hatching amount", Range(0, 1)) = 1
        _Hatch ("Cross-hatching in deep shadow", Range(0, 1)) = 1
        _HatchDark ("Hatch line darkness", Range(0, 1)) = 0.72
        _DotCell ("Dot cell (m)", Float) = 0.008
        _HatchCell ("Hatch spacing (m)", Float) = 0.006
        _PatternFadeNear ("Pattern fade start (m)", Float) = 3.5
        _PatternFadeFar ("Pattern gone by (m)", Float) = 7
        [Header(Rim and specular)]
        _RimColor ("Rim Color", Color) = (1.0, 0.478, 0.851, 1)
        _RimT ("Rim threshold (on fresnel^3)", Range(0, 1)) = 0.55
        _RimAmount ("Rim amount", Range(0, 1)) = 0.55
        _RimCurve ("Rim/spec only where curvature > (1/m)", Float) = 2
        _SpecAmount ("Hard specular pop", Range(0, 1)) = 0
        _SpecT ("Specular threshold", Range(0, 1)) = 0.55
        _SpecPower ("Specular tightness", Range(4, 256)) = 48
        [Header(Fill and contact)]
        _FillAmount ("Hemisphere fill from ambient", Range(0, 1)) = 0.6
        _ContactAO ("Contact darkening near the floor", Range(0, 1)) = 0.45
        _ContactHeight ("Contact height (m)", Float) = 0.1
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
                float _Halftone, _Hatch, _HatchDark, _DotCell, _HatchCell, _PatternFadeNear, _PatternFadeFar;
                float4 _RimColor; float _RimT, _RimAmount, _RimCurve;
                float _SpecAmount, _SpecT, _SpecPower;
                float _FillAmount, _ContactAO, _ContactHeight;
                float _Unlit;
                float _Cutoff;
            CBUFFER_END

            // globals (never set = 0 = everything on, scale 1, floor at y = 0)
            float _ComicPatternsOff, _ComicFillOff, _ComicContactOff, _ComicPatternScale, _ComicFloorY;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
                float3 patternPos : TEXCOORD4;     // object space scaled to metres: the print sticks to the object
                float3 normalOS : TEXCOORD5;       // triplanar weights
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
                float4x4 m = GetObjectToWorldMatrix();
                float3 scale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
                o.patternPos = v.positionOS.xyz * scale;
                o.normalOS = v.normalOS;
                return o;
            }

            static const float3 LUM = float3(0.2126, 0.7152, 0.0722);

            // a band's colour: darker and leaning violet the further it is from the lit band
            float3 Tone(float3 c, float b) { return c * b * lerp(float3(1, 1, 1), _ShadowTint.rgb, saturate((1.0 - b) * 2.2) * _TintAmount); }

            // one projection plane of the print. q: plane coordinates in dot cells, px: one pixel in dot cells,
            // hr: hatch lines per dot cell. Returns (halftone dot, light dot, hatch) masks, each faded to its
            // average coverage when its cells get too small on screen (no moire, same mean tone).
            float3 PrintPlane(float2 q, float px, float hr, float dotR, float fade)
            {
                float2 r = float2(q.x * 0.9063 - q.y * 0.4226, q.x * 0.4226 + q.y * 0.9063);    // screen angle 25 deg
                float d = length(frac(r) - 0.5);
                float aaD = px * 0.75 + 0.01;
                float visD = (1.0 - smoothstep(0.3, 0.55, px)) * fade;
                float dotM = lerp(saturate(3.14159 * dotR * dotR), 1.0 - smoothstep(dotR - aaD, dotR + aaD, d), visD);
                float liteM = lerp(0.139, 1.0 - smoothstep(0.21 - aaD, 0.21 + aaD, d), visD);
                float2 h = q * hr; float pxh = px * hr;
                float a = abs(frac(h.x + h.y) - 0.5), b = abs(frac((h.x - h.y) * 0.8696) - 0.5);
                float aaH = pxh + 0.01;
                float hat = max(1.0 - smoothstep(0.14 - aaH, 0.14 + aaH, a), 0.75 * (1.0 - smoothstep(0.115 - aaH, 0.115 + aaH, b)));
                float visH = (1.0 - smoothstep(0.3, 0.55, pxh)) * fade;
                return float3(dotM, liteM, lerp(0.4, hat, visH));
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;

                float3 n = normalize(i.normalWS);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = dot(n, L.direction);
                float lit = saturate(ndl) * L.shadowAttenuation;
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                // all screen derivatives first, outside any branch
                float aa = clamp(fwidth(lit), 1e-3, 0.08);
                float gScale = _ComicPatternScale > 0.0 ? _ComicPatternScale : 1.0;
                float cell = max(_DotCell * gScale, 1e-4);
                float3 pd = i.patternPos / cell;
                float3 fwd = fwidth(pd);
                float curv = length(fwidth(n)) / max(length(fwidth(i.positionWS)), 1e-6);   // 1 / radius of curvature
                float ndv = saturate(dot(n, v));
                float fres = (1.0 - ndv) * (1.0 - ndv) * (1.0 - ndv);
                float aaR = clamp(fwidth(fres), 0.01, 0.08);
                float3 hv = normalize(L.direction + v);
                float sp = pow(saturate(dot(n, hv)), _SpecPower) * L.shadowAttenuation * step(0.0, ndl);
                float aaS = clamp(fwidth(sp) * 0.75, 0.01, 0.15);

                if (_Unlit > 0.5) return half4(baseCol, 1);

                // bands (antialiased edges, so terminators do not crawl)
                float bLit = smoothstep(_T1 - aa, _T1 + aa, lit);
                float bMid = smoothstep(_T2 - aa, _T2 + aa, lit);
                float bSh = smoothstep(_T3 - aa, _T3 + aa, lit);
                float band = lerp(lerp(lerp(_DeepMul, _ShadowMul, bSh), _MidMul, bMid), 1.0, bLit);
                float3 col = Tone(baseCol, band);

                // print texture (triplanar in object metres)
                float pat = _Halftone * (1.0 - saturate(_ComicPatternsOff));
                float3 nO = normalize(i.normalOS);
                float3 w = nO * nO; w *= w; w /= (w.x + w.y + w.z + 1e-5);
                float fade = 1.0 - smoothstep(_PatternFadeNear, _PatternFadeFar, length(GetCameraPositionWS() - i.positionWS));
                float hr = _DotCell / max(_HatchCell, 1e-4);
                float cover = 1.0 - saturate((lit - _T2) / max(_T1 - _T2, 1e-3));
                float dotR = 0.12 + 0.5 * cover;                                  // dots grow towards the terminator
                float3 m = w.x * PrintPlane(pd.yz, max(fwd.y, fwd.z), hr, dotR, fade)
                         + w.y * PrintPlane(pd.xz, max(fwd.x, fwd.z), hr, dotR, fade)
                         + w.z * PrintPlane(pd.xy, max(fwd.x, fwd.y), hr, dotR, fade);
                // mid band: dots of shadow colour
                col = lerp(col, Tone(baseCol, _ShadowMul), m.x * bMid * (1.0 - bLit) * 0.8 * pat);
                // shadow band: sparse light dots near its edge
                col = lerp(col, Tone(baseCol, _MidMul), m.y * (1.0 - bMid) * smoothstep(_T3, _T2, lit) * 0.5 * pat);
                // deep shadow and cast shadows: cross-hatching
                col = lerp(col, Tone(baseCol, _DeepMul) * _HatchDark, m.z * (1.0 - bSh) * _Hatch * pat);

                // hemisphere fill: the ambient's hue and its up/down gradient (normalised, so the ambient's
                // intensity never washes out the bands); strongest in the shadow bands
                float3 amb = SampleSH(n);
                float refL = dot(float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), LUM);
                float3 fill = refL > 1e-4 ? clamp(amb / refL, 0.5, 1.6) : float3(1, 1, 1);
                float fillW = _FillAmount * (1.0 - saturate(_ComicFillOff)) * lerp(0.3, 1.0, saturate((1.0 - band) * 2.5));
                col *= lerp(float3(1, 1, 1), fill, fillW);

                // contact darkening: the bottom of walls, furniture and legs darkens towards the floor
                float hgt = max(i.positionWS.y - _ComicFloorY, 0.0);
                float c = saturate(1.0 - hgt / max(_ContactHeight, 1e-3)); c *= c;
                float ao = c * (1.0 - smoothstep(0.55, 0.85, n.y)) * _ContactAO * (1.0 - saturate(_ComicContactOff));
                col = lerp(col, col * _ShadowTint.rgb * 0.72, ao);

                // rim band and specular pop: curved surfaces only (a flat floor seen at a grazing angle stays clean)
                float curved = smoothstep(_RimCurve * 0.5, _RimCurve, curv);
                float rim = smoothstep(_RimT - aaR, _RimT + aaR, fres) * (1.0 - bLit * 0.5) * curved;
                col = lerp(col, lerp(baseCol, _RimColor.rgb, 0.5) * 1.08, rim * _RimAmount);
                col = lerp(col, float3(1.0, 0.97, 0.92), smoothstep(_SpecT - aaS, _SpecT + aaS, sp) * _SpecAmount * curved);

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
