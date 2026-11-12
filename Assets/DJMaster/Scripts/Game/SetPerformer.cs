using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // Runs a set you planned in Build mode as a live show. The next song is loaded for you, a step-by-step
    // checklist (with the controls glowing) walks you through each planned transition, and every transition
    // is graded on how well you pulled off its style. Each transition is recorded so you can listen back,
    // and the whole set gets a critique like a DJ would give their own recording.
    public class SetPerformer : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;
        public MixRecorder recorder;
        [Tooltip("How many bars the last song plays before the set ends.")]
        public int finaleBars = 16;

        public enum Grade { Rough, Good, Perfect }

        public class Result
        {
            public int from, to;
            public TransitionStyle style;
            public Grade grade;
            public string note;
            public AudioClip recording;
        }

        public class Task
        {
            public string text;
            public string[] highlight;
            public Func<bool> done;
            public bool complete;
        }

        // The "listen back" checklist for the whole set.
        public class Critique
        {
            public float syncPercent = 100f;   // beat alignment while two songs played together
            public float clashSeconds;         // trainwrecks
            public float deadAirSeconds;       // silence
            public float echoSeconds, setSeconds;
            public readonly List<string> tempoJumps = new();
            public string practise = "";
        }

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public SetList Set { get; private set; }
        public int SongIndex { get; private set; }          // the song that owns the dance floor
        public int OutDeck => SongIndex % 2;               // songs alternate A, B, A...
        public int InDeck => 1 - OutDeck;
        public bool InFinale => Running && SongIndex >= Set.Count - 1;
        public TransitionStyle NextStyle => Set.transitions[SongIndex];
        public int NextTrack => Set.songs[SongIndex + 1];
        public float FinaleLeft { get; private set; }
        public float Elapsed { get; private set; }
        public readonly List<Result> Results = new();
        public readonly List<Task> Tasks = new();
        public Task CurrentTask { get; private set; }
        public int CurrentTaskIndex => CurrentTask == null ? Tasks.Count : Tasks.IndexOf(CurrentTask);
        public Critique Review { get; private set; } = new();

        public event Action Completed;

        // What happened during the current transition, for grading.
        float overlap, synced, clash, bassClash, transitionStart;
        float fadeStart, fadeEnd, outCutAt, inHeardAt;
        bool filterSwept, inHeard, inLowCutOnEntry, enteredOnPhrase, enteredOnBar, echoWhenCut, outCut;
        // Whole set, for the critique.
        float mixTime, mixSynced;

        Deck Out => board.channels[OutDeck].deck;
        Deck In => board.channels[InDeck].deck;

        public void Begin(SetList set)
        {
            Set = set;
            Results.Clear();
            Review = new Critique();
            mixTime = mixSynced = 0;
            board.ResetControls();
            board.A.deck.Load(set.songs[0]);
            board.A.deck.Play();
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.B.deck.Load(set.songs[1]);
            judge.ResetScore();
            SongIndex = 0;
            Elapsed = 0;
            Running = true;
            Finished = false;
            StartTransition();
        }

        public void Stop()
        {
            Running = false;
            board.ClearHighlights();
        }

        public void Finish()
        {
            if (!Running) return;
            Stop();
            Finished = true;
            BuildCritique();
            Completed?.Invoke();
        }

        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            Review.setSeconds += dt;
            if (mixer.DominantDeck < 0) Review.deadAirSeconds += dt;
            if (judge.Clashing) Review.clashSeconds += dt;
            if (board.A.deck.Strip.echo || board.B.deck.Strip.echo) Review.echoSeconds += dt;

            if (InFinale)
            {
                FinaleLeft -= dt;
                if (FinaleLeft <= 0) Finish();
                return;
            }

            foreach (var t in Tasks)
                if (!t.complete && t.done()) t.complete = true;
            var current = Tasks.Find(t => !t.complete);
            if (current != CurrentTask)
            {
                CurrentTask = current;
                board.ClearHighlights();
                if (current?.highlight != null)
                    foreach (var id in current.highlight)
                        if (board.Find(id) is { } c) c.Highlighted = true;
            }

            Watch(dt);

            // The transition is done once the crowd only hears the new song.
            if (mixer.DominantDeck == InDeck && mixer.CrowdGain(OutDeck) < 0.05f && In.IsPlaying) CompleteTransition();
        }

        void Watch(float dt)
        {
            float gOut = mixer.CrowdGain(OutDeck), gIn = mixer.CrowdGain(InDeck);
            var outFader = board.channels[OutDeck].volume.value;
            if (gIn > 0.15f && !inHeard)
            {
                inHeard = true;
                inHeardAt = Time.time;
                inLowCutOnEntry = In.Strip.low < 0.2f;
                double bar = Out.Beat / TrackSynth.BeatsPerBar;
                enteredOnBar = DJMath.DistanceToBeat(bar) < 0.15f;
                enteredOnPhrase = enteredOnBar && (int)Math.Round(bar) % 4 == 0;
            }
            if (gOut > 0.15f && gIn > 0.15f)
            {
                overlap += dt;
                if (judge.InSync) synced += dt;
                if (judge.Clashing) clash += dt;
                if (Out.Strip.low > 0.35f && In.Strip.low > 0.35f) bassClash += dt;
                if (Mathf.Abs(Out.Strip.filter - 0.5f) > 0.3f) filterSwept = true;
                if (SetList.NeedsBeatmatch(NextStyle)) { mixTime += dt; if (judge.InSync) mixSynced += dt; }
            }
            // Fades and echo outs happen on the old song's channel fader.
            if (outFader < 0.7f && fadeStart < 0) fadeStart = Time.time;
            if (outFader > 0.85f && !inHeard) fadeStart = -1;
            if (gOut < 0.05f && fadeEnd < 0 && fadeStart >= 0) fadeEnd = Time.time;
            if (!outCut && outFader < 0.08f)
            {
                outCut = true;
                outCutAt = Time.time;
                echoWhenCut = Out.Strip.echo;
            }
        }

        void CompleteTransition()
        {
            var style = NextStyle;
            var (grade, note) = GradeTransition(style);
            var result = new Result { from = Set.songs[SongIndex], to = NextTrack, style = style, grade = grade, note = note };
            Results.Add(result);
            if (recorder != null) StartCoroutine(Record(result, Time.time - Mathf.Max(transitionStart, (inHeard ? inHeardAt : Time.time) - 8f)));
            judge.Award($"{SetList.Name(style)}: {grade.ToString().ToUpper()}", grade == Grade.Perfect ? 1000 : grade == Grade.Good ? 600 : 200);
            if (grade == Grade.Perfect) BeatBus.FireDrop();

            int freed = OutDeck;
            SongIndex++;
            board.ClearHighlights();
            CurrentTask = null;
            Tasks.Clear();

            if (InFinale)
            {
                // SongIndex already moved on, so Out is the last song, the one playing now
                FinaleLeft = finaleBars * TrackSynth.BeatsPerBar * 60f / Mathf.Max(60f, Out.BPM);
                return;
            }
            // Load the next song onto the deck that just went quiet, with its controls back at home.
            var ch = board.channels[freed];
            ch.tempo.SetValue(0.5f);
            ch.volume.SetValue(0f);
            ch.high.SetValue(0.5f); ch.mid.SetValue(0.5f); ch.low.SetValue(0.5f); ch.filter.SetValue(0.5f);
            mixer.headphoneCue[freed] = false;
            ch.deck.Strip.echo = false;
            ch.deck.Load(NextTrack);
            StartTransition();
        }

        // Keep recording a couple of seconds past the end of the transition (from a few seconds before the
        // new song came in), then save it for LISTEN BACK.
        IEnumerator Record(Result result, float seconds)
        {
            yield return new WaitForSeconds(2f);
            result.recording = recorder.Snapshot(Mathf.Clamp(seconds + 2f, 6f, recorder.bufferSeconds));
        }

        (Grade, string) GradeTransition(TransitionStyle style)
        {
            float syncedBars = synced * Mathf.Max(60f, Out.BPM) / 60f / TrackSynth.BeatsPerBar;
            switch (style)
            {
                case TransitionStyle.Cut:
                    if (overlap < 0.6f && enteredOnPhrase) return (Grade.Perfect, "Right on the phrase!");
                    if (overlap < 2f) return (Grade.Good, enteredOnBar ? "Cut on bar 1, 5, 9 or 13 for a perfect phrase." : "Hit PLAY exactly on bar 1 of a phrase.");
                    return (Grade.Rough, "A cut has no overlap: PLAY and crossfader at the same moment.");
                case TransitionStyle.Fade:
                {
                    float len = fadeStart < 0 ? 0 : (fadeEnd > 0 ? fadeEnd : Time.time) - fadeStart;
                    if (len >= 6f && len <= 25f && enteredOnPhrase && clash < 1f) return (Grade.Perfect, "A confident, steady fade.");
                    if (len >= 3f && clash < 3f) return (Grade.Good, len < 6f ? "Fade slower: about 10 seconds." : "Start the new song on a phrase (bar 1, 5, 9, 13).");
                    return (Grade.Rough, clash >= 3f ? "The new song came in while the old one was still loud." : "Dip the fader, then fade over about 10 seconds.");
                }
                case TransitionStyle.EchoOut:
                {
                    float gap = outCut && inHeard ? inHeardAt - outCutAt : 99f;
                    if (echoWhenCut && enteredOnBar && gap < 4f) return (Grade.Perfect, "Echoes rang out, new song dropped on time.");
                    if (echoWhenCut) return (Grade.Good, gap >= 4f ? "Drop the new song sooner, while the echoes fade." : "Start the new song on bar 1.");
                    return (Grade.Rough, "Turn ECHO on before you pull the fader down.");
                }
                case TransitionStyle.BassSwap:
                    if (inLowCutOnEntry && bassClash < 1.5f && clash < 1f && synced >= 3f) return (Grade.Perfect, "Clean basslines all the way.");
                    if (bassClash < 4f && clash < 3f) return (Grade.Good, inLowCutOnEntry ? "Swap the LOWs sooner." : "Cut the new song's LOW before you bring it in.");
                    return (Grade.Rough, clash >= 3f ? "The beats clashed: match TEMPO and JOG first." : "Two basslines at once got muddy.");
                case TransitionStyle.Filter:
                    if (filterSwept && clash < 1f) return (Grade.Perfect, "Great filter sweep.");
                    if (filterSwept || (clash < 1f && synced > 1.5f)) return (Grade.Good, filterSwept ? "Beatmatch before you blend." : "Sweep the old song's FILTER while you mix.");
                    return (Grade.Rough, "Beatmatch first, then sweep the FILTER.");
                default:
                    if (clash < 1f && syncedBars >= 4f && syncedBars <= 20f && enteredOnPhrase) return (Grade.Perfect, $"{syncedBars:0} bars together, right on the phrase.");
                    if (clash < 3f && syncedBars >= 1.5f)
                        return (Grade.Good, syncedBars > 20f ? "Too long together sounds muddy: keep it to about 8 bars."
                            : syncedBars < 4f ? "Let both songs play for a full phrase (about 8 bars)." : "Bring the new song in on bar 1, 5, 9 or 13.");
                    return (Grade.Rough, "The beats clashed: match TEMPO and JOG (or press SYNC) before you crossfade.");
            }
        }

        void StartTransition()
        {
            overlap = synced = clash = bassClash = 0;
            filterSwept = inHeard = inLowCutOnEntry = enteredOnPhrase = enteredOnBar = echoWhenCut = outCut = false;
            fadeStart = fadeEnd = -1;
            transitionStart = Time.time;
            Tasks.Clear();
            CurrentTask = null;

            int o = OutDeck, n = InDeck;
            string O = o == 0 ? "A" : "B", N = n == 0 ? "A" : "B";
            string outName = BoardColors.Deck(o, $"Deck {O}"), inName = BoardColors.Deck(n, $"Deck {N}");
            var inCh = board.channels[n];
            var outCh = board.channels[o];
            bool XfToIn() => n == 0 ? board.crossfader.value < 0.08f : board.crossfader.value > 0.92f;
            bool XfMiddle() => board.crossfader.value > 0.3f && board.crossfader.value < 0.7f;

            void Add(string text, Func<bool> done, params string[] ids) => Tasks.Add(new Task { text = text, done = done, highlight = ids });
            void Phones() => Add($"Preview it: press {inName}'s {BoardColors.Yellow("PHONES")}, then {BoardColors.Green("PLAY")}.",
                () => mixer.headphoneCue[n] && In.IsPlaying || mixer.CrowdGain(n) > 0.15f, $"{N}.headphones", $"{N}.play");
            void Tempo() => Add($"Slide {inName}'s TEMPO until its BPM matches {outName}.", () => In.IsLoaded && Mathf.Abs(In.BPM - Out.BPM) < 0.35f, $"{N}.tempo");
            void Sync() => Add($"Line up the kicks: nudge {inName}'s JOG (or press SYNC) until SYNC turns green.", () => judge.InSync, $"{N}.jog", $"{N}.sync");
            void Fader() => Add($"Raise {inName}'s channel fader all the way.", () => inCh.volume.value > 0.85f, $"{N}.volume");
            void Middle() => Add("On a phrase line (bar 1, 5, 9 or 13), crossfader to the middle.", XfMiddle, "crossfader");
            void CrossOver() => Add($"Finish: crossfader all the way to {inName}.", XfToIn, "crossfader");
            void CueIn() => Add($"Press {BoardColors.Orange("CUE")} on {inName} so it waits at bar 1.", () => In.IsLoaded && !In.IsPlaying && In.Beat < 0.05, $"{N}.cue");

            switch (NextStyle)
            {
                case TransitionStyle.BassSwap:
                    Add($"Turn {inName}'s LOW all the way down first.", () => In.Strip.low < 0.1f, $"{N}.low");
                    Phones(); Tempo(); Sync(); Fader(); Middle();
                    Add($"When the new bassline should take over, swap: {outName} LOW down, {inName} LOW up.", () => Out.Strip.low < 0.15f && In.Strip.low > 0.4f, $"{O}.low", $"{N}.low");
                    CrossOver();
                    break;
                case TransitionStyle.Filter:
                    Phones(); Tempo(); Sync(); Fader(); Middle();
                    Add($"Sweep {outName}'s {BoardColors.Yellow("FILTER")} down to muffle it.", () => Out.Strip.filter < 0.2f || Out.Strip.filter > 0.8f, $"{O}.filter");
                    CrossOver();
                    break;
                case TransitionStyle.Cut:
                    Tempo(); CueIn(); Fader();
                    Add($"Count {outName}'s bars. On bar 1, 5, 9 or 13: {BoardColors.Green("PLAY")} on {inName} and SLAM the crossfader across!", () => false, $"{N}.play", "crossfader");
                    break;
                case TransitionStyle.Fade:
                    CueIn();
                    Add("Crossfader to the middle: in a fade, the channel faders do the work.", XfMiddle, "crossfader");
                    Fader();
                    Add($"Dip {outName}'s fader sharply to about two thirds, so everyone hears the fade begin.", () => outCh.volume.value < 0.7f, $"{O}.volume");
                    Add($"Keep fading {outName} down slowly, over about 10 seconds.", () => outCh.volume.value < 0.15f, $"{O}.volume");
                    Add($"As it disappears, on bar 1 of a phrase: {BoardColors.Green("PLAY")} on {inName}!", () => false, $"{N}.play");
                    break;
                case TransitionStyle.EchoOut:
                    CueIn();
                    Add("Crossfader to the middle.", XfMiddle, "crossfader");
                    Fader();
                    Add($"Turn on {outName}'s {BoardColors.Yellow("ECHO")}.", () => Out.Strip.echo, $"{O}.echo");
                    Add($"On a phrase line, pull {outName}'s fader all the way down: the echoes ring out.", () => outCh.volume.value < 0.08f, $"{O}.volume");
                    Add($"While the echoes fade, on bar 1: {BoardColors.Green("PLAY")} on {inName}!", () => false, $"{N}.play");
                    break;
                default:
                    Phones(); Tempo(); Sync(); Fader(); Middle();
                    Add("Let both songs play together for one phrase: about 8 bars.", () => synced * Mathf.Max(60f, Out.BPM) / 60f / TrackSynth.BeatsPerBar >= 7.5f);
                    CrossOver();
                    break;
            }
        }

        void BuildCritique()
        {
            var r = Review;
            r.syncPercent = mixTime > 1f ? 100f * mixSynced / mixTime : 100f;
            r.tempoJumps.Clear();
            for (int i = 0; i + 1 < Set.Count && i < Set.transitions.Count; i++)
                if (SetList.Advice(Set.songs[i], Set.songs[i + 1], Set.transitions[i]) != null)
                    r.tempoJumps.Add($"{TrackLibrary.Tracks[Set.songs[i]].title} > {TrackLibrary.Tracks[Set.songs[i + 1]].title}");

            // The one thing to practise next: the weakest area.
            Result worst = null;
            foreach (var res in Results) if (worst == null || res.grade < worst.grade) worst = res;
            r.practise = r.clashSeconds > 5f ? "Beatmatching: match TEMPO first, then nudge the JOG until SYNC is green."
                : r.deadAirSeconds > 3f ? "No dead air: always have the next song ready before the old one ends."
                : r.echoSeconds > r.setSeconds * 0.3f ? "Less is more: use ECHO for moments, not the whole set."
                : r.tempoJumps.Count > 0 ? "Programming: beatmix songs within 4% tempo, or use a FADE or CUT."
                : worst != null && worst.grade < Grade.Perfect ? $"{SetList.Name(worst.style)}: {worst.note}"
                : Results.Count < Set.Count - 1 ? "Play the whole set through to the last song."
                : "Headliner material. Try a longer set with different transitions!";
        }

        public static int Stars(IReadOnlyList<Result> results)
        {
            if (results.Count == 0) return 0;
            int points = 0;
            foreach (var r in results) points += (int)r.grade;
            float avg = points / (float)results.Count;
            return avg >= 1.6f ? 3 : avg >= 0.9f ? 2 : 1;
        }
    }
}
