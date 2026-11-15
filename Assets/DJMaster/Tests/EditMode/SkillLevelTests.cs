using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    public class SkillLevelTests
    {
        bool hadSave;
        int saved;

        // These tests change the saved level, so we put back whatever was there
        [SetUp]
        public void SaveLevel()
        {
            hadSave = PlayerPrefs.HasKey(SkillLevels.PrefKey);
            saved = PlayerPrefs.GetInt(SkillLevels.PrefKey, 0);
        }

        [TearDown]
        public void RestoreLevel()
        {
            if (hadSave) PlayerPrefs.SetInt(SkillLevels.PrefKey, saved);
            else PlayerPrefs.DeleteKey(SkillLevels.PrefKey);
            PlayerPrefs.Save();
            SkillLevels.Reload();
        }

        [Test]
        public void NothingSavedMeansIntermediate()
        {
            PlayerPrefs.DeleteKey(SkillLevels.PrefKey);
            SkillLevels.Reload();
            Assert.AreEqual(SkillLevel.Intermediate, SkillLevels.Current);
            Assert.AreSame(SkillLevels.Intermediate, SkillLevels.Rules);
        }

        [Test]
        public void IntermediateIsTheGameAsItWas()
        {
            var r = SkillLevels.Intermediate;
            Assert.AreEqual(0.35f, r.syncBpm);
            Assert.AreEqual(0.07f, r.syncBeats);
            Assert.AreEqual(0.12f, r.tapWindow);
            Assert.AreEqual(25f, r.hintAfter);
            Assert.IsTrue(r.syncAllowed && r.checklist && r.scored);
            Assert.AreEqual(0f, r.syncPenalty);
            Assert.AreEqual(1f, r.clashPerfect);
            Assert.AreEqual(3f, r.clashGood);
            Assert.AreEqual(0.15f, r.onBarWindow);
            Assert.AreEqual(0.6f, r.cutPerfect);
            Assert.AreEqual(2f, r.cutGood);
            Assert.AreEqual(0, r.phraseBonus + r.harmonicBonus);
            Assert.AreEqual(0.12f, r.crowdDrop);
            Assert.AreEqual(0.05f, r.crowdRise);

            // and the game reads them from there
            SkillLevels.Set(SkillLevel.Intermediate);
            Assert.AreEqual(0.35f, MixJudge.SyncBpm);
            Assert.AreEqual(0.07f, MixJudge.SyncBeats);
            Assert.AreEqual(0.12f, LessonDirector.TapWindow);
        }

        [Test]
        public void BeginnerIsLooserThanIntermediateLooserThanAdvanced()
        {
            var b = SkillLevels.Beginner; var i = SkillLevels.Intermediate; var a = SkillLevels.Advanced;
            void Looser(float beg, float mid, float adv, string what)
            {
                Assert.Greater(beg, mid, what);
                Assert.Greater(mid, adv, what);
            }
            Looser(b.syncBpm, i.syncBpm, a.syncBpm, "sync BPM");
            Looser(b.syncBeats, i.syncBeats, a.syncBeats, "sync beats");
            Looser(b.tapWindow, i.tapWindow, a.tapWindow, "tap window");
            Looser(b.clashPerfect, i.clashPerfect, a.clashPerfect, "clash for PERFECT");
            Looser(b.clashGood, i.clashGood, a.clashGood, "clash for GOOD");
            Looser(b.onBarWindow, i.onBarWindow, a.onBarWindow, "on the bar");
            Looser(b.cutPerfect, i.cutPerfect, a.cutPerfect, "cut overlap for PERFECT");
            Looser(b.cutGood, i.cutGood, a.cutGood, "cut overlap for GOOD");
            // hints come sooner, and the crowd drops off slower, the easier it is
            Looser(a.hintAfter, i.hintAfter, b.hintAfter, "hint delay");
            Looser(a.crowdDrop, i.crowdDrop, b.crowdDrop, "crowd drop");

            // a looser level still has to sound like one kick
            foreach (float bpm in new[] { 120f, 128f, 140f })
                Assert.Less(b.syncBeats * 60000f / bpm, 50f, $"{bpm} BPM");
        }

        [Test]
        public void TheSyncRuleFollowsTheLevel()
        {
            SkillLevels.Set(SkillLevel.Beginner);
            Assert.IsTrue(MixJudge.BeatsLocked(0.45f, 0.08f), "close enough for a beginner");
            SkillLevels.Set(SkillLevel.Intermediate);
            Assert.IsFalse(MixJudge.BeatsLocked(0.45f, 0.08f));
            Assert.IsTrue(MixJudge.BeatsLocked(0.3f, 0.06f));
            SkillLevels.Set(SkillLevel.Advanced);
            Assert.IsFalse(MixJudge.BeatsLocked(0.3f, 0.06f), "too loose for advanced");
            Assert.IsTrue(MixJudge.BeatsLocked(0.2f, 0.04f));
        }

        [Test]
        public void AdvancedHasNoSyncAndNoChecklist()
        {
            Assert.IsFalse(SkillLevels.Advanced.syncAllowed);
            Assert.IsFalse(SkillLevels.Advanced.checklist);
            Assert.Greater(SkillLevels.Advanced.phraseBonus, 0);
            Assert.Greater(SkillLevels.Advanced.harmonicBonus, 0);
            foreach (var level in new[] { SkillLevel.Beginner, SkillLevel.Intermediate, SkillLevel.Solo })
                Assert.IsTrue(SkillLevels.For(level).syncAllowed, level.ToString());
        }

        [Test]
        public void SoloHasNoScore()
        {
            Assert.IsFalse(SkillLevels.Solo.scored);
            Assert.IsFalse(SkillLevels.Solo.checklist);
            foreach (var level in new[] { SkillLevel.Beginner, SkillLevel.Intermediate, SkillLevel.Advanced })
                Assert.IsTrue(SkillLevels.For(level).scored, level.ToString());

            var go = new GameObject("judge");
            try
            {
                var judge = go.AddComponent<MixJudge>();
                SkillLevels.Set(SkillLevel.Solo);
                judge.Award("BASS DROP!", 300);
                Assert.AreEqual(0f, judge.Score, "no points in Solo");
                Assert.AreEqual("BASS DROP!", judge.Shoutout, "but the crowd still cheers");
                SkillLevels.Set(SkillLevel.Intermediate);
                judge.Award("BASS DROP!", 300);
                Assert.AreEqual(300f, judge.Score);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetAndCurrentRoundTrip()
        {
            int changes = 0;
            void Count(SkillLevel _) => changes++;
            SkillLevels.Changed += Count;
            try
            {
                SkillLevels.Set(SkillLevel.Intermediate);
                changes = 0;
                foreach (SkillLevel level in System.Enum.GetValues(typeof(SkillLevel)))
                {
                    SkillLevels.Set(level);
                    Assert.AreEqual(level, SkillLevels.Current);
                    Assert.AreEqual((int)level, PlayerPrefs.GetInt(SkillLevels.PrefKey, -1), "saved");
                    SkillLevels.Reload();
                    Assert.AreEqual(level, SkillLevels.Current, "read back from the save");
                    Assert.AreEqual(level, SkillLevels.For(level).level);
                }
                Assert.AreEqual(4, changes);
                SkillLevels.Set(SkillLevels.Current);
                Assert.AreEqual(4, changes, "the same level again isn't a change");
            }
            finally { SkillLevels.Changed -= Count; }
        }

        [Test]
        public void JunkInTheSaveFallsBackToIntermediate()
        {
            PlayerPrefs.SetInt(SkillLevels.PrefKey, 42);
            SkillLevels.Reload();
            Assert.AreEqual(SkillLevel.Intermediate, SkillLevels.Current);
        }

        [Test]
        public void KeysMatchOnTheCircleOfFifths()
        {
            TrackInfo T(int root) => new("t", "a", 128f, root, Genre.House, 1);
            Assert.IsTrue(SkillLevels.KeysMatch(T(9), T(9)), "same key");
            Assert.IsTrue(SkillLevels.KeysMatch(T(9), T(4)), "Am to Em");
            Assert.IsTrue(SkillLevels.KeysMatch(T(9), T(2)), "Am to Dm");
            Assert.IsTrue(SkillLevels.KeysMatch(T(11), T(4)), "wraps past C");
            Assert.IsFalse(SkillLevels.KeysMatch(T(9), T(3)), "a tritone clashes");
            Assert.IsFalse(SkillLevels.KeysMatch(T(9), T(10)), "a half step clashes");
            var rec = TrackInfo.Recording("r", "a", 128f, Genre.House, "clip");
            Assert.IsFalse(SkillLevels.KeysMatch(rec, rec), "recordings don't know their key");
        }
    }
}
