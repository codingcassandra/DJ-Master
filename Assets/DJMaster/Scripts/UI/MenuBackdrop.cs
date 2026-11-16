using UnityEngine;

namespace DJMaster
{
    // The start screen's living background (#40): big soft lights drifting slowly behind the glass menu, breathing with
    // the beat of the menu music, and the little EQ bars of the DJ MASTER wordmark bouncing along.
    public class MenuBackdrop : MonoBehaviour
    {
        [Tooltip("Soft round lights behind the menu.")]
        public Renderer[] lights;
        public Color[] lightColors;
        [Tooltip("How far each light wanders from its start, in local metres.")]
        public Vector2 drift = new Vector2(0.12f, 0.07f);
        [Tooltip("Seconds for one slow lap. Kept long so it never pulls the eye from the buttons.")]
        public float lapSeconds = 24f;
        [Tooltip("How much brighter the lights get on each beat.")]
        public float beatBoost = 0.35f;

        [Tooltip("The wordmark's EQ bars, left to right. They grow up from their base.")]
        public Transform[] bars;
        public float barMin = 0.25f, barMax = 1f;

        Vector3[] home;
        float[] barHeight;
        MaterialPropertyBlock block;

        void Awake() => Init();

        void Init()
        {
            if (home != null) return;
            home = new Vector3[lights.Length];
            for (int i = 0; i < lights.Length; i++) home[i] = lights[i].transform.localPosition;
            barHeight = new float[bars.Length];
            for (int i = 0; i < bars.Length; i++) barHeight[i] = bars[i].localScale.y;
            block = new MaterialPropertyBlock();
        }

        void Update() => Apply(Time.unscaledTime);

        // Public so the editor preview can render one frame of it.
        public void Apply(float t)
        {
            Init();
            float w = Mathf.PI * 2f / Mathf.Max(1f, lapSeconds);
            float beat = BeatBus.Playing ? BeatBus.Pulse : 0f;

            for (int i = 0; i < lights.Length; i++)
            {
                // Each light on its own slow Lissajous loop, so the pattern never visibly repeats
                float p = i * 2.1f;
                var offset = new Vector3(Mathf.Sin(t * w * (1f + i * 0.17f) + p) * drift.x, Mathf.Cos(t * w * (0.8f + i * 0.11f) + p * 1.3f) * drift.y, 0);
                lights[i].transform.localPosition = home[i] + offset;
                var c = lightColors[i % lightColors.Length];
                float breathe = 0.85f + 0.15f * Mathf.Sin(t * w * 3f + p);
                c.a *= breathe * (1f + beat * beatBoost);
                block.SetColor("_Color", c);
                lights[i].SetPropertyBlock(block);
            }

            for (int i = 0; i < bars.Length; i++)
            {
                // A little level meter: each bar has its own wobble, and they all jump on the kick
                float wobble = 0.5f + 0.5f * Mathf.Sin(t * (5f + i * 1.7f) + i * 1.9f);
                float k = Mathf.Lerp(barMin, barMax, Mathf.Clamp01(0.35f * wobble + 0.65f * beat * (0.6f + 0.4f * Mathf.Sin(i * 2.4f + 1f))));
                if (!BeatBus.Playing) k = barMin + (barMax - barMin) * 0.2f * wobble;
                var s = bars[i].localScale;
                bars[i].localScale = new Vector3(s.x, barHeight[i] * k, s.z);
            }
        }
    }
}
