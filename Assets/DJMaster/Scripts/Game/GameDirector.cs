using UnityEngine;

namespace DJMaster
{
    public enum GameMode { Title, ChooseVenue, ChooseLesson, Learn, Build, Live, LiveResults, Freestyle, Vocab }

    // Top-level flow: on the black start screen, pick a mode (Learn / Build a set / Freestyle / DJ Vocab) and where to play
    // (your room, a club or a festival), then step into the booth. Also wires up the scene and comfort buttons.
    public class GameDirector : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;
        public LessonDirector lessons;
        public SetPerformer performer;
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

        public GameMode Mode { get; private set; }
        public GameMode ChosenMode { get; private set; } // what starts after the venue is picked

        bool paused, focusLost, appPaused, appUnfocused;
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
            lessons.Completed += ChooseLesson;
            performer.Completed += ShowResults;
            builder.BackPressed += EnterTitle;
            vocab.MenuPressed += EnterTitle;
            builder.GoLivePressed += StartLive;

            OVRManager.InputFocusLost += () => { focusLost = true; RefreshPause(); };
            OVRManager.InputFocusAcquired += () => { focusLost = false; RefreshPause(); };

            EnterTitle();
            // Something to listen to straight away while you look around the menu.
            board.A.deck.Load(0);
            board.B.deck.Load(1);
        }

        void OnApplicationPause(bool pause) { appPaused = pause; RefreshPause(); }
        void OnApplicationFocus(bool focus) { if (!Application.isEditor) { appUnfocused = !focus; RefreshPause(); } }

        // Three separate signals can pause the game (system menu, headset off, app in the background),
        // and they can arrive in any order, so it only resumes once all of them have cleared.
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
        }

        void EnterTitle()
        {
            lessons.Stop();
            performer.Stop();
            recorder.StopReplay();
            board.A.deck.Pause();
            board.B.deck.Pause();
            SetMode(GameMode.Title);
            int best = PlayerPrefs.GetInt("bestSet", 0);
            startMenu.Show("DJ MASTER", "Learn to DJ with just your hands",
                best > 0 ? $"Best live set: {best} pts" : LessonDirector.IsDone(0) ? "" : "New to DJing? Start with LEARN.");
            startMenu.SetRows(
                ("LEARN", "Three short lessons: your first mix, fades and cuts, then sync, loops and effects."),
                ("BUILD A SET", "Pick songs and transitions, then perform them live for a crowd."),
                ("FREESTYLE", "Just play. No timer, no rules."),
                ("DJ VOCAB", "What do BPM, cue, drop and the rest mean? Learn the DJ lingo."),
                ("COLOR-BLIND", BoardColors.ColorSafe
                    ? "ON: color-blind friendly colors, and every color has a shape. ◄ Deck A  ► Deck B  ● PLAY  ■ CUE  ♦ FX  ♥ CROWD"
                    : "OFF: tap for color-blind friendly colors, with a shape on every color so nothing relies on color alone."));
            startMenu.buttons[4].Lit = BoardColors.ColorSafe;
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
            string what = next == GameMode.Learn ? "LEARN" : next == GameMode.Build ? "BUILD A SET" : "FREESTYLE";
            startMenu.Show("WHERE DO YOU WANT TO PLAY?", $"Mode: {what}", "You can switch anytime with the SCENE panel next to the booth.");
            startMenu.SetRows(
                ("MY ROOM", "Mixed reality: the booth appears in your real surroundings."),
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
                ("MENU", ""));
        }

        void MenuAction(int i)
        {
            switch (Mode)
            {
                case GameMode.Title:
                    if (i == 3) ShowVocab();
                    else if (i == 4) { BoardColors.SetColorSafe(!BoardColors.ColorSafe); EnterTitle(); }
                    else ChooseVenue(i == 0 ? GameMode.Learn : i == 1 ? GameMode.Build : GameMode.Freestyle);
                    break;
                case GameMode.ChooseVenue:
                    if (i == 3) { EnterTitle(); break; }
                    environment.SetMode(i == 0 ? EnvMode.Room : i == 1 ? EnvMode.Club : EnvMode.Festival);
                    if (ChosenMode == GameMode.Learn) ChooseLesson();
                    else if (ChosenMode == GameMode.Build) StartBuild();
                    else StartFreestyle();
                    break;
                case GameMode.ChooseLesson:
                    if (i == 3) EnterTitle();
                    else StartLearn(i);
                    break;
            }
        }

        void Action(int i)
        {
            switch (Mode)
            {
                case GameMode.Learn:
                    if (i == 0) ChooseLesson();
                    else if (i == 1) lessons.Skip();
                    else StartLearn(lessons.LessonIndex);
                    break;
                case GameMode.Live:
                    if (i == 0) EnterTitle();
                    else if (i == 1) StartLive();
                    else performer.Finish();
                    break;
                case GameMode.LiveResults:
                    if (i != 3) recorder.StopReplay();
                    if (i == 0) EnterTitle();
                    else if (i == 1) StartLive();
                    else if (i == 2) StartBuild();
                    else ListenBack();
                    break;
                case GameMode.Freestyle:
                    EnterTitle();
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

        void StartLive()
        {
            if (!builder.Set.Ready) { StartBuild(); return; }
            lessons.Stop();
            SetMode(GameMode.Live);
            panel.SetActions("MENU", "RESTART", "END SET");
            performer.Begin(builder.Set);
        }

        void StartFreestyle()
        {
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Freestyle);
            panel.SetActions("MENU");
            judge.ResetScore();
        }

        void ShowResults()
        {
            SetMode(GameMode.LiveResults);
            lastScore = judge.Score;
            if (lastScore > PlayerPrefs.GetInt("bestSet", 0)) { PlayerPrefs.SetInt("bestSet", (int)lastScore); PlayerPrefs.Save(); }
            Sfx.Cheer(1f);
            BeatBus.FireDrop();
            replayIndex = 0;
            replaying = "";
            panel.SetActions("MENU", "PLAY AGAIN", "EDIT SET", "LISTEN BACK");
        }

        void Update()
        {
            RefreshPanel();
        }

        static string Title(int track) => TrackLibrary.Tracks[track].title;

        void RefreshPanel()
        {
            switch (Mode)
            {
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
                    string title = $"LIVE  SONG {performer.SongIndex + 1}/{set.Count}  {TextUtil.Clock(performer.Elapsed)}";
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
                    bool listening = recorder.Replaying;
                    panel.Show("SET COMPLETE!", sb.ToString(),
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
