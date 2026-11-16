using NUnit.Framework;

namespace DJMaster.Tests
{
    public class SkillTests
    {
        [TearDown] public void Cleanup() => Skill.Reset();

        [Test]
        public void PickerUsesTheSharedSavedLevel()
        {
            Skill.Reset();
            Assert.AreEqual(SkillLevel.Intermediate, Skill.Current, "nothing saved: the game as it always was");
            Skill.Current = SkillLevel.Advanced;
            Assert.AreEqual(SkillLevel.Advanced, SkillLevels.Current, "the picker sets the level the rules use");
            Assert.IsFalse(SkillLevels.Rules.checklist);
            SkillLevels.Reload();
            Assert.AreEqual(SkillLevel.Advanced, Skill.Current, "saved between sessions");
            Assert.AreEqual("ADVANCED", Skill.Name(SkillLevel.Advanced));
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
