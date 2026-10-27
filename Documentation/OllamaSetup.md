# Ollama Setup and Troubleshooting

The Tiny Town uses a locally installed [Ollama](https://ollama.com) service for residents' free-time decisions, their spoken lines and their end-of-day reflections. Ollama and the model are not bundled with the game. Without them the game still runs: residents follow their built-in routine.

## Setup (Windows)

1. Install Ollama from https://ollama.com/download and start it. It runs in the system tray and listens on `http://localhost:11434`.
2. Open PowerShell and download the model once:

   ```powershell
   ollama pull qwen2.5:7b
   ```

3. Check it is installed:

   ```powershell
   ollama list
   ```

   The list should include `qwen2.5:7b`.
4. Start the game. The AI line at the bottom of the panel shows "connecting", then "loading model", then "connected to qwen2.5:7b".

## Settings

`TheTinyTown_Data/StreamingAssets/Configuration/settings.json` in the game folder:

| Setting | Default | Meaning |
| --- | --- | --- |
| `enabled` | `true` | `false` never contacts Ollama; residents use their built-in routine. |
| `endpoint` | `http://localhost:11434` | Where Ollama listens. Another PC on your network works too, e.g. `http://192.168.1.20:11434`. |
| `model` | `qwen2.5:7b` | Any installed Ollama chat model. |
| `timeoutSeconds` | `60` | Longest wait for one answer (1 to 600). |
| `maxRetries` | `1` | Extra attempts after a bad answer, timeout or connection error (0 to 3). |
| `decisionWaitMinutes` | `20` | Game minutes a resident waits for the AI before deciding by routine (1 to 240). At 1x speed that is 20 real seconds. |

Invalid values are reported in `Player.log`, and the game uses the defaults instead.

## How the AI is used

- The game decides everything that has rules: needs, sleep, work hours, walking, map changes and saving. When a resident has free time, the game lists the activities that are currently valid and the AI picks one, with a short reason and something to say. The game checks the answer against the current situation and ignores anything else.
- One request is sent at a time. Decisions come before reflections. A bad answer is retried at most `maxRetries` times; after that, or if the answer takes too long, the resident uses the routine.
- After three connection errors in a row, the game treats Ollama as offline and checks again every 30 seconds.
- At bedtime each resident reflects on their day once. The AI suggests the memory and how important it was; the game stores it, capped at importance 6.

## Troubleshooting

| AI line says | What to do |
| --- | --- |
| `offline (... Cannot connect ...)` | Ollama isn't running. Start it from the Start menu, or run `ollama serve` in PowerShell. |
| `model missing (... run 'ollama pull ...')` | Run the `ollama pull` command it shows. |
| `offline (qwen2.5:7b took longer than 60s to load)` | The model is loading slowly, usually on first use or with little free memory. Wait for the next automatic check, or raise `timeoutSeconds`. |
| Many "no AI answer in time" lines in the log | Answers are slower than the resident's wait. Play at 1x or 2x, or raise `decisionWaitMinutes`. |
| `off (settings.json)` | Set `"enabled": true` in `settings.json`. |

`Player.log` is in `%USERPROFILE%\AppData\LocalLow\The Tiny Town\The Tiny Town\`. Lines starting `[TinyTown] AI:` record each connection change, decision and fallback.
