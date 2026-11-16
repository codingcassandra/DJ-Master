using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // #22: songs from your own folder show up under MY MUSIC, load and play on both decks with their beat found,
    // keep that beat after a restart, and a broken or wrong file just gets a message
    public class MyMusicTest
    {
        string folder;
        const string Good = "Test Groove.wav";

        [UnitySetUp]
        public IEnumerator Load()
        {
            folder = Path.Combine(Application.temporaryCachePath, "mymusic-test");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            MyMusic.FolderOverride = folder;
            BeatGridStore.Forget("user:" + Good);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            MyMusic.FolderOverride = null;
            TrackLibrary.SetUserTracks(new TrackInfo[0]);
            BeatGridStore.Forget("user:" + Good);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        // A 16-bit stereo WAV of one of our songs, the way you'd export one
        static void WriteWav(string path, float[] stereo, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = stereo.Length * 2;
            w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(bytes);
            foreach (var s in stereo) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator YourSongsLoadPlayAndSurviveARestart()
        {
            WriteWav(Path.Combine(folder, Good), TrackSynth.Render(TrackLibrary.Tracks[0]), TrackSynth.SampleRate); // Neon Tides, 128 BPM
            var junk = new byte[40000];
            new System.Random(1).NextBytes(junk);
            File.WriteAllBytes(Path.Combine(folder, "broken.mp3"), junk);
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a song");

            var menu = Object.FindAnyObjectByType<StartMenu>();
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var browser = board.browser;
            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[1].SimulatePress(); // INTERMEDIATE
            menu.buttons[1].SimulatePress(); // CLUB
            yield return null;

            // SORT round to MY MUSIC: it scans the folder
            browser.SetSort(BrowseSort.Mine);
            Assert.AreEqual(2, MyMusic.Count, "the MP3 and the WAV, not the text file");
            Assert.AreEqual(2, browser.Order.Count);
            Assert.IsTrue(browser.Order.All(TrackLibrary.IsUser), "MY MUSIC lists only your songs");
            Assert.IsFalse(SongBrowser.SortedOrder(BrowseSort.Crate).Any(TrackLibrary.IsUser), "and our crate stays ours");
            int good = browser.Order.First(i => TrackLibrary.Tracks[i].title == "Test Groove");
            int broken = browser.Order.First(i => TrackLibrary.Tracks[i].title == "broken");
            Assert.AreEqual(0f, TrackLibrary.Tracks[good].bpm, "the beat isn't known until it's loaded");

            // Load it on A: the file loads, the beat is found, it plays in time
            browser.Select(good);
            Assert.IsTrue(browser.LoadOnto(0));
            var A = board.A.deck;
            yield return WaitFor(() => A.IsLoaded, 30f, "your song loading on A");
            Assert.AreEqual(128f, A.Track.bpm, 1f, "found the beat");
            A.Play();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(A.IsPlaying);
            Assert.Greater(A.Beat, 0.5, "it plays and counts beats");

            // And on B
            Assert.IsTrue(browser.LoadOnto(1));
            var B = board.B.deck;
            yield return WaitFor(() => B.IsLoaded, 10f, "your song loading on B");
            B.Play();
            yield return null;
            Assert.IsTrue(B.IsPlaying);

            // A broken file: a message on the deck screen, no crash, no deck stuck LOADING
            B.Pause();
            browser.Select(broken);
            // Unity's decoder logs this for a file it can't read; we turn it into a message instead of failing
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("FMOD"));
            Assert.IsTrue(browser.LoadOnto(1));
            yield return WaitFor(() => !B.HasTrack, 10f, "the broken file giving up");
            StringAssert.Contains("Couldn't play", B.Problem);
            var display = Object.FindObjectsByType<DeckDisplay>().First(d => d.deck == B);
            yield return null;
            StringAssert.Contains("Couldn't play", display.title.text);

            // A restart rescans the folder: the beat we found is remembered, so it's ready straight away
            MyMusic.Scan();
            int again = Enumerable.Range(TrackLibrary.BuiltInCount, TrackLibrary.Tracks.Length - TrackLibrary.BuiltInCount)
                .First(i => TrackLibrary.Tracks[i].title == "Test Groove");
            Assert.AreEqual(128f, TrackLibrary.Tracks[again].bpm, 1f);
        }

        [UnityTest]
        public IEnumerator AnEmptyFolderShowsHowToAddSongs()
        {
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[2].SimulatePress(); menu.buttons[1].SimulatePress(); menu.buttons[1].SimulatePress();
            yield return null;
            var browser = board.browser;
            browser.SetSort(BrowseSort.Mine);
            yield return null;
            Assert.AreEqual(0, browser.Order.Count);
            Assert.AreEqual(-1, browser.Selected);
            StringAssert.Contains("Copy MP3, OGG or WAV", browser.titles[0].text.Replace("\n", " "));
            StringAssert.Contains("rights", browser.titles[0].text.Replace("\n", " "));
            Assert.IsFalse(browser.LoadOnto(0), "nothing to load");
            browser.Move(1); // turning BROWSE on an empty list is fine
            browser.SetSort(BrowseSort.Crate);
            Assert.AreEqual(0, browser.Cursor);
        }
    }
}
