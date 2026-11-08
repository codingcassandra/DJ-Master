using System;
using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // Learn mode: three short guided lessons, each one control at a time and well under 10 minutes.
    //   1 FIRST MIX       the color code, the beat, phrases, and the three parts of a beatmix: tempo, beats, phrase
    //   2 FADE & CUT      the DJ loop, plus two transitions that need no beatmatching
    //   3 SYNC LOOPS FX   tap tempo, SYNC, a one-phrase beatmix, loops, and an echo out ("less is more")
    public class LessonDirector : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;

        public class Step
        {
            public string title;
            public string text;
            public string hint;
            public string[] highlight;
            public Func<bool> done;
            public float hold;        // condition must stay true this long
            public string praise = "Nice!";
            public Action enter;
            public Action onTap;      // what the TAP pad does during this step
            public string[] answers;  // color quiz: touching any of these controls completes the step
            public string color;      // color quiz: the color being asked for
            public Action tick;       // per-frame logic for steps that judge timing
        }

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount => steps.Count;
        public int LessonIndex { get; private set; }
        public static readonly string[] LessonNames = { "FIRST MIX", "FADE & CUT", "SYNC, LOOPS & FX" };
        public static string DoneKey(int lesson) => lesson == 0 ? "lessonComplete" : $"lesson{lesson + 1}Complete";
        public static bool IsDone(int lesson) => PlayerPrefs.GetInt(DoneKey(lesson), 0) == 1;
        public Step Current => Running && StepIndex < steps.Count ? steps[StepIndex] : null;
        public string Feedback { get; private set; } = "";
        public bool ShowingPraise => praiseTimer > 0;
        public bool ShowHint => stepTime > 25f;

        public event Action Completed;

        readonly List<Step>[] lessons = { new(), new(), new() };
        List<Step> steps;
        float heldFor, stepTime, praiseTimer;

        // Step-local state
        int tapStreak;
        bool filterWentDown;
        bool hornPressed;
        bool tapOnLine;
        bool quizHit;
        bool hotCuePressed, wasPlaying, timingOk, heard, sawRepeat, tooFast;
        float timer, together;
        int taps, startTrack;

        Deck A => board.A.deck;
        Deck B => board.B.deck;

        void Awake()
        {
            BuildFirstMix(lessons[0]);
            BuildFadeAndCut(lessons[1]);
            BuildSyncLoopsFx(lessons[2]);
            steps = lessons[0];
            board.tap.Pressed += OnTap;
            board.A.hotCues[0].Pressed += () => hotCuePressed = true;
            board.HornPressed += () => hornPressed = true;
        }

        void OnEnable() => DJControl.AnyTouched += OnTouched;
        void OnDisable() => DJControl.AnyTouched -= OnTouched;

        public void Begin(int lesson = 0)
        {
            LessonIndex = Mathf.Clamp(lesson, 0, lessons.Length - 1);
            steps = lessons[LessonIndex];
            board.ResetControls();
            A.Load(0);  // Neon Tides, 128 BPM house
            if (LessonIndex == 0) B.Unload();
            else
            {
                // Lessons 2 and 3 start mid-set: a song already playing to the room.
                A.Play();
                board.A.volume.SetValue(1f);
                board.crossfader.SetValue(0f);
                B.Load(LessonIndex == 1 ? 3 : 4); // Sundial (122, too slow to beatmix) / Glass Arcade (126)
            }
            Running = true;
            Finished = false;
            Goto(0);
        }

        public void Stop()
        {
            Running = false;
            board.ClearHighlights();
        }

        public void Skip()
        {
            if (!Running) return;
            praiseTimer = 0;
            Next();
        }

        void Goto(int i)
        {
            StepIndex = i;
            heldFor = 0; stepTime = 0;
            tapStreak = 0; filterWentDown = false; hornPressed = false; tapOnLine = false; quizHit = false;
            hotCuePressed = timingOk = heard = sawRepeat = tooFast = false;
            wasPlaying = B.IsPlaying;
            timer = together = 0; taps = 0;
            startTrack = A.TrackIndex;
            Feedback = "";
            board.ClearHighlights();
            if (i >= steps.Count) { Finish(); return; }
            var s = steps[i];
            s.enter?.Invoke();
            if (s.highlight != null)
                foreach (var id in s.highlight)
                    if (board.Find(id) is { } c) c.Highlighted = true;
        }

        void Next() => Goto(StepIndex + 1);

        void Finish()
        {
            Running = false;
            Finished = true;
            PlayerPrefs.SetInt(DoneKey(LessonIndex), 1);
            PlayerPrefs.Save();
            Sfx.Cheer(1f);
            BeatBus.FireDrop();
            Completed?.Invoke();
        }

        void Update()
        {
            if (!Running) return;
            if (praiseTimer > 0)
            {
                praiseTimer -= Time.deltaTime;
                if (praiseTimer <= 0) Next();
                return;
            }

            var s = steps[StepIndex];
            stepTime += Time.deltaTime;
            // Stuck on the color quiz? Point at the answers.
            if (s.answers != null && ShowHint)
                foreach (var id in s.answers)
                    if (board.Find(id) is { } c) c.Highlighted = true;
            s.tick?.Invoke();
            bool done = s.done();
            heldFor = done ? heldFor + Time.deltaTime : 0;
            if (done && heldFor >= s.hold)
            {
                Sfx.Success();
                Feedback = s.praise;
                praiseTimer = 1.6f;
                board.ClearHighlights();
            }
        }

        void OnTap()
        {
            if (!Running || praiseTimer > 0 || !A.IsPlaying) return;
            taps++;
            steps[StepIndex].onTap?.Invoke();
        }

        void OnTouched(DJControl c)
        {
            if (!Running || praiseTimer > 0) return;
            var s = steps[StepIndex];
            if (s.answers == null) return;
            if (Array.IndexOf(s.answers, c.id) >= 0) { quizHit = true; return; }
            string touched = BoardColors.TaggedName(c.id); // drawn in its color, so color-blind mode adds its shape
            if (touched == "") return;
            Feedback = $"That one is {touched}. Find something {s.color}.";
            Sfx.Fail();
        }

        void TapOnBeat()
        {
            if (DJMath.DistanceToBeat(A.Beat) < 0.2f) { tapStreak++; Feedback = new string('*', tapStreak) + "  on beat!"; }
            else { tapStreak = 0; Feedback = "Off the beat. Listen for the kick drum and try again."; Sfx.Fail(); }
        }

        void TapOnPhrase()
        {
            double bar = A.Beat / TrackSynth.BeatsPerBar;
            int nearestBar = (int)Math.Round(bar) % TrackSynth.Bars;
            bool onBarLine = DJMath.DistanceToBeat(bar) < 0.07f; // within about a quarter beat
            if (onBarLine && nearestBar % 4 == 0) { tapOnLine = true; return; }
            Feedback = onBarLine ? $"That was bar {nearestBar + 1}. Wait for bar 1, 5, 9 or 13." : "Not quite on the line. Count 1-2-3-4 with the bar counter.";
            Sfx.Fail();
        }

        void BuildFirstMix(List<Step> steps)
        {
            string blue = BoardColors.Blue("BLUE"), pink = BoardColors.Pink("PINK"), green = BoardColors.Green("GREEN"),
                orange = BoardColors.Orange("ORANGE"), yellow = BoardColors.Yellow("YELLOW"), red = BoardColors.Red("RED");
            string deckA = BoardColors.Blue("Deck A"), deckB = BoardColors.Pink("Deck B");

            // ---- The color code ----
            void Quiz(string title, string text, string color, string hint, params string[] answers) => steps.Add(new Step
            {
                title = title, text = text, hint = hint, color = color, answers = answers, done = () => quizHit,
                praise = $"Yes! That's {color}.",
            });
            Quiz("Colors: blue",
                $"Every control is color-coded, so you always know what it does. {blue} is {deckA}, the left deck. Touch anything {blue}.",
                "BLUE", "Look at the left half of the board: the ring around the big jog wheel and the HI / MID / LOW knobs.",
                "A.high", "A.mid", "A.low", "A.jog");
            Quiz("Colors: pink",
                $"{pink} is {deckB}, the right deck. Everything for the next song lives here. Touch anything {pink}.",
                "PINK", "Look at the right half of the board.",
                "B.high", "B.mid", "B.low", "B.jog");
            Quiz("Colors: yellow",
                $"{yellow} is for shaping the sound and for your ears only: the FILTER knobs and the PHONES buttons. Touch something {yellow}.",
                "YELLOW", "The yellow knobs are the lowest knob on each side of the mixer, labelled FILTER.",
                "A.filter", "B.filter", "A.headphones", "B.headphones");
            Quiz("Colors: orange",
                $"{orange} is CUE: it jumps a song back to a starting point. The {orange} pads jump to bar 1, 5, 9 or 13. {green} is PLAY. Touch something {orange}.",
                "ORANGE", "The big round CUE button is just above PLAY, at the front corner of each deck.",
                "A.cue", "B.cue", "A.hotcue0", "A.hotcue1", "A.hotcue2", "A.hotcue3", "B.hotcue0", "B.hotcue1", "B.hotcue2", "B.hotcue3");

            // ---- Playing a track ----
            steps.Add(new Step
            {
                title = "Welcome to the booth",
                text = $"Now you know the colors. {green} means go! Tap the {green} PLAY button on {deckA} (the {blue} side) to start your first record.",
                hint = "PLAY is the big round button at the front left corner of the board. Poke it with your fingertip.",
                highlight = new[] { "A.play" },
                done = () => A.IsPlaying,
                praise = "It's playing!",
                enter = () =>
                {
                    // Undo anything poked during the color quiz.
                    board.ResetControls();
                    if (A.TrackIndex != 0) A.Load(0);
                    else A.Cue();
                    B.Unload();
                },
            });
            steps.Add(new Step
            {
                title = "Volume up",
                text = $"You can't hear it yet because {deckA}'s channel fader is down. Touch the fader cap and slide it all the way up, away from you.",
                hint = "The channel fader is the vertical slider just left of center. Rest your fingertip on its cap and push.",
                highlight = new[] { "A.volume" },
                done = () => board.A.volume.value > 0.9f,
            });
            steps.Add(new Step
            {
                title = "The crossfader",
                text = $"The crossfader blends between the two decks. Slide it all the way LEFT, toward the {blue} A, to give {deckA} to the whole room.",
                hint = "The crossfader is the sideways slider at the front of the mixer.",
                highlight = new[] { "crossfader" },
                done = () => board.crossfader.value < 0.08f,
                praise = "Listen to that crowd!",
            });
            steps.Add(new Step
            {
                title = "Feel the beat",
                text = $"Dance music counts in fours: every kick drum is one beat. Tap the round {red} TAP button between the channel faders on each kick, four in a row.",
                hint = "Tap exactly when you hear the boom of the kick. Nod your head to find it first.",
                highlight = new[] { "tap" },
                done = () => tapStreak >= 4,
                onTap = TapOnBeat,
                praise = "You've got rhythm!",
            });
            steps.Add(new Step
            {
                title = "Count the phrases",
                text = $"Four beats make a bar, and songs change every 4 bars. Watch {deckA}'s bar counter and tap {red} TAP right as it rolls over to bar 1, 5, 9 or 13.",
                hint = "Count along: 1-2-3-4, 2-2-3-4, 3-2-3-4, 4-2-3-4... then TAP on the next 1.",
                highlight = new[] { "tap" },
                done = () => tapOnLine,
                onTap = TapOnPhrase,
                praise = "That's phrasing. Good DJs always mix on these lines.",
            });

            // ---- Preparing the next track ----
            steps.Add(new Step
            {
                title = "Load Deck B",
                text = $"Time to pick the next record. Tap the {pink} LOAD button on {deckB}.",
                hint = "LOAD is the small pink button just below Deck B's jog wheel, above the round CUE button.",
                highlight = new[] { "B.load" },
                done = () => B.IsLoaded,
            });
            steps.Add(new Step
            {
                title = "Headphone cue",
                text = $"DJs prepare the next song in their headphones. Tap {deckB}'s {yellow} PHONES button so only YOU can hear it.",
                hint = "The two yellow PHONES buttons are at the top middle of the mixer. Press the B one.",
                highlight = new[] { "B.headphones" },
                done = () => mixer.headphoneCue[1],
            });
            steps.Add(new Step
            {
                title = "Start Deck B",
                text = $"Tap {green} PLAY on {deckB}. It's quieter because it's only in your headphones; the crowd still hears {deckA}.",
                highlight = new[] { "B.play" },
                done = () => B.IsPlaying,
                praise = "Two tracks at once. Hear the clash? Let's fix it.",
            });

            // ---- Beatmatching ----
            steps.Add(new Step
            {
                title = "Beatmix part 1: tempo",
                text = $"A beatmix has three parts: tempo, beats and phrase. Part 1: {deckA} runs at 128 BPM but {deckB} is slower. Slide {deckB}'s TEMPO fader up until both BPM readouts match.",
                hint = "TEMPO is the long slider on the far right edge. Small moves! Watch the BPM difference shrink to 0.0.",
                highlight = new[] { "B.tempo" },
                done = () => B.IsPlaying && Mathf.Abs(B.BPM - A.BPM) < 0.35f,
                hold = 1f,
                praise = "Tempos matched!",
            });
            steps.Add(new Step
            {
                title = "Beatmix part 2: the beats",
                text = $"Same speed, but the kicks don't land together. Spin {deckB}'s {pink} jog wheel to nudge it until the SYNC light turns green and both platter markers point the same way.",
                hint = "Spin clockwise to catch up, counter-clockwise to hold back. Short gentle spins.",
                highlight = new[] { "B.jog" },
                done = () => A.IsPlaying && B.IsPlaying && Mathf.Abs(B.BPM - A.BPM) < 0.6f && Mathf.Abs(DJMath.PhaseOffset(A.Beat, B.Beat)) < 0.07f,
                hold = 1.5f,
                praise = "Locked in! That's beatmatching, the core DJ skill.",
            });

            // ---- The mix ----
            steps.Add(new Step
            {
                title = "Make room for the bass",
                text = $"Two basslines at once sound muddy. Turn {deckB}'s LOW knob all the way down before the crowd hears it.",
                hint = "Touch the LOW knob and slide your finger toward you to turn it down.",
                highlight = new[] { "B.low" },
                done = () => board.B.low.value < 0.08f,
            });
            steps.Add(new Step
            {
                title = "Beatmix part 3: the phrase",
                text = $"Bring {deckB} in on a phrase line (bar 1, 5, 9 or 13): raise its channel fader all the way and slide the crossfader to the middle. Now the crowd hears both songs.",
                highlight = new[] { "B.volume", "crossfader" },
                done = () => board.B.volume.value > 0.85f && board.crossfader.value > 0.35f && board.crossfader.value < 0.65f,
                enter = () => mixer.headphoneCue[1] = false,
                praise = "Two songs, one groove.",
            });
            steps.Add(new Step
            {
                title = "Bass swap",
                text = $"On the next 4-bar line, swap the basslines: turn {deckA}'s LOW down and {deckB}'s LOW up.",
                hint = "Do both knobs at once, one hand each: left hand down, right hand up.",
                highlight = new[] { "A.low", "B.low" },
                done = () => board.A.low.value < 0.15f && board.B.low.value > 0.4f,
                praise = "Smooth bass swap!",
            });
            steps.Add(new Step
            {
                title = "Finish the mix",
                text = $"Slide the crossfader all the way to the {pink} B, then pull {deckA}'s channel fader down. You just mixed your first transition!",
                highlight = new[] { "crossfader", "A.volume" },
                done = () => board.crossfader.value > 0.92f && board.A.volume.value < 0.1f,
                praise = "TRANSITION COMPLETE!",
                enter = () => Sfx.Cheer(0.6f),
            });

            // ---- Effects ----
            steps.Add(new Step
            {
                title = "Filter sweep",
                text = $"Effects build tension. Turn {deckB}'s {yellow} FILTER knob down to the left (it sounds muffled), then back to the center for a big release.",
                hint = "Center is straight up. Double-tap any knob to snap it back to center.",
                highlight = new[] { "B.filter" },
                done = () =>
                {
                    if (board.B.filter.value < 0.2f) filterWentDown = true;
                    return filterWentDown && Mathf.Abs(board.B.filter.value - 0.5f) < 0.06f;
                },
                praise = "Big release!",
            });
            steps.Add(new Step
            {
                title = "Drop it!",
                text = $"{red} is for the crowd: the {red} SAMPLER pads play sound effects. Every DJ needs a signature move: hit HORN. Then try BUILD A SET to plan your own show!",
                highlight = new[] { "airhorn" },
                done = () => hornPressed,
                praise = "LESSON COMPLETE!",
            });
        }

        // A phrase line on a deck: within about half a beat of bar 1, 5, 9 or 13. Returns the bar (1-based) for feedback.
        static bool OnPhrase(Deck d, out int bar, out bool onBar)
        {
            double b = d.Beat / TrackSynth.BeatsPerBar;
            int nearest = (int)Math.Round(b);
            bar = nearest % TrackSynth.Bars + 1;
            onBar = DJMath.DistanceToBeat(b) < 0.15f;
            return onBar && nearest % 4 == 0;
        }

        bool XfMiddle => board.crossfader.value > 0.3f && board.crossfader.value < 0.7f;

        // Lesson 2: the DJ loop, the FADE and the CUT. Deck A plays Neon Tides (128); Deck B has Sundial (122) loaded.
        void BuildFadeAndCut(List<Step> steps)
        {
            string deckA = BoardColors.Blue("Deck A"), deckB = BoardColors.Pink("Deck B");
            string phones = BoardColors.Yellow("PHONES"), play = BoardColors.Green("PLAY"), cue = BoardColors.Orange("CUE");

            steps.Add(new Step
            {
                title = "The DJ loop",
                text = $"Every DJ repeats one loop: load the next song, preview it in your headphones, bring it in, perform the transition, stop the old song. {deckB} is loaded. Tap its {phones} button.",
                highlight = new[] { "B.headphones" },
                done = () => mixer.headphoneCue[1],
            });
            steps.Add(new Step
            {
                title = "Preview",
                text = $"Press {play} on {deckB}. Only you hear it, in your headphones. It's 122 BPM, much slower than {deckA}: too far apart to beatmix. Today's transitions don't need beatmatching.",
                highlight = new[] { "B.play" },
                done = () => B.IsPlaying,
                praise = "That's previewing.",
            });
            steps.Add(new Step
            {
                title = "Ready at bar 1",
                text = $"Press {deckB}'s {cue}: it jumps back to bar 1 and waits there, ready to start on a phrase.",
                highlight = new[] { "B.cue" },
                done = () => !B.IsPlaying && B.Beat < 0.05,
            });

            // ---- The fade ----
            steps.Add(new Step
            {
                title = "The fade: set up",
                text = $"A FADE works between any two songs, even different speeds. Slide the crossfader to the middle, then raise {deckB}'s channel fader. {deckB} is paused, so it stays silent.",
                highlight = new[] { "crossfader", "B.volume" },
                done = () => XfMiddle && board.B.volume.value > 0.85f,
                enter = () => mixer.headphoneCue[1] = false,
            });
            steps.Add(new Step
            {
                title = "The fade: the dip",
                text = $"Pull {deckA}'s fader down sharply, to about two thirds. A clear dip tells the dance floor a fade is starting. A timid fade just sounds like a mistake.",
                highlight = new[] { "A.volume" },
                done = () => board.A.volume.value < 0.7f,
            });
            steps.Add(new Step
            {
                title = "The fade: slowly",
                text = $"Now keep fading {deckA} down slowly, over about 10 seconds.",
                hint = "Move the fader a little at a time. Count to ten as you go.",
                highlight = new[] { "A.volume" },
                enter = () => timer = 0,
                tick = () =>
                {
                    timer += Time.deltaTime;
                    float v = board.A.volume.value;
                    if (!tooFast && v < 0.12f && timer < 6f)
                    {
                        tooFast = true;
                        Feedback = "Too fast! Push it back up to about two thirds and fade slower.";
                        Sfx.Fail();
                    }
                    else if (tooFast && v > 0.6f) { tooFast = false; timer = 0; }
                    if (!tooFast) Feedback = $"Fading... {timer:0} s";
                },
                done = () => !tooFast && board.A.volume.value < 0.12f && timer >= 6f,
            });
            steps.Add(new Step
            {
                title = "The fade: drop the new song",
                text = $"As {deckA} disappears, start {deckB} on a phrase: press its {play} right as {deckA}'s bar counter hits 1, 5, 9 or 13.",
                hint = "Watch Deck A's screen and count along: 1-2-3-4, 2-2-3-4... Press PLAY on the next 1.",
                highlight = new[] { "B.play" },
                tick = () =>
                {
                    if (B.IsPlaying && !wasPlaying && !timingOk)
                    {
                        if (OnPhrase(A, out int bar, out bool onBar)) timingOk = true;
                        else
                        {
                            Feedback = onBar ? $"That was bar {bar}. Wait for bar 1, 5, 9 or 13." : "Not on the bar line. Count along with Deck A's beats.";
                            Sfx.Fail();
                            B.Cue();
                        }
                    }
                    wasPlaying = B.IsPlaying;
                },
                done = () => timingOk,
                praise = "Smooth fade!",
            });
            steps.Add(new Step
            {
                title = "Stop the old song",
                text = $"Last part of the DJ loop: pull {deckA}'s fader to zero and press its {play} to stop it. Now {deckA} is free for the next song.",
                highlight = new[] { "A.volume", "A.play" },
                done = () => board.A.volume.value < 0.05f && !A.IsPlaying,
            });

            // ---- The cut ----
            steps.Add(new Step
            {
                title = "The cut: load",
                text = $"A CUT is an instant switch, exactly on the beat. Load a new song: tap {deckA}'s {BoardColors.Blue("LOAD")}.",
                highlight = new[] { "A.load" },
                done = () => A.IsLoaded && A.TrackIndex != startTrack,
            });
            steps.Add(new Step
            {
                title = "The cut: set up",
                text = $"Slide the crossfader all the way to {deckB}, then raise {deckA}'s fader. The crossfader keeps {deckA} silent until the moment you cut.",
                highlight = new[] { "crossfader", "A.volume" },
                done = () => board.crossfader.value > 0.92f && board.A.volume.value > 0.85f,
            });
            steps.Add(new Step
            {
                title = "The cut: hot cue",
                text = $"Tap {deckA}'s {BoardColors.Orange("HOT CUE 1")} pad. Hot cues are saved starting points: this one waits at bar 1.",
                highlight = new[] { "A.hotcue0" },
                done = () => hotCuePressed,
            });
            steps.Add(new Step
            {
                title = "Cut!",
                text = $"Watch {deckB}'s bar counter. Right on bar 1, 5, 9 or 13: press {play} on {deckA} AND slam the crossfader to {deckA}, at the same moment.",
                hint = "Rest one finger on Deck A's PLAY and another on the crossfader. Count 1-2-3-4 along with Deck B, then go on the 1.",
                highlight = new[] { "A.play", "crossfader" },
                tick = () =>
                {
                    float gA = mixer.CrowdGain(0), gB = mixer.CrowdGain(1);
                    if (gA > 0.15f && !heard)
                    {
                        heard = true;
                        timingOk = OnPhrase(B, out _, out _);
                    }
                    if (gA > 0.15f && gB > 0.15f) together += Time.deltaTime;
                    bool switched = mixer.DominantDeck == 0 && gB < 0.05f;
                    bool late = A.IsPlaying && !heard && A.Beat > TrackSynth.BeatsPerBar; // played but never cut across
                    if (switched && timingOk && together < 0.75f) { quizHit = true; return; }
                    if (switched || late)
                    {
                        Feedback = late ? "Slam the crossfader at the same moment as PLAY!"
                            : !timingOk ? "Not on a phrase line. Wait for bar 1, 5, 9 or 13 on Deck B."
                            : "Too slow: a cut has no overlap. Slam the crossfader in one move.";
                        Sfx.Fail();
                        A.Cue();
                        board.crossfader.SetValue(1f);
                        heard = timingOk = false;
                        together = 0;
                    }
                },
                done = () => quizHit,
                praise = "Perfect cut!",
            });
            steps.Add(new Step
            {
                title = "Stop the old song",
                text = $"Press {play} on {deckB} to stop it. That's the whole DJ loop: load, preview, bring in, transition, stop.",
                highlight = new[] { "B.play" },
                done = () => !B.IsPlaying,
                praise = "LESSON COMPLETE!",
            });
        }

        // Lesson 3: tap tempo, SYNC, the one-phrase beatmix, loops and effects.
        // Deck A plays Neon Tides (128); Deck B has Glass Arcade (126) loaded.
        void BuildSyncLoopsFx(List<Step> steps)
        {
            string deckA = BoardColors.Blue("Deck A"), deckB = BoardColors.Pink("Deck B");
            string phones = BoardColors.Yellow("PHONES"), play = BoardColors.Green("PLAY");

            steps.Add(new Step
            {
                title = "Tap the tempo",
                text = $"DJs find a song's BPM by tapping along. Tap the {BoardColors.Red("TAP")} button on every kick drum, and keep going until your BPM shows up.",
                hint = "Steady taps, one per boom. Nod your head first to find the beat.",
                highlight = new[] { "tap" },
                tick = () => { if (board.TapBpm > 0) Feedback = $"You're tapping {board.TapBpm:0.0} BPM. The song is {A.BPM:0}."; },
                done = () => taps >= 4 && Mathf.Abs(board.TapBpm - A.BPM) < 2.5f,
                praise = "Spot on!",
            });
            steps.Add(new Step
            {
                title = "Preview the next song",
                text = $"Tap {deckB}'s {phones}, then its {play}, to hear it in your headphones.",
                highlight = new[] { "B.headphones", "B.play" },
                done = () => mixer.headphoneCue[1] && B.IsPlaying,
            });
            steps.Add(new Step
            {
                title = "SYNC",
                text = $"{deckB} is 126 BPM, {deckA} is 128. SYNC matches the tempo AND lines up the beats in one press. Press {deckB}'s SYNC. (Pros learn by hand first, like in lesson 1: SYNC can be fooled by songs with a wobbly tempo.)",
                highlight = new[] { "B.sync" },
                done = () => judge.InSync,
                hold = 0.5f,
                praise = "Synced!",
            });
            steps.Add(new Step
            {
                title = "One-phrase beatmix",
                text = $"On a phrase line (bar 1, 5, 9 or 13), raise {deckB}'s channel fader and slide the crossfader to the middle.",
                highlight = new[] { "B.volume", "crossfader" },
                done = () => board.B.volume.value > 0.85f && XfMiddle,
                enter = () => mixer.headphoneCue[1] = false,
            });
            steps.Add(new Step
            {
                title = "Ride it for 8 bars",
                text = "Let both songs play together for one phrase, about 8 bars. Mix over the drums, and don't go much longer: two kick drums for ages gets muddy.",
                tick = () =>
                {
                    if (mixer.CrowdGain(0) > 0.15f && mixer.CrowdGain(1) > 0.15f && judge.InSync) together += Time.deltaTime;
                    Feedback = $"Together: bar {Mathf.Min(8, Bars(together) + 1):0} of 8";
                },
                done = () => Bars(together) >= 7.5f,
                praise = "That's a phrase.",
            });
            steps.Add(new Step
            {
                title = "Finish the mix",
                text = $"Slide the crossfader all the way to {deckB}, then stop {deckA} with its {play}.",
                highlight = new[] { "crossfader", "A.play" },
                done = () => board.crossfader.value > 0.92f && !A.IsPlaying,
            });
            steps.Add(new Step
            {
                title = "Loops",
                text = $"A LOOP repeats the current 4-bar phrase, to stretch an intro or outro while you get ready. Press {deckB}'s {BoardColors.Orange("LOOP")}.",
                highlight = new[] { "B.loop" },
                done = () => B.Looping,
            });
            steps.Add(new Step
            {
                title = "Let it repeat",
                text = $"Watch the bar counter jump back. Once the phrase has repeated, press {BoardColors.Orange("LOOP")} again so the song carries on. Use loops for moments: too many make a set boring.",
                highlight = new[] { "B.loop" },
                tick = () =>
                {
                    if (B.Looping && B.LoopRepeats >= 1) sawRepeat = true;
                    if (!B.Looping && !sawRepeat) Feedback = "Wait for it to repeat once first. Press LOOP again.";
                    else if (sawRepeat && B.Looping) Feedback = "It repeated! Now press LOOP to release it.";
                },
                done = () => sawRepeat && !B.Looping,
            });
            steps.Add(new Step
            {
                title = "Effects: ECHO",
                text = $"Effects are seasoning. Turn on {deckB}'s {BoardColors.Yellow("ECHO")} and listen to the repeats.",
                highlight = new[] { "B.echo" },
                done = () => B.Strip.echo,
            });
            steps.Add(new Step
            {
                title = "Echo out",
                text = $"An ECHO OUT ends a song in style: with ECHO on, pull {deckB}'s fader all the way down on a phrase line. The echoes ring on after the music stops.",
                highlight = new[] { "B.volume" },
                done = () => B.Strip.echo && board.B.volume.value < 0.05f,
                praise = "Echo out!",
            });
            steps.Add(new Step
            {
                title = "Less is more",
                text = $"Turn the {BoardColors.Yellow("ECHO")} off and bring {deckB}'s fader back up. The rule pros follow: less is more. Effects should add to the music, never cover it.",
                highlight = new[] { "B.echo", "B.volume" },
                done = () => !B.Strip.echo && board.B.volume.value > 0.85f,
                praise = "LESSON COMPLETE!",
            });
        }

        float Bars(float seconds) => seconds * Mathf.Max(60f, A.BPM) / 60f / TrackSynth.BeatsPerBar;
    }
}
