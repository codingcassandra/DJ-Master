using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // DAILY on the title menu: plays the challenge as a 2-song live set, then saves the pass and shows the streak.
    public class DailyChallengeTest
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
            DailyChallenge.ResetProgress();
            // A known challenge, so the test can play it: a QUICK CUT from Neon Tides into Sundial
            DailyChallenge.TestOverride = new DailyChallenge.Challenge { from = 0, to = 3, style = TransitionStyle.Cut };
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            DailyChallenge.TestOverride = null;
            DailyChallenge.ResetProgress();
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        static bool OnPhraseLine(Deck d)
        {
            double bar = d.Beat / TrackSynth.BeatsPerBar;
            return bar % 1.0 < 0.03 && (int)bar % 4 == 0;
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator PassTheDailyChallengeAndStartAStreak()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var performer = Object.FindAnyObjectByType<SetPerformer>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var system = Object.FindAnyObjectByType<SystemButtons>(FindObjectsInactive.Include);

            Assert.AreEqual("DAILY", menu.labels[4].text);
            StringAssert.Contains("QUICK CUT: Neon Tides into Sundial", menu.descriptions[4].text.Replace("\n", " "));
            Assert.IsTrue(menu.buttons[4].Lit, "lit while today's challenge is waiting");
            StringAssert.DoesNotContain("streak", menu.footer.text);

            menu.buttons[4].SimulatePress(); // DAILY
            menu.buttons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.Live, game.Mode);
            CollectionAssert.AreEqual(new[] { 0, 3 }, performer.Set.songs);
            var A = board.A; var B = board.B;
            yield return WaitFor(() => A.deck.IsPlaying && B.deck.IsLoaded, 20f, "song 1 playing, song 2 loaded");
            yield return null;
            StringAssert.StartsWith("DAILY CHALLENGE", panel.title.text);

            // The cut: on a phrase line, PLAY the new song and slam the crossfader across
            B.volume.SetValue(1f);
            yield return WaitFor(() => OnPhraseLine(A.deck), 10f, "phrase line");
            B.play.SimulatePress();
            board.crossfader.SetValue(1f);
            yield return WaitFor(() => performer.SongIndex == 1, 3f, "cut complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[0].grade, performer.Results[0].note);

            panel.actionButtons[2].SimulatePress(); // END SET
            Assert.AreEqual(GameMode.LiveResults, game.Mode);
            Assert.AreEqual(1, DailyChallenge.Progress.StreakOn(DailyChallenge.Today));
            Assert.AreEqual((int)SetPerformer.Grade.Perfect, DailyChallenge.Progress.BestGradeOn(DailyChallenge.Today));
            Assert.IsFalse(panel.actionButtons[2].gameObject.activeSelf, "no EDIT SET for the daily challenge");
            yield return null;
            StringAssert.Contains("Passed! Streak: 1 day in a row", panel.body.text.Replace("\n", " "));

            system.home.SimulatePress();
            Assert.AreEqual(GameMode.Title, game.Mode);
            StringAssert.Contains("Daily streak: 1 day in a row", menu.footer.text);
            StringAssert.Contains("Your best: PERFECT", menu.descriptions[4].text.Replace("\n", " "));
            Assert.IsFalse(menu.buttons[4].Lit, "done for today");
        }
    }
}
