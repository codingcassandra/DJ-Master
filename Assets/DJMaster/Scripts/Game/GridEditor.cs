using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DJMaster
{
    // The tap-the-one screen, for a song that doesn't start on beat 1. The tempo and the beats are found in the
    // background; you listen and tap the TAP pad on the first big beat of a bar ("the one"), which snaps to the
    // nearest beat. A click plays on every bar 1 so you can hear if it's right, and EARLIER / LATER nudge it.
    // SAVE keeps it for good. Hands only: the TAP pad and the panel buttons.
    public class GridEditor : MonoBehaviour
    {
        public DJBoard board;

        public bool Running { get; private set; }
        public bool Detecting => detect != null && !detect.IsCompleted;
        public int Track { get; private set; } = -1;
        public BeatGrid Grid { get; private set; }
        public int Taps { get; private set; }
        public string Feedback { get; private set; } = "";
        public const float NudgeSeconds = 0.01f;

        public event Action Done;

        int deckIndex;
        Task<BeatGrid> detect;
        BeatGrid heard; // what the detector found: taps snap to these beats, whatever the nudges did
        DeckControls Ch => board.channels[deckIndex];
        Deck Deck => Ch.deck;

        void Awake()
        {
            board.tap.Pressed += OnTap;
            foreach (var ch in board.channels) ch.deck.BeatTick += OnBeat;
        }

        // Opens the screen for a song: loads it on a deck, plays it to you and finds the beat if it isn't saved yet.
        public void Begin(int track, int deck = 0)
        {
            Track = track;
            deckIndex = deck;
            Taps = 0;
            Feedback = "";
            detect = null;
            board.ResetControls();
            Deck.Load(track);
            Ch.volume.SetValue(1f);
            board.crossfader.SetValue(deck == 0 ? 0f : 1f);
            Deck.Play();
            Running = true;
            heard = default;
            if (BeatGridStore.ApplySaved(track)) { Grid = TrackLibrary.GridOf(track); Feedback = "This song already has a beatgrid. Check it, then SAVE."; }
            else Grid = default;
        }

        public void Stop() => Running = false;

        void Update()
        {
            if (!Running) return;
            // Start finding the beat once the song's audio is ready
            if (Grid.bpm <= 0 && detect == null && Deck.IsLoaded) detect = BeatGridStore.DetectAsync(Deck.Source.clip);
            if (detect != null && detect.IsCompleted && Grid.bpm <= 0)
            {
                if (detect.IsFaulted) { Debug.LogException(detect.Exception); Grid = new BeatGrid { bpm = TrackLibrary.Tracks[Track].bpm }; }
                else Grid = detect.Result;
                heard = Grid;
                Apply();
                Feedback = Grid.confidence < 0.15f ? "The beat is hard to hear in this one. Tap carefully." : "";
            }
        }

        void OnTap()
        {
            if (!Running || Grid.bpm <= 0 || !Deck.IsPlaying) return;
            float heard = Deck.Source.time - LessonDirector.TapLag; // what you heard when you tapped
            var g = Grid;
            var beats = this.heard.bpm == g.bpm ? this.heard : new BeatGrid { bpm = g.bpm }; // after HALF / x2 there's nothing to snap to
            g.firstBeat = BeatDetector.DownbeatFromTap(heard, beats);
            Grid = g;
            Taps++;
            Apply();
            Feedback = "Bar 1 is there now. Listen for the click on every bar 1. Off? Tap again or nudge it.";
        }

        // EARLIER / LATER by 10 ms
        public void Nudge(int steps)
        {
            if (Grid.bpm <= 0) return;
            var g = Grid;
            g.firstBeat = BeatGrid.WrapToFirstBar(g.firstBeat + steps * NudgeSeconds, g.bpm);
            Grid = g;
            Apply();
            Feedback = $"Moved {(steps < 0 ? "earlier" : "later")} by {Mathf.Abs(steps) * NudgeSeconds * 1000:0} ms.";
        }

        // Tempo found at double or half speed? Fix it here.
        public void HalfDouble()
        {
            if (Grid.bpm <= 0) return;
            var g = Grid;
            g.bpm = g.bpm < 100f ? g.bpm * 2f : g.bpm / 2f;
            Grid = g;
            Apply();
            Feedback = $"Tempo is now {g.bpm:0.0} BPM.";
        }

        public void Save()
        {
            if (Grid.bpm <= 0) return;
            BeatGridStore.Save(TrackLibrary.Tracks[Track].GridId, TrackLibrary.GridOf(Track));
            Running = false;
            Sfx.Success();
            Done?.Invoke();
        }

        void Apply() => TrackLibrary.SetGrid(Track, Grid);

        // The metronome: a click on every bar 1 of the grid, so you can hear it line up with the music
        void OnBeat(Deck d, int beat)
        {
            if (!Running || d != Deck || Grid.bpm <= 0) return;
            if (((beat % 4) + 4) % 4 == 0) Sfx.Click();
        }

        public string Title => "SET THE BEATGRID";

        public string Body
        {
            get
            {
                var t = TrackLibrary.Tracks[Track];
                if (Grid.bpm <= 0) return $"{t.title}\n\nFinding the beat...";
                return $"{t.title}: {Grid.bpm:0.0} BPM\n\n" +
                       $"Listen for the first big beat of a bar, \"the one\", and tap the {BoardColors.Red("TAP")} pad right on it. " +
                       "A click plays on every bar 1 after that: if it's early or late, use EARLIER or LATER. SAVE when it lines up.";
            }
        }

        public string Status => Grid.bpm <= 0 ? "" : $"Bar {Deck.Bar + 1}   beat {Deck.BeatInBar + 1}   first beat at {Grid.firstBeat * 1000f:0} ms";
    }
}
