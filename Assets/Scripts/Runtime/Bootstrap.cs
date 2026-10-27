using System.Collections.Generic;
using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Entry point created automatically when the game starts, so the boot scene needs no hand-placed objects.
    /// Sets up the camera, loads settings and starts the <see cref="GameController"/>.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public const string ObjectName = "TinyTownBootstrap";

        private static readonly Color BackgroundColour = new Color(0.98f, 0.91f, 0.78f);

        public GameSettings Settings { get; private set; }

        public IReadOnlyList<string> SettingsProblems { get; private set; } = new List<string>();

        public GameController Game { get; private set; }

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
            Game = gameObject.AddComponent<GameController>();
            Game.Settings = Settings;
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
    }
}
