using System;

namespace DJMaster
{
    public enum KeyMatch { Unknown, Clash, Good, Perfect }
    public enum TempoMatch { Green, Yellow, Red }

    // Harmonic mixing hints: does the next song's key and tempo go with the one playing?
    // Keys use the Camelot wheel DJs use: 12 numbers like a clock, A = minor, B = major (A minor is 8A, C major is 8B).
    // Plain logic, no Unity, so it's easy to test
    public static class MixMatch
    {
        public const float BeatmixLimit = 4f; // past this a beatmix sounds off
        public const float FaderLimit = 8f;   // past this our TEMPO fader can't get there
        const float Slack = 0.001f;           // so 4.0000001% from float math still counts as 4%

        // 1 to 12. Walking up the circle of fifths moves one step clockwise
        public static int CamelotNumber(int pitchClass, bool major)
        {
            int fifths = (((pitchClass % 12) + 12) % 12) * 7 % 12; // C = 0, G = 1, D = 2 ...
            return (fifths + (major ? 7 : 4)) % 12 + 1;
        }

        public static string Camelot(int pitchClass, bool major) => CamelotNumber(pitchClass, major) + (major ? "B" : "A");

        // "" when we don't know the song's key
        public static string Camelot(TrackInfo t) => t.HasKey ? Camelot(t.PitchClass, t.major) : "";

        // Same key is perfect. One step around the wheel, or the relative major/minor (same number, other letter), is good
        public static KeyMatch Keys(int pitchA, bool majorA, int pitchB, bool majorB)
        {
            int a = CamelotNumber(pitchA, majorA), b = CamelotNumber(pitchB, majorB);
            if (majorA == majorB)
            {
                if (a == b) return KeyMatch.Perfect;
                int steps = Math.Abs(a - b);
                return steps == 1 || steps == 11 ? KeyMatch.Good : KeyMatch.Clash; // 11 is the 12 to 1 wrap
            }
            return a == b ? KeyMatch.Good : KeyMatch.Clash;
        }

        public static KeyMatch Keys(TrackInfo a, TrackInfo b)
        {
            if (!a.HasKey || !b.HasKey) return KeyMatch.Unknown;
            return Keys(a.PitchClass, a.major, b.PitchClass, b.major);
        }

        // Same math as SetList.TempoGapPercent: how far the new song's tempo is from the one playing
        public static float TempoGapPercent(float fromBpm, float toBpm) => Math.Abs(toBpm / fromBpm - 1f) * 100f;

        // Green: beatmix it. Yellow: do a fade, cut or echo out. Red: too far for the TEMPO fader
        public static TempoMatch Tempo(float fromBpm, float toBpm)
        {
            float gap = TempoGapPercent(fromBpm, toBpm);
            if (gap <= BeatmixLimit + Slack) return TempoMatch.Green;
            return gap <= FaderLimit + Slack ? TempoMatch.Yellow : TempoMatch.Red;
        }

        public static TempoMatch Tempo(TrackInfo from, TrackInfo to) => Tempo(from.bpm, to.bpm);
    }
}
