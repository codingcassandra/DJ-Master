using NUnit.Framework;

namespace DJMaster.Tests
{
    public class MixMatchTests
    {
        const int C = 0, Db = 1, D = 2, Eb = 3, E = 4, F = 5, Gb = 6, G = 7, Ab = 8, A = 9, Bb = 10, B = 11;

        static TrackInfo Synth(int root, float bpm = 128f) => new("t", "a", bpm, root, Genre.House, 1);
        static TrackInfo Rec(int root = -1, bool major = false, float bpm = 128f) =>
            TrackInfo.Recording("r", "a", bpm, Genre.House, "clip", root: root, major: major);

        [Test]
        public void MinorKeysGoOnTheARing()
        {
            Assert.AreEqual("8A", MixMatch.Camelot(A, false));
            Assert.AreEqual("9A", MixMatch.Camelot(E, false));
            Assert.AreEqual("7A", MixMatch.Camelot(D, false));
            Assert.AreEqual("5A", MixMatch.Camelot(C, false));
            Assert.AreEqual("1A", MixMatch.Camelot(Ab, false));
            Assert.AreEqual("12A", MixMatch.Camelot(Db, false));
            Assert.AreEqual("11A", MixMatch.Camelot(Gb, false));
            Assert.AreEqual("2A", MixMatch.Camelot(Eb, false));
        }

        [Test]
        public void MajorKeysGoOnTheBRing()
        {
            Assert.AreEqual("8B", MixMatch.Camelot(C, true));
            Assert.AreEqual("9B", MixMatch.Camelot(G, true));
            Assert.AreEqual("1B", MixMatch.Camelot(B, true));
            Assert.AreEqual("2B", MixMatch.Camelot(Gb, true));
            Assert.AreEqual("7B", MixMatch.Camelot(F, true));
            Assert.AreEqual("12B", MixMatch.Camelot(E, true));
            Assert.AreEqual("6B", MixMatch.Camelot(Bb, true));
        }

        [Test]
        public void EveryKeyGetsItsOwnSpot()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int pc = 0; pc < 12; pc++)
            {
                Assert.IsTrue(seen.Add(MixMatch.Camelot(pc, false)));
                Assert.IsTrue(seen.Add(MixMatch.Camelot(pc, true)));
            }
            Assert.AreEqual(24, seen.Count);
        }

        [Test]
        public void SynthTracksAreMinorAndRecordingsCanBeUnknown()
        {
            Assert.AreEqual("8A", MixMatch.Camelot(Synth(A)));
            Assert.AreEqual("Am", Synth(A).Key);
            Assert.AreEqual("8B", MixMatch.Camelot(Rec(C, major: true)));
            Assert.AreEqual("C", Rec(C, major: true).Key);
            Assert.IsFalse(Rec().HasKey);
            Assert.AreEqual("", MixMatch.Camelot(Rec()));
            Assert.AreEqual("", Rec().Key);
        }

        [Test]
        public void SameKeyIsPerfect()
        {
            Assert.AreEqual(KeyMatch.Perfect, MixMatch.Keys(A, false, A, false));
            Assert.AreEqual(KeyMatch.Perfect, MixMatch.Keys(Synth(D), Synth(D)));
        }

        [Test]
        public void OneStepAroundTheWheelIsGood()
        {
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(A, false, E, false)); // 8A -> 9A
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(A, false, D, false)); // 8A -> 7A
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(C, true, G, true));   // 8B -> 9B
            Assert.AreEqual(KeyMatch.Clash, MixMatch.Keys(A, false, B, false)); // 8A -> 10A is two steps
        }

        [Test]
        public void WheelWrapsFrom12To1()
        {
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(Db, false, Ab, false)); // 12A -> 1A
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(Ab, false, Db, false)); // 1A -> 12A
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(E, true, B, true));     // 12B -> 1B
            Assert.AreEqual(KeyMatch.Clash, MixMatch.Keys(Db, false, Eb, false)); // 12A -> 2A
        }

        [Test]
        public void RelativeMajorAndMinorAreGood()
        {
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(A, false, C, true));  // 8A -> 8B
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(G, true, E, false));  // 9B -> 9A
            // Same root but major vs minor (A minor and A major) is a different spot on the wheel
            Assert.AreEqual(KeyMatch.Clash, MixMatch.Keys(A, false, A, true));
            // Changing number and letter at once is too far
            Assert.AreEqual(KeyMatch.Clash, MixMatch.Keys(A, false, G, true)); // 8A -> 9B
        }

        [Test]
        public void UnknownKeyMeansNoHint()
        {
            Assert.AreEqual(KeyMatch.Unknown, MixMatch.Keys(Rec(), Synth(A)));
            Assert.AreEqual(KeyMatch.Unknown, MixMatch.Keys(Synth(A), Rec()));
            Assert.AreEqual(KeyMatch.Good, MixMatch.Keys(Rec(C, major: true), Synth(A)));
        }

        [Test]
        public void TempoBandsAtTheEdges()
        {
            Assert.AreEqual(TempoMatch.Green, MixMatch.Tempo(100f, 100f));
            Assert.AreEqual(TempoMatch.Green, MixMatch.Tempo(100f, 104f));  // exactly 4%
            Assert.AreEqual(TempoMatch.Yellow, MixMatch.Tempo(100f, 104.2f));
            Assert.AreEqual(TempoMatch.Yellow, MixMatch.Tempo(100f, 108f)); // exactly 8%
            Assert.AreEqual(TempoMatch.Red, MixMatch.Tempo(100f, 108.2f));
            Assert.AreEqual(TempoMatch.Green, MixMatch.Tempo(100f, 96f));   // slower works the same
            Assert.AreEqual(TempoMatch.Yellow, MixMatch.Tempo(100f, 92f));
            Assert.AreEqual(TempoMatch.Red, MixMatch.Tempo(100f, 91.8f));
        }

        [Test]
        public void TempoForRealSongs()
        {
            Assert.AreEqual(TempoMatch.Green, MixMatch.Tempo(Synth(A, 128f), Synth(D, 124f)));
            Assert.AreEqual(TempoMatch.Yellow, MixMatch.Tempo(Synth(A, 128f), Synth(D, 120f)));
            Assert.AreEqual(TempoMatch.Red, MixMatch.Tempo(Synth(A, 128f), Synth(D, 140f)));
            Assert.AreEqual(3.125f, MixMatch.TempoGapPercent(128f, 124f), 1e-3f);
        }

        [Test]
        public void EveryLibrarySongWithAKeyHasACamelotCode()
        {
            foreach (var t in TrackLibrary.Tracks)
            {
                if (!t.HasKey) continue;
                StringAssert.IsMatch("^(1[0-2]|[1-9])[AB]$", MixMatch.Camelot(t), t.title);
            }
        }
    }
}
