using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DJMaster
{
    public enum BrowseSort { Crate, Bpm, Key, Mine }

    // The center screen, like the browser on a real controller. Turn BROWSE to scroll the crate, SORT to order it by
    // BPM or key, then LOAD on a deck puts the highlighted song on that deck. While a song plays out loud, each row's
    // BPM and key light up green / yellow / red for how well it mixes with that song (MixMatch, #13). A deck the crowd can hear can't be loaded:
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
        public int Selected => order != null && order.Length > 0 ? order[Cursor] : -1; // track index of the highlighted row, -1 when the list is empty
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
        bool bonusListed;

        void Awake()
        {
            order ??= SortedOrder(Sort);
            bonusListed = Listed(TrackLibrary.BonusTrack);
            if (browseKnob != null)
            {
                knobStep = Mathf.RoundToInt(browseKnob.value * rowsPerUnit);
                browseKnob.Changed += v =>
                {
                    int s = Mathf.RoundToInt(v * rowsPerUnit);
                    if (s != knobStep) { Move(s - knobStep); knobStep = s; }
                };
            }
            if (sortButton != null) sortButton.Pressed += () => SetSort((BrowseSort)(((int)Sort + 1) % 4));
            TrackLibrary.UserTracksChanged += OnTracksChanged;
        }

        void OnDestroy() => TrackLibrary.UserTracksChanged -= OnTracksChanged;

        // A rescan changed your songs: redo the list, keeping the highlighted song if it's still there
        void OnTracksChanged(Func<int, int> newIndex)
        {
            if (order == null) return;
            int keep = Selected >= 0 ? newIndex(Selected) : -1;
            order = SortedOrder(Sort);
            if (keep >= 0 && Array.IndexOf(order, keep) >= 0) Select(keep);
            else { Cursor = 0; Top = 0; lastSelected = -2; ScrollToCursor(); }
            lastDrawn = null;
        }

        public void Move(int rows)
        {
            order ??= SortedOrder(Sort);
            if (order.Length == 0) return;
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
            if (sort == BrowseSort.Mine && sort != Sort) MyMusic.Open(); // asks for music access the first time, then scans
            Sort = sort;
            order = SortedOrder(sort);
            if (Array.IndexOf(order, keep) >= 0) Select(keep);
            else { Cursor = 0; Top = 0; ScrollToCursor(); }
        }

        void ScrollToCursor()
        {
            if (Selected != lastSelected) { lastSelected = Selected; if (Selected >= 0) SelectionChanged?.Invoke(Selected); }
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
            if (Selected < 0) { Say("No songs here yet.", true); return false; }
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
            // The daily challenge's bonus song shows up once it's unlocked
            if (Listed(TrackLibrary.BonusTrack) != bonusListed) { bonusListed = !bonusListed; SetSort(Sort); }
            var a = board.A.deck;
            var b = board.B.deck;
            string msg = Message;
            int live = LiveTrack;
            string key = $"{Cursor}|{Top}|{Sort}|{a.TrackIndex}|{b.TrackIndex}|{msg}|{live}|{order.Length}|{MyMusic.Status}";
            if (key == lastDrawn) return;
            lastDrawn = key;

            if (header != null)
                header.text = $"BROWSE   <size=80%>SORT: {SortName(Sort)}{(live >= 0 ? $"   MATCH: {Fit(TrackLibrary.Tracks[live].title, 14)}" : "")}</size>";
            bool emptyMine = Sort == BrowseSort.Mine && order.Length == 0;
            if (status != null) status.text = msg != "" ? msg : order.Length == 0 ? "0 / 0" : $"{Cursor + 1} / {order.Length}";

            for (int r = 0; r < Rows; r++)
            {
                int i = Top + r;
                bool has = i < order.Length;
                var t = has ? TrackLibrary.Tracks[order[i]] : default;
                if (titles[r] != null)
                    titles[r].text = has ? $"{Fit(t.title, 18)}\n<size=72%><color=#9AA0B0>{(t.IsUser ? "MY MUSIC" : $"{Fit(t.artist, 18)}  -  {t.genre}")}</color></size>"
                        : emptyMine && r == 0 ? $"<size=80%>{TextUtil.Wrap(MyMusic.Status != "" ? MyMusic.Status : MyMusic.HowTo, 52)}</size>" : "";
                if (details[r] != null) details[r].text = has ? Details(t, live >= 0 && live != order[i] ? TrackLibrary.Tracks[live] : (TrackInfo?)null) : "";
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

        // The song the crowd mostly hears right now, or -1 when the room is quiet. The rows are matched against it.
        public int LiveTrack
        {
            get
            {
                if (!Application.isPlaying) return -1; // the editor preview has no audio running
                int d = board.mixer.DominantDeck;
                return d < 0 ? -1 : board.channels[d].deck.TrackIndex;
            }
        }

        // BPM over key. Matched against the live song when there is one: green mixes, yellow needs a fade or cut, red clashes.
        public static string Details(TrackInfo t, TrackInfo? live)
        {
            if (t.bpm <= 0) return $"<color=#9AA0B0>BPM on LOAD</color>\n<size=72%><color=#9AA0B0>{KeyLabel(t)}</color></size>"; // your songs: the beat is found the first time
            string bpm = $"{t.bpm:0} BPM", keyText = KeyLabel(t);
            if (live is TrackInfo l)
            {
                var tempo = MixMatch.Tempo(l, t);
                bpm = tempo == TempoMatch.Green ? BoardColors.Green(bpm) : tempo == TempoMatch.Yellow ? BoardColors.Yellow(bpm) : BoardColors.Red(bpm);
                var keys = MixMatch.Keys(l, t);
                if (keys == KeyMatch.Perfect || keys == KeyMatch.Good) return $"{bpm}\n<size=72%>{BoardColors.Green(keyText)}</size>";
                if (keys == KeyMatch.Clash) return $"{bpm}\n<size=72%>{BoardColors.Red(keyText)}</size>";
            }
            return $"{bpm}\n<size=72%><color=#9AA0B0>{keyText}</color></size>";
        }

        public float RowY(int row) => titles[0] != null ? titles[0].transform.localPosition.y - row * rowPitch : 0f;

        // ---- Pure helpers, covered by EditMode tests ----

        public static string SortName(BrowseSort s) => s switch { BrowseSort.Bpm => "BPM", BrowseSort.Key => "KEY", BrowseSort.Mine => "MY MUSIC", _ => "CRATE" };

        // Crate: our songs, then the free ones. BPM: slowest first. Key: around the Camelot wheel, so keys that mix well
        // sit next to each other; songs with no key on file go last (by BPM).
        public static int[] SortedOrder(BrowseSort sort)
        {
            var tracks = TrackLibrary.Tracks;
            var list = new List<int>();
            // MY MUSIC lists only your songs, the other tabs only ours
            for (int i = 0; i < tracks.Length; i++) if (Listed(i) && TrackLibrary.IsUser(i) == (sort == BrowseSort.Mine)) list.Add(i);
            var idx = list.ToArray();
            if (sort == BrowseSort.Crate || sort == BrowseSort.Mine) return idx;
            Array.Sort(idx, (x, y) =>
            {
                var a = tracks[x];
                var b = tracks[y];
                int c = 0;
                if (sort == BrowseSort.Key) c = WheelPosition(a).CompareTo(WheelPosition(b));
                if (c == 0) c = a.bpm.CompareTo(b.bpm);
                if (c == 0) c = string.CompareOrdinal(a.title, b.title);
                return c != 0 ? c : x.CompareTo(y);
            });
            return idx;
        }

        // Whether a song shows in the browser: the daily challenge's bonus song stays hidden until it's unlocked.
        public static bool Listed(int index) => !TrackLibrary.IsLocked(index);

        // Around the wheel (1A, 1B, 2A ...), songs with no key on file last
        public static int WheelPosition(TrackInfo t) => t.HasKey ? MixMatch.CamelotNumber(t.PitchClass, t.major) * 2 + (t.major ? 1 : 0) : 999;

        public static string KeyLabel(TrackInfo t) => t.HasKey ? $"{t.Key}  {MixMatch.Camelot(t)}" : "key --";

        static string Fit(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 2) + "..";
    }
}
