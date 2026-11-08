using System.Collections.Generic;
using NUnit.Framework;

namespace DJMaster.Tests
{
    public class DJVocabTests
    {
        [Test]
        public void WordsAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var w in DJVocab.Words)
                Assert.IsTrue(seen.Add(w.term), $"{w.term} is listed twice");
            Assert.GreaterOrEqual(DJVocab.Words.Length, VocabPanel.QuizLength + 3, "the quiz needs enough words for its questions and wrong answers");
        }

        // TextMesh has no word wrap: each list entry gets two lines, and each quiz clue five.
        [Test]
        public void WordsFitThePanel()
        {
            foreach (var w in DJVocab.Words)
            {
                Assert.LessOrEqual(w.term.Length, 14, w.term);
                string entry = TextUtil.Wrap($"{w.term}  {w.meaning}", 68);
                Assert.LessOrEqual(entry.Split('\n').Length, 2, entry);
                string clue = TextUtil.Wrap(DJVocab.Clue(w), 40);
                Assert.LessOrEqual(clue.Split('\n').Length, 4, clue);
            }
        }

        [Test]
        public void ClueHidesTheWord()
        {
            foreach (var w in DJVocab.Words)
            {
                string visible = System.Text.RegularExpressions.Regex.Replace(DJVocab.Clue(w), "<[^>]+>", "");
                StringAssert.DoesNotContain(w.term.ToLowerInvariant(), visible.ToLowerInvariant().Replace(w.term.ToLowerInvariant() + "s", "#"), w.term);
            }
            StringAssert.Contains("_____", DJVocab.Clue(System.Array.Find(DJVocab.Words, x => x.term == "BAR")));
        }
    }
}
