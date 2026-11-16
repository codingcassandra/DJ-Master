using TMPro;
using UnityEngine;

namespace DJMaster
{
    // The little screen above each deck: track, BPM, tempo, key, time left, the scrolling waveform with its beat and
    // phrase lines, bar counter, phrase progress and beat lights.
    public class DeckDisplay : MonoBehaviour
    {
        public Deck deck;
        public TextMeshPro title, bpm, position;
        [Tooltip("Camelot key and time left, under the BPM.")]
        public TextMeshPro info;
        [Tooltip("Quad with the DJMaster/Waveform shader.")]
        public Renderer waveform;
        [Tooltip("Seconds of music across the waveform.")]
        public float waveSeconds = 6f;
        [Tooltip("Phrase countdown (PhraseCue), right of the bar counter. Hidden for ADVANCED players.")]
        public TextMeshPro countdown;
        public Color countColor = new Color(0.75f, 0.78f, 0.85f);
        public Color headsUpColor = new Color(1f, 0.7f, 0.15f);
        public Renderer[] beatLights = new Renderer[4];
        public Transform phraseFill;     // scaled on X from 0..1 across the 16-bar loop
        public Renderer levelFill;
        public Transform levelPivot;
        public Color beatOn = new Color(1f, 0.55f, 0.1f);
        public Color downbeatOn = new Color(1f, 0.15f, 0.3f);
        public Color beatOff = new Color(0.08f, 0.08f, 0.1f);

        MaterialPropertyBlock block, waveBlock;
        AudioClip waveClip;
        bool waveReady, waveSafe;
        int lastSecondsLeft = -1;
        int lastBar = -1, lastBeatInBar = -1, lastTrack = -2;
        bool lastShowCount, lastPlaying, lastByEar;
        float lastBpm = -1, lastTempo = -99;

        void Update()
        {
            block ??= new MaterialPropertyBlock();

            if (deck.TrackIndex != lastTrack || (deck.IsLoading && Time.frameCount % 15 == 0))
            {
                lastTrack = deck.TrackIndex;
                lastSecondsLeft = -2; // the key changes with the song
                title.text = !deck.HasTrack ? "NO TRACK  -  pick a song, tap LOAD"
                    : deck.IsLoading ? "LOADING..."
                    : $"{deck.Track.title}\n<size=85%>{deck.Track.artist}   {deck.Track.genre}   {(deck.Track.IsRecording ? deck.Track.license : deck.Track.Key)}</size>";
            }

            float b = Mathf.Round(deck.BPM * 10f) / 10f;
            float t = Mathf.Round(deck.TempoPercent * 10f) / 10f;
            bool byEar = ByEar.Active;
            if (b != lastBpm || t != lastTempo || byEar != lastByEar)
            {
                lastBpm = b; lastTempo = t; lastByEar = byEar;
                // By ear (#21) the numbers are hidden: you match the tempo with your ears
                bpm.text = !deck.HasTrack ? "--- BPM" : byEar ? "BY EAR" : $"{b:0.0} BPM  <size=62.5%>{(t >= 0 ? "+" : "")}{t:0.0}%</size>";
            }

            int bar = deck.Bar, beatInBar = deck.BeatInBar;
            bool showCount = Skill.PhraseCountdown;
            bool playing = deck.IsPlaying;
            if (bar != lastBar || beatInBar != lastBeatInBar || showCount != lastShowCount || playing != lastPlaying)
            {
                lastBar = bar; lastBeatInBar = beatInBar; lastShowCount = showCount; lastPlaying = playing;
                UpdateCountdown(bar, beatInBar, showCount);
                // Section names describe the synthesized arrangement, so recordings just show the bar.
                position.text = deck.IsLoaded ? $"BAR {bar + 1,2}/16   {(deck.Track.IsRecording ? "" : SectionName(deck.Section))}" : "";
                if (phraseFill != null) phraseFill.localScale = new Vector3(Mathf.Max(0.001f, (bar + 1) / 16f), 1, 1);
            }

            float frac = (float)(deck.Beat % 1.0);
            for (int i = 0; i < beatLights.Length; i++)
            {
                bool on = deck.IsLoaded && i == beatInBar && (frac < 0.6f || !deck.IsPlaying);
                block.SetColor("_Color", on ? (i == 0 ? downbeatOn : beatOn) : beatOff);
                beatLights[i].SetPropertyBlock(block);
            }

            UpdateWaveform();
            UpdateInfo();

            if (levelPivot != null)
            {
                float lvl = Mathf.Clamp01(deck.Strip.Level);
                levelPivot.localScale = new Vector3(1, Mathf.Max(0.001f, lvl), 1);
                block.SetColor("_Color", lvl > 0.95f ? Color.red : Color.Lerp(new Color(0.1f, 0.9f, 0.4f), new Color(1f, 0.8f, 0.1f), lvl));
                levelFill.SetPropertyBlock(block);
            }
        }

        // Lows warm, mids green, highs blue. Color-blind mode swaps in Okabe-Ito colors that stay apart for everyone
        static readonly Color[] WaveColors = { new Color(1f, 0.42f, 0.08f), new Color(0.2f, 0.9f, 0.35f), new Color(0.35f, 0.65f, 1f), new Color(1f, 0.2f, 0.35f) };
        static readonly Color[] WaveColorsSafe = { new Color32(0xD5, 0x5E, 0x00, 0xFF), new Color32(0xF0, 0xE4, 0x42, 0xFF), new Color32(0x56, 0xB4, 0xE9, 0xFF), Color.white };

        void UpdateWaveform()
        {
            if (waveform == null) return;
            waveBlock ??= new MaterialPropertyBlock();
            var clip = deck.IsLoaded ? deck.Source.clip : null;
            bool safe = BoardColors.ColorSafe;
            if (clip != waveClip || !waveReady || safe != waveSafe)
            {
                waveClip = clip;
                waveSafe = safe;
                var tex = Waveform.TryGet(clip, out int columns);
                waveReady = clip == null || tex != null;
                waveform.GetPropertyBlock(waveBlock);
                waveBlock.SetFloat("_Columns", tex != null ? columns : 0);
                if (tex != null)
                {
                    waveBlock.SetTexture("_WaveTex", tex);
                    waveBlock.SetVector("_TexSize", new Vector4(tex.width, tex.height, 0, 0));
                }
                var colors = safe ? WaveColorsSafe : WaveColors;
                waveBlock.SetColor("_LowColor", colors[0]);
                waveBlock.SetColor("_MidColor", colors[1]);
                waveBlock.SetColor("_HighColor", colors[2]);
                waveBlock.SetColor("_PhraseColor", colors[3]);
            }
            if (clip == null) { waveform.SetPropertyBlock(waveBlock); return; }
            float rate = Waveform.ColumnRate(clip.frequency);
            waveBlock.SetFloat("_Playhead", deck.Source.timeSamples / (float)clip.frequency * rate);
            waveBlock.SetFloat("_Window", waveSeconds * rate);
            waveBlock.SetFloat("_BeatCols", 60f / deck.Track.bpm * rate);
            waveBlock.SetFloat("_FirstBeatCol", deck.Track.firstBeat * rate);
            waveform.SetPropertyBlock(waveBlock);
        }

        // "8A   -0:24": the key on the Camelot wheel (what matches what, see MixMatch) and how long until the song loops
        void UpdateInfo()
        {
            if (info == null) return;
            var clip = deck.IsLoaded ? deck.Source.clip : null;
            int left = clip == null ? -1 : Mathf.CeilToInt((clip.samples - deck.Source.timeSamples) / (float)clip.frequency / Mathf.Max(0.01f, deck.Rate));
            if (left == lastSecondsLeft) return;
            lastSecondsLeft = left;
            if (left < 0) { info.text = ""; return; }
            string key = deck.Track.HasKey ? $"KEY {MixMatch.Camelot(deck.Track)}" : "";
            info.text = $"{key}   -{left / 60}:{left % 60:00}";
        }

        void UpdateCountdown(int bar, int beatInBar, bool show)
        {
            if (countdown == null) return;
            if (!show || !deck.IsLoaded || !deck.IsPlaying) { countdown.text = ""; return; }
            string text = PhraseCue.Text(bar, beatInBar, !deck.Track.IsRecording, out var kind);
            countdown.color = kind == PhraseCue.Kind.HeadsUp ? headsUpColor : kind == PhraseCue.Kind.NewPhrase ? Color.white : countColor;
            // The 1 of a new phrase flashes big and bright for a beat
            countdown.text = kind == PhraseCue.Kind.NewPhrase ? $"<size=125%><b>{text}</b></size>" : text;
        }

        static string SectionName(TrackSynth.Section s) => s switch
        {
            TrackSynth.Section.Drop => "DROP",
            TrackSynth.Section.Groove => "GROOVE",
            TrackSynth.Section.Breakdown => "BREAKDOWN",
            _ => "BUILD",
        };
    }
}
