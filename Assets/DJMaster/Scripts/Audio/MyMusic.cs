using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace DJMaster
{
    // MY MUSIC (#22): songs you copy onto the headset yourself. The Quest's system file picker doesn't hand files back to
    // Unity apps reliably, so we use a folder: Music/DJ Master on the Quest (copy files in over USB or with Meta Quest
    // Developer Hub). We ask for permission to read music the way your Android version wants (READ_MEDIA_AUDIO on
    // Android 13 and up, storage before that), scan the folder, and add the songs to the browser's MY MUSIC list.
    // Nothing is uploaded anywhere: the files are only read from the headset.
    public static class MyMusic
    {
        public const string FolderName = "DJ Master";
        public static readonly string[] Extensions = { ".mp3", ".ogg", ".wav" };
        public const long MaxBytes = 150L * 1024 * 1024; // bigger than any normal song; skips anything huge
        public const int MaxSongs = 200;

        // Tests point this at a temporary folder
        public static string FolderOverride;

        public static string Folder => FolderOverride ?? DefaultFolder;

        static string DefaultFolder
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return "/storage/emulated/0/Music/" + FolderName;
#else
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), FolderName);
#endif
            }
        }

        // Where to put the files, in words for the screen
        public static string WhereToCopy =>
#if UNITY_ANDROID && !UNITY_EDITOR
            "Music > DJ Master on your Quest";
#else
            Folder;
#endif

        public static string HowTo => $"Copy MP3, OGG or WAV files into {WhereToCopy} (over USB or Meta Quest Developer Hub), then press SORT to come back here. Only add music you have the rights to play.";

        // What the last scan found, for the browser's status line
        public static string Status { get; private set; } = "";
        public static int Count { get; private set; }
        public static bool Scanned { get; private set; }
        public static event Action Changed;

        // ---- Permission ----

#if UNITY_ANDROID && !UNITY_EDITOR
        static int SdkInt
        {
            get { using var v = new AndroidJavaClass("android.os.Build$VERSION"); return v.GetStatic<int>("SDK_INT"); }
        }
        static string PermissionName => SdkInt >= 33 ? "android.permission.READ_MEDIA_AUDIO" : Permission.ExternalStorageRead;
        public static bool HasPermission => Permission.HasUserAuthorizedPermission(PermissionName);
#else
        public static bool HasPermission => true;
#endif

        static bool asking;

        // Scan if we're already allowed, without popping anything up (at startup)
        public static void ScanIfAllowed()
        {
            if (HasPermission) Scan();
        }

        // Opening MY MUSIC: ask for permission the first time, then scan
        public static void Open()
        {
            if (HasPermission) { Scan(); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (asking) return;
            asking = true;
            Status = "Allow DJ Master to read your music to see your songs.";
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => { asking = false; Scan(); };
            callbacks.PermissionDenied += _ => { asking = false; Status = "No music access, so MY MUSIC stays empty. You can allow it in the Quest's app settings."; Changed?.Invoke(); };
            Permission.RequestUserPermission(PermissionName, callbacks);
#endif
        }

        // ---- The folder ----

        // Lists the folder and puts its songs in the library. Never throws: a missing folder, an unreadable file or a
        // weird name just means fewer songs and a message
        public static void Scan()
        {
            var songs = new List<TrackInfo>();
            int skipped = 0;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder); // so it's there to copy into
#endif
                var files = Directory.Exists(Folder) ? new List<string>(Directory.GetFiles(Folder)) : new List<string>();
                files.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    if (!IsSong(f)) continue;
                    long size;
                    try { size = new FileInfo(f).Length; } catch { skipped++; continue; }
                    if (size <= 0 || size > MaxBytes) { skipped++; continue; }
                    if (songs.Count >= MaxSongs) { skipped++; continue; }
                    songs.Add(SongFor(f));
                }
                Status = songs.Count == 0 ? "No songs yet. " + HowTo
                    : $"{songs.Count} song{(songs.Count == 1 ? "" : "s")} from {WhereToCopy}" + (skipped > 0 ? $"  ({skipped} skipped: too big or unreadable)" : "");
            }
            catch (Exception e)
            {
                Status = "Couldn't read your music folder. " + HowTo;
                Debug.LogWarning("[DJMaster] My music: " + e.Message);
            }
            TrackLibrary.SetUserTracks(songs);
            // Songs whose beat we found before keep it, so they're ready to play straight away after a restart
            for (int i = TrackLibrary.BuiltInCount; i < TrackLibrary.Tracks.Length; i++) BeatGridStore.ApplySaved(i);
            Count = songs.Count;
            Scanned = true;
            Changed?.Invoke();
        }

        public static bool IsSong(string path) => Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;

        // A file as a song: its name is the title, the beat comes later (saved grid or found on first load)
        public static TrackInfo SongFor(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Trim();
            if (name.Length == 0) name = "Untitled";
            return new TrackInfo(name, "My music", 0f, -1, Genre.Yours, 0) { path = path };
        }
    }
}
