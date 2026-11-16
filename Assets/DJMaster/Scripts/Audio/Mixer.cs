using UnityEngine;

namespace DJMaster
{
    // Two-channel DJ mixer. Reads control values each frame and writes the final gain into each ChannelStrip.
    public class Mixer : MonoBehaviour
    {
        public Deck[] decks = new Deck[2];
        [Range(0, 1)] public float[] channelFader = { 0f, 0f };
        [Range(0, 1)] public float crossfader = 0.5f;
        public bool[] headphoneCue = { false, false };
        [Tooltip("Level of a cued (headphone) channel that's otherwise silent, so you can preview it.")]
        public float cueLevel = 0.35f;
        [Tooltip("Quiet music on the start screens: deck A at this level, whatever the board's faders say. Below 0 is off.")]
        public float menuMusic = -1f;

        // What the crowd hears from each deck (ignores headphone cue).
        public float CrowdGain(int i)
        {
            var d = decks[i];
            if (d == null || !d.IsPlaying) return 0f;
            return DJMath.FaderGain(channelFader[i]) * DJMath.CrossfaderGain(crossfader, i);
        }

        // The deck the crowd mostly hears right now, or -1 if the room is silent.
        public int DominantDeck
        {
            get
            {
                float a = CrowdGain(0) * EqWeight(0), b = CrowdGain(1) * EqWeight(1);
                if (a < 0.05f && b < 0.05f) return -1;
                return a >= b ? 0 : 1;
            }
        }

        // Bass carries most of the energy, so a deck with its low cut counts for less.
        float EqWeight(int i) => 0.35f + 0.65f * Mathf.Min(1f, DJMath.EqGain(decks[i].Strip.low));

        void Update()
        {
            for (int i = 0; i < decks.Length; i++)
            {
                if (decks[i] == null) continue;
                if (menuMusic >= 0f) { decks[i].Strip.gain = i == 0 ? menuMusic : 0f; continue; }
                float crowd = CrowdGain(i);
                float cue = headphoneCue[i] && decks[i].IsPlaying ? cueLevel : 0f;
                decks[i].Strip.gain = Mathf.Max(crowd, cue);
            }
        }
    }
}
