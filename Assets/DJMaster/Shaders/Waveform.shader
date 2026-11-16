// The scrolling three-band waveform on a deck screen. The playhead sits in the middle and the song moves right to left
// under it. Reads the texture from Waveform.cs (r = lows, g = mids, b = highs per column) and draws beat, bar and phrase
// lines from the deck's beatgrid. Colors are set by DeckDisplay so color-blind mode can swap them.
Shader "DJMaster/Waveform"
{
    Properties
    {
        _WaveTex ("Waveform", 2D) = "black" {}
        _TexSize ("Texture Size (w, h)", Vector) = (1024, 1, 0, 0)
        _Columns ("Columns In Song", Float) = 0
        _Playhead ("Playhead Column", Float) = 0
        _Window ("Columns Across", Float) = 480
        _BeatCols ("Columns Per Beat", Float) = 40
        _FirstBeatCol ("First Downbeat Column", Float) = 0
        _LowColor ("Lows", Color) = (1, 0.45, 0.1, 1)
        _MidColor ("Mids", Color) = (0.2, 0.9, 0.35, 1)
        _HighColor ("Highs", Color) = (0.3, 0.6, 1, 1)
        _PhraseColor ("Phrase Line", Color) = (1, 0.2, 0.35, 1)
        _Back ("Background", Color) = (0.015, 0.02, 0.03, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _WaveTex;
            float4 _TexSize;
            float _Columns, _Playhead, _Window, _BeatCols, _FirstBeatCol;
            fixed4 _LowColor, _MidColor, _HighColor, _PhraseColor, _Back;

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

            float3 Column(float c)
            {
                if (_Columns < 1) return 0;
                c = c - _Columns * floor(c / _Columns); // songs loop, so the waveform wraps too
                float i = floor(c);
                float2 uv = float2((fmod(i, _TexSize.x) + 0.5) / _TexSize.x, (floor(i / _TexSize.x) + 0.5) / _TexSize.y);
                return tex2Dlod(_WaveTex, float4(uv, 0, 0)).rgb;
            }

            // 1 within half a line width of a grid line every `every` beats
            float Line(float beat, float every, float halfWidth)
            {
                float b = beat / every;
                return step(abs(frac(b + 0.5) - 0.5) * every * _BeatCols, halfWidth);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float col = _Playhead + (i.uv.x - 0.5) * _Window;
                float px = _Window * fwidth(i.uv.x); // columns per pixel
                float y = abs(i.uv.y * 2 - 1);       // 0 on the center line, 1 at the top and bottom edges

                // Two taps a pixel apart, so a peak between pixels still shows when it scrolls
                float3 w = max(Column(col), Column(col + px * 0.5)) * 0.94;
                // Lows widest, mids inside them, highs in the middle: each band drawn as far as it reaches, scaled so they nest
                fixed3 c = _Back.rgb;
                c = lerp(c, _LowColor.rgb, step(y, w.r));
                c = lerp(c, _MidColor.rgb, step(y, w.g * 0.7));
                c = lerp(c, _HighColor.rgb, step(y, w.b * 0.42));
                // Already played: dimmed, like the left half of a real deck screen
                c *= lerp(0.5, 1.0, step(0.5, i.uv.x));

                if (_Columns >= 1 && _BeatCols > 0)
                {
                    float beat = (col - _FirstBeatCol) / _BeatCols;
                    float h = max(px * 0.6, 0.35);
                    c = lerp(c, fixed3(0.55, 0.55, 0.6), Line(beat, 1, h) * 0.35);           // every beat, faint
                    c = lerp(c, fixed3(0.85, 0.85, 0.9), Line(beat, 4, h) * 0.6);            // every bar
                    float phrase = Line(beat, 16, h * 1.8);                                   // every phrase (4 bars)
                    c = lerp(c, _PhraseColor.rgb, phrase);
                    // A little flag at the top of each phrase line, so the next one is easy to spot coming
                    c = lerp(c, _PhraseColor.rgb, Line(beat, 16, px * 5) * step(0.8, i.uv.y));
                }

                // The playhead
                float d = abs(i.uv.x - 0.5) * _Window;
                c = lerp(c, fixed3(1, 1, 1), step(d, max(px * 0.9, 0.6)));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
