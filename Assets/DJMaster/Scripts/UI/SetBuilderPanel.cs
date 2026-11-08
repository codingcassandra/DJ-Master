using System;
using TMPro;
using UnityEngine;

namespace DJMaster
{
    // Build mode: plan a set for a live show. Pick a transition style, then tap songs in the crate to add them.
    // Each song you add is mixed in with the style that's selected. The set is saved between sessions.
    public class SetBuilderPanel : MonoBehaviour
    {
        public PokeButton[] crateButtons = new PokeButton[8];
        public TextMeshPro[] crateLabels = new TextMeshPro[8];
        public PokeButton[] styleButtons = new PokeButton[6];   // in TransitionStyle order
        public TextMeshPro setText, info, status;
        public PokeButton back, undo, clear, goLive;
        [Tooltip("Crate tabs: the game's own tracks, then free (CC0) music. Each page holds one crate's worth of songs.")]
        public PokeButton gameTab, freeTab;
        [Tooltip("Plays each song as you add it.")]
        public Deck preview;

        public SetList Set { get; private set; } = new();
        public TransitionStyle Style { get; private set; }

        public event Action BackPressed, GoLivePressed;

        public int Page { get; private set; }
        int PageCount => (TrackLibrary.Tracks.Length + crateButtons.Length - 1) / crateButtons.Length;
        int TrackAt(int button) => Page * crateButtons.Length + button;

        string message = "";
        float messageTimer;

        void Awake()
        {
            Set = SetList.Parse(PlayerPrefs.GetString("set", ""));
            for (int i = 0; i < crateButtons.Length; i++)
            {
                int button = i;
                crateButtons[i].Pressed += () => { if (TrackAt(button) < TrackLibrary.Tracks.Length) AddSong(TrackAt(button)); };
            }
            if (gameTab != null) gameTab.Pressed += () => ShowPage(0);
            if (freeTab != null) freeTab.Pressed += () => ShowPage(1);
            for (int i = 0; i < styleButtons.Length; i++)
            {
                var style = (TransitionStyle)i;
                styleButtons[i].Pressed += () => PickStyle(style);
            }
            back.Pressed += () => BackPressed?.Invoke();
            undo.Pressed += () => { Set.Undo(); Changed(); };
            clear.Pressed += () => { Set.Clear(); Changed(); };
            goLive.Pressed += () =>
            {
                if (Set.Ready) GoLivePressed?.Invoke();
                else Say($"Add at least {SetList.MinSongs} songs first.", true);
            };
        }

        void OnEnable() => Refresh();

        public void AddSong(int track)
        {
            string error = Set.Add(track, Style);
            if (error != null) { Say(error, true); return; }
            if (preview != null) { preview.Load(track); preview.Play(); }
            string advice = Set.Count > 1 ? SetList.Advice(Set.songs[Set.Count - 2], track, Style) : null;
            Say(advice != null ? BoardColors.Red(advice)
                : Set.Count == 1 ? "Opener locked in. Pick a transition, then the next song."
                : $"Added with a {SetList.Name(Style)}.", false);
            Changed();
        }

        public void PickStyle(TransitionStyle style)
        {
            Style = style;
            Refresh();
        }

        void Changed()
        {
            if (Application.isPlaying)
            {
                PlayerPrefs.SetString("set", Set.Serialize());
                PlayerPrefs.Save();
            }
            Refresh();
        }

        void Say(string text, bool error)
        {
            message = text;
            messageTimer = 4f;
            if (error) Sfx.Fail();
            Refresh();
        }

        void Update()
        {
            if (messageTimer <= 0) return;
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0) { message = ""; Refresh(); }
        }

        public void ShowPage(int page)
        {
            Page = Mathf.Clamp(page, 0, PageCount - 1);
            Refresh();
        }

        public void Refresh()
        {
            var tracks = TrackLibrary.Tracks;
            for (int i = 0; i < crateButtons.Length; i++)
            {
                int track = TrackAt(i);
                bool has = track < tracks.Length;
                crateButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                crateButtons[i].Lit = Set.songs.Contains(track);
                var t = tracks[track];
                if (crateLabels[i] != null)
                    crateLabels[i].text = t.IsRecording
                        ? $"{t.title}\n<size=80%>{t.bpm:0} BPM  {t.artist}</size>"
                        : $"{t.title}\n{t.bpm:0} BPM {t.genre.ToString().ToUpper()}";
            }
            if (gameTab != null) gameTab.Lit = Page == 0;
            if (freeTab != null) freeTab.Lit = Page == 1;
            for (int i = 0; i < styleButtons.Length; i++) styleButtons[i].Lit = (int)Style == i;
            goLive.Lit = Set.Ready;

            var sb = new System.Text.StringBuilder("YOUR SET\n");
            if (Set.Count == 0) sb.Append("<color=#8890A0>empty: tap a song\nin the crate</color>");
            for (int i = 0; i < Set.Count; i++)
            {
                if (i > 0)
                {
                    bool risky = SetList.Advice(Set.songs[i - 1], Set.songs[i], Set.transitions[i - 1]) != null;
                    string name = $"  {SetList.Name(Set.transitions[i - 1])}{(risky ? " !" : "")}";
                    sb.Append(risky ? BoardColors.Red(name) : BoardColors.Yellow(name)).Append('\n');
                }
                sb.Append($"{i + 1} {tracks[Set.songs[i]].title}\n");
            }
            setText.text = sb.ToString();
            info.text = TextUtil.Wrap($"{BoardColors.Yellow(SetList.Name(Style))}: {SetList.Describe(Style)}", 24);
            status.text = message != "" ? message
                : Set.Ready ? $"{Set.Count} songs, about {Set.Count * 45 / 60 + 1} min. Ready to GO LIVE!"
                : $"Add {SetList.MinSongs - Set.Count} more song{(SetList.MinSongs - Set.Count == 1 ? "" : "s")} ({SetList.MaxSongs} max).";
        }
    }
}
