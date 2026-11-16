using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DJMaster
{
    // The crate of records. The first block is synthesized on a worker thread the first time it's needed;
    // the second block is real music released as CC0 (public domain) on OpenGameArt.org, credited in
    // Resources/Music/CREDITS.md. Each recording is a 16-bar excerpt cut to start exactly on beat 1,
    // so the decks treat it just like a synthesized loop.
    public static class TrackLibrary
    {
        public const int GameTrackCount = 8; // the synthesized tracks come first; the lessons use them by index

        // The built-in songs first (the lessons, the daily challenge and the bonus song use them by index), then the songs
        // you added yourself (MyMusic), which come and go with the files in your folder
        public static TrackInfo[] Tracks =
        {
            new("Neon Tides",       "Lumen Kid",       128f, 9,  Genre.House,  101),
            new("Velvet Static",    "Night Bus Audio", 124f, 2,  Genre.Disco,  202),
            new("Concrete Bloom",   "Ferro",           130f, 5,  Genre.Techno, 303),
            new("Sundial",          "Mira Okafor",     122f, 7,  Genre.House,  404),
            new("Glass Arcade",     "Pocket Rocket",   126f, 0,  Genre.Disco,  505),
            new("Low Orbit",        "Ferro",           128f, 4,  Genre.Techno, 606),
            new("Afterglow Avenue", "Lumen Kid",       125f, 11, Genre.House,  707),
            new("Pulse Garden",     "Mira Okafor",     127f, 3,  Genre.Techno, 808),

            // Keys for the recordings are estimated from the audio (chroma + key profiles), double check by ear.
            // Party Sector has no clear third so it could be F#m. Espelhar and Nosferatu's Party came out too
            // close to call (Espelhar leans G or Bm, Nosferatu leans Em), so they stay unknown for now
            TrackInfo.Recording("Funky House",        "Of Far Different Nature", 128f,       Genre.House,     "funky-house",               root: 4),  // Em
            TrackInfo.Recording("Vengeance Electro",  "Of Far Different Nature", 128f,       Genre.Electro,   "vengeance-electro",         root: 9),  // Am
            TrackInfo.Recording("Bouncer",            "Of Far Different Nature", 128f,       Genre.House,     "bouncer-0",                 root: 9),  // Am
            TrackInfo.Recording("Liquid Flame",       "Of Far Different Nature", 125f,       Genre.House,     "liquid-flame",              root: 9),  // Am
            TrackInfo.Recording("Party Sector",       "Joth",                    120f,       Genre.Techno,    "party-sector",              root: 6, major: true), // F#
            TrackInfo.Recording("Slampe",             "Fupi",                    113.99998f, Genre.Synthwave, "slampe-synthwave-house",    root: 0, major: true), // C
            TrackInfo.Recording("Espelhar",           "Fupi",                    140f,       Genre.EDM,       "espelhar-loud-melodic-edm"),
            TrackInfo.Recording("Nosferatu's Party",  "Cygnus",                  140f,       Genre.House,     "nosferatus-house-party"),

            // Unlocked by a daily challenge streak (see DailyChallenge). Synthesized like the game tracks.
            new("Streak Rider",     "DJ Master Crew",  126f, 2,  Genre.House,  909),
        };

        public static readonly int BuiltInCount = Tracks.Length;
        public static int BonusTrack => BuiltInCount - 1; // the last built-in song, whatever you've added after it
        public static bool IsUser(int index) => index >= BuiltInCount && index < Tracks.Length;

        // ---- Your own songs (#22) ----

        // Longest song we'll load: decoded, a song takes about 10 MB a minute of memory
        public const float MaxUserSeconds = 12 * 60f;
        // Seconds of a song the beat finder listens to
        public const float DetectSeconds = 90f;

        static readonly Dictionary<int, UnityWebRequest> userLoads = new();
        static readonly Dictionary<int, Task<BeatGrid>> userDetects = new();
        static readonly Dictionary<int, AudioClip> userPending = new();
        static readonly Dictionary<int, string> errors = new();

        // Why a song couldn't load, or null
        public static string LoadError(int index) => errors.TryGetValue(index, out var e) ? e : null;

        // Fires after your songs change, with where each old index went (-1: the file's gone). Decks and the browser
        // follow their songs to the new place
        public static event Action<Func<int, int>> UserTracksChanged;

        // Replaces the songs you added with this list (from a folder scan). The built-in songs keep their place and grids;
        // a song that's still there keeps its loaded audio and beat, one that's gone is let go
        public static void SetUserTracks(IList<TrackInfo> songs)
        {
            var old = Tracks;
            var all = new TrackInfo[BuiltInCount + songs.Count];
            Array.Copy(old, all, BuiltInCount);
            var newIndexOfPath = new Dictionary<string, int>();
            for (int i = 0; i < songs.Count; i++)
            {
                all[BuiltInCount + i] = songs[i];
                newIndexOfPath[songs[i].path] = BuiltInCount + i;
            }
            var map = new Dictionary<int, int>();
            for (int i = BuiltInCount; i < old.Length; i++)
                map[i] = newIndexOfPath.TryGetValue(old[i].path, out int n) ? n : -1;

            // Move what we know about each song along with it
            var oldClips = new Dictionary<int, AudioClip>();
            foreach (var kv in map) if (clips.TryGetValue(kv.Key, out var c)) { oldClips[kv.Key] = c; clips.Remove(kv.Key); }
            var oldPending = new Dictionary<int, AudioClip>(userPending);
            foreach (var kv in userLoads) kv.Value.Dispose();
            userLoads.Clear(); userDetects.Clear(); userPending.Clear();
            var oldErrors = new Dictionary<int, string>(errors);
            foreach (var kv in map) errors.Remove(kv.Key);
            foreach (var kv in map)
            {
                int n = kv.Value;
                if (n >= 0)
                {
                    all[n].bpm = old[kv.Key].bpm;
                    all[n].firstBeat = old[kv.Key].firstBeat;
                    if (oldClips.TryGetValue(kv.Key, out var c)) clips[n] = c;
                    if (oldErrors.TryGetValue(kv.Key, out var e)) errors[n] = e;
                }
                else
                {
                    if (oldClips.TryGetValue(kv.Key, out var c) && c != null) UnityEngine.Object.Destroy(c);
                }
                if (oldPending.TryGetValue(kv.Key, out var p) && p != null) UnityEngine.Object.Destroy(p); // half loaded: starts again
            }
            Tracks = all;
            UserTracksChanged?.Invoke(i => i < BuiltInCount ? i : map.TryGetValue(i, out int n) ? n : -1);
        }

        public static AudioType AudioTypeOf(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".wav" => AudioType.WAV,
            _ => AudioType.UNKNOWN,
        };

        // Main thread. Loads the file, then finds its beat (or uses the saved grid), and only then hands the clip over,
        // because the decks need the BPM to play it in time. Anything wrong ends up in LoadError instead of a crash
        static AudioClip TryGetUser(int index)
        {
            if (errors.ContainsKey(index)) return null;
            var t = Tracks[index];
            if (!userPending.TryGetValue(index, out var clip))
            {
                if (!userLoads.TryGetValue(index, out var req))
                {
                    try
                    {
                        var info = new System.IO.FileInfo(t.path);
                        if (!info.Exists) { errors[index] = "The file isn't there any more."; return null; }
                        if (AudioTypeOf(t.path) == AudioType.UNKNOWN) { errors[index] = "Only MP3, OGG and WAV files play."; return null; }
                        req = UnityWebRequestMultimedia.GetAudioClip(new Uri(t.path).AbsoluteUri, AudioTypeOf(t.path));
                        ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                        ((DownloadHandlerAudioClip)req.downloadHandler).compressed = false; // the decks, meters and waveform read the samples
                        req.SendWebRequest();
                        userLoads[index] = req;
                    }
                    catch (Exception e) { errors[index] = "Couldn't open that file."; Debug.LogWarning($"[DJMaster] {t.path}: {e.Message}"); return null; }
                }
                if (!req.isDone) return null;
                userLoads.Remove(index);
                try
                {
                    if (req.result != UnityWebRequest.Result.Success) throw new Exception(req.error);
                    clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip == null || clip.samples == 0 || clip.length < 4f) throw new Exception("no audio");
                    if (clip.length > MaxUserSeconds) { UnityEngine.Object.Destroy(clip); errors[index] = $"Too long: songs up to {MaxUserSeconds / 60:0} minutes play."; return null; }
                    clip.name = t.title;
                }
                catch (Exception e)
                {
                    errors[index] = "Couldn't play that file. Is it a working MP3, OGG or WAV?";
                    Debug.LogWarning($"[DJMaster] {t.path}: {e.Message}");
                    if (clip != null) UnityEngine.Object.Destroy(clip);
                    return null;
                }
                finally { req.Dispose(); }
                userPending[index] = clip;
            }

            // The beatgrid: saved from last time, or found now
            if (Tracks[index].bpm <= 0 && !BeatGridStore.ApplySaved(index))
            {
                if (!userDetects.TryGetValue(index, out var detect))
                {
                    int frames = Mathf.Min(clip.samples, (int)(DetectSeconds * clip.frequency));
                    var data = new float[frames * clip.channels];
                    clip.GetData(data, 0);
                    int ch = clip.channels, rate = clip.frequency;
                    userDetects[index] = detect = Task.Run(() => BeatDetector.Analyze(data, ch, rate));
                }
                if (!detect.IsCompleted) return null;
                userDetects.Remove(index);
                if (detect.IsFaulted || detect.Result.bpm <= 0) { errors[index] = "Couldn't find a beat in that song."; return null; }
                SetGrid(index, detect.Result);
                BeatGridStore.Save(Tracks[index].GridId, detect.Result);
            }
            userPending.Remove(index);
            clips[index] = clip;
            return clip;
        }

        // The bonus song stays hidden until you've done the daily challenge a few days in a row.
        public static bool IsLocked(int index) => index == BonusTrack && !DailyChallenge.BonusUnlocked;

        // Puts a beatgrid on a song: the decks read the BPM and first beat from here, so the bar counter, hot cues,
        // loops and SYNC all follow it.
        public static void SetGrid(int index, BeatGrid grid)
        {
            Tracks[index].bpm = grid.bpm;
            Tracks[index].firstBeat = BeatGrid.WrapToFirstBar(grid.firstBeat, grid.bpm);
        }

        public static BeatGrid GridOf(int index) => new BeatGrid { bpm = Tracks[index].bpm, firstBeat = Tracks[index].firstBeat, confidence = 1 };

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
                if (Tracks[i].IsUser) continue; // loaded when you pick one
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
            if (Tracks[index].IsUser) return TryGetUser(index);
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
            // The clip has its own copy of the samples, so we drop the render's copy (about 11 MB per song)
            renders.TryRemove(index, out _);
            return clip;
        }
    }
}
