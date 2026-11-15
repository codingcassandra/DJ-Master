using NUnit.Framework;

namespace DJMaster.Tests
{
    public class PhraseCueTests
    {
        static string T(int bar, int beat, bool sections, out PhraseCue.Kind k) => PhraseCue.Text(bar, beat, sections, out k);

        [Test]
        public void CountsBarsThenBeatsToTheNextPhrase()
        {
            Assert.AreEqual("NEW PHRASE!", T(4, 0, false, out var k)); Assert.AreEqual(PhraseCue.Kind.NewPhrase, k);
            Assert.AreEqual("PHRASE IN 4 BARS", T(4, 1, false, out k)); Assert.AreEqual(PhraseCue.Kind.Count, k);
            Assert.AreEqual("PHRASE IN 3 BARS", T(5, 0, false, out _));
            Assert.AreEqual("PHRASE IN 2 BARS", T(6, 3, false, out _));
            Assert.AreEqual("PHRASE IN 4 BEATS", T(7, 0, false, out k)); Assert.AreEqual(PhraseCue.Kind.LastBar, k);
            Assert.AreEqual("PHRASE IN 1 BEAT", T(7, 3, false, out _));
            Assert.AreEqual("NEW PHRASE!", T(8, 0, false, out _), "...and the 1 of bar 9 flashes");
            Assert.AreEqual("PHRASE IN 2 BEATS", T(15, 2, false, out _), "bar 16 rolls round to bar 1");
            for (int bar = 0; bar < 16; bar++)
            {
                Assert.That(PhraseCue.BarsToNextPhrase(bar), Is.InRange(1, 4));
                for (int beat = 0; beat < 4; beat++)
                    foreach (bool sections in new[] { false, true })
                        Assert.LessOrEqual(T(bar, beat, sections, out _).Length, 18, "fits beside the beat lights");
            }
        }

        [Test]
        public void WarnsBeforeTheBreakdownAndTheDrop()
        {
            // Our songs: bars 1-8 DROP, 9-12 GROOVE, 13-14 BREAKDOWN, 15-16 BUILD (0-based 0-7, 8-11, 12-13, 14-15)
            Assert.AreEqual("PHRASE IN 3 BARS", T(9, 0, true, out _), "3 bars out: no warning yet");
            Assert.AreEqual("BREAKDOWN: 2 BARS", T(10, 1, true, out var k)); Assert.AreEqual(PhraseCue.Kind.HeadsUp, k);
            Assert.AreEqual("BREAKDOWN: 3 BEATS", T(11, 1, true, out _));
            Assert.AreEqual("DROP: 2 BARS", T(14, 0, true, out _));
            Assert.AreEqual("DROP: 1 BEAT", T(15, 3, true, out _));
            Assert.AreEqual("NEW PHRASE!", T(0, 0, true, out _), "the drop itself is a new phrase");
            Assert.AreEqual("PHRASE IN 2 BARS", T(6, 0, true, out _), "the groove at bar 9 isn't a big moment");
            Assert.AreEqual("PHRASE IN 4 BEATS", T(11, 0, false, out _), "recordings don't know their sections");
        }
    }
}
