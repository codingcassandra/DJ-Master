using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // HOME works from every mode, BACK goes one screen back on every menu screen, and the COLOR-BLIND button
    // in the corner works from inside the booth.
    public class NavigationTest
    {
        GameDirector game;
        StartMenu menu;
        InstructionPanel panel;
        SetBuilderPanel builder;
        VocabPanel vocab;
        SystemButtons system;
        DJBoard board;
        Pointer finger;

        // A spot next to a control, with the offset in real metres like a real fingertip (the board is drawn scaled)
        static Vector3 AtMetres(Transform t, Vector3 m) => t.TransformPoint(new Vector3(m.x / t.lossyScale.x, m.y / t.lossyScale.y, m.z / t.lossyScale.z));

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
            game = Object.FindAnyObjectByType<GameDirector>();
            menu = Object.FindAnyObjectByType<StartMenu>(FindObjectsInactive.Include);
            panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            builder = Object.FindAnyObjectByType<SetBuilderPanel>(FindObjectsInactive.Include);
            vocab = Object.FindAnyObjectByType<VocabPanel>(FindObjectsInactive.Include);
            system = Object.FindAnyObjectByType<SystemButtons>(FindObjectsInactive.Include);
            board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            finger = Pointers.Create();
            finger.Active = false;
        }

        [TearDown]
        public void Cleanup()
        {
            Pointers.Remove(finger);
            BoardColors.SetColorSafe(false, save: false);
        }

        IEnumerator Poke(PokeButton b)
        {
            yield return new WaitForSeconds(0.3f); // button debounce
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

        // Presses HOME with a fingertip and checks we're back on a clean start menu.
        IEnumerator PressHome(string from)
        {
            Assert.IsTrue(system.gameObject.activeInHierarchy && system.home.gameObject.activeInHierarchy, $"HOME should show in {from}");
            yield return Poke(system.home);
            Assert.AreEqual(GameMode.Title, game.Mode, $"HOME from {from} should land on the start menu");
            Assert.IsTrue(menu.gameObject.activeInHierarchy);
            Assert.IsFalse(board.gameObject.activeInHierarchy);
            Assert.IsFalse(board.A.deck.IsPlaying || board.B.deck.IsPlaying, $"HOME from {from} should pause the decks");
            Assert.IsFalse(system.home.gameObject.activeInHierarchy, "no HOME on the start menu itself");
            Assert.IsTrue(system.colorBlind.gameObject.activeInHierarchy, "COLOR-BLIND stays on the start menu");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator HomeWorksFromEveryMode()
        {
            // Learn, in the middle of lesson 1
            menu.buttons[0].SimulatePress(); // LEARN
            menu.buttons[1].SimulatePress(); // CLUB
            menu.buttons[0].SimulatePress(); // lesson 1
            Assert.AreEqual(GameMode.Learn, game.Mode);
            yield return null;
            yield return PressHome("Learn");
            Assert.IsNull(Object.FindAnyObjectByType<LessonDirector>().Current, "going home stops the lesson");

            // Freestyle, with a song playing
            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.Freestyle, game.Mode);
            yield return WaitFor(() => board.A.deck.IsLoaded, 20f, "deck A loaded");
            board.A.play.SimulatePress();
            Assert.IsTrue(board.A.deck.IsPlaying);
            yield return PressHome("Freestyle");

            // Build a set
            menu.buttons[1].SimulatePress(); // BUILD A SET
            menu.buttons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.Build, game.Mode);
            yield return null;
            Assert.AreEqual(builder.corner, system.transform.parent, "the tab moves onto the set builder");
            yield return PressHome("Build a set");

            // Live, then the results screen
            menu.buttons[1].SimulatePress();
            menu.buttons[1].SimulatePress();
            builder.clear.SimulatePress();
            builder.crateButtons[0].SimulatePress();
            builder.crateButtons[1].SimulatePress();
            builder.crateButtons[3].SimulatePress();
            builder.goLive.SimulatePress();
            Assert.AreEqual(GameMode.Live, game.Mode);
            Assert.IsFalse(panel.actionButtons[0].gameObject.activeSelf, "HOME in the corner replaces the old MENU button");
            yield return null;
            yield return PressHome("Live");
            Assert.IsFalse(Object.FindAnyObjectByType<SetPerformer>().Running, "going home stops the set");

            menu.buttons[1].SimulatePress();
            menu.buttons[1].SimulatePress();
            builder.goLive.SimulatePress();
            yield return null;
            panel.actionButtons[2].SimulatePress(); // END SET
            yield return WaitFor(() => game.Mode == GameMode.LiveResults, 5f, "results screen");
            panel.actionButtons[3].SimulatePress(); // LISTEN BACK
            yield return PressHome("the results screen");
            Assert.IsFalse(Object.FindAnyObjectByType<MixRecorder>().Replaying, "going home stops the replay");

            // DJ VOCAB, mid quiz
            menu.buttons[3].SimulatePress();
            Assert.AreEqual(GameMode.Vocab, game.Mode);
            vocab.quizButton.SimulatePress();
            yield return null;
            yield return PressHome("DJ VOCAB");
        }

        [UnityTest]
        public IEnumerator BackGoesOneScreenBack()
        {
            // Venue picker -> start menu
            menu.buttons[0].SimulatePress(); // LEARN
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            Assert.AreEqual("BACK", menu.labels[3].text);
            menu.buttons[3].SimulatePress();
            Assert.AreEqual(GameMode.Title, game.Mode);

            // Lesson list -> venue picker, still for LEARN
            menu.buttons[0].SimulatePress(); // LEARN
            menu.buttons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.ChooseLesson, game.Mode);
            Assert.AreEqual("BACK", menu.labels[3].text);
            menu.buttons[3].SimulatePress();
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            Assert.AreEqual(GameMode.Learn, game.ChosenMode);

            // Set builder -> venue picker, still for BUILD A SET
            menu.buttons[3].SimulatePress(); // BACK to the start menu
            menu.buttons[1].SimulatePress(); // BUILD A SET
            menu.buttons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.Build, game.Mode);
            builder.back.SimulatePress();
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            Assert.AreEqual(GameMode.Build, game.ChosenMode);

            // DJ VOCAB -> start menu
            menu.buttons[3].SimulatePress();
            menu.buttons[3].SimulatePress(); // DJ VOCAB
            Assert.AreEqual(GameMode.Vocab, game.Mode);
            yield return Poke(vocab.menuButton);
            Assert.AreEqual(GameMode.Title, game.Mode);
        }

        [UnityTest]
        public IEnumerator ColorBlindButtonWorksFromTheBooth()
        {
            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[0].SimulatePress(); // MY ROOM
            yield return null;
            Assert.IsFalse(BoardColors.ColorSafe);
            Assert.AreEqual(panel.corner, system.transform.parent, "the tab sits on the instruction panel in the booth");
            Color normalPlay = board.A.play.onColor;

            yield return Poke(system.colorBlind);
            Assert.IsTrue(BoardColors.ColorSafe);
            Assert.IsTrue(system.colorBlind.Lit);
            Assert.AreEqual("COLOR-BLIND: ON", system.colorBlindLabel.text);
            Assert.AreNotEqual(normalPlay, board.A.play.onColor, "the board switches palettes right away");

            // Same setting on the start menu, and it switches back from there
            yield return Poke(system.home);
            Assert.AreEqual(menu.corner, system.transform.parent);
            Assert.IsTrue(system.colorBlind.Lit);
            yield return Poke(system.colorBlind);
            Assert.IsFalse(BoardColors.ColorSafe);
            Assert.AreEqual(normalPlay, board.A.play.onColor);
            Assert.AreEqual("COLOR-BLIND: OFF", system.colorBlindLabel.text);
        }
    }
}
