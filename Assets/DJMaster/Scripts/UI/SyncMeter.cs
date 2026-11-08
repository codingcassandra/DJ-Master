using TMPro;
using UnityEngine;

namespace DJMaster
{
    // Center screen: how far apart the two decks are in tempo and in beat phase.
    // The needle sits in the middle and the light turns green when the beats are locked.
    public class SyncMeter : MonoBehaviour
    {
        public MixJudge judge;
        public Mixer mixer;
        public TextMeshPro label;
        public Transform needle;        // moves along local X
        public float halfWidth = 0.045f;
        public Renderer lockLight;
        public Renderer energyFill;
        public Transform energyPivot;

        MaterialPropertyBlock block;
        string lastText;

        void Update()
        {
            block ??= new MaterialPropertyBlock();
            var a = mixer.decks[0];
            var b = mixer.decks[1];
            bool both = a.IsPlaying && b.IsPlaying;

            float phase = both ? judge.PhaseOffset : 0f;
            needle.localPosition = new Vector3(phase * 2f * halfWidth, needle.localPosition.y, needle.localPosition.z);

            Color c = !both ? new Color(0.1f, 0.1f, 0.12f) : judge.InSync ? new Color(0.1f, 1f, 0.4f) : Mathf.Abs(judge.BpmDifference) < 0.6f ? new Color(1f, 0.75f, 0.1f) : new Color(1f, 0.15f, 0.2f);
            block.SetColor("_Color", c);
            lockLight.SetPropertyBlock(block);

            string text;
            if (!a.HasTrack || !b.HasTrack) text = "SYNC";
            else
            {
                float d = Mathf.Round(judge.BpmDifference * 10f) / 10f;
                string phaseWord = !both ? "" : judge.InSync ? "LOCKED" : phase > 0 ? "B AHEAD" : "B BEHIND";
                text = $"BPM {(d > 0 ? "+" : "")}{d:0.0}\n<size=62.5%>{phaseWord}</size>";
            }
            if (text != lastText) { label.text = text; lastText = text; }

            if (energyPivot != null)
            {
                energyPivot.localScale = new Vector3(Mathf.Max(0.001f, BeatBus.Energy), 1, 1);
                block.SetColor("_Color", Color.Lerp(new Color(0.3f, 0.3f, 1f), new Color(1f, 0.2f, 0.7f), BeatBus.Energy));
                energyFill.SetPropertyBlock(block);
            }
        }
    }
}
