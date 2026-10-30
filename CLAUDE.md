# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 0. Repository Layout, Commands and Current State

Phases 0 (foundation), 1 (one resident living a basic day), 2 (local AI and memory) and 3 (social simulation: three residents, relationships, conversations) are in place. Phase 4 (full town) was delivered in three builds, all in place: 1 (ten residents, jobs, money, shops, fair AI queue), 2 (hobbies, health, ageing, death) and 3 (romance, community events, town history, soak tests). Phase 5 (observer interface) was delivered in three builds, all in place: 1 (camera panels and inspector), 2 (journal, timelines, search, notifications, export) and 3 (save slots, duplication, save export). On 29 October 2026 the owner renamed the game from The Tiny Town to **Tiny City** and changed the art to a neon cyberpunk look (`Documentation/ArtStyle.md`); every system stayed the same. On 30 October 2026 the owner made a **fresh start**: older saves no longer load (`SaveSerializer.Format` "tiny-city-save", version 1, `StarterTown.TownId` "town-tiny-city"; a save in the old "tiny-town-save" format is refused with a message), towns are kept in a new `Towns` folder, and nothing is copied from the old The Tiny Town data folder. Map and object ids such as `map-village` were kept, because the art's noise is seeded from them. The GitHub repository is `BigDaddy284/tiny-city`. Phase 6 (art and audio) is in progress in three builds: 1 (places: ground, buildings, trees, furniture, rooms, seasons, day/night light) and 2 (residents: fifteen looks, animations, mood icons) are in place; 3 (sound: music, ambience, effects, volume) is built and awaiting merge. The user chose to have all art and sound generated in code, with optional files that override it. Full build and test instructions, including the Unity licence secrets CI needs, are in `Documentation/BuildAndTest.md`.

- Unity **6000.3.27f1** (6.3 LTS), set in `ProjectSettings/ProjectVersion.txt`. Only `ProjectVersion.txt` and `Packages/manifest.json` are hand-written; Unity generates the other project settings, the `Assets/Scenes/Boot.unity` scene and `packages-lock.json` on first open. Commit generated files from a real editor run rather than writing Unity YAML by hand.
- Windows builds and Unity tests run only in GitHub Actions (`.github/workflows/build.yml`, GameCI on Ubuntu, Mono backend). The cloud coding container has no Unity editor or licence, so do not claim a Unity build or Unity test result unless a CI run actually reported it. Download builds from the run's `TinyCity-Windows-*` artifact.
- Assemblies:
  - `TinyCity.Simulation` (`Assets/Scripts/Simulation`): all game rules and data. `noEngineReferences: true`, no `UnityEngine` usage, C# 9. `TownSimulation.Tick()` advances one in-game minute deterministically: needs decay (`NeedRules`), then each resident's state machine (Idle → Travelling → Performing, or Waiting). `ActivityPlanner` is the built-in rule-based decision maker (urgent needs → bedtime → work shift → comfort → shopping → events → a daytime outing → a night out → leisure; `OutingChoice` sends residents from 10:00 to 19:00 by `Chance` per hour (likelier for the sociable and active, with at least £20 and 90 minutes before bed) to one of the city venues, favourites first, because otherwise their distance from most homes means they're hardly ever picked; bed and wake times are each resident's own, from `SleepSchedule`: by age plus a fixed personal shift of up to 30 minutes, earlier for morning shifts so they're up `GetReadyMinutes` before leaving, after work for late shifts; never stored) and is also the no-AI fallback for Phase 2. `Pathfinder` returns one step at a time from a cached BFS distance field, crossing maps through `Portal`s (each target also caches every door's cost to reach it, a backwards Dijkstra over the doors, so a distance only looks at the doors on the walker's map; `NavigationTests` checks it against a plain search over every tile), so no path is ever stored or saved. `StarterTown` (Content) defines the 64×40 city (the first 43×27 town in its top-left corner), sixteen cottages (`map-cottage-1`–`16`), two six-floor apartment towers along the back edge (Skyline and Aurora, `BuildingArt.TowerHeight`) with eight flats each (`map-flat-a1`–`a8`, `-b1`–`b8`, `StarterTown.FlatMapPrefix`, each laid out like a cottage, with its own door: four along the tower's front and four up its right side, so `BuildingPlan.Doors` lists them all), so 32 homes in all, 17 of them empty at the start (empty homes are private like any other), the cue club (pool and snooker, `map-cue-club`/`biz-cue-club`), Strike Lanes bowling (`map-bowling`), the Pixel Arcade (`map-arcade`) and the Pulse Gym (`map-gym`, with a shower), each with one job (`StarterTown.VenueJobs`, whose shifts are the venue's hours, listed as the world's `TownWorld.JobOpenings`); a job no living resident holds is a vacancy (`TownSimulation.Jobs.cs`: `Vacancies`, and at 09:00 `FillVacancies` hires jobseekers, adults on jobseeker's allowance, in id order, stopping the allowance and writing a notable `NewJob` history entry), an outdoor market square (five `MarketStall`s, `BrowseMarket`, and a Saturday-morning "street-market" event at `MarketSquareId`), a park with a `Fountain` (four seats on its rim, used by `WatchTown`) and more benches, the business interiors (the neon bar, `map-bar`/`biz-bar`, open 18:00–23:30 Wednesday to Sunday with Felix as bar host), the businesses and the fifteen residents (`res-0001`–`res-0015`, data-driven `Designs`; the last five, Marcus, Lola, Danny and Ruby at the gym, lanes, arcade and cue club and Walter, retired, came with the bigger city and have their own looks and fingerprint in `ArtStyleTests`); its ids are stable and referenced by saves. Furniture blocks walking (`WorldObject.BlocksPassing`, all types except garden plots and exercise, fishing and gathering spots; `TownWorld.IsBlocked`): the `Pathfinder` never routes through it, and a resident steps onto it only as the destination, so layouts must not put furniture right inside a door or close off a corner (`NavigationTests.StarterTown_FurnitureNeverBlocksTheWay`). `ActivityPlanner.IsAvailableTo` keeps other people's homes private (business interiors are public) and respects `WorldObject.Capacity` (café tables seat two, one per chair), and the planner counts each current user as `CrowdedSteps` extra steps so people spread over tables and benches; `ActivityPlanner.CanDo` also asks `TownEconomy.CanAfford`.
  - `Economy`: money is `long` pence (`Money.Format`). `Job` has a `WorkDays` bitmask (`Job.Weekdays`, `Job.EveryDay`, bit = `Weekday`) and `HourlyWagePence`; residents have `MoneyPence`, `Groceries`, `WeeklyRentPence`, `WeeklyIncomePence`/`IncomeLabel` and `UnpaidWorkMinutes`. `TownEconomy` (on `TownSimulation.Economy`) holds `BusinessState` (stock, takings) for each `Business` (opening hours and days, `ShopItem`s; each has its own interior map) and the capped `Ledger` (`txn-000001` ids). Wages are paid when a shift ends for minutes actually worked; Monday 07:00 settles income then rent (money may go negative, purchases can't); businesses restock at opening; `Pay` runs as a paid activity starts. Eat (`UsesGroceries`) needs groceries; `Snack` is always free so hunger is never blocked; `BuyGroceries` is an `Errand` chosen when food runs low. A business is open (`TownEconomy.IsOpen`) only within its hours while one of its staff (a resident whose job's work object is on its map) is there, alive and on shift (`ActivityPlanner.IsWorkTime`); if its only staff have died it stays shut, and one nobody in town works at keeps to the clock (single-resident test towns). The inspector says "closed, nobody at work".
  - `Health`: `HealthRules` holds every number (28-day years via `DaysPerYear`, `BirthdayCycleDay`, `MaxHealthFor(age)`, neglect drain, recovery, daily illness risk, old-age chance from 80). Luck comes only from `Chance.Roll(id, day, salt)` (FNV hash, never `Random` or `GetHashCode`), so runs replay exactly. `IllnessCatalog`: cold (can work), flu (can't work, keeps to themselves), pneumonia (serious: flu turns into it below `FrailHealth`; recovery starts only after `SeeDoctor` at the open clinic). `TownSimulation.Life.cs` runs birthdays at 00:00, old age at 04:00, illness at 05:00, `StepHealth` each minute, `Die` (keeps the resident with `DiedAt`/`CauseOfDeath`, memories for everyone else). `TownSimulation.Hobbies.cs`: hobby activities (`ActivityDefinition.Hobby`, only for residents with that hobby) and `Shared` ones give company, a once-a-day memory per pair and an invitation (`InviteToHobby`) to a free neighbour with the same hobby (partner first, then nearest); `Coffee` and `CafeMeal` are shared outings, offered only to a partner or good friend (`MaxOutingDistance`).
  - Romance (`Social/Romance.cs`, `TownSimulation.Romance.cs`): `Relationship.Romance` (0–100) and `BrokeUpMinute`; `RelationshipBook.Partnerships` (`Partnership`, `RomanceStage` Dating/Committed, `PartnerOf`). `RomanceRules` holds every number: `Compatible` (living adults, both `Resident.OpenToRomance`, age gap ≤ 12), a fixed per-direction `Spark` from `Chance` (≈40% of pairs, sets a `FeelingsCeiling`), growth from warm/playful lines heard once friends, loss from disagreements, daily fade when apart, daily quarrel chance for couples, grudges (dislike) fading daily. `UpdateCouple` (after each chat and at midnight) starts dating, commits after 14 days, breaks up, or reconciles after 7 days; `Widow` on death. `TownSimulation.RomanceEnabled` comes from `settings.json` `simulation.romance`.
  - Community (`Community/CommunityEvent.cs`, `TownSimulation.Community.cs`): `CommunityCalendar` on `TownWorld.Calendar` (weekly events by weekday mask, yearly by `YearDay`, which replaces weekly ones that day) held at `ObjectType.GatheringSpot`s. Nightlife: `BarDrink` (paid, `BarTable`) and `Dance` (free, needs the bar open, nine `DanceFloor` squares in a 3 × 3 floor, non-blocking, two dancers each) are shared, and the planner's `NightOutChoice` sends residents from 19:00 by `Chance` per evening hour (likelier for the sociable and young, dancing for the active), with money to spare and an hour before bed; a Friday club night is held at the bar's `GatheringSpot` (drawn as a DJ booth: `FurnitureArt.Render` takes the `PlaceKind`). `ActivityCatalog.AttendEvent` is only doable at the current event's spot (`ActivityPlanner.EventAt`), lasts until it ends, is offered to the AI in `FreeTimeOptions`, and the planner's `EventChoice` sends residents by `FanciesGoing` (Chance × sociability). One memory per attendee per occurrence (tag `OccurrenceId`); at the end a `Gathering` entry goes in the `TownHistory` (`History/TownHistory.cs`, `evt-000001` ids, capped), which also records couples, break-ups, deaths, birthdays and life stages, illness and recovery, first meetings and new good friendships (each pair once). `TownEventKind` is saved by name, so add new kinds at the end. `Journal` (topics, `IsNotable` for notifications, `Search` newest first with every word matching summary, names or date) and `LifeStory` (`Timeline` = town records + important memories not already covered + AI reflections marked by `TimelineSource`; `Write`/`WriteTownHistory` plain text for export) only read the history. `CommunityEventsEnabled` comes from `simulation.communityEvents`.
  - Newcomers and families (`Residents/PopulationRules.cs` holds every number, `TownSimulation.Newcomers.cs`, `TownSimulation.Families.cs`, `Content/Newcomers.cs`, `Content/Births.cs`): never more than `MaxLiving` (50) alive. At most `MaxNewcomers` (10) people ever move in from out of town: from `FirstArrivalDay` a `Chance` roll at 10:00 (`ArrivalChance` 0.25) brings one, with the next `res-NNNN` id after the highest (so a future starter resident must not take `res-0016` or later), and a surname nobody in town has ever had (so a shared surname only comes from family), a look, personality, age 20–60, two hobbies, money and an income ("Works remotely" or "Jobseeker's allowance", no job, so staffed businesses keep their hours) rolled from that id; they appear on the path nearest the edge of the biggest outdoor map (41, 38) and walk to a free home (`FreeHomes`: an indoor map with a bed, not a business, where nobody alive lives), or, with none free, are homeless. `Resident.IsHomeless` (no `HomeMapId`/`BedObjectId`, no rent): they sleep on an empty bench (`ActivityCatalog.Sleep` takes `Bench`, only for the homeless, and nobody else sits on a bench someone sleeps on: `ActivityPlanner.SuitsHousing`), eat `Leftovers` at a market stall (homeless only) and don't shop; at 09:00 (`HousingMinute`) `HouseTheHomeless` moves them into homes that came free. Babies: at 06:30 each committed couple who `CanHaveBaby` (both adults no older than `MaxParentAge` 50, one with a home, no baby already, under `MaxChildrenPerCouple` 3) has one at `BirthChance` 0.05; the baby (`Resident.BornDay`, `ParentIds`, the carer's surname and home) is a `LifeStage.Baby` until `WalkingAge` 3, skips the planner (`StepBaby`: needs kept met, location = `CarerOf`, a parent sharing the home), can't chat or fall ill; children age a year every `ChildDaysPerYear` (14) days, twice adult speed (`HealthRules.IsBirthday`), until `AdultAge` 18, so grow up in 252 days; minors (`IsMinor`) have no job, an earlier bedtime, pocket money from 8, share the home bed (`IsAvailableTo`: a bed in one's own home is never full), can't use `Business.AdultsOnly` places (the bar) and aren't `RomanceRules.Compatible`, nor is family (`IsFamilyOf`). At 18 `GrowingUp` moves them into a free home or onto a bench. History kinds `MovedIn`, `Housed`, `Homeless`, `Born`, `MovedOut` (all notable). `NewcomersEnabled` and `BabiesEnabled` are off by default so test towns keep their residents; the game sets them from `simulation.newcomers` and `simulation.babies`.
  - `Social`: `Relationship` (directional friendship/trust/respect/dislike, 0–100) in a `RelationshipBook`; `ConversationSession` is a chat in progress; `SocialRules` holds every number (line counts, cooldowns, meeting distance, tone effects, reasons to leave; time spent waiting for an AI line is not counted towards a chat's length, and `AiService.LineWaitSeconds` gives up on a line in real time). The simulation starts chats by chance meetings (`StartChanceMeetings`) or by a resident seeking a partner (`ResidentState.Travelling` with `TargetResidentId`), runs them in `StepConversations` (residents are `ResidentState.Talking`), and `EndConversation` records history, applies `SocialRules.ApplyConversation` and creates shared memories. Lines come from the AI via `ApplyConversationLine` (checked against the session's `LineRequestId` and speaker) or from deterministic `SmallTalk`.
  - `Memory`: `MemoryStore` (validated, capped per resident, `Recall` scores importance + recency + tag matches) and `ConversationHistory` (remarks and finished chats, capped at `MaxRecords` 1500, about a week of a busy town, which keeps saves near 4.5 MB). Ids like `mem-000001` are never reused.
  - `AI`: the engine-free side of Ollama. `AiService` checks `/api/tags`, preloads the model, sets `TownSimulation.AiDecisionsEnabled` only while connected, and enqueues free-time decisions, conversation lines (`DialoguePrompt`, priority `Dialogue`) and bedtime reflections. `AiRequestQueue` is the single queue: up to `MaxConcurrent` requests at a time (settings.json `parallelRequests`, default 1, to match Ollama's `OLLAMA_NUM_PARALLEL`), priority with ageing, a fading penalty for a resident served in the last `RecentlyServedSeconds` (fairness), dedupe by key, timeout (after `AdaptiveTimeoutAfterCalls` answers, `CurrentTimeoutSeconds` is 6× the average answer time, at least 10 s and never more than the setting, so a lost reply from a fast model doesn't stall everyone), bounded attempts, stale-request cancellation, stats. `OllamaProtocol` builds and reads `/api/chat` JSON (structured-output `format` schema); `DecisionPrompt`/`ReflectionPrompt` build prompts and parse answers. Networking goes through `IAiTransport` (Unity: `UnityAiTransport`; tests: `FakeAiTransport`).
  - AI rules: only free-time choices (`PlanChoice.IsDiscretionary`) go to the AI; the resident waits in `ResidentState.Deciding` until an answer, a give-up or `DecisionWaitMinutes` (set by `AiService.SetGameSpeed`, which `GameController.SetSpeed` calls: the longer of settings `decisionWaitMinutes` and `decisionWaitSeconds` real seconds at the current speed), and urgent needs, bedtime and work still preempt. `TownSimulation.ApplyDecision` accepts only a pair from `ActivityPlanner.FreeTimeOptions` for the current `PendingDecisionId`. Deciding residents are saved as Idle, so no unfinished request is persisted. One answer is a plan: the activity for now plus a `then` list of up to `MaxPlanSteps` − 1 more (`DecisionReply.Then`, `PlanStep`), kept on `Resident.PlannedSteps` only if each action is among the options when the plan is made. When the resident is next free and the routine's choice is discretionary, `TownSimulation.TryFollowPlan` starts the next step without a request if it is still a free-time option right then (same target, or the nearest one for the same activity; chats need the same person), skipping steps that no longer fit; work, bedtime, naps, turning the AI off, death and `PlanMinutes` (180) drop the rest. Plans are never saved (after a load the resident asks again), and `PlanStepsTaken` is a diagnostic count for the stress tool.
  - `Persistence`: `SaveSerializer` writes versioned JSON (`CurrentVersion`) through the in-house `Json` reader/writer (Unity's `JsonUtility` is not available to the engine-free assembly). Loading validates, repairs what it can with warnings (invalid position → back to bed, unknown activity → idle) and rejects what it can't. Version 1 is the fresh start's layout (residents with money, health, hobbies, `movedInDay`, `bornDay` and `parents`, a home that may be missing; memories; conversations and chats in progress; relationships and `partnerships`; `economy`; `history`). There are no older layouts to upgrade. Bump the version and add an upgrade step whenever the layout changes. A save → load → continue must match an uninterrupted run exactly; `PersistenceTests.LoadedGame_ContinuesExactlyAsIfNeverSaved` enforces this, and `SoakTests` runs 168 days with weekly save/reload against an uninterrupted run (about 2 minutes under .NET and 5 in Unity, so it carries a `[Timeout]` above Unity's 3-minute default) plus two weeks at 4x against a slow, unreliable fake AI.
  - `TinyCity.Art` (`Assets/Scripts/Art`): engine-free (`noEngineReferences`, references only `TinyCity.Simulation`), draws every picture as a `PixelCanvas` of `Rgba` (row 0 at the top; `AnchorX`/`AnchorY` is the pixel on the centre of the picture's anchor tile). `IsoPainter` projects (u, v in tiles, z in pixels) to pixels with the game's 64 × 32 tile (`px = (u - v) * 32`, `py = (u + v - 1) * 16 - z` from the anchor) and fills faces by inverse-mapping each pixel to face coordinates. Luck is `Noise` (FNV hashes), never `Random`. `Palette.For(Season)` holds seasonal outdoor colours (`Season`/`Seasons` live in `Simulation/Time`: four 7-day seasons per 28-day year). `GroundArt` paints a whole map as one canvas (anchor = tile (0, 0) centre) on a sub-tile grid whose edges wander outdoors; `NatureArt` trees (some pines); `BuildingArt.Find` turns each House-tile block plus its door into a `BuildingPlan` (style from `PlaceStyle.Of` the interior), `Render` gives a body and a lit-window `glow` canvas, and `Strips` cuts them into 32-pixel columns each sorted by its front-most footprint tile (`FrontTile`), which is what lets residents walk behind and in front of buildings; `InteriorArt` draws doll's-house walls (back walls `BackHeight`, front walls `FrontHeight`, inner walls half height) per wall/door tile; `FurnitureArt` has a picture for every `ObjectType` (add one when adding a type; `ArtTests` checks). `MapArt.Build` returns the ground plus `ArtPiece`s (anchor tile, sort tile, `ArtLayer` Solid = 1 / Glow = 2) and asks an optional replacement function by `ArtRequest.Key` (`tree`, `pine`, `object-<type>`, `building-<interior map id>`, `ground-<map id>`, each tried first with `-<season>`); `MapArt.AnchorFor` places replacements, and `PngReader` decodes them (8-bit, non-interlaced; the project has no `com.unity.modules.imageconversion`, so `Texture2D.LoadImage` does not compile in CI). Residents: `ResidentLook.For(id, age)` (a fixed look per starter resident, otherwise built from the id, grey from 65, `Shrink` rows shorter as a child or teenager and `IsBaby` a bundle drawn in the carer's arms by `TownView.PlaceBaby`; techwear with neon `Trim` piping, optional `Visor`, `HairTips` and long `Coat`, approved 29 October), `ResidentPose` (`ResidentAction` × `Stance` × `Facing` × frame; `Choose` reads the action from the resident's state and activity, `Placement.For(ObjectType, seat)` (one place per person for shared furniture; `Behind` places are drawn under the furniture's layer, and the view-only `Seating` keeps each user's seat until they leave) says where on the tile and at what seat height someone using that furniture is drawn and which way they face), `ResidentArt.Render` (32 × 56 canvas, anchor at the feet or seat; lying is drawn on the bed's tile) and `MoodArt.Icon` for each `Mood` (`Simulation/Residents/Mood.cs`: `Moods.Of` derives the mood from needs, illness, sleep and a partner in the same chat; never stored or saved). `Tools/ArtPreview` `--residents <png>` draws every look and pose (`Documentation/Images/residents.png`) and `--scene <folder> <minutes>` runs a new town and draws each map with its residents. `Lighting.At` gives a `Daylight` (tint multiplied into sprites, window glow 0–1) from minute of day and season (`SunTimes`), lamplight indoors. `Tools/ArtPreview` (`dotnet run` in that folder, output folder as the argument) renders every map by day and night to PNG so the art can be inspected without Unity; look at its output after changing art. **The art style and palette are locked by the owner** (`Documentation/ArtStyle.md`): every colour is a named field in `Palette.cs` (no `Rgba.Hex`/`new Rgba` anywhere else in `Art`), and `ArtStyleTests` fingerprints the palette, the style constants and every drawn picture. New art (residents, effects) must follow the guide and use palette colours; changing a colour, a rule or an existing picture needs the owner's approval first, then update `ArtStyle.md` (regenerate the table and `Documentation/Images/palette.png` with `dotnet run -- --palette <png>` in `Tools/ArtPreview`) and the fingerprints in the same commit.
  - `TinyCity.Sound` (`Assets/Scripts/Sound`): engine-free (`noEngineReferences`, references only `TinyCity.Simulation`), makes every sound as a mono `SoundBuffer` at 22050 Hz. `Synth` holds the building blocks (MIDI notes, wavetables, envelopes, one-pole filters, echo, hashed noise, never `Random`), `WrapTail` (music: what rings past the end is added onto the start) and `Crossfade` (ambience) make seamless loops. `Music.Compose` (Day and Night pieces, 16 bars of pad, bass, plucked arpeggio with echo, day hats), `Ambience.Make` (CityDay, CityNight, Indoors) and `SoundEffects.Make` (footsteps ×4, door, typing, till, cooking, drink, chess, piano, splash, digging, chime). `SoundLibrary` lists every key (`music-day`, `ambience-night`, `sfx-footstep-2`, …), generates it, or `Load`s a replacement WAV (`Wav` reads 8/16/24-bit PCM and float, any channels, and writes 16-bit mono; documented in `Documentation/SoundOverrides.md`). `SoundDirector.Update` runs after each tick and returns `SoundCue`s: a footstep per step, a door on a map change, and an activity's sound when it starts and every `RepeatMinutes` (`EffectFor`); it only reads the town and is never saved. `Tools/ArtPreview` `--sounds <folder>` writes every sound as WAV.
  - `TinyCity.Runtime` (`Assets/Scripts/Runtime`): Unity glue only. `Bootstrap` is created by `[RuntimeInitializeOnLoadMethod]` (the boot scene stays empty, nothing references scripts by GUID) and adds `GameController`, which drives the clock (1 game minute per real second at 1x), autosaves every 6 game hours and on exit via `SaveStore` (temp file + `.bak`), and hands drawing to `TownView` (every map drawn at once, side by side at its own world origin, via one `MapView` each, plus the resident sprites; `MapView` turns `MapArt` into sprites through `SpriteFactory` (point filter, `FullRect`, textures released on redraw) with `ArtOverrides` PNGs from `StreamingAssets/Art` (documented in `Documentation/ArtOverrides.md`); sorting order is `IsoGrid.SortingOrder(x, y, layer)` with the ground at 0, `ArtLayer`s 1–2 and residents at `IsoGrid.ResidentLayer` 3; `TownView.ApplyTime` redraws when the season changes and applies `Lighting` per map, residents taking their map's tint; `TownView.UpdateResidents` picks each resident's pose, facing (step direction, chat partner or furniture) and frame (walking with the tick's progress, the rest with real time), caches their sprites in its own `SpriteFactory` and shows a `MoodIcons` child renderer at sorting order 32000; `PlaceholderArt` now only makes IMGUI backgrounds), `CameraPanels` (1–10 `CameraPanel`s, each its own orthographic camera with `Camera.rect` from a grid `Layout`; panel 0 borrows `Camera.main`; a panel follows a resident's sprite, so interior/exterior moves need no special handling, or is a free camera over one map; names, door signs and speech bubbles are drawn per panel; with the pointer over a building outdoors a card shows the engine-free `PlaceInfo` (`Simulation/World`: name, open or who lives there, who is inside, for every place behind the building's doors), found by `TownView.BuildingAt` → `MapArt.BuildingAt` (opaque pixel of the building's whole picture, `MapArt.Buildings`, front-most strip wins), and a double-click opens `PlaceInfo.EnterMapId` in that panel; the layout is kept in `PlayerPrefs`) `Inspector` (tabbed IMGUI panel; the top row is Town journal, −/+ and Watch everyone with the panel count on the right, then a box with how many people live in the city (styled like the panel count), a People list and a find-a-person box built on the engine-free `PeopleList` (`Simulation/Residents`: `Of` with `PeopleFilter`/`PeopleOrder`, `Search` best match first); AI-written text uses the `GuiStyles.AiNote` style so it is never mistaken for game fact), `SettingsWindow` (the Settings button beside the title in `Inspector.DrawTownControls`; AI on/off via `AiService.SetEnabled`, installed models via `AiService.RefreshModelList`/`InstalledModels` (works while the AI is off) and `UseModel`, volumes, `ScreenSettings`, theme; choices are `PlayerPreferences` (engine-free, `Simulation/Configuration`) in `persistentDataPath/player-settings.json` via `PlayerSettingsStore`, and override settings.json's `enabled`/`model` through `PlayerPreferences.ApplyTo` before `AiService` is made). Interface colours come only from `UiPalette` (Dark default and Light, copied value for value from the owner's Stills & Reels `src/Theme.cs`, the same palette StackSpace uses; keep the three in step, and there is deliberately no Auto option); `UiTheme` holds the choice and builds one `UiSkin` and `GuiStyles` per palette, set as `GUI.skin` at the start of `GameController.OnGUI`. Labels drawn on the town (`GuiStyles.Name`, `Sign`) are neon whatever the theme (`CameraPanels.Label`: cream text, dark shadow and a glow of `GuiStyles.PersonGlow`/`DoorGlow`/`ThingGlow`, the owner's pick of 30 October). A camera never shows more than its whole map and stays over it (`CameraPanel.Aim`/`Keep`, so a free camera zoomed fully out can't be dragged), zooms in to `MinHalfHeight` at the pointer (`ZoomAt`), and maps sit `TownView.MapGap` (200 units) apart so another map never shows beside it, even on an ultra-wide screen. The People list's filter row has an Expand toggle (`Inspector.DrawPeopleGrid`: the side panel shows the controls and everyone in a two-column grid, in the chosen order, first name A–Z by default, instead of the resident's tabs); "Hide ▶" beside Settings, or H, sets `GameController.SidebarHidden` (kept in `PlayerPrefs`), which gives `CameraArea` the whole width and puts a "◀ Panel" button first in the top-right camera panel's header (`ShowsSidebarButton`). `JournalWindow` (J; search box, so `HandleKeys` ignores keys while `GUIUtility.keyboardControl` is set), `Notifications` (pop-ups for `Journal.IsNotable` events from `TownHistory.Since`, reset on load) `SoundPlayer` (added by `GameController`; makes every sound on a background `Task` at start-up, then `AudioClip.Create`s them; crossfades music by `Lighting` night and ambience by the active panel's map, plays the director's cues on that map with distance falloff, at most 4 a frame, and a chime for new notifications; volumes from `PlayerPreferences.Volume`; needs `com.unity.modules.audio`), `WindowFrame` (Windows only: DWM dark mode and Windows 11 caption, text and border colours from `UiPalette` `Window`/`Text`/`BarBorder`, applied with the theme and on focus), `ExportStore` (text files and save copies under `persistentDataPath/Exports`) and `SavesWindow` (S). Save slots: each town is `Saves/<slot>.json` (+ `.bak`), slot ids `town`, `town-2`, … (`Persistence/SaveSlots.cs`: `SaveSlotIndex.IsValidSlotId`/`NewSlotId`, names in `Saves/slots.json` so a copied file is the town exactly; `SaveSummary.Read` gives the list's date and residents without a full load). `GameController.CurrentSlot` (remembered in `PlayerPrefs`) receives autosaves, F5 and the exit save; `LoadSlot`, `NewTownSlot` and copying the current town save it first; the town being played can't be deleted. It pumps `AiService.Update` every frame with real time. `BuildScript` sets `insecureHttpOption = AlwaysAllowed` so the player can reach Ollama over http. Keyboard input uses IMGUI events, not the Input Manager or Input System.
  - `TinyCity.Editor` (`Assets/Scripts/Editor`): `BuildScript.BuildWindows`, the CI build entry point.
  - Tests: `Assets/Tests/EditMode` (plain NUnit, must not use `UnityEngine`, also compiled by `DotNetTests/` together with the Simulation and Art sources) and `Assets/Tests/PlayMode` (Unity-only). Unity bundles NUnit 3.5, so avoid newer constraint features (e.g. `Has.Count` on arrays fails there; prefer `List<T>` or LINQ + `Is.True`). Under .NET the tests compile into the same assembly as the simulation, but in Unity they are separate, so `internal` members need the `InternalsVisibleTo` in `Assets/Scripts/Simulation/AssemblyInfo.cs`.
- Player settings live in `Assets/StreamingAssets/Configuration/settings.json` (Ollama `enabled`, `endpoint`, `model`, `timeoutSeconds`, `maxRetries`, `decisionWaitMinutes`, `decisionWaitSeconds`, `parallelRequests`; `simulation.romance`, `simulation.communityEvents`, `simulation.newcomers` and `simulation.babies`, read through `GameSettings.Simulation` so older files without the section get the defaults); `GameSettings.ValidOrDefault` falls back to defaults on invalid input. Player-facing AI setup is in `Documentation/OllamaSetup.md`. Ollama cannot be installed in the cloud container (ollama.com is blocked), so real-model checks are manual on the user's PC.
- `Tools/StressTest` (`dotnet run -c Release -- <report.md> [days] [aiDays] [bigDays] [districts] [parallel] [waitSeconds]` in that folder; the stand-in Ollama gets `parallel` slots, each reply 30% slower per other request beside it, an assumed figure) runs a long town with autosaves, the game loop at every speed against a slow, unreliable stand-in Ollama (GPU-like and CPU-like reply times) and an Ollama outage, then a big city of `districts` side-by-side copies of the starter town (`BigCity.cs`, ten residents each), and writes the numbers as Markdown; the latest results are in `Documentation/StressTest.md`. Its numbers are .NET; Unity's Mono is roughly 2–3 times slower. Tool `.csproj` files are committed (`!/Tools/*/*.csproj` in `.gitignore`).
- When adding files under `Assets/`, also add a `.meta` file with a new random GUID so CI and the editor agree on asset identity.

Commands runnable in the cloud container (install the SDK first with `sudo apt-get install -y dotnet-sdk-8.0` if `dotnet` is missing):

```bash
dotnet test DotNetTests                                          # Simulation + Art + Edit Mode tests
dotnet test DotNetTests --filter "FullyQualifiedName~GameSettingsTests.InvalidEndpoint_IsRejected"   # single test
```

# Tiny City

## 1. Your Role

You are the lead engineer and implementation agent for **Tiny City**, a Windows desktop life-simulation game built with Unity 6 and C#.

You are responsible for:
- Inspecting the existing repository before changing it.
- Implementing working game features.
- Maintaining clean, modular architecture.
- Writing and running automated tests where the environment permits.
- Preparing verified builds and reporting actual results.
- Maintaining technical documentation.
- Working incrementally through the project's GDD.
- Preserving existing work and preventing avoidable regressions.

This is a real game-development project. Do not stop at suggestions, mockups or code examples when the cloud environment permits you to implement and verify the feature directly.

## 2. Authoritative Documentation

Read these documents before substantial implementation work:

- `GameDesignDocument.md` — authoritative game design and scope.
- `CLAUDE.md` — persistent project instructions.
- Relevant architecture, testing and development notes in the repository.

If the GDD is moved into `Documentation/`, update references accordingly.

The GDD defines the intended game. This file defines how to implement it.

If an important requirement is ambiguous and blocks safe implementation, ask one focused question. Otherwise, make a conservative, documented decision and continue.

Do not invent additional major features or expand the scope without approval.

## 3. Core Game Vision

Tiny City is a cosy isometric 2D pixel-art life simulation. The full initial town contains ten autonomous residents with needs, personalities, routines, jobs, finances, relationships, memories and life histories.

The player observes the town rather than directly controlling residents. The interface eventually supports one to ten camera panels, resident inspection, a town journal, individual timelines, save management and adjustable simulation speed.

The game uses a locally installed Ollama service, initially configured for Qwen2.5 7B, for contextual decisions and natural dialogue.

The simulation owns the rules and persistent truth of the world. The LLM proposes decisions and generates language but cannot bypass game rules.

The long-term vision includes multiple towns, a larger city, family generations and deeper social simulation. These are future possibilities, not initial release requirements.

## 4. Technology and Deployment

- Unity 6.
- C#.
- Windows desktop target.
- Isometric 2D tile-based world.
- GitHub repository as the source of truth.
- Claude Code on the web as the chosen cloud development environment.
- Ollama HTTP API for local AI.
- Qwen2.5 7B as the initial model configuration.

Do not bundle the Ollama application or model.

Do not introduce mandatory paid AI APIs or cloud inference dependencies.

Keep the Ollama endpoint and model configurable.

Before relying on a Unity feature, package, editor command or build capability, verify that it is available and compatible with the actual project.

## 5. Cloud Environment Rules

You are working through a cloud coding workflow. Do not assume that the environment has a complete Unity editor, a Windows desktop, a valid Unity licence, the correct build modules or access to the user's local computer.

At the start of work, establish:
- The actual repository state.
- The Unity project version.
- Available .NET or Unity-related tools.
- Whether Unity batch mode is available.
- Whether a valid Unity installation and licence are available.
- Whether a Windows build runner is available.
- Which tests can genuinely be executed in this environment.

Distinguish between:
1. Source inspection and static checks.
2. Automated tests actually executed.
3. Unity editor or batch-mode tests.
4. Windows builds actually produced.
5. Manual gameplay tests actually performed.

Never claim that an unexecuted test passed.

If a required build capability is missing:
- Continue with safe work that can be verified.
- Document the missing capability and its impact.
- Investigate supported GitHub Actions or other compatible cloud build options when appropriate.
- Do not fabricate build outputs or pretend a source-only check is equivalent to a playable build.

Use the correct Unity version and required build modules for a Windows target. Verify applicable Unity licensing and terms before configuring automated builds.

The final delivery must include a verified Windows build when the necessary environment and permissions are available. If it is not yet possible, state the exact blocker and provide reproducible next steps.

## 6. GitHub and Repository Safety

The GitHub repository is the source of truth.

Before making changes:
1. Inspect the current branch and repository status.
2. Review existing project files and documentation.
3. Identify uncommitted changes and existing user work.
4. Avoid destructive commands and unnecessary rewrites.
5. Make a small, coherent implementation plan.

Use focused commits or reviewable pull requests when supported by the cloud environment.

Do not overwrite unrelated changes.

Do not commit credentials, API keys, personal paths, private data, local save files, generated caches or large build outputs unnecessarily.

Do not force-push, rewrite shared history, delete branches or discard changes without explicit authorisation.

Keep `.gitignore` appropriate for Unity, C#, IDE files, local settings, generated caches and build artefacts. Do not ignore source assets or project files required to build the game.

If repository permissions prevent a push, pull request or other action, report the limitation rather than claiming it succeeded.

## 7. Development Strategy: Playable Vertical Slices

Do not implement the entire game in one pass.

Work through these phases in order.

### Phase 0 — Foundation
- Inspect the repository and existing Unity project.
- Verify available tooling and build capabilities.
- Establish project structure, documentation, configuration and tests.
- Create or verify a minimal boot scene.
- Verify the available build and test process.

Do not proceed until the foundation is checked.

### Phase 1 — One Resident
- One resident.
- One small exterior map and one home interior.
- Simulation clock.
- Basic needs.
- Daily routine.
- Walking and valid map transitions.
- Eating, sleeping and scheduled activity.
- Basic resident inspection.
- Save/load.
- Safe built-in behaviour without Ollama.

Use placeholder art.

### Phase 2 — Local AI and Memory
- Configurable Ollama client.
- Central AI request queue.
- Structured action proposals.
- Prompt construction.
- Response parsing and validation.
- Bounded retries and timeouts.
- Safe fallback behaviour.
- Basic dialogue.
- Persistent memories and conversation history.
- Save/load tests including memory.

### Phase 3 — Social Simulation
- Two or three residents.
- Structured relationships.
- Multi-turn conversations.
- Shared memories.
- Social needs and interruptions.
- Persistent social outcomes.

### Phase 4 — Full Town
- Ten residents.
- Jobs, wages and finances.
- Businesses and shopping.
- Hobbies.
- Health and aging.
- Optional romance.
- Community events.
- Prioritised AI requests.

### Phase 5 — Observer Interface
- Flexible one-to-ten camera panels.
- Resident inspector.
- Town journal.
- Individual resident timelines.
- Notifications and search.
- Save slots, duplication and export.

### Phase 6 — Art and Audio
- Replace placeholder art with coherent isometric pixel art.
- Character animation.
- Environment and activity feedback.
- Music and ambience.
- Seasonal visuals.

### Phase 7 — Release
- Windows build and packaging.
- Setup documentation.
- Configuration and troubleshooting.
- Save recovery and migration tests.
- Long-running simulation and performance checks.
- Release notes.

Complete and verify each phase before advancing. Do not skip ahead because later features seem more exciting.

## 8. Simulation Integrity

The game owns simulation truth.

The following must be controlled by deterministic game logic:
- Needs and need decay.
- Health and illness.
- Aging and death.
- Movement and pathfinding.
- Map transitions.
- Finances and transactions.
- Job schedules.
- Activity validity.
- Relationship data changes.
- Save/load and persistent records.

The AI may propose actions, generate dialogue and influence validated outcomes.

Never let an LLM directly execute arbitrary code or change unrestricted game state.

Every AI action must be checked against:
- Allowed action types.
- Resident identity and life status.
- Current needs and activity.
- Valid target IDs.
- Current map and destination access.
- Relevant schedule and world constraints.

Reject invalid responses safely.

## 9. AI Request Queue

Use one central AI request service rather than ten continuously running AI loops.

Start with one active request at a time. Measure performance before changing concurrency.

Implement:
- Request IDs and resident IDs.
- Priority classes.
- Deduplication.
- Timeouts.
- Bounded retries.
- Stale-response rejection.
- Fair scheduling.
- Queue-length and latency diagnostics.
- Safe built-in fallback.

Prioritise urgent needs and important decisions over optional dialogue.

Prevent endless retries, duplicate requests, infinite conversations and starvation of residents.

If the simulation changes while a request is in progress, validate its result against the latest state before applying it.

Use a mock AI service or controlled test responses for cloud automated tests. Do not assume the cloud environment can reach the user's local Ollama server.

Do not claim real-model integration has passed unless a real request was actually tested against the configured service.

## 10. Memory and Persistent Data

Use stable unique IDs for towns, residents, relationships, memories, conversations, world maps and events.

Keep separate:
- Current simulation state.
- Structured persistent memories.
- Conversation records.
- Town event history.

Do not rely on an indefinitely growing conversation prompt for memory.

Retrieve relevant memories when constructing prompts. Record important events rather than every trivial line.

Keep persistent data separate from Unity scene objects and transient runtime state.

Use versioned save data and explicit migration logic. Preserve resident identity, relationships, memories and death records during upgrades.

Do not delete deceased residents from historical records. Deceased residents must not continue consuming AI or living-simulation resources.

## 11. Save and Load Requirements

Implement:
- Multiple independent save slots.
- Manual saving.
- Periodic autosaving.
- Saving before normal shutdown.
- Save duplication.
- Data validation.
- Save versioning and migrations.
- Recovery from incomplete or invalid data where feasible.

The simulation stops when the application closes. Do not implement offline progression in the initial release.

Do not persist incomplete AI requests as completed decisions. Reconcile or discard in-flight requests safely during loading.

Every new persistent data feature must have save/load round-trip tests.

## 12. Code Quality

- Use modular C# components and clear responsibilities.
- Avoid giant monolithic manager scripts.
- Avoid unnecessary frameworks and dependencies.
- Keep configuration and content separate from core simulation code.
- Reuse existing working code where appropriate.
- Validate external data and model responses.
- Handle nulls, missing references and malformed save data.
- Prevent unbounded loops, queues, retries and collections.
- Use stable IDs rather than names as keys.
- Avoid hard-coding exactly ten residents into foundational systems.
- Keep comments useful and accurate.
- Remove obsolete code only when safe and justified.
- Do not add fake implementations that merely appear complete.
- Do not suppress failures simply to make tests pass.

Do not create every class in the proposed architecture before it is needed. Implement the smallest coherent set of components for the current milestone.

## 13. Automated and Manual Testing

Each phase must have relevant automated tests and a manual playtesting checklist.

Test at least:
- Needs and simulation updates.
- Critical need priorities.
- Valid and invalid actions.
- Navigation and map transitions.
- Relationship changes.
- Memory creation and retrieval.
- Save/load round trips.
- Save migrations.
- Malformed AI responses.
- Ollama timeouts and disconnection.
- Fallback behaviour.
- Queue deduplication and priority.
- Deceased resident handling.

Use Unity Edit Mode and Play Mode tests where the project and available environment support them.

Run the tests that are actually available. Record exact outcomes and failures.

Never weaken or remove a test just to obtain a passing result.

For performance, measure simulation update time, AI latency, queue length and memory usage. Do not invent benchmark results or guarantee a simulation speed without evidence.

## 14. Art and Audio

Use simple placeholders during the early milestones.

The intended final style is cosy, polished isometric pixel art with:
- A small neon cyberpunk city (see `Documentation/ArtStyle.md`).
- Futuristic flat-roofed buildings with neon trim and signs.
- Streets, plazas and neon-lit trees.
- Ten distinguishable resident appearances.
- Readable walking, sitting, eating, sleeping, working, talking and emotional animations.
- Gentle synth music and city ambience.
- Footsteps, doors, work sounds and activity effects.

Do not delay simulation correctness for polished art. Replace placeholders in the dedicated art and polish phase while preserving working functionality.

## 15. Future Features

Keep future expansion possible through stable IDs, modular services and versioned data.

Do not implement the following in the initial release without explicit approval:
- Multiple connected towns.
- Central city.
- Resident migration.
- Family generations or children.
- Complex economy.
- Player building tools.
- Dedicated mod editor.

Do not overengineer for speculative future requirements. Establish the necessary boundaries and continue building the current playable milestone.

## 16. Documentation

Maintain:
- `GameDesignDocument.md`
- `CLAUDE.md`
- A development roadmap or milestone checklist.
- Architecture notes when they become useful.
- Build and test instructions.
- Ollama setup and troubleshooting instructions.
- Release notes for meaningful changes.

Document configuration, dependencies and limitations. Do not claim that a feature exists when it is only planned.

## 17. Required Report After Every Work Session

Provide a concise, factual report containing:

1. **Implemented:** what changed.
2. **Files changed:** important files and scenes.
3. **Build status:** whether a build actually ran and its result.
4. **Tests:** exact tests executed and their outcomes.
5. **Verification:** what was and was not verified.
6. **Known problems:** blockers and remaining limitations.
7. **Launch instructions:** how to run the latest verified build, if one exists.
8. **Next task:** the single recommended next implementation step.

Never report success based solely on code having been written.

If a cloud capability is unavailable, explain what is missing and what can still be verified.

## 18. User Interaction

The user prefers straightforward, step-by-step guidance for technical setup and troubleshooting.

Do substantial implementation work yourself when possible. Avoid asking questions whose answers are already in the GDD.

If user action is necessary, provide clear instructions and avoid overwhelming the user with multiple unrelated setup tasks.

Do not ask the user to manually create files or code that you can safely create and verify yourself.

Do not claim to have accessed the user's local computer, local Ollama service or Unity editor unless the relevant access actually exists.

## 19. First Task: Phase 0 Only

When starting work in the repository:

1. Read this file and the GDD.
2. Inspect the repository, current branch and existing changes.
3. Determine whether the repository contains a Unity project or is currently empty.
4. Identify the Unity version and relevant packages if present.
5. Check which build and test tools are actually available.
6. Report findings and the smallest safe Phase 0 plan.
7. Implement the minimal foundation.
8. Run available tests and builds.
9. Document actual results and limitations.
10. Stop at the Phase 0 checkpoint.

Do not begin Phase 1 until Phase 0 acceptance criteria have been checked.

Do not attempt to implement the whole game in the initial task.

**Primary objective: deliver a real, tested, playable game in small increments while preserving project integrity and honest verification.**