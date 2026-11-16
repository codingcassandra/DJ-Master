using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    // Pick player (#49): the player list saves and loads, and each player's progress is their own.
    public class PlayersTests
    {
        // The first player's real saves in the editor, put back after each test
        (string key, bool has, int i, string s)[] saved;

        [SetUp]
        public void Fresh()
        {
            saved = Players.ProgressKeys().Select(k => (k, PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0), PlayerPrefs.GetString(k, ""))).ToArray();
            foreach (var p in Players.All.ToArray()) if (p.id != 0) Players.Remove(p.id);
            Players.ResetAll();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var p in Players.All.ToArray()) if (p.id != 0) Players.Remove(p.id);
            Players.ResetAll();
            foreach (var (key, has, i, str) in saved)
            {
                PlayerPrefs.DeleteKey(key);
                if (!has) continue;
                if (key == DailyChallenge.PrefKey || key == SetBuilderPanel.SetKey) PlayerPrefs.SetString(key, str);
                else PlayerPrefs.SetInt(key, i);
            }
            SkillLevels.Reload();
            DailyChallenge.Reload();
        }

        [Test]
        public void NoSaveMeansOneDefaultPlayerOnThePlainKeys()
        {
            Assert.AreEqual(1, Players.All.Count);
            Assert.AreEqual(0, Players.CurrentId);
            Assert.AreEqual(Players.DefaultName, Players.Current.name);
            Assert.AreEqual("bestSet", Players.Key("bestSet"), "progress saved before players existed stays with the first player");
        }

        [Test]
        public void ListSurvivesSaveAndLoad()
        {
            var list = Players.Parse("0:DJ|3:DJ NOVA|5:Sam");
            CollectionAssert.AreEqual(new[] { 0, 3, 5 }, list.Select(p => p.id));
            CollectionAssert.AreEqual(new[] { "DJ", "DJ NOVA", "Sam" }, list.Select(p => p.name));
            Assert.AreEqual("0:DJ|3:DJ NOVA|5:Sam", Players.Serialize(list));
            Assert.AreEqual(1, Players.Parse("junk|:x|-2:y").Count, "bad saves fall back to the default player");
            Assert.AreEqual("1:a b c", Players.Serialize(new[] { new Players.Player(1, "a|b:c") }), "names can't break the format");
        }

        [Test]
        public void EachPlayerHasTheirOwnProgress()
        {
            foreach (var k in Players.ProgressKeys()) PlayerPrefs.DeleteKey(k); // a clean first player (put back after)
            DailyChallenge.Reload();
            SkillLevels.Reload();
            // The first player finishes lesson 1, sets a best and picks ADVANCED
            PlayerPrefs.SetInt(Players.Key(LessonDirector.DoneKey(0)), 1);
            Players.BestSet = 420;
            Skill.Current = SkillLevel.Advanced;
            DailyChallenge.Record(SetPerformer.Grade.Perfect);

            int nova = Players.Add();
            Assert.AreEqual("DJ NOVA", Players.All.First(p => p.id == nova).name);
            Players.Pick(nova);
            Assert.AreEqual(nova, Players.CurrentId);
            Assert.IsFalse(LessonDirector.IsDone(0), "a new player starts fresh");
            Assert.AreEqual(0, Players.BestSet);
            Assert.AreEqual(SkillLevel.Intermediate, Skill.Current);
            Assert.AreEqual(0, DailyChallenge.Progress.StreakOn(DailyChallenge.Today));

            Skill.Current = SkillLevel.Beginner;
            Players.BestSet = 90;

            Players.Pick(0);
            Assert.IsTrue(LessonDirector.IsDone(0), "switching back brings the first player's progress back");
            Assert.AreEqual(420, Players.BestSet);
            Assert.AreEqual(SkillLevel.Advanced, Skill.Current);
            Assert.AreEqual(1, DailyChallenge.Progress.StreakOn(DailyChallenge.Today));
            StringAssert.Contains("ADVANCED", Players.Summary(0));
            StringAssert.Contains("Lessons done: 1", Players.Summary(0));
            StringAssert.Contains("Best set 420", Players.Summary(0));
            StringAssert.Contains("BEGINNER", Players.Summary(nova));
        }

        [Test]
        public void PickedPlayerIsRememberedBetweenSessions()
        {
            int nova = Players.Add();
            Players.Pick(nova);
            Assert.AreEqual(nova, PlayerPrefs.GetInt(Players.CurrentKey));
            StringAssert.Contains($"{nova}:DJ NOVA", PlayerPrefs.GetString(Players.ListKey));
        }

        [Test]
        public void RemovingClearsTheirSavesAndNeverReusesTheId()
        {
            int nova = Players.Add();
            Players.Pick(nova);
            Players.BestSet = 300;
            Assert.IsTrue(Players.Remove(nova));
            Assert.AreEqual(0, Players.CurrentId, "removing the current player switches to another one");
            Assert.IsFalse(PlayerPrefs.HasKey(Players.KeyFor(nova, Players.BestSetKey)), "their saves are gone");
            Assert.IsFalse(Players.Remove(0), "the last player can't be removed");
            Assert.AreNotEqual(nova, Players.Add(), "a new player never gets an old player's id");
        }

        [Test]
        public void AtMostFourPlayersWithFreshNames()
        {
            while (!Players.Full) Players.Add();
            Assert.AreEqual(Players.MaxPlayers, Players.All.Count);
            Assert.AreEqual(-1, Players.Add());
            Assert.AreEqual(Players.All.Count, Players.All.Select(p => p.name).Distinct().Count(), "no two players share a name");
        }

        [Test]
        public void SignedInUserIsTheFirstPlayer()
        {
            Players.Add();
            Players.SignedIn("Cass");
            Assert.AreEqual("Cass", Players.All[0].name);
        }
    }
}
