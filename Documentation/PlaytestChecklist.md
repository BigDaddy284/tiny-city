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

## Phase 4, build 1: ten residents, jobs, money and shops

1. **The bigger village:** a new town has ten residents. The name buttons wrap onto two rows. On the village map each door is labelled with its building: ten cottages, the General store, the Café, the Bakery and the Clinic. Tab steps through every map, including the four shop interiors.
2. **Jobs and wages:** pick Rosa (shopkeeper, Mon–Sat 08:00–17:00), Tom (café cook), Priya (café server, Wed–Sun 12:00–20:00), Ivy (baker, Tue–Sun from 07:00), Samuel (doctor) or Mabel/Nell. The line under their name shows their job, days, hours and wage. They walk to work in time, and when the shift ends the log says "Was paid £… in wages" and the money line goes up.
3. **Days off:** Priya stays home on Monday and Tuesday; Ivy on Monday; Rosa on Sunday.
4. **Monday bills:** at 07:00 every Monday (including the very first minute of a new game) each resident's rent leaves and Arthur's and Edith's pensions and Felix's allowance arrive. The three latest payments show under the money line.
5. **Groceries:** cooking at home lowers "food at home". When it gets to two meals or fewer and the store is open, residents go shopping (£12.00 for six meals), and the store's stock goes down.
6. **Eating out:** during opening hours residents sometimes have a meal or coffee at the café or a pastry at the bakery, and the log says "Paid £… (…)". Outside opening hours nobody buys anything there, and the inspector's "Where" line shows a shop as closed.
7. **Never stuck hungry:** make a resident who is out of food and money get hungry (or just watch Felix, whose money is tight): they "make do with a snack" at home instead of going without.
8. **Owing rent:** a resident who can't cover the rent goes below £0 and remembers it under "Remembers".
9. **AI (Ollama running):** free-time choices still come through; paid options are listed with their prices, and a resident with little money tends to choose free things. The AI status line should not show a growing queue at 4x or 10x with ten residents.
10. **Old save:** a Phase 3 save loads without errors; the status line says seven new neighbours moved in, and Mabel, Arthur and Nell get their starting money, groceries and wages.
11. **Save and load:** F5, carry on for a few hours, then F9: money, food at home, shop stock and the recent payments are as they were at the save.

## Phase 4, build 2: hobbies, health and ageing

1. **Hobbies:** each resident's inspector shows two hobbies. New spots on the map: an easel by the top pond, a chess table in the village square, a fishing spot by the park pond, and a piano in the café. Residents only use the spots for their own hobbies (knitting uses the armchair, baking the kitchen).
2. **Doing hobbies together:** chess, fishing and gardening are shared. When someone sets off for one, the log often says "Invited … to join them", and the other resident says "Accepted …'s invitation". Two residents at the same spot both get "I spent time … with …" under "Remembers", and "People" shows they've met.
3. **Birthdays:** the inspector shows each birthday as a day of the 28-day year. On that day at midnight their age goes up, the log says "Turned …", and there's a birthday memory. Turning 30, 45 or 65 also adds a life-stage memory.
4. **Health:** the inspector shows health out of a maximum that falls with age (Edith's is about 67). Health holds steady or slowly recovers when needs are looked after.
5. **Colds:** someone wakes up with a cold (log: "Woke up with a cold"). They still go to work, and get better in a day or so ("Felt better: over a cold").
6. **Flu:** someone with flu calls in sick (log: "Called in sick"), rests in bed ("resting in bed, feeling poorly") and won't chat. There are no wages for the day.
7. **Pneumonia and the clinic:** this is rare in normal play. If flu strikes someone whose health is below 30, it turns into pneumonia. They go to the clinic as soon as it's open (Monday to Friday, 09:00–17:00), and after "being seen by the doctor" their recovery starts.
8. **Old age:** residents aged 80 or over can die of old age in the small hours. Edith, 82, usually does within a few game months. Their name button gets a †, the inspector shows the day and cause, they disappear from the map, and their neighbours remember them (friends most deeply). They no longer pay rent or use the AI.
9. **AI (Ollama running):** decisions still come through, and paid, hobby and chat options all appear. A resident with a cold or flu is described as poorly in the prompt.
10. **Old save:** a build 1 save loads without errors. Everyone is healthy, and the starter residents get their hobbies.
11. **Save and load:** F5 while someone is ill, then F9: the illness, its recovery time, health and any death record are kept.

## Phase 4, build 3: romance, community events and village news

1. **Calendar:** under the clock, the panel shows the next event ("Next: Quiz night, Wednesday (day 3) at 18:00") or the one on now. There are three: a Sunday get-together on the pond-side lawn (15:00–17:00), a Wednesday quiz night in the café (18:00–20:00), and the village fête on the village green on day 14 of every 28-day year (14:00–17:00, instead of that Sunday's get-together). The gathering spots are new orange squares on the map.
2. **Going to events:** while an event is on, a few free residents head there (log: "Decided to go to a village event … The quiz night is on at the café"). Sociable residents go more often, and the fête draws a crowd. Each one remembers it once ("I went to the quiz night at the café. Tom was there too.").
3. **Village news:** the bottom of the panel lists the latest town history: who came to each event, couples, break-ups and deaths.
4. **Romance (slow):** "People" shows "soft spot" when someone has feelings for a neighbour. Feelings grow only between adults who are open to it, are within 12 years of each other's age, already friends, and have chemistry, which most pairs don't. Arthur and Edith aren't looking. The inspector's "Love life" line says single, dating or committed. In the trial run one couple got together on about day 15 and committed two weeks later; in your game it may be different people, later, or nobody.
5. **Couples:** a couple seek each other out first for chats and invite each other for coffee or a meal at the café. Now and then they have a row (log: "Had a row with …"). Enough rows or fading feelings end in a break-up, and after a week apart they can get back together.
6. **Loss:** if someone in a couple dies, their partner's top memory is losing them.
7. **Settings:** set `"romance": false` or `"communityEvents": false` in `settings.json` (see the Ollama setup page) and restart: no new couples, or no events, and the panel says so.
8. **AI (Ollama running):** a resident's prompt mentions who they're dating or have a soft spot for, and "Go to a village event" is one of the choices while one is on. Warm and playful AI lines help feelings grow; disagreements set them back.
9. **Old save:** a build 2 save loads without errors, with nobody in a couple and empty village news. Arthur and Edith are "not looking for romance".
10. **Save and load:** F5 while a couple exists, then F9: the couple, their stage and the village news are as they were.

Saves are in `%USERPROFILE%\AppData\LocalLow\The Tiny Town\The Tiny Town\Saves\` (`town.json` plus `town.json.bak`).
