using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Beatgrids on real music: tempo detection on the free songs, and the tap-the-one screen driven like a hand would.
    public class BeatgridTest
    {
        const int Song = TrackLibrary.GameTrackCount; // Funky House, a real recording that starts on beat 1
        TrackInfo original;

        [UnitySetUp]
        public IEnumerator Load()
        {
            original = TrackLibrary.Tracks[Song];
            BeatGridStore.Forget(original.GridId);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            BeatGridStore.Forget(original.GridId);
            TrackLibrary.Tracks[Song] = original;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        static float PhaseError(float a, float b, float period)
        {
            float d = ((a - b) % period + period) % period;
            return Mathf.Min(d, period - d);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DetectsTheTempoOfEveryFreeSong()
        {
            var deck = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include).A.deck;
            for (int i = TrackLibrary.GameTrackCount; i < TrackLibrary.GameTrackCount + 8; i++)
            {
                var t = TrackLibrary.Tracks[i];
                Assert.IsTrue(t.IsRecording);
                deck.Load(i);
                yield return WaitFor(() => deck.IsLoaded, 10f, t.title);
                var task = BeatGridStore.DetectAsync(deck.Source.clip);
                yield return WaitFor(() => task.IsCompleted, 20f, $"{t.title} analysis");
                var g = task.Result;
                Assert.AreEqual(t.bpm, g.bpm, 1f, $"{t.title} BPM");
                // These excerpts are cut to start on beat 1
                Assert.Less(PhaseError(g.firstBeat, 0f, 60f / t.bpm), 0.035f, $"{t.title}: beat found at {g.firstBeat * 1000:0} ms");
            }
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TapTheOneNudgeAndSave()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var editor = Object.FindAnyObjectByType<GridEditor>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[1].SimulatePress(); // CLUB

            game.EditGrid(Song);
            Assert.AreEqual(GameMode.Grid, game.Mode);
            var deck = board.A.deck;
            yield return WaitFor(() => editor.Grid.bpm > 0, 20f, "beat found");
            Assert.AreEqual(original.bpm, editor.Grid.bpm, 1f);
            Assert.IsTrue(deck.IsPlaying);
            yield return null;
            StringAssert.Contains("TAP", panel.body.text);

            // The detector finds the beats, but not which one is bar 1. LATER moves the grid 10 ms a press
            float before = editor.Grid.firstBeat;
            for (int i = 0; i < 25; i++) panel.actionButtons[1].SimulatePress();
            Assert.AreEqual(0f, PhaseError(editor.Grid.firstBeat, before + 0.25f, editor.Grid.BarSeconds), 1e-3f);

            // Tap the TAP pad when a player hears bar 1 (the song's own bar lines are every 4 beats from 0 s)
            float bar = 4f * 60f / original.bpm;
            yield return WaitFor(() => { float heard = deck.Source.time - LessonDirector.TapLag; return heard > 0.5f && heard % bar < 0.03f; }, 10f, "the one");
            board.tap.SimulatePress();
            Assert.AreEqual(1, editor.Taps);
            Assert.Less(PhaseError(editor.Grid.firstBeat, 0f, bar), 0.04f, $"bar 1 at {editor.Grid.firstBeat * 1000:0} ms");

            panel.actionButtons[3].SimulatePress(); // SAVE
            Assert.AreEqual(GameMode.Freestyle, game.Mode);
            Assert.IsTrue(BeatGridStore.TryLoad(original.GridId, out var saved));
            Assert.AreEqual(editor.Grid.firstBeat, saved.firstBeat, 1e-4f);

            // The decks follow the grid: a hot cue lands on its bar
            deck.HotCue(4);
            Assert.AreEqual(4, deck.Bar);
            Assert.AreEqual(0, deck.BeatInBar);
        }
    }
}
