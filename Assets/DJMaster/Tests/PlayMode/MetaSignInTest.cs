using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Meta sign-in (#46): the editor falls back cleanly, and a signed-in name becomes the first player and the greeting.
    public class MetaSignInTest
    {
        string firstName;

        [UnitySetUp]
        public IEnumerator Load()
        {
            firstName = Players.All[0].name;
            GameDirector.GreetOnStart = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            GameDirector.GreetOnStart = false;
            Players.Rename(Players.All[0].id, firstName);
        }

        [UnityTest]
        public IEnumerator EditorFallsBackThenANameUpdatesTheGreeting()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            yield return null; yield return null;
            Assert.IsNotNull(game.SignIn);
            Assert.AreEqual(MetaSignIn.State.Offline, game.SignIn.Status, "no platform in the editor: play on as the local player");
            Assert.AreEqual(GameMode.Greeting, game.Mode);
            Assert.AreEqual(GameDirector.Greeting(Players.Current.name), menu.title.text);

            // What the Quest does once the account answers
            Players.Pick(Players.All[0].id);
            game.SignIn.Apply("Cass");
            Assert.AreEqual(MetaSignIn.State.SignedIn, game.SignIn.Status);
            Assert.AreEqual("Cass", Players.All[0].name, "the signed-in user is the first player");
            Assert.AreEqual("HI, CASS!", menu.title.text, "the greeting updates with the name");
            Assert.AreEqual(GameMode.Greeting, game.Mode);
        }
    }
}
