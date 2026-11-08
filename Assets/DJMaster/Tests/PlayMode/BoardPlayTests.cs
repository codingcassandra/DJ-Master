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
    }
}
