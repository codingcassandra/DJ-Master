using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // #45 with reading time on: a step you finish at once still stays up long enough to read, and a step that's already
    // done when it shows waits for NEXT > instead of flashing past.
    public class ReadingTimeTest
    {
        Pointer finger;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Reading.Enabled = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
            finger = Pointers.Create();
            finger.Active = false;
        }

        [TearDown]
        public void Cleanup()
        {
            Reading.Enabled = false;
            Pointers.Remove(finger);
        }

        static Vector3 AtMetres(Transform t, Vector3 m) => t.TransformPoint(new Vector3(m.x / t.lossyScale.x, m.y / t.lossyScale.y, m.z / t.lossyScale.z));

        IEnumerator Poke(PokeButton b)
        {
            yield return new WaitForSeconds(0.3f);
            finger.DirectPress = false;
            finger.Active = true;
            finger.Position = AtMetres(b.transform, new Vector3(0, 0.015f, 0));
            yield return null; yield return null;
            finger.Position = AtMetres(b.transform, new Vector3(0, -0.01f, 0));
            yield return null; yield return null;
            finger.Position = AtMetres(b.transform, new Vector3(0, 0.03f, 0));
            yield return null;
            finger.Active = false;
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator PromptsStayUpToReadAndInfoStepsWaitForNext()
        {
            var lessons = Object.FindAnyObjectByType<LessonDirector>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[0].SimulatePress(); // LEARN
            menu.buttons[1].SimulatePress(); // INTERMEDIATE
            menu.buttons[2].SimulatePress(); // FESTIVAL: where QA saw the prompts run away
            menu.buttons[0].SimulatePress(); // lesson 1
            yield return WaitFor(() => lessons.Running, 5f, "lesson 1");
            float shown = Time.time; // the first step is up
            yield return WaitFor(() => board.A.deck.IsPlaying, 10f, "lesson 1 playing");

            // An action step done straight away: its text stays up for its reading time
            Assert.AreEqual(0, lessons.StepIndex);
            float readFor = Reading.Seconds(lessons.Current.text);
            board.A.low.SetValue(0.6f);
            yield return WaitFor(() => lessons.StepIndex == 1, readFor + 3f, "on to step 2");
            Assert.GreaterOrEqual(Time.time - shown, readFor - 0.1f, "the first step stayed up long enough to read");

            // Skip to "Welcome to the booth". A quick player pushes the fader up before pressing PLAY, so when
            // "Volume up" shows it's already done: nothing to do but read it, so it waits for NEXT
            for (int i = 0; i < 20 && lessons.Current?.title != "Welcome to the booth"; i++) { lessons.Skip(); yield return null; }
            Assert.AreEqual("Welcome to the booth", lessons.Current.title);
            yield return null;
            board.A.volume.SetValue(1f);
            board.A.play.SimulatePress();
            yield return WaitFor(() => lessons.Current?.title == "Volume up", 20f, "Volume up");
            yield return null; yield return null;
            Assert.IsTrue(lessons.WaitingForNext, "already done when it showed: wait for NEXT");
            yield return null;
            var next = panel.actionButtons[3];
            Assert.IsTrue(next.gameObject.activeInHierarchy, "NEXT > is on the panel");
            Assert.AreEqual("NEXT >", panel.actionLabels[3].text);
            Assert.IsTrue(next.Lit);
            int step = lessons.StepIndex;
            yield return new WaitForSeconds(Reading.Seconds(lessons.Current.text) + 1f);
            Assert.AreEqual(step, lessons.StepIndex, "it doesn't move on by itself");

            yield return Poke(next);
            Assert.AreEqual(step + 1, lessons.StepIndex, "NEXT moves on");
            yield return null; yield return null;
            Assert.IsFalse(lessons.WaitingForNext && lessons.StepIndex == step);
        }
    }
}
