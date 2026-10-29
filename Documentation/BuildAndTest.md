# Building and Testing Tiny City

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
| Windows build | Yes | `game-ci/unity-builder` calling `TinyCity.Editor.BuildScript.BuildWindows`, uploaded as `TinyCity-Windows-0.0.<run>` |

The workflow runs on pushes to `main`, on pull requests, and manually from the **Actions** tab (**Run workflow**).

## Getting the Windows build

1. Open the repository's **Actions** tab and select the latest successful **Tests and Windows build** run.
2. Download the `TinyCity-Windows-...` artifact at the bottom of the run page. It is a zip.
3. Extract the whole zip (keep `TinyCity_Data` next to the .exe) and run `TinyCity.exe`.

Windows may show a SmartScreen warning because the build is unsigned; choose **More info → Run anyway**.

## Settings

The built game reads `TinyCity_Data/StreamingAssets/Configuration/settings.json`. The source copy is `Assets/StreamingAssets/Configuration/settings.json`. Invalid or missing settings fall back to the defaults (`http://localhost:11434`, `qwen2.5:7b`, 60 second timeout) and the problem is written to `Player.log`. All AI settings are described in [OllamaSetup.md](OllamaSetup.md).

## AI in tests

Automated tests never contact a real Ollama. Edit Mode tests use `FakeAiTransport`, a scripted stand-in that can answer well, answer badly, answer late or refuse the connection. The Play Mode test `WithoutOllama_AiReportsOffline_AndTheTownKeepsRunning` relies on the CI runner having no Ollama. A real-model check is manual: see the Phase 2 steps in [PlaytestChecklist.md](PlaytestChecklist.md).

## Running tests in a cloud coding session

Only code under `Assets/Scripts/Simulation` (assembly `TinyCity.Simulation`, `noEngineReferences: true`) and the tests under `Assets/Tests/EditMode` can run without Unity:

```bash
dotnet test DotNetTests                                         # all
dotnet test DotNetTests --filter "FullyQualifiedName~GameSettingsTests.Defaults_AreValid_AndPointAtLocalQwen"   # one test
```

The .NET SDK may need installing first (`sudo apt-get install -y dotnet-sdk-8.0` on Ubuntu). Edit Mode tests must not use `UnityEngine` so that they compile in both runners; tests that need Unity go in `Assets/Tests/PlayMode`, which only CI runs.

## Stress test

`Tools/StressTest` runs the town for a long time without Unity and writes what it measured as Markdown. It covers months of play with autosaves, the game loop at every speed against a slow and unreliable stand-in for Ollama, and an Ollama outage:

```bash
cd Tools/StressTest
dotnet run -c Release -- report.md          # 336 game days, then 3 game days at each speed for each AI profile
dotnet run -c Release -- report.md 56 1     # a shorter run
dotnet run -c Release -- report.md 0 0 2 9  # only the big city: 2 game days per speed, 9 districts (90 residents)
```

The fourth argument is the big city's game days per speed (0 skips it) and the fifth how many copies of the starter town it has, ten residents each (10 by default). The sixth is how many requests are sent at once, with the stand-in Ollama given that many parallel slots (1 by default), and the seventh the settings' `decisionWaitSeconds` (8 by default; 0 gives the old game-minute wait only).

It takes about 15 minutes in the cloud container and exits with 1 if a check fails. The latest results are in `Documentation/StressTest.md`. The numbers come from .NET; Unity's Mono is roughly 2–3 times slower.

## Building locally (optional)

With Unity 6000.3.27f1 installed, open the project and use **Tiny City → Build Windows**, or run:

```
Unity.exe -batchmode -quit -projectPath . -executeMethod TinyCity.Editor.BuildScript.BuildWindows -customBuildPath Builds/Windows/TinyCity.exe
```
