using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // The level chips on the title screen: poked with a fingertip, saved, shown while you play, and each opens its mode.
    public class SkillLevelTest
    {
        Pointer finger;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Skill.Reset();
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
            Skill.Reset();
        }

        IEnumerator Poke(PokeButton b)
        {
            yield return new WaitForSeconds(0.3f); // button debounce
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

        [UnityTest]
        public IEnumerator PickLevelsWithAFingertip()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var menu = Object.FindAnyObjectByType<StartMenu>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>(FindObjectsInactive.Include);
            var system = Object.FindAnyObjectByType<SystemButtons>(FindObjectsInactive.Include);

            Assert.IsTrue(menu.levelRow.activeSelf, "the picker is on the title screen");
            Assert.IsTrue(menu.levelButtons[0].Lit, "BEGINNER until you pick");

            // ADVANCED opens BUILD A SET, and the picker hides on the next screen
            yield return Poke(menu.levelButtons[(int)SkillLevel.Advanced]);
            Assert.AreEqual(SkillLevel.Advanced, Skill.Current);
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            Assert.AreEqual(GameMode.Build, game.ChosenMode);
            Assert.IsFalse(menu.levelRow.activeSelf);
            StringAssert.Contains("Level: ADVANCED", menu.subtitle.text);
            Assert.AreEqual(SkillLevel.Advanced, Skill.Parse(PlayerPrefs.GetInt("skillLevel", -1)), "saved between sessions");

            // Back home, it's still lit
            system.home.SimulatePress();
            Assert.AreEqual(GameMode.Title, game.Mode);
            Assert.IsTrue(menu.levelRow.activeSelf);
            Assert.IsTrue(menu.levelButtons[(int)SkillLevel.Advanced].Lit);
            Assert.IsFalse(menu.levelButtons[0].Lit);

            // SOLO opens FREESTYLE, with the level on the panel
            yield return Poke(menu.levelButtons[(int)SkillLevel.Solo]);
            yield return Poke(menu.buttons[1]); // CLUB
            Assert.AreEqual(GameMode.Freestyle, game.Mode);
            yield return null;
            StringAssert.Contains("Level: SOLO", panel.status.text);

            // BEGINNER opens the lessons
            system.home.SimulatePress();
            yield return Poke(menu.levelButtons[(int)SkillLevel.Beginner]);
            Assert.AreEqual(GameMode.Learn, game.ChosenMode);
            yield return Poke(menu.buttons[1]); // CLUB
            Assert.AreEqual(GameMode.ChooseLesson, game.Mode);
            yield return Poke(menu.buttons[0]); // lesson 1
            Assert.AreEqual(GameMode.Learn, game.Mode);
            yield return null;
            StringAssert.Contains("Level: BEGINNER", panel.status.text);
        }
    }
}
