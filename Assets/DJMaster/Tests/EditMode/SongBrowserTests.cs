using System.Linq;
using NUnit.Framework;

namespace DJMaster.Tests
{
    public class SongBrowserTests
    {
        [Test]
        public void EverySortListsAllSongsOnce()
        {
            foreach (BrowseSort sort in System.Enum.GetValues(typeof(BrowseSort)))
            {
                var order = SongBrowser.SortedOrder(sort);
                var listed = Enumerable.Range(0, TrackLibrary.Tracks.Length).Where(SongBrowser.Listed);
                Assert.GreaterOrEqual(order.Length, 16, $"{sort} should show at least our 16 songs");
                CollectionAssert.AreEquivalent(listed, order, $"{sort} lost or doubled a song");
            }
        }

        [Test]
        public void CrateOrderIsOurSongsThenTheFreeOnes()
        {
            CollectionAssert.AreEqual(Enumerable.Range(0, TrackLibrary.Tracks.Length).Where(SongBrowser.Listed), SongBrowser.SortedOrder(BrowseSort.Crate));
        }

        [Test]
        public void BpmSortGoesSlowestFirst()
        {
            var bpms = SongBrowser.SortedOrder(BrowseSort.Bpm).Select(i => TrackLibrary.Tracks[i].bpm).ToArray();
            for (int i = 1; i < bpms.Length; i++) Assert.LessOrEqual(bpms[i - 1], bpms[i]);
        }

        [Test]
        public void KeySortWalksTheCamelotWheelWithUnknownKeysLast()
        {
            var tracks = SongBrowser.SortedOrder(BrowseSort.Key).Select(i => TrackLibrary.Tracks[i]).ToArray();
            int firstRecording = System.Array.FindIndex(tracks, t => t.IsRecording);
            Assert.AreEqual(TrackLibrary.GameTrackCount, firstRecording, "songs with a key come first");
            Assert.IsTrue(tracks.Skip(firstRecording).All(t => t.IsRecording));
            for (int i = 1; i < firstRecording; i++)
                Assert.LessOrEqual(tracks[i - 1].CamelotNumber, tracks[i].CamelotNumber);
        }

        static int Cam(int root) => new TrackInfo("", "", 120f, root, Genre.House, 0).CamelotNumber;

        [Test]
        public void CamelotNumbersMatchTheWheel()
        {
            Assert.AreEqual(8, Cam(9));  // A minor = 8A
            Assert.AreEqual(9, Cam(4));  // E minor = 9A, a fifth up
            Assert.AreEqual(5, Cam(0));  // C minor = 5A
            Assert.AreEqual(1, Cam(8));  // G# minor = 1A
            Assert.AreEqual(12, Cam(1)); // C# minor = 12A
            Assert.AreEqual(0, TrackLibrary.Tracks[8].CamelotNumber, "recordings have no key on file");
            Assert.AreEqual("8A", new TrackInfo("", "", 120f, 9, Genre.House, 0).Camelot);
        }
    }
}
