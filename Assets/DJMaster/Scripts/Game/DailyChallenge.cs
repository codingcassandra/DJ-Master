using System;
using UnityEngine;

namespace DJMaster
{
    // Your record on the daily challenge: the streak of days in a row you've passed it, and your best grade today.
    // Pure logic plus a compact string for PlayerPrefs, so it's unit tested.
    public class DailyProgress
    {
        public int lastDay = int.MinValue; // the last day you passed (GOOD or better)
        public int streak, bestStreak;
        public int gradeDay = int.MinValue, bestGrade = -1; // best grade on gradeDay, as a SetPerformer.Grade

        // A streak only counts while it's alive: you passed today or yesterday.
        public int StreakOn(int today) => lastDay == today || lastDay == today - 1 ? streak : 0;
        public bool PassedOn(int today) => lastDay == today;
        public int BestGradeOn(int today) => gradeDay == today ? bestGrade : -1;

        // Saves a result. Returns true when it grew the streak (the first pass of the day).
        public bool Record(int day, SetPerformer.Grade grade)
        {
            if (gradeDay != day) { gradeDay = day; bestGrade = (int)grade; }
            else bestGrade = Math.Max(bestGrade, (int)grade);
            if (grade < SetPerformer.Grade.Good || lastDay == day) return false;
            streak = lastDay == day - 1 ? streak + 1 : 1;
            lastDay = day;
            bestStreak = Math.Max(bestStreak, streak);
            return true;
        }

        public string Serialize() => $"1,{lastDay},{streak},{bestStreak},{gradeDay},{bestGrade}";

        public static DailyProgress Parse(string s)
        {
            var p = new DailyProgress();
            var parts = (s ?? "").Split(',');
            if (parts.Length < 6 || parts[0] != "1") return p;
            int[] v = new int[5];
            for (int i = 0; i < 5; i++) if (!int.TryParse(parts[i + 1], out v[i])) return new DailyProgress();
            p.lastDay = v[0]; p.streak = v[1]; p.bestStreak = v[2]; p.gradeDay = v[3]; p.bestGrade = v[4];
            return p;
        }
    }

    // One short challenge a day, the same for everyone: a single transition between two songs, picked from the date.
    // It plays as a 2-song live set, so it gets the live checklist, the grade and LISTEN BACK.
    public static class DailyChallenge
    {
        public struct Challenge
        {
            public int from, to;
            public TransitionStyle style;
            public string Title => $"{SetList.Name(style)}: {TrackLibrary.Tracks[from].title} into {TrackLibrary.Tracks[to].title}";
            public SetList ToSet()
            {
                var set = new SetList();
                set.Add(from, style);
                set.Add(to, style);
                return set;
            }
        }

        public const int UnlockStreak = 3; // days in a row that unlock the bonus song
        const string PrefKey = "daily";
        static readonly DateTime Epoch = new DateTime(2026, 1, 1);

        public static int DayNumber(DateTime date) => (int)(date.Date - Epoch).TotalDays;
        public static int Today => DayNumber(DateTime.Now);

        static DailyProgress progress;
        public static DailyProgress Progress => progress ??= DailyProgress.Parse(PlayerPrefs.GetString(PrefKey, ""));

        public static bool BonusUnlocked => Progress.bestStreak >= UnlockStreak;

        // Saves today's result. Returns true if this pass just unlocked the bonus song.
        public static bool Record(SetPerformer.Grade grade)
        {
            bool wasUnlocked = BonusUnlocked;
            Progress.Record(Today, grade);
            PlayerPrefs.SetString(PrefKey, Progress.Serialize());
            PlayerPrefs.Save();
            return !wasUnlocked && BonusUnlocked;
        }

        // For tests: forget the saved record.
        public static void ResetProgress()
        {
            progress = new DailyProgress();
            PlayerPrefs.DeleteKey(PrefKey);
        }

        // The same day always gives the same challenge, on every headset. Uses its own hash (not System.Random, whose
        // sequence isn't promised to stay the same between .NET versions).
        // Lets the PlayMode tests play a known challenge instead of today's.
        public static Challenge? TestOverride;

        public static Challenge ForDay(int day)
        {
            if (TestOverride is { } forced) return forced;
            var c = Pick(day, 0);
            var y = Pick(day - 1, 0);
            if (c.from == y.from && c.to == y.to && c.style == y.style) c = Pick(day, 1000); // never the same two days running
            return c;
        }

        public static Challenge ForDate(DateTime date) => ForDay(DayNumber(date));

        static Challenge Pick(int day, int salt)
        {
            int n = TrackLibrary.BonusTrack; // the bonus song isn't in the rotation, so everyone gets the same challenge
            var styles = (TransitionStyle[])Enum.GetValues(typeof(TransitionStyle));
            for (int k = 0; ; k++)
            {
                ulong h = Mix((ulong)(uint)day * 0x9E3779B97F4A7C15UL + (ulong)(salt + k));
                var c = new Challenge
                {
                    style = styles[(int)(h % (ulong)styles.Length)],
                    from = (int)((h >> 16) % (ulong)n),
                    to = (int)((h >> 40) % (ulong)n),
                };
                // Two different songs that suit the transition (beatmixes need tempos within reach)
                if (c.from != c.to && SetList.Advice(c.from, c.to, c.style) == null) return c;
            }
        }

        // splitmix64
        static ulong Mix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }
}
