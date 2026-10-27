# Building and Testing The Tiny Town

The game is built and tested in the cloud by GitHub Actions (`.github/workflows/build.yml`), so no Unity editor is needed on your PC to get a playable Windows build.

## Unity version

Unity **6000.3.27f1** (Unity 6.3 LTS), set in `ProjectSettings/ProjectVersion.txt`. GameCI reads this file to pick the matching `unityci/editor` Docker image, so change it only to a version that has a `ubuntu-<version>-windows-mono` image.

## One-time setup: Unity licence secrets

Unity requires a licence even for cloud builds. With a free Personal licence:

1. Install Unity Hub and sign in.
2. In Hub, open **Preferences → Licenses → Add → Get a free personal license**. Do this even if a licence is already listed, so that a fresh licence file is written.
3. Open `C:\ProgramData\Unity\Unity_lic.ulf` (ProgramData is hidden by default).
4. In the GitHub repository, open **Settings → Secrets and variables → Actions** and add:
   - `UNITY_LICENSE`: the full contents of `Unity_lic.ulf`
   - `UNITY_EMAIL`: the Unity account email
   - `UNITY_PASSWORD`: the Unity account password

Without these secrets, the workflow still runs the .NET simulation tests but skips the Unity tests and the Windows build, with a warning.

Reference: https://game.ci/docs/github/activation/

## What CI runs

| Job | Needs licence | What it does |
|---|---|---|
| Simulation tests (.NET) | No | `dotnet test DotNetTests` |
| Unity Edit Mode and Play Mode tests | Yes | `game-ci/unity-test-runner`, results uploaded as `unity-test-results` |
| Windows build | Yes | `game-ci/unity-builder` calling `TinyTown.Editor.BuildScript.BuildWindows`, uploaded as `TheTinyTown-Windows-0.0.<run>` |

The workflow runs on pushes to `main`, on pull requests, and manually from the **Actions** tab (**Run workflow**).

## Getting the Windows build

1. Open the repository's **Actions** tab and select the latest successful **Tests and Windows build** run.
2. Download the `TheTinyTown-Windows-...` artifact at the bottom of the run page. It is a zip.
3. Extract the whole zip (keep `TheTinyTown_Data` next to the .exe) and run `TheTinyTown.exe`.

Windows may show a SmartScreen warning because the build is unsigned; choose **More info → Run anyway**.

## Settings

The built game reads `TheTinyTown_Data/StreamingAssets/Configuration/settings.json`. The source copy is `Assets/StreamingAssets/Configuration/settings.json`. Invalid or missing settings fall back to the defaults (`http://localhost:11434`, `qwen2.5:7b`, 60 second timeout) and the problem is written to `Player.log`. All AI settings are described in [OllamaSetup.md](OllamaSetup.md).

## AI in tests

Automated tests never contact a real Ollama. Edit Mode tests use `FakeAiTransport`, a scripted stand-in that can answer well, answer badly, answer late or refuse the connection. The Play Mode test `WithoutOllama_AiReportsOffline_AndTheTownKeepsRunning` relies on the CI runner having no Ollama. A real-model check is manual: see the Phase 2 steps in [PlaytestChecklist.md](PlaytestChecklist.md).

## Running tests in a cloud coding session

Only code under `Assets/Scripts/Simulation` (assembly `TinyTown.Simulation`, `noEngineReferences: true`) and the tests under `Assets/Tests/EditMode` can run without Unity:

```bash
dotnet test DotNetTests                                         # all
dotnet test DotNetTests --filter "FullyQualifiedName~GameSettingsTests.Defaults_AreValid_AndPointAtLocalQwen"   # one test
```

The .NET SDK may need installing first (`sudo apt-get install -y dotnet-sdk-8.0` on Ubuntu). Edit Mode tests must not use `UnityEngine` so that they compile in both runners; tests that need Unity go in `Assets/Tests/PlayMode`, which only CI runs.

## Building locally (optional)

With Unity 6000.3.27f1 installed, open the project and use **Tiny Town → Build Windows**, or run:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod TinyTown.Editor.BuildScript.BuildWindows -customBuildPath Builds/Windows/TheTinyTown.exe
```
