namespace DJMaster
{
    // The phrase countdown on the deck screens. Dance music changes every 4 bars (a phrase starts on bar 1, 5, 9
    // and 13), and the next song should come in on one of those lines. Beginners lose count, so the screen counts
    // for them, flashes on the 1, and warns before the big moments: the breakdown and the drop. Pure, so it's tested.
    public static class PhraseCue
    {
        public const int PhraseBars = 4;
        public const int HeadsUpBars = 2; // how early the drop / breakdown warning starts

        public enum Kind { Count, LastBar, NewPhrase, HeadsUp }

        // Bars until the next phrase line: 4 right after one starts, 1 in its last bar.
        public static int BarsToNextPhrase(int bar) => PhraseBars - ((bar % PhraseBars) + PhraseBars) % PhraseBars;

        // What the countdown says (18 characters at most, to fit beside the beat lights) at this bar and beat (both 0-based, bar 0..15). sections: the song has our
        // drop / breakdown layout (the synthesized tracks), so the warnings can be given.
        public static string Text(int bar, int beatInBar, bool sections, out Kind kind)
        {
            int beatsLeft = TrackSynth.BeatsPerBar - beatInBar;
            if (bar % PhraseBars == 0 && beatInBar == 0)
            {
                kind = Kind.NewPhrase;
                return "NEW PHRASE!";
            }
            if (sections)
            {
                // The next section that's a big moment, within the warning window
                for (int ahead = 1; ahead <= HeadsUpBars; ahead++)
                {
                    int next = (bar + ahead) % TrackSynth.Bars;
                    var now = TrackSynth.SectionOfBar((next + TrackSynth.Bars - 1) % TrackSynth.Bars);
                    var then = TrackSynth.SectionOfBar(next);
                    if (then == now || (then != TrackSynth.Section.Drop && then != TrackSynth.Section.Breakdown)) continue;
                    kind = Kind.HeadsUp;
                    string name = then == TrackSynth.Section.Drop ? "DROP" : "BREAKDOWN";
                    return ahead == 1 ? $"{name}: {beatsLeft} BEAT{(beatsLeft == 1 ? "" : "S")}" : $"{name}: {ahead} BARS";
                }
            }
            int bars = BarsToNextPhrase(bar);
            if (bars == 1)
            {
                kind = Kind.LastBar;
                return $"PHRASE IN {beatsLeft} BEAT{(beatsLeft == 1 ? "" : "S")}";
            }
            kind = Kind.Count;
            return $"PHRASE IN {bars} BARS";
        }
    }
}
