using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Loads the real scene and drives the board with a simulated fingertip.
    public class BoardPlayTests
    {
        Pointer finger;

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
            finger = Pointers.Create();
            finger.Active = false;
        }

        [TearDown]
        public void Cleanup() => Pointers.Remove(finger);

        // Freestyle in your room: the booth with nothing else going on.
        static IEnumerator EnterBooth()
        {
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[0].SimulatePress(); // MY ROOM
            yield return null;
        }

        IEnumerator Poke(PokeButton b)
        {
            finger.DirectPress = false;
            finger.Active = true;
            finger.Position = b.transform.TransformPoint(new Vector3(0, 0.015f, 0));
            yield return null; yield return null;
            finger.Position = b.transform.TransformPoint(new Vector3(0, -0.01f, 0));
            yield return null; yield return null;
            finger.Position = b.transform.TransformPoint(new Vector3(0, 0.03f, 0));
            yield return null;
            finger.Active = false;
            yield return null;
        }

        IEnumerator Drag(Fader f, float from, float to)
        {
            finger.DirectPress = false;
            Vector3 Along(float v) => f.horizontal ? new Vector3((v - 0.5f) * f.length, 0.005f, 0) : new Vector3(0, 0.005f, (v - 0.5f) * f.length);
            finger.Active = true;
            for (int i = 0; i <= 20; i++)
            {
                finger.Position = f.transform.TransformPoint(Along(Mathf.Lerp(from, to, i / 20f)));
                yield return null;
            }
            finger.Active = false;
            yield return null;
        }

        IEnumerator WaitFor(System.Func<bool> cond, float seconds)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator PokingPlayStartsTheDeck()
        {
            yield return EnterBooth();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            Assert.IsNotNull(board);
            var deck = board.A.deck;
            yield return WaitFor(() => deck.IsLoaded, 20f);
            Assert.IsTrue(deck.IsLoaded, "track should finish rendering");
            Assert.IsFalse(deck.IsPlaying);

            yield return Poke(board.A.play);
            Assert.IsTrue(deck.IsPlaying, "a poke from above should press PLAY");

            // Sliding a finger in from the side must not press it.
            finger.Active = true;
            finger.Position = board.A.play.transform.TransformPoint(new Vector3(0.05f, -0.01f, 0));
            yield return null;
            finger.Position = board.A.play.transform.TransformPoint(new Vector3(0f, -0.01f, 0));
            yield return null; yield return null;
            finger.Active = false;
            yield return null;
            Assert.IsTrue(deck.IsPlaying, "brushing in from the side should not toggle PLAY");
        }

        // Poking a venue closes the menu in the same frame. The finger must not stay owned by the hidden button.
        [UnityTest]
        public IEnumerator FingerStillWorksAfterPokingThroughTheMenu()
        {
            var menu = Object.FindAnyObjectByType<StartMenu>();
            yield return Poke(menu.buttons[2]); // FREESTYLE
            yield return Poke(menu.buttons[0]); // MY ROOM
            Assert.IsNull(finger.Owner);
            var board = Object.FindAnyObjectByType<DJBoard>();
            yield return Drag(board.A.volume, 0f, 1f);
            Assert.Greater(board.A.volume.value, 0.9f);
        }

        [UnityTest]
        public IEnumerator VocabListAndQuiz()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            yield return Poke(menu.buttons[3]); // DJ VOCAB
            Assert.AreEqual(GameMode.Vocab, game.Mode);
            Assert.IsFalse(menu.gameObject.activeInHierarchy);
            var vocab = Object.FindAnyObjectByType<VocabPanel>();
            Assert.IsTrue(vocab.listRoot.activeSelf);
            StringAssert.Contains("BPM", vocab.entries[0].text);

            vocab.Seed(7);
            yield return Poke(vocab.quizButton); // QUIZ ME
            Assert.IsTrue(vocab.Quizzing);
            Assert.IsFalse(vocab.listRoot.activeSelf);
            Assert.IsFalse(vocab.next.gameObject.activeSelf, "NEXT waits for an answer");

            for (int q = 0; q < VocabPanel.QuizLength; q++)
            {
                // Right on even questions, wrong on odd ones.
                int pick = q % 2 == 0 ? vocab.CorrectChoice : (vocab.CorrectChoice + 1) % 4;
                yield return new WaitForSeconds(0.3f); // button debounce
                yield return Poke(vocab.answers[pick]);
                Assert.IsTrue(vocab.Answered);
                Assert.IsTrue(vocab.answers[vocab.CorrectChoice].Lit, "the right word lights up");
                yield return new WaitForSeconds(0.3f);
                yield return Poke(vocab.next);
            }
            Assert.IsTrue(vocab.Finished);
            Assert.AreEqual(VocabPanel.QuizLength / 2, vocab.Score);
            StringAssert.Contains($"{VocabPanel.QuizLength / 2} out of {VocabPanel.QuizLength}", vocab.question.text);

            yield return Poke(vocab.menuButton);
            Assert.AreEqual(GameMode.Title, game.Mode);
            Assert.IsFalse(vocab.gameObject.activeInHierarchy);
        }

        // Every free song must be exactly 16 bars at its BPM, or the beat grid, hot cues and SYNC drift.
        [UnityTest]
        public IEnumerator FreeMusicLoadsOnTheBeatGrid()
        {
            yield return EnterBooth();
            var deck = Object.FindAnyObjectByType<DJBoard>().A.deck;
            int checkedCount = 0;
            for (int i = 0; i < TrackLibrary.Tracks.Length; i++)
            {
                var t = TrackLibrary.Tracks[i];
                if (!t.IsRecording) continue;
                deck.Load(i);
                float until = Time.realtimeSinceStartup + 10f;
                while (!deck.IsLoaded && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(deck.IsLoaded, $"{t.title} never loaded");
                var clip = deck.Source.clip;
                Assert.AreEqual(2, clip.channels, t.title);
                double expected = TrackSynth.TotalBeats * 60.0 / t.bpm * clip.frequency;
                Assert.AreEqual(expected, clip.samples, 2.0, $"{t.title}: {clip.samples} samples at {clip.frequency} Hz");
                deck.SeekBeat(16);
                Assert.AreEqual(4, deck.Bar, t.title);
                checkedCount++;
            }
            Assert.AreEqual(TrackLibrary.Tracks.Length - TrackLibrary.GameTrackCount, checkedCount);
        }

        [UnityTest]
        public IEnumerator FreeMusicTabAddsRecordingsToTheSet()
        {
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[1].SimulatePress(); // BUILD A SET
            menu.buttons[0].SimulatePress(); // MY ROOM
            yield return null;
            var builder = Object.FindAnyObjectByType<SetBuilderPanel>();
            builder.Set.Clear();
            builder.freeTab.SimulatePress();
            Assert.AreEqual(1, builder.Page);
            builder.crateButtons[0].SimulatePress();
            Assert.AreEqual(TrackLibrary.GameTrackCount, builder.Set.songs[0]);
            Assert.IsTrue(TrackLibrary.Tracks[builder.Set.songs[0]].IsRecording);
            StringAssert.Contains(TrackLibrary.Tracks[TrackLibrary.GameTrackCount].title, builder.crateLabels[0].text);
            builder.gameTab.SimulatePress();
            Assert.AreEqual(0, builder.Page);
            builder.Set.Clear();
        }

        [UnityTest]
        public IEnumerator FaderFollowsFingerAndDrivesMixer()
        {
            yield return EnterBooth();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var mixer = board.mixer;
            Assert.AreEqual(0f, board.A.volume.value, 1e-4f);
            yield return Drag(board.A.volume, 0f, 1f);
            Assert.Greater(board.A.volume.value, 0.9f);
            Assert.Greater(mixer.channelFader[0], 0.9f);

            yield return Drag(board.crossfader, 0.5f, 0f);
            Assert.Less(board.crossfader.value, 0.1f);
        }

        [UnityTest]
        public IEnumerator TempoFaderIsFineGrained()
        {
            yield return EnterBooth();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var tempo = board.B.tempo;
            yield return Drag(tempo, 0.5f, 1f);
            // Finger moved half the slot; with the 0.35 drag ratio the cap moves much less.
            Assert.Greater(tempo.value, 0.6f);
            Assert.Less(tempo.value, 0.75f);
        }

        [UnityTest]
        public IEnumerator MenuLeadsThroughVenueToTheColorQuiz()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var lessons = Object.FindAnyObjectByType<LessonDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var env = Object.FindAnyObjectByType<EnvironmentManager>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            Assert.AreEqual(GameMode.Title, game.Mode);
            Assert.IsTrue(env.MenuScreen, "the start screen is its own black space");
            Assert.IsFalse(board.gameObject.activeSelf, "the booth is hidden on the start screen");

            yield return Poke(menu.buttons[0]); // LEARN
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            yield return new WaitForSeconds(0.3f);
            yield return Poke(menu.buttons[2]); // FESTIVAL
            Assert.AreEqual(GameMode.ChooseLesson, game.Mode);
            Assert.AreEqual(EnvMode.Festival, env.Mode);
            yield return new WaitForSeconds(0.3f);
            yield return Poke(menu.buttons[0]); // 1 FIRST MIX
            Assert.AreEqual(GameMode.Learn, game.Mode);
            Assert.IsFalse(env.MenuScreen);
            Assert.IsTrue(board.gameObject.activeSelf, "the booth appears once you pick a lesson");
            Assert.AreEqual(0, lessons.StepIndex);

            // Wrong color: told what it was, no progress.
            board.B.low.SimulateTouch();
            yield return null;
            StringAssert.Contains("PINK", lessons.Feedback);
            Assert.AreEqual(0, lessons.StepIndex);

            board.A.low.SimulateTouch();
            yield return WaitFor(() => lessons.StepIndex == 1, 4f);
            Assert.AreEqual(1, lessons.StepIndex);
        }

        [UnityTest]
        public IEnumerator EnvironmentButtonsSwitchVenues()
        {
            yield return EnterBooth();
            var env = Object.FindAnyObjectByType<EnvironmentManager>();
            env.clubButton.SimulatePress();
            yield return null;
            Assert.AreEqual(EnvMode.Club, env.Mode);
            Assert.IsTrue(env.club.root.activeSelf);
            Assert.IsFalse(env.festival.root.activeSelf);
            env.festivalButton.SimulatePress();
            yield return null;
            Assert.IsTrue(env.festival.root.activeSelf);
            env.roomButton.SimulatePress();
            yield return null;
            Assert.IsFalse(env.club.root.activeSelf);
            Assert.AreEqual(CameraClearFlags.SolidColor, env.cam.clearFlags);
        }

        [UnityTest]
        public IEnumerator SlowPokePressesOnce()
        {
            yield return EnterBooth();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var deck = board.A.deck;
            yield return WaitFor(() => deck.IsLoaded, 20f);
            Assert.IsFalse(deck.IsPlaying);

            // A real fingertip moves about a millimetre a frame on a careful poke, so it spends frames
            // between the surface and the press depth.
            finger.DirectPress = false;
            finger.Active = true;
            for (int mm = 15; mm >= -10; mm--)
            {
                finger.Position = board.A.play.transform.TransformPoint(new Vector3(0, mm * 0.001f, 0));
                yield return null;
            }
            Assert.IsTrue(deck.IsPlaying, "a slow poke from above should press PLAY");

            // Wobbling while still pushed in (as tracked fingers do) must not press it again.
            float end = Time.time + 0.6f;
            for (int i = 0; Time.time < end; i++)
            {
                finger.Position = board.A.play.transform.TransformPoint(new Vector3(0, i % 2 == 0 ? -0.002f : -0.009f, 0));
                yield return null;
            }
            finger.Active = false;
            yield return null;
            Assert.IsTrue(deck.IsPlaying, "wobbling inside the button should not toggle PLAY again");
        }

        [UnityTest]
        public IEnumerator KnobIgnoresTrackingBlipButDoubleTapStillResets()
        {
            yield return EnterBooth();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var knob = board.A.low;
            Vector3 At(float y, float z) => knob.transform.TransformPoint(new Vector3(0, y, z));

            // Turn the LOW down.
            finger.DirectPress = false;
            finger.Active = true;
            finger.Position = At(0.005f, 0);
            yield return null; yield return null;
            for (int i = 1; i <= 10; i++) { finger.Position = At(0.005f, -0.002f * i); yield return null; }
            float turned = knob.value;
            Assert.Less(turned, 0.3f, "sliding toward you should turn the knob down");

            // Hand tracking drops out for one frame, then comes back on the same spot.
            finger.Active = false;
            yield return null;
            finger.Active = true;
            yield return null; yield return null;
            Assert.AreEqual(turned, knob.value, 0.01f, "a tracking blip must not reset the knob to center");

            // Keep turning, then lift off: that's not a tap.
            for (int i = 1; i <= 5; i++) { finger.Position = At(0.005f, -0.02f + 0.002f * i); yield return null; }
            finger.Position = At(0.06f, 0);
            yield return new WaitForSeconds(0.4f);
            Assert.AreNotEqual(0.5f, knob.value, "turning and lifting off should leave the knob where it is");

            // A real double-tap still resets it.
            finger.Position = At(0.005f, 0); yield return null; yield return null;
            finger.Position = At(0.06f, 0); yield return null;
            finger.Position = At(0.005f, 0); yield return null; yield return null;
            Assert.AreEqual(0.5f, knob.value, 0.001f, "a double-tap should reset the knob to center");
            finger.Active = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WigglingTheCrossfaderScoresOneTransition()
        {
            yield return EnterBooth();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var judge = Object.FindAnyObjectByType<MixJudge>();
            yield return WaitFor(() => board.A.deck.IsLoaded && board.B.deck.IsLoaded, 20f);

            board.A.volume.SetValue(1f);
            board.B.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.A.deck.Play();
            board.B.deck.Play();
            yield return null;
            board.Sync(1);
            yield return WaitFor(() => judge.InSync, 3f);
            Assert.IsTrue(judge.InSync, "SYNC should line the two decks up");

            yield return null;
            judge.ResetScore();
            for (int i = 0; i < 6; i++)
            {
                board.crossfader.SetValue(i % 2 == 0 ? 1f : 0f);
                yield return null; yield return null;
            }
            Assert.AreEqual(1, judge.Transitions, "the same two songs should only score one transition");
        }

        [UnityTest]
        public IEnumerator ColorBlindToggle_RecolorsBoardAndAddsShapes_ThenRestores()
        {
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            if (BoardColors.ColorSafe) { menu.buttons[4].SimulatePress(); yield return null; } // start from the normal look

            Color normalPlay = board.A.play.onColor;
            var playLabel = board.A.play.GetComponentInChildren<TMPro.TextMeshPro>(true);
            Assert.AreEqual("PLAY", playLabel.text);

            menu.buttons[4].SimulatePress(); // COLOR-BLIND on the start screen
            yield return null;
            Assert.IsTrue(BoardColors.ColorSafe);
            Assert.IsTrue(menu.buttons[4].Lit, "the COLOR-BLIND row should light up when it's on");
            Assert.AreNotEqual(normalPlay, board.A.play.onColor, "PLAY should switch to the color-blind palette");
            Assert.AreEqual("● PLAY", playLabel.text);
            Assert.AreEqual("◄ LOAD", board.A.load.GetComponentInChildren<TMPro.TextMeshPro>(true).text);
            foreach (char shape in "◄►●■♦♥")
                Assert.IsTrue(playLabel.font.HasCharacter(shape, searchFallbacks: true, tryAddCharacter: true), $"the label font can't draw {shape}");

            menu.buttons[4].SimulatePress();
            yield return null;
            Assert.IsFalse(BoardColors.ColorSafe);
            Assert.AreEqual(normalPlay, board.A.play.onColor, "turning it off should restore the original colors");
            Assert.AreEqual("PLAY", playLabel.text);
        }
    }
}
