using UnityEngine;

namespace DJMaster
{
    // Drives a venue's lasers, moving-head beams, LED panels and fireworks from the beat and crowd energy.
    // #35: the beams sweep in time with the music, the LED wall is a music visualizer (columns that jump with the
    // loudness and the beat, a sweep across on every bar), haze glows at the back, and blinders hit on the downbeats
    // and the drops. All unlit or additive, no real time shadows or post effects, so it stays cheap on Quest.
    public class LightShow : MonoBehaviour
    {
        public Transform[] lasers;          // long thin quads pivoted at the emitter
        public Renderer[] laserRenderers;
        public Transform[] beams;           // cones pivoted at the fixture
        public Renderer[] beamRenderers;
        public Renderer[] pulsers;          // LED walls, strips: flash on the beat
        public Light[] lights;
        public Transform spinner;           // mirror ball
        public ParticleSystem[] fireworks;
        [Tooltip("LED wall tiles, row by row from the bottom left. Drawn as a music visualizer.")]
        public Renderer[] wall = new Renderer[0];
        public int wallColumns = 1;
        [Tooltip("Big soft additive sheets: haze catching the light.")]
        public Renderer[] haze = new Renderer[0];
        [Tooltip("Blinders: white flashes on the downbeats when the room is up, and on drops.")]
        public Renderer[] blinders = new Renderer[0];
        public Color[] palette =
        {
            new Color(1f, 0.1f, 0.6f), new Color(0.1f, 0.6f, 1f), new Color(0.6f, 0.1f, 1f), new Color(0.1f, 1f, 0.5f),
        };

        Quaternion[] laserRest, beamRest;
        float[] lightBase;
        MaterialPropertyBlock block;
        float flash;

        void Awake()
        {
            laserRest = Rests(lasers);
            beamRest = Rests(beams);
            lightBase = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightBase[i] = lights[i].intensity;
        }

        void OnEnable() => BeatBus.Drop += OnDrop;
        void OnDisable() => BeatBus.Drop -= OnDrop;

        static Quaternion[] Rests(Transform[] ts)
        {
            var r = new Quaternion[ts.Length];
            for (int i = 0; i < ts.Length; i++) r[i] = ts[i].localRotation;
            return r;
        }

        void OnDrop()
        {
            flash = 1f;
            foreach (var fw in fireworks) if (fw != null) fw.Emit(60);
        }

        void Update()
        {
            block ??= new MaterialPropertyBlock();
            float e = BeatBus.Playing ? BeatBus.Energy * Mathf.Clamp01(BeatBus.Loudness * 2f) : 0f;
            float beat = (float)BeatBus.Beat;
            float pulse = BeatBus.Pulse;
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 2.5f);
            int bar = Mathf.FloorToInt(beat / 4f);
            Color main = palette[(bar / 4) % palette.Length];
            Color alt = palette[(bar / 4 + 1) % palette.Length];

            // Lasers fan out across the crowd; only once the energy is up.
            for (int i = 0; i < lasers.Length; i++)
            {
                float side = (i % 2 == 0 ? 1 : -1);
                float yaw = Mathf.Sin(beat * Mathf.PI * 0.25f + i * 0.7f) * 35f * side;
                float pitch = Mathf.Sin(beat * Mathf.PI * 0.125f + i) * 8f;
                lasers[i].localRotation = laserRest[i] * Quaternion.Euler(pitch, yaw, 0);
                float on = e > 0.45f ? Mathf.Clamp01((e - 0.45f) * 4f) : 0f;
                var c = (i % 2 == 0 ? main : alt) * (0.4f + 0.6f * pulse);
                c.a = on;
                block.SetColor("_Color", c);
                laserRenderers[i].SetPropertyBlock(block);
                laserRenderers[i].enabled = on > 0.01f;
            }

            // Moving heads sweep on the beat: one figure-eight every 8 beats, so they speed up and slow down with the
            // tempo, and swing wider as the energy rises. With no music they drift slowly.
            for (int i = 0; i < beams.Length; i++)
            {
                float t = BeatBus.Playing ? beat * Mathf.PI / 4f + i * 1.3f : Time.time * 0.3f + i * 1.3f;
                float reach = 0.6f + 0.4f * e;
                beams[i].localRotation = beamRest[i] * Quaternion.Euler(Mathf.Sin(t * 2f) * 14f * reach, Mathf.Sin(t) * 28f * reach, 0);
                var c = (i % 2 == 0 ? alt : main);
                c.a = 0.05f + 0.12f * e + 0.15f * pulse * e;
                block.SetColor("_Color", c);
                beamRenderers[i].SetPropertyBlock(block);
            }

            for (int i = 0; i < pulsers.Length; i++)
            {
                float k = 0.15f + 0.85f * Mathf.Max(pulse * (0.3f + e), flash);
                var c = Color.Lerp(main, alt, (i % 3) / 2f) * k;
                c.a = 1;
                block.SetColor("_Color", c);
                pulsers[i].SetPropertyBlock(block);
            }

            UpdateWall(beat, pulse, e, main, alt);

            for (int i = 0; i < haze.Length; i++)
            {
                var c = Color.Lerp(main, alt, i % 2) * (0.04f + 0.1f * e + 0.06f * pulse * e + 0.1f * flash);
                c.a = 1;
                block.SetColor("_Color", c);
                haze[i].SetPropertyBlock(block);
            }

            float blind = Mathf.Max(flash, e > 0.6f ? BeatBus.BarPulse * Mathf.Clamp01((e - 0.6f) * 3f) : 0f);
            for (int i = 0; i < blinders.Length; i++)
            {
                var c = new Color(1f, 0.92f, 0.8f) * (0.06f + 0.94f * blind);
                c.a = 1;
                block.SetColor("_Color", c);
                blinders[i].SetPropertyBlock(block);
            }

            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].color = i % 2 == 0 ? main : alt;
                lights[i].intensity = lightBase[i] * (0.3f + 0.7f * pulse * (0.4f + e) + flash);
            }

            if (spinner != null) spinner.Rotate(0, Time.deltaTime * 25f, 0, Space.Self);
        }

        // How high each column of the LED wall reaches, 0..1: the loudness, kicked up on the beat, each column a bit
        // different and reshuffled every beat so it dances. Pure math, so it's tested
        public static float ColumnLevel(int column, int beatIndex, float loudness, float pulse, float energy)
        {
            uint h = (uint)(column * 73856093) ^ (uint)(beatIndex * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            float jitter = (h & 1023) / 1023f;
            return Mathf.Clamp01(loudness * (0.45f + 0.55f * jitter) * (0.7f + 0.6f * energy) + 0.35f * pulse * loudness);
        }

        void UpdateWall(float beat, float pulse, float e, Color main, Color alt)
        {
            if (wall == null || wall.Length == 0) return;
            int cols = Mathf.Max(1, wallColumns), rows = Mathf.Max(1, wall.Length / cols);
            float loud = BeatBus.Playing ? Mathf.Clamp01(BeatBus.Loudness * 1.6f) : 0f;
            int beatIndex = Mathf.FloorToInt(beat);
            // A bright band that sweeps across the wall once a bar
            float sweep = ((beat % 4f + 4f) % 4f) / 4f * (cols + 2) - 1;
            for (int i = 0; i < wall.Length; i++)
            {
                if (wall[i] == null) continue;
                int col = i % cols, row = i / cols;
                float level = ColumnLevel(col, beatIndex, loud, pulse, e);
                bool lit = (row + 0.5f) / rows <= level;
                float band = Mathf.Clamp01(1f - Mathf.Abs(col - sweep)) * e;
                var c = Color.Lerp(main, alt, rows > 1 ? row / (rows - 1f) : 0f);
                float k = lit ? 0.55f + 0.45f * pulse : 0.07f;
                k = Mathf.Max(k, band * 0.8f, flash);
                c *= k;
                c.a = 1;
                block.SetColor("_Color", c);
                wall[i].SetPropertyBlock(block);
            }
        }
    }
}
