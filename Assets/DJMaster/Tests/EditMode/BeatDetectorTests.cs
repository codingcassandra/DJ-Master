using NUnit.Framework;

namespace DJMaster.Tests
{
    public class BeatDetectorTests
    {
        // A song that doesn't start on beat 1: some silence first, like most real songs.
        static float[] WithLeadIn(float[] stereo, float seconds)
        {
            int pad = (int)(seconds * TrackSynth.SampleRate) * 2;
            var x = new float[stereo.Length + pad];
            System.Array.Copy(stereo, 0, x, pad, stereo.Length);
            return x;
        }

        // Distance between two beat phases, in seconds, wrapping around the beat.
        static float PhaseError(float a, float b, float beat)
        {
            float d = (a - b) % beat;
            if (d < 0) d += beat;
            return System.Math.Min(d, beat - d);
        }

        [Test]
        public void FindsTempoAndBeatOfEverySynthTrack()
        {
            float[] leadIns = { 0f, 0.37f, 1.913f, 0.05f };
            for (int i = 0; i < TrackLibrary.GameTrackCount; i++)
            {
                var t = TrackLibrary.Tracks[i];
                float lead = leadIns[i % leadIns.Length];
                var grid = BeatDetector.Analyze(WithLeadIn(TrackSynth.Render(t), lead), 2, TrackSynth.SampleRate);
                Assert.AreEqual(t.bpm, grid.bpm, 0.5f, $"{t.title} BPM");
                Assert.Less(PhaseError(grid.firstBeat, lead, 60f / t.bpm), 0.02f, $"{t.title}: beat at {grid.firstBeat:0.000}s, expected {lead:0.000}s");
                Assert.Greater(grid.confidence, 0.2f, t.title);
            }
        }

        [Test]
        public void SilenceHasNoConfidence()
        {
            var grid = BeatDetector.Analyze(new float[44100 * 2 * 5], 2, 44100);
            Assert.AreEqual(0f, grid.confidence, 1e-6f);
        }

        [Test]
        public void TapSnapsToTheNearestBeatAndBecomesBarOne()
        {
            var grid = new BeatGrid { bpm = 120f, firstBeat = 0.1f, confidence = 1 }; // beats at 0.1, 0.6, 1.1 ... bars of 2 s
            Assert.AreEqual(0.6f, BeatDetector.DownbeatFromTap(0.62f, grid), 1e-4f, "a tap just after a beat");
            Assert.AreEqual(1.1f, BeatDetector.DownbeatFromTap(13.05f, grid), 1e-4f, "13.1 s is 6 bars after 1.1 s");
            Assert.AreEqual(1.6f, BeatDetector.DownbeatFromTap(-0.35f, grid), 1e-4f, "snaps to the beat at -0.4 s, one bar before 1.6 s");
            Assert.AreEqual(0.85f, BeatDetector.DownbeatFromTap(0.85f, grid), 1e-4f, "a tap between beats is taken as it is");
            grid.confidence = 0;
            Assert.AreEqual(0.62f, BeatDetector.DownbeatFromTap(0.62f, grid), 1e-4f, "nothing detected: no snapping");
            Assert.AreEqual(1.9f, BeatGrid.WrapToFirstBar(-0.1f, 120f), 1e-4f);
        }
    }
}
