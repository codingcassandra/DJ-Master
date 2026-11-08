using NUnit.Framework;

namespace DJMaster.Tests
{
    public class MixMathTests
    {
        [Test]
        public void CrossfaderIsConstantPower()
        {
            Assert.AreEqual(1f, DJMath.CrossfaderGain(0f, 0), 1e-5f);
            Assert.AreEqual(0f, DJMath.CrossfaderGain(0f, 1), 1e-5f);
            Assert.AreEqual(1f, DJMath.CrossfaderGain(1f, 1), 1e-5f);
            for (float x = 0; x <= 1f; x += 0.1f)
            {
                float a = DJMath.CrossfaderGain(x, 0), b = DJMath.CrossfaderGain(x, 1);
                Assert.AreEqual(1f, a * a + b * b, 1e-4f);
            }
        }

        [Test]
        public void EqKnobKillsUnityAndBoosts()
        {
            Assert.AreEqual(0f, DJMath.EqGain(0f), 1e-6f);
            Assert.AreEqual(1f, DJMath.EqGain(0.5f), 1e-6f);
            Assert.AreEqual(2f, DJMath.EqGain(1f), 1e-6f);
        }

        [Test]
        public void TempoFaderCoversEightPercent()
        {
            Assert.AreEqual(1f, DJMath.TempoRate(0.5f), 1e-6f);
            Assert.AreEqual(1.08f, DJMath.TempoRate(1f), 1e-6f);
            Assert.AreEqual(0.92f, DJMath.TempoRate(0f), 1e-6f);
            // The lesson's 124 BPM track can reach 128.
            Assert.Greater(124f * DJMath.TempoRate(1f), 128f);
        }

        [TestCase(10.0, 10.25, 0.25f)]
        [TestCase(10.0, 9.75, -0.25f)]
        [TestCase(3.9, 8.1, 0.2f)]
        [TestCase(5.0, 7.0, 0f)]
        public void PhaseOffsetWraps(double a, double b, float expected)
        {
            Assert.AreEqual(expected, DJMath.PhaseOffset(a, b), 1e-4f);
        }

        [Test]
        public void DistanceToBeat()
        {
            Assert.AreEqual(0.1f, DJMath.DistanceToBeat(4.1), 1e-5f);
            Assert.AreEqual(0.1f, DJMath.DistanceToBeat(3.9), 1e-5f);
            Assert.AreEqual(0.5f, DJMath.DistanceToBeat(2.5), 1e-5f);
        }

        [Test]
        public void WrapBreaksOnWords()
        {
            string w = TextUtil.Wrap("tap play on deck a to start", 10);
            foreach (var line in w.Split('\n')) Assert.LessOrEqual(line.Length, 10);
            Assert.AreEqual("tap play on deck a to start", w.Replace('\n', ' '));
        }

        [Test]
        public void WrapIgnoresColorTags()
        {
            string tagged = $"tap {BoardColors.Green("PLAY")} on deck a";
            string w = TextUtil.Wrap(tagged, 13);
            Assert.AreEqual(tagged, w.Replace('\n', ' '), "tags must never be split");
            foreach (var line in w.Split('\n')) Assert.LessOrEqual(TextUtil.VisibleLength(line), 13);
            Assert.AreEqual("a\nb", TextUtil.Wrap("a\nb", 20), "existing line breaks are kept");
        }

        [Test]
        public void SetStars()
        {
            SetPerformer.Result R(SetPerformer.Grade g) => new SetPerformer.Result { grade = g };
            Assert.AreEqual(0, SetPerformer.Stars(new SetPerformer.Result[0]));
            Assert.AreEqual(1, SetPerformer.Stars(new[] { R(SetPerformer.Grade.Rough), R(SetPerformer.Grade.Rough) }));
            Assert.AreEqual(2, SetPerformer.Stars(new[] { R(SetPerformer.Grade.Good), R(SetPerformer.Grade.Good) }));
            Assert.AreEqual(3, SetPerformer.Stars(new[] { R(SetPerformer.Grade.Perfect), R(SetPerformer.Grade.Perfect), R(SetPerformer.Grade.Good) }));
        }

        [Test]
        public void TapTempo()
        {
            float gap = 60f / 128f;
            Assert.AreEqual(0f, DJMath.BpmFromTaps(new[] { 0f, gap }), "needs at least 3 taps");
            Assert.AreEqual(128f, DJMath.BpmFromTaps(new[] { 0f, gap, gap * 2, gap * 3 }), 0.01f);
            Assert.AreEqual(0f, DJMath.BpmFromTaps(new[] { 0f, gap, gap * 3 }), "uneven taps are ignored");
        }

        [Test]
        public void TempoFaderForIsTheInverseOfTempoRate()
        {
            foreach (float v in new[] { 0f, 0.3f, 0.5f, 0.9f, 1f })
                Assert.AreEqual(v, DJMath.TempoFaderFor(DJMath.TempoRate(v)), 1e-5f);
            Assert.Greater(DJMath.TempoFaderFor(1.1f), 1f, "10% is out of range");
        }
    }
}
