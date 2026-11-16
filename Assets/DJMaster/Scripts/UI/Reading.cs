using System.Text.RegularExpressions;

namespace DJMaster
{
    // How long a prompt has to stay up so you can read it (#45): about 1 second per 4 words, and never less than 3 seconds.
    public static class Reading
    {
        public const float WordsPerSecond = 4f, MinSeconds = 3f;

        // The PlayMode tests turn this off so they don't wait out every prompt; ReadingTimeTest turns it on.
        public static bool Enabled = true;

        static readonly Regex Tags = new("<[^>]*>");

        public static int Words(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return Tags.Replace(text, " ").Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static float Seconds(string text) => System.Math.Max(MinSeconds, Words(text) / WordsPerSecond);
    }
}
