using System;
using UnityEngine;

namespace DJMaster
{
    // Build mode: plan a set for a live show. Pick a transition style, then tap songs in the crate to add them.
    // Each song you add is mixed in with the style that's selected. The set is saved between sessions.
    public class SetBuilderPanel : MonoBehaviour
    {
        public PokeButton[] crateButtons = new PokeButton[8];
        public TextMesh[] crateLabels = new TextMesh[8];
        public PokeButton[] styleButtons = new PokeButton[6];   // in TransitionStyle order
        public TextMesh setText, info, status;
        public PokeButton back, undo, clear, goLive;
        [Tooltip("Plays each song as you add it.")]
        public Deck preview;

        public SetList Set { get; private set; } = new();
        public TransitionStyle Style { get; private set; }

        public event Action BackPressed, GoLivePressed;

        string message = "";
        float messageTimer;

        void Awake()
        {
            Set = SetList.Parse(PlayerPrefs.GetString("set", ""));
            for (int i = 0; i < crateButtons.Length; i++)
            {
                int track = i;
                crateButtons[i].Pressed += () => AddSong(track);
            }
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

        public void Refresh()
        {
            var tracks = TrackLibrary.Tracks;
            for (int i = 0; i < crateButtons.Length && i < tracks.Length; i++)
            {
                crateButtons[i].Lit = Set.songs.Contains(i);
                if (crateLabels[i] != null) crateLabels[i].text = $"{tracks[i].title}\n{tracks[i].bpm:0} BPM {tracks[i].genre.ToString().ToUpper()}";
            }
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
            setText.text = BoardColors.Display(sb.ToString());
            info.text = TextUtil.Wrap(BoardColors.Display($"{BoardColors.Yellow(SetList.Name(Style))}: {SetList.Describe(Style)}"), 26);
            status.text = message != "" ? BoardColors.Display(message)
                : Set.Ready ? $"{Set.Count} songs, about {Set.Count * 45 / 60 + 1} min. Ready to GO LIVE!"
                : $"Add {SetList.MinSongs - Set.Count} more song{(SetList.MinSongs - Set.Count == 1 ? "" : "s")} ({SetList.MaxSongs} max).";
        }
    }
}
