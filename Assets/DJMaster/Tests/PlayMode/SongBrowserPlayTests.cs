using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // The center screen: turn BROWSE with a fingertip, LOAD onto A and B, and never load over a live deck.
    public class SongBrowserPlayTests
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

        // A spot next to a control, with the offset in real metres like a real fingertip (the board is drawn scaled)
        static Vector3 AtMetres(Transform t, Vector3 m) => t.TransformPoint(new Vector3(m.x / t.lossyScale.x, m.y / t.lossyScale.y, m.z / t.lossyScale.z));

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // Touch the knob and slide away from you by `metres`.
        IEnumerator Turn(Knob knob, float metres)
        {
            Vector3 At(float z) => AtMetres(knob.transform, new Vector3(0, 0.005f, z));
            finger.DirectPress = false;
            finger.Active = true;
            finger.Position = At(0);
            yield return null; yield return null;
            for (int i = 1; i <= 10; i++) { finger.Position = At(metres * i / 10f); yield return null; }
            finger.Position = AtMetres(knob.transform, new Vector3(0, 0.08f, 0));
            yield return null;
            finger.Active = false;
            yield return null;
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

        [UnityTest]
        public IEnumerator ScrollSortAndLoadOntoBothDecks()
        {
            var menu = Object.FindAnyObjectByType<StartMenu>();
            menu.buttons[2].SimulatePress(); // FREESTYLE
            menu.buttons[1].SimulatePress(); // INTERMEDIATE
            menu.buttons[0].SimulatePress(); // MY ROOM
            yield return null;
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var browser = board.browser;
            Assert.IsNotNull(browser, "the board should have a song browser");
            yield return WaitFor(() => board.A.deck.IsLoaded && board.B.deck.IsLoaded, 20f);

            // SORT by BPM with a poke
            yield return Poke(browser.sortButton);
            Assert.AreEqual(BrowseSort.Bpm, browser.Sort);

            // Turn BROWSE away from you: the highlight moves down the list
            int before = browser.Cursor;
            yield return Turn(browser.browseKnob, 0.03f);
            Assert.Greater(browser.Cursor, before + 1, "turning BROWSE about 3 cm should scroll a few songs");
            int pickA = browser.Selected;
            Assert.AreNotEqual(board.A.deck.TrackIndex, pickA);
            Assert.GreaterOrEqual(browser.Cursor, browser.Top);
            Assert.Less(browser.Cursor, browser.Top + browser.Rows, "the highlighted song should stay on screen");

            yield return Poke(board.A.load);
            Assert.AreEqual(pickA, board.A.deck.TrackIndex, "LOAD A should load the highlighted song");

            // Turning back toward you scrolls up
            yield return Turn(browser.browseKnob, -0.02f);
            int pickB = browser.Selected;
            Assert.AreNotEqual(pickA, pickB);
            yield return Poke(board.B.load);
            Assert.AreEqual(pickB, board.B.deck.TrackIndex, "LOAD B should load the highlighted song");
            yield return WaitFor(() => board.A.deck.IsLoaded && board.B.deck.IsLoaded, 20f);

            // Deck A goes out to the crowd: LOAD A must not cut the music
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.A.deck.Play();
            yield return null; yield return null;
            browser.Move(1);
            Assert.IsFalse(browser.LoadOnto(0), "loading over a live deck should be refused");
            Assert.AreEqual(pickA, board.A.deck.TrackIndex);
            Assert.IsTrue(board.A.deck.IsPlaying, "the music keeps playing");
            StringAssert.Contains("live", browser.Message);

            // Deck B is only in the headphones, so it's fair game
            board.mixer.headphoneCue[1] = true;
            board.B.deck.Play();
            yield return null;
            Assert.IsTrue(browser.LoadOnto(1), "a deck the crowd can't hear can be loaded");
        }

        [UnityTest]
        public IEnumerator SortKeepsTheHighlightedSong()
        {
            var board = Object.FindAnyObjectByType<DJBoard>(FindObjectsInactive.Include);
            var browser = board.browser;
            int heard = -1;
            browser.SelectionChanged += t => heard = t;
            browser.Select(13);
            Assert.AreEqual(13, heard, "Select should announce the new highlight");
            browser.SetSort(BrowseSort.Key);
            Assert.AreEqual(13, browser.Selected);
            browser.SetSort(BrowseSort.Bpm);
            Assert.AreEqual(13, browser.Selected);
            browser.Move(-100);
            Assert.AreEqual(0, browser.Cursor, "scrolling stops at the top");
            browser.Move(100);
            Assert.AreEqual(browser.Order.Count - 1, browser.Cursor, "and at the bottom");
            yield return null;
        }
    }
}
