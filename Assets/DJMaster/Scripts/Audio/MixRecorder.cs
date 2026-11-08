using UnityEngine;

namespace DJMaster
{
    // "Record every set and listen back": keeps the last few seconds of the final mix (on the AudioListener) so
    // each live transition can be saved as a clip and replayed on the results screen.
    [RequireComponent(typeof(AudioListener))]
    public class MixRecorder : MonoBehaviour
    {
        public float bufferSeconds = 30f;
        [Tooltip("Plays recordings back. Recording pauses while it plays, so replays aren't re-recorded.")]
        public AudioSource replay;

        float[] ring;
        int writePos, channels = 2, sampleRate;
        long written;
        volatile bool replaying;
        readonly object gate = new();

        public bool Replaying => replay != null && replay.isPlaying;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            ring = new float[(int)(bufferSeconds * sampleRate) * 2];
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            if (ring == null || ch < 1 || replaying) return;
            lock (gate)
            {
                channels = Mathf.Min(ch, 2);
                int frames = data.Length / ch;
                for (int i = 0; i < frames; i++)
                {
                    ring[writePos] = data[i * ch];
                    ring[writePos + 1] = channels > 1 ? data[i * ch + 1] : data[i * ch];
                    writePos = (writePos + 2) % ring.Length;
                }
                written += frames;
            }
        }

        void Update() => replaying = Replaying;

        // The last `seconds` of the mix as a stereo clip, or null if nothing has been recorded.
        public AudioClip Snapshot(float seconds)
        {
            // The audio thread waits on this lock, so only the copy happens inside it. Allocating the buffer
            // and building the clip happen outside, otherwise the mix can drop out right after a transition.
            int frames = (int)Mathf.Min(seconds * sampleRate, Mathf.Min(System.Threading.Interlocked.Read(ref written), ring.Length / 2));
            if (frames <= 0) return null;
            var data = new float[frames * 2];
            lock (gate)
            {
                int start = (writePos - data.Length + ring.Length) % ring.Length;
                int first = Mathf.Min(data.Length, ring.Length - start);
                System.Array.Copy(ring, start, data, 0, first);
                if (first < data.Length) System.Array.Copy(ring, 0, data, first, data.Length - first);
            }
            var clip = AudioClip.Create("Recording", frames, 2, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(AudioClip clip)
        {
            if (replay == null || clip == null) return;
            replay.Stop();
            replay.clip = clip;
            replay.Play();
        }

        public void StopReplay() { if (replay != null) replay.Stop(); }
    }
}
