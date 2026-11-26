namespace MicroduckVR.Comic
{
    /// <summary>
    /// Material property names of the toon shader (and URP Lit) that the game sets when it builds a material.
    /// They are set once per material, so they stay names; properties set every frame keep a Shader.PropertyToID
    /// next to their user (ComicFX, ComicLook, GrassField, ComfortVignette, MirrorStation).
    /// </summary>
    public static class ShaderIds
    {
        /// <summary>Toon shader: 1 = flat colour, no light bands (bubbles, panels, signs, sparks).</summary>
        public const string Unlit = "_Unlit";
        /// <summary>Base colour and texture.</summary>
        public const string BaseColor = "_BaseColor", BaseMap = "_BaseMap";
        /// <summary>Toon shading: rim band, specular pop and contact darkening.</summary>
        public const string RimAmount = "_RimAmount", RimT = "_RimT", SpecAmount = "_SpecAmount", ContactAO = "_ContactAO";
        /// <summary>Toon shading: halftone dots and hatching.</summary>
        public const string Halftone = "_Halftone", Hatch = "_Hatch";
        /// <summary>URP alpha clipping.</summary>
        public const string AlphaClip = "_AlphaClip", Cutoff = "_Cutoff";
    }
}
