using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DJMaster.Tests
{
    // Meta's "airplane seat test": a seated player should reach everything without leaning, so every control has to be
    // inside 2 ft (0.61 m) of the eyes. And pads have to stay finger sized, because hand tracking wobbles a few mm.
    // Measured on the built scene, from the eye point SeatedAnchor places the board for.
    public class ReachTests
    {
        const float MaxReach = 0.61f;
        const float MinPad = 0.02f;

        Vector3 eye;
        SystemButtons tab;

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/DJMaster.unity");
            var anchor = Object.FindAnyObjectByType<SeatedAnchor>();
            eye = anchor.transform.TransformPoint(new Vector3(0, anchor.boardHeight + anchor.below, anchor.boardForward - anchor.forward));
            tab = Object.FindAnyObjectByType<SystemButtons>(FindObjectsInactive.Include);
        }

        static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        // Every control in the scene, plus the HOME / COLOR-BLIND tab on each panel it can be mounted on
        IEnumerable<(string name, DJControl control)> AllControls()
        {
            foreach (var c in Object.FindObjectsByType<DJControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!c.transform.IsChildOf(tab.transform)) yield return ($"{c.transform.parent.name}/{c.name}", c);
            foreach (var corner in new[] { Find<InstructionPanel>().corner, Find<SetBuilderPanel>().corner, Find<StartMenu>().corner, Find<VocabPanel>().corner })
            {
                tab.MountOn(corner, true);
                yield return ($"tab on {corner.parent.name}/HOME", tab.home);
                yield return ($"tab on {corner.parent.name}/COLOR-BLIND", tab.colorBlind);
            }
        }

        [Test]
        public void EveryControlIsInsideTwoFeet()
        {
            var tooFar = new List<string>();
            int count = 0;
            foreach (var (name, c) in AllControls())
            {
                count++;
                float d = Vector3.Distance(c.transform.position, eye);
                if (d > MaxReach) tooFar.Add($"{name} {d:0.000} m");
            }
            Assert.Greater(count, 90, "the scene should have all its controls");
            Assert.IsEmpty(tooFar, "out of reach: " + string.Join(", ", tooFar));
        }

        [Test]
        public void EveryButtonIsFingerSized()
        {
            var tooSmall = new List<string>();
            foreach (var (name, c) in AllControls())
            {
                if (c is not PokeButton b) continue;
                Vector3 s = b.transform.lossyScale;
                float w = b.size.x * s.x, h = b.size.y * s.z;
                if (Mathf.Min(w, h) < MinPad - 1e-4f) tooSmall.Add($"{name} {w * 100:0.0}x{h * 100:0.0} cm");
            }
            Assert.IsEmpty(tooSmall, "smaller than 2 cm: " + string.Join(", ", tooSmall));
        }

        [Test]
        public void BoardIsDrawnAtSurfaceScale()
        {
            var surface = Find<DJBoard>().transform.Find("Surface");
            Assert.IsNotNull(surface);
            Assert.AreEqual(DJBoard.SurfaceScale, surface.localScale.x, 1e-4f);
            Assert.AreEqual(1f, Find<InstructionPanel>().transform.lossyScale.x, 1e-4f, "panels keep their full size text");
        }
    }
}
