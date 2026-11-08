using System;
using System.Collections.Generic;

namespace DJMaster
{
    // The transitions from the "five basic DJ transitions", plus two effects-based ones. Values are saved, so only append.
    public enum TransitionStyle { Blend, BassSwap, Filter, Cut, Fade, EchoOut }

    // A planned DJ set: songs in order, and the transition style used to mix into each song after the first.
    [Serializable]
    public class SetList
    {
        public const int MinSongs = 2;
        public const int MaxSongs = 5;

        public readonly List<int> songs = new();
        public readonly List<TransitionStyle> transitions = new(); // transitions[i] mixes songs[i] into songs[i + 1]

        public int Count => songs.Count;
        public bool Ready => songs.Count >= MinSongs;
        public bool Full => songs.Count >= MaxSongs;

        public static string Name(TransitionStyle t) => t switch
        {
            TransitionStyle.BassSwap => "BASS SWAP",
            TransitionStyle.Filter => "FILTER MIX",
            TransitionStyle.Cut => "QUICK CUT",
            TransitionStyle.Fade => "FADE",
            TransitionStyle.EchoOut => "ECHO OUT",
            _ => "PHRASE MIX",
        };

        // Transitions that play both songs together, so their tempos must be matched.
        public static bool NeedsBeatmatch(TransitionStyle t) => t == TransitionStyle.Blend || t == TransitionStyle.BassSwap || t == TransitionStyle.Filter;

        public static string Describe(TransitionStyle t) => t switch
        {
            TransitionStyle.BassSwap => "Beatmix with the new LOW cut, then trade basslines on a phrase. Best for house and techno.",
            TransitionStyle.Filter => "Beatmix while sweeping the old song's FILTER to muffle it away.",
            TransitionStyle.Cut => "Instant switch: on bar 1 of a phrase, PLAY the new song and slam the crossfader.",
            TransitionStyle.Fade => "Fade the old song over about 10 seconds, then start the new one on a phrase. Works for any tempo.",
            TransitionStyle.EchoOut => "Turn on ECHO and pull the old song's fader: the echoes ring out, then drop the new song.",
            _ => "Beatmatch, play both songs together for one phrase (about 8 bars), then crossfade.",
        };

        // Beatmixing only sounds right within about 4% of each other.
        public static float TempoGapPercent(int fromTrack, int toTrack)
        {
            var t = TrackLibrary.Tracks;
            return Math.Abs(t[toTrack].bpm / t[fromTrack].bpm - 1f) * 100f;
        }

        // A tip for the player when a planned transition doesn't suit the two songs, or null.
        public static string Advice(int fromTrack, int toTrack, TransitionStyle t)
        {
            float gap = TempoGapPercent(fromTrack, toTrack);
            if (NeedsBeatmatch(t) && gap > 4f)
                return $"{gap:0}% tempo gap: too far to beatmix smoothly. Try a FADE, QUICK CUT or ECHO OUT.";
            return null;
        }

        // Returns an error to show the player, or null if the song was added.
        public string Add(int track, TransitionStyle into)
        {
            if (Full) return $"Your set is full ({MaxSongs} songs). GO LIVE!";
            if (track < 0 || track >= TrackLibrary.Tracks.Length) return "Unknown track.";
            if (songs.Contains(track)) return $"{TrackLibrary.Tracks[track].title} is already in your set.";
            if (songs.Count > 0) transitions.Add(into);
            songs.Add(track);
            return null;
        }

        public void Undo()
        {
            if (songs.Count == 0) return;
            songs.RemoveAt(songs.Count - 1);
            if (transitions.Count > 0 && transitions.Count >= songs.Count) transitions.RemoveAt(transitions.Count - 1);
        }

        public void Clear() { songs.Clear(); transitions.Clear(); }

        // Compact form for PlayerPrefs, e.g. "0,1,3|1,3".
        public string Serialize() => string.Join(",", songs) + "|" + string.Join(",", transitions.ConvertAll(t => (int)t));

        public static SetList Parse(string s)
        {
            var set = new SetList();
            if (string.IsNullOrEmpty(s)) return set;
            var parts = s.Split('|');
            var styles = new List<TransitionStyle>();
            if (parts.Length > 1)
                foreach (var t in parts[1].Split(','))
                    if (int.TryParse(t, out int v) && Enum.IsDefined(typeof(TransitionStyle), v)) styles.Add((TransitionStyle)v);
            int n = 0;
            foreach (var p in parts[0].Split(','))
            {
                if (!int.TryParse(p, out int track)) continue;
                var style = n > 0 && n - 1 < styles.Count ? styles[n - 1] : TransitionStyle.Blend;
                if (set.Add(track, style) == null) n++;
            }
            return set;
        }
    }
}
