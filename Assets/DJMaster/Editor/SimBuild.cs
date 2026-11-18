using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// macOS player for the Meta XR Simulator (run-sim.sh build). Not the Quest build.
public static class SimBuild
{
    public static void BuildMac()
    {
        DJMaster.EditorTools.SceneBuilder.Build();
        // The simulator window takes focus, so the player must keep running in the background.
        // Both settings are only for this build and are put back afterwards.
        bool oldBg = PlayerSettings.runInBackground;
        string oldMic = PlayerSettings.macOS.microphoneUsageDescription;
        PlayerSettings.runInBackground = true;
        PlayerSettings.macOS.microphoneUsageDescription = "DJ Master listens to your mix.";
        BuildReport r;
        try
        {
            r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/DJMaster.unity" },
                locationPathName = "Builds/SimMac/DJMaster.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            });
        }
        finally
        {
            PlayerSettings.runInBackground = oldBg;
            PlayerSettings.macOS.microphoneUsageDescription = oldMic;
            AssetDatabase.SaveAssets();
        }
        Debug.Log("SIMBUILD " + r.summary.result + " errors=" + r.summary.totalErrors);
        EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
