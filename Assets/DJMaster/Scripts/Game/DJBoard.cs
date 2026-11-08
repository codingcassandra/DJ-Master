using System;
using UnityEngine;

namespace DJMaster
{
    [Serializable]
    public class DeckControls
    {
        public Deck deck;
        public PokeButton play, cue, load, headphones, sync, loop, echo;
        public Fader tempo, volume;
        public Knob high, mid, low, filter;
        public JogWheel jog;
        public PokeButton[] hotCues = new PokeButton[4];   // jump to bar 1, 5, 9, 13
        public PokeButton[] samplers = new PokeButton[4];  // crowd sounds, see Sfx.SampleNames
    }

    // Connects the physical controls to the decks and mixer, and keeps button lights in sync.
    public class DJBoard : MonoBehaviour
    {
        public Mixer mixer;
        public DeckControls[] channels = new DeckControls[2];
        public Fader crossfader;
        public PokeButton tap;
        [Tooltip("Deck A's HORN sampler pad, the one the lessons point at.")]
        public PokeButton airHorn;

        public event System.Action HornPressed; // either deck's HORN pad

        // TAP tempo: the BPM of your last few taps, 0 until they're steady.
        public float TapBpm { get; private set; }
        readonly System.Collections.Generic.List<float> taps = new();

        // Why the last SYNC press didn't fully work, or "" if it did.
        public string SyncMessage => Time.time - syncMessageAt < 4f ? syncMessage : "";
        string syncMessage = "";
        float syncMessageAt = -10f;

        public DJControl[] AllControls { get; private set; }

        public DeckControls A => channels[0];
        public DeckControls B => channels[1];

        void Awake()
        {
            AllControls = GetComponentsInChildren<DJControl>(true);
            for (int i = 0; i < channels.Length; i++)
            {
                var ch = channels[i];
                int idx = i;
                ch.play.Pressed += ch.deck.TogglePlay;
                ch.cue.Pressed += ch.deck.Cue;
                ch.load.Pressed += () => ch.deck.LoadNext(channels[1 - idx].deck.TrackIndex);
                ch.headphones.Pressed += () => mixer.headphoneCue[idx] = !mixer.headphoneCue[idx];
                for (int k = 0; k < ch.hotCues.Length; k++)
                {
                    int bar = k * 4;
                    ch.hotCues[k].Pressed += () => ch.deck.HotCue(bar);
                }
                for (int k = 0; k < ch.samplers.Length; k++)
                {
                    int sample = k;
                    ch.samplers[k].Pressed += () => Sfx.Sample(sample);
                }
                ch.samplers[0].Pressed += () => HornPressed?.Invoke();
                ch.sync.Pressed += () => Sync(idx);
                ch.loop.Pressed += ch.deck.ToggleLoop;
                ch.echo.Pressed += () => ch.deck.Strip.echo = !ch.deck.Strip.echo;
            }
            tap.Pressed += () =>
            {
                float now = Time.time;
                if (taps.Count > 0 && now - taps[taps.Count - 1] > 2f) taps.Clear(); // a long pause starts a new count
                taps.Add(now);
                if (taps.Count > 5) taps.RemoveAt(0);
                TapBpm = DJMath.BpmFromTaps(taps);
            };
        }

        void Update()
        {
            for (int i = 0; i < channels.Length; i++)
            {
                var ch = channels[i];
                ch.deck.tempoFader = ch.tempo.value;
                mixer.channelFader[i] = ch.volume.value;
                ch.deck.Strip.high = ch.high.value;
                ch.deck.Strip.mid = ch.mid.value;
                ch.deck.Strip.low = ch.low.value;
                ch.deck.Strip.filter = ch.filter.value;

                ch.play.Lit = ch.deck.IsPlaying && (ch.deck.Beat % 1.0) < 0.5; // blinks on the beat
                ch.cue.Lit = ch.deck.IsLoaded && !ch.deck.IsPlaying;
                ch.load.Lit = ch.deck.IsLoading && Time.time % 0.4f < 0.2f;
                ch.headphones.Lit = mixer.headphoneCue[i];
                ch.loop.Lit = ch.deck.Looping && (ch.deck.Beat % 1.0) < 0.5;
                ch.echo.Lit = ch.deck.Strip.echo;
                var other = channels[1 - i].deck;
                ch.sync.Lit = ch.deck.IsPlaying && other.IsPlaying && Mathf.Abs(ch.deck.BPM - other.BPM) < 0.1f
                              && Mathf.Abs(DJMath.PhaseOffset(other.Beat, ch.deck.Beat)) < 0.03f;
                for (int k = 0; k < ch.hotCues.Length; k++) ch.hotCues[k].Lit = ch.deck.IsLoaded && ch.deck.Bar / 4 == k; // the phrase you're in
            }
            mixer.crossfader = crossfader.value;
            int dom = mixer.DominantDeck;
            tap.Lit = dom >= 0 && (channels[dom].deck.Beat % 1.0) < 0.15;
        }

        // SYNC: match this deck's tempo to the other deck, and if both are playing, line up the beats too.
        public void Sync(int i)
        {
            var me = channels[i];
            var other = channels[1 - i].deck;
            syncMessageAt = Time.time;
            if (!me.deck.IsLoaded || !other.IsLoaded) { syncMessage = "Load both decks first."; return; }
            float fader = DJMath.TempoFaderFor(other.BPM / me.deck.Track.bpm);
            syncMessage = fader < 0 || fader > 1 ? "Too far apart to sync: the tempo fader only reaches 8%." : "";
            me.tempo.SetValue(fader);
            me.deck.tempoFader = me.tempo.value; // apply now, so the beats line up at the new speed
            if (me.deck.IsPlaying && other.IsPlaying) me.deck.AlignTo(other);
        }

        public DJControl Find(string id)
        {
            foreach (var c in AllControls) if (c.id == id) return c;
            return null;
        }

        public void ClearHighlights()
        {
            foreach (var c in AllControls) c.Highlighted = false;
        }

        // Put every control back to a known state: faders down, EQs centered, tempo at 0%.
        public void ResetControls()
        {
            foreach (var ch in channels)
            {
                ch.tempo.SetValue(0.5f);
                ch.volume.SetValue(0f);
                ch.high.SetValue(0.5f); ch.mid.SetValue(0.5f); ch.low.SetValue(0.5f); ch.filter.SetValue(0.5f);
            }
            crossfader.SetValue(0.5f);
            mixer.headphoneCue[0] = mixer.headphoneCue[1] = false;
            foreach (var ch in channels)
            {
                ch.deck.Strip.echo = false;
                if (ch.deck.Looping) ch.deck.ToggleLoop();
            }
        }
    }
}
