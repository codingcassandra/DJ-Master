using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DJMaster.Tests
{
    // #43: every button label stays readable (WCAG 4.5:1) against its cap, lit and unlit, pressed, in both color modes
    public class LabelContrastTests
    {
        [Test]
        public void BlackOnLightCaps_WhiteOnDarkCaps()
        {
            Assert.AreEqual(PokeButton.DarkText, PokeButton.LabelColorFor(BoardColors.Fx), "black on yellow");
            Assert.AreEqual(PokeButton.DarkText, PokeButton.LabelColorFor(new Color32(0xF0, 0xE4, 0x42, 0xFF)), "black on the color-blind yellow");
            Assert.AreEqual(PokeButton.DarkText, PokeButton.LabelColorFor(BoardColors.Play), "black on light green");
            Assert.AreEqual(PokeButton.DarkText, PokeButton.LabelColorFor(BoardColors.DeckA), "black on light blue");
            Assert.AreEqual(PokeButton.LightText, PokeButton.LabelColorFor(new Color(0.12f, 0.12f, 0.13f)), "white on a dark pad");
            Assert.AreEqual(PokeButton.LightText, PokeButton.LabelColorFor(new Color(0.45f, 0.2f, 0.75f)), "white on purple");
        }

        [Test]
        public void EveryButtonInTheSceneIsReadable()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/DJMaster.unity", OpenSceneMode.Additive);
            try
            {
                int checkedButtons = 0;
                var problems = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                foreach (var b in root.GetComponentsInChildren<PokeButton>(true))
                {
                    if (b.capRenderer == null) continue;
                    // Every text on the button sits on its cap, so PokeButton recolors all of it
                    var all = b.GetComponentsInChildren<TMP_Text>(true);
                    Assert.AreEqual(all.Length, b.Labels.Length, $"{b.name}: a label that isn't on the cap won't get recolored");
                    if (all.Length == 0) continue;
                    checkedButtons++;

                    // What PokeButton shows: off or lit, and either one flashing toward a bright version of the lit color
                    var looks = new List<(string, Color)>();
                    void Add(string mode, Color on, Color off)
                    {
                        var bright = Color.Lerp(on, Color.white, 0.55f);
                        looks.Add((mode + " off", off));
                        looks.Add((mode + " lit", on));
                        looks.Add((mode + " off pressed", Color.Lerp(off, bright, 0.8f)));
                        looks.Add((mode + " lit pressed", Color.Lerp(on, bright, 0.8f)));
                    }
                    Add("normal", b.onColor, b.offColor);
                    if (ColorSafeView.SafeButtonColors(b.onColor, b.offTint, out var safeOn, out var safeOff)) Add("color-blind", safeOn, safeOff);
                    foreach (var (state, c) in looks)
                    {
                        float ratio = PokeButton.Contrast(PokeButton.LabelColorFor(c), PokeButton.Seen(c));
                        if (ratio < 4.5f) problems.Add($"{b.id ?? b.name} {state}: {ratio:0.0}:1");
                    }
                }
                Assert.Greater(checkedButtons, 40, "the board, panels, menu and vocab buttons");
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
