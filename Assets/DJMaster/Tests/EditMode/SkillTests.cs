using NUnit.Framework;

namespace DJMaster.Tests
{
    public class SkillTests
    {
        [TearDown] public void Cleanup() => Skill.Reset();

        [Test]
        public void EachLevelOpensTheRightMode()
        {
            Assert.AreEqual(GameMode.Learn, Skill.ModeFor(SkillLevel.Beginner));
            Assert.AreEqual(GameMode.Build, Skill.ModeFor(SkillLevel.Intermediate));
            Assert.AreEqual(GameMode.Build, Skill.ModeFor(SkillLevel.Advanced));
            Assert.AreEqual(GameMode.Freestyle, Skill.ModeFor(SkillLevel.Solo));
        }

        [Test]
        public void LevelIsSavedAndDefaultsToBeginner()
        {
            Skill.Reset();
            Assert.AreEqual(SkillLevel.Beginner, Skill.Current);
            Skill.Current = SkillLevel.Advanced;
            Skill.Reset(); // forget the cache... and the save
            Assert.AreEqual(SkillLevel.Beginner, Skill.Current);
            Skill.Current = SkillLevel.Solo;
            Assert.AreEqual(SkillLevel.Solo, Skill.Parse(UnityEngine.PlayerPrefs.GetInt("skillLevel", -1)), "written to PlayerPrefs");
            Assert.AreEqual(SkillLevel.Beginner, Skill.Parse(99), "an unknown saved value falls back to beginner");
        }

        [Test]
        public void OnlyAdvancedHidesThePhraseCountdown()
        {
            foreach (SkillLevel l in System.Enum.GetValues(typeof(SkillLevel)))
            {
                Skill.Current = l;
                Assert.AreEqual(l != SkillLevel.Advanced, Skill.PhraseCountdown, l.ToString());
            }
        }
    }
}
