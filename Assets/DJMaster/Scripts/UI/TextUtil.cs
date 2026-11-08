using System.Text;
using System.Text.RegularExpressions;

namespace DJMaster
{
    public static class TextUtil
    {
        static readonly Regex Tags = new Regex("<[^>]+>");

        // Length as it appears on screen: rich-text color tags take no space.
        public static int VisibleLength(string s) => Tags.Replace(s, "").Length;

        // TextMesh has no word wrap, so break lines by character count. Existing line breaks are kept.
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
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
