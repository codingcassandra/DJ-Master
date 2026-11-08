using NUnit.Framework;

namespace DJMaster.Tests
{
    public class TrackSynthTests
    {
        [Test]
        public void LoopIsExactlySixteenBars()
        {
            var t = TrackLibrary.Tracks[0];
            float[] data = TrackSynth.Render(t);
            int frames = data.Length / 2;
            Assert.AreEqual(TrackSynth.FrameCount(t.bpm), frames);
            double beats = frames * (double)t.bpm / (60.0 * TrackSynth.SampleRate);
            Assert.AreEqual(64.0, beats, 0.01);
        }

        [Test]
        public void EveryTrackRendersLoudButNotClipping()
        {
            foreach (var t in TrackLibrary.Tracks)
            {
                if (t.IsRecording) continue;
                float[] data = TrackSynth.Render(t);
                float peak = 0; double sum = 0;
                foreach (float s in data)
                {
                    Assert.IsFalse(float.IsNaN(s), t.title);
                    peak = System.Math.Max(peak, System.Math.Abs(s));
                    sum += s * s;
                }
                double rms = System.Math.Sqrt(sum / data.Length);
                Assert.LessOrEqual(peak, 0.86f, t.title);
                Assert.Greater(rms, 0.08, t.title);
            }
        }

        [Test]
        public void KickLandsOnTheDownbeat()
        {
            var t = TrackLibrary.Tracks[0];
            float[] data = TrackSynth.Render(t);
            int beat = (int)(60.0 / t.bpm * TrackSynth.SampleRate);
            // Energy in the first 30 ms after a beat should beat the 30 ms just before the next one.
            double on = 0, off = 0;
            int win = TrackSynth.SampleRate * 30 / 1000;
            for (int i = 0; i < win; i++)
            {
                on += data[(beat * 4 + i) * 2] * data[(beat * 4 + i) * 2];
                off += data[(beat * 5 - win + i) * 2] * data[(beat * 5 - win + i) * 2];
            }
            Assert.Greater(on, off * 2);
        }

        [Test]
        public void SectionsFollowTheArrangement()
        {
            Assert.AreEqual(TrackSynth.Section.Drop, TrackSynth.SectionOfBar(0));
            Assert.AreEqual(TrackSynth.Section.Groove, TrackSynth.SectionOfBar(8));
            Assert.AreEqual(TrackSynth.Section.Breakdown, TrackSynth.SectionOfBar(12));
            Assert.AreEqual(TrackSynth.Section.Build, TrackSynth.SectionOfBar(15));
        }

        [Test]
        public void LessonTracksAreSetUp()
        {
            // The lesson depends on Deck A = 128 BPM and Deck B (track 1) being slower but reachable.
            Assert.AreEqual(128f, TrackLibrary.Tracks[0].bpm);
            Assert.Less(TrackLibrary.Tracks[1].bpm, 128f);
            Assert.Greater(TrackLibrary.Tracks[1].bpm * DJMath.TempoRate(1f), 128f);
        }
    }
}
