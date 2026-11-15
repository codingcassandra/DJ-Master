using System;
using UnityEngine;

namespace DJMaster
{
    // What the lights and the crowd follow: the beat of whatever the room is hearing, and how hyped they are.
    public static class BeatBus
    {
        public static double Beat;          // beat position of the dominant deck
        public static bool Playing;
        public static float Pulse;          // 1 on each beat, decays quickly
        public static float BarPulse;       // 1 on each bar's downbeat
        public static float Energy = 0.4f;  // crowd energy 0..1
        public static float Loudness;       // 0..1, what the crowd hears
        public static event Action Drop;    // big moments: bass drops, finished transitions

        public static void FireDrop() => Drop?.Invoke();
    }

    // Listens to the mix like a crowd would and scores it. Rewards: in-sync blends, finished transitions,
    // well-timed bass drops. Penalties: train wrecks (two tracks out of sync), bass clashes, silence,
    // and playing one track forever.
    public class MixJudge : MonoBehaviour
    {
        public Mixer mixer;

        public float Score { get; private set; }
        public string Warning { get; private set; } = "";
        public string Shoutout { get; private set; } = "";
        public int Transitions { get; private set; }
        public bool Clashing { get; private set; }
        public bool InSync => inSync;
        public float PhaseOffset { get; private set; }
        public float BpmDifference { get; private set; }

        public event Action<string, int> Bonus; // message, points

        // One "in sync" rule for everything: the SYNC lights, the sync meter, the lessons and the live checklist.
        // Tempo within 0.35 BPM is about 6 mm either way on the TEMPO fader, so a finger can hit it, and the beats
        // only drift about 3 ms a second at 128 BPM, so one good nudge lasts most of a phrase.
        // Kicks within 0.07 beat is 33 ms at 128 BPM (35 ms at 120, 32 ms at 130). That still sounds like one
        // kick, and a short spin on the jog can land in it.
        // Those are Intermediate's numbers, the other levels loosen or tighten them (see SkillLevels).
        public static float SyncBpm => SkillLevels.Rules.syncBpm;
        public static float SyncBeats => SkillLevels.Rules.syncBeats;
        public static bool BeatsLocked(float bpmDifference, float phaseOffset) =>
            Mathf.Abs(bpmDifference) < SyncBpm && Mathf.Abs(phaseOffset) < SyncBeats;

        int lastDominant = -1;
        int lastBeat = -1, lastBar = -1;
        float silence, trackTime, shoutoutTimer;
        float[] lowWas = { 0.5f, 0.5f };
        float[] lowCutAt = { -10f, -10f };
        bool inSync;
        float echoTime, quietTime;

        // Gain staging: a channel stuck in the red distorts, and a song far quieter than it should be sounds weak.
        public const float ClipSeconds = 1f, QuietSeconds = 3f;
        public const float QuietLevel = 0.25f; // -12 dB: the peaks are nowhere near the yellow
        public int TooLoudDeck { get; private set; } = -1;
        public int TooQuietDeck { get; private set; } = -1;
        int awardedFrom = -1, awardedTo = -1; // the two tracks of the last transition that scored

        public void ResetScore()
        {
            Score = 0; Transitions = 0; trackTime = 0; silence = 0;
            awardedFrom = awardedTo = -1;
            BeatBus.Energy = 0.45f;
            lastDominant = mixer.DominantDeck;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var a = mixer.decks[0];
            var b = mixer.decks[1];
            float gA = mixer.CrowdGain(0), gB = mixer.CrowdGain(1);
            int dom = mixer.DominantDeck;

            // Sync between the decks
            BpmDifference = b.BPM - a.BPM;
            PhaseOffset = a.IsLoaded && b.IsLoaded ? DJMath.PhaseOffset(a.Beat, b.Beat) : 0f;
            inSync = a.IsPlaying && b.IsPlaying && BeatsLocked(BpmDifference, PhaseOffset);
            bool bothAudible = gA > 0.2f && gB > 0.2f;
            Clashing = bothAudible && !inSync;
            bool bassClash = bothAudible && a.Strip.low > 0.35f && b.Strip.low > 0.35f;

            // Beat clock for the visuals
            BeatBus.Playing = dom >= 0;
            BeatBus.Loudness = Mathf.Clamp01(gA + gB);
            if (dom >= 0)
            {
                var d = mixer.decks[dom];
                BeatBus.Beat = d.Beat;
                int beat = (int)d.Beat;
                if (beat != lastBeat) { BeatBus.Pulse = 1f; lastBeat = beat; }
                int bar = d.Bar;
                if (bar != lastBar) { BeatBus.BarPulse = 1f; lastBar = bar; }
            }
            BeatBus.Pulse = Mathf.MoveTowards(BeatBus.Pulse, 0, dt * 4f);
            BeatBus.BarPulse = Mathf.MoveTowards(BeatBus.BarPulse, 0, dt * 1.5f);

            // Crowd energy drifts toward a target set by what's happening.
            silence = dom < 0 ? silence + dt : 0;
            if (dom != lastDominant)
            {
                if (dom >= 0 && lastDominant >= 0 && mixer.decks[lastDominant].IsPlaying && inSync)
                {
                    // Each pair of songs only scores once, so wiggling the crossfader back and forth doesn't farm points
                    int from = mixer.decks[lastDominant].TrackIndex, to = mixer.decks[dom].TrackIndex;
                    bool samePair = (from == awardedFrom && to == awardedTo) || (from == awardedTo && to == awardedFrom);
                    if (!samePair)
                    {
                        awardedFrom = from; awardedTo = to;
                        Transitions++;
                        Award("SMOOTH TRANSITION!", 500);
                        BeatBus.FireDrop();
                    }
                }
                if (dom >= 0) trackTime = 0;
                lastDominant = dom;
            }
            trackTime += dom >= 0 ? dt : 0;

            // Effects: less is more.
            echoTime = a.Strip.echo || b.Strip.echo ? echoTime + dt : 0;
            bool fxOveruse = echoTime > 20f;

            // Gain: what the crowd hears in the red, or the song on the floor way too quiet.
            TooLoudDeck = -1;
            for (int i = 0; i < 2; i++)
                if (mixer.CrowdGain(i) > 0.2f && mixer.decks[i].Strip.ClipTime > ClipSeconds) TooLoudDeck = i;
            bool quiet = dom >= 0 && mixer.CrowdGain(dom) > 0.3f && mixer.decks[dom].Strip.PeakHold < QuietLevel;
            quietTime = quiet ? quietTime + dt : 0;
            TooQuietDeck = quietTime > QuietSeconds ? dom : -1;

            float freshness = Mathf.Clamp01(1.1f - Mathf.Max(0, trackTime - 60f) / 90f); // crowd tires of one track after a minute
            float target = dom < 0 ? 0.05f : 0.55f + 0.35f * freshness;
            if (bothAudible && inSync) target = Mathf.Max(target, 0.9f);
            if (Clashing) target = 0.15f;
            else if (bassClash) target = Mathf.Min(target, 0.45f);
            if (fxOveruse) target = Mathf.Min(target, 0.5f);
            if (TooLoudDeck >= 0) target = Mathf.Min(target, 0.4f);
            if (TooQuietDeck >= 0) target = Mathf.Min(target, 0.5f);
            var rules = SkillLevels.Rules;
            float rate = target < BeatBus.Energy ? rules.crowdDrop : rules.crowdRise;
            BeatBus.Energy = Mathf.MoveTowards(BeatBus.Energy, target, dt * rate);

            Warning = Clashing ? "TRAINWRECK! The beats are clashing. Match the tempo or pull a fader down."
                : bassClash ? "Muddy bass: cut the LOW on one deck."
                : silence > 1.5f ? "The dance floor is silent! Get a track playing."
                : TooLoudDeck >= 0 ? $"Deck {(TooLoudDeck == 0 ? "A" : "B")} is in the red and distorting! Turn its GAIN down."
                : TooQuietDeck >= 0 ? $"Deck {(TooQuietDeck == 0 ? "A" : "B")} is too quiet. Turn its GAIN up until the meter hits the yellow."
                : trackTime > 75f ? "The crowd wants something new. Mix in the other deck."
                : fxOveruse ? "Less is more: switch the ECHO off and let the music breathe."
                : "";

            // Bass drop: cutting a deck's low, then slamming it back on the downbeat of a bar.
            for (int i = 0; i < 2; i++)
            {
                var d = mixer.decks[i];
                float low = d.Strip.low;
                if (low < 0.12f && lowWas[i] >= 0.12f) lowCutAt[i] = Time.time;
                if (low > 0.45f && lowWas[i] <= 0.45f && mixer.CrowdGain(i) > 0.3f && Time.time - lowCutAt[i] > 0.6f
                    && Time.time - lowCutAt[i] < 30f && d.IsPlaying)
                {
                    bool onTheOne = DJMath.DistanceToBeat(d.Beat / TrackSynth.BeatsPerBar) < 0.12f; // within ~half a beat of a bar line
                    if (onTheOne) { Award("BASS DROP!", 300); BeatBus.FireDrop(); BeatBus.Energy = Mathf.Min(1, BeatBus.Energy + 0.15f); }
                    lowCutAt[i] = -10f;
                }
                lowWas[i] = low;
            }

            if (rules.scored) Score += BeatBus.Energy * BeatBus.Energy * dt * 20f * (Clashing ? 0 : 1);
            shoutoutTimer -= dt;
            if (shoutoutTimer <= 0) Shoutout = "";
        }

        public void Award(string message, int points)
        {
            // Solo has no score, but we still cheer the moment
            bool scored = SkillLevels.Rules.scored;
            if (scored) Score += points;
            Shoutout = scored ? $"{message}  +{points}" : message;
            shoutoutTimer = 2.5f;
            Sfx.Cheer(0.8f);
            Bonus?.Invoke(message, points);
        }
    }
}
