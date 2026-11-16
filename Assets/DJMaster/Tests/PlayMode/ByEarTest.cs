using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // #21: BY EAR in ADVANCED hides the numbers and lights, and the ear drill scores how close you got
    public class ByEarTest
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

        [TearDown]
        public void Cleanup()
        {
            ByEar.Enabled = false;
            Skill.Reset();
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator ToggleHidesTheNumbers_DrillScoresYou()
        {
            ByEar.Enabled = false;
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var drill = Object.FindAnyObjectByType<EarDrill>();
            var A = board.A.deck;
            var B = board.B.deck;

            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[2].SimulatePress(); // ADVANCED
            menu.buttons[1].SimulatePress(); // CLUB
            yield return null;
            Assert.AreEqual(GameMode.Freestyle, game.Mode);
            Assert.AreEqual("BY EAR: OFF", panel.actionLabels[0].text);

            panel.actionButtons[0].SimulatePress(); // BY EAR on
            Assert.IsTrue(ByEar.Active);
            Assert.AreEqual("BY EAR: ON", panel.actionLabels[0].text);

            panel.actionButtons[1].SimulatePress(); // EAR DRILL
            Assert.AreEqual(GameMode.EarDrill, game.Mode);
            yield return WaitFor(() => A.IsPlaying && B.IsPlaying, 25f, "both decks playing");
            yield return null;
            Assert.Greater(Mathf.Abs(B.BPM - A.BPM), 2f, "Deck B starts at a different tempo");
            Assert.IsTrue(board.mixer.headphoneCue[1], "in your PHONES");
            Assert.Less(board.mixer.CrowdGain(1), 0.01f, "the crowd doesn't hear the practice deck");

            // No numbers, no lights
            var displays = Object.FindObjectsByType<DeckDisplay>();
            Assert.IsTrue(displays.All(d => d.bpm.text == "BY EAR"), "the BPM is hidden");
            Assert.IsFalse(board.B.sync.Lit);
            StringAssert.StartsWith("BY EAR", Object.FindAnyObjectByType<SyncMeter>().label.text);

            panel.actionButtons[0].SimulatePress(); // DONE, way off
            Assert.IsTrue(drill.HasResult);
            Assert.AreNotEqual(ByEar.Grade.Perfect, drill.LastGrade);
            yield return null;
            StringAssert.Contains("BPM off by", panel.body.text.Replace("\n", " "));

            // Match it the way the fader and jog wheel would
            board.B.tempo.SetValue(DJMath.TempoFaderFor(A.BPM / B.Track.bpm));
            yield return null;
            B.AlignTo(A);
            yield return null;
            panel.actionButtons[0].SimulatePress(); // DONE
            Assert.AreEqual(ByEar.Grade.Perfect, drill.LastGrade, $"{drill.BpmOff} BPM, {drill.MsOff} ms");
            Assert.AreEqual(2, drill.Rounds);
            yield return null;
            StringAssert.Contains("PERFECT", panel.body.text);

            panel.actionButtons[1].SimulatePress(); // NEW TEMPO
            yield return null; yield return null;
            Assert.IsFalse(drill.HasResult);
            Assert.Greater(Mathf.Abs(B.BPM - A.BPM), 2f, "a fresh tempo to find");

            panel.actionButtons[2].SimulatePress(); // back to FREESTYLE
            Assert.AreEqual(GameMode.Freestyle, game.Mode);
            Assert.IsFalse(drill.Running);
        }
    }
}
