using UnityEngine;

namespace DJMaster
{
    // BY EAR (#21): the way pros learn to beatmatch, without looking at numbers. A toggle for ADVANCED players: the deck
    // screens hide their BPM, the SYNC strip and the SYNC buttons' lights go dark, and you match the TEMPO fader and the
    // jog wheel by listening in PHONES. Afterwards it tells you how close you got.
    public static class ByEar
    {
        public const string PrefKey = "byEar";

        // The player's choice, saved with their progress
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Players.Key(PrefKey), 0) == 1;
            set { PlayerPrefs.SetInt(Players.Key(PrefKey), value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // Only in ADVANCED: the other levels keep their numbers and lights
        public static bool Active => Enabled && Skill.Current == SkillLevel.Advanced;

        public enum Grade { Perfect, Good, Close, Off }

        // How far apart the kicks are, in milliseconds, from the phase offset in beats
        public static float MsOff(float phaseBeats, float bpm) => Mathf.Abs(phaseBeats) * 60000f / Mathf.Max(1f, bpm);

        // Perfect: the tempo is within a tenth of a BPM and the kicks within 20 ms, so they sound like one.
        // Good: you'd get away with it in a club. Close: the right idea, still drifting or flamming
        public static Grade Score(float bpmOff, float msOff)
        {
            bpmOff = Mathf.Abs(bpmOff); msOff = Mathf.Abs(msOff);
            if (bpmOff <= 0.1f && msOff <= 20f) return Grade.Perfect;
            if (bpmOff <= 0.3f && msOff <= 45f) return Grade.Good;
            if (bpmOff <= 1f && msOff <= 100f) return Grade.Close;
            return Grade.Off;
        }

        public static string GradeName(Grade g) => g switch
        {
            Grade.Perfect => "PERFECT",
            Grade.Good => "GOOD",
            Grade.Close => "CLOSE",
            _ => "OFF",
        };

        // "BPM off by 0.2, kicks 15 ms apart (B ahead)"
        public static string HowClose(float bpmDifference, float phaseBeats, float bpm)
        {
            float ms = MsOff(phaseBeats, bpm);
            string side = ms < 5f ? "" : phaseBeats > 0 ? " (B ahead)" : " (B behind)";
            return $"BPM off by {Mathf.Abs(bpmDifference):0.0}, kicks {ms:0} ms apart{side}";
        }

        // A tip for what to fix next, from what was further off
        public static string Tip(float bpmOff, float msOff)
        {
            if (Score(bpmOff, msOff) == Grade.Perfect) return "Locked by ear. That's the real skill.";
            if (Mathf.Abs(bpmOff) > 0.3f) return "Tempo first: if the kicks keep drifting apart, ride the TEMPO fader until they stop drifting.";
            return "Tempo's there. Now nudge the jog wheel until the two kicks sound like one.";
        }
    }
}
