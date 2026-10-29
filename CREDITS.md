# Credits

Tiny City's own code, pictures, music and sounds are all made by the game's code in this repository and are covered by the [MIT licence](LICENSE). Everything below belongs to other people. It is used under its own terms, and none of it is covered by this repository's licence.

## Tools the game is built with or runs alongside

- **[Unity](https://unity.com/)** 6 by Unity Technologies: the game engine. It is used under the Unity Terms of Service with a free Personal licence, and the Unity runtime inside a Windows build stays under Unity's own terms.
- **[Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest)** by Unity Technologies, which bundles **[NUnit](https://nunit.org/)**: runs the Edit Mode and Play Mode tests.
- **[Ollama](https://ollama.com/)** (MIT licence): the local AI service the game talks to. It is not included with the game. Players install it themselves.
- **[Qwen2.5](https://github.com/QwenLM/Qwen2.5)** by the Qwen team at Alibaba Cloud: the default AI model (`qwen2.5:7b`). It is not included with the game. Players download it through Ollama under the model's own licence.

## Building and testing

- **[GameCI](https://game.ci/)** (`game-ci/unity-builder` and `game-ci/unity-test-runner`, MIT licence): builds the Windows game and runs the Unity tests in GitHub Actions.
- **[GitHub Actions](https://github.com/actions)** (`actions/checkout`, `actions/cache`, `actions/setup-dotnet`, `actions/upload-artifact`, MIT licence).
- **[.NET](https://dotnet.microsoft.com/)**, **[NUnit](https://nunit.org/)**, **NUnit3TestAdapter** and **Microsoft.NET.Test.Sdk** (MIT licence): run the simulation tests outside Unity (`DotNetTests/`).

## Published methods the code uses

- **FNV-1a hash**, by Glenn Fowler, Landon Curt Noll and Kiem-Phong Vo (public domain): the game's deterministic dice (`Chance`), noise for the pictures (`Noise`) and the sound generator (`Synth`).
- The **mixing step from MurmurHash3's 64-bit finaliser**, by Austin Appleby (public domain): spreads the bits of `Chance`'s hash.

## Influences

- The interface colours (`UiPalette`) are copied from the owner's own app **Stills & Reels** (`src/Theme.cs`), the same palette as the owner's **StackSpace**.
- The code was written with **[Claude Code](https://claude.com/claude-code)** by Anthropic, working for the owner.

If you spot something that should be credited here and isn't, it's an oversight. Please let the owner know.
