using System;
using UnityEngine;

namespace DJMaster
{
    // How much help you get. Values are saved, so only append.
    public enum SkillLevel { Beginner, Intermediate, Advanced, Solo }

    // Every number that changes with the level, in one place so we can tune them.
    // Intermediate is the game as it was before levels, so keep its numbers in step with that.
    public class SkillRules
    {
        public SkillLevel level;

        // The one "in sync" rule (SYNC lights, lessons, live checklist, crowd): BPM apart and kicks apart in beats
        public float syncBpm, syncBeats;

        // Lessons: how far off the kick a tap can be (seconds either way), and how long before the hint shows up
        public float tapWindow, hintAfter;

        // Live sets
        public bool syncAllowed;    // false: the SYNC button does nothing
        public bool checklist;      // false: the panel only says NEXT: song + style
        public bool scored;         // false: no points at all, the crowd still reacts
        public float syncPenalty;   // a transition where we pressed SYNC loses this share of its points
        public float clashPerfect, clashGood;  // seconds of trainwreck a PERFECT / GOOD transition can have
        public float onBarWindow;   // how close to a bar line the new song has to come in, in bars
        public float cutPerfect, cutGood;      // seconds of overlap a QUICK CUT can have
        public int phraseBonus, harmonicBonus; // extra points for coming in on a phrase / in a matching key

        // Crowd: how fast the energy falls when we mess up, and how fast it comes back
        public float crowdDrop, crowdRise;
    }

    public static class SkillLevels
    {
        public const string PrefKey = "skillLevel";

        // Today's game. Tempo 0.35 BPM is about 6 mm on the TEMPO fader, kicks 0.07 beat is 33 ms at 128 BPM.
        public static readonly SkillRules Intermediate = new()
        {
            level = SkillLevel.Intermediate,
            syncBpm = 0.35f, syncBeats = 0.07f,
            tapWindow = 0.12f, hintAfter = 25f,
            syncAllowed = true, checklist = true, scored = true,
            syncPenalty = 0f, // the issue wants a small cut here (like 0.15), left at 0 so nothing changes yet
            clashPerfect = 1f, clashGood = 3f, onBarWindow = 0.15f,
            cutPerfect = 0.6f, cutGood = 2f,
            phraseBonus = 0, harmonicBonus = 0,
            crowdDrop = 0.12f, crowdRise = 0.05f,
        };

        // All the help: wide windows, hints quick, a crowd that doesn't give up on you.
        public static readonly SkillRules Beginner = new()
        {
            level = SkillLevel.Beginner,
            syncBpm = 0.5f, syncBeats = 0.09f, // 42 ms at 128 BPM, still one kick
            tapWindow = 0.16f, hintAfter = 8f,
            syncAllowed = true, checklist = true, scored = true,
            syncPenalty = 0f,
            clashPerfect = 2f, clashGood = 5f, onBarWindow = 0.25f,
            cutPerfect = 1f, cutGood = 3f,
            phraseBonus = 0, harmonicBonus = 0,
            crowdDrop = 0.06f, crowdRise = 0.07f,
        };

        // No SYNC, no checklist, tight windows, but points for mixing in key and on the phrase.
        public static readonly SkillRules Advanced = new()
        {
            level = SkillLevel.Advanced,
            syncBpm = 0.25f, syncBeats = 0.05f,
            tapWindow = 0.08f, hintAfter = 45f,
            syncAllowed = false, checklist = false, scored = true,
            syncPenalty = 0f,
            clashPerfect = 0.5f, clashGood = 2f, onBarWindow = 0.08f,
            cutPerfect = 0.4f, cutGood = 1.2f,
            phraseBonus = 250, harmonicBonus = 250,
            crowdDrop = 0.18f, crowdRise = 0.05f,
        };

        // Just DJ: no score, no checklist. Same sync feel as Intermediate.
        public static readonly SkillRules Solo = new()
        {
            level = SkillLevel.Solo,
            syncBpm = 0.35f, syncBeats = 0.07f,
            tapWindow = 0.12f, hintAfter = 25f,
            syncAllowed = true, checklist = false, scored = false,
            syncPenalty = 0f,
            clashPerfect = 1f, clashGood = 3f, onBarWindow = 0.15f,
            cutPerfect = 0.6f, cutGood = 2f,
            phraseBonus = 0, harmonicBonus = 0,
            crowdDrop = 0.12f, crowdRise = 0.05f,
        };

        public static SkillRules For(SkillLevel level) => level switch
        {
            SkillLevel.Beginner => Beginner,
            SkillLevel.Advanced => Advanced,
            SkillLevel.Solo => Solo,
            _ => Intermediate,
        };

        // The rules for the level we're playing at right now
        public static SkillRules Rules => For(Current);

        // Fires after the level changes, so the panel can update
        public static event Action<SkillLevel> Changed;

        static int current = -1; // -1 until we read the save, so we don't hit PlayerPrefs every frame

        // Nothing saved yet means Intermediate, the game as it always was.
        public static SkillLevel Current
        {
            get
            {
                if (current < 0)
                {
                    int saved = PlayerPrefs.GetInt(PrefKey, (int)SkillLevel.Intermediate);
                    current = Enum.IsDefined(typeof(SkillLevel), saved) ? saved : (int)SkillLevel.Intermediate;
                }
                return (SkillLevel)current;
            }
        }

        // What the level picker calls. Saves it and lets everyone know.
        public static void Set(SkillLevel level)
        {
            bool changed = level != Current;
            current = (int)level;
            PlayerPrefs.SetInt(PrefKey, current);
            PlayerPrefs.Save();
            if (changed) Changed?.Invoke(level);
        }

        // Read the save again next time, like after Reset Saved Progress wipes it
        public static void Reload() => current = -1;

        // Two songs sound good together if they're in the same key or one step round the circle of fifths.
        // All our synth tracks are minor keys, so that's the same root or 5 / 7 semitones apart.
        // Recordings don't know their key, so they never count. Can move over to MixMatch once that lands.
        public static bool KeysMatch(TrackInfo a, TrackInfo b)
        {
            if (a.IsRecording || b.IsRecording) return false;
            int step = ((b.root - a.root) % 12 + 12) % 12;
            return step == 0 || step == 5 || step == 7;
        }
    }
}
