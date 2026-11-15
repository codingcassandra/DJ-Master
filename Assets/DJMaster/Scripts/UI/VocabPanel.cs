using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DJMaster
{
    // DJ VOCAB on the start screen: every DJ word with what it means, and a QUIZ ME button at the top.
    // The quiz shows a meaning (with the word blanked out) and four words to pick from.
    // After you answer the right word lights green and a wrong pick lights red. In color-blind mode they also get
    // their color's shape in front (● right, ♥ wrong), same as the board, so it's not color only.
    public class VocabPanel : MonoBehaviour
    {
        public const int QuizLength = 10;

        public TextMeshPro info;
        public PokeButton quizButton, menuButton;
        public TextMeshPro quizButtonLabel;

        [Header("Word list")]
        public GameObject listRoot;
        public TextMeshPro[] entries;
        public int wrapChars = 68;

        [Header("Quiz")]
        public GameObject quizRoot;
        public TextMeshPro progress, question, feedback;
        public int questionWrapChars = 44;
        public PokeButton[] answers = new PokeButton[4];
        public TextMeshPro[] answerLabels = new TextMeshPro[4];
        public PokeButton next;
        public TextMeshPro nextLabel;

        public event Action MenuPressed;

        public bool Quizzing { get; private set; }
        public int QuestionIndex { get; private set; }
        public int Score { get; private set; }
        public bool Answered { get; private set; }
        public bool Finished => QuestionIndex >= QuizLength;
        public int CorrectChoice { get; private set; } // which answer button holds the right word

        readonly List<int> order = new();
        readonly int[] choices = new int[4];
        int picked;
        string feedbackText = "";
        System.Random rng = new();

        void Awake()
        {
            quizButton.Pressed += () => { if (Quizzing) ShowList(); else StartQuiz(); };
            menuButton.Pressed += () => MenuPressed?.Invoke();
            for (int i = 0; i < answers.Length; i++)
            {
                int idx = i;
                answers[i].Pressed += () => Answer(idx);
            }
            next.Pressed += () => { if (Finished) StartQuiz(); else if (Answered) NextQuestion(); };
            FillList();
        }

        void OnEnable() => BoardColors.Changed += Redraw;
        void OnDisable() => BoardColors.Changed -= Redraw;

        // Color-blind mode was switched, so we draw the list or the question again with the new look
        void Redraw()
        {
            FillList();
            feedback.text = BoardColors.Display(feedbackText);
            if (!Quizzing || Finished) return;
            question.text = TextUtil.Wrap(BoardColors.Display(DJVocab.Clue(DJVocab.Words[order[QuestionIndex]])), questionWrapChars);
            if (Answered) ShowMarks();
        }

        public void FillList()
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (i >= DJVocab.Words.Length) { entries[i].text = ""; continue; }
                var w = DJVocab.Words[i];
                entries[i].text = TextUtil.Wrap(BoardColors.Display($"<b><color=#FFFFFF>{w.term}</color></b>  {w.meaning}"), wrapChars);
            }
        }

        // For the tests: the same questions every run.
        public void Seed(int seed) => rng = new System.Random(seed);

        public void ShowList()
        {
            FillList(); // picks up color-blind mode if it changed
            Quizzing = false;
            listRoot.SetActive(true);
            quizRoot.SetActive(false);
            quizButtonLabel.text = "QUIZ ME";
            int best = DJVocab.BestQuizScore;
            info.text = $"{DJVocab.Words.Length} words every DJ uses." + (best > 0 ? $"   Best quiz score: {best} / {QuizLength}" : "   Think you know them? Hit QUIZ ME.");
        }

        public void StartQuiz()
        {
            Quizzing = true;
            listRoot.SetActive(false);
            quizRoot.SetActive(true);
            quizButtonLabel.text = "WORD LIST";
            info.text = "Which word matches the meaning?";
            order.Clear();
            for (int i = 0; i < DJVocab.Words.Length; i++) order.Add(i);
            Shuffle(order);
            Score = 0;
            QuestionIndex = 0;
            ShowQuestion();
        }

        void NextQuestion()
        {
            QuestionIndex++;
            if (Finished) ShowResult();
            else ShowQuestion();
        }

        void ShowQuestion()
        {
            Answered = false;
            int target = order[QuestionIndex];
            var wrong = new List<int>();
            for (int i = 0; i < DJVocab.Words.Length; i++) if (i != target) wrong.Add(i);
            Shuffle(wrong);
            CorrectChoice = rng.Next(answers.Length);
            for (int i = 0, w = 0; i < answers.Length; i++)
            {
                choices[i] = i == CorrectChoice ? target : wrong[w++];
                answers[i].gameObject.SetActive(true);
                answers[i].Lit = false;
                answerLabels[i].text = DJVocab.Words[choices[i]].term;
            }
            progress.text = $"Question {QuestionIndex + 1} of {QuizLength}      Score: {Score}";
            question.text = TextUtil.Wrap(BoardColors.Display(DJVocab.Clue(DJVocab.Words[target])), questionWrapChars);
            SetFeedback("");
            next.gameObject.SetActive(false);
        }

        public void Answer(int i)
        {
            if (!Quizzing || Answered || Finished) return;
            Answered = true;
            picked = i;
            bool right = i == CorrectChoice;
            if (right) Score++;
            ShowMarks();
            string word = DJVocab.Words[choices[CorrectChoice]].term;
            SetFeedback(right ? BoardColors.Green("Correct!") : $"{BoardColors.Red("Not quite.")} It's {BoardColors.Green(word)}.");
            Sfx.Click();
            progress.text = $"Question {QuestionIndex + 1} of {QuizLength}      Score: {Score}";
            nextLabel.text = QuestionIndex + 1 < QuizLength ? "NEXT" : "SEE SCORE";
            next.gameObject.SetActive(true);
        }

        void ShowResult()
        {
            foreach (var a in answers) a.gameObject.SetActive(false);
            bool newBest = Score > DJVocab.BestQuizScore;
            if (newBest) DJVocab.BestQuizScore = Score;
            progress.text = "Quiz complete";
            question.text = $"You got {Score} out of {QuizLength}!\n" +
                (Score == QuizLength ? "Perfect. You talk like a real DJ." :
                 Score >= 7 ? "Nice work. You know your stuff." :
                 Score >= 4 ? "Getting there. Look over the list and go again." :
                 "Check the word list, then try again.");
            SetFeedback(newBest ? BoardColors.Green("New best score!") : $"Best: {DJVocab.BestQuizScore} / {QuizLength}");
            nextLabel.text = "TRY AGAIN";
            next.gameObject.SetActive(true);
        }

        // Lights the right word, and the wrong one if you picked it, in the colors we use right now
        void ShowMarks()
        {
            for (int i = 0; i < answers.Length; i++)
            {
                var family = i == CorrectChoice ? BoardColors.Family.Play : i == picked ? BoardColors.Family.Crowd : BoardColors.Family.None;
                string term = DJVocab.Words[choices[i]].term;
                answerLabels[i].text = BoardColors.ColorSafe && family != BoardColors.Family.None ? $"{BoardColors.Shape(family)} {term}" : term;
                if (family == BoardColors.Family.None) continue;
                answers[i].onColor = BoardColors.Of(family);
                answers[i].Lit = true;
            }
        }

        void SetFeedback(string text)
        {
            feedbackText = text;
            feedback.text = BoardColors.Display(text);
        }

        void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
