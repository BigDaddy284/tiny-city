# Tiny City stress test

These results come from `Tools/StressTest` (see `Documentation/BuildAndTest.md`), run in the cloud container on 29 October 2026. Ollama can't run there, so a stand-in answers instead. It takes a set time per reply and works on one request at a time, as Ollama does with one model loaded. Two of its failure rates are assumptions, not measurements: 1 reply in 30 is not JSON, and 1 in 50 never arrives. All the numbers are from .NET; Unity's Mono is roughly 2–3 times slower. Nothing here was measured in the Windows build or against a real model.

## What it found and what changed

| Finding | Before | After |
|---|---|---|
| Saves kept growing for months, mostly old chat transcripts | 12.7 MB after ~6 months, autosave ~200 ms (slowest 570 ms) | Chat history kept to the last 1,500 (about a week): 5.2 MB at most, autosave median 66 ms |
| Heap growth over a long game | Grew with the saves | Flat once the capped records fill (+0.9 MB from day 168 to day 336) |
| Route finding scanned every door in town for every distance; with 100 residents some ticks took over half a second | 145 ticks over 40 ms in two game days of the 100-resident city, slowest 776 ms | 3 such ticks, all when everyone sets off at once; 0.8 ms per tick on average. The .NET test suite also runs in half the time |
| One lost AI reply held up the whole queue for the 60 s timeout | Every request waited up to 60 s | After 5 answers, the queue gives up after 6× the model's average answer time (at least 10 s, at most the setting) |
| Ollama going away | (checked, already fine) | Marked offline within about 1 s; reconnects about 2 s after Ollama returns |

## What it means for play

- **The simulation itself is light.** Ten residents cost about 0.015 ms of CPU per game minute, and the 100-resident city about 0.8 ms. Even at 10x that's well under 1% of one core.
- **How much the AI decides depends on the model's speed and the game speed.** A resident waits up to 20 game minutes for an AI suggestion (`decisionWaitMinutes`). At 10x that's only 2 real seconds.
  - With qwen2.5:7b on a graphics card (~2.7 s a reply), the AI made 87% of free-time choices at 1x but 1% at 10x.
  - With qwen2.5:0.5b (~0.4 s), it made 97% at 1x and 77% at 10x, and wrote nearly every line of chat.
  - On the CPU only (~20 s a reply), it made almost none at any speed. That setup needs a longer `decisionWaitMinutes`, or a smaller model.
- **100 residents with one 0.4 s model:** the AI made 78% of choices at 1x, 55% at 2x, 24% at 4x and 7% at 10x. The queue stayed full at its 32-request limit, and the least important requests were dropped, so those residents used their routine. Nobody waited past the deadline and nothing grew without limit. One model answering one request at a time simply can't keep up with 100 people at high speed. The usual next step would be letting the game send 2–4 requests at once to an Ollama started with `OLLAMA_NUM_PARALLEL`. That needs real-model measurements first.
- **Frames:** the slowest frame was 37 ms at 1x in the ten-resident town (startup) and 14 ms at 10x. In the 100-resident city it was 230–310 ms, when a tick, an autosave of the bigger city and the AI updates landed in the same frame.

## Full results

Run with .NET 8.0.32 on 4 cores (Ubuntu 24.04.5 LTS), 2026-10-29 12:37 UTC.
Unity's Mono runs the same simulation code roughly 2–3 times slower (the soak test takes about 2 minutes under .NET and 5 in Unity).

### 1. 336 game days with the built-in decision maker

The town runs flat out, one tick per game minute, with an autosave every 6 game hours as in the game. Each row is 28 game days (one resident year).

| Days | Alive | ms per game day | Slowest tick (ms) | 99.9% of ticks under (ms) | Heap after GC (MB) | Save size (KB) | Save (ms) | Load (ms) | Memories | Chats | Ledger | History |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 28 | 10 | 43.4 | 71.44 | 1.045 | 11.1 | 2844 | 39.5 | 191.4 | 549 | 1145 | 506 | 117 |
| 56 | 9 | 19.5 | 2.27 | 0.401 | 14.0 | 3960 | 35.0 | 134.2 | 903 | 1500 | 1014 | 138 |
| 84 | 9 | 18.5 | 2.96 | 0.314 | 14.7 | 4192 | 74.5 | 187.2 | 1229 | 1500 | 1499 | 166 |
| 112 | 9 | 18.9 | 5.52 | 0.384 | 15.6 | 4449 | 53.3 | 190.3 | 1570 | 1500 | 1987 | 195 |
| 140 | 9 | 19.3 | 2.56 | 0.441 | 16.3 | 4685 | 62.8 | 186.7 | 1833 | 1500 | 2470 | 216 |
| 168 | 9 | 20.8 | 4.75 | 0.473 | 17.0 | 4916 | 57.3 | 184.9 | 2095 | 1500 | 2951 | 246 |
| 196 | 9 | 21.0 | 7.92 | 0.465 | 17.4 | 5020 | 123.8 | 263.2 | 2295 | 1500 | 3000 | 266 |
| 224 | 9 | 23.0 | 41.29 | 0.523 | 17.6 | 5109 | 57.1 | 295.3 | 2493 | 1500 | 3000 | 299 |
| 252 | 9 | 24.3 | 3.54 | 0.593 | 17.8 | 5159 | 96.6 | 280.3 | 2562 | 1500 | 3000 | 324 |
| 280 | 9 | 21.8 | 2.58 | 0.536 | 17.8 | 5168 | 107.7 | 274.6 | 2601 | 1500 | 3000 | 351 |
| 308 | 9 | 20.8 | 3.61 | 0.552 | 17.9 | 5194 | 66.7 | 274.3 | 2633 | 1500 | 3000 | 377 |
| 336 | 9 | 22.0 | 2.83 | 0.567 | 17.9 | 5198 | 92.2 | 282.4 | 2651 | 1500 | 3000 | 427 |

Whole run: 100.7 s for 336 game days (4806 ticks a second, with saves and checks). Autosaves: median 66.0 ms, slowest 387.6 ms.
The last block took 0.51× as long per day as the first. From day 168, when the capped records are full, to the end the heap changed by +0.9 MB.

### 2. Game loop with qwen2.5:7b on a graphics card

The stand-in Ollama takes 1.5 to 4 s a reply, like Qwen2.5 7B on a mid-range GPU, and works on one request at a time. One reply in 30 is not JSON and one in 50 never arrives (it times out). Frames run at 60 a second as in the game, with ticks, autosaves and AI updates where the game does them. Default settings: decisions wait up to 20 game minutes, requests time out after 60 s, 1 retry. 3 game days at each speed.

| Speed | Real time | Free-time choices | Chosen by AI | AI lines in chats | Avg / longest queue | Avg reply (s) | Timed out | Not JSON | Stale | Dropped | Longest wait (game min) | AI share of each resident's choices (lowest / highest) | Model busy | Slowest frame (ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 1.2 h | 445 | 87% | 65% | 0.5 / 9 | 2.8 | 8 | 35 | 121 | 0 | 20 | 64% / 95% | 40% | 36.8 |
| 2x | 36 min | 379 | 74% | 45% | 0.9 / 9 | 2.7 | 0 | 27 | 162 | 0 | 20 | 43% / 85% | 55% | 7.3 |
| 4x | 18 min | 332 | 38% | 35% | 1.4 / 9 | 2.6 | 1 | 14 | 252 | 0 | 20 | 12% / 50% | 68% | 4.6 |
| 10x | 7 min | 292 | 1% | 39% | 2.6 / 9 | 2.6 | 1 | 3 | 323 | 0 | 20 | 0% / 8% | 85% | 6.2 |

### 3. Game loop with qwen2.5:7b on the CPU only

The stand-in Ollama takes 12 to 30 s a reply, like Qwen2.5 7B without a usable GPU, and works on one request at a time. One reply in 30 is not JSON and one in 50 never arrives (it times out). Frames run at 60 a second as in the game, with ticks, autosaves and AI updates where the game does them. Default settings: decisions wait up to 20 game minutes, requests time out after 60 s, 1 retry. 3 game days at each speed.

| Speed | Real time | Free-time choices | Chosen by AI | AI lines in chats | Avg / longest queue | Avg reply (s) | Timed out | Not JSON | Stale | Dropped | Longest wait (game min) | AI share of each resident's choices (lowest / highest) | Model busy | Slowest frame (ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 1.2 h | 359 | 8% | 0% | 1.8 / 9 | 18.1 | 1 | 3 | 433 | 0 | 20 | 4% / 22% | 77% | 5.7 |
| 2x | 36 min | 346 | 0% | 0% | 2.6 / 9 | 20.4 | 0 | 1 | 449 | 0 | 20 | 0% / 0% | 91% | 9.5 |
| 4x | 18 min | 340 | 0% | 0% | 5.0 / 13 | 20.5 | 0 | 1 | 390 | 0 | 20 | 0% / 0% | 98% | 5.8 |
| 10x | 7 min | 323 | 0% | 0% | 10.9 / 22 | 21.9 | 0 | 1 | 356 | 0 | 20 | 0% / 0% | 96% | 9.5 |

### 4. Game loop with qwen2.5:0.5b

The stand-in Ollama takes 0.3 to 0.5 s a reply (0.4 s on average, as measured by the owner with qwen2.5:0.5b at 10x), and works on one request at a time. One reply in 30 is not JSON and one in 50 never arrives (it times out). Frames run at 60 a second as in the game, with ticks, autosaves and AI updates where the game does them. Default settings: decisions wait up to 20 game minutes, requests time out after 60 s, 1 retry. 3 game days at each speed.

| Speed | Real time | Free-time choices | Chosen by AI | AI lines in chats | Avg / longest queue | Avg reply (s) | Timed out | Not JSON | Stale | Dropped | Longest wait (game min) | AI share of each resident's choices (lowest / highest) | Model busy | Slowest frame (ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 1.2 h | 546 | 97% | 96% | 0.1 / 9 | 0.4 | 7 | 32 | 14 | 0 | 13 | 91% / 99% | 8% | 5.1 |
| 2x | 36 min | 528 | 95% | 96% | 0.2 / 9 | 0.4 | 0 | 33 | 31 | 0 | 20 | 88% / 98% | 15% | 4.5 |
| 4x | 18 min | 479 | 91% | 97% | 0.4 / 9 | 0.4 | 0 | 38 | 41 | 0 | 20 | 85% / 94% | 26% | 6.8 |
| 10x | 7 min | 374 | 77% | 99% | 0.9 / 9 | 0.4 | 1 | 27 | 87 | 0 | 20 | 64% / 91% | 47% | 13.6 |

### 5. Ollama stops for five minutes and comes back

At 4x, 2 game days (12 real minutes) with the graphics-card stand-in. Ollama stops answering after 2 real minutes and starts again 5 minutes later.

- The game marked the AI offline 1 s after Ollama stopped, and residents carried on with the built-in decision maker.
- It reconnected 2 s after Ollama came back (it checks every 30 s).
- Over the run the AI made 54 of 152 free-time choices; the longest anyone waited on it was 20 game minutes.

### 6. A city ten times the size: 100 residents and one qwen2.5:0.5b

The owner's test: the starter town copied ten times side by side into one city (150 maps, 920 objects, 40 businesses), with 100 residents in one simulation and one AI queue, all served by one model. The districts are not joined up, so people only meet their own neighbours. Copied residents start without the starter residents' background memories.

The stand-in Ollama takes 0.3 to 0.5 s a reply (0.4 s on average, as measured by the owner with qwen2.5:0.5b at 10x), and works on one request at a time. One reply in 30 is not JSON and one in 50 never arrives (it times out). Frames run at 60 a second as in the game, with ticks, autosaves and AI updates where the game does them. Default settings: decisions wait up to 20 game minutes, requests time out after 60 s, 1 retry. 2 game days at each speed.

| Speed | Real time | Free-time choices | Chosen by AI | AI lines in chats | Avg / longest queue | Avg reply (s) | Timed out | Not JSON | Stale | Dropped | Longest wait (game min) | AI share of each resident's choices (lowest / highest) | Model busy | Slowest frame (ms) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 48 min | 3271 | 78% | 21% | 10.5 / 32 | 0.4 | 48 | 263 | 703 | 814 | 20 | 26% / 96% | 46% | 229.5 |
| 2x | 24 min | 3027 | 55% | 4% | 15.9 / 32 | 0.4 | 3 | 200 | 936 | 1322 | 20 | 12% / 85% | 61% | 280.9 |
| 4x | 12 min | 3078 | 24% | 0% | 18.4 / 32 | 0.4 | 2 | 88 | 1497 | 1614 | 20 | 0% / 47% | 68% | 306.0 |
| 10x | 5 min | 3537 | 7% | 0% | 23.7 / 32 | 0.4 | 2 | 38 | 1648 | 2363 | 20 | 0% / 28% | 79% | 309.0 |

Without the AI, two game days of the 100-resident city: ticks take 0.79 ms on average, 99.9% under 46.6 ms, slowest 72.7 ms. At 10x that is about 0.8% of one core. The save is 3221 KB; saving takes 46 ms and loading 167 ms.


## Camera panels (owner's PC, 30 October 2026)

Measured with Settings → Test Camera Panels on build 0.0.76: Intel Core i9-10900F, NVIDIA GeForce RTX 3090, a 2558 × 1360 window. The game was capped at 240 fps.

| Panels | Average ms | FPS | Slowest 1 in 20 (ms) |
|---:|---:|---:|---:|
| 1–7 | 4.2 | 237–240 | 4.2 |
| 8 | 5.0 | 199 | 8.3 |
| 9 | 10.7 | 94 | 16.7 |
| 10 | 13.4 | 74 | 20.2 |
| 12 | 14.7 | 68 | 19.8 |
| 14 | 12.5 | 80 | 17.3 |
| 16 | 14.2 | 70 | 20.8 |

Every count up to 16 stayed above 60 fps, so the limit went from 10 to 16, enough for Watch Everyone to show all fifteen starting residents. From 9 panels a frame takes about three times as long, so slower graphics cards may want fewer; the test in Settings measures any PC.

### With 50 people (build 0.0.81)

The same PC with the test filling a copy of the city to 50 people, so every panel follows someone: 1–2 panels about 230 fps, 7 panels 80 fps, 9 and 13 panels about 60–66 fps, 16 panels 52 fps, 25 panels 40 fps, 32 panels 31 fps and 50 panels 41 fps (the slowest frame in twenty 29–62 ms from 20 panels up). The limit stays at 16: smooth with today's fifteen residents and still above 50 fps with fifty. Every panel draws its own name labels, which is likely part of that cost (not yet measured on its own); object names now show only when in use or under the pointer.
