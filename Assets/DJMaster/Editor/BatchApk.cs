using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DJMaster.EditorTools
{
    // Command-line Competition APK build. Keystore passwords come from environment variables
    // (DJ_KEYSTORE_PASS / DJ_KEYALIAS_PASS) so they never land in a file or the repo.
    public static class BatchApk
    {
        public static void Build()
        {
            int exit = 1;
            try
            {
                string ks = Environment.GetEnvironmentVariable("DJ_KEYSTORE");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("DJ_KEYALIAS");
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("DJ_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("DJ_KEYALIAS_PASS");

                SceneBuilder.Build();
                DJMasterSubmission.ApplyAppIcon();
                var problems = new List<string>(); var warnings = new List<string>();
                DJMasterSubmission.RunAll(problems, warnings);
                foreach (var w in warnings) Debug.Log("[APK] warning: " + w);
                if (problems.Count > 0) { foreach (var p in problems) Debug.Log("[APK] PROBLEM: " + p); return; }

                int code = PlayerSettings.Android.bundleVersionCode + 1;
                PlayerSettings.Android.bundleVersionCode = code;
                Directory.CreateDirectory("Builds");
                string path = $"Builds/DJMaster-{PlayerSettings.bundleVersion}-{code}.apk";
                EditorUserBuildSettings.buildAppBundle = false;
                var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/DJMaster.unity" },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                });
                Debug.Log($"[APK] {r.summary.result} {path} {r.summary.totalSize / (1024f * 1024f):0.0} MB version code {code} errors={r.summary.totalErrors}");
                if (r.summary.result == BuildResult.Succeeded) exit = 0;
            }
            catch (Exception e) { Debug.Log("[APK] EXCEPTION " + e); }
            finally
            {
                PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
                EditorApplication.Exit(exit);
            }
        }
    }
}
