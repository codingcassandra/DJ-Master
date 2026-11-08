using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DJMaster
{
    // The crate of records. The first block is synthesized on a worker thread the first time it's needed;
    // the second block is real music released as CC0 (public domain) on OpenGameArt.org, credited in
    // Resources/Music/CREDITS.md. Each recording is a 16-bar excerpt cut to start exactly on beat 1,
    // so the decks treat it just like a synthesized loop.
    public static class TrackLibrary
    {
        public const int GameTrackCount = 8; // the synthesized tracks come first; the lessons use them by index

        public static readonly TrackInfo[] Tracks =
        {
            new("Neon Tides",       "Lumen Kid",       128f, 9,  Genre.House,  101),
            new("Velvet Static",    "Night Bus Audio", 124f, 2,  Genre.Disco,  202),
            new("Concrete Bloom",   "Ferro",           130f, 5,  Genre.Techno, 303),
            new("Sundial",          "Mira Okafor",     122f, 7,  Genre.House,  404),
            new("Glass Arcade",     "Pocket Rocket",   126f, 0,  Genre.Disco,  505),
            new("Low Orbit",        "Ferro",           128f, 4,  Genre.Techno, 606),
            new("Afterglow Avenue", "Lumen Kid",       125f, 11, Genre.House,  707),
            new("Pulse Garden",     "Mira Okafor",     127f, 3,  Genre.Techno, 808),

            TrackInfo.Recording("Funky House",        "Of Far Different Nature", 128f,       Genre.House,     "funky-house"),
            TrackInfo.Recording("Vengeance Electro",  "Of Far Different Nature", 128f,       Genre.Electro,   "vengeance-electro"),
            TrackInfo.Recording("Bouncer",            "Of Far Different Nature", 128f,       Genre.House,     "bouncer-0"),
            TrackInfo.Recording("Liquid Flame",       "Of Far Different Nature", 125f,       Genre.House,     "liquid-flame"),
            TrackInfo.Recording("Party Sector",       "Joth",                    120f,       Genre.Techno,    "party-sector"),
            TrackInfo.Recording("Slampe",             "Fupi",                    113.99998f, Genre.Synthwave, "slampe-synthwave-house"),
            TrackInfo.Recording("Espelhar",           "Fupi",                    140f,       Genre.EDM,       "espelhar-loud-melodic-edm"),
            TrackInfo.Recording("Nosferatu's Party",  "Cygnus",                  140f,       Genre.House,     "nosferatus-house-party"),
        };

        static readonly ConcurrentDictionary<int, Task<float[]>> renders = new();
        static readonly Dictionary<int, AudioClip> clips = new();
        static readonly Dictionary<int, ResourceRequest> loads = new();
        // Rendering all the songs at once spikes memory and CPU while you're on the menu, so only two run at a time.
        static readonly SemaphoreSlim renderSlots = new(2);

        static bool HasClip(int index) => clips.TryGetValue(index, out var c) && c != null;

        // Main thread only. Synthesized tracks render in the background; recordings (and songs whose clip
        // already exists) have nothing to render, so they return a finished task with no data.
        public static Task<float[]> Render(int index)
        {
            if (Tracks[index].IsRecording || HasClip(index)) return Task.FromResult<float[]>(null);
            return renders.GetOrAdd(index, i => Task.Run(async () =>
            {
                await renderSlots.WaitAsync();
                try { return TrackSynth.Render(Tracks[i]); }
                finally { renderSlots.Release(); }
            }));
        }

        // Start rendering and loading everything in the background so LOAD is instant later.
        public static void Prewarm()
        {
            for (int i = 0; i < Tracks.Length; i++)
            {
                if (Tracks[i].IsRecording) StartLoad(i);
                else Render(i);
            }
        }

        static void StartLoad(int index)
        {
            if (!loads.ContainsKey(index)) loads[index] = Resources.LoadAsync<AudioClip>("Music/" + Tracks[index].clip);
        }

        // Main thread only. Returns null until the track is ready to play.
        public static AudioClip TryGetClip(int index)
        {
            if (clips.TryGetValue(index, out var clip) && clip != null) return clip;
            return Tracks[index].IsRecording ? TryGetRecording(index) : TryGetRendered(index);
        }

        static AudioClip TryGetRecording(int index)
        {
            StartLoad(index);
            var req = loads[index];
            if (!req.isDone) return null;
            var clip = req.asset as AudioClip;
            if (clip == null) { Debug.LogError($"[DJMaster] Missing recording Resources/Music/{Tracks[index].clip}"); return null; }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return null;
            clips[index] = clip;
            return clip;
        }

        static AudioClip TryGetRendered(int index)
        {
            var task = Render(index);
            if (!task.IsCompleted) return null;
            if (task.IsFaulted) { Debug.LogException(task.Exception); return null; }

            float[] data = task.Result;
            var clip = AudioClip.Create(Tracks[index].title, data.Length / 2, 2, TrackSynth.SampleRate, false);
            clip.SetData(data, 0);
            clips[index] = clip;
            // The clip keeps its own copy of the samples, so let go of the render's (about 11 MB per song).
            renders.TryRemove(index, out _);
            return clip;
        }
    }
}
