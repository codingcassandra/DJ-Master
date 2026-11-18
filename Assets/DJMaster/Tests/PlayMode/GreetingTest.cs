using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // The greeting (#50) when the game starts: hi with the player's name, one suggestion, CONTINUE and SWITCH PLAYER.
    public class GreetingTest
    {
        Pointer finger;

        [UnitySetUp]
        public IEnumerator Load()
        {
            foreach (var p in Players.All.ToArray()) if (p.id != 0) Players.Remove(p.id);
            Players.ResetAll();
            GameDirector.GreetOnStart = true;
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
            GameDirector.GreetOnStart = false;
            Pointers.Remove(finger);
        }

        static Vector3 AtMetres(Transform t, Vector3 m) => t.TransformPoint(new Vector3(m.x / t.lossyScale.x, m.y / t.lossyScale.y, m.z / t.lossyScale.z));

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

        [UnityTest]
        public IEnumerator SaysHiThenContinuesOrSwitchesPlayer()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var system = Object.FindAnyObjectByType<SystemButtons>(FindObjectsInactive.Include);

            Assert.AreEqual(GameMode.Greeting, game.Mode, "the game opens with a hello");
            Assert.AreEqual(GameDirector.Greeting(Players.Current.name), menu.title.text);
            StringAssert.StartsWith("HI, ", menu.title.text);
            Assert.IsNotEmpty(menu.subtitle.text, "one suggestion for what's next");
            Assert.AreEqual(GameDirector.Suggestion(), menu.subtitle.text.Replace("\n", " ").Replace(TextUtil.Glue, ' '));
            Assert.AreEqual("CONTINUE", menu.labels[2].text);
            Assert.AreEqual("SWITCH PLAYER", menu.labels[3].text);
            Assert.IsFalse(menu.buttons[0].gameObject.activeSelf || menu.buttons[1].gameObject.activeSelf || menu.buttons[4].gameObject.activeSelf);
            Assert.IsFalse(menu.wordmark.activeSelf || menu.playerButton.gameObject.activeSelf, "its own big hello, no wordmark or tab");
            Assert.IsFalse(system.home.gameObject.activeInHierarchy, "nowhere to go home to yet");
            Assert.IsTrue(game.board.A.deck.IsPlaying, "the menu music is on");

            // SWITCH PLAYER, then BACK lands on the start menu
            yield return Poke(menu.buttons[3]);
            Assert.AreEqual(GameMode.ChoosePlayer, game.Mode);
            yield return Poke(menu.buttons[4]); // BACK
            Assert.AreEqual(GameMode.Title, game.Mode);

            // Picking a player says hi again, and CONTINUE goes to the start menu
            yield return Poke(menu.playerButton);
            yield return Poke(menu.buttons[0]); // the same player again: hi again
            Assert.AreEqual(GameMode.Greeting, game.Mode);
            yield return Poke(menu.buttons[2]); // CONTINUE
            Assert.AreEqual(GameMode.Title, game.Mode);
            Assert.IsTrue(menu.wordmark.activeSelf);
        }
    }
}
