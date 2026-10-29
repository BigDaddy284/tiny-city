using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyCity.Editor
{
    /// <summary>
    /// Command-line build entry point used by CI:
    /// Unity -batchmode -quit -projectPath . -executeMethod TinyCity.Editor.BuildScript.BuildWindows -customBuildPath Builds/Windows/TinyCity.exe
    /// </summary>
    public static class BuildScript
    {
        public const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string DefaultBuildPath = "Builds/Windows/TinyCity.exe";

        [MenuItem("Tiny City/Build Windows")]
        public static void BuildWindows()
        {
            EnsureBootScene();
            ApplyPlayerSettings();

            var outputPath = GetArgument("-customBuildPath") ?? DefaultBuildPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[TinyCity] Build {summary.result}: {summary.outputPath}, {summary.totalSize} bytes, {summary.totalErrors} errors.");
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Windows build failed with result {summary.result}.");
            }
        }

        /// <summary>
        /// The boot scene is intentionally empty: <c>TinyCity.Runtime.Bootstrap</c> creates everything at startup.
        /// Creating it here means the scene file is always produced by the editor rather than written by hand.
        /// </summary>
        public static void EnsureBootScene()
        {
            if (!File.Exists(BootScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, BootScenePath);
                AssetDatabase.Refresh();
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Tiny City";
            PlayerSettings.productName = "Tiny City";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            // Ollama is reached over plain http on the player's own machine (http://localhost:11434 by default).
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            var version = GetArgument("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none")
            {
                PlayerSettings.bundleVersion = version;
            }
        }

        private static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
