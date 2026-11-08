using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DJMaster.EditorTools
{
    // On a Retina Mac, the Game view's "Low Resolution Aspect Ratios" option renders the game at half resolution
    // and stretches it, which makes text blurry. Turn it off whenever Play starts.
    [InitializeOnLoad]
    static class GameViewSharpness
    {
        static GameViewSharpness() => EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredPlayMode) FullResolution();
        };

        [MenuItem("DJ Master/Sharpen Game View")]
        static void FullResolution()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var lowRes = type?.GetProperty("lowResolutionForAspectRatios", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (lowRes == null) return;
            foreach (var view in Resources.FindObjectsOfTypeAll(type))
            {
                if (lowRes.GetValue(view) is true)
                {
                    lowRes.SetValue(view, false);
                    Debug.Log("[DJMaster] Turned off the Game view's Low Resolution Aspect Ratios so the game renders at full sharpness.");
                }
            }
        }
    }
}
