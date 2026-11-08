using System.Text.RegularExpressions;
using UnityEngine;

namespace DJMaster
{
    // The DJ lingo shown on the DJ VOCAB screen, and the clues the vocab quiz asks about.
    public static class DJVocab
    {
        public struct Word
        {
            public string term, meaning;
            public Word(string term, string meaning) { this.term = term; this.meaning = meaning; }
        }

        public static readonly Word[] Words = Build();

        public static int BestQuizScore
        {
            get => PlayerPrefs.GetInt("vocabQuizBest", 0);
            set { PlayerPrefs.SetInt("vocabQuizBest", value); PlayerPrefs.Save(); }
        }

        // The meaning with the word itself blanked out, so the quiz doesn't give the answer away.
        public static string Clue(Word w) =>
            Regex.Replace(w.meaning, $@"(?<![\w#=]){Regex.Escape(w.term)}(?!\w)", "_____", RegexOptions.IgnoreCase);

        static Word W(string term, string meaning) => new Word(term, meaning);

        static Word[] Build()
        {
            string deckA = BoardColors.Blue("Deck A"), deckB = BoardColors.Pink("Deck B");
            return new[]
            {
                W("BPM", "Beats per minute: how fast a song is. House music usually sits around 120 to 128 BPM."),
                W("BEAT", "One pulse of the music, the kick drum you nod your head to. Dance music counts them in 4s."),
                W("BAR", "A group of 4 beats. The platter on each deck turns once per bar."),
                W("PHRASE", "A musical sentence, usually 8 or 16 bars. Songs change at a new phrase, so start mixes there."),
                W("DECK", $"A player for one song. {deckA} is on the left, {deckB} is on the right."),
                W("JOG WHEEL", "The big round platter. Spin it to nudge a song ahead or behind, or to scrub when paused."),
                W("TEMPO", "The long slider beside each deck. It speeds a song up or slows it down, by up to 8%."),
                W("SYNC", "Matches a song's tempo to the other deck for you, so you can focus on the mix."),
                W("CUE", $"A starting point in a song. The {BoardColors.Orange("CUE")} button jumps back to it, ready to start on the beat."),
                W("HOT CUE", $"{BoardColors.Orange("Orange")} pads that jump straight to a spot in a song. Here: bar 1, 5, 9 or 13."),
                W("CHANNEL FADER", "Each deck's volume slider in the middle of the board. All the way up is full volume."),
                W("CROSSFADER", $"The sideways slider at the front. Left plays {deckA}, right plays {deckB}, middle plays both."),
                W("EQ", "The HI, MID and LOW knobs: treble, middle and bass. Turn one down to take that part out."),
                W("FILTER", $"One {BoardColors.Yellow("yellow")} knob that makes a song muffled one way and thin the other. Great for build-ups."),
                W("LOOP", "Repeats a few beats over and over, so a short part of a song lasts as long as you need."),
                W("ECHO", "Repeats the sound and lets it fade away. Use it on the last beat to leave a song smoothly."),
                W("PHONES", $"Headphone cue: hear a deck in {BoardColors.Yellow("your ears only")} to get the next song ready."),
                W("TAP", $"Hit the {BoardColors.Red("red")} TAP button on every kick and it works out the song's BPM."),
                W("BEATMATCH", "Lining two songs up so their beats land together: same tempo, kicks in step."),
                W("TRANSITION", "Moving from one song to the next without the music stopping. Also just called a mix."),
                W("BASS SWAP", "Turn one song's LOW down as the other's comes up, so two basslines never clash."),
                W("QUICK CUT", "Switch songs in an instant, right on beat 1 of a new phrase. No blending at all."),
                W("DROP", "The big moment a song builds up to, when the bass and the beat crash back in."),
                W("TRAIN WRECK", "When two beats clash and it sounds like a mess. Fix it fast: fade it out or cut!"),
            };
        }
    }
}
