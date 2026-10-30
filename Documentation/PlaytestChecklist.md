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
12. **Settings warning:** edit `TinyCity_Data/StreamingAssets/Configuration/settings.json` so the model is `""`. The game falls back to the default settings (qwen2.5:7b), and `Player.log` contains a settings warning.

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
12. **Plans:** after an AI decision the inspector often shows "Planned next" with one or two more activities. When the first one ends, the next starts straight away without "thinking about what to do", and the log line ends with "(AI plan)". If something gets in the way (she is hungry, it's bedtime, the café shut), the plan gives way: hunger comes first, bedtime clears the plan, and a step that no longer fits is skipped.
13. **Old save:** a Phase 1 save loads without errors, and Mabel gains her two background memories.

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

## Phase 5, build 1: camera panels and inspector

1. **One panel:** the game opens with one camera panel left of the inspector. Its dark header names who it follows and where ("Mia · Mia's cottage"). Outdoors the camera stays close and moves with them; indoors it shows the whole room. Walk-throughs of doors should switch maps without a jump to the wrong place.
2. **More panels:** the + and − buttons under the speed controls (or the + and − keys) add and remove panels, from one to ten. Each new panel follows someone nobody is watching yet, and the layout rearranges into a grid. "Watch everyone" gives each living resident their own panel.
3. **Active panel:** click inside a panel to make it active (orange border when there is more than one). Clicking a resident's figure shows them in the inspector. Picking a name in the roster makes the active panel follow them.
4. **Header buttons:** ◀ and ▶ move a following panel to the previous or next resident. "Free" turns it into a free camera on the same map; "Follow" puts it back on the resident chosen in the inspector. × closes the panel (not shown when only one is left).
5. **Free camera:** drag to pan (it stays over its map), and Tab or ◀ ▶ move it to the next map, so you can watch an empty shop or the village green. The mouse wheel zooms any panel; names of objects and doors appear once you zoom in.
6. **Keys:** N and B are next and previous resident in the active panel, Tab is free camera and next map, F follows the inspector's resident. Space, 1–4, F5 and F9 work as before.
7. **Inspector tabs:** "Now" has health, where, doing, why, the routine's next step, last words, needs and village news. "People" lists relationships, closest first. "Memories" shows the latest memories and the last three conversations line by line. "Work" has money, rent, income and the last ten payments. "Life" has birthday, hobbies, love life, life events from the village records and the recent log.
8. **Facts and AI text:** anything the AI wrote (an AI-chosen reason, AI-written lines in conversations, bedtime reflections) sits in a blue-tinted box or is labelled "(AI)"; everything else is game fact.
9. **Layout kept:** quit with three panels following different people, restart: the same three panels come back.
10. **Death:** a panel following someone who dies turns into a free camera where they were.
11. **Performance:** with ten panels at 4x the game should stay smooth. Note it if it doesn't, with roughly how many frames you think you're getting.

## Phase 5, build 2: journal, timelines, notifications and export

1. **Town journal:** press J or click "Open the town journal" in the inspector. It lists the village records newest first, grouped by day: birthdays, new life stages, illness and recovery, first meetings, new good friends, couples, break-ups, gatherings and deaths. Big moments have a ★.
2. **Search:** type in the search box, for example a name, "flu", "quiz" or "day 3". Every word has to match. While you type, the game's keys (Space, N, J and so on) are ignored. Press Escape or click outside the journal to use them again.
3. **Filters:** the All / Life / Love / Health / Friends / Gatherings buttons narrow the list. "Only <name>" shows only the resident chosen in the inspector.
4. **Go to someone:** clicking an entry shows that resident in the inspector, and the active camera panel follows them if they're alive.
5. **Notifications:** deaths, couples, break-ups, getting back together, committing, a new life stage and serious illness pop up at the top left of the camera area for about 12 seconds (at most four at once). Clicking one goes to that resident. Birthdays and colds don't pop up; they're only in the journal.
6. **Timeline tab:** the inspector's last tab is now "Timeline": the village records about that resident plus their important memories, newest first. Their own memories say "(their memory)". Reflections the AI wrote are in a blue box.
7. **Export:** "Export life story" (Timeline tab or journal) and "Export town history" (journal) write text files. The status line shows where. "Open exports folder" opens it. The files are in `%USERPROFILE%\AppData\LocalLow\Tiny City\Tiny City\Exports\`. In a life story, `[memory]` and `[AI]` mark what didn't come from the village records.
8. **Old save:** a build 1 save loads normally. Its journal starts with what was recorded before (gatherings, couples, deaths) and fills in from then on.
9. **Save and load:** F5, then F9: the journal is the same.
10. **Place menu:** every panel header has a "Place ▾" button. It lists the village (outside) and every building. Picking one turns that panel into a free camera there, even if it was following someone indoors, and you can drag it around. A ✓ marks where a free panel is now. Click elsewhere or press Escape to close the list. In free mode, ◀ and ▶ (or Tab) step back and forward through the same list.

## Phase 5, build 3: save slots, copying and export

1. **Your existing town:** it appears as "My first town" in the saved towns list (press S, or "Saved towns" at the bottom of the inspector). The inspector shows "Town: …" under the title.
2. **Save list:** each town shows its name, the game date, how many residents are alive and when it was last saved. "(playing now)" marks the current one. A damaged file says it can't be read, and its Load button is greyed out.
3. **New town:** type a name next to "New town:" and click "Start it". The town you were playing is saved first, then a fresh village starts in its own slot. Your old town is untouched.
4. **Switching:** click Load on another town. The current one is saved first, so nothing is lost. The camera panels and inspector carry on with the loaded town.
5. **Copy:** makes "Copy of <name>" as a new slot, exactly as the town was at that moment (the town you're playing is saved first). Load the copy and play it differently. The original stays as it was.
6. **Rename:** type the new name and press Enter or OK. Names are kept after you quit.
7. **Export:** copies the save file to the Exports folder as "<name> save - day N.json", to back it up or share it. "Open saves folder" and "Open exports folder" open those folders.
8. **Delete:** click Delete, then "Click again to delete" within 4 seconds. You can't delete the town you're playing.
9. **Autosave, F5 and quitting** save the town you're playing. When you restart the game, it reopens that town.
10. **Typing:** while typing a name, the game's keys (S, J, Space and so on) are ignored. Click elsewhere or press Escape to use them again.

Saves are in `%USERPROFILE%\AppData\LocalLow\Tiny City\Tiny City\Saves\`: one `town.json`, `town-2.json`, … per town, each with a `.bak` backup, plus `slots.json` holding the town names.

## Phase 6, build 1: places

1. **Village:** the village is drawn in pixel art on a little block of earth: grass with flowers, cobbled paths with darker edges, the pond and the fishing pond, the vegetable garden, trees all around (some of them pines), and fourteen buildings. Each cottage has its own wall, roof and door colours, windows with flower boxes and a chimney. The store, café and bakery have striped awnings and a sign with a picture (apple, cup, loaf); the clinic is white with a red cross.
2. **Walking around buildings:** follow someone along a path behind a building and in front of it. They should disappear behind the building when they are behind it and stay in front of it when they are in front of it (including standing at a door). Note any spot where someone is drawn on top of a roof or hidden when they shouldn't be.
3. **Rooms:** go inside any cottage or shop (Place menu). The two back walls stand tall with wallpaper, windows and pictures; the two front walls are low so you can see in; the bathroom wall is half height. Both back walls are the same height and meet in a clean corner. Floors differ: wooden boards in cottages and the café, tiles in the store, bakery and clinic. Every piece of furniture has its own picture (bed, kitchen, sink, toilet, shower, armchair, shelves, counter, tables, piano, clinic beds, quiz board and so on).
4. **Outdoor things:** benches, the garden tap and watering can, the garden plot, the workshop bench, the easel, the chess table, the exercise mat, the jetty by the fishing pond and the maypole on the green.
5. **Day and night:** at speed 4, watch an evening: the light turns golden, then blue, and the windows light up. Indoors it turns warm lamplight instead of dark. Morning brings it back. Residents are tinted along with the place they're in.
6. **Seasons:** a year is 28 days, so each season lasts 7 days (days 1–7 spring, 8–14 summer, 15–21 autumn, 22–28 winter). The village changes when a new season starts: blossom and pale flowers in spring, fruit in summer, orange trees and fallen leaves in autumn, snow on the ground, roofs and pines, bare trees and a frozen pond in winter. There may be a short pause (about a second) at the moment the season changes while the village is redrawn. Note it if it's long.
7. **Zoom:** zoom in close: pixels should stay sharp and square, not blurry.
8. **Old saves:** any older town loads and shows the right season for its day.
9. **Performance:** with ten panels at 4x the game should stay as smooth as in Phase 5. Note it if it doesn't.
10. **Your own pictures (optional):** see `Documentation/ArtOverrides.md`. Put a `tree.png` in the game's `TinyCity_Data/StreamingAssets/Art/` folder, restart, and every broadleaf tree uses it.
11. **Walking around furniture:** watch someone cross a room or the café. They walk around cookers, sinks, tables, beds and benches and only step onto one to use it. The shop counter is now one tile to the left of the door and the café piano stands by the left wall, so neither is in anyone's way.

## Phase 6, build 2: residents

1. **Ten looks:** follow each resident in turn. Everyone is easy to tell apart: Mabel (orange, straw hat, ponytail), Arthur (blue cardigan, bald with glasses), Nell (green, short dark hair), Rosa (pink skirt, black curls, cream apron), Tom (yellow, ginger beard, white apron), Priya (lavender dress, long black hair), Ivy (yellow, blonde bob, white apron), Samuel (teal, beard and glasses), Felix (pale blue, messy hair) and Edith (grey cardigan, white bun, glasses).
2. **Walking:** at speed 1, legs step and arms swing as they walk, and they face the way they are going in all four directions (down-left, down-right, up-left, up-right).
3. **Using furniture:** they sit in armchairs, on benches, at café tables, on the piano bench, at the chess table and on the toilet; stand at the front of cookers, sinks, counters, shelves and workbenches facing them; and lie in bed with their head on the pillow when asleep (also in the clinic beds).
4. **Activities:** eating raises a hand to the mouth, reading holds a red book, work and hobbies move both hands, chatting opens the mouth and gestures, residents at a village gathering cheer, and exercise is star jumps.
5. **Talking:** two people chatting turn to face each other.
6. **Mood bubbles:** watch for the little bubbles over heads: "z" when asleep, bread when hungry, a drop when thirsty, heavy eyes when tired, "!" for the toilet, wavy lines when grubby, a rain cloud when lonely, "..." when bored, a green face when ill, a smile when everything is fine and a heart when talking with their partner. Names sit above the bubbles and speech above the names; note any overlap.
7. **Night:** residents darken with the light like the rest of the place they are in; the mood bubbles stay bright.
8. **Clicking:** clicking a resident still selects them in the inspector, including someone sitting or lying down.
9. **Performance:** with ten panels at 4x the game should stay smooth. Note it if it doesn't.

## Settings window

1. **Button:** a **Settings** button sits at the top right of the side panel, beside "Tiny City". Clicking it opens the Settings window over the town; **Done**, Escape or the button again closes it. Opening it closes the journal or saved towns, and vice versa.
2. **Theme:** the side panel, windows, buttons, sliders and camera panel headers use the Stills & Reels / StackSpace colours: navy in Dark (the default), white and pale blue in Light, with a blue-to-purple gradient on selected buttons. Switch between **Dark** and **Light**: everything changes at once, and the town's own art stays the same. Names over residents' heads and signs on the map stay readable in both.
3. **AI on/off:** with Ollama running, switch **Use local AI** off: the AI line says "off (Settings)" and residents carry on by routine. Switch it on: it reconnects within a few seconds.
4. **Model list:** the window lists your installed Ollama models, with the one in use highlighted. Pick another: the AI line shows it loading, then connected. Close Ollama and press **Refresh list**: a message says it couldn't be reached. Start it again and press Refresh list.
5. **Sound:** the four sliders move and keep their values (there is no sound until the next update).
6. **Screen:** try Windowed, Borderless and Full screen; step the resolution with ◀ ▶; turn VSync off and pick a frame limit. Each takes effect straight away.
7. **Kept:** quit and start the game again. The theme, AI choice, model, volumes and screen settings are as you left them.

## Tiny City: name and neon look

1. **Name:** the window title, the side panel ("Tiny City"), exported histories and life stories all say Tiny City. The build's folder and program are `TinyCity`.
2. **Old saves:** on a PC that played The Tiny Town, start Tiny City once. Your saved towns appear in the saved towns window (S) and load as before, and your Settings choices are kept. The old folder (`AppData\LocalLow\The Tiny Town`) is left untouched. The panel layout and which town was open last start fresh.
3. **City by day:** navy paving plates, purple tarmac streets with a neon kerb, and flat-roofed blocks in magenta, violet, blue and teal with air units and antennas on the roof. Homes have two floors of windows and sliding glass doors; shops have big windows, a neon tube along the front and a neon picture sign above the door. Nothing sticks out from a wall, and no lines cross a door.
4. **Evening and night:** from dusk, the roof edges, plinths, door frames, shop tubes and signs glow, windows light up amber or cyan, and antenna tips glow red. Residents walking past buildings go behind and in front of them correctly.
5. **Rooms:** dark panelled walls with a pink neon rail and cyan LED skirting, and windows that show the lit city.
6. **Seasons:** teal trees in spring, pink in summer, orange in autumn, and snow on the paving and roofs in winter.


## Café seats and finding people

1. **Café:** there are four tables (window, middle, centre and corner). Two people at a table sit on opposite chairs facing each other; the one on the far chair is behind the table. A third customer goes to another table, and someone on their own picks an empty table over sitting with a stranger.
2. **Benches and chess:** two people on a bench sit side by side; two chess players sit at either end of the board. When one leaves, the other stays put.
3. **Inspector top row:** "Town journal" (J still works), then −, + and Watch everyone, with the number of camera panels in a box on the far right. The row of name buttons is gone.
4. **People list:** click "People ▼". A scrolling list shows each person's name and age († for those who have died). Filter by Everyone, Living or Died and sort by first name, surname, oldest or youngest. Clicking a name shows them in the inspector and the active panel.
5. **Find a person:** the box left of "People ▼" shows how many people live in the city, like the panel count above it, and goes up when someone moves in or is born. Click the "Find a person…" box and type. Matching names appear underneath as you type (case doesn't matter, surnames count). Clicking one shows that person and clears the box. While typing, the game's keys (J, S, F5 and so on) don't fire; Escape or clicking the town leaves the box.

## Phase 6, build 3: sound and the themed window

1. **Music:** soft synth music starts a few seconds after the town appears. From dusk it crossfades to a slower night piece, and back at dawn.
2. **Ambience:** with the active panel outdoors you hear the city hum and hover traffic passing (by night, quieter traffic and the buzz of neon). Following someone indoors, it fades to a quiet room tone.
3. **Effects:** follow a resident. You hear their footsteps, a door whoosh going in or out, sizzling when they cook, a pour when they drink, beeps at shop counters, key taps at desks, digging in the garden, chess pieces, the piano and fishing. People far from the middle of the panel are quieter, and people on other maps are silent.
4. **City news:** a bright chime plays when a notable event pops up.
5. **Volumes:** in Settings, Master, Music, Ambience and Effects change the sound straight away, and 0 silences each one.
6. **Replacement sounds:** put a WAV called `music-day.wav` in `TinyCity_Data\StreamingAssets\Audio` and restart. It plays instead of the built-in music (see `Documentation/SoundOverrides.md`).
7. **Title bar:** on Windows 11 the window's title bar is the theme's dark navy, with light text, like Stills & Reels. Switch the theme to Light and it turns light. On Windows 10 it switches between the dark and light system title bars.

## Cyberpunk outfits

1. **Techwear:** every resident wears dark techwear with neon piping on the collar, down the front, at the cuffs, on the belt or skirt hem and on their trainer toes. From behind, a neon stripe crosses the shoulders.
2. **Touches:** Nell and Felix wear glowing visors (cyan and violet); Mabel, Rosa, Priya, Ivy and Felix have neon hair tips; Arthur, Samuel (white lab coat) and Edith wear long coats that hang to the knees; Tom wears a white chef's jacket.
3. **Readable:** everyone is easy to tell apart on the dark streets by day and at night, sitting and walking.

## Shop staff, the expanded people view and hiding the side panel

1. **Shops need staff:** on a weekday morning, watch Rosa walk to the general store. Before she arrives, inspecting someone in the store shows "closed, nobody at work", and nobody can buy groceries. Once she is in, it says "open". The same goes for the café (Tom and Priya), the bakery (Ivy) and the clinic (Samuel).
2. **Off sick:** when a shopkeeper is too ill to work and stays home, their shop stays shut for the day. If the only person who worked there dies, it stays shut for good.
3. **Expand:** open People ▼ and click Expand at the end of the Everyone / Living / Died row. The side panel shows the speed and panel controls, the filter and order buttons, and everyone's names in a two-column grid, A–Z by first name. Click a name to pick that person; the grid stays open. Click Back to return to the normal view.
4. **Hide the side panel:** click "Hide ▶" beside Settings, or press H. The city fills the whole window, and a "◀ Panel" button sits in the top-right camera panel's header, in line with its other buttons and not over them. Click it, or press H again, to bring the panel back. The choice is remembered the next time you start the game.

## Staggered sleep and the neon bar

1. **Bed and wake times:** at night, watch the whole town. People go to bed over about two hours rather than all at once. Younger people stay up later and older people turn in earlier. Mornings stagger the same way. Morning workers are always up in time for their shift.
2. **The bar:** there is a violet building with a cocktail-glass sign east of the lower cottages, with its door onto the bottom street. Inside are a bar counter, two high tables with stools, a 3 × 3 light-up dance floor, a DJ booth, a toilet and a sink.
3. **Felix's job:** from Wednesday to Sunday, Felix walks to the bar by 17:30 and works until 23:30. He goes to bed after his shift (about 00:15) and sleeps in. His inspector shows the job "Bar host" and his wages. An older save gives him the job when it is loaded, and his jobseeker's allowance stops.
4. **Opening hours:** the inspector shows the bar as "closed" on Monday, Tuesday and in the daytime. From 18:00 it is "open" once Felix is in.
5. **A night out:** on open evenings from 19:00, a few residents go for a drink at the high tables (paid, about £5.50) or to dance on the floor (free, arms up). Friends are sometimes invited along. Nobody heads out within an hour of their bedtime or with less than £30 to spend.
6. **Club night:** on Fridays from 20:00 to 23:00, a "Club night" event gathers people at the DJ booth. It appears in the journal like the quiz night does.

## Several AI requests at once and waiting longer at high speed

1. **High speed:** with the AI on, play at 4x and then 10x. Fewer residents log "no AI answer in time" than before, and residents in the Deciding state wait up to about 8 real seconds before the routine takes over.
2. **Parallel requests:** set `OLLAMA_NUM_PARALLEL` to 2, restart Ollama, set `"parallelRequests": 2` in `settings.json`, then start the game. The AI line in the inspector can show a queue count that includes two requests being answered. More free-time choices come from the AI at 4x than with 1.
3. **Too many at once:** with `parallelRequests` higher than Ollama's `OLLAMA_NUM_PARALLEL`, the game still works. The extra requests simply wait inside Ollama.

## The bigger city

1. **More room:** a new town is twice the size. The old streets and buildings are where they were, with new streets running on to the right and below.
2. **Apartment towers:** two tall six-floor towers stand along the back edge at the top right, behind everything else, so they don't hide anything. Each has four glass doors along its front and four up its right side, one per flat. At night their windows light up one by one.
3. **New homes:** six new cottages below the old ones, and sixteen flats in the towers. Nobody lives in them yet, and nobody wanders in to use them.
4. **Venues:** the cue club (eight-ball sign), Strike Lanes (skittle), the Pixel Arcade (space invader) and the Pulse Gym (dumbbell) sit right of the old shops. In the daytime and evening, residents with money to spare head out to play pool or snooker, bowl, play the arcade games or work out, and pay for it. They're shut at night.
5. **Market and park:** the market square has five striped stalls that residents browse, and a "Saturday market" event on Saturday mornings in the journal. The park below has a fountain residents sit round, plus more benches.
6. **Older saves:** load a town saved before this build. Everyone is where they were and carries on, and the new parts of the city are there around them.

## Newcomers

1. **Someone arrives:** start a new town and run it at 10x for a few days. From day 3, about two days a week, someone new appears at 10:00 on the path at the bottom edge of the city, a notification says they moved in, and they walk to an empty cottage or flat.
2. **Their own life:** select them. The inspector says "Moved to the city on day N" under their name, they have hobbies and a weekly income ("Works remotely" or "Jobseeker's allowance"), and they sleep in their new home at night.
3. **Journal:** the Town journal lists "… moved to the city, into …" under Life.
4. **Limits:** however long it runs, no more than ten people ever move in, and nobody arrives once fifty people live in the city. Someone who arrives when every home is taken sleeps on a park bench until one comes free.
5. **Save and load:** save, quit and reload. The newcomers are still there with the same day they moved in.
6. **Settings:** set `"newcomers": false` in `settings.json` and restart: nobody new arrives.

## Babies, children and homelessness

1. **A baby:** let a town run at 10x until two residents become a committed couple (the journal says so). Within a few weeks they have a baby: a notification, a journal entry under Life, and a little bundle in one parent's arms that goes everywhere with them, tucked up beside them in bed at night. The inspector says who the baby was born to and who carries them.
2. **Growing up:** a child ages a year every 14 days, twice as fast as adults. From 3 they walk about on their own, smaller than adults, go to bed earlier, sleep in their parent's bed, never work and never go into the bar. Teenagers are a little taller.
3. **Moving out:** at 18 they move into an empty cottage or flat, with a notification. If every home is taken they sleep on a park bench instead.
4. **Homeless:** someone with nowhere to live lies along a park bench at night (nobody sits on it with them), eats leftovers at the market stalls, and moves into the first home that comes free, at 09:00.
5. **Limits:** no baby is born while fifty people are alive, a couple has at most three children, and parents are never older than 50. Family never fall for each other.
6. **Save and load:** save with a baby and a child in town, then reload: they're still there with the same parents and birthdays.
7. **Settings:** set `"babies": false` in `settings.json` and restart: no more babies.

## Building cards and opening buildings

1. **Hover:** outdoors, rest the pointer on a building. A card appears beside it with the building's name, then for a home who lives there ("Home of …" or "Nobody lives here") and for a business whether it's open ("Open now, 18:00–23:30", "Closed, open …", "Closed today" or "Closed, nobody at work"), then who is inside right now ("Inside: …" or "Nobody inside"). A tower's card is named for the tower and covers all eight flats.
2. **Only buildings:** no card over the ground, trees, benches or a resident's figure, over a panel's header, or while the pointer is over the journal, saved towns, settings or the place menu. Where one tower stands in front of another, the card is for the one you can see.
3. **Double-click:** double-clicking a building turns that panel into a free camera inside it, just like picking it from the place menu. For a tower it opens the first flat with someone in it, otherwise flat 1. A single click still only selects the panel, and clicking a resident still shows them in the inspector.

## Neon labels and zoom

1. **Labels:** residents' names, door signs and object names are pale text with a soft neon glow: pink for people, cyan for doors and violet for things. They are readable by day and by night, in Dark and Light themes.
2. **Zoom out:** in a free camera over the city, scroll out. It stops once the whole city fits in the panel and goes no further. Fully zoomed out, dragging does nothing. On an ultra-wide screen the space beside the city is empty: no building interiors show next to it.
3. **Zoom in:** scroll in. It goes much closer than before, to about six rows of tiles, and zooms towards the pointer. Dragging pans but never leaves the city's edge.
4. **Following:** a panel following someone outdoors can zoom out to the whole city and in to the same close view. Indoors the room still fits the panel.
