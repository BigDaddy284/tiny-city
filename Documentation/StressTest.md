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

## Tiny City 2: the AI budget (4 November 2026)

Run on the `v2` branch with `dotnet run -c Release -- <report.md> 0 0 0 10 1 0 1` (the last argument is how many game days of Tiny City 2 to run at each speed). The city is the fifteen starter residents with `AiRunsEverything`, so every choice goes to the model and the clock waits whenever anyone is deciding, as in the game. The same stand-in Ollama is used, with the same caveats: nothing here was measured against a real model or in the Windows build.

| Finding | Before | After |
|---|---|---|
| Every "Saw …" memory was sent to the model to be rated | About 350 rating requests a game day, the biggest single kind | Observations keep the game's figure (`PerceptionRules.ObservationImportance`), and the rest are rated only once a resident has `AiService.RatingBatchMin` (4) to rate or the oldest has waited `RatingMaxWaitMinutes` (6 game hours): about 30 a day |
| Residents were asked whether to go over and talk to almost everyone they passed | About 250 reaction requests a game day | Each resident is offered at most one reaction every `PerceptionRules.ReactEveryMinutes` (120): about 100 a day |
| Calls per game day with qwen2.5:7b on a graphics card | About 1,000 | About 400 to 430 |

What it means for play: with one request at a time, decisions alone (about 190 a game day, 3 s each on a mid-range GPU) take up much of a game day at 1x, and residents tend to decide at the same moments (waking, finishing work), so they queue. The city then runs at about 0.6x when 1x is chosen and about 0.9x at 4x. With `parallelRequests` 2 (and Ollama's `OLLAMA_NUM_PARALLEL` 2) the same run reached 0.65x, 0.87x and 1.06x, with the average wait for a choice down from 14 s to 10 s. On the CPU only the city runs at about a seventh of normal speed whatever is chosen. A small model (qwen2.5:0.5b) keeps up at 1x and reaches about 2.5x at 4x. Nobody died in any run, though the stand-in picks options in turn, so that says nothing about a real model.

### Tiny City 2 with qwen2.5:7b on a graphics card

The AI makes every choice and the clock waits for it, as in the v2 game. The stand-in Ollama takes 1.5 to 4 s a reply, like Qwen2.5 7B on a mid-range GPU and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 1 game day at each speed.

| Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 40 min | 0.60x | 48% | 427 | 192 | 30 | 58 | 116 | 0 | 8 | 23 | 14.2 | 131 | 3.2 | 1 | 0 |
| 2x | 29 min | 0.82x | 64% | 402 | 181 | 29 | 52 | 103 | 0 | 9 | 28 | 14.4 | 131 | 3.2 | 0 | 0 |
| 4x | 26 min | 0.94x | 80% | 392 | 173 | 29 | 70 | 82 | 0 | 8 | 30 | 14.9 | 131 | 3.2 | 0 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act, which is what holds the clock. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

### Tiny City 2 with qwen2.5:7b on the CPU only

The AI makes every choice and the clock waits for it, as in the v2 game. The stand-in Ollama takes 12 to 30 s a reply, like Qwen2.5 7B without a usable GPU and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 0.5 game days at each speed.

| Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 1.6 h | 0.13x | 89% | 466 | 212 | 32 | 90 | 96 | 0 | 2 | 34 | 138.4 | 877 | 24.3 | 0 | 0 |
| 2x | 1.5 h | 0.14x | 94% | 450 | 212 | 32 | 74 | 96 | 0 | 2 | 34 | 137.6 | 877 | 24.2 | 0 | 0 |
| 4x | 1.4 h | 0.14x | 97% | 450 | 212 | 32 | 74 | 96 | 0 | 2 | 34 | 137.8 | 877 | 24.2 | 0 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act, which is what holds the clock. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

### Tiny City 2 with qwen2.5:0.5b

The AI makes every choice and the clock waits for it, as in the v2 game. The stand-in Ollama takes 0.3 to 0.5 s a reply (0.4 s on average, as measured by the owner with qwen2.5:0.5b at 10x) and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 1 game day at each speed.

| Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1x | 27 min | 0.91x | 20% | 496 | 199 | 30 | 103 | 116 | 0 | 10 | 38 | 3.0 | 30 | 0.5 | 1 | 0 |
| 2x | 16 min | 1.54x | 37% | 527 | 205 | 28 | 124 | 121 | 0 | 9 | 40 | 3.2 | 30 | 0.5 | 1 | 0 |
| 4x | 10 min | 2.47x | 47% | 426 | 185 | 27 | 67 | 103 | 0 | 10 | 34 | 3.4 | 30 | 0.5 | 0 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act, which is what holds the clock. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

## Tiny City 2 on the open land: how many people (5 November 2026)

Run on the `v2` branch with `dotnet run -c Release -- <report.md> 0 0 0 10 1 0 1` after the flat map came in: the open land with 15 people (the starter residents), 27 (the camp full) and 50 (the most who can ever be alive), each run 1 game day at each speed (half a day on the CPU stand-in). It ran on the first layout of the open land, before it was laid out to the owner's drawing; the people and the AI requests are the same on the new layout, so the numbers should hold, but they were not run again. Nothing here was measured against a real model or in the Windows build.

| Finding | Before | After |
|---|---|---|
| The AI request queue held at most 32 waiting requests, and in Tiny City 2 each person can wait on a choice, a day plan, a chat line and a reaction at once | At 50 people it gave up 11,000 to 77,000 requests a game day, each asked again straight away | `AiRequestQueue.DefaultMaxPending` is 4 × `PopulationRules.MaxLiving` (200): 146 to 275 given up a game day, the same rate per person as with 15 |

What it means for play: every extra person adds decisions to the one queue, so the clock is held more. With qwen2.5:7b on a graphics card the land runs at about 0.56x with 15 people, 0.38x with 27 and 0.17x with 50, and a choice waits 16, 24 and 39 seconds on average. On the CPU only it is 0.09x, 0.05x and 0.02x. A small model (qwen2.5:0.5b) keeps up best: 0.89x, 0.78x and 0.58x at 1x, and up to 2.28x at 4x with 15 people. More than about 15 people with a 7B model will feel slow unless Ollama answers several requests at once (`parallelRequests`).

### Tiny City 2 on the open land with qwen2.5:7b on a graphics card

The AI makes every choice and the clock waits for it, as in the v2 game, on the flat map with 15, 27, 50 people (the starter residents, then newcomers until the camp is full, then the most who can ever be alive; past the camp's beds they sleep on benches). The stand-in Ollama takes 1.5 to 4 s a reply, like Qwen2.5 7B on a mid-range GPU and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 1 game day at each speed.

| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 15 | 1x | 43 min | 0.56x | 52% | 528 | 209 | 28 | 107 | 136 | 1 | 8 | 39 | 15.6 | 131 | 3.1 | 24 | 0 |
| 15 | 2x | 34 min | 0.71x | 70% | 534 | 212 | 29 | 107 | 135 | 2 | 7 | 42 | 15.7 | 131 | 3.2 | 26 | 0 |
| 15 | 4x | 29 min | 0.83x | 82% | 499 | 225 | 28 | 95 | 107 | 0 | 7 | 37 | 17.1 | 131 | 3.2 | 36 | 0 |
| 27 | 1x | 1.1 h | 0.38x | 71% | 960 | 485 | 53 | 125 | 221 | 1 | 7 | 68 | 23.6 | 286 | 3.2 | 79 | 0 |
| 27 | 2x | 54 min | 0.44x | 82% | 889 | 429 | 52 | 145 | 182 | 0 | 13 | 68 | 25.9 | 286 | 3.2 | 73 | 0 |
| 27 | 4x | 50 min | 0.48x | 90% | 838 | 421 | 52 | 154 | 136 | 0 | 16 | 59 | 27.4 | 286 | 3.3 | 73 | 0 |
| 50 | 1x | 2.4 h | 0.17x | 91% | 2386 | 1229 | 97 | 476 | 439 | 1 | 19 | 125 | 39.3 | 577 | 3.2 | 275 | 0 |
| 50 | 2x | 2.3 h | 0.17x | 95% | 2353 | 1228 | 99 | 477 | 408 | 2 | 17 | 122 | 39.8 | 577 | 3.2 | 274 | 0 |
| 50 | 4x | 2.3 h | 0.18x | 97% | 2288 | 1182 | 97 | 465 | 407 | 2 | 14 | 121 | 40.7 | 577 | 3.2 | 256 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act, which is what holds the clock. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

### Tiny City 2 on the open land with qwen2.5:7b on the CPU only

The AI makes every choice and the clock waits for it, as in the v2 game, on the flat map with 15, 27, 50 people (the starter residents, then newcomers until the camp is full, then the most who can ever be alive; past the camp's beds they sleep on benches). The stand-in Ollama takes 12 to 30 s a reply, like Qwen2.5 7B without a usable GPU and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 0.5 game days at each speed.

| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 15 | 1x | 2.3 h | 0.09x | 93% | 712 | 240 | 32 | 240 | 148 | 0 | 0 | 52 | 149.3 | 877 | 23.2 | 12 | 0 |
| 15 | 2x | 2.2 h | 0.09x | 97% | 712 | 240 | 32 | 240 | 148 | 0 | 0 | 52 | 150.4 | 877 | 23.2 | 12 | 0 |
| 15 | 4x | 2.2 h | 0.09x | 98% | 712 | 240 | 32 | 240 | 148 | 0 | 0 | 52 | 151.0 | 877 | 23.2 | 12 | 0 |
| 27 | 1x | 4.0 h | 0.05x | 97% | 1208 | 514 | 58 | 338 | 216 | 0 | 0 | 82 | 260.6 | 1956 | 23.6 | 41 | 0 |
| 27 | 2x | 4.0 h | 0.05x | 98% | 1208 | 514 | 58 | 338 | 216 | 0 | 0 | 82 | 261.3 | 1956 | 23.6 | 41 | 0 |
| 27 | 4x | 4.0 h | 0.05x | 99% | 1208 | 514 | 58 | 338 | 216 | 0 | 0 | 82 | 261.7 | 1956 | 23.6 | 41 | 0 |
| 50 | 1x | 10.4 h | 0.02x | 99% | 3094 | 1420 | 106 | 908 | 506 | 0 | 2 | 152 | 412.9 | 4096 | 23.6 | 146 | 0 |
| 50 | 2x | 10.4 h | 0.02x | 100% | 3094 | 1420 | 106 | 908 | 506 | 0 | 2 | 152 | 413.2 | 4096 | 23.6 | 146 | 0 |
| 50 | 4x | 10.4 h | 0.02x | 100% | 3094 | 1420 | 106 | 908 | 506 | 0 | 2 | 152 | 413.3 | 4096 | 23.6 | 146 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act, which is what holds the clock. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

### Tiny City 2 on the open land with qwen2.5:0.5b

The AI makes every choice and the clock waits for it, as in the v2 game, on the flat map with 15, 27, 50 people (the starter residents, then newcomers until the camp is full, then the most who can ever be alive; past the camp's beds they sleep on benches). The stand-in Ollama takes 0.3 to 0.5 s a reply (0.4 s on average, as measured by the owner with qwen2.5:0.5b at 10x) and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 1 game day at each speed.

| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 15 | 1x | 27 min | 0.89x | 20% | 575 | 232 | 26 | 118 | 149 | 0 | 11 | 39 | 3.5 | 33 | 0.5 | 29 | 0 |
| 15 | 2x | 15 min | 1.56x | 35% | 602 | 231 | 30 | 136 | 153 | 1 | 3 | 48 | 3.2 | 33 | 0.5 | 26 | 0 |
| 15 | 4x | 11 min | 2.28x | 52% | 563 | 215 | 27 | 122 | 147 | 1 | 5 | 46 | 3.5 | 33 | 0.5 | 22 | 0 |
| 27 | 1x | 31 min | 0.78x | 39% | 1186 | 504 | 53 | 250 | 288 | 3 | 11 | 77 | 4.7 | 63 | 0.5 | 76 | 0 |
| 27 | 2x | 19 min | 1.23x | 53% | 1028 | 450 | 50 | 199 | 245 | 0 | 13 | 71 | 5.3 | 63 | 0.5 | 75 | 0 |
| 27 | 4x | 15 min | 1.55x | 70% | 1008 | 433 | 49 | 194 | 246 | 2 | 13 | 71 | 5.0 | 63 | 0.5 | 61 | 0 |
| 50 | 1x | 41 min | 0.58x | 65% | 2524 | 1189 | 95 | 529 | 533 | 7 | 17 | 154 | 7.9 | 122 | 0.5 | 241 | 0 |
| 50 | 2x | 35 min | 0.69x | 82% | 2520 | 1167 | 95 | 567 | 518 | 10 | 20 | 143 | 7.8 | 122 | 0.5 | 234 | 0 |
| 50 | 4x | 31 min | 0.77x | 89% | 2381 | 1222 | 96 | 452 | 462 | 3 | 14 | 132 | 7.9 | 122 | 0.6 | 256 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act, which is what holds the clock. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

