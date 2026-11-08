using UnityEngine;

namespace DJMaster
{
    // The board's color code. Every control family has one color, and Learn mode teaches it:
    //   BLUE = Deck A (left)   PINK = Deck B (right)   GREEN = PLAY   ORANGE = CUE and the hot cue pads
    //   YELLOW = headphones and FILTER (things only you hear / effects)   RED = TAP and the sampler pads (for the crowd)
    public static class BoardColors
    {
        public static readonly Color DeckA = new Color(0.1f, 0.85f, 1f);
        public static readonly Color DeckB = new Color(1f, 0.25f, 0.75f);
        public static readonly Color Play = new Color(0.1f, 1f, 0.35f);
        public static readonly Color Cue = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Fx = new Color(1f, 0.9f, 0.15f);
        public static readonly Color Crowd = new Color(1f, 0.15f, 0.1f);

        public static string Blue(string s) => Tag(s, DeckA);
        public static string Pink(string s) => Tag(s, DeckB);
        public static string Green(string s) => Tag(s, Play);
        public static string Orange(string s) => Tag(s, Cue);
        public static string Yellow(string s) => Tag(s, Fx);
        public static string Red(string s) => Tag(s, Crowd);
        public static string Deck(int i, string s) => i == 0 ? Blue(s) : Pink(s);

        public static string Tag(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";

        // The color name of a control, from its id ("A.low", "B.cue", "airhorn"...).
        public static string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id.EndsWith(".play")) return "GREEN";
            if (id.EndsWith(".cue") || id.Contains(".hotcue") || id.EndsWith(".loop")) return "ORANGE";
            if (id.EndsWith(".echo")) return "YELLOW";
            if (id.Contains(".sampler") || id == "airhorn") return "RED";
            if (id.EndsWith(".filter") || id.EndsWith(".headphones")) return "YELLOW";
            if (id == "tap") return "RED";
            if (id.StartsWith("A.")) return "BLUE";
            if (id.StartsWith("B.")) return "PINK";
            return "";
        }
    }
}
