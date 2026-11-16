using NUnit.Framework;

namespace DJMaster.Tests
{
    // #45: about 1 second per 4 words, at least 3 seconds. Color tags don't count as words.
    public class ReadingTests
    {
        [Test]
        public void OneSecondPerFourWordsAndAtLeastThree()
        {
            Assert.AreEqual(0, Reading.Words(""));
            Assert.AreEqual(3, Reading.Words("Press  PLAY\nnow"));
            Assert.AreEqual(2, Reading.Words("<color=#FF0000>Deck</color> <b>A</b>"));
            Assert.AreEqual(3f, Reading.Seconds("Short."), 1e-4);
            string forty = string.Join(" ", System.Linq.Enumerable.Repeat("word", 40));
            Assert.AreEqual(10f, Reading.Seconds(forty), 1e-4);
        }
    }
}
