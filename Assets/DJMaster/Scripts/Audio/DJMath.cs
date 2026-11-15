using System;

namespace DJMaster
{
    // The small pieces of mixer math, kept pure so they can be unit tested.
    public static class DJMath
    {
        public const float TempoRange = 0.08f; // tempo fader covers +/-8%, like most DJ gear

        // Channel faders use an audio taper so the top half of the throw does most of the work.
        public static float FaderGain(float v) => Clamp01(v) * Clamp01(v);

        // Constant-power crossfader: 0 = all Deck A, 1 = all Deck B, center = both at -3dB.
        public static float CrossfaderGain(float x, int deck)
        {
            x = Clamp01(x);
            double angle = x * Math.PI * 0.5;
            return (float)(deck == 0 ? Math.Cos(angle) : Math.Sin(angle));
        }

        // EQ knob: 0 = kill, 0.5 = unity, 1 = +6dB.
        public static float EqGain(float v)
        {
            v = Clamp01(v);
            if (v < 0.5f) { float t = v / 0.5f; return t * t; }
            return 1f + (v - 0.5f) * 2f;
        }

        // GAIN (trim) knob: 0 = -12 dB, 0.5 = 0 dB, 1 = +12 dB. Songs are mastered at different volumes, and DJs use this
        // to bring each one to the same level before it goes out to the room.
        public const float TrimRangeDb = 12f;
        public static float TrimGain(float v) => (float)Math.Pow(10.0, (Clamp01(v) - 0.5f) * 2f * TrimRangeDb / 20.0);
        public static float TrimFor(float gain) => Clamp01(0.5f + (float)(20.0 * Math.Log10(Math.Max(1e-6, gain))) / (2f * TrimRangeDb));

        public static float ToDb(float level) => (float)(20.0 * Math.Log10(Math.Max(1e-6, level)));

        // The channel level meter: 8 lights from the bottom. 4 green, 3 yellow, and the top one red, which means the
        // channel is clipping (full scale). A good level has the peaks bouncing in the yellow and never touching red.
        public const float ClipLevel = 0.999f;
        public static readonly float[] MeterDb = { -30f, -21f, -15f, -10f, -6f, -3.5f, -1.5f, 0f };
        public const int MeterGreen = 4, MeterYellow = 3;
        public static float YellowLevel => (float)Math.Pow(10.0, MeterDb[MeterGreen] / 20.0); // -6 dB, about 0.5

        // How many meter lights a peak level turns on (0..8).
        public static int MeterLights(float level)
        {
            if (level >= ClipLevel) return MeterDb.Length;
            float db = ToDb(level);
            int n = 0;
            for (int i = 0; i < MeterDb.Length - 1; i++) if (db >= MeterDb[i]) n = i + 1;
            return n;
        }

        public enum Zone { Low, Good, Red }
        // Where a peak sits on the meter: below the yellow, in the yellow (just right), or in the red.
        public static Zone LevelZone(float peak) => peak >= ClipLevel ? Zone.Red : peak >= YellowLevel ? Zone.Good : Zone.Low;

        // Tempo fader 0..1 (0.5 = center) to playback rate.
        public static float TempoRate(float fader01) => 1f + (Clamp01(fader01) - 0.5f) * 2f * TempoRange;

        // The tempo fader position that gives a playback rate (the inverse of TempoRate). Not clamped,
        // so callers can tell when a rate is out of the fader's range.
        public static float TempoFaderFor(float rate) => 0.5f + (rate - 1f) / (2f * TempoRange);

        // BPM from tap times in seconds: the average gap between taps. 0 until there are at least 3 steady taps.
        public static float BpmFromTaps(System.Collections.Generic.IReadOnlyList<float> times)
        {
            if (times == null || times.Count < 3) return 0f;
            float span = times[times.Count - 1] - times[0];
            float avg = span / (times.Count - 1);
            for (int i = 1; i < times.Count; i++)
                if (Math.Abs(times[i] - times[i - 1] - avg) > avg * 0.25f) return 0f; // too uneven to trust
            return avg > 0 ? 60f / avg : 0f;
        }

        // Signed beat-phase offset of B relative to A, in beats, wrapped to [-0.5, 0.5).
        public static float PhaseOffset(double beatA, double beatB)
        {
            double d = (beatB - beatA) % 1.0;
            if (d < -0.5) d += 1.0;
            if (d >= 0.5) d -= 1.0;
            return (float)d;
        }

        // How far a beat position is from the nearest whole beat, in beats (0..0.5).
        public static float DistanceToBeat(double beat)
        {
            double f = beat - Math.Floor(beat);
            return (float)Math.Min(f, 1.0 - f);
        }

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
