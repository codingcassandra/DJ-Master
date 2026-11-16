// Dark glass for the menu panels and rows: see-through tinted fill, a soft sheen from the top, and a thin bright rim.
// The rim is a fixed number of pixels wide, so it stays crisp on any size of box. Works on Unity's cube (0..1 UVs per face).
// _Color comes from the material or a MaterialPropertyBlock (PokeButton tints its cap this way).
Shader "DJMaster/Glass"
{
    Properties
    {
        _Color ("Tint (alpha = how solid)", Color) = (0.3, 0.1, 0.5, 0.5)
        _Rim ("Rim color", Color) = (0.78, 0.6, 1, 1)
        _RimPixels ("Rim width (pixels)", Float) = 1.5
        _Sheen ("Sheen from the top", Range(0, 1)) = 0.25
        _RadiusPixels ("Corner radius (pixels)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _Rim;
            float _RimPixels, _Sheen, _RadiusPixels;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Distance to the nearest edge, in pixels, with the corners rounded off
                float2 d = min(i.uv, 1 - i.uv) / max(fwidth(i.uv), 1e-5);
                float R = _RadiusPixels;
                float2 c = max(R - d, 0);
                float edge = (c.x > 0 && c.y > 0) ? R - length(c) : min(d.x, d.y);
                float inside = saturate(edge + 0.5);
                float rim = saturate(_RimPixels + 0.5 - edge);
                // Lighter toward the top edge, like light from above catching the glass
                float sheen = _Sheen * smoothstep(0.35, 1, 1 - i.uv.y);
                fixed3 fill = _Color.rgb * (1 + sheen * 2) + sheen * 0.08;
                fixed4 col = fixed4(fill, saturate(_Color.a + sheen * 0.25));
                col = lerp(col, fixed4(_Rim.rgb, _Rim.a), rim * _Rim.a);
                col.a *= inside;
                return col;
            }
            ENDCG
        }
    }
}
