using UnityEngine;

namespace DJMaster
{
    public enum GameMode { Title, ChooseVenue, ChooseLesson, Learn, Build, Live, LiveResults, Freestyle, Vocab, Daily, Grid }

    // Top-level flow: on the black start screen, pick a mode (Learn / Build a set / Freestyle / DJ Vocab) and where to play
    // (your room, a club or a festival), then step into the booth. Also wires up the scene and comfort buttons.
    public class GameDirector : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;
        public LessonDirector lessons;
        public SetPerformer performer;
        public GridEditor gridEditor;
        public MixRecorder recorder;
        public InstructionPanel panel;
        public StartMenu startMenu;
        public VocabPanel vocab;
        [HideInInspector, Tooltip("Which version of the scene builder made this scene, so the editor can rebuild a stale one.")]
        public string builtFrom;
        public SetBuilderPanel builder;
        public EnvironmentManager environment;
        public SeatedAnchor anchor;
        public PokeButton roomButton, clubButton, festivalButton, recenterButton, raiseButton, lowerButton;
        [Tooltip("HOME and COLOR-BLIND, moved onto whichever panel is showing.")]
        public SystemButtons systemButtons;

        public GameMode Mode { get; private set; }
        public GameMode ChosenMode { get; private set; } // what starts after the venue is picked

        bool paused, focusLost, appPaused, appUnfocused;
        // The live set running now is today's daily challenge (not a set from BUILD A SET)
        bool dailyRun, dailyCounted, dailyUnlocked;
        float lastScore;
        int replayIndex;

        void Start()
        {
            TrackLibrary.Prewarm();
            BoardColors.LoadSettings();
            board.gameObject.AddComponent<ColorSafeView>().Init(); // the panels and set builder live under the board too

            for (int i = 0; i < panel.actionButtons.Length; i++)
            {
                int idx = i;
                panel.actionButtons[i].Pressed += () => Action(idx);
            }
            for (int i = 0; i < startMenu.buttons.Length; i++)
            {
                int idx = i;
                startMenu.buttons[i].Pressed += () => MenuAction(idx);
            }
            roomButton.Pressed += () => environment.SetMode(EnvMode.Room);
            clubButton.Pressed += () => environment.SetMode(EnvMode.Club);
            festivalButton.Pressed += () => environment.SetMode(EnvMode.Festival);
            recenterButton.Pressed += anchor.Recenter;
            raiseButton.Pressed += () => anchor.AdjustHeight(0.02f);
            lowerButton.Pressed += () => anchor.AdjustHeight(-0.02f);
            anchor.gameObject.AddComponent<DeskSnap>().Init(anchor, environment); // in MY ROOM the board goes on your real desk
            lessons.Completed += ChooseLesson;
            performer.Completed += ShowResults;
            builder.BackPressed += () => ChooseVenue(GameMode.Build);
            vocab.MenuPressed += EnterTitle;
            systemButtons.home.Pressed += EnterTitle;
            systemButtons.colorBlind.Pressed += () => BoardColors.SetColorSafe(!BoardColors.ColorSafe);
            builder.GoLivePressed += StartLive;
            if (gridEditor != null) gridEditor.Done += StartFreestyle;

            OVRManager.InputFocusLost += () => { focusLost = true; RefreshPause(); };
            OVRManager.InputFocusAcquired += () => { focusLost = false; RefreshPause(); };

            EnterTitle();
            // Something to listen to straight away while you look around the menu.
            board.A.deck.Load(0);
            board.B.deck.Load(1);
        }

        void OnApplicationPause(bool pause) { appPaused = pause; RefreshPause(); }
        void OnApplicationFocus(bool focus) { if (!Application.isEditor) { appUnfocused = !focus; RefreshPause(); } }

        // 3 different things can pause the game (system menu, headset off, app in the background)
        // and they can come in any order, so we only unpause when all of them are cleared
        void RefreshPause() => SetPaused(focusLost || appPaused || appUnfocused);

        void SetPaused(bool p)
        {
            if (paused == p) return;
            paused = p;
            AudioListener.pause = p;
            Time.timeScale = p ? 0f : 1f;
        }

        public bool OnStartScreen => Mode == GameMode.Title || Mode == GameMode.ChooseVenue || Mode == GameMode.ChooseLesson || Mode == GameMode.Vocab;

        void SetMode(GameMode mode)
        {
            Mode = mode;
            // The start screen is its own black space: no booth, no venue, no passthrough.
            bool menu = OnStartScreen;
            startMenu.gameObject.SetActive(menu && mode != GameMode.Vocab);
            vocab.gameObject.SetActive(mode == GameMode.Vocab);
            board.gameObject.SetActive(!menu);
            environment.SetMenuScreen(menu);
            bool building = mode == GameMode.Build;
            panel.gameObject.SetActive(!building && !menu);
            builder.gameObject.SetActive(building);

            // The same HOME / COLOR-BLIND tab on the corner of whatever is showing. No HOME on the start menu itself
            var corner = mode == GameMode.Vocab ? vocab.corner : menu ? startMenu.corner : building ? builder.corner : panel.corner;
            systemButtons.MountOn(corner, mode != GameMode.Title);
        }

        // The tap-the-one screen for a song that doesn't start on beat 1 (songs you add yourself). Saves its beatgrid,
        // then goes to Freestyle.
        public void EditGrid(int track)
        {
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Grid);
            panel.SetActions("◄ EARLIER", "LATER ►", "HALF / x2", "SAVE");
            gridEditor.Begin(track);
        }

        void EnterTitle()
        {
            gridEditor?.Stop();
            lessons.Stop();
            performer.Stop();
            recorder.StopReplay();
            board.A.deck.Pause();
            board.B.deck.Pause();
            SetMode(GameMode.Title);
            int best = PlayerPrefs.GetInt("bestSet", 0);
            int today = DailyChallenge.Today;
            var daily = DailyChallenge.Progress;
            int streak = daily.StreakOn(today);
            string footer = streak > 0 ? $"Daily streak: {streak} day{(streak == 1 ? "" : "s")} in a row" : "";
            if (best > 0) footer += (footer != "" ? "     " : "") + $"Best live set: {best} pts";
            if (footer == "" && !LessonDirector.IsDone(0)) footer = "New to DJing? Start with LEARN.";
            startMenu.Show("DJ MASTER", "Learn to DJ with just your hands", footer);
            startMenu.SetRows(
                ("LEARN", "Three short lessons: your first mix, fades and cuts, then sync, loops and effects."),
                ("BUILD A SET", "Pick songs and transitions, then perform them live for a crowd."),
                ("FREESTYLE", "Just play. No timer, no rules."),
                ("DJ VOCAB", "What do BPM, cue, drop and the rest mean? Learn the DJ lingo."),
                ("DAILY", DailyRow(today, daily)));
            startMenu.buttons[4].Lit = !daily.PassedOn(today); // a new challenge is waiting
        }

        // The DAILY row on the title menu: today's challenge, your best grade today and what the streak unlocks.
        static string DailyRow(int today, DailyProgress p)
        {
            string text = $"Today: {DailyChallenge.ForDay(today).Title}.";
            int grade = p.BestGradeOn(today);
            if (grade >= 0) text += $" Your best: {GradeName((SetPerformer.Grade)grade)}.";
            if (!DailyChallenge.BonusUnlocked)
                text += $" {DailyChallenge.UnlockStreak} days in a row unlocks a bonus song.";
            return text;
        }

        static string GradeName(SetPerformer.Grade g) => g == SetPerformer.Grade.Perfect ? "PERFECT" : g == SetPerformer.Grade.Good ? "GOOD" : "ROUGH";

        // Every DJ word with its meaning, plus a quiz.
        void ShowVocab()
        {
            SetMode(GameMode.Vocab);
            vocab.ShowList();
        }

        void ChooseVenue(GameMode next)
        {
            ChosenMode = next;
            SetMode(GameMode.ChooseVenue);
            string what = next == GameMode.Learn ? "LEARN" : next == GameMode.Build ? "BUILD A SET" : next == GameMode.Daily ? "DAILY CHALLENGE" : "FREESTYLE";
            startMenu.Show("WHERE DO YOU WANT TO PLAY?", $"Mode: {what}", "You can switch anytime with the SCENE panel next to the booth.");
            startMenu.SetRows(
                ("MY ROOM", "Your real room. We'll ask for your room scan so the board can sit on your desk."),
                ("CLUB", "A dark club with lasers, a mirror ball and a packed dance floor."),
                ("FESTIVAL", "A night-time main stage with fireworks and a huge crowd."),
                ("BACK", ""));
        }

        void ChooseLesson()
        {
            lessons.Stop();
            SetMode(GameMode.ChooseLesson);
            string Done(int n) => LessonDirector.IsDone(n) ? "DONE.  " : "";
            startMenu.Show("PICK A LESSON", "Each one is a few minutes, one control at a time", "New? Start with lesson 1.");
            startMenu.SetRows(
                ("1 FIRST MIX", Done(0) + "The color code, the beat, and your first beatmix. About 7 minutes."),
                ("2 FADE & CUT", Done(1) + "The DJ loop, fades and cuts. No beatmatching needed. About 4 minutes."),
                ("3 SYNC & FX", Done(2) + "Tap tempo, SYNC, an 8-bar mix, loops and echo. About 5 minutes."),
                ("BACK", ""));
        }

        void MenuAction(int i)
        {
            switch (Mode)
            {
                case GameMode.Title:
                    if (i == 3) ShowVocab();
                    else if (i == 4) ChooseVenue(GameMode.Daily);
                    else ChooseVenue(i == 0 ? GameMode.Learn : i == 1 ? GameMode.Build : GameMode.Freestyle);
                    break;
                case GameMode.ChooseVenue:
                    if (i == 3) { EnterTitle(); break; }
                    environment.SetMode(i == 0 ? EnvMode.Room : i == 1 ? EnvMode.Club : EnvMode.Festival);
                    if (ChosenMode == GameMode.Learn) ChooseLesson();
                    else if (ChosenMode == GameMode.Build) StartBuild();
                    else if (ChosenMode == GameMode.Daily) StartDaily();
                    else StartFreestyle();
                    break;
                case GameMode.ChooseLesson:
                    if (i == 3) ChooseVenue(GameMode.Learn);
                    else StartLearn(i);
                    break;
            }
        }

        void Action(int i)
        {
            switch (Mode)
            {
                case GameMode.Grid:
                    if (i == 0) gridEditor.Nudge(-1);
                    else if (i == 1) gridEditor.Nudge(1);
                    else if (i == 2) gridEditor.HalfDouble();
                    else gridEditor.Save();
                    break;
                case GameMode.Learn:
                    if (i == 0) ChooseLesson();
                    else if (i == 1) lessons.Skip();
                    else StartLearn(lessons.LessonIndex);
                    break;
                case GameMode.Live:
                    if (i == 1) Restart();
                    else performer.Finish();
                    break;
                case GameMode.LiveResults:
                    if (i != 3) recorder.StopReplay();
                    if (i == 1) Restart();
                    else if (i == 2) StartBuild();
                    else ListenBack();
                    break;
            }
        }

        void StartLearn(int lesson)
        {
            performer.Stop();
            SetMode(GameMode.Learn);
            panel.SetActions("LESSONS", "SKIP STEP", "RESTART");
            lessons.Begin(lesson);
        }

        // Replays each recorded transition in turn, with the decks paused so you hear only the recording.
        void ListenBack()
        {
            var results = performer.Results;
            for (int tries = 0; tries < results.Count; tries++)
            {
                var r = results[replayIndex++ % results.Count];
                if (r.recording == null) continue;
                board.A.deck.Pause();
                board.B.deck.Pause();
                recorder.Play(r.recording);
                replaying = $"Listening back: {SetList.Name(r.style)} into {Title(r.to)}";
                return;
            }
            replaying = "No recordings yet.";
        }

        string replaying = "";

        void StartBuild()
        {
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Build);
            // Songs you add play on Deck A so you can hear your picks.
            board.ResetControls();
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.B.deck.Pause();
        }

        void Restart()
        {
            if (dailyRun) StartDaily();
            else StartLive();
        }

        // Today's challenge: one transition between two songs, played as a 2-song live set.
        void StartDaily()
        {
            lessons.Stop();
            dailyRun = true;
            dailyCounted = dailyUnlocked = false;
            SetMode(GameMode.Live);
            panel.SetActions(null, "RESTART", "END SET");
            performer.Begin(DailyChallenge.ForDay(DailyChallenge.Today).ToSet());
        }

        void StartLive()
        {
            if (!builder.Set.Ready) { StartBuild(); return; }
            dailyRun = false;
            lessons.Stop();
            SetMode(GameMode.Live);
            panel.SetActions(null, "RESTART", "END SET");
            performer.Begin(builder.Set);
        }

        void StartFreestyle()
        {
            gridEditor?.Stop();
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Freestyle);
            panel.SetActions();
            judge.ResetScore();
        }

        void ShowResults()
        {
            SetMode(GameMode.LiveResults);
            lastScore = judge.Score;
            if (dailyRun)
            {
                // Only a finished transition counts. A set ended before the mix has nothing to grade
                if (performer.Results.Count > 0)
                {
                    var grade = performer.Results[0].grade;
                    int before = DailyChallenge.Progress.StreakOn(DailyChallenge.Today);
                    dailyUnlocked = DailyChallenge.Record(grade);
                    dailyCounted = DailyChallenge.Progress.StreakOn(DailyChallenge.Today) > before;
                }
            }
            else if (lastScore > PlayerPrefs.GetInt("bestSet", 0)) { PlayerPrefs.SetInt("bestSet", (int)lastScore); PlayerPrefs.Save(); }
            Sfx.Cheer(1f);
            BeatBus.FireDrop();
            replayIndex = 0;
            replaying = "";
            panel.SetActions(null, dailyRun ? "TRY AGAIN" : "PLAY AGAIN", dailyRun ? null : "EDIT SET", "LISTEN BACK");
        }

        void Update()
        {
            RefreshPanel();
        }

        static string Title(int track) => TrackLibrary.Tracks[track].title;

        // The top of the results for a daily challenge: passed or not, the streak, and any unlock.
        string DailyResult(System.Collections.Generic.List<SetPerformer.Result> results)
        {
            int streak = DailyChallenge.Progress.StreakOn(DailyChallenge.Today);
            string days = $"{streak} day{(streak == 1 ? "" : "s")} in a row";
            string line = results.Count == 0 ? "You ended it before the mix. TRY AGAIN!"
                : results[0].grade == SetPerformer.Grade.Rough ? $"Not quite: you need GOOD or better to pass. TRY AGAIN! Streak: {days}."
                : dailyCounted ? BoardColors.Green($"Passed! Streak: {days}.") : $"Passed again. Streak: {days}. Come back tomorrow for a new one.";
            if (dailyUnlocked) line += "\n" + BoardColors.Yellow($"UNLOCKED: a bonus song, {Title(TrackLibrary.BonusTrack)}. Find it in FREESTYLE.");
            return line + "\n";
        }

        void RefreshPanel()
        {
            switch (Mode)
            {
                case GameMode.Grid:
                    panel.Show(gridEditor.Title, gridEditor.Body, gridEditor.Status, gridEditor.Feedback);
                    break;

                case GameMode.Learn:
                    var s = lessons.Current;
                    if (s == null) break;
                    string body = s.text;
                    if (lessons.ShowHint && !string.IsNullOrEmpty(s.hint)) body += "\n\nHINT: " + s.hint;
                    panel.Show($"LESSON {lessons.LessonIndex + 1}  {lessons.StepIndex + 1}/{lessons.StepCount}  {s.title.ToUpper()}", body, "", lessons.Feedback);
                    break;

                case GameMode.Live:
                    if (!performer.Running) break;
                    var set = performer.Set;
                    string title = $"{(dailyRun ? "DAILY CHALLENGE" : "LIVE")}  SONG {performer.SongIndex + 1}/{set.Count}  {TextUtil.Clock(performer.Elapsed)}";
                    string text;
                    if (performer.InFinale)
                        text = $"Last song: {Title(set.songs[performer.SongIndex])}. Enjoy it! Try a {BoardColors.Yellow("FILTER")} sweep, a bass drop or the {BoardColors.Red("AIR HORN")}.\nSet ends in {TextUtil.Clock(performer.FinaleLeft)}.";
                    else
                    {
                        var task = performer.CurrentTask;
                        text = $"NEXT: {Title(performer.NextTrack)} with a {BoardColors.Yellow(SetList.Name(performer.NextStyle))}\n" +
                               (task != null ? $"Step {performer.CurrentTaskIndex + 1}/{performer.Tasks.Count}: {task.text}" : "");
                    }
                    if (judge.Warning != "") text += "\n" + BoardColors.Red(judge.Warning);
                    panel.Show(title, text, $"Score {judge.Score:0}   Crowd {(int)(BeatBus.Energy * 100)}%", judge.Shoutout);
                    break;

                case GameMode.LiveResults:
                    var results = performer.Results;
                    int stars = SetPerformer.Stars(results);
                    var sb = new System.Text.StringBuilder();
                    foreach (var r in results)
                    {
                        string grade = r.grade == SetPerformer.Grade.Perfect ? BoardColors.Green("PERFECT") : r.grade == SetPerformer.Grade.Good ? BoardColors.Yellow("GOOD") : BoardColors.Red("ROUGH");
                        sb.Append($"{SetList.Name(r.style)}: {grade}. {r.note}\n");
                    }
                    if (results.Count < performer.Set.Count - 1) sb.Append("You ended the set early.\n");
                    var c = performer.Review;
                    sb.Append($"LISTEN BACK: beats in sync {c.syncPercent:0}% of mix time, {c.clashSeconds:0} s of clashing, {c.deadAirSeconds:0} s of silence.\n");
                    sb.Append($"PRACTISE NEXT: {c.practise}");
                    if (dailyRun) sb.Insert(0, DailyResult(results));
                    bool listening = recorder.Replaying;
                    panel.Show(dailyRun ? "DAILY CHALLENGE" : "SET COMPLETE!", sb.ToString(),
                        listening ? replaying : $"Score {lastScore:0}   Best {PlayerPrefs.GetInt("bestSet", 0)} pts",
                        new string('*', stars) + new string('-', 3 - stars));
                    break;

                case GameMode.Freestyle:
                    panel.Show("FREESTYLE",
                        judge.Warning != "" ? judge.Warning : "No timer, no rules. LOAD songs, mix with any transition, try bass drops (cut the LOW, slam it back on bar 1), LOOPs and ECHO. Remember: less is more.",
                        $"Crowd {(int)(BeatBus.Energy * 100)}%{(board.TapBpm > 0 ? $"   Tapped {board.TapBpm:0.0} BPM" : "")}", board.SyncMessage != "" ? board.SyncMessage : judge.Shoutout);
                    break;
            }
        }
    }
}
