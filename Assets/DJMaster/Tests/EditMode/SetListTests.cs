using NUnit.Framework;

namespace DJMaster.Tests
{
    public class SetListTests
    {
        [Test]
        public void AddingSongsRecordsTheTransitionIntoEach()
        {
            var set = new SetList();
            Assert.IsNull(set.Add(0, TransitionStyle.Cut));   // the opener has no transition
            Assert.IsNull(set.Add(3, TransitionStyle.BassSwap));
            Assert.IsNull(set.Add(5, TransitionStyle.Filter));
            CollectionAssert.AreEqual(new[] { 0, 3, 5 }, set.songs);
            CollectionAssert.AreEqual(new[] { TransitionStyle.BassSwap, TransitionStyle.Filter }, set.transitions);
            Assert.IsTrue(set.Ready);
        }

        [Test]
        public void RejectsDuplicatesAndOverfilling()
        {
            var set = new SetList();
            set.Add(1, TransitionStyle.Blend);
            Assert.IsNotNull(set.Add(1, TransitionStyle.Blend));
            for (int i = 2; set.Count < SetList.MaxSongs; i++) set.Add(i, TransitionStyle.Blend);
            Assert.IsNotNull(set.Add(7, TransitionStyle.Blend));
            Assert.AreEqual(SetList.MaxSongs, set.Count);
        }

        [Test]
        public void UndoRemovesTheSongAndItsTransition()
        {
            var set = new SetList();
            set.Add(0, TransitionStyle.Blend);
            set.Add(1, TransitionStyle.Cut);
            set.Undo();
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(0, set.transitions.Count);
            set.Undo();
            set.Undo(); // undo on an empty set is harmless
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void SurvivesSaveAndLoad()
        {
            var set = new SetList();
            set.Add(2, TransitionStyle.Blend);
            set.Add(4, TransitionStyle.Cut);
            set.Add(6, TransitionStyle.BassSwap);
            var back = SetList.Parse(set.Serialize());
            CollectionAssert.AreEqual(set.songs, back.songs);
            CollectionAssert.AreEqual(set.transitions, back.transitions);
            Assert.AreEqual(0, SetList.Parse("").Count);
            Assert.AreEqual(0, SetList.Parse("garbage|x").Count);
        }

        [Test]
        public void AdvisesAgainstBeatmixingBigTempoGaps()
        {
            // Neon Tides 128 > Sundial 122 is about 5%: fine to fade or cut, too far to beatmix.
            Assert.IsNotNull(SetList.Advice(0, 3, TransitionStyle.Blend));
            Assert.IsNull(SetList.Advice(0, 3, TransitionStyle.Fade));
            Assert.IsNull(SetList.Advice(0, 3, TransitionStyle.Cut));
            Assert.IsNull(SetList.Advice(0, 1, TransitionStyle.BassSwap)); // 128 > 124 is about 3%
        }

        [Test]
        public void NewStylesSurviveSaveAndLoad()
        {
            var set = new SetList();
            set.Add(0, TransitionStyle.Blend);
            set.Add(3, TransitionStyle.Fade);
            set.Add(5, TransitionStyle.EchoOut);
            CollectionAssert.AreEqual(set.transitions, SetList.Parse(set.Serialize()).transitions);
        }
    }
}
