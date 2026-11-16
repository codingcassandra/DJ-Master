using UnityEngine;

namespace DJMaster
{
    public enum GameMode { Title, ChooseVenue, ChooseLesson, Learn, Build, Live, LiveResults, Freestyle, Vocab, Daily, Grid, ChooseLevel, ChoosePlayer, Greeting }

    // Top-level flow: on the black start screen, pick a mode (Learn / Build a set / Freestyle / DJ Vocab / Daily), then your
    // level, then where to play (your room, a club or a festival), then step into the booth. Also wires up the scene and comfort buttons.
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

        [Tooltip("Say hi when the game starts. The PlayMode tests turn it off so they start on the start menu.")]
        public static bool GreetOnStart = true;

        public GameMode Mode { get; private set; }
        public GameMode ChosenMode { get; private set; } // what starts after the venue is picked

        [Tooltip("How loud deck A plays under the start screens.")]
        public float menuMusicLevel = 0.3f;

        bool menuMusicOn;
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
            if (startMenu.playerButton != null) startMenu.playerButton.Pressed += ChoosePlayer;
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

            // Something to listen to straight away while you look around the menu (loaded first so the menu can start it)
            board.A.deck.Load(0);
            board.B.deck.Load(1);
            if (GreetOnStart) EnterGreeting();
            else EnterTitle();
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

        public bool OnStartScreen => Mode == GameMode.Title || Mode == GameMode.Greeting || Mode == GameMode.ChoosePlayer || Mode == GameMode.ChooseLevel || Mode == GameMode.ChooseVenue || Mode == GameMode.ChooseLesson || Mode == GameMode.Vocab;

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
            systemButtons.MountOn(corner, mode != GameMode.Title && mode != GameMode.Greeting);
            SetMenuMusic(menu);
        }

        // Deck A plays quietly behind the start screens (#40). Stepping into the booth stops it and cues it back to the top,
        // so every mode starts with the decks still, like before.
        void SetMenuMusic(bool on)
        {
            var a = board.A.deck;
            if (on)
            {
                board.B.deck.Pause();
                if (!a.HasTrack) a.Load(0);
                a.Play();
                mixer.menuMusic = menuMusicLevel;
            }
            else if (menuMusicOn)
            {
                a.Pause(); // also cancels a play that was waiting for the song to load
                a.Cue();
                mixer.menuMusic = -1f;
            }
            menuMusicOn = on;
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
            // Just the buttons (#47). What each one does is on the next screen.
            startMenu.Show("", "", ""); // the wordmark takes the title's place
            startMenu.SetButtons("LEARN", "BUILD A SET", "FREESTYLE", "DJ VOCAB", "DAILY");
            startMenu.ShowPlayer(Players.Current.name);
            startMenu.buttons[4].Lit = !DailyChallenge.Progress.PassedOn(DailyChallenge.Today); // a new challenge is waiting
        }

        // The hello (#50): "HI, <NAME>!", one thing to do next, then CONTINUE to the start menu or SWITCH PLAYER.
        // Shown when the game starts and after picking a player. The name is the signed-in or picked player's.
        void EnterGreeting()
        {
            EnterTitle(); // same clean start: decks paused, menu music on
            SetMode(GameMode.Greeting);
            startMenu.Show(Greeting(Players.Current.name), TextUtil.Wrap(Suggestion(), 46), "");
            startMenu.SetButtons(null, null, "CONTINUE", "SWITCH PLAYER");
            startMenu.HideExtras();
            startMenu.buttons[2].Lit = true; // the obvious next poke
        }

        public static string Greeting(string name) => $"HI, {(string.IsNullOrWhiteSpace(name) ? Players.DefaultName : name.Trim()).ToUpperInvariant()}!";

        // One thing to do next: the first lesson for a new player, a daily streak to keep, the next lesson, today's
        // challenge, then beating your best set.
        public static string Suggestion()
        {
            int today = DailyChallenge.Today;
            var daily = DailyChallenge.Progress;
            int streak = daily.StreakOn(today);
            bool dailyWaiting = !daily.PassedOn(today);
            if (!LessonDirector.IsDone(0)) return $"New here? Start with LEARN: lesson 1, {LessonDirector.LessonNames[0]}. Music in seconds.";
            if (dailyWaiting && streak > 0) return $"Keep your {streak}-day streak going: today's DAILY is {DailyChallenge.ForDay(today).Title}.";
            for (int i = 1; i < LessonDirector.LessonNames.Length; i++)
                if (!LessonDirector.IsDone(i)) return $"Next up in LEARN: lesson {i + 1}, {LessonDirector.LessonNames[i]}.";
            if (dailyWaiting) return $"Today's DAILY: {DailyChallenge.ForDay(today).Title}.";
            int best = Players.BestSet;
            return best > 0 ? $"Every lesson done! BUILD A SET and beat your best: {best} pts." : "Every lesson done! BUILD A SET and play it live for the crowd.";
        }

        // PICK PLAYER (#49): everyone on this headset, with their progress. Up to 4, then + NEW PLAYER, then BACK.
        void ChoosePlayer()
        {
            SetMode(GameMode.ChoosePlayer);
            startMenu.Show("WHO'S PLAYING?", "Everyone keeps their own lessons, scores, streak and level", Players.Full ? $"That's the most: {Players.MaxPlayers} players." : "");
            var all = Players.All;
            var rows = new (string, string)[StartMenu.Rows];
            for (int i = 0; i < all.Count && i < StartMenu.Rows - 1; i++) rows[i] = (all[i].name, Players.Summary(all[i].id));
            if (!Players.Full) rows[all.Count] = ("+ NEW PLAYER", $"Adds {Players.NextName()}, with a fresh start.");
            rows[StartMenu.Rows - 1] = ("BACK", "");
            startMenu.SetRows(rows);
            for (int i = 0; i < all.Count && i < StartMenu.Rows - 1; i++) startMenu.buttons[i].Lit = all[i].id == Players.CurrentId;
        }

        void PickPlayerRow(int i)
        {
            var all = Players.All;
            if (i == StartMenu.Rows - 1) { EnterTitle(); return; }
            if (i < all.Count) Players.Pick(all[i].id);
            else if (i == all.Count && !Players.Full) Players.Pick(Players.Add());
            else return;
            EnterGreeting();
        }

        static string ModeName(GameMode m) => m == GameMode.Learn ? "LEARN" : m == GameMode.Build ? "BUILD A SET" : m == GameMode.Daily ? "DAILY CHALLENGE" : "FREESTYLE";

        // What a mode is about, under its name on the level and venue screens. DAILY gets today's challenge and the streak.
        static string ModeInfo(GameMode m)
        {
            switch (m)
            {
                case GameMode.Learn: return "Four short lessons: your first mix, fades and cuts, sync, loops and effects, then picking the next song.";
                case GameMode.Build:
                    int best = Players.BestSet;
                    return "Pick songs and transitions, then perform them live for a crowd." + (best > 0 ? $"  Best live set: {best} pts" : "");
                case GameMode.Daily: return DailyInfo(DailyChallenge.Today, DailyChallenge.Progress);
                default: return "Just play. No timer, no rules.";
            }
        }

        // Today's challenge, your best grade today, the streak and what it unlocks.
        static string DailyInfo(int today, DailyProgress p)
        {
            string text = $"Today: {DailyChallenge.ForDay(today).Title}.";
            int grade = p.BestGradeOn(today);
            if (grade >= 0) text += $" Your best: {GradeName((SetPerformer.Grade)grade)}.";
            int streak = p.StreakOn(today);
            if (streak > 0) text += $" Streak: {streak} day{(streak == 1 ? "" : "s")} in a row.";
            if (!DailyChallenge.BonusUnlocked)
                text += $" {DailyChallenge.UnlockStreak} days in a row unlocks a bonus song.";
            return text;
        }

        static string GradeName(SetPerformer.Grade g) => g == SetPerformer.Grade.Perfect ? "PERFECT" : g == SetPerformer.Grade.Good ? "GOOD" : "ROUGH";

        // Step 2 (#48): your level, for the mode you picked. The saved one is lit.
        void ChooseLevel(GameMode next)
        {
            ChosenMode = next;
            SetMode(GameMode.ChooseLevel);
            startMenu.Show("PICK YOUR LEVEL", $"Mode: {ModeName(next)}", TextUtil.Wrap(ModeInfo(next), 70));
            startMenu.SetRows(
                (Skill.Name(SkillLevel.Beginner), Skill.Describe(SkillLevel.Beginner)),
                (Skill.Name(SkillLevel.Intermediate), Skill.Describe(SkillLevel.Intermediate)),
                (Skill.Name(SkillLevel.Advanced), Skill.Describe(SkillLevel.Advanced)),
                (Skill.Name(SkillLevel.Solo), Skill.Describe(SkillLevel.Solo)),
                ("BACK", ""));
            startMenu.buttons[(int)Skill.Current].Lit = true;
        }

        // A level: saved, then on to the venue.
        void PickLevel(SkillLevel level)
        {
            Skill.Current = level;
            ChooseVenue(ChosenMode);
        }

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
            startMenu.Show("WHERE DO YOU WANT TO PLAY?", $"Mode: {ModeName(next)}     Level: {Skill.Name(Skill.Current)}",
                next == GameMode.Daily ? TextUtil.Wrap(ModeInfo(next), 70) : "You can switch anytime with the SCENE panel next to the booth.");
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
                ("4 NEXT SONG", Done(3) + "Pick what's next by tempo and key, hear a bad pick, then mix yours in. About 5 minutes."),
                ("BACK", ""));
        }

        void MenuAction(int i)
        {
            switch (Mode)
            {
                case GameMode.Title:
                    // DJ VOCAB is a menu of its own, not graded and not in a venue, so it skips the level and venue steps
                    if (i == 3) ShowVocab();
                    else ChooseLevel(i == 0 ? GameMode.Learn : i == 1 ? GameMode.Build : i == 2 ? GameMode.Freestyle : GameMode.Daily);
                    break;
                case GameMode.Greeting:
                    if (i == 2) EnterTitle();
                    else if (i == 3) ChoosePlayer();
                    break;
                case GameMode.ChoosePlayer:
                    PickPlayerRow(i);
                    break;
                case GameMode.ChooseLevel:
                    if (i == 4) EnterTitle();
                    else PickLevel((SkillLevel)i);
                    break;
                case GameMode.ChooseVenue:
                    if (i == 3) { ChooseLevel(ChosenMode); break; }
                    environment.SetMode(i == 0 ? EnvMode.Room : i == 1 ? EnvMode.Club : EnvMode.Festival);
                    if (ChosenMode == GameMode.Learn) ChooseLesson();
                    else if (ChosenMode == GameMode.Build) StartBuild();
                    else if (ChosenMode == GameMode.Daily) StartDaily();
                    else StartFreestyle();
                    break;
                case GameMode.ChooseLesson:
                    if (i == 4) ChooseVenue(GameMode.Learn);
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
                    else if (i == 2) StartLearn(lessons.LessonIndex);
                    else lessons.PressNext();
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
            nextShown = false;
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
        bool nextShown;

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
            else if (lastScore > Players.BestSet) Players.BestSet = (int)lastScore;
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

        // The level you picked, on the panel's status line while you play.
        static string LevelTag => $"Level: {Skill.Name(Skill.Current)}";

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
                    // NEXT > shows (lit) only while a step is waiting to be read
                    if (lessons.WaitingForNext != nextShown)
                    {
                        nextShown = lessons.WaitingForNext;
                        panel.SetActions("LESSONS", "SKIP STEP", "RESTART", nextShown ? "NEXT >" : null);
                        panel.actionButtons[3].Lit = nextShown;
                    }
                    var s = lessons.Current;
                    if (s == null) break;
                    string body = s.text;
                    if (lessons.ShowHint && !string.IsNullOrEmpty(s.hint)) body += "\n\nHINT: " + s.hint;
                    panel.Show($"LESSON {lessons.LessonIndex + 1}  {lessons.StepIndex + 1}/{lessons.StepCount}  {s.title.ToUpper()}", body, LevelTag, lessons.Feedback);
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
                    panel.Show(title, text, $"Score {judge.Score:0}   Crowd {(int)(BeatBus.Energy * 100)}%   {LevelTag}", judge.Shoutout);
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
                        listening ? replaying : $"Score {lastScore:0}   Best {Players.BestSet} pts",
                        new string('*', stars) + new string('-', 3 - stars));
                    break;

                case GameMode.Freestyle:
                    panel.Show("FREESTYLE",
                        judge.Warning != "" ? judge.Warning : "No timer, no rules. Turn BROWSE to pick a song and LOAD it, mix with any transition, try bass drops (cut the LOW, slam it back on bar 1), LOOPs and ECHO. Remember: less is more.",
                        $"Crowd {(int)(BeatBus.Energy * 100)}%{(board.TapBpm > 0 ? $"   Tapped {board.TapBpm:0.0} BPM" : "")}   {LevelTag}", board.SyncMessage != "" ? board.SyncMessage : judge.Shoutout);
                    break;
            }
        }
    }
}
