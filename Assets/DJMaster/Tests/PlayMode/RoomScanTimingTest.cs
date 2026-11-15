using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // DeskSnap asks for the room scan when the booth opens in MY ROOM. These check the timing it relies on:
    // the game starts on the menu screen, picking a venue doesn't count, and stepping into the booth does, once.
    public class RoomScanTimingTest
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

        [UnityTest]
        public IEnumerator BoothOpensOnlyAfterPickingAVenue()
        {
            var env = Object.FindAnyObjectByType<EnvironmentManager>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            Assert.IsTrue(env.MenuScreen, "cold start is on the menu screen, so no room scan yet");

            var events = new List<bool>();
            env.MenuScreenChanged += events.Add;

            menu.buttons[2].SimulatePress(); // FREESTYLE
            Assert.IsEmpty(events, "the venue picker is still the menu screen");
            StringAssert.Contains("room scan", menu.descriptions[0].text, "MY ROOM says why we'll ask");

            menu.buttons[0].SimulatePress(); // MY ROOM
            yield return null;
            CollectionAssert.AreEqual(new[] { false }, events, "stepping into the booth fires once");
            Assert.AreEqual(EnvMode.Room, env.Mode);
            env.MenuScreenChanged -= events.Add;
        }
    }
}
