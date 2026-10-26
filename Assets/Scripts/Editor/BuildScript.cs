using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyTown.Editor
{
    /// <summary>
    /// Command-line build entry point used by CI:
    /// Unity -batchmode -quit -projectPath . -executeMethod TinyTown.Editor.BuildScript.BuildWindows -customBuildPath Builds/Windows/TheTinyTown.exe
    /// </summary>
    public static class BuildScript
    {
        public const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string DefaultBuildPath = "Builds/Windows/TheTinyTown.exe";

        [MenuItem("Tiny Town/Build Windows")]
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
            Debug.Log($"[TinyTown] Build {summary.result}: {summary.outputPath}, {summary.totalSize} bytes, {summary.totalErrors} errors.");
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Windows build failed with result {summary.result}.");
            }
        }

        /// <summary>
        /// The boot scene is intentionally empty: <c>TinyTown.Runtime.Bootstrap</c> creates everything at startup.
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
            PlayerSettings.companyName = "The Tiny Town";
            PlayerSettings.productName = "The Tiny Town";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

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
