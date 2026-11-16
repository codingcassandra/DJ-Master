namespace DJMaster
{
    // The level screen's side of the skill levels: names and what the screens show.
    // The level itself (saved, default INTERMEDIATE) and its rules live in SkillLevels (#15).
    public static class Skill
    {
        public static SkillLevel Current
        {
            get => SkillLevels.Current;
            set => SkillLevels.Set(value);
        }

        public static string Name(SkillLevel l) => l switch
        {
            SkillLevel.Beginner => "BEGINNER",
            SkillLevel.Advanced => "ADVANCED",
            SkillLevel.Solo => "SOLO",
            _ => "INTERMEDIATE",
        };

        public static string Describe(SkillLevel l) => l switch
        {
            SkillLevel.Beginner => "Never touched a deck? All the help on: easy timing, quick hints.",
            SkillLevel.Advanced => "No checklist, no SYNC, no phrase countdown.",
            SkillLevel.Solo => "Just DJ. No rules, no score.",
            _ => "SYNC, the checklist and the phrase countdown.",
        };

        // The deck screens count down to the next phrase for everyone but ADVANCED players, who count it themselves.
        public static bool PhraseCountdown => Current != SkillLevel.Advanced;

        // For tests: forget the saved level (back to the default, INTERMEDIATE).
        public static void Reset()
        {
            UnityEngine.PlayerPrefs.DeleteKey(SkillLevels.PrefKey);
            SkillLevels.Reload();
        }
    }
}
