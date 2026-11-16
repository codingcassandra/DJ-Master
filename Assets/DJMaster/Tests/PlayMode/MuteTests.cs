using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DJMaster.Tests
{
    // Keeps the PlayMode tests quiet. Every scene a test loads gets a Silencer on its AudioListener, added last so it
    // runs after the MixRecorder: the decks, meters and recorder all hear the real mix, only the speakers get silence.
    [SetUpFixture]
    public class MuteTests
    {
        [OneTimeSetUp]
        public void Mute()
        {
            SceneManager.sceneLoaded += AddSilencer;
            AddSilencer(default, default);
        }

        [OneTimeTearDown]
        public void Unmute() => SceneManager.sceneLoaded -= AddSilencer;

        static void AddSilencer(Scene scene, LoadSceneMode mode)
        {
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
                if (l.GetComponent<Silencer>() == null) l.gameObject.AddComponent<Silencer>();
        }
    }

    public class Silencer : MonoBehaviour
    {
        void OnAudioFilterRead(float[] data, int channels) => System.Array.Clear(data, 0, data.Length);
    }
}
