using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DJMaster.EditorTools
{
    // Stuff we need before sending DJ Master to the Meta competition: the checks, the APK build,
    // the app icon and a way to wipe saved progress
    // To run the checks from the terminal use -executeMethod DJMaster.EditorTools.DJMasterSubmission.RunFromCommandLine
    public static class DJMasterSubmission
    {
        const string AppId = "com.djmaster.vr";
        const int MinApi = 32; // Quest needs Android 12L or newer
        const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        const string ProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";
        const string IconPath = "Assets/DJMaster/Art/AppIcon.png";
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        static readonly string[] TextFiles = { ".cs", ".json", ".txt", ".md" };

        // Checks

        [MenuItem("DJ Master/Run Submission Checks")]
        static void RunFromMenu()
        {
            var problems = new List<string>();
            var warnings = new List<string>();
            RunAll(problems, warnings);
            EditorUtility.DisplayDialog("DJ Master", Summary(problems, warnings), "OK");
        }

        public static void RunFromCommandLine()
        {
            var problems = new List<string>();
            var warnings = new List<string>();
            RunAll(problems, warnings);
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }

        public static void RunAll(List<string> problems, List<string> warnings)
        {
            CheckAndroid(problems, warnings);
            CheckScene(problems);
            CheckHandTracking(problems);
            CheckManifest(problems);
            CheckIcon(problems);
            CheckTracks(problems);
            CheckEnglish(problems);

            foreach (var warning in warnings) Debug.LogWarning("[DJMaster] " + warning);
            foreach (var problem in problems) Debug.LogError("[DJMaster] " + problem);
            Debug.Log(problems.Count == 0
                ? $"[DJMaster] PASS: ready for the competition ({warnings.Count} warnings)."
                : $"[DJMaster] FAIL: {problems.Count} problems, {warnings.Count} warnings.");
        }

        static string Summary(List<string> problems, List<string> warnings)
        {
            if (problems.Count == 0 && warnings.Count == 0) return "Everything passed! We're good to build.";
            if (problems.Count == 0) return $"Everything passed! {warnings.Count} warnings to read though. Details are in the Console.";
            return $"{problems.Count} problems and {warnings.Count} warnings. Fix the problems before we build. Details are in the Console.";
        }

        // App id, IL2CPP + ARM64, Android version and signing. Meta rejects the upload if any of these are off
        static void CheckAndroid(List<string> problems, List<string> warnings)
        {
            var android = NamedBuildTarget.Android;

            string id = PlayerSettings.GetApplicationIdentifier(android);
            if (id != AppId) problems.Add($"Android app id is '{id}', should be '{AppId}'. Run DJ Master > Configure Player Settings for Quest.");

            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP)
                problems.Add("Scripting backend isn't IL2CPP. Quest needs IL2CPP for 64-bit.");

            var arch = PlayerSettings.Android.targetArchitectures;
            if ((arch & AndroidArchitecture.ARM64) == 0) problems.Add("ARM64 isn't checked in Target Architectures.");
            else if (arch != AndroidArchitecture.ARM64) warnings.Add($"Target Architectures is {arch}. Quest only needs ARM64, the rest just makes the APK bigger.");

            int minApi = (int)PlayerSettings.Android.minSdkVersion;
            if (minApi < MinApi) problems.Add($"Minimum API level is {minApi}, needs to be {MinApi} or higher.");

            // 0 means "highest installed", that's fine
            int targetApi = (int)PlayerSettings.Android.targetSdkVersion;
            if (targetApi != 0 && targetApi < MinApi) warnings.Add($"Target API level is {targetApi}, set it to {MinApi} or higher (or Automatic).");

            warnings.Add($"Version code is {PlayerSettings.Android.bundleVersionCode}. It has to go up for every upload to the \"Competition\" release channel. Build Competition APK does that for us.");

            // A debug signed APK gets turned down by the Meta dashboard
            if (!PlayerSettings.Android.useCustomKeystore)
                problems.Add("The APK would be signed with the debug key. Set up a custom keystore in Player Settings > Publishing Settings.");
            else if (!File.Exists(PlayerSettings.Android.keystoreName))
                problems.Add($"Keystore file '{PlayerSettings.Android.keystoreName}' isn't there.");

            if (EditorUserBuildSettings.development) problems.Add("Development Build is on. Turn it off in Build Profiles, Meta won't take a debug build.");
        }

        static void CheckScene(List<string> problems)
        {
            var scene = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == SceneBuilder.ScenePath);
            if (scene == null) problems.Add($"{SceneBuilder.ScenePath} isn't in the build scene list.");
            else if (!scene.enabled) problems.Add($"{SceneBuilder.ScenePath} is in the build scene list but unchecked.");
            if (!File.Exists(SceneBuilder.ScenePath)) problems.Add($"{SceneBuilder.ScenePath} doesn't exist. Run DJ Master > Build Scene.");
        }

        // The game is hands only, so Meta needs to know that or it asks for controllers
        static void CheckHandTracking(List<string> problems)
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config == null) config = AssetDatabase.LoadAssetAtPath<OVRProjectConfig>(ProjectConfigPath);
            if (config == null)
            {
                problems.Add($"Couldn't load the Meta project config ({ProjectConfigPath}).");
                return;
            }

            if (config.handTrackingSupport != OVRProjectConfig.HandTrackingSupport.HandsOnly)
                problems.Add($"Hand Tracking Support is {config.handTrackingSupport}, should be HandsOnly.");
            if (config.handTrackingFrequency != OVRProjectConfig.HandTrackingFrequency.HIGH)
                problems.Add($"Hand Tracking Frequency is {config.handTrackingFrequency}, should be HIGH.");
        }

        static void CheckManifest(List<string> problems)
        {
            if (!File.Exists(ManifestPath))
            {
                problems.Add($"{ManifestPath} is missing.");
                return;
            }

            var doc = new XmlDocument();
            try { doc.Load(ManifestPath); }
            catch (XmlException e)
            {
                problems.Add($"{ManifestPath} isn't valid XML: {e.Message}");
                return;
            }

            bool handsRequired = false, handsFound = false, permission = false;
            foreach (XmlElement feature in doc.GetElementsByTagName("uses-feature"))
            {
                if (feature.GetAttribute("name", AndroidNs) != "oculus.software.handtracking") continue;
                handsFound = true;
                handsRequired = feature.GetAttribute("required", AndroidNs) == "true";
            }
            foreach (XmlElement perm in doc.GetElementsByTagName("uses-permission"))
                if (perm.GetAttribute("name", AndroidNs) == "com.oculus.permission.HAND_TRACKING") permission = true;

            if (!handsFound) problems.Add("Manifest doesn't have the oculus.software.handtracking feature.");
            else if (!handsRequired) problems.Add("Manifest has the hand tracking feature but not required=\"true\".");
            if (!permission) problems.Add("Manifest is missing the com.oculus.permission.HAND_TRACKING permission.");
        }

        static void CheckIcon(List<string> problems)
        {
            bool hasIcon = HasIcon(NamedBuildTarget.Android) || HasIcon(NamedBuildTarget.Unknown);
            if (!hasIcon) problems.Add("No app icon. Run DJ Master > Apply App Icon.");
        }

        static bool HasIcon(NamedBuildTarget target)
        {
            foreach (var kind in new[] { IconKind.Application, IconKind.Any })
            {
                var icons = PlayerSettings.GetIcons(target, kind);
                if (icons != null && icons.Any(icon => icon != null)) return true;
            }
            return false;
        }

        // Synthesized songs are made on the fly so they always work, recordings need their file in Resources/Music
        static void CheckTracks(List<string> problems)
        {
            var tracks = TrackLibrary.Tracks;
            if (tracks.Length == 0) problems.Add("TrackLibrary has no songs.");

            for (int i = 0; i < tracks.Length; i++)
            {
                var track = tracks[i];
                if (!track.IsRecording) continue;
                if (Resources.Load<AudioClip>("Music/" + track.clip) == null)
                    problems.Add($"Song {i} '{track.title}' can't play: Resources/Music/{track.clip} is missing.");
            }
        }

        // The judges read English, so no letters from other alphabets or accents in our text.
        // Symbols like the color-blind shapes are fine
        static void CheckEnglish(List<string> problems)
        {
            if (!IsEnglish(PlayerSettings.productName)) problems.Add($"Product name '{PlayerSettings.productName}' has non-English letters.");

            foreach (var path in ProjectTextFiles())
            {
                var lines = File.ReadAllLines(path);
                for (int n = 0; n < lines.Length; n++)
                {
                    if (IsEnglish(lines[n])) continue;
                    problems.Add($"{path}:{n + 1} has non-English letters: {lines[n].Trim()}");
                }
            }
        }

        static bool IsEnglish(string text) => !text.Any(c => c > 127 && char.IsLetter(c));

        // Every .cs/.json/.txt/.md under Assets/DJMaster, plus the README
        static IEnumerable<string> ProjectTextFiles()
        {
            var files = Directory.GetFiles("Assets/DJMaster", "*.*", SearchOption.AllDirectories)
                .Where(path => TextFiles.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path)
                .ToList();
            if (File.Exists("README.md")) files.Add("README.md");
            return files;
        }

        // Build

        [MenuItem("DJ Master/Build Competition APK")]
        static void BuildCompetitionApk()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("DJ Master", "Stop Play first, then build.", "OK");
                return;
            }

            ApplyAppIcon();

            var problems = new List<string>();
            var warnings = new List<string>();
            RunAll(problems, warnings);
            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("DJ Master", $"Not building yet, {problems.Count} problems first. Details are in the Console.", "OK");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            // Meta won't take an upload with the same version code twice, so every build gets a new one
            int oldCode = PlayerSettings.Android.bundleVersionCode;
            int code = oldCode + 1;
            PlayerSettings.Android.bundleVersionCode = code;
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("Builds");
            string path = $"Builds/DJMaster-{PlayerSettings.bundleVersion}-{code}.apk";
            EditorUserBuildSettings.buildAppBundle = false; // the dashboard wants an APK

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                // The build failed so nothing got uploaded, we put the old version code back
                PlayerSettings.Android.bundleVersionCode = oldCode;
                AssetDatabase.SaveAssets();
                Debug.LogError($"[DJMaster] Build {report.summary.result} with {report.summary.totalErrors} errors.");
                EditorUtility.DisplayDialog("DJ Master", "The build didn't finish. Details are in the Console.", "OK");
                return;
            }

            float mb = report.summary.totalSize / (1024f * 1024f);
            Debug.Log($"[DJMaster] Built {path} ({mb:0.0} MB), version code {code}.");
            EditorUtility.RevealInFinder(Path.GetFullPath(path));
            EditorUtility.DisplayDialog("DJ Master",
                $"Built {Path.GetFileName(path)} ({mb:0.0} MB), version code {code}.\n\n" +
                "Next: open the Meta developer dashboard, go to our app's release channels and upload it to the one named exactly \"Competition\".",
                "OK");
        }

        // App icon

        [MenuItem("DJ Master/Apply App Icon")]
        public static void ApplyAppIcon()
        {
            AssetDatabase.ImportAsset(IconPath);
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[DJMaster] Couldn't find the icon at {IconPath}.");
                return;
            }

            // Icons get resized for every size Android wants, so keep the full picture with no compression
            if (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            SetIcon(NamedBuildTarget.Unknown, icon); // the default icon every platform falls back to
            SetIcon(NamedBuildTarget.Android, icon);
            AssetDatabase.SaveAssets();
            Debug.Log("[DJMaster] App icon set for Android and the default.");
        }

        // Unity only takes the list if it has one texture for every size, so we use ours for all of them
        static void SetIcon(NamedBuildTarget target, Texture2D icon)
        {
            foreach (var kind in new[] { IconKind.Application, IconKind.Any })
            {
                int sizes = PlayerSettings.GetIconSizes(target, kind).Length;
                if (sizes == 0) continue;
                PlayerSettings.SetIcons(target, Enumerable.Repeat(icon, sizes).ToArray(), kind);
                return;
            }
            Debug.LogWarning($"[DJMaster] Unity didn't give any icon sizes for {target.TargetName}, skipped it.");
        }

        // Saved progress

        // Every PlayerPrefs key the game saves (the lesson ones come from LessonDirector.DoneKey)
        static IEnumerable<string> SavedKeys()
        {
            yield return "bestSet";       // GameDirector, best set score
            yield return "env";           // EnvironmentManager, room / club / festival
            yield return "boardHeight";   // SeatedAnchor, how high the board sits
            yield return "set";           // SetBuilderPanel, the saved set list
            yield return "colorSafe";     // BoardColors, color-blind mode
            yield return "vocabQuizBest"; // DJVocab, best quiz score
            for (int i = 0; i < LessonDirector.LessonNames.Length; i++) yield return LessonDirector.DoneKey(i);
        }

        [MenuItem("DJ Master/Reset Saved Progress")]
        static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("DJ Master",
                    "Erase the finished lessons, best scores, saved set and settings on this computer?\n(The headset keeps its own, uninstall the app there to wipe it.)",
                    "Erase", "Cancel")) return;

            foreach (var key in SavedKeys()) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log("[DJMaster] Progress erased: " + string.Join(", ", SavedKeys()));
        }
    }
}
