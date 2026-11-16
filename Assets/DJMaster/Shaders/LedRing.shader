// The LED ring around a knob, drawn on one flat quad: a 270 degree arc of segments with the gap at the bottom, the way
// club mixers do it. Segments between _From and _Value are lit, the rest glow faintly so you can see the ring is there.
// _From = 0 fills from the left like a volume knob, 0.5 fills out from the top like an EQ (center = no change).
// The color isn't called _Color on purpose: ColorSafeView recolors any material with a family color in _Color, and the
// Knob sets this one itself.
Shader "DJMaster/LedRing"
{
    Properties
    {
        _LedColor ("LED Color", Color) = (1,1,1,1)
        _Value ("Value", Range(0,1)) = 0.5
        _From ("Fill From", Range(0,1)) = 0
        _Segments ("Segments", Float) = 15
        _Inner ("Inner Radius (0..1)", Range(0,1)) = 0.78
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _LedColor;
            float _Value, _From, _Segments, _Inner;

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
                float2 c = i.uv * 2 - 1;
                float r = length(c);
                // Soft inner and outer edges, about one pixel wide at arm's length
                float aa = fwidth(r) * 1.2;
                float ring = smoothstep(_Inner - aa, _Inner + aa, r) * (1 - smoothstep(1 - aa * 2, 1, r));
                // 0 at the bottom left (7 o'clock), 1 at the bottom right (5 o'clock), clockwise like the knob's line
                float angle = degrees(atan2(c.x, c.y));
                float t = (angle + 135) / 270;
                float inArc = step(0, t) * step(t, 1);
                float s = t * _Segments;
                float f = frac(s);
                float gap = smoothstep(0.0, 0.12, f) * (1 - smoothstep(0.72, 0.84, f));
                float center = (floor(s) + 0.5) / _Segments;
                float lo = min(_From, _Value), hi = max(_From, _Value);
                // The segment the fill starts from is always lit, so a centered EQ still shows one light at 12 o'clock
                float lit = step(lo - 0.5 / _Segments, center) * step(center, hi + 0.5 / _Segments);
                fixed3 col = lerp(_LedColor.rgb * 0.13 + 0.015, _LedColor.rgb * 1.15, lit);
                return fixed4(col, ring * inArc * gap * _LedColor.a);
            }
            ENDCG
        }
    }
}
