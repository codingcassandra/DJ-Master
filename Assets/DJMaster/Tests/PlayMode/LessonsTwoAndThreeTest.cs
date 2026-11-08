using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Plays Learn lessons 2 (Fade & Cut) and 3 (Sync, Loops & FX) start to finish, setting controls directly.
    public class LessonsTwoAndThreeTest
    {
        LessonDirector lessons;
        DJBoard board;
        int step;

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
            lessons = Object.FindAnyObjectByType<LessonDirector>();
            board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            step = 0;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        IEnumerator Next(float seconds = 6f)
        {
            step++;
            yield return WaitFor(() => lessons.StepIndex == step || lessons.Finished, seconds, $"step {step} ({lessons.Current?.title}) {lessons.Feedback}");
        }

        static bool OnPhraseLine(Deck d)
        {
            double bar = d.Beat / TrackSynth.BeatsPerBar;
            return bar % 1.0 < 0.03 && (int)bar % 4 == 0;
        }

        IEnumerator StartLesson(int lesson)
        {
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[0].SimulatePress(); // LEARN
            menu.buttons[0].SimulatePress(); // MY ROOM
            menu.buttons[lesson].SimulatePress();
            yield return WaitFor(() => lessons.Running && board.A.deck.IsPlaying && board.B.deck.IsLoaded, 20f, "lesson start");
            Assert.AreEqual(lesson, lessons.LessonIndex);
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator FadeAndCut()
        {
            yield return StartLesson(1);
            var A = board.A; var B = board.B;

            B.headphones.SimulatePress(); yield return Next();
            B.play.SimulatePress(); yield return Next();
            B.cue.SimulatePress(); yield return Next();
            board.crossfader.SetValue(0.5f); B.volume.SetValue(1f); yield return Next();
            A.volume.SetValue(0.65f); yield return Next();

            // Too fast first: the lesson makes you go back and fade slower.
            A.volume.SetValue(0.05f);
            yield return null; yield return null;
            StringAssert.Contains("Too fast", lessons.Feedback);
            A.volume.SetValue(0.65f);
            for (float t = 0; t < 7f; t += Time.deltaTime) { A.volume.SetValue(Mathf.Lerp(0.65f, 0.1f, t / 7f)); yield return null; }
            yield return Next();

            yield return WaitFor(() => OnPhraseLine(A.deck), 10f, "phrase line on A");
            B.play.SimulatePress(); yield return Next();
            A.volume.SetValue(0f); A.play.SimulatePress(); yield return Next();

            A.load.SimulatePress(); yield return Next(20f);
            board.crossfader.SetValue(1f); A.volume.SetValue(1f); yield return Next();
            A.hotCues[0].SimulatePress(); yield return Next();

            yield return WaitFor(() => OnPhraseLine(B.deck), 10f, "phrase line on B");
            A.play.SimulatePress(); board.crossfader.SetValue(0f); yield return Next();
            B.play.SimulatePress();
            yield return WaitFor(() => lessons.Finished, 6f, "lesson finished");
            Assert.IsTrue(LessonDirector.IsDone(1));
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator SyncLoopsAndFx()
        {
            yield return StartLesson(2);
            var A = board.A; var B = board.B;

            for (int i = 0; i < 5; i++)
            {
                yield return WaitFor(() => A.deck.Beat % 1.0 < 0.03, 3f, "beat");
                board.tap.SimulatePress();
                yield return new WaitForSeconds(0.2f);
            }
            yield return Next();

            B.headphones.SimulatePress(); B.play.SimulatePress(); yield return Next();
            B.sync.SimulatePress(); yield return Next();
            Assert.AreEqual(A.deck.BPM, B.deck.BPM, 0.05f, "SYNC matched the tempo");
            B.volume.SetValue(1f); board.crossfader.SetValue(0.5f); yield return Next();
            yield return Next(25f); // eight bars together
            board.crossfader.SetValue(1f); A.play.SimulatePress(); yield return Next();

            B.loop.SimulatePress(); yield return Next();
            yield return WaitFor(() => B.deck.LoopRepeats >= 1, 12f, "loop repeat");
            B.loop.SimulatePress(); yield return Next();

            B.echo.SimulatePress(); yield return Next();
            B.volume.SetValue(0f); yield return Next();
            B.echo.SimulatePress(); B.volume.SetValue(1f);
            yield return WaitFor(() => lessons.Finished, 6f, "lesson finished");
            Assert.IsTrue(LessonDirector.IsDone(2));
        }
    }
}
