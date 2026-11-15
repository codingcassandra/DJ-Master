using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace DJMaster
{
    // Saved beatgrids, one per song, so finding the beat only happens once. Detection runs in the background.
    public static class BeatGridStore
    {
        static string Key(string id) => "grid:" + id;

        public static bool TryLoad(string id, out BeatGrid grid)
        {
            grid = default;
            var parts = PlayerPrefs.GetString(Key(id), "").Split(',');
            if (parts.Length != 2) return false;
            var inv = CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[0], NumberStyles.Float, inv, out grid.bpm) || grid.bpm < 40f) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, inv, out grid.firstBeat)) return false;
            grid.confidence = 1f;
            return true;
        }

        public static void Save(string id, BeatGrid grid)
        {
            var inv = CultureInfo.InvariantCulture;
            PlayerPrefs.SetString(Key(id), grid.bpm.ToString("R", inv) + "," + grid.firstBeat.ToString("R", inv));
            PlayerPrefs.Save();
        }

        public static void Forget(string id) => PlayerPrefs.DeleteKey(Key(id));

        // Puts a song's saved grid on it, if it has one. Returns true if it did.
        public static bool ApplySaved(int track)
        {
            if (!TryLoad(TrackLibrary.Tracks[track].GridId, out var g)) return false;
            TrackLibrary.SetGrid(track, g);
            return true;
        }

        // Main thread: copies the samples out of the clip, then finds the beat on a worker thread.
        public static Task<BeatGrid> DetectAsync(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            int channels = clip.channels, rate = clip.frequency;
            return Task.Run(() => BeatDetector.Analyze(data, channels, rate));
        }
    }
}
