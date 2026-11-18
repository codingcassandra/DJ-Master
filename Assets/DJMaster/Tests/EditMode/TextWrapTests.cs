using NUnit.Framework;

namespace DJMaster.Tests
{
    // Wrap keeps a number with its unit, and "9 or 13", on one line (#55)
    public class TextWrapTests
    {
        static void NoLineStartsWith(string wrapped, params string[] starts)
        {
            foreach (var line in wrapped.Split('\n'))
                foreach (var s in starts)
                    Assert.IsFalse(line.StartsWith(s), $"line starts with '{s}': {wrapped}");
        }

        [Test]
        public void NumberAndUnitStayTogether()
        {
            for (int width = 8; width < 60; width++)
            {
                string w = TextUtil.Wrap("LISTEN BACK: beats in sync 84% of mix time, 0 s of clashing, 0 s of silence.", width);
                NoLineStartsWith(w, "s ", "s.", "s,");
            }
        }

        [Test]
        public void PhraseBarsStayTogether()
        {
            for (int width = 12; width < 60; width++)
            {
                string w = TextUtil.Wrap("hit PLAY right on bar 1, 5, 9 or 13 and slam the crossfader across.", width);
                NoLineStartsWith(w, "or ", "13");
            }
        }

        [Test]
        public void GlueOnlyBetweenNumbersAndUnits()
        {
            Assert.AreEqual("press 5 songs", TextUtil.KeepTogether("press 5 songs"), "'songs' isn't a unit");
            Assert.AreEqual("12" + TextUtil.Glue + "bars", TextUtil.KeepTogether("12 bars"));
            Assert.AreEqual("128" + TextUtil.Glue + "BPM.", TextUtil.KeepTogether("128 BPM."));
        }
    }
}
