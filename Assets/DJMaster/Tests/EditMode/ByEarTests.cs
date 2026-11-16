using NUnit.Framework;

namespace DJMaster.Tests
{
    public class ByEarTests
    {
        [Test]
        public void GradesHowCloseTheBeatmatchIs()
        {
            Assert.AreEqual(ByEar.Grade.Perfect, ByEar.Score(0.05f, 10f));
            Assert.AreEqual(ByEar.Grade.Good, ByEar.Score(0.2f, 30f));
            Assert.AreEqual(ByEar.Grade.Close, ByEar.Score(0.8f, 80f));
            Assert.AreEqual(ByEar.Grade.Off, ByEar.Score(3f, 10f), "kicks together but drifting apart");
            Assert.AreEqual(ByEar.Grade.Off, ByEar.Score(0f, 200f), "same tempo, flamming");
            Assert.AreEqual(ByEar.Score(-0.2f, -30f), ByEar.Score(0.2f, 30f), "ahead or behind counts the same");
        }

        [Test]
        public void MillisecondsFromThePhase()
        {
            Assert.AreEqual(117.2f, ByEar.MsOff(0.25f, 128f), 0.1f, "a quarter beat at 128 BPM");
            Assert.AreEqual(ByEar.MsOff(0.1f, 120f), ByEar.MsOff(-0.1f, 120f));
        }

        [Test]
        public void SaysHowCloseInWords()
        {
            Assert.AreEqual("BPM off by 0.3, kicks 23 ms apart (B ahead)", ByEar.HowClose(-0.3f, 0.05f, 128f));
            Assert.AreEqual("BPM off by 0.0, kicks 0 ms apart", ByEar.HowClose(0f, 0f, 128f));
            StringAssert.Contains("TEMPO", ByEar.Tip(1f, 10f), "drifting: fix the tempo first");
            StringAssert.Contains("jog", ByEar.Tip(0.1f, 60f), "tempo right: nudge the kicks together");
        }
    }
}
