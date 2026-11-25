Shader "Hidden/Microduck/FilmAccum"
{
    // Film tool only: adds a weighted copy of a frame into an accumulation buffer (sub-frame motion blur,
    // and the 2x supersampled render is filtered down on the way).
    Properties { _MainTex ("", 2D) = "white" {} _Weight ("", Float) = 1 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Weight;
            float4 frag (v2f_img i) : SV_Target { return float4(tex2D(_MainTex, i.uv).rgb * _Weight, _Weight); }
            ENDCG
        }
    }
}
