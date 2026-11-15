using System;
using UnityEngine;

namespace DJMaster
{
    // Four ways to play, from never touched a deck to already DJing. Values are saved, so only append.
    public enum SkillLevel { Beginner, Intermediate, Advanced, Solo }

    // The level you picked on the start menu: saved between sessions and shown on the panel while you play.
    // How much help and how strict the grading is per level lives with the rules (#15); this is the picker.
    public static class Skill
    {
        const string PrefKey = "skillLevel";
        static SkillLevel? current;

        public static SkillLevel Current
        {
            get => current ??= Parse(PlayerPrefs.GetInt(PrefKey, (int)SkillLevel.Beginner));
            set
            {
                bool changed = current != value;
                current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                if (changed) Changed?.Invoke();
            }
        }

        public static event Action Changed;

        public static SkillLevel Parse(int saved) => Enum.IsDefined(typeof(SkillLevel), saved) ? (SkillLevel)saved : SkillLevel.Beginner;

        public static string Name(SkillLevel l) => l switch
        {
            SkillLevel.Intermediate => "INTERMEDIATE",
            SkillLevel.Advanced => "ADVANCED",
            SkillLevel.Solo => "SOLO",
            _ => "BEGINNER",
        };

        public static string Describe(SkillLevel l) => l switch
        {
            SkillLevel.Intermediate => "Build a set and play it live, with the checklist.",
            SkillLevel.Advanced => "Play a set live: no checklist, no SYNC, no phrase countdown.",
            SkillLevel.Solo => "Just DJ. No rules, no score.",
            _ => "Never touched a deck? The lessons, with all the help on.",
        };

        // The mode each level opens.
        public static GameMode ModeFor(SkillLevel l) => l switch
        {
            SkillLevel.Intermediate or SkillLevel.Advanced => GameMode.Build,
            SkillLevel.Solo => GameMode.Freestyle,
            _ => GameMode.Learn,
        };

        // The deck screens count down to the next phrase for everyone but ADVANCED players, who count it themselves.
        public static bool PhraseCountdown => Current != SkillLevel.Advanced;

        // For tests: forget the saved level.
        public static void Reset()
        {
            current = null;
            PlayerPrefs.DeleteKey(PrefKey);
        }
    }
}
