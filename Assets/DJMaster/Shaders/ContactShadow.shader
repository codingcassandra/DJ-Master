// A soft blob under the board, drawn on one flat quad: a rounded rectangle that fades out at the edges.
// Premultiplied, and it writes alpha on purpose: in MY ROOM the real room shows through wherever the eye buffer is
// transparent, so a black color with some alpha darkens your real desk (the shadow), and a color with a little alpha
// adds light to it (the LED glow).
//   _Size: the rectangle's half size in UV (0..0.5), _Soft: how far the fade runs past it, _Alpha: how much it covers
Shader "DJMaster/ContactShadow"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        _Alpha ("Alpha", Range(0,1)) = 0.6
        _Size ("Half Size (uv)", Vector) = (0.3, 0.3, 0, 0)
        _Soft ("Softness (uv)", Float) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Alpha, _Soft;
            float4 _Size;

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
                // Distance outside a rounded rectangle, 0 inside it
                float2 d = abs(i.uv - 0.5) - _Size.xy;
                float outside = length(max(d, 0));
                float k = 1 - smoothstep(0, _Soft, outside);
                k *= k; // a softer tail, like light wrapping round
                float a = k * _Alpha;
                return fixed4(_Color.rgb * a, a);
            }
            ENDCG
        }
    }
}
