using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    // The greeting (#50): the name, the fallback, and the one suggestion for what's next. Runs as a fresh extra player
    // so the editor's own progress is left alone.
    public class GreetingTests
    {
        int id;

        [SetUp]
        public void FreshPlayer()
        {
            id = Players.Add();
            Assume.That(id, Is.GreaterThanOrEqualTo(0), "room for a test player");
            Players.Pick(id);
        }

        [TearDown]
        public void Cleanup()
        {
            Players.Remove(id);
            Players.Pick(0);
        }

        [Test]
        public void SaysHiWithTheNameOrFallsBackToDJ()
        {
            Assert.AreEqual("HI, CASS!", GameDirector.Greeting("Cass"));
            Assert.AreEqual("HI, DJ NOVA!", GameDirector.Greeting("DJ NOVA"));
            Assert.AreEqual("HI, DJ!", GameDirector.Greeting(""));
            Assert.AreEqual("HI, DJ!", GameDirector.Greeting(null));
        }

        [Test]
        public void SuggestsTheFirstLessonThenTheNextOne()
        {
            StringAssert.Contains("lesson 1", GameDirector.Suggestion());
            PlayerPrefs.SetInt(Players.Key(LessonDirector.DoneKey(0)), 1);
            StringAssert.Contains("lesson 2", GameDirector.Suggestion());
            PlayerPrefs.SetInt(Players.Key(LessonDirector.DoneKey(1)), 1);
            StringAssert.Contains("lesson 3", GameDirector.Suggestion());
        }

        [Test]
        public void KeepingAStreakComesBeforeTheNextLesson()
        {
            PlayerPrefs.SetInt(Players.Key(LessonDirector.DoneKey(0)), 1);
            // Passed yesterday, not yet today
            PlayerPrefs.SetString(Players.Key(DailyChallenge.PrefKey), "");
            DailyChallenge.Reload();
            DailyChallenge.Progress.Record(DailyChallenge.Today - 1, SetPerformer.Grade.Good);
            StringAssert.Contains("1-day streak", GameDirector.Suggestion());
        }

        [Test]
        public void WithEveryLessonDoneItSendsYouToTheDailyThenToBuildASet()
        {
            for (int i = 0; i < LessonDirector.LessonNames.Length; i++) PlayerPrefs.SetInt(Players.Key(LessonDirector.DoneKey(i)), 1);
            StringAssert.Contains("DAILY", GameDirector.Suggestion());
            DailyChallenge.Progress.Record(DailyChallenge.Today, SetPerformer.Grade.Good);
            StringAssert.Contains("BUILD A SET", GameDirector.Suggestion());
            Players.BestSet = 512;
            StringAssert.Contains("512", GameDirector.Suggestion());
        }
    }
}
