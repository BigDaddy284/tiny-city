# Ollama Setup and Troubleshooting

Tiny City uses a locally installed [Ollama](https://ollama.com) service for residents' free-time decisions, their conversation lines and their end-of-day reflections. Ollama and the model are not bundled with the game. Without them the game still runs: residents follow their built-in routine.

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

## Turning the AI on or off and choosing a model in the game

Click **Settings** at the top right of the side panel. Under *Local AI (Ollama)*:

- **Use local AI: On / Off** switches the AI at once. While it is off the game never contacts Ollama and residents use their built-in routine.
- **Installed models** lists every model Ollama has (the game asks Ollama when the window opens; press **Refresh List** after installing one). Click a model to use it; the game checks and loads it first, and residents use their routine while it loads.
- If the list can't be read, the window says why. Usually Ollama isn't running: start it and press **Refresh List**.

These choices are kept in `player-settings.json` in the game's data folder (`%USERPROFILE%\AppData\LocalLow\<company>\Tiny City\`) and take the place of `enabled` and `model` below. Delete that file to go back to `settings.json`.

## Settings file

`TinyCity_Data/StreamingAssets/Configuration/settings.json` in the game folder:

| Setting | Default | Meaning |
| --- | --- | --- |
| `enabled` | `true` | `false` never contacts Ollama; residents use their built-in routine. |
| `endpoint` | `http://localhost:11434` | Where Ollama listens. Another PC on your network works too, e.g. `http://192.168.1.20:11434`. |
| `model` | `qwen2.5:7b` | Any installed Ollama chat model. |
| `timeoutSeconds` | `60` | Longest wait for one answer (1 to 600). |
| `maxRetries` | `1` | Extra attempts after a bad answer, timeout or connection error (0 to 3). |
| `decisionWaitMinutes` | `20` | Game minutes a resident waits for the AI before deciding by routine (1 to 240). At 1x speed that is 20 real seconds. |
| `decisionWaitSeconds` | `8` | Real seconds a resident waits at the least, at any speed (0 to 120; 0 turns it off). At 4x that is 32 game minutes, and at 10x it's 80, so answers still arrive in time at high speed. |
| `parallelRequests` | `1` | How many requests the game sends to Ollama at once (1 to 8). Raise it only after setting up Ollama to answer that many together; see below. |

The same file has a `simulation` section for optional parts of the game, which work with or without Ollama:

| Setting | Default | Meaning |
| --- | --- | --- |
| `romance` | `true` | `false` stops new romances and changes in feelings. Couples already together stay together. |
| `communityEvents` | `true` | `false` cancels the get-togethers, quiz nights and the village fête. |
| `babies` | `true` | `false` stops couples having babies (never while 50 people live in the city). |
| `newcomers` | `true` | `false` stops people moving to the city from out of town (at most 10 ever, never past 50 residents). |

Invalid values are reported in `Player.log`, and the game uses the defaults instead.

## How the AI is used

- The game decides everything that has rules: needs, sleep, work hours, walking, map changes and saving. When a resident has free time, the game lists the activities that are currently valid and the AI picks one, with a short reason and something to say. The game checks the answer against the current situation and ignores anything else.
- In a conversation the game decides who speaks, how many lines there are and when it ends. For each line the AI writes the words, a tone (warm, playful, neutral or disagree), the topic and whether the speaker wants to wrap up. The tone changes the relationship by fixed game rules. The conversation pauses while it waits for a line. If the line doesn't arrive within 6 real seconds (at any game speed), or the answer is unusable, the resident uses built-in small talk for it.
- One request is sent at a time, or up to `parallelRequests` at once. Decisions come first, then conversation lines, then reflections. A bad answer is retried at most `maxRetries` times; after that, or if the answer takes too long, the resident uses the routine.
- After three connection errors in a row, the game treats Ollama as offline and checks again every 30 seconds.
- At bedtime each resident reflects on their day once. The AI suggests the memory and how important it was; the game stores it, capped at importance 6.

## Answering several requests at once (bigger towns, high speeds)

With many residents or at 4x and 10x, one model answering one request at a time can't keep up. Ollama can answer several at once, which works well with a small, fast model on a graphics card:

1. Open the Start menu, type **environment**, and choose **Edit environment variables for your account**.
2. Under **User variables**, click **New**. Enter the name `OLLAMA_NUM_PARALLEL` and the value `2` (or `4`). Click **OK** twice.
3. Quit Ollama from its icon in the taskbar's notification area, then start it again from the Start menu.
4. In the game's `settings.json`, set `"parallelRequests"` to the same number, then start the game.

Each answer gets a little slower when several are worked on together, but more answers arrive overall. Each extra request also uses more graphics memory. If answers start timing out, go back to 2 or 1.

## Troubleshooting

| AI line says | What to do |
| --- | --- |
| `offline (... Cannot connect ...)` | Ollama isn't running. Start it from the Start menu, or run `ollama serve` in PowerShell. |
| `model missing (... run 'ollama pull ...')` | Run the `ollama pull` command it shows. |
| `offline (qwen2.5:7b took longer than 60s to load)` | The model is loading slowly, usually on first use or with little free memory. Wait for the next automatic check, or raise `timeoutSeconds`. |
| Many "no AI answer in time" lines in the log | Answers are slower than the resident's wait. Play at 1x or 2x, raise `decisionWaitSeconds`, or let Ollama answer several at once (see above). |
| `off (Settings)` | Turn **Use local AI** on in the Settings window (top right). |

`Player.log` is in `%USERPROFILE%\AppData\LocalLow\Tiny City\Tiny City\`. Lines starting `[TinyCity] AI:` record each connection change, decision and fallback.
