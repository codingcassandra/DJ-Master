using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DJMaster.Tests
{
    public class DailyChallengeTests
    {
        [SetUp] public void NoOverride() => DailyChallenge.TestOverride = null;

        [Test]
        public void SameDateAlwaysGivesTheSameChallenge()
        {
            var day = new DateTime(2026, 11, 18, 9, 30, 0);
            var a = DailyChallenge.ForDate(day);
            var b = DailyChallenge.ForDate(day.Date.AddHours(23)); // any time that day
            Assert.AreEqual(a.from, b.from);
            Assert.AreEqual(a.to, b.to);
            Assert.AreEqual(a.style, b.style);
            Assert.AreEqual(a.Title, DailyChallenge.ForDate(new DateTime(2026, 11, 18)).Title);
        }

        [Test]
        public void EveryChallengeIsPlayableAndChangesDaily()
        {
            var styles = new HashSet<TransitionStyle>();
            var songs = new HashSet<int>();
            DailyChallenge.Challenge? yesterday = null;
            for (int d = 0; d < 365; d++)
            {
                var c = DailyChallenge.ForDay(d);
                Assert.AreNotEqual(c.from, c.to, $"day {d}");
                Assert.AreNotEqual(TrackLibrary.BonusTrack, c.from, "the bonus song isn't in the rotation");
                Assert.AreNotEqual(TrackLibrary.BonusTrack, c.to, "the bonus song isn't in the rotation");
                Assert.IsNull(SetList.Advice(c.from, c.to, c.style), $"day {d}: {c.Title} needs tempos within reach");
                Assert.IsTrue(c.ToSet().Ready);
                if (yesterday is { } y) Assert.IsFalse(y.from == c.from && y.to == c.to && y.style == c.style, $"day {d} repeats yesterday");
                yesterday = c;
                styles.Add(c.style); songs.Add(c.from); songs.Add(c.to);
            }
            Assert.AreEqual(Enum.GetValues(typeof(TransitionStyle)).Length, styles.Count, "every transition style comes up");
            Assert.GreaterOrEqual(songs.Count, 14, "most of the library comes up");
        }

        [Test]
        public void StreakCountsDaysInARow()
        {
            var p = new DailyProgress();
            Assert.AreEqual(0, p.StreakOn(10));
            Assert.IsFalse(p.Record(10, SetPerformer.Grade.Rough), "ROUGH doesn't pass");
            Assert.AreEqual(0, p.StreakOn(10));
            Assert.AreEqual((int)SetPerformer.Grade.Rough, p.BestGradeOn(10));

            Assert.IsTrue(p.Record(10, SetPerformer.Grade.Good));
            Assert.IsFalse(p.Record(10, SetPerformer.Grade.Perfect), "a second pass the same day doesn't grow the streak");
            Assert.AreEqual(1, p.StreakOn(10));
            Assert.AreEqual((int)SetPerformer.Grade.Perfect, p.BestGradeOn(10), "best grade today");
            Assert.AreEqual(-1, p.BestGradeOn(11), "a new day starts with no grade");
            Assert.AreEqual(1, p.StreakOn(11), "the streak is still alive the next day");

            Assert.IsTrue(p.Record(11, SetPerformer.Grade.Good));
            Assert.IsTrue(p.Record(12, SetPerformer.Grade.Perfect));
            Assert.AreEqual(3, p.StreakOn(12));
            Assert.AreEqual(0, p.StreakOn(14), "missing a day breaks it");
            Assert.IsTrue(p.Record(14, SetPerformer.Grade.Good));
            Assert.AreEqual(1, p.StreakOn(14));
            Assert.AreEqual(3, p.bestStreak, "the unlock is kept after a break");

            var back = DailyProgress.Parse(p.Serialize());
            Assert.AreEqual(p.Serialize(), back.Serialize());
            Assert.AreEqual(1, back.StreakOn(14));
            Assert.AreEqual(0, DailyProgress.Parse("garbage").StreakOn(14));
            Assert.AreEqual(0, DailyProgress.Parse(null).bestStreak);
        }

        [Test]
        public void ThreeDaysUnlocksTheBonusSong()
        {
            DailyChallenge.ResetProgress();
            Assert.IsTrue(TrackLibrary.IsLocked(TrackLibrary.BonusTrack));
            Assert.IsFalse(TrackLibrary.IsLocked(0));
            var p = DailyChallenge.Progress;
            for (int d = 0; d < DailyChallenge.UnlockStreak; d++) p.Record(100 + d, SetPerformer.Grade.Good);
            Assert.IsFalse(TrackLibrary.IsLocked(TrackLibrary.BonusTrack));
            DailyChallenge.ResetProgress();
        }
    }
}
