# Tiny City 2 stress test

Run with .NET 8.0.33 on 4 cores (Ubuntu 24.04.5 LTS), 2026-11-06 02:55 UTC.
Unity's Mono runs the same simulation code roughly 2–3 times slower.

## 1. Tiny City 2 on the open land with qwen2.5:7b on a graphics card

The AI makes every choice, as in the game; the clock keeps running while it thinks, on the flat map with its 20 people (nobody has a bed, so they sleep on benches or the ground). The stand-in Ollama takes 1.5 to 4 s a reply, like Qwen2.5 7B on a mid-range GPU and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 1 game day at each speed.

| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 20 | 1x | 24 min | 1.00x | 0% | 439 | 188 | 33 | 174 | 3 | 13 | 11 | 17 | 26.9 | 185 | 3.1 | 2 | 0 |
| 20 | 2x | 12 min | 2.00x | 0% | 209 | 105 | 33 | 55 | 0 | 5 | 11 | 0 | 43.7 | 172 | 3.2 | 2 | 0 |
| 20 | 4x | 6 min | 4.00x | 0% | 98 | 62 | 25 | 6 | 0 | 0 | 5 | 0 | 54.5 | 156 | 3.4 | 1 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

## 2. Tiny City 2 on the open land with qwen2.5:7b on the CPU only

The AI makes every choice, as in the game; the clock keeps running while it thinks, on the flat map with its 20 people (nobody has a bed, so they sleep on benches or the ground). The stand-in Ollama takes 12 to 30 s a reply, like Qwen2.5 7B without a usable GPU and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 0.5 game days at each speed.

| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 20 | 1x | 12 min | 1.00x | 0% | 54 | 10 | 44 | 0 | 0 | 0 | 0 | 0 | 650.7 | 695 | 25.4 | 0 | 0 |
| 20 | 2x | 6 min | 2.00x | 0% | 30 | 0 | 30 | 0 | 0 | 0 | 0 | 0 | 0.0 | 0 | 25.5 | 0 | 0 |
| 20 | 4x | 3 min | 3.99x | 0% | 16 | 0 | 16 | 0 | 0 | 0 | 0 | 0 | 0.0 | 0 | 25.5 | 0 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

## 3. Tiny City 2 on the open land with qwen2.5:0.5b

The AI makes every choice, as in the game; the clock keeps running while it thinks, on the flat map with its 20 people (nobody has a bed, so they sleep on benches or the ground). The stand-in Ollama takes 0.3 to 0.5 s a reply (0.4 s on average, as measured by the owner with qwen2.5:0.5b at 10x) and works on 1 request at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. 1 game day at each speed.

| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | decisions | day plans | lines | reacts | insights | reflections | ratings | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 20 | 1x | 24 min | 1.00x | 0% | 2084 | 445 | 39 | 972 | 482 | 35 | 20 | 91 | 2.9 | 30 | 0.4 | 3 | 0 |
| 20 | 2x | 12 min | 2.00x | 0% | 1156 | 315 | 39 | 502 | 215 | 24 | 19 | 42 | 4.0 | 36 | 0.4 | 3 | 0 |
| 20 | 4x | 6 min | 4.00x | 0% | 569 | 233 | 36 | 223 | 49 | 17 | 11 | 0 | 4.7 | 33 | 0.5 | 1 | 0 |

"Requests per game day" and the columns by kind count every request sent to the model, retries included. "Wait for a choice" is real time from a resident starting to decide until they act. "Actually ran at" is game minutes per real second, so 1.00x means the city never had to wait.

