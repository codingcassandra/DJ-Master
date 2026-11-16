using NUnit.Framework;

namespace DJMaster.Tests
{
    // #33: keep the sharpest eye resolution that holds the frame rate.
    public class RenderQualityTests
    {
        [Test]
        public void StepsDownOnlyWhenTheFrameRateDrops()
        {
            Assert.AreEqual(1.2f, RenderQuality.NextScale(1.2f, 72f, 72f), 1e-4, "holding 72: stay sharp");
            Assert.AreEqual(1.2f, RenderQuality.NextScale(1.2f, 69f, 72f), 1e-4, "a little under (within 5%) is fine");
            Assert.AreEqual(1.1f, RenderQuality.NextScale(1.2f, 60f, 72f), 1e-4, "too slow: one notch down");
            Assert.AreEqual(1.0f, RenderQuality.NextScale(1.1f, 60f, 72f), 1e-4);
            Assert.AreEqual(1.0f, RenderQuality.NextScale(1.0f, 40f, 72f), 1e-4, "never below 1.0");
        }
    }
}
