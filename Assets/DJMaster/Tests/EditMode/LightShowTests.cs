using NUnit.Framework;

namespace DJMaster.Tests
{
    public class LightShowTests
    {
        [Test]
        public void WallIsDarkInSilence()
        {
            for (int c = 0; c < 10; c++) Assert.AreEqual(0f, LightShow.ColumnLevel(c, 3, 0f, 1f, 1f));
        }

        [Test]
        public void LouderMusicLightsMoreOfTheWall()
        {
            float quiet = 0, loud = 0;
            for (int beat = 0; beat < 16; beat++)
                for (int c = 0; c < 10; c++)
                {
                    quiet += LightShow.ColumnLevel(c, beat, 0.3f, 0f, 0.5f);
                    loud += LightShow.ColumnLevel(c, beat, 0.9f, 0f, 0.5f);
                }
            Assert.Greater(loud, quiet * 2f);
        }

        [Test]
        public void ColumnsDanceFromBeatToBeat()
        {
            // Not one flat bar: different columns, and a column changes on the next beat
            Assert.AreNotEqual(LightShow.ColumnLevel(0, 5, 0.8f, 0f, 0.5f), LightShow.ColumnLevel(1, 5, 0.8f, 0f, 0.5f));
            Assert.AreNotEqual(LightShow.ColumnLevel(2, 5, 0.8f, 0f, 0.5f), LightShow.ColumnLevel(2, 6, 0.8f, 0f, 0.5f));
            Assert.Greater(LightShow.ColumnLevel(2, 5, 0.8f, 1f, 0.5f), LightShow.ColumnLevel(2, 5, 0.8f, 0f, 0.5f), "the kick pushes it up");
        }
    }
}
