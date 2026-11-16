using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // PICK PLAYER (#49) with a fingertip: the tab on the start menu, add a player, switch, and progress follows the player.
    public class PickPlayerTest
    {
        Pointer finger;

        static void ClearExtraPlayers()
        {
            foreach (var p in Players.All.ToArray()) if (p.id != 0) Players.Remove(p.id);
            Players.ResetAll();
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            ClearExtraPlayers();
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
            Pointers.Remove(finger);
            ClearExtraPlayers();
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
        public IEnumerator AddSwitchAndKeepProgressApart()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var system = Object.FindAnyObjectByType<SystemButtons>(FindObjectsInactive.Include);
            string firstName = Players.Current.name;

            // The current player shows on the start menu, and only there
            Assert.IsTrue(menu.playerButton.gameObject.activeInHierarchy);
            StringAssert.Contains(firstName, menu.playerLabel.text);

            yield return Poke(menu.playerButton);
            Assert.AreEqual(GameMode.ChoosePlayer, game.Mode);
            Assert.IsFalse(menu.playerButton.gameObject.activeInHierarchy, "the tab hides on the other screens");
            Assert.AreEqual(firstName, menu.labels[0].text);
            Assert.IsTrue(menu.buttons[0].Lit, "the current player is lit");
            Assert.AreEqual("+ NEW PLAYER", menu.labels[1].text);
            Assert.AreEqual("BACK", menu.labels[4].text);

            // BACK goes home without changing anything
            yield return Poke(menu.buttons[4]);
            Assert.AreEqual(GameMode.Title, game.Mode);
            Assert.AreEqual(1, Players.All.Count);

            // + NEW PLAYER adds one, switches to them and goes back to the start menu
            bool firstHadBest = Players.BestSet > 0;
            yield return Poke(menu.playerButton);
            yield return Poke(menu.buttons[1]);
            Assert.AreEqual(GameMode.Title, game.Mode);
            Assert.AreEqual(2, Players.All.Count);
            string newName = Players.Current.name;
            Assert.AreNotEqual(firstName, newName);
            StringAssert.Contains(newName, menu.playerLabel.text);
            Assert.AreEqual(0, Players.BestSet, "a new player starts with no best set");

            // The new player picks BEGINNER; the first player's level doesn't change
            var firstLevel = (SkillLevel)PlayerPrefs.GetInt(SkillLevels.PrefKey, (int)SkillLevel.Intermediate);
            menu.buttons[0].SimulatePress(); // LEARN
            yield return Poke(menu.buttons[(int)SkillLevel.Beginner]);
            Assert.AreEqual(SkillLevel.Beginner, Skill.Current);
            system.home.SimulatePress();

            // Switch back to the first player: their level comes back
            yield return Poke(menu.playerButton);
            Assert.IsTrue(menu.buttons[1].Lit, "now the new player is the lit one");
            yield return Poke(menu.buttons[0]);
            Assert.AreEqual(0, Players.CurrentId);
            Assert.AreEqual(firstLevel, Skill.Current);
            Assert.AreEqual(firstHadBest, Players.BestSet > 0);
            StringAssert.Contains(firstName, menu.playerLabel.text);
        }
    }
}
