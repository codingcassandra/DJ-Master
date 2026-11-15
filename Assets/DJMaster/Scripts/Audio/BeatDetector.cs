using System;

namespace DJMaster
{
    // Where the beats are in a song: its tempo and where one beat lands. DJ software calls this the beatgrid.
    [Serializable]
    public struct BeatGrid
    {
        public float bpm;
        public float firstBeat;   // seconds into the song of a downbeat ("the one"), 0 .. one bar
        public float confidence;  // 0..1, how clearly the beat stood out (detection only)

        public float BeatSeconds => 60f / bpm;
        public float BarSeconds => 4f * 60f / bpm;

        // Keeps the same grid but puts its first downbeat at the earliest point in the song (0 .. one bar).
        public static float WrapToFirstBar(float seconds, float bpm)
        {
            float bar = 4f * 60f / bpm;
            float t = seconds % bar;
            return t < 0 ? t + bar : t;
        }
    }

    // Finds the tempo and beat phase of a recording. Pure managed code, so it runs on a worker thread.
    //   1. An onset curve: how suddenly the sound gets louder, every 256 samples, with the bass (kick drum) counting double.
    //   2. For each candidate tempo, fold the curve onto one beat. At the right tempo every kick (and hi-hat) lands in
    //      the same spot, so the fold is spiky; at a wrong tempo it smears flat. The spikiness is the fold's variance.
    //      (Just the tallest peak gets fooled by 5:4 tempos like 160 for a 128 song; the variance doesn't.)
    //      Coarse search, then a fine one.
    //   3. The peak of the winning fold is where the beats land. Which of the 4 beats is "the one" is up to the DJ
    //      (the tap-the-one screen), because that's musical, not something loudness tells you reliably.
    public static class BeatDetector
    {
        public const float MinBpm = 78f, MaxBpm = 160f; // dance music: house/techno sit at 118-135, this leaves room each side
        const int Hop = 256;
        const int Bins = 48;

        public static BeatGrid Analyze(float[] samples, int channels, int sampleRate)
        {
            float[] onset = OnsetCurve(samples, channels, sampleRate);
            double hopSec = (double)Hop / sampleRate;
            if (onset.Length < 64) return new BeatGrid { bpm = 120f, firstBeat = 0, confidence = 0 };

            double bestBpm = 120, bestScore = double.MinValue;
            for (double bpm = MinBpm; bpm <= MaxBpm; bpm += 0.25)
            {
                double s = FoldScore(onset, hopSec, bpm, out _);
                if (s > bestScore) { bestScore = s; bestBpm = bpm; }
            }
            for (double bpm = bestBpm - 0.3; bpm <= bestBpm + 0.3; bpm += 0.01)
            {
                double s = FoldScore(onset, hopSec, bpm, out _);
                if (s > bestScore) { bestScore = s; bestBpm = bpm; }
            }
            // Songs are almost always made at a whole (or half) BPM, so snap when it's that close
            double rounded = Math.Round(bestBpm * 2) / 2;
            if (Math.Abs(rounded - bestBpm) < 0.06) bestBpm = rounded;

            FoldScore(onset, hopSec, bestBpm, out double phase); // phase of the beat in seconds, 0 .. one beat
            return new BeatGrid
            {
                bpm = (float)bestBpm,
                firstBeat = (float)phase,
                // Real songs score 0.3-0.6 at their tempo, wrong tempos and noise about 0.02-0.04
                confidence = (float)Math.Max(0, Math.Min(1, (bestScore - 0.04) / 0.3)),
            };
        }

        // How sharply the onsets pile up when folded onto one beat at this tempo: the variance of the fold over its
        // mean squared (0 = perfectly flat, no beat). Also returns where the peak is, in seconds after a beat.
        static double FoldScore(float[] onset, double hopSec, double bpm, out double phaseSeconds)
        {
            double beat = 60.0 / bpm;
            var fold = new double[Bins];
            double total = 0;
            for (int i = 0; i < onset.Length; i++)
            {
                float v = onset[i];
                if (v <= 0) continue;
                double t = (i + 0.5) * hopSec;
                double p = t / beat;
                p -= Math.Floor(p);
                fold[(int)(p * Bins) % Bins] += v;
                total += v;
            }
            phaseSeconds = 0;
            if (total <= 0) return 0;
            double best = 0; int bestBin = 0;
            for (int b = 0; b < Bins; b++)
            {
                double s = fold[(b + Bins - 1) % Bins] + fold[b] + fold[(b + 1) % Bins];
                if (s > best) { best = s; bestBin = b; }
            }
            // Centre of mass around the peak, for a phase finer than one bin
            double l = fold[(bestBin + Bins - 1) % Bins], c = fold[bestBin], r = fold[(bestBin + 1) % Bins];
            double offset = (c + l + r) > 0 ? (r - l) / (c + l + r) : 0;
            double phaseBeats = (bestBin + 0.5 + offset) / Bins;
            phaseSeconds = (phaseBeats - Math.Floor(phaseBeats)) * beat;
            double mean = total / Bins, variance = 0;
            for (int b = 0; b < Bins; b++) variance += (fold[b] - mean) * (fold[b] - mean);
            return variance / Bins / (mean * mean);
        }

        // Positive changes in loudness (log energy), per hop. The low band gets extra weight because the kick drum
        // marks the beat in dance music.
        static float[] OnsetCurve(float[] samples, int channels, int sampleRate)
        {
            int frames = samples.Length / Math.Max(1, channels);
            int hops = frames / Hop;
            var curve = new float[hops];
            double lowCoef = 1.0 - Math.Exp(-2 * Math.PI * 150.0 / sampleRate); // one-pole low-pass at 150 Hz
            double low = 0, prevLowE = 0, prevE = 0;
            for (int h = 0; h < hops; h++)
            {
                double e = 0, le = 0;
                for (int i = 0; i < Hop; i++)
                {
                    int f = h * Hop + i;
                    double x = 0;
                    for (int c = 0; c < channels; c++) x += samples[f * channels + c];
                    x /= channels;
                    low += lowCoef * (x - low);
                    e += x * x;
                    le += low * low;
                }
                double logE = Math.Log(e + 1e-6), logLow = Math.Log(le + 1e-6);
                double d = Math.Max(0, logE - prevE) + 2 * Math.Max(0, logLow - prevLowE);
                curve[h] = h == 0 ? 0 : (float)d;
                prevE = logE; prevLowE = logLow;
            }
            // Keep only clear onsets: subtract a local average so steady sounds don't count
            var outCurve = new float[hops];
            const int w = 16;
            for (int h = 0; h < hops; h++)
            {
                double sum = 0; int n = 0;
                for (int k = Math.Max(0, h - w); k <= Math.Min(hops - 1, h + w); k++) { sum += curve[k]; n++; }
                outCurve[h] = (float)Math.Max(0, curve[h] - sum / n);
            }
            return outCurve;
        }

        // How far a tap can be from a detected beat and still snap to it. A fingertip is about this accurate.
        public const float SnapSeconds = 0.08f;

        // The tap-the-one screen: a tap picks bar 1. Returns the grid's first downbeat (0 .. one bar) for a tap heard at
        // this time. A tap close to a beat the detector heard snaps to it, so it lands exactly on the kick; otherwise
        // (or with no detection to go on) the tap itself is used.
        public static float DownbeatFromTap(float tapSeconds, BeatGrid heard)
        {
            double t = tapSeconds;
            if (heard.confidence > 0.1f)
            {
                double beat = heard.BeatSeconds;
                double snapped = heard.firstBeat + Math.Round((t - heard.firstBeat) / beat) * beat;
                if (Math.Abs(snapped - t) < SnapSeconds) t = snapped;
            }
            return BeatGrid.WrapToFirstBar((float)t, heard.bpm);
        }
    }
}
