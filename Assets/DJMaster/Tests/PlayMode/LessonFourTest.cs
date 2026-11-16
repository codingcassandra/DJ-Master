using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Plays Lesson 4 (picking the next song) start to finish with the real browser, SORT, LOAD and SYNC buttons.
    public class LessonFourTest
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PickTheNextSong()
        {
            var lessons = Object.FindAnyObjectByType<LessonDirector>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var browser = board.browser;
            var A = board.A.deck;
            var B = board.B.deck;
            int cheers = 0;
            System.Action onCheer = () => cheers++;
            Sfx.Cheered += onCheer;
            try
            {
                menu.buttons[0].SimulatePress(); // LEARN
                menu.buttons[1].SimulatePress(); // INTERMEDIATE
                menu.buttons[1].SimulatePress(); // CLUB
                StringAssert.StartsWith("4 NEXT SONG", menu.labels[3].text);
                menu.buttons[3].SimulatePress(); // lesson 4
                yield return WaitFor(() => lessons.Running && A.IsPlaying, 20f, "Neon Tides playing");
                Assert.AreEqual(3, lessons.LessonIndex);
                Assert.IsFalse(B.HasTrack, "Deck B waits for your pick");

                int step = 0;
                IEnumerator Next(float seconds = 8f)
                {
                    step++;
                    yield return WaitFor(() => lessons.StepIndex == step || lessons.Finished, seconds, $"step {step} ({lessons.Current?.title}) {lessons.Feedback}");
                }

                // SORT: crate -> BPM
                browser.sortButton.SimulatePress();
                Assert.AreEqual(BrowseSort.Bpm, browser.Sort);
                yield return Next();

                // A tempo match that clashes in key isn't enough later, but it is for this step
                browser.Select(LessonDirector.BadPick);
                Assert.IsFalse(lessons.GoodPick(LessonDirector.BadPick), "Concrete Bloom's key clashes with Neon Tides");
                yield return Next();

                // SORT: BPM -> KEY, then a song that fits both ways (Low Orbit, 128 BPM, 9A)
                browser.sortButton.SimulatePress();
                yield return Next();
                browser.Select(LessonDirector.BadPick);
                yield return new WaitForSeconds(1f);
                Assert.AreEqual(3, lessons.StepIndex, "a clashing key doesn't pass");
                browser.Select(5);
                yield return Next();

                // The bad pick plays in the headphones, beat-matched, and you listen
                yield return WaitFor(() => B.IsPlaying, 20f, "bad pick playing");
                Assert.AreEqual(LessonDirector.BadPick, B.TrackIndex);
                Assert.AreEqual(KeyMatch.Clash, MixMatch.Keys(A.Track, B.Track));
                Assert.Less(Mathf.Abs(B.BPM - A.BPM), 0.1f, "the lesson matches its tempo so you hear only the key");
                Assert.Less(board.mixer.CrowdGain(1), 0.01f, "the crowd doesn't hear the bad pick");
                board.B.headphones.SimulatePress();
                yield return Next(10f);

                // Swap in your pick with LOAD, then PLAY and SYNC
                Assert.AreEqual(5, browser.Selected, "your pick is highlighted again");
                board.B.load.SimulatePress();
                yield return Next(20f);
                Assert.AreEqual(5, B.TrackIndex);
                board.B.play.SimulatePress();
                yield return WaitFor(() => B.IsPlaying, 5f, "B playing");
                board.B.sync.SimulatePress();
                yield return Next(10f);

                // Mix it in for the cheer
                int before = cheers;
                board.B.volume.SetValue(1f);
                board.crossfader.SetValue(1f);
                yield return WaitFor(() => lessons.Finished, 6f, "lesson finished");
                Assert.Greater(cheers, before, "the crowd cheers your pick");
                Assert.IsTrue(LessonDirector.IsDone(3));
            }
            finally { Sfx.Cheered -= onCheer; }
        }
    }
}
