using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    public class BoardColorsTests
    {
        [TearDown]
        public void TurnColorBlindModeOff() => BoardColors.SetColorSafe(false, save: false);

        [Test]
        public void Display_Normally_LeavesTextAlone()
        {
            string text = $"Press {BoardColors.Green("PLAY")} on {BoardColors.Blue("Deck A")}.";
            Assert.AreEqual(text, BoardColors.Display(text));
        }

        [Test]
        public void Display_ColorBlind_SwapsColorAndAddsShape()
        {
            BoardColors.SetColorSafe(true, save: false);
            string shown = BoardColors.Display($"Press {BoardColors.Green("PLAY")} on {BoardColors.Blue("Deck A")}.");

            string play = ColorUtility.ToHtmlStringRGB(BoardColors.Of(BoardColors.Family.Play));
            string deckA = ColorUtility.ToHtmlStringRGB(BoardColors.Of(BoardColors.Family.DeckA));
            Assert.AreEqual($"Press <color=#{play}>PLAY ●</color> on <color=#{deckA}>Deck A ◄</color>.", shown);
        }

        [Test]
        public void Display_ColorBlind_EverySixColorsGetsItsOwnShape()
        {
            BoardColors.SetColorSafe(true, save: false);
            string shown = BoardColors.Display(BoardColors.Blue("a") + BoardColors.Pink("b") + BoardColors.Green("c")
                + BoardColors.Orange("d") + BoardColors.Yellow("e") + BoardColors.Red("f"));
            foreach (var shape in new[] { "a ◄", "b ►", "c ●", "d ■", "e ♦", "f ♥" })
                StringAssert.Contains(shape, shown);
        }

        [Test]
        public void Display_ColorBlind_LeavesOtherColorsAlone()
        {
            BoardColors.SetColorSafe(true, save: false);
            string text = "<color=#8890A0>empty</color>";
            Assert.AreEqual(text, BoardColors.Display(text));
        }

        [Test]
        public void ColorBlindPalette_KeepsAllSixColorsApart()
        {
            BoardColors.SetColorSafe(true, save: false);
            for (int a = 1; a <= 6; a++)
            for (int b = a + 1; b <= 6; b++)
            {
                Color ca = BoardColors.Of((BoardColors.Family)a), cb = BoardColors.Of((BoardColors.Family)b);
                float d = Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b);
                Assert.Greater(d, 0.15f, $"{(BoardColors.Family)a} and {(BoardColors.Family)b} are too close");
            }
        }

        [Test]
        public void FamilyOf_DimmedRingColor_FindsFamilyAndBrightness()
        {
            var family = BoardColors.FamilyOf(BoardColors.Cue * 0.7f, out float brightness);
            Assert.AreEqual(BoardColors.Family.Cue, family);
            Assert.AreEqual(0.7f, brightness, 0.01f);
        }

        [Test]
        public void FamilyOf_GreyOrBlack_IsNone()
        {
            Assert.AreEqual(BoardColors.Family.None, BoardColors.FamilyOf(new Color(0.75f, 0.78f, 0.85f), out _));
            Assert.AreEqual(BoardColors.Family.None, BoardColors.FamilyOf(new Color(0.03f, 0.03f, 0.035f), out _));
        }

        [Test]
        public void FamilyOf_ControlIds_MatchTheColorCode()
        {
            Assert.AreEqual(BoardColors.Family.Play, BoardColors.FamilyOf("A.play"));
            Assert.AreEqual(BoardColors.Family.Cue, BoardColors.FamilyOf("B.hotcue2"));
            Assert.AreEqual(BoardColors.Family.Fx, BoardColors.FamilyOf("A.filter"));
            Assert.AreEqual(BoardColors.Family.Crowd, BoardColors.FamilyOf("airhorn"));
            Assert.AreEqual(BoardColors.Family.DeckB, BoardColors.FamilyOf("B.low"));
            Assert.AreEqual(BoardColors.Family.None, BoardColors.FamilyOf("panel.action0"));
            Assert.AreEqual("GREEN", BoardColors.NameOf("A.play"));
        }
    }
}
