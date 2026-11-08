using TMPro;
using UnityEngine;

namespace DJMaster
{
    // The little screen above each deck: track, BPM, tempo, bar counter, phrase progress and beat lights.
    public class DeckDisplay : MonoBehaviour
    {
        public Deck deck;
        public TextMeshPro title, bpm, position;
        public Renderer[] beatLights = new Renderer[4];
        public Transform phraseFill;     // scaled on X from 0..1 across the 16-bar loop
        public Renderer levelFill;
        public Transform levelPivot;
        public Color beatOn = new Color(1f, 0.55f, 0.1f);
        public Color downbeatOn = new Color(1f, 0.15f, 0.3f);
        public Color beatOff = new Color(0.08f, 0.08f, 0.1f);

        MaterialPropertyBlock block;
        int lastBar = -1, lastBeatInBar = -1, lastTrack = -2;
        float lastBpm = -1, lastTempo = -99;

        void Update()
        {
            block ??= new MaterialPropertyBlock();

            if (deck.TrackIndex != lastTrack || (deck.IsLoading && Time.frameCount % 15 == 0))
            {
                lastTrack = deck.TrackIndex;
                title.text = !deck.HasTrack ? "NO TRACK  -  tap LOAD"
                    : deck.IsLoading ? "LOADING..."
                    : $"{deck.Track.title}\n<size=85%>{deck.Track.artist}   {deck.Track.genre}   {(deck.Track.IsRecording ? deck.Track.license : deck.Track.Key)}</size>";
            }

            float b = Mathf.Round(deck.BPM * 10f) / 10f;
            float t = Mathf.Round(deck.TempoPercent * 10f) / 10f;
            if (b != lastBpm || t != lastTempo)
            {
                lastBpm = b; lastTempo = t;
                bpm.text = deck.HasTrack ? $"{b:0.0} BPM  <size=62.5%>{(t >= 0 ? "+" : "")}{t:0.0}%</size>" : "--- BPM";
            }

            int bar = deck.Bar, beatInBar = deck.BeatInBar;
            if (bar != lastBar || beatInBar != lastBeatInBar)
            {
                lastBar = bar; lastBeatInBar = beatInBar;
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

            if (levelPivot != null)
            {
                float lvl = Mathf.Clamp01(deck.Strip.Level);
                levelPivot.localScale = new Vector3(1, Mathf.Max(0.001f, lvl), 1);
                block.SetColor("_Color", lvl > 0.95f ? Color.red : Color.Lerp(new Color(0.1f, 0.9f, 0.4f), new Color(1f, 0.8f, 0.1f), lvl));
                levelFill.SetPropertyBlock(block);
            }
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
