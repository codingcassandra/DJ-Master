using System.Text;
using System.Text.RegularExpressions;

namespace DJMaster
{
    public static class TextUtil
    {
        static readonly Regex Tags = new Regex("<[^>]+>");
        // Word pairs that read wrong split over two lines: "0 s", "128 BPM", "9 or 13" (#55)
        static readonly Regex NumberUnit = new Regex(@"(\d) (s|ms|dB|BPM|bars?|beats?|pts|days?|%)(?=[\s.,:;!)<]|$)");
        static readonly Regex NumberOrNumber = new Regex(@"(\d) (or|to|and|of) (\d)");
        public const char Glue = '\u00A0'; // no-break space: Wrap never breaks there, TextMeshPro draws it as a space

        // Joins the pairs above with no-break spaces so Wrap keeps them on one line.
        public static string KeepTogether(string s)
        {
            s = NumberUnit.Replace(s, "$1" + Glue + "$2");
            return NumberOrNumber.Replace(s, m => m.Groups[1].Value + Glue + m.Groups[2].Value + Glue + m.Groups[3].Value);
        }

        // Length as it appears on screen: rich-text color tags take no space.
        public static int VisibleLength(string s) => Tags.Replace(s, "").Length;

        // TextMesh has no word wrap, so break lines by character count. Existing line breaks are kept.
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = KeepTogether(text);
            var sb = new StringBuilder();
            var paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) sb.Append('\n');
                int line = 0;
                foreach (var word in paragraphs[p].Split(' '))
                {
                    int len = VisibleLength(word);
                    if (line > 0 && line + 1 + len > maxChars) { sb.Append('\n'); line = 0; }
                    else if (line > 0) { sb.Append(' '); line++; }
                    sb.Append(word);
                    line += len;
                }
            }
            return sb.ToString();
        }

        public static string Clock(float seconds)
        {
            if (seconds < 0) seconds = 0;
            int s = (int)seconds;
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
