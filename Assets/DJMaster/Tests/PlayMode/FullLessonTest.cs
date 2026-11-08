using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Plays Learn mode start to finish (setting controls directly) to prove every step can be completed.
    public class FullLessonTest
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
        [Timeout(180000)]
        public IEnumerator CompleteDJSchool()
        {
            var lessons = Object.FindAnyObjectByType<LessonDirector>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var A = board.A.deck;
            var B = board.B.deck;
            menu.buttons[0].SimulatePress(); // LEARN
            menu.buttons[1].SimulatePress(); // CLUB
            menu.buttons[0].SimulatePress(); // lesson 1
            yield return WaitFor(() => lessons.Running && A.IsLoaded, 20f, "lesson start");

            int step = 0;
            IEnumerator Next()
            {
                step++;
                yield return WaitFor(() => lessons.StepIndex == step || lessons.Finished, 6f, $"step {step} ({lessons.Current?.title})");
            }

            // The color code quiz.
            board.A.mid.SimulateTouch(); yield return Next();
            board.B.jog.SimulateTouch(); yield return Next();
            board.B.headphones.SimulatePress(); // pressing a button is a touch too...
            board.B.headphones.SimulateTouch(); yield return Next();
            board.A.cue.SimulateTouch(); yield return Next();
            Assert.IsFalse(board.mixer.headphoneCue[1], "...and the quiz cleans up after itself");
            yield return WaitFor(() => A.IsLoaded, 5f, "deck A ready");

            board.A.play.SimulatePress(); yield return Next();
            board.A.volume.SetValue(1f); yield return Next();
            board.crossfader.SetValue(0f); yield return Next();

            for (int i = 0; i < 4; i++)
            {
                yield return WaitFor(() => DJMath.DistanceToBeat(A.Beat) < 0.05f && A.Beat % 1.0 < 0.5, 3f, "beat");
                board.tap.SimulatePress();
                yield return new WaitForSeconds(0.3f);
            }
            yield return Next();

            yield return WaitFor(() => { double bar = A.Beat / 4; return (int)bar % 4 == 0 && bar % 1.0 < 0.03; }, 12f, "phrase line");
            board.tap.SimulatePress(); yield return Next();

            board.B.load.SimulatePress(); yield return Next();
            board.B.headphones.SimulatePress(); yield return Next();
            board.B.play.SimulatePress(); yield return Next();

            board.B.tempo.SetValue(0.5f + (A.BPM / B.Track.bpm - 1f) / (2f * DJMath.TempoRange));
            yield return null; // the board copies fader values to the deck each frame
            Assert.Less(Mathf.Abs(B.BPM - A.BPM), 0.05f);
            yield return Next();

            // Line the kicks up exactly (what the jog wheel does by hand).
            double frac = A.Beat % 1.0;
            double target = System.Math.Floor(B.Beat) + frac + 0.02;
            B.Source.timeSamples = (int)(target * 60.0 / B.Track.bpm * B.Source.clip.frequency);
            yield return Next();

            board.B.low.SetValue(0f); yield return Next();
            board.B.volume.SetValue(1f); board.crossfader.SetValue(0.5f); yield return Next();
            board.A.low.SetValue(0f); board.B.low.SetValue(0.5f); yield return Next();
            board.crossfader.SetValue(1f); board.A.volume.SetValue(0f); yield return Next();
            board.B.filter.SetValue(0.1f); yield return null; yield return null;
            board.B.filter.SetValue(0.5f); yield return Next();
            board.airHorn.SimulatePress();
            yield return WaitFor(() => lessons.Finished, 6f, "lesson finished");
            Assert.AreEqual(1, PlayerPrefs.GetInt("lessonComplete", 0));
        }
    }
}
