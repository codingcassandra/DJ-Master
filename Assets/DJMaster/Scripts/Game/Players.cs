using System;
using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // More than one person on the same headset (#49), each with their own progress: lessons done, best set, daily streak,
    // vocab best, level and the set they built. Device settings (color-blind, board height, venue, beatgrids) stay shared.
    //
    // Every per-player save goes through Key(). The first player (id 0) uses the plain keys, so the progress saved
    // before players existed belongs to them. Ids never get reused, so a removed player's saves can't leak into a new one.
    public static class Players
    {
        public const string ListKey = "players", CurrentKey = "player", NextIdKey = "playersNextId";
        public const int MaxPlayers = 4;
        public const string DefaultName = "DJ";

        public struct Player
        {
            public int id;
            public string name;
            public Player(int id, string name) { this.id = id; this.name = name; }
        }

        // Names for new players, picked hands-only (no keyboard in the headset). The signed-in Meta user keeps their own name.
        static readonly string[] Names = { "DJ NOVA", "DJ ECHO", "DJ PULSE", "DJ LUMEN", "DJ BLAZE", "DJ ORBIT" };

        // Fires after switching player, so screens that cached progress can read it again.
        public static event Action Switched;

        static List<Player> list;
        static int current = -1;

        public static IReadOnlyList<Player> All { get { Load(); return list; } }
        public static int CurrentId { get { Load(); return current; } }
        public static Player Current { get { Load(); return list[IndexOf(current)]; } }
        public static bool Full => All.Count >= MaxPlayers;

        // The save key for the current player.
        public static string Key(string key) => KeyFor(CurrentId, key);
        public static string KeyFor(int id, string key) => id == 0 ? key : $"p{id}.{key}";

        // Every key a player owns, so removing them clears it all.
        public static IEnumerable<string> ProgressKeys()
        {
            for (int i = 0; i < LessonDirector.LessonNames.Length; i++) yield return LessonDirector.DoneKey(i);
            yield return BestSetKey;
            yield return DailyChallenge.PrefKey;
            yield return DJVocab.QuizBestKey;
            yield return SkillLevels.PrefKey;
            yield return SetBuilderPanel.SetKey;
        }

        public const string BestSetKey = "bestSet";
        public static int BestSet
        {
            get => PlayerPrefs.GetInt(Key(BestSetKey), 0);
            set { PlayerPrefs.SetInt(Key(BestSetKey), value); PlayerPrefs.Save(); }
        }

        static void Load()
        {
            if (list != null) return;
            list = Parse(PlayerPrefs.GetString(ListKey, ""));
            int saved = PlayerPrefs.GetInt(CurrentKey, list[0].id);
            current = IndexOf(saved) >= 0 ? saved : list[0].id;
        }

        static int IndexOf(int id) => list.FindIndex(p => p.id == id);

        // "0:DJ|3:DJ NOVA". Never empty: no save means one player, the default.
        public static List<Player> Parse(string s)
        {
            var result = new List<Player>();
            foreach (var part in (s ?? "").Split('|'))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0 || !int.TryParse(part.Substring(0, colon), out int id) || id < 0) continue;
                if (result.Exists(p => p.id == id)) continue;
                string name = part.Substring(colon + 1).Trim();
                result.Add(new Player(id, name == "" ? DefaultName : name));
            }
            if (result.Count == 0) result.Add(new Player(0, DefaultName));
            return result;
        }

        public static string Serialize(IEnumerable<Player> players)
        {
            var parts = new List<string>();
            foreach (var p in players) parts.Add($"{p.id}:{Clean(p.name)}");
            return string.Join("|", parts);
        }

        static string Clean(string name) => (name ?? "").Replace("|", " ").Replace(":", " ").Trim();

        static void Save()
        {
            PlayerPrefs.SetString(ListKey, Serialize(list));
            PlayerPrefs.SetInt(CurrentKey, current);
            PlayerPrefs.Save();
        }

        // The next free name from the list, then PLAYER 5, PLAYER 6 ...
        public static string NextName()
        {
            foreach (var n in Names)
                if (!All.Exists(n)) return n;
            return $"PLAYER {All.Count + 1}";
        }

        static bool Exists(this IReadOnlyList<Player> players, string name)
        {
            foreach (var p in players) if (p.name == name) return true;
            return false;
        }

        // Adds a player (not switched to). Returns its id, or -1 when the headset is full.
        public static int Add(string name = null)
        {
            Load();
            if (Full) return -1;
            int id = 0;
            foreach (var p in list) id = Mathf.Max(id, p.id + 1);
            id = Mathf.Max(id, PlayerPrefs.GetInt(NextIdKey, 0)); // never reuse a removed player's id
            PlayerPrefs.SetInt(NextIdKey, id + 1);
            list.Add(new Player(id, string.IsNullOrWhiteSpace(name) ? NextName() : Clean(name)));
            Save();
            return id;
        }

        public static void Pick(int id)
        {
            Load();
            if (IndexOf(id) < 0) return;
            current = id;
            Save();
            // Progress the game keeps in memory belongs to the old player
            SkillLevels.Reload();
            DailyChallenge.Reload();
            Switched?.Invoke();
        }

        // Removes a player and their saves. The last player can't be removed.
        public static bool Remove(int id)
        {
            Load();
            int i = IndexOf(id);
            if (i < 0 || list.Count <= 1) return false;
            foreach (var k in ProgressKeys()) PlayerPrefs.DeleteKey(KeyFor(id, k));
            list.RemoveAt(i);
            if (current == id) Pick(list[0].id);
            else Save();
            return true;
        }

        public static void Rename(int id, string name)
        {
            Load();
            int i = IndexOf(id);
            if (i < 0 || string.IsNullOrWhiteSpace(name)) return;
            list[i] = new Player(id, Clean(name));
            Save();
        }

        // For Meta sign-in (Angie): the signed-in user is the first player, under their own name.
        public static void SignedIn(string displayName) => Rename(All[0].id, displayName);

        // One line about a player's progress, for the pick player screen.
        public static string Summary(int id)
        {
            int lessons = 0;
            for (int i = 0; i < LessonDirector.LessonNames.Length; i++) if (PlayerPrefs.GetInt(KeyFor(id, LessonDirector.DoneKey(i)), 0) == 1) lessons++;
            int best = PlayerPrefs.GetInt(KeyFor(id, BestSetKey), 0);
            var daily = DailyProgress.Parse(PlayerPrefs.GetString(KeyFor(id, DailyChallenge.PrefKey), ""));
            int streak = daily.StreakOn(DailyChallenge.Today);
            var level = (SkillLevel)PlayerPrefs.GetInt(KeyFor(id, SkillLevels.PrefKey), (int)SkillLevel.Intermediate);
            string text = $"{Skill.Name(level)}.  Lessons done: {lessons}.";
            if (best > 0) text += $"  Best set {best}.";
            if (streak > 0) text += $"  Streak {streak}.";
            return text;
        }

        // For tests: back to one default player, with the in-memory progress read again.
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ListKey);
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.DeleteKey(NextIdKey);
            list = null;
            current = -1;
            SkillLevels.Reload();
            DailyChallenge.Reload();
            Switched?.Invoke();
        }
    }
}
