using NUnit.Framework;

namespace DJMaster.Tests
{
    public class GainTests
    {
        [Test]
        public void GainKnobCoversPlusMinusTwelveDb()
        {
            Assert.AreEqual(1f, DJMath.TrimGain(0.5f), 1e-5f, "center is 0 dB");
            Assert.AreEqual(12f, DJMath.ToDb(DJMath.TrimGain(1f)), 0.01f);
            Assert.AreEqual(-12f, DJMath.ToDb(DJMath.TrimGain(0f)), 0.01f);
            Assert.AreEqual(-9f, DJMath.ToDb(DJMath.TrimGain(LessonDirector.LowGainStart)), 0.01f, "lesson 2 starts Deck B at -9 dB");
            foreach (float v in new[] { 0.1f, 0.33f, 0.5f, 0.8f })
                Assert.AreEqual(v, DJMath.TrimFor(DJMath.TrimGain(v)), 1e-4f, "TrimFor undoes TrimGain");
        }

        [Test]
        public void MeterIsGreenThenYellowThenRed()
        {
            Assert.AreEqual(0, DJMath.MeterLights(0f));
            Assert.AreEqual(4, DJMath.MeterLights(0.45f), "-7 dB: all four green");
            Assert.AreEqual(5, DJMath.MeterLights(0.55f), "-5 dB: first yellow");
            Assert.AreEqual(7, DJMath.MeterLights(0.95f), "just under full scale: all yellow, no red");
            Assert.AreEqual(8, DJMath.MeterLights(1f), "full scale is the red light");
            Assert.AreEqual(DJMath.MeterGreen + DJMath.MeterYellow + 1, DJMath.MeterDb.Length);

            Assert.AreEqual(DJMath.Zone.Low, DJMath.LevelZone(0.3f));
            Assert.AreEqual(DJMath.Zone.Good, DJMath.LevelZone(0.7f));
            Assert.AreEqual(DJMath.Zone.Red, DJMath.LevelZone(1f));
            Assert.Less(MixJudge.QuietLevel, DJMath.YellowLevel, "the crowd only complains about songs well below the yellow");
        }

        [Test]
        public void GainControlsAreColorCodedByDeck()
        {
            Assert.AreEqual("BLUE", BoardColors.NameOf("A.gain"));
            Assert.AreEqual("PINK", BoardColors.NameOf("B.gain"));
        }
    }
}
