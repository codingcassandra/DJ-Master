using System;
using UnityEngine;

namespace DJMaster
{
    // The by-ear practice drill (#21): Deck A plays to the room, Deck B gets a song at a random tempo, out of step, in
    // your PHONES. Match it with the TEMPO fader and the jog wheel, listening only, then press DONE to see how close you got.
    public class EarDrill : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;

        public bool Running { get; private set; }
        public bool HasResult { get; private set; }
        public int Rounds { get; private set; }
        public float BpmOff { get; private set; }
        public float MsOff { get; private set; }
        public ByEar.Grade LastGrade { get; private set; }
        public int Perfects { get; private set; }
        public float TargetOffsetPercent { get; private set; } // how far off we set Deck B, for the tests

        // Neon Tides on A, Low Orbit on B: both 128 BPM, so the fader can always get there
        public const int TrackA = 0, TrackB = 5;

        System.Random rng = new();
        bool aligned;

        Deck A => board.A.deck;
        Deck B => board.B.deck;

        public void Seed(int seed) => rng = new System.Random(seed);

        public void Begin()
        {
            Running = true;
            Rounds = 0; Perfects = 0;
            board.ResetControls();
            if (A.TrackIndex != TrackA || !A.IsLoaded) A.Load(TrackA);
            A.Play();
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            NewRound();
        }

        // A fresh tempo for Deck B: 2 to 5% off, faster or slower, and started off the beat
        public void NewRound()
        {
            HasResult = false;
            aligned = false;
            float pct = (2f + (float)rng.NextDouble() * 3f) * (rng.Next(2) == 0 ? -1 : 1);
            TargetOffsetPercent = pct;
            if (B.TrackIndex != TrackB) B.Load(TrackB);
            board.B.volume.SetValue(0f);
            board.B.tempo.SetValue(Mathf.Clamp01(DJMath.TempoFaderFor(A.Track.bpm * (1f + pct / 100f) / B.Track.bpm)));
            mixer.headphoneCue[1] = true;
        }

        public void Stop()
        {
            Running = false;
            HasResult = false;
        }

        void Update()
        {
            if (!Running) return;
            // Once B is ready: playing, a random part of a beat off, so you have to find the kicks
            if (!aligned && B.IsLoaded && A.IsPlaying)
            {
                aligned = true;
                B.Play();
                B.SeekBeat(Math.Floor(A.Beat) + 0.25 + rng.NextDouble() * 0.5);
            }
        }

        // DONE: how close you got right now
        public void Finish()
        {
            if (!Running || !A.IsPlaying || !B.IsPlaying) return;
            BpmOff = B.BPM - A.BPM;
            float phase = DJMath.PhaseOffset(A.Beat, B.Beat);
            MsOff = ByEar.MsOff(phase, A.BPM);
            LastGrade = ByEar.Score(BpmOff, MsOff);
            Rounds++;
            if (LastGrade == ByEar.Grade.Perfect) Perfects++;
            HasResult = true;
            LastPhase = phase;
            if (LastGrade <= ByEar.Grade.Good) { Sfx.Success(); if (LastGrade == ByEar.Grade.Perfect) Sfx.Cheer(0.6f); }
            else Sfx.Fail();
        }

        public float LastPhase { get; private set; }
    }
}
