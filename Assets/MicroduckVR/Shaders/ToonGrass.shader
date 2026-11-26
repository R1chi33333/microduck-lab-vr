Shader "Microduck/ToonGrass"
{
    // Comic lawn: fat leaf blades in flat colour bands (root / mid / tip), big two-tone patches like comic
    // colouring, light wind waves rolling across, and blades that part around ducks and balls.
    // The blades write a zero normal into the normals texture: the ink pass reads that as "lawn" and never
    // outlines blade against blade, so the lawn stays one clean mass with ink only where other things meet it.
    // _Ground = 1 is the soil under the blades (same patches, root colour, no motion).
    Properties
    {
        _RootA ("Root A", Color) = (0.184, 0.561, 0.251, 1)
        _MidA ("Mid A", Color) = (0.275, 0.651, 0.290, 1)
        _TipA ("Tip A", Color) = (0.424, 0.749, 0.310, 1)
        _RootB ("Root B", Color) = (0.243, 0.604, 0.271, 1)
        _MidB ("Mid B", Color) = (0.365, 0.722, 0.298, 1)
        _TipB ("Tip B", Color) = (0.561, 0.827, 0.353, 1)
        _WindTip ("Wind wave tip", Color) = (0.776, 0.933, 0.478, 1)
        _ShadowMul ("Shadow colour", Color) = (0.62, 0.66, 0.86, 1)
        _ClumpShade ("Clump shaded side", Color) = (0.70, 0.84, 0.80, 1)
        _InkTufts ("Share of tufts with an ink outline", Range(0, 1)) = 0.8
        _PatchScale ("Patch scale", Float) = 0.75
        _WindDir ("Wind dir (xz) wavelength speed", Vector) = (0.94, 0.34, 2.6, 1.1)
        _Sway ("Idle sway", Range(0, 1)) = 0.1
        _WaveLean ("Wave lean", Range(0, 1.5)) = 0.55
        _Ground ("Ground (0 blades, 1 soil)", Range(0, 1)) = 0
        _GrassHatch ("Hatching in cast shadow", Range(0, 1)) = 1
        _GrassHatchDark ("Hatch darkness", Range(0, 1)) = 0.74
        _GrassHatchCell ("Soil hatch spacing (m)", Float) = 0.009
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _RootA, _MidA, _TipA, _RootB, _MidB, _TipB, _WindTip, _ShadowMul, _ClumpShade;
            float _PatchScale;
            float4 _WindDir;
            float _Sway, _WaveLean, _Ground, _InkTufts;
            float _GrassHatch, _GrassHatchDark, _GrassHatchCell;
        CBUFFER_END

        // comic print globals shared with ToonLit (unset = on, scale 1)
        float _ComicPatternsOff, _ComicPatternScale;

        // set by GrassField: xyz = position, w = radius
        float4 _GrassPushers[12];
        float _GrassPusherCount;

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 blade : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float4 data : TEXCOORD1;          // x height fraction, y patch, z wave, w random
            float4 tuft : TEXCOORD2;          // x shaded side of the clump, y inked, zw tuft id
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float VNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
        }
        float Patch(float2 xz) { return VNoise(xz * _PatchScale) * 0.65 + VNoise(xz * _PatchScale * 2.7 + 7.3) * 0.35; }
        float Wave(float2 xz)
        {
            float2 d = normalize(_WindDir.xy);
            float ph = dot(xz, d) * (6.2832 / _WindDir.z) - _Time.y * _WindDir.w + (VNoise(xz * 0.6) - 0.5) * 5.0;
            return sin(ph);
        }

        Varyings GrassVert(Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 ws = TransformObjectToWorld(v.positionOS.xyz);
            if (_Ground > 0.5)
            {
                o.data = float4(0, Patch(ws.xz), 0, 0.5);
                o.tuft = 0;
            }
            else
            {
                float2 root = v.blade.xy; float h = v.blade.z, rnd = v.blade.w, t = v.uv.y;
                float wave = Wave(root);
                // cel shading per clump: the blades on the side away from the sun sit in the clump's own shadow
                float2 away = -normalize(_MainLightPosition.xz + float2(1e-4, 0));
                float shaded = step(0.0035, dot(ws.xz - root, away));
                float2 id = float2(Hash(root), Hash(root + 17.3));
                o.tuft = float4(shaded, step(1.0 - _InkTufts, Hash(root + 5.1)), id);
                float lean = smoothstep(0.35, 0.95, wave);
                float2 wd = normalize(_WindDir.xy);
                float2 off = wd * (_Sway * sin(_Time.y * 2.3 + rnd * 6.2832 + root.x * 2.0) + _WaveLean * lean) * h;
                float down = 0.25 * lean;
                // part around ducks and balls
                int n = (int)_GrassPusherCount;
                for (int k = 0; k < n; k++)
                {
                    float4 p = _GrassPushers[k];
                    float2 d = root - p.xz; float dist = length(d);
                    float f = saturate(1.0 - dist / p.w); f = f * f * (3.0 - 2.0 * f);
                    off += (dist > 1e-4 ? d / dist : float2(1, 0)) * f * h * 1.1;
                    down = max(down, f * 0.7);
                }
                float t2 = t * t;
                ws.xz += off * t2;
                ws.y -= down * h * t2;
                o.data = float4(t, Patch(root) + (rnd - 0.5) * 0.12, wave, rnd);
            }
            o.positionWS = ws;
            o.positionCS = TransformWorldToHClip(ws);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex GrassVert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = i.data.x; bool b = i.data.y > 0.5;
                float3 root = b ? _RootB.rgb : _RootA.rgb, mid = b ? _MidB.rgb : _MidA.rgb, tip = b ? _TipB.rgb : _TipA.rgb;
                // hard comic bands up the blade; the band heights wobble a little per blade
                float j = (i.data.w - 0.5) * 0.14;
                float3 col = t < 0.34 + j ? root : (t < 0.68 + j ? mid : tip);
                if (_Ground < 0.5 && i.data.z > 0.8 && t >= 0.5 + j) col = _WindTip.rgb;       // the crest of a wind wave
                if (i.tuft.x > 0.5) col *= _ClumpShade.rgb;
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                float sh = MainLightRealtimeShadow(sc);
                float inShadow = 1.0 - step(0.5, sh);
                // cast-shadow hatching. Soil: cross-hatch lines fixed to the ground (world xz), antialiased and
                // faded to their mean tone once the spacing drops under ~2 px. Blades move in the wind, so a
                // world pattern would crawl over them: in shadow their root band takes the hatch tone instead.
                float gScale = _ComicPatternScale > 0.0 ? _ComicPatternScale : 1.0;
                float2 q = i.positionWS.xz / max(_GrassHatchCell * gScale, 1e-4);
                float px = max(fwidth(q.x), fwidth(q.y));
                float a = abs(frac(q.x + q.y) - 0.5), bb = abs(frac((q.x - q.y) * 0.8696) - 0.5);
                float aaH = px + 0.01;
                float hat = max(1.0 - smoothstep(0.14 - aaH, 0.14 + aaH, a), 0.75 * (1.0 - smoothstep(0.115 - aaH, 0.115 + aaH, bb)));
                hat = lerp(0.4, hat, 1.0 - smoothstep(0.3, 0.55, px));
                float bladeHatch = 1.0 - step(0.34 + j, t);                  // the root band of a blade
                float hatchM = (_Ground > 0.5 ? hat : bladeHatch * 0.8) * inShadow * _GrassHatch * (1.0 - saturate(_ComicPatternsOff));
                col = lerp(col, col * _ShadowMul.rgb, inShadow);
                col *= lerp(1.0, _GrassHatchDark, hatchM);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex GrassVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag (Varyings i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex GrassVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            // a short "normal" (length < 0.5) = "this is lawn": x says whether this tuft gets an ink silhouette, yz are
            // the tuft's id, so the ink pass can outline whole clumps and never blade against blade inside one
            half4 frag (Varyings i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return half4(i.tuft.y * 0.25, i.tuft.z * 0.25, i.tuft.w * 0.25, 0); }
            ENDHLSL
        }
    }
}
