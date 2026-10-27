# Manual Playtest Checklist

Run these on the Windows build downloaded from the latest green CI run. Automated tests cover the rules; this list covers what only a person can judge.

## Phase 1: one resident

1. **Start:** a new install opens to Rose Cottage with Mabel in bed. The panel on the right shows the day and time, her needs and "Recently".
2. **Clock:** time advances about one in-game minute per real second at 1x. Pause, 2x, 4x and 10x work from the buttons and from Space and keys 1–4.
3. **Morning routine:** within the first in-game hour she uses the toilet and cooks breakfast. The need bars rise while she does each activity.
4. **Walking and doors:** she walks tile by tile, never through walls, and leaves the cottage by the front door. The view switches to the village when she goes outside.
5. **Work:** on a weekday she goes to the community garden by about 08:30 and works until 17:00. If she gets very thirsty, she visits the garden tap and goes back to work.
6. **Evening and night:** after work she showers, eats and relaxes (reading, the park bench or the fitness corner). Around 22:30 she goes to bed and sleeps until about 06:30.
7. **Weekend:** on Saturday (day 6) she does not go to work.
8. **Viewing the other map:** Tab shows the other map and F returns to following her.
9. **Save and load:** press F5 at a recognisable moment, wait a few minutes, then press F9. She returns to the same place and activity, with the same need values.
10. **Exit and reopen:** close the window, then reopen the game. The town continues from where it was, and the status line says the saved town was loaded.
11. **Start over:** "Start over" asks for a second click, then begins a fresh town on day 1.
12. **Settings warning:** edit `TheTinyTown_Data/StreamingAssets/Configuration/settings.json` so the model is `""`. The game falls back to the default settings (qwen2.5:7b), and `Player.log` contains a settings warning.

## Phase 2: local AI and memory

Start Ollama first (see [OllamaSetup.md](OllamaSetup.md)), then the game.

1. **Connection:** within a few seconds the AI line at the bottom of the panel says "connected to qwen2.5:7b". The first connection can take up to a minute while Ollama loads the model.
2. **AI decisions:** after work or on a weekend, the inspector shows "thinking about what to do" for a moment, then a new activity. "Why" says "in Mabel's words, via AI" and the reason reads like her, not like a rule. The log line ends with "(AI)".
3. **Speech:** a speech bubble appears over Mabel for a few seconds when the AI gives her a line, and "Last said" shows it in the panel.
4. **The game stays in charge:** the AI never sends her to work on a weekend, to bed in the afternoon or through a wall. When she is desperately hungry, thirsty, tired or needs the toilet, she deals with it at once without waiting for the AI.
5. **Reflection:** after she goes to bed for the night, a new memory for that day appears under "Remembers" within a minute or so.
6. **Ollama stopped mid-game:** quit Ollama from the system tray. Within a minute the AI line says "offline" and Mabel carries on with her built-in routine. Nothing freezes.
7. **Ollama restarted:** start Ollama again. Within about 30 seconds the AI line says "connected" again and AI decisions resume.
8. **Wrong model:** set `"model": "no-such-model"` in `settings.json` and restart the game. The AI line says the model is missing and how to install it; Mabel uses her routine.
9. **AI turned off:** set `"enabled": false` in `settings.json` and restart. The AI line says the AI is off, and the game behaves like Phase 1.
10. **Memories survive saving:** press F5, then F9. "Remembers" and "Last said" are unchanged. Close and reopen the game: they are still there.
11. **Speed:** at 10x the AI often can't answer within her short wait, so she falls back to her routine more often ("no AI answer in time" in the log). At 1x most free-time decisions should come from the AI.
12. **Old save:** a Phase 1 save loads without errors, and Mabel gains her two background memories.

## Phase 3: social simulation

1. **Neighbours:** a new town has Mabel in Rose Cottage, Arthur in Willow Cottage and Nell in Fern Cottage. The buttons under the speed controls switch the inspector and camera between them; N does the same.
2. **Several residents on screen:** on the village map everyone outdoors is drawn with their name. Two residents on the same tile stand side by side.
3. **Meeting and chatting:** within the first day or two, residents stop to talk when they pass each other, or go "looking for" someone when they feel lonely or sociable. While they talk, both show "chatting with ...", and speech bubbles alternate between them.
4. **Relationships:** after a chat, "People" in the inspector changes from "hasn't met yet" to a description such as "acquaintance", with friendship and trust numbers. Further chats raise them gradually.
5. **Shared memories:** after a first meeting, both residents have a "met ... for the first time" memory under "Remembers".
6. **Interruptions:** a chat ends early when someone gets desperately hungry, thirsty, tired or needs the toilet, when work starts for one of them, or at bedtime. The log says who had to go and why.
7. **Private homes:** residents never use furniture in someone else's cottage.
8. **Work:** on weekdays Nell walks to the repair workshop by about 09:30 and works until 18:00. Arthur is retired.
9. **AI conversation lines (Ollama running):** lines read like the residents and follow on from each other. The topic appears after "chatting with ... about". If Ollama is slow or stopped, the chat carries on with simple small talk.
10. **Save mid-chat:** press F5 while two residents are talking, then F9. They are still talking, and what was already said is kept.
11. **Old save:** a Phase 2 save loads without errors; the status line says two new neighbours moved in, and Mabel keeps her memories.

Saves are in `%USERPROFILE%\AppData\LocalLow\The Tiny Town\The Tiny Town\Saves\` (`town.json` plus `town.json.bak`).
