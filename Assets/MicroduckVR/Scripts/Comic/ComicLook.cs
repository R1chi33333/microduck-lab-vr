using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MicroduckVR.Comic
{
    /// <summary>
    /// The comic palette and the duck's part colouring (port of look.js RULES / SKINS): every rig mesh is
    /// matched by name to a colour kind; small hardware and internal electronics are hidden.
    /// </summary>
    public static class ComicLook
    {
        public static readonly Color Ink = Hex("#1B1430");
        public static readonly Color Paper = Hex("#FFF7E8");

        public class Skin { public Color shell, accent, accent2, soft, secondary; }
        public static readonly Dictionary<string, Skin> Skins = new Dictionary<string, Skin>
        {
            { SkinIds.Cream,    new Skin { shell = Hex("#FBF4E4"), accent = Hex("#FF8A1F"), accent2 = Hex("#FFC928"), soft = Hex("#F3D9E8"), secondary = Hex("#DCE0E7") } },
            { SkinIds.Graphite, new Skin { shell = Hex("#4A505C"), accent = Hex("#FFC928"), accent2 = Hex("#FFC928"), soft = Hex("#6B7280"), secondary = Hex("#353A44") } },
            { SkinIds.Lavender, new Skin { shell = Hex("#C3B2EC"), accent = Hex("#FFC928"), accent2 = Hex("#FFD95A"), soft = Hex("#EEE6FA"), secondary = Hex("#9D93BD") } },
            { SkinIds.Sky,      new Skin { shell = Hex("#A6D8F5"), accent = Hex("#FF8A1F"), accent2 = Hex("#FFC928"), soft = Hex("#E6F4FC"), secondary = Hex("#8FB0C8") } },
        };

        static readonly Dictionary<string, Color> Fixed = new Dictionary<string, Color>
        {
            { "servo", Hex("#97A0AE") }, { "graphite", Hex("#97A0AE") }, { "steelGrey", Hex("#97A0AE") }, { "steel", Hex("#B8BFCA") },
            { "eye", Hex("#0B0C12") }, { "bezel", Hex("#2A2B2E") }, { "highlight", Color.white },
            { "pcb", Hex("#0C4A33") }, { "chip", Hex("#5B6372") }, { "battery", Hex("#3A6EA5") },
        };

        // [pattern, kind] in order, first match wins (look.js RULES)
        static readonly (Regex re, string kind)[] Rules =
        {
            (R(@"^leg_.*(bracket|plate)|^leg_.*_(hip|shin)$"), "servo"),
            (R(@"^electronics_camera_lens$"), "eye"),
            (R(@"^electronics_camera_lens_holder"), "bezel"),
            (R(@"^eye_highlight"), "highlight"),
            (R(@"^eye_"), "yellow"),
            (R(@"^shell_beak_(upper|lower_soft)"), "soft"),
            (R(@"^shell_beak|beak|bill|mouth"), "orange"),
            (R(@"^shell_head_bottom"), "orange"),
            (R(@"^shell_head_face"), "warmGrey"),
            (R(@"^shell_"), "shell"),
            (R(@"^foot_.*sole"), "yellow"),
            (R(@"^foot_"), "orange"),
            (R(@"^leg_.*thigh$"), "shell"),
            (R(@"^leg_.*bracket"), "graphite"),
            (R(@"^leg_"), "warmGrey"),
            (R(@"^neck_.*bracket"), "warmGrey"),
            (R(@"^neck_pitch_link"), "steelGrey"),
            (R(@"^neck_"), "graphite"),
            (R(@"^head_"), "graphite"),
            (R(@"^frame_"), "graphite"),
            (R(@"^bearing_"), "steel"),
            (R(@"^servo_"), "servo"),
            (R(@"^electronics_.*(servo_hat|main_board|pcb$|board)"), "pcb"),
            (R(@"^electronics_battery_holder"), "warmGrey"),
            (R(@"^electronics_battery"), "battery"),
            (R(@"^electronics_.*(locker)"), "graphite"),
            (R(@"^electronics_"), "chip"),
        };
        static readonly Regex Hide = R(@"^(bearing_|neck_bracket_|screw|bolt|nut_|horn_|servo_horn|cable)");
        static readonly Regex Internal = R(@"^electronics_(battery|battery_holder|main_board|speaker|pcb_locker|cable)");

        static Regex R(string p) => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static Shader toon;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Shader ToonShader => toon != null ? toon : (toon = Shader.Find("Microduck/ToonLit"));

        /// <summary>Web toonify() defaults for the coloured rim band (uTnRim, uTnRimT).</summary>
        public const float DefaultRim = 0.55f, DefaultRimT = 0.55f;

        /// <summary>
        /// A runtime toon material (cached by key). rim: coloured edge light; spec: hard specular pop (0 = matte).
        /// Runtime things (toys, props, gates) stand on or move over the floor, so the floor contact term is off.
        /// </summary>
        public static Material ToonMaterial(Color c, string key = null, float rim = DefaultRim, float spec = 0f)
        {
            key ??= ColorUtility.ToHtmlStringRGB(c) + rim.ToString("F2") + spec.ToString("F2");
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(ToonShader) { name = "Toon " + key, enableInstancing = true };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_RimAmount", rim);
            m.SetFloat("_RimT", DefaultRimT);
            m.SetFloat("_SpecAmount", spec);
            m.SetFloat("_ContactAO", 0f);
            cache[key] = m;
            return m;
        }

        /// <summary>Character print (web CHAR_TOON): gentle halftone, no hatching, matte, no floor contact.</summary>
        public static Material Character(Material m)
        {
            m.SetFloat("_Halftone", 0.4f); m.SetFloat("_Hatch", 0f); m.SetFloat("_SpecAmount", 0f); m.SetFloat("_ContactAO", 0f);
            return m;
        }

        // ---------------------------------------------------------------- global switches (ToonLit, ToonGrass)
        static readonly int PatternsOffId = Shader.PropertyToID("_ComicPatternsOff"), FillOffId = Shader.PropertyToID("_ComicFillOff"),
            ContactOffId = Shader.PropertyToID("_ComicContactOff"), PatternScaleId = Shader.PropertyToID("_ComicPatternScale"),
            FloorYId = Shader.PropertyToID("_ComicFloorY");
        /// <summary>Halftone dots and cross-hatching printed on surfaces (on by default).</summary>
        public static void SetPrintPatterns(bool on) => Shader.SetGlobalFloat(PatternsOffId, on ? 0f : 1f);
        /// <summary>Cell size multiplier for the dots and hatching (1 = 8 mm dots, 6 mm hatching).</summary>
        public static void SetPatternScale(float scale) => Shader.SetGlobalFloat(PatternScaleId, Mathf.Max(0.05f, scale));
        /// <summary>Hemisphere fill from the scene ambient (on by default).</summary>
        public static void SetFill(bool on) => Shader.SetGlobalFloat(FillOffId, on ? 0f : 1f);
        /// <summary>Contact darkening towards the floor (on by default); floorY = floor height in metres.</summary>
        public static void SetContact(bool on, float floorY = 0f) { Shader.SetGlobalFloat(ContactOffId, on ? 0f : 1f); Shader.SetGlobalFloat(FloorYId, floorY); }

        public static string KindFor(string meshName)
        {
            foreach (var (re, kind) in Rules) if (re.IsMatch(meshName)) return kind;
            return "shell";
        }

        public static Color ColorFor(string kind, Skin s) => kind switch
        {
            "shell" or "shellAlt" => s.shell,
            "orange" or "beak" or "foot" => s.accent,
            "yellow" => s.accent2,
            "soft" => s.soft,
            "warmGrey" => s.secondary,
            _ => Fixed.TryGetValue(kind, out var c) ? c : s.shell,
        };

        /// <summary>Recolour a duck rig with toon materials for a skin; hide small hardware and internals.</summary>
        /// <summary>Customised duck: a base colourway, then optional colourways per part (head, body, legs, feet).</summary>
        public static readonly (string part, Regex re)[] PaintParts =
        {
            (PartIds.Head, new Regex("^(shell_head|shell_beak)", RegexOptions.IgnoreCase)),
            (PartIds.Body, new Regex("^shell_body", RegexOptions.IgnoreCase)),
            (PartIds.Legs, new Regex("^leg_", RegexOptions.IgnoreCase)),
            (PartIds.Feet, new Regex("^foot_", RegexOptions.IgnoreCase)),
        };

        public static void ApplyCustom(Transform rig, string baseSkin, IDictionary<string, string> parts)
        {
            ApplyToDuck(rig, baseSkin);
            if (parts == null) return;
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || IsAccessory(r)) continue;
                string n = MeshName(r);
                foreach (var (part, re) in PaintParts)
                {
                    if (!re.IsMatch(n) || !parts.TryGetValue(part, out var sid) || string.IsNullOrEmpty(sid) || !Skins.TryGetValue(sid, out var skin)) continue;
                    string kind = KindFor(n);
                    var mat = Character(ToonMaterial(ColorFor(kind, skin), sid + ":" + kind, kind == "shell" ? 0.35f : 0f));
                    var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = mat; r.sharedMaterials = mats;
                }
            }
        }

        public static void ApplyToDuck(Transform rig, string skinId = SkinIds.Cream)
        {
            var skin = Skins.TryGetValue(skinId, out var sk) ? sk : Skins[SkinIds.Cream];
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                if (IsAccessory(r)) continue;
                string n = MeshName(r);
                if (Hide.IsMatch(n) || Internal.IsMatch(n)) { r.enabled = false; continue; }
                string kind = KindFor(n);
                var mat = Character(ToonMaterial(ColorFor(kind, skin), skinId + ":" + kind, kind == "shell" ? 0.35f : 0f));
                if (kind == "eye" || kind == "highlight") mat.SetFloat("_Unlit", 1f);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = kind == "highlight" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        // glTFast puts a mesh on the node's GameObject, or on "primitive" children when a mesh has several
        /// <summary>Things worn on the duck (skates) keep their own colours.</summary>
        static bool IsAccessory(Renderer r) { for (var t = r.transform; t != null; t = t.parent) if (t.name.StartsWith("Skate ")) return true; return false; }

        public static string MeshName(Renderer r)
        {
            var t = r.transform;
            while (t != null && (t.name.StartsWith("primitive") || t.name.StartsWith("Primitive"))) t = t.parent;
            return t != null ? t.name : r.name;
        }
    }
}
