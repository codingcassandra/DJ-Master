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
            int firstUnknown = System.Array.FindIndex(tracks, t => !t.HasKey);
            Assert.Greater(firstUnknown, TrackLibrary.GameTrackCount - 1, "every song with a key comes before the unknown ones");
            Assert.IsTrue(tracks.Skip(firstUnknown).All(t => !t.HasKey));
            for (int i = 1; i < firstUnknown; i++)
                Assert.LessOrEqual(SongBrowser.WheelPosition(tracks[i - 1]), SongBrowser.WheelPosition(tracks[i]));
        }

        [Test]
        public void KeyLabelUsesTheSharedCamelotKeys()
        {
            Assert.AreEqual("Am  8A", SongBrowser.KeyLabel(TrackLibrary.Tracks[0])); // Neon Tides
            var unknown = System.Array.Find(TrackLibrary.Tracks, t => !t.HasKey);
            Assert.AreEqual("key --", SongBrowser.KeyLabel(unknown));
        }

        [Test]
        public void TheBonusSongHidesUntilUnlocked()
        {
            Assert.AreEqual(!TrackLibrary.IsLocked(TrackLibrary.BonusTrack), SongBrowser.Listed(TrackLibrary.BonusTrack));
            if (TrackLibrary.IsLocked(TrackLibrary.BonusTrack))
                CollectionAssert.DoesNotContain(SongBrowser.SortedOrder(BrowseSort.Crate), TrackLibrary.BonusTrack);
        }

        [Test]
        public void RowsLightUpForHowWellTheyMixWithTheLiveSong()
        {
            var live = TrackLibrary.Tracks[0];                             // Neon Tides, 128 BPM, Am (8A)
            var near = new TrackInfo("", "", 126f, 4, Genre.House, 0);       // Em (9A), 1.6% slower: both green
            var far = new TrackInfo("", "", 110f, 1, Genre.House, 0);        // C#m (12A), 14% slower: both red
            string nearText = SongBrowser.Details(near, live), farText = SongBrowser.Details(far, live);
            StringAssert.Contains(BoardColors.Green("126 BPM"), nearText);
            StringAssert.Contains(BoardColors.Green("Em  9A"), nearText);
            StringAssert.Contains(BoardColors.Red("110 BPM"), farText);
            StringAssert.Contains(BoardColors.Red("C#m  12A"), farText);
            StringAssert.Contains("#9AA0B0", SongBrowser.Details(near, null), "nothing live: plain grey");
        }
    }
}
