using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    public class WaveformTests
    {
        static float[] Sine(float hz, float seconds, int rate = 44100)
        {
            int n = (int)(seconds * rate);
            var data = new float[n * 2];
            for (int i = 0; i < n; i++)
                data[2 * i] = data[2 * i + 1] = 0.8f * Mathf.Sin(2 * Mathf.PI * hz * i / rate);
            return data;
        }

        [Test]
        public void OneColumnPerSlice_AtTheColumnRate()
        {
            var cols = Waveform.Analyze(Sine(440, 2f), 2, 44100);
            Assert.AreEqual(Mathf.CeilToInt(2f * Waveform.ColumnRate(44100)), cols.Length, 1);
        }

        [Test]
        public void ABassNoteLightsTheLows_AHiHatTheHighs()
        {
            // Each band is scaled to its own peak, so compare a bass and a hiss within the same song
            var bass = Sine(60, 1f);
            var hat = Sine(9000, 1f);
            var song = new float[bass.Length + hat.Length];
            bass.CopyTo(song, 0);
            hat.CopyTo(song, bass.Length);
            var cols = Waveform.Analyze(song, 2, 44100);
            var first = cols[cols.Length / 4];      // in the bass half
            var second = cols[cols.Length * 3 / 4]; // in the hi-hat half
            Assert.Greater(first.r, 200, "a 60 Hz note is all lows");
            Assert.Less(first.b, 40, "and no highs");
            Assert.Greater(second.b, 200, "a 9 kHz tone is all highs");
            Assert.Less(second.r, 40, "and no lows");
        }

        [Test]
        public void SilenceIsFlat()
        {
            var cols = Waveform.Analyze(new float[44100 * 2], 2, 44100);
            foreach (var c in cols) Assert.AreEqual(0, c.r + c.g + c.b);
        }
    }
}
