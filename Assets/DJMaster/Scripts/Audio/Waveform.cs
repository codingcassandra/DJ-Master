using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DJMaster
{
    // The three-band waveform the deck screens scroll. Worked out once per song on a worker thread and kept in a small
    // texture: one texel per column (1/ColumnsPerSecond of a second), red = lows, green = mids, blue = highs, each the
    // loudest peak in that column scaled 0..1 for the song. The screen's shader (DJMaster/Waveform) does the rest.
    public static class Waveform
    {
        public const int ColumnsPerSecond = 80;
        public const int TextureWidth = 1024;
        public const float MaxSeconds = 6 * 60f; // the columns wrap into rows, so long songs stay inside the GPU's size limit

        // Columns per second for a clip at this sample rate, exactly (each column is a whole number of samples)
        public static float ColumnRate(int sampleRate) => sampleRate / (float)Mathf.Max(1, sampleRate / ColumnsPerSecond);

        static readonly Dictionary<AudioClip, (Texture2D tex, int columns)> textures = new();
        static readonly Dictionary<AudioClip, Task<Color32[]>> pending = new();

        // Main thread. Returns the song's waveform once it's ready, null while it's still being worked out.
        // columns is how many texels of it hold the song.
        public static Texture2D TryGet(AudioClip clip, out int columns)
        {
            columns = 0;
            if (clip == null) return null;
            if (textures.TryGetValue(clip, out var done) && done.tex != null) { columns = done.columns; return done.tex; }
            if (!pending.TryGetValue(clip, out var task))
            {
                if (clip.loadState != AudioDataLoadState.Loaded) return null;
                if (clip.length > MaxSeconds) return null; // a long song of your own: no waveform rather than a big copy in memory
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) return null; // streamed clips can't hand over their samples
                int channels = clip.channels, rate = clip.frequency;
                pending[clip] = task = Task.Run(() => Analyze(data, channels, rate));
            }
            if (!task.IsCompleted) return null;
            pending.Remove(clip);
            if (task.IsFaulted) { Debug.LogException(task.Exception); return null; }
            columns = task.Result.Length;
            var tex = ToTexture(task.Result, clip.name);
            textures[clip] = (tex, columns);
            return tex;
        }

        static Texture2D ToTexture(Color32[] cols, string name)
        {
            int h = Mathf.Max(1, (cols.Length + TextureWidth - 1) / TextureWidth);
            var pixels = new Color32[TextureWidth * h];
            Array.Copy(cols, pixels, cols.Length);
            var tex = new Texture2D(TextureWidth, h, TextureFormat.RGBA32, false, true)
            {
                name = "Waveform " + name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true); // no CPU copy needed after upload
            return tex;
        }

        // Pure math, safe on any thread. Splits the song into lows (under ~250 Hz), highs (over ~3 kHz) and the mids
        // between with one-pole filters, then takes each band's peak per column.
        public static Color32[] Analyze(float[] interleaved, int channels, int rate)
        {
            int frames = interleaved.Length / Mathf.Max(1, channels);
            int perCol = Mathf.Max(1, rate / ColumnsPerSecond);
            int n = Mathf.Max(1, (frames + perCol - 1) / perCol);
            var low = new float[n]; var mid = new float[n]; var high = new float[n];

            float aLow = 1f - Mathf.Exp(-2f * Mathf.PI * 250f / rate);
            float aHigh = 1f - Mathf.Exp(-2f * Mathf.PI * 3000f / rate);
            float lp1 = 0, lp2 = 0;
            for (int f = 0; f < frames; f++)
            {
                float x = 0;
                for (int c = 0; c < channels; c++) x += interleaved[f * channels + c];
                x /= channels;
                lp1 += aLow * (x - lp1);   // lows
                lp2 += aHigh * (x - lp2);  // everything under 3 kHz
                int col = f / perCol;
                low[col] = Mathf.Max(low[col], Mathf.Abs(lp1));
                mid[col] = Mathf.Max(mid[col], Mathf.Abs(lp2 - lp1));
                high[col] = Mathf.Max(high[col], Mathf.Abs(x - lp2));
            }

            var result = new Color32[n];
            float mLow = Peak(low), mMid = Peak(mid), mHigh = Peak(high);
            // Each band is scaled to its own loudest, the shader nests them (lows outside, highs inside)
            for (int i = 0; i < n; i++)
                result[i] = new Color32(Byte(low[i] / mLow), Byte(mid[i] / mMid), Byte(high[i] / mHigh), 255);
            return result;
        }

        // The loudest column, ignoring the top 0.5% so a single click doesn't flatten the rest
        static float Peak(float[] v)
        {
            var sorted = (float[])v.Clone();
            Array.Sort(sorted);
            return Mathf.Max(1e-4f, sorted[Mathf.Clamp((int)(sorted.Length * 0.995f), 0, sorted.Length - 1)]);
        }

        // A gentle curve so quiet parts still show, then 0..255
        static byte Byte(float v) => (byte)Mathf.RoundToInt(Mathf.Pow(Mathf.Clamp01(v), 0.7f) * 255f);
    }
}
