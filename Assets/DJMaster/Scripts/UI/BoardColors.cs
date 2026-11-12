using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DJMaster
{
    // The board's color code. Every control family has one color, and Learn mode teaches it:
    //   BLUE = Deck A (left)   PINK = Deck B (right)   GREEN = PLAY   ORANGE = CUE and the hot cue pads
    //   YELLOW = headphones and FILTER (things only you hear / effects)   RED = TAP and the sampler pads (for the crowd)
    //
    // COLOR-BLIND mode (on the start menu) switches to the Okabe-Ito colors, which you can tell apart with every common
    // type of color blindness, and gives each color a shape on the board and in the lesson text, so nothing is color only:
    //   ◄ Deck A   ► Deck B   ● PLAY   ■ CUE   ♦ FX and PHONES   ♥ CROWD
    public static class BoardColors
    {
        public enum Family { None, DeckA, DeckB, Play, Cue, Fx, Crowd }

        public static readonly Color DeckA = new Color(0.1f, 0.85f, 1f);
        public static readonly Color DeckB = new Color(1f, 0.25f, 0.75f);
        public static readonly Color Play = new Color(0.1f, 1f, 0.35f);
        public static readonly Color Cue = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Fx = new Color(1f, 0.9f, 0.15f);
        public static readonly Color Crowd = new Color(1f, 0.15f, 0.1f);

        // One per Family, same order
        static readonly Color[] Standard = { Color.clear, DeckA, DeckB, Play, Cue, Fx, Crowd };
        static readonly Color[] Safe =
        {
            Color.clear,
            new Color32(0x56, 0xB4, 0xE9, 0xFF), // sky blue
            new Color32(0xCC, 0x79, 0xA7, 0xFF), // reddish purple
            new Color32(0x00, 0x9E, 0x73, 0xFF), // bluish green
            new Color32(0xE6, 0x9F, 0x00, 0xFF), // orange
            new Color32(0xF0, 0xE4, 0x42, 0xFF), // yellow
            new Color32(0xD5, 0x5E, 0x00, 0xFF), // vermillion
        };
        static readonly string[] Shapes = { "", "◄", "►", "●", "■", "♦", "♥" };
        static readonly string[] StandardHex = Array.ConvertAll(Standard, c => ColorUtility.ToHtmlStringRGB(c));
        static readonly Regex Tagged = new Regex("<color=#([0-9A-Fa-f]{6})>(.*?)</color>");

        const string PrefKey = "colorSafe";

        // True when color-blind mode is on
        public static bool ColorSafe { get; private set; }
        public static event Action Changed;

        public static void LoadSettings() => SetColorSafe(PlayerPrefs.GetInt(PrefKey, 0) == 1, save: false);

        public static void SetColorSafe(bool on, bool save = true)
        {
            bool changed = on != ColorSafe;
            ColorSafe = on;
            if (save) { PlayerPrefs.SetInt(PrefKey, on ? 1 : 0); PlayerPrefs.Save(); }
            if (changed) Changed?.Invoke();
        }

        // The color we draw a family in right now
        public static Color Of(Family f) => (ColorSafe ? Safe : Standard)[(int)f];

        // The shape for a family, like "●" for PLAY
        public static string Shape(Family f) => Shapes[(int)f];

        // Text made with the helpers below, the way it shows on screen. Normally it stays the same, in color-blind
        // mode it gets the safe colors and each color's shape after the words, like "PLAY ●"
        public static string Display(string text)
        {
            if (!ColorSafe || string.IsNullOrEmpty(text)) return text;
            return Tagged.Replace(text, m =>
            {
                int f = Array.FindIndex(StandardHex, 1, h => string.Equals(h, m.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                if (f < 1) return m.Value;
                return $"<color=#{ColorUtility.ToHtmlStringRGB(Safe[f])}>{m.Groups[2].Value} {Shapes[f]}</color>";
            });
        }

        // Which family a color from the scene belongs to, counting the dimmer versions the board uses
        // (button rings at 70%, knob skirts at 60%). brightness is how dim it is, 1 for the full color
        public static Family FamilyOf(Color c, out float brightness)
        {
            brightness = 0f;
            for (int i = 1; i < Standard.Length; i++)
            {
                var s = Standard[i];
                float k = (c.r * s.r + c.g * s.g + c.b * s.b) / (s.r * s.r + s.g * s.g + s.b * s.b);
                if (k < 0.2f || k > 1.05f) continue;
                if (Mathf.Abs(c.r - k * s.r) + Mathf.Abs(c.g - k * s.g) + Mathf.Abs(c.b - k * s.b) < 0.03f)
                {
                    brightness = k;
                    return (Family)i;
                }
            }
            return Family.None;
        }

        public static string Blue(string s) => Tag(s, DeckA);
        public static string Pink(string s) => Tag(s, DeckB);
        public static string Green(string s) => Tag(s, Play);
        public static string Orange(string s) => Tag(s, Cue);
        public static string Yellow(string s) => Tag(s, Fx);
        public static string Red(string s) => Tag(s, Crowd);
        public static string Deck(int i, string s) => i == 0 ? Blue(s) : Pink(s);

        public static string Tag(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        // The color family of a control, from its id ("A.low", "B.cue", "airhorn"...)
        public static Family FamilyOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return Family.None;
            if (id.EndsWith(".play")) return Family.Play;
            if (id.EndsWith(".cue") || id.Contains(".hotcue") || id.EndsWith(".loop")) return Family.Cue;
            if (id.EndsWith(".echo")) return Family.Fx;
            if (id.Contains(".sampler") || id == "airhorn") return Family.Crowd;
            if (id.EndsWith(".filter") || id.EndsWith(".headphones")) return Family.Fx;
            if (id == "tap") return Family.Crowd;
            if (id.StartsWith("A.")) return Family.DeckA;
            if (id.StartsWith("B.")) return Family.DeckB;
            return Family.None;
        }

        // The color name of a control, from its id ("A.low", "B.cue", "airhorn"...).
        public static string NameOf(string id) => FamilyOf(id) switch
        {
            Family.DeckA => "BLUE",
            Family.DeckB => "PINK",
            Family.Play => "GREEN",
            Family.Cue => "ORANGE",
            Family.Fx => "YELLOW",
            Family.Crowd => "RED",
            _ => "",
        };

        // The color name drawn in its color (so Display can add the shape), or "" if the control isn't color coded
        public static string TaggedName(string id)
        {
            var f = FamilyOf(id);
            return f == Family.None ? "" : Tag(NameOf(id), Standard[(int)f]);
        }
    }
}
