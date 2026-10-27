using System.Collections.Generic;
using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Entry point created automatically when the game starts, so the boot scene needs no
    /// hand-placed objects. Phase 0 only shows that the build runs and which settings it loaded.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public const string ObjectName = "TinyTownBootstrap";

        private static readonly Color BackgroundColour = new Color(0.98f, 0.91f, 0.78f);

        public GameSettings Settings { get; private set; }
        public IReadOnlyList<string> SettingsProblems { get; private set; } = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStartup()
        {
            if (FindAnyObjectByType<Bootstrap>() != null)
            {
                return;
            }

            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<Bootstrap>();
        }

        private void Awake()
        {
            EnsureCamera();
            Settings = SettingsLoader.Load(out var problems);
            SettingsProblems = problems;
            Debug.Log($"[TinyTown] Started version {Application.version}. Ollama: {Settings.ollama.endpoint} ({Settings.ollama.model}).");
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            DontDestroyOnLoad(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColour;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void OnGUI()
        {
            var title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            var body = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            title.normal.textColor = body.normal.textColor = new Color(0.30f, 0.20f, 0.12f);

            GUILayout.BeginArea(new Rect(40, 40, Screen.width - 80, Screen.height - 80));
            GUILayout.Label("The Tiny Town", title);
            GUILayout.Label($"Phase 0 foundation build, version {Application.version}", body);
            GUILayout.Space(12);
            GUILayout.Label($"Ollama endpoint: {Settings?.ollama.endpoint}", body);
            GUILayout.Label($"Ollama model: {Settings?.ollama.model}", body);
            foreach (var problem in SettingsProblems)
            {
                GUILayout.Label($"Settings warning: {problem}", body);
            }
            GUILayout.EndArea();
        }
    }
}
