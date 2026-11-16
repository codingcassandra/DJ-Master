using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // The level screen (step 2, after the mode): poked with a fingertip, saved, lit next time, and shown while you play.
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

            // ADVANCED for BUILD A SET: saved, then on to the venue
            yield return Poke(menu.buttons[1]); // BUILD A SET
            Assert.AreEqual(GameMode.ChooseLevel, game.Mode);
            Assert.AreEqual("ADVANCED", menu.labels[(int)SkillLevel.Advanced].text);
            Assert.IsTrue(menu.buttons[(int)SkillLevel.Intermediate].Lit, "INTERMEDIATE until you pick");
            yield return Poke(menu.buttons[(int)SkillLevel.Advanced]);
            Assert.AreEqual(SkillLevel.Advanced, Skill.Current);
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            Assert.AreEqual(GameMode.Build, game.ChosenMode);
            StringAssert.Contains("Level: ADVANCED", menu.subtitle.text);
            Assert.AreEqual((int)SkillLevel.Advanced, PlayerPrefs.GetInt(SkillLevels.PrefKey, -1), "saved between sessions");

            // Back home and into FREESTYLE: ADVANCED is still the lit one
            system.home.SimulatePress();
            Assert.AreEqual(GameMode.Title, game.Mode);
            yield return Poke(menu.buttons[2]); // FREESTYLE
            Assert.IsTrue(menu.buttons[(int)SkillLevel.Advanced].Lit);
            Assert.IsFalse(menu.buttons[(int)SkillLevel.Intermediate].Lit);

            // SOLO, with the level on the panel while you play
            yield return Poke(menu.buttons[(int)SkillLevel.Solo]);
            yield return Poke(menu.buttons[1]); // CLUB
            Assert.AreEqual(GameMode.Freestyle, game.Mode);
            yield return null;
            StringAssert.Contains("Level: SOLO", panel.status.text);

            // BEGINNER lessons
            system.home.SimulatePress();
            yield return Poke(menu.buttons[0]); // LEARN
            yield return Poke(menu.buttons[(int)SkillLevel.Beginner]);
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
