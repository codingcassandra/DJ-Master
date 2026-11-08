using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Builds a three-song set in Build mode, then performs it live: a blend and a quick cut, both done right.
    public class LiveSetTest
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

        static void MatchTempo(DeckControls ch, float bpm) =>
            ch.tempo.SetValue(0.5f + (bpm / ch.deck.Track.bpm - 1f) / (2f * DJMath.TempoRange));

        // What a perfect jog-wheel nudge does: put the incoming deck's kicks exactly on the playing deck's.
        static void LineUp(Deck playing, Deck incoming)
        {
            double target = System.Math.Floor(incoming.Beat) + playing.Beat % 1.0 + 0.01;
            incoming.Source.timeSamples = (int)(target * 60.0 / incoming.Track.bpm * incoming.Source.clip.frequency);
        }

        // Just after bar 1, 5, 9 or 13 starts.
        static bool OnPhraseLine(Deck d)
        {
            double bar = d.Beat / TrackSynth.BeatsPerBar;
            return bar % 1.0 < 0.03 && (int)bar % 4 == 0;
        }

        static bool OnBarLine(Deck d) => d.Beat / TrackSynth.BeatsPerBar % 1.0 < 0.03;

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator BuildAndPerformASet()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var builder = Object.FindAnyObjectByType<SetBuilderPanel>(FindObjectsInactive.Include);
            var performer = Object.FindAnyObjectByType<SetPerformer>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);

            menu.buttons[1].SimulatePress(); // BUILD A SET
            menu.buttons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.Build, game.Mode);
            Assert.IsTrue(builder.gameObject.activeSelf);
            Assert.IsFalse(panel.gameObject.activeSelf);

            builder.clear.SimulatePress();
            builder.goLive.SimulatePress();
            Assert.AreEqual(GameMode.Build, game.Mode, "can't go live with an empty set");

            builder.styleButtons[(int)TransitionStyle.Blend].SimulatePress();
            builder.crateButtons[0].SimulatePress();                          // Neon Tides, 128
            builder.crateButtons[1].SimulatePress();                          // Velvet Static, 124, blended in
            builder.styleButtons[(int)TransitionStyle.Cut].SimulatePress();
            builder.crateButtons[3].SimulatePress();                          // Sundial, 122, cut in
            CollectionAssert.AreEqual(new[] { TransitionStyle.Blend, TransitionStyle.Cut }, builder.Set.transitions);

            builder.goLive.SimulatePress();
            Assert.AreEqual(GameMode.Live, game.Mode);
            Assert.IsTrue(panel.gameObject.activeSelf);
            var A = board.A; var B = board.B;
            yield return WaitFor(() => A.deck.IsPlaying && B.deck.IsLoaded, 20f, "song 1 playing, song 2 loaded");
            Assert.AreEqual(0, performer.SongIndex);
            Assert.IsTrue(B.play.Highlighted, "first step of a blend: start the next song");

            // ---- Blend into song 2 ----
            B.play.SimulatePress();
            MatchTempo(B, A.deck.BPM);
            yield return null;
            LineUp(A.deck, B.deck);
            B.volume.SetValue(1f);
            yield return WaitFor(() => OnPhraseLine(A.deck), 10f, "phrase line");
            board.crossfader.SetValue(0.5f);                                  // bring it in on the phrase
            yield return new WaitForSeconds(9f);                              // about 5 bars together
            board.crossfader.SetValue(1f);
            yield return WaitFor(() => performer.SongIndex == 1, 3f, "blend complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[0].grade, performer.Results[0].note);

            // ---- Quick cut into song 3, now loading on Deck A ----
            yield return WaitFor(() => A.deck.IsLoaded && A.deck.TrackIndex == 3, 10f, "song 3 loaded on deck A");
            Assert.AreEqual(0f, A.volume.value, "the freed deck is reset");
            MatchTempo(A, B.deck.BPM);
            A.volume.SetValue(1f);
            yield return null;
            // Wait until just after a bar line on song 2, then PLAY and slam the crossfader together.
            yield return WaitFor(() => OnPhraseLine(B.deck), 10f, "phrase line");
            A.play.SimulatePress();
            board.crossfader.SetValue(0f);
            yield return WaitFor(() => performer.SongIndex == 2, 3f, "cut complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[1].grade, performer.Results[1].note);
            Assert.IsTrue(performer.InFinale);

            panel.actionButtons[2].SimulatePress(); // END SET
            Assert.AreEqual(GameMode.LiveResults, game.Mode);
            Assert.AreEqual(3, SetPerformer.Stars(performer.Results));
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator FadeAndEchoOutAreGradedAndRecorded()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var builder = Object.FindAnyObjectByType<SetBuilderPanel>(FindObjectsInactive.Include);
            var performer = Object.FindAnyObjectByType<SetPerformer>();
            var recorder = Object.FindAnyObjectByType<MixRecorder>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var A = board.A; var B = board.B;

            menu.buttons[1].SimulatePress(); // BUILD A SET
            menu.buttons[0].SimulatePress(); // MY ROOM
            builder.clear.SimulatePress();
            builder.crateButtons[0].SimulatePress();                          // Neon Tides, 128
            builder.styleButtons[(int)TransitionStyle.Fade].SimulatePress();
            builder.crateButtons[3].SimulatePress();                          // Sundial, 122: too far to beatmix, fine to fade
            builder.styleButtons[(int)TransitionStyle.EchoOut].SimulatePress();
            builder.crateButtons[5].SimulatePress();                          // Low Orbit, 128
            builder.goLive.SimulatePress();
            Assert.AreEqual(GameMode.Live, game.Mode);
            yield return WaitFor(() => A.deck.IsPlaying && B.deck.IsLoaded, 20f, "song 1 playing, song 2 loaded");

            // ---- Fade: dip, fade over ~10 seconds, then start the new song on a phrase ----
            board.crossfader.SetValue(0.5f);
            B.volume.SetValue(1f);
            A.volume.SetValue(0.65f);
            for (float t = 0; t < 10f; t += Time.deltaTime)
            {
                A.volume.SetValue(Mathf.Lerp(0.65f, 0.1f, t / 10f));
                yield return null;
            }
            yield return WaitFor(() => OnPhraseLine(A.deck), 10f, "phrase line");
            B.play.SimulatePress();
            yield return WaitFor(() => performer.SongIndex == 1, 3f, "fade complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[0].grade, performer.Results[0].note);

            // ---- Echo out: echo on, cut the fader on a phrase, drop the next song on the next bar ----
            yield return WaitFor(() => A.deck.IsLoaded && A.deck.TrackIndex == 5, 10f, "song 3 loaded");
            A.volume.SetValue(1f);
            B.echo.SimulatePress();
            yield return WaitFor(() => OnPhraseLine(B.deck), 10f, "phrase line");
            B.volume.SetValue(0f);
            yield return new WaitForSeconds(0.5f);
            yield return WaitFor(() => OnBarLine(B.deck), 3f, "next bar");
            A.play.SimulatePress();
            yield return WaitFor(() => performer.SongIndex == 2, 3f, "echo out complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[1].grade, performer.Results[1].note);

            yield return new WaitForSeconds(2.5f); // recordings are saved two seconds after each transition
            panel.actionButtons[2].SimulatePress(); // END SET
            Assert.AreEqual(GameMode.LiveResults, game.Mode);
            Assert.IsNotNull(performer.Results[0].recording, "the fade was recorded");
            Assert.IsNotNull(performer.Results[1].recording, "the echo out was recorded");
            Assert.IsNotEmpty(performer.Review.practise, "a critique is always written");
            panel.actionButtons[3].SimulatePress(); // LISTEN BACK
            Assert.IsTrue(recorder.Replaying);
            Assert.IsFalse(A.deck.IsPlaying || B.deck.IsPlaying, "the decks pause while you listen back");
        }
    }
}
