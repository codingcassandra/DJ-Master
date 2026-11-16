using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DJMaster
{
    public enum BrowseSort { Crate, Bpm, Key }

    // The center screen, like the browser on a real controller. Turn BROWSE to scroll the crate, SORT to order it by
    // BPM or key, then LOAD on a deck puts the highlighted song on that deck. A deck the crowd can hear can't be loaded:
    // swapping the song under a live channel kills the music, so real DJs never do it.
    public class SongBrowser : MonoBehaviour
    {
        public DJBoard board;
        public Knob browseKnob;
        public PokeButton sortButton;
        public TextMeshPro header, status;
        public TextMeshPro[] titles = new TextMeshPro[4];  // title, then artist and genre
        public TextMeshPro[] details = new TextMeshPro[4]; // BPM, then key
        public TextMeshPro[] tags = new TextMeshPro[4];    // A / B when that song is on a deck
        public Transform selectBar;
        public float rowPitch = 0.017f;
        [Tooltip("Rows the list moves per 1.0 of knob value. The editor mouse wheel nudges 0.05, so 20 is one row per notch.")]
        public float rowsPerUnit = 20f;
        [Tooltip("A deck louder than this in the room counts as live, and LOAD won't touch it.")]
        public float liveGain = 0.05f;

        public BrowseSort Sort { get; private set; }
        public IReadOnlyList<int> Order => order;
        public int Cursor { get; private set; }
        public int Selected => order[Cursor]; // track index of the highlighted row
        public int Rows => titles.Length;
        public int Top { get; private set; }  // first visible row

        // Set while something else picks the songs (a live set). LOAD shows this instead of loading.
        public string LockReason { get; set; }
        // Fires with the track index whenever the highlighted song changes (knob, Select or a re-sort).
        public event Action<int> SelectionChanged;
        // Fires with (track, deck) after LOAD puts a song on a deck.
        public event Action<int, int> SongLoaded;
        public string Message => Time.time - messageAt < 3.5f ? message : "";

        int[] order;
        int knobStep;
        int lastSelected = -1;
        string message = "";
        float messageAt = -10f;
        string lastDrawn;

        void Awake()
        {
            order ??= SortedOrder(Sort);
            if (browseKnob != null)
            {
                knobStep = Mathf.RoundToInt(browseKnob.value * rowsPerUnit);
                browseKnob.Changed += v =>
                {
                    int s = Mathf.RoundToInt(v * rowsPerUnit);
                    if (s != knobStep) { Move(s - knobStep); knobStep = s; }
                };
            }
            if (sortButton != null) sortButton.Pressed += () => SetSort((BrowseSort)(((int)Sort + 1) % 3));
        }

        public void Move(int rows)
        {
            order ??= SortedOrder(Sort);
            Cursor = Mathf.Clamp(Cursor + rows, 0, order.Length - 1);
            ScrollToCursor();
        }

        public void Select(int track)
        {
            int i = Array.IndexOf(order, track);
            if (i < 0) return;
            Cursor = i;
            ScrollToCursor();
        }

        public void SetSort(BrowseSort sort)
        {
            order ??= SortedOrder(Sort);
            int keep = Selected;
            Sort = sort;
            order = SortedOrder(sort);
            Select(keep);
        }

        void ScrollToCursor()
        {
            if (Selected != lastSelected) { lastSelected = Selected; SelectionChanged?.Invoke(Selected); }
            if (Cursor < Top) Top = Cursor;
            else if (Cursor >= Top + Rows) Top = Cursor - Rows + 1;
            Top = Mathf.Clamp(Top, 0, Mathf.Max(0, order.Length - Rows));
        }

        public bool IsLive(int deck) => board.mixer.CrowdGain(deck) > liveGain;

        // LOAD on deck 0 (A) or 1 (B). Returns false, with the reason on screen, when it refuses.
        public bool LoadOnto(int deck)
        {
            string name = deck == 0 ? "A" : "B";
            if (LockReason != null) { Say(LockReason, true); return false; }
            if (IsLive(deck)) { Say($"Deck {name} is live! Fade it out first.", true); return false; }
            var t = TrackLibrary.Tracks[Selected];
            board.channels[deck].deck.Load(Selected);
            Say($"{t.title} on Deck {name}", false);
            SongLoaded?.Invoke(Selected, deck);
            return true;
        }

        void Say(string text, bool warning)
        {
            message = warning ? BoardColors.Red(text) : text;
            messageAt = Time.time;
        }

        void Update() => Draw();

        // Also called by the scene builder to render a filled-in preview.
        public void Draw()
        {
            order ??= SortedOrder(Sort);
            var a = board.A.deck;
            var b = board.B.deck;
            string msg = Message;
            string key = $"{Cursor}|{Top}|{Sort}|{a.TrackIndex}|{b.TrackIndex}|{msg}";
            if (key == lastDrawn) return;
            lastDrawn = key;

            if (header != null) header.text = $"BROWSE   <size=80%>SORT: {SortName(Sort)}</size>";
            if (status != null) status.text = msg != "" ? msg : $"{Cursor + 1} / {order.Length}";

            for (int r = 0; r < Rows; r++)
            {
                int i = Top + r;
                bool has = i < order.Length;
                var t = has ? TrackLibrary.Tracks[order[i]] : default;
                if (titles[r] != null) titles[r].text = has ? $"{Fit(t.title, 18)}\n<size=72%><color=#9AA0B0>{Fit(t.artist, 18)}  -  {t.genre}</color></size>" : "";
                if (details[r] != null) details[r].text = has ? $"{t.bpm:0} BPM\n<size=72%><color=#9AA0B0>{KeyLabel(t)}</color></size>" : "";
                if (tags[r] != null)
                {
                    bool onA = has && a.TrackIndex == order[i], onB = has && b.TrackIndex == order[i];
                    tags[r].text = !has ? "" : (onA ? BoardColors.Blue("A") : "") + (onB ? BoardColors.Pink("B") : "");
                }
            }
            if (selectBar != null)
            {
                var p = selectBar.localPosition;
                selectBar.localPosition = new Vector3(p.x, RowY(Cursor - Top), p.z);
            }
        }

        public float RowY(int row) => titles[0] != null ? titles[0].transform.localPosition.y - row * rowPitch : 0f;

        // ---- Pure helpers, covered by EditMode tests ----

        public static string SortName(BrowseSort s) => s switch { BrowseSort.Bpm => "BPM", BrowseSort.Key => "KEY", _ => "CRATE" };

        // Crate: our songs, then the free ones. BPM: slowest first. Key: around the Camelot wheel, so keys that mix well
        // sit next to each other; recordings have no key on file, so they go last (by BPM).
        public static int[] SortedOrder(BrowseSort sort)
        {
            var tracks = TrackLibrary.Tracks;
            var list = new List<int>();
            for (int i = 0; i < tracks.Length; i++) if (Listed(i)) list.Add(i);
            var idx = list.ToArray();
            if (sort == BrowseSort.Crate) return idx;
            Array.Sort(idx, (x, y) =>
            {
                var a = tracks[x];
                var b = tracks[y];
                int c = 0;
                if (sort == BrowseSort.Key) c = CamelotOrNone(a).CompareTo(CamelotOrNone(b));
                if (c == 0) c = a.bpm.CompareTo(b.bpm);
                if (c == 0) c = string.CompareOrdinal(a.title, b.title);
                return c != 0 ? c : x.CompareTo(y);
            });
            return idx;
        }

        // Whether a song shows in the browser. When the daily-challenge bonus song lands (PR #27), this becomes
        // !TrackLibrary.IsLocked(index) so it stays hidden until it's unlocked.
        public static bool Listed(int index) => true;

        static int CamelotOrNone(TrackInfo t) => t.IsRecording ? 99 : t.CamelotNumber;

        public static string KeyLabel(TrackInfo t) => t.IsRecording ? "key --" : $"{t.Key}  {t.Camelot}";

        static string Fit(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 2) + "..";
    }
}
