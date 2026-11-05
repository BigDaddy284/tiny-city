# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 0. Repository Layout, Commands and Current State

Phases 0 (foundation), 1 (one resident living a basic day), 2 (local AI and memory) and 3 (social simulation: three residents, relationships, conversations) are in place. Phase 4 (full town) was delivered in three builds, all in place: 1 (ten residents, jobs, money, shops, fair AI queue), 2 (hobbies, health, ageing, death) and 3 (romance, community events, town history, soak tests). Phase 5 (observer interface) was delivered in three builds, all in place: 1 (camera panels and inspector), 2 (journal, timelines, search, notifications, export) and 3 (save slots, duplication, save export). On 29 October 2026 the owner renamed the game from The Tiny Town to **Tiny City** and changed the art to a neon cyberpunk look (`Documentation/ArtStyle.md`); every system stayed the same. On 30 October 2026 the owner made a **fresh start**: older saves no longer load (`SaveSerializer.Format` "tiny-city-save", version 1, `StarterTown.TownId` "town-tiny-city"; a save in the old "tiny-town-save" format is refused with a message), towns are kept in a new `Towns` folder, and nothing is copied from the old The Tiny Town data folder. Map and object ids such as `map-village` were kept, because the art's noise is seeded from them. The GitHub repository is `BigDaddy284/tiny-city`. Phase 6 (art and audio) is in progress in three builds: 1 (places: ground, buildings, trees, furniture, rooms, seasons, day/night light) and 2 (residents: fifteen looks, animations, mood icons) are in place; 3 (sound: music, ambience, effects, volume) is built and awaiting merge. The user chose to have all art and sound generated in code, with optional files that override it. Full build and test instructions, including the Unity licence secrets CI needs, are in `Documentation/BuildAndTest.md`.

- **v1 and v2** (decided 4 November 2026): `main` is v1, the current game, and keeps getting fixes. **Tiny City 2** is developed on the long-lived `v2` branch, through pull requests into `v2`, and does not reach `main` until the owner says it is complete. Merge `main` into `v2` now and then so fixes carry over. On `v2` the product name is "Tiny City 2" (`BuildScript.ProductName`, so its saves and `PlayerPrefs` are separate from v1's) and the exe is `TinyCity2.exe`. The download always holds both versions, even after v2 ships: CI on `v2` builds v1 from `main` and v2 from the commit, and the `package` job puts them in one `TinyCity-Windows-*` artifact with a `Tiny City` folder and a `Tiny City 2` folder (the owner chose two folders and two .exe files over a launcher).
- **Tiny City 2 so far** (plan: the owner's "Tiny City 2: generative agents plan" doc; the LLM makes every choice, with no rule-based fallback, game time waits for the AI, residents must make their own food, bosses exist only if someone takes the role): saves are `SaveSerializer.Format` "tiny-city-2-save" version 1 and v1's "tiny-city-save" is refused (`FormatTinyCityOne`). Profiles: `Residents/ResidentProfile.cs` (`ResidentProfiles.Parse`/`Write`, documented in `Documentation/ResidentProfiles.md`) read `StreamingAssets/Configuration/residents.json` through the Runtime `ProfilesLoader` when a new town is made; the same file's `brief` and `roles` (owner, 5 November) are a `Residents/WorldBrief` (`ResidentProfiles.ParseBrief`, `ProfilesLoader.LoadBrief`, `WorldBrief.Default` when absent) on `TownSimulation.Brief` (saved as `brief`), put into every v2 prompt by `PromptText.Persona(r, sim)`; nobody is given a role: each resident picks one (or `TownSimulation.RoleNone`) as the day plan's `role` (`ApplyDayPlan(..., role)`, `Resident.Role`, saved as `role`), and `AppendPeople` shows it; each `RoleOption` has an `About` paragraph and `CanTax` (the shipped "tax man"): in a chat, a speaker whose role `WorldBrief.CanTax` may set `tax` (percent) on the listener, a `ConversationSession.TaxDemand` (not saved), and the listener's next line's `pay_tax` hands over that share of each food kind and material (`ApplyMoneyAndTaxInChat`, `PayTax`); anyone's `give_money` (pounds) moves money at once (`TransactionKind.Gift`); each profile's one-sentence `personality` becomes `Resident.Outlook` (saved as `outlook`), which replaces the trait text in `PromptText.Persona`; with `AiRunsEverything` nobody falls ill (the illness check is skipped), and the flat map's pond gives `drink-pond`/`wash-pond` and the trees `toilet-outdoors`; for small models (owner, 5 November: qwen2.5:0.5b had about half its answers refused), every chat body and the model load ask for `OllamaProtocol.ContextLength` (8192, `num_ctx`) because v2 prompts are about 2,500 tokens, and with `AiRunsEverything` `ApplyDecision` mends near misses instead of refusing them (a wrong action name whose target only one option uses, a target that doesn't go with the action: the nearest option for it, a missing or unusable ground spot: the nearest of `FreeGroundNear`), and `AiService.Summary` shows unusable answers and the last one's reason; `StarterTown.CreateNewGame(profiles)` starts only the listed starter ids with the profile's name, age, money, `Resident.About` (in `PromptText.Persona`) and `Resident.OwnsBusinessId`; `DefaultProfiles()` is what the shipped file holds. Memory stream: `MemoryRecord.ImportanceRated` (saved) and `Embedding` (not saved, made again after loading); `AiService.RequestMemoryWork` enqueues at `Flavour` priority one `ImportancePrompt` request per resident (up to 8 memories) and one `/api/embed` batch (`EmbedBatch` 16, `OllamaSettings.embeddingModel`, default `nomic-embed-text`, used only while `EmbeddingsAvailable`); `AiRequest.Url` sends a request somewhere other than the chat endpoint; `MemoryStore.Recall(..., focus)` adds up to `RelevanceWeight` for cosine closeness to `FocusOf` (the newest embedded memory) and is exactly v1's score without one. Build 3a: `TownSimulation.AiRunsEverything` (the game sets it in `GameController.SetSimulation`; off in tests unless set) sends every free resident to `Deciding` with no deadline (`StartNextActivity`), lets nothing the routine wants interrupt them, offers every possible activity (`ActivityPlanner.FreeTimeOptions(r, everything: true)`: eating, sleeping, the doctor when ill, and only their own job during its hours), re-asks instead of falling back (`AbandonDecision` gives a new `PendingDecisionId`, `UseSmallTalk` a new `LineRequestId`), and conversations wait for AI lines; `WaitingForAi` is true while anyone waits, and `GameController.HoldingForAi` holds the clock then, or while Ollama isn't connected, with `ActualSpeed` shown in the inspector; `Speeds` are 0, 1, 2, 4 (no 10x in v2, the owner's choice); `AiService.RequestMemoryWork` runs only when no non-`Flavour` request is queued or in flight. `ActivityPlanner`'s routine stays in the code for v1-style test towns, never for the game. Build 3b: day plans (`Residents/DayPlan.cs`, `AI/DayPlanPrompt.cs`): with `AiRunsEverything`, a Deciding resident without a plan for today (`TownSimulation.NeedsDayPlan`; a plan day runs from `DayPlan.DayStartHour` 04:00) first gets one `dayplan:<id>` request at `Decision` priority (`AiService.WaitingForDayPlan` holds their decision until it is answered or given up on for the day); `ApplyDayPlan` keeps up to `MaxEntries` hour + text entries in day order on `Resident.DayPlan` (not saved, so after loading they plan the rest of the day again), and `PromptText.AppendDayPlan` puts the plan and the part for now into every decision prompt, and the reflection prompt says what they planned. The inspector shows the plan in `AiNote` style. Build 4a, perceive and react (`TownSimulation.Perception.cs`, `Social/Perception.cs` `PerceptionRules`, `AI/ReactionPrompt.cs`): with `AiRunsEverything`, `Perceive` replaces `StartChanceMeetings` and `InviteToHobby` is skipped; each minute a resident who `CanReact` notices one `FreeToTalk` person within `SightDistance` on the same map whom they haven't been asked about for `ReactCooldownMinutes` (the cheap filter, `lastNoticed`, not saved), which adds a "Saw …" observation memory and sets `Resident.Reaction` (`PendingReaction`, not saved); `AiService` sends one `react:<id>` request at `Dialogue` priority, which does not hold the clock, and `ApplyReaction` either carries on or interrupts them to walk over for a chat (`StartChoice` with the partner, `GoalSource` Ai); the question is dropped after `ReactWaitMinutes` or once it no longer fits. In this mode `SeekPartner` checks `FreeToTalk` instead of v1's `CanBeApproached`, so indoor chats work too. Build 4b, insights (`TownSimulation.Insights.cs`, `AI/InsightPrompt.cs`): with `AiRunsEverything`, once the importance of a resident's memories since their newest `MemoryType.Insight` adds up to `InsightThreshold` (60), `AiService.RequestInsights` (checked once a game minute, retried after `InsightRetrySeconds`) sends one `insight:<id>` request at `Reflection` priority with the latest `InsightEvidenceCount` memories numbered 1, 2, 3; `ApplyInsights` keeps up to `MaxInsightsPerReflection` that cite at least one offered memory, as `Insight` memories (importance `InsightImportance`, tags `insight` plus the cited ids, so they are saved with the memories), and is stale once a newer insight exists. The day plan prompt lists the newest `DayPlanPrompt.InsightCount`, and `LifeStory` and the inspector show them as AI text. AI budget: "Saw …" observation memories keep the game's importance (rated at once in `Perceive`), `AiService.RequestMemoryWork` in v2 rates a resident's memories only once `RatingBatchMin` (4) wait or the oldest is `RatingMaxWaitMinutes` (6 game hours) old, and there is no per-resident limit on reactions (owner, 5 November: `ReactEveryMinutes` removed), only the per-pair `ReactCooldownMinutes`; `Tools/StressTest`'s eighth argument runs that many game days of the v2 city at 1x, 2x and 4x for each stand-in model (`V2.cs`: actual speed, clock held, requests per game day by kind, wait for a choice), results in `Documentation/StressTest.md`. Food, part 1 (`Economy/Food.cs`, `TownSimulation.Food.cs`): `TownSimulation.FoodMustBeMade` (set with `AiRunsEverything` in `GameController.SetSimulation`, off in tests unless set; it is `Food.MustBeMade`, the `FoodSupply` also given to `ActivityPlanner.Food`) removes v1's free food (`FoodRules.IsFreeFood`: Eat, Snack, Leftovers, BuyGroceries, CafeMeal, Pastry) and offers the new activities (`FoodRules.IsMadeFood`, never in v1): `plant-crops`, `water-crops` (once a day) and `harvest` at a `GardenPlot` or a crop patch planted wherever they chose (see building blocks; a `Crop` per plot or patch, ready after `WateredDaysToGrow` watered days, `HarvestPortions` vegetables), `catch-fish` at a `FishingSpot` (`CatchPortions`), `cook` at a kitchen (up to `CookPortions` raw into as many meals, at the end) and `eat-meal`/`eat-raw` (a portion taken as they start, `FoodSupply.TakeAtStart`); produce comes in `FinishFood` when the activity finishes. Each resident's `Resident.Food` is a `FoodStock` of `FoodBatch`es (`FoodKind` saved by name; used soonest to go off first; at most `MaxPortions`), thrown away at `SpoilMinute` (04:00) on its `GoesOffDay`; `Groceries` turn into cooked meals on the first tick (starting food, newcomers). Saves keep each resident's `food` and the town's `crops` (absent in older v2 saves, so no version bump). `PromptText.AppendFood` puts the resident's food, "nothing is free" and each garden plot's state into decision and day plan prompts. Food, part 2 (owner, 4 November: a shop exists only if a resident opens one, and the AI decides who sells): `run-stall` at a `MarketStall` (needs food, one seller per stall, `FoodSupply.SellerAt`) lets others `buy-food` there, one portion at a time (meals first) at the seller's own `Resident.StallPricePence` (saved as `stallPrice`; owner, 5 November: no fixed prices), which they name as the decision's `price` in pounds when they choose `run-stall` (`DecisionReply.PricePence`, `ApplyDecision(..., pricePence)`; none given keeps the last; 0 gives it away), paid through the ledger (`TransactionKind.Sale` for the seller, added at the end) in `StartFood`; the seller gets a memory of what they sold when the stall closes (`stallSales`, not saved). In a chat, `DialogueReply.GiveFood`/`PricePence` (`give_food`, `price` in pounds; no caps, only `FoodRules.LargestPricePence` as an overflow guard and the giver's food and buyer's money) puts a `ConversationSession.FoodOffer` (not saved) to the other person, whose next line's `accept_food` takes it (`ApplyFoodInChat`: food moves with `FoodStock.MoveTo`, meals first, and money only if they can pay; memories for both). The v1 shops' staff jobs are unchanged. Tools and crafting (`Economy/Crafting.cs`, `TownSimulation.Crafting.cs`; owner's pending choice, built as "tools make food quicker"; three tiers of each material, owner 4 November): each resident's `Resident.Belongings` holds `MaterialKind`s (tier 1 wood and stone, tier 2 hardwood and iron ore, tier 3 heartwood and crystal, `CraftingRules.TierOf`; up to `MaxMaterials` each) and one `ToolKind` tool of each kind with a tier (1 wooden, 2 stone, 3 iron, `MaxTier`) and uses left (`UsesFor(tier)` 10/20/30; a tool breaks on its last use), saved as `materials` and `tools` (with `tier`, default 1; absent in older v2 saves). The resource area in the village's open south-east corner has two `Woodland`s, two `RockPile`s (both new `ObjectType`s, non-blocking, with pictures in `FurnitureArt`) and a crafting bench (`StarterTown.CraftingBenchId`, a `Workbench`). With `FoodMustBeMade`, `CraftingRules.Gatherings` gives each `gather-*` activity a `Gathering` (material, tool, `MinTier`, `BaseYield`): wood 4 (wooden axe; by hand only from fallen branches, see below), hardwood 2 (stone axe), heartwood 1 (iron axe), stone 3 (wooden pickaxe), iron 2 (stone pickaxe), crystal 1 (iron pickaxe), each tier of tool above the minimum adding one (`YieldWith`); `CraftingRules.Recipes` gives each `craft-*` activity at any workbench a `Recipe` (wooden axe/pickaxe 3 wood, stone 2 wood + 3 stone, iron 2 hardwood + 3 iron, hoe and rod 2 wood), allowed only for a better tier than the one held (`CraftingRules.Allows`, from `FoodSupply.Allows`), paid at the end in `FinishCrafting`; a hoe adds `HoeExtraPortions` to a harvest and a rod `RodExtraPortions` to a catch (`FoodSupply.Finish`). Stone, heartwood and crystal have no use until building blocks. `PromptText.AppendFood` adds "Your things" and `CraftingRules.RecipesText()`; the inspector shows them. Building blocks (`Building/Construction.cs`, `TownSimulation.Building.cs`, `AI/SketchPrompt.cs`, `Art/BlockArt.cs`; owner's choice of 4 November: the AI draws its own small plan, the game checks it, then building follows it block by block; owner, 5 November: a block is a quarter of a tile, a building can be a home, and there are no plots: "they are to make houses where ever and however they want"). A plan is up to `GridSize` (16) rows of 16 letters, `CellsPerTile` (2) each way per tile, so at most `MaxSize` (8) tiles each way; `BuildPlan.Width`/`Depth` are the tiles its letters reach and `DoorTile` its first doorway. With `FoodMustBeMade`, `plan-building` and `plant-crops` are done on open ground (`ActivityCatalog.IsDoneOnGround`): the planner offers each once as a `FreeTimeOption.OnGround` option (target id `ground`, `IsGround`) on an outdoor map, the decision prompt adds `PromptText.AppendLand` (the map's squares, where things are, a few free spots near them) and asks for `x`, `y` (and `what` when planting; `DecisionReply.X`/`Y`/`What`), and `ApplyDecision` takes the spot only if `TownSimulation.GroundProblem` finds nothing wrong (outdoors, walkable, no object, no building planned over it, reachable; otherwise `InvalidTarget` and a log line the next prompt shows). Planting there makes a `CropPatch` (`obj-patch-N`, a `GardenPlot` named after what they grow, `FoodSupply.Patches`, removed when picked); planning makes a `Site` (`obj-site-N`, a `BuildPlot` at the builder's spot, `Construction.Sites`; choosing a new spot lets an unplanned one go, as does giving up on the sketch). Nobody is limited to one building. `plan-building` at the site sets `Resident.Sketch` (`PendingSketch.SiteId`, not saved, dropped after `SketchWaitMinutes`); `AiService` sends one `sketch:<id>` request at `Decision` priority (`SketchPrompt`: the spot, the clear square there (`SketchPrompt.Room`), `name`, `height` 1–`MaxHeight` 6, `rows` of block letters W wood, S stone, H hardwood, I iron ore, T heartwood, C crystal, `.` floor, D doorway, B bed); `ApplySketch` accepts it only if `BuildPlan.TryParse` passes (known letters, at least `MinBlocks`, no open tile shut in within the plan, the bed reachable) and `Construction.CheckPlacement` does (the plan's back-left corner on the site's spot, every tile it covers open ground with nothing on it and no other building, and nothing on the map that could be reached before is shut in after, checked with all planned walls), otherwise the reply is invalid and asked again; the builder then stands in the first doorway, or on open ground beside a building with none (`Site.StandAt`, where the site's object moves). People stand on whole tiles, so a tile is blocked when any block stands on its ground; a D clears its whole tile up to `DoorHeight` (4, higher blocks stay as a lintel) and the first B its whole tile (`BuildPlan.BedTile`, `IsHome`). One material makes `BlocksPerMaterial` (4) blocks (`BuildingRules.MaterialsFor`, `Project.NextTakesMaterial`). The `Project` (site id, owner, `Corner`, `BuildPlan`, `Placed`) lives in `TownSimulation.Construction`; `build` at the site lays up to `BlocksPerSession` (32) of the next `Cells` (bottom layer first, one material each, never on a tile someone stands on) at the end of the hour, and finishing adds a notable `TownEventKind.Built`. Owner, 5 November: nothing belongs to anyone. Anyone may `build` anyone's unfinished building with their own materials (`Project.Builders`, saved as `builders`, all remembered when it is finished), and plans hold furniture (`BuildingRules.Furniture`: B bed, K stove, M market stall, X crafting bench, N bench; each takes its whole tile, must be reachable, `BuildPlan.Furniture`, `BedTile` is the first bed); a plan needs `MinBlocks` blocks or some furniture, and one with no blocks (a stall alone) is finished as soon as it is drawn. A finished building's furniture is put in the world (`Construction.FurnitureIdOf`: the first bed is `BedIdOf`, site id + `BedSuffix`, the rest site id + `-built-N`; `BuildingRules.IsBuilt`), nobody is moved in, and a bed outdoors is anyone's who finds it free. Everything residents put in the world (sites, beds, crop patches) goes through `TownWorld.SetPlaced(source, …)` (`Placed`, `IsPlaced`, `PlacedVersion`; `Construction` and `FoodSupply.Attach` clear their own part for each new town). Laid blocks block walking: `Pathfinder` takes `Construction.IsBlocked` and `Forget()`s its caches when blocks change. Saves keep `sites` (id, owner, map, corner, stand, and the plan's name, height, rows, placed and started day once planned), `nextSiteNumber`, `patches` (id, map, x, y, what, planter) and `nextPatchNumber`, read before the residents so the objects are there; v2 saves from before this (with `projects` on fixed plots) load with those buildings dropped and a warning. `PromptText.AppendFood` lists every crop with its spot and planter and each of the resident's buildings. `BlockArt` draws each tile's blocks as up to four stacked columns of half-size cubes (`BlockHeight` 12) from palette colours, plus everything placed (a finished home's bed indoors, crop patches, and a site's marker until its first block); `MapArt` skips placed objects; `TownView.ShowBlocks` redraws when `Construction.Version`, `TownWorld.PlacedVersion` or the season changes, and `Tools/ArtPreview --blocks <png>` draws the city's open land (`StarterTown.OpenLandMapId`, now with no plot markers) with six sample buildings, five of them homes. Gathering by hand (owner, 5 November): fallen branches (`ObjectType.StickPile`, `gather-sticks`) and loose stones (`ObjectType.LooseStones`, `gather-loose-stones`) give `CraftingRules.ByHand` (1) a go with no tool (a `Gathering` with `MinTier` 0); trees (`Woodland`, `gather-wood`) now need a wooden axe (4 a go). The flat map (owner's choice of 5 November: a starter camp; layout from the owner's drawing of 5 November): the game's new towns and loads use `Content/FlatLand.cs` (`FlatLand.CreateWorld`/`CreateNewGame`/`CreateCrowdedTown` in `GameController`), not the city: one 56 × 44 `map-flat-land` ("Open land", town id `town-tiny-city-2`, so city saves are refused as "a different town") with a cross of paths (`PathRow` 21, `PathColumn` 27) splitting it into four. The bottom quarter (x > 27, y > 21) is the starter camp: (owner, 5 November) one `Kitchen` camp stove, four benches and two crafting benches: no beds and no stalls unless residents build them. Residents start at `FlatLand.StartingSpots` with no home or bed and sleep where they choose: any free bed, a free bench (anyone in v2, `SuitsHousing`), or the bare ground (`Sleep` is `IsDoneOnGround`; the spot becomes a `SleepingSpot` object, `obj-sleep-N`, in `TownSimulation.Sleeping` (`World/SleepingGround.cs`, saved as `sleepSpots`), pruned each tick once its sleeper is no longer heading for it or lying on it). `GameController.SetSimulation` turns newcomers and babies off whatever settings.json says: the world never grows. When the last resident dies, `Die` adds a notable `TownEventKind.WorldEnded` and `GameController` writes `History.Legacy.Write` (the lessons under `Legacy.LessonsHeading`, deaths, history, every life story) through `LegacyStore` to `persistentDataPath/Legacy/<slot> - day N.txt` and `latest.txt`; `StartNewTown` reads `latest.txt`'s lessons (`Legacy.ReadLessons`) into `FlatLand.CreateNewGame(profiles, lessons)`, so each new resident starts with them as "legacy" memories. The left quarter has eight `Woodland` "Trees" with four "Fallen branches" along its far edge, four "Rocks" with three "Loose stones" in its corner, the crop field (`IsCropField`, Garden tiles x 18–26, y 22–26, only soil: crops go anywhere), the pond (`IsPond`, x 20–25, y 29–34) with three fishing spots, and open ground between where the owner drew a sawmill and a stone mason's for residents to build if they choose. The top and right quarters are empty open land. There are no garden or building plots. Residents are the starter designs (or `residents.json` profiles) with no bed, job, rent, income or business; each gets a "came to this open land to start over" memory and their `About` as a memory instead of `AddLifeHistory`. There is no clinic, venue, event or shop on the flat map, and no tap, toilet or shower, so v2 adds `drink-pond` and `wash-pond` at the pond's `FishingSpot`s and `toilet-outdoors` at the `Woodland`s (`ActivityCatalog.IsInTheWild`, never in v1: `FoodSupply.Allows`). `StarterTown`'s city stays in the code for tests and v1-style towns. `Tools/ArtPreview --flat <png>` draws it with two built homes, a half-built sawmill, a stall and two beds standing alone, a sleeping spot, a new site and four crop patches.
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
  - `TinyCity.Runtime` (`Assets/Scripts/Runtime`): Unity glue only. `Bootstrap` is created by `[RuntimeInitializeOnLoadMethod]` (the boot scene stays empty, nothing references scripts by GUID) and adds `GameController`, which drives the clock (1 game minute per real second at 1x), autosaves every 6 game hours and on exit via `SaveStore` (temp file + `.bak`), and hands drawing to `TownView` (every map drawn at once, side by side at its own world origin, via one `MapView` each, plus the resident sprites; `MapView` turns `MapArt` into sprites through `SpriteFactory` (point filter, `FullRect`, textures released on redraw) with `ArtOverrides` PNGs from `StreamingAssets/Art` (documented in `Documentation/ArtOverrides.md`); sorting order is `IsoGrid.SortingOrder(x, y, layer)` with the ground at 0, `ArtLayer`s 1–2 and residents at `IsoGrid.ResidentLayer` 3; `TownView.ApplyTime` redraws when the season changes and applies `Lighting` per map, residents taking their map's tint; `TownView.UpdateResidents` picks each resident's pose, facing (step direction, chat partner or furniture) and frame (walking with the tick's progress, the rest with real time), caches their sprites in its own `SpriteFactory` and shows a `MoodIcons` child renderer at sorting order 32000; `PlaceholderArt` now only makes IMGUI backgrounds), `CameraPanels` (1–16 `CameraPanel`s, `MaxPanels`, raised from 10 after the owner's panel test stayed above 60 fps at 16, each its own orthographic camera with `Camera.rect` from a grid `Layout`; panel 0 borrows `Camera.main`; a panel follows a resident's sprite, so interior/exterior moves need no special handling, or is a free camera over one map; names, door signs and speech bubbles are drawn per panel, and a thing's name only while a resident is performing an activity at it (camera close enough) or while the pointer is over it (`CameraPanels.ObjectAt`: the tile's diamond, stretched up over the furniture); with the pointer over a building outdoors a card shows the engine-free `PlaceInfo` (`Simulation/World`: name, open or who lives there, who is inside, for every place behind the building's doors), found by `TownView.BuildingAt` → `MapArt.BuildingAt` (opaque pixel of the building's whole picture, `MapArt.Buildings`, front-most strip wins), and a double-click opens `PlaceInfo.EnterMapId` in that panel; the layout is kept in `PlayerPrefs`) `Inspector` (tabbed IMGUI panel; the top row is Town Journal, Town View (`CameraPanels.ToggleTownView`: one free camera over the whole city, remembering the panels so a second press, or Watch Everyone, puts them back; `InTownView`), −/+ and Watch Everyone, each button as wide as its words, then a box with how many people live in the city, a People list and a find-a-person box built on the engine-free `PeopleList` (`Simulation/Residents`: `Of` with `PeopleFilter`/`PeopleOrder`, `Search` best match first); AI-written text uses the `GuiStyles.AiNote` style so it is never mistaken for game fact), `CameraWindow` (the Cameras button beside Settings: the auto cycle, `PlayerPreferences.CycleCameras`/`CycleMinutes` (one of `CameraCycle.Intervals`)/`CycleMode` (Residents, Buildings, HalfAndHalf, Everything)/`CycleBusyOnly`; the engine-free `CameraCycle` (`Simulation/Configuration`) lists `Targets` (living residents, and businesses plus homes someone lives in) and `Assign`s them to the open panels a page at a time (turn × panel count, wrapping; half and half gives the first half of the panels residents, and one panel alternates), and `GameController.StepCameraCycle` turns the panels when `Due` (every so many game minutes) or when the panel count changes, paused by Town View and the panel test; nothing is saved but the choices), `SettingsWindow` (the Settings button beside the title in `Inspector.DrawTownControls`; Test Camera Panels runs `GameController.StartPanelTest`, which saves the town, swaps in a throwaway copy of the city filled to 50 people (`StarterTown.CreateCrowdedTown`: newcomers past `MaxNewcomers` via `MoveInNewcomer(anyNumber: true)`, the homeless on benches; nothing is saved and loads are refused while it shows), raises `CameraPanels.Limit` to `PanelTest.Largest` (50, the most residents who can be alive), shows 1 panel and then each count up to 50 at 1x, and with the engine-free `PanelTest` (`Simulation/Configuration`: settle, then time frames; smooth = 60 fps average and the slowest frame in twenty under 33 ms) writes a report to the exports folder and puts the player's town, panels and speed back; AI on/off via `AiService.SetEnabled`, installed models via `AiService.RefreshModelList`/`InstalledModels` (works while the AI is off) and `UseModel`, volumes, `ScreenSettings`, theme; choices are `PlayerPreferences` (engine-free, `Simulation/Configuration`) in `persistentDataPath/player-settings.json` via `PlayerSettingsStore`, and override settings.json's `enabled`/`model` through `PlayerPreferences.ApplyTo` before `AiService` is made). Interface colours come only from `UiPalette` (Dark default and Light, copied value for value from the owner's Stills & Reels `src/Theme.cs`, the same palette StackSpace uses; keep the three in step, and there is deliberately no Auto option); `UiTheme` holds the choice and builds one `UiSkin` and `GuiStyles` per palette, set as `GUI.skin` at the start of `GameController.OnGUI`. Labels drawn on the town (`GuiStyles.Name`, `Sign`) are neon whatever the theme (`CameraPanels.Label`: cream text, dark shadow and a glow of `GuiStyles.PersonGlow`/`DoorGlow`/`ThingGlow`, the owner's pick of 30 October). A camera never shows more than its whole map and stays over it (`CameraPanel.Aim`/`Keep`, so a free camera zoomed fully out can't be dragged), zooms in to `MinHalfHeight` at the pointer (`ZoomAt`), and maps sit `TownView.MapGap` (200 units) apart so another map never shows beside it, even on an ultra-wide screen. The People list's filter row has an Expand toggle (`Inspector.DrawPeopleGrid`: the side panel shows the controls and everyone in a two-column grid, in the chosen order, first name A–Z by default, instead of the resident's tabs); "Hide ▶" beside Settings, or H, sets `GameController.SidebarHidden` (kept in `PlayerPrefs`), which gives `CameraArea` the whole width and puts a "◀ Panel" button first in the top-right camera panel's header (`ShowsSidebarButton`). `JournalWindow` (J; search box, so `HandleKeys` ignores keys while `GUIUtility.keyboardControl` is set), `Notifications` (pop-ups for `Journal.IsNotable` events from `TownHistory.Since`, reset on load) `SoundPlayer` (added by `GameController`; makes every sound on a background `Task` at start-up, then `AudioClip.Create`s them; crossfades music by `Lighting` night and ambience by the active panel's map, plays the director's cues on that map with distance falloff, at most 4 a frame, and a chime for new notifications; volumes from `PlayerPreferences.Volume`; needs `com.unity.modules.audio`), `WindowFrame` (Windows only: DWM dark mode and Windows 11 caption, text and border colours from `UiPalette` `Window`/`Text`/`BarBorder`, applied with the theme and on focus), `ExportStore` (text files and save copies under `persistentDataPath/Exports`) and `SavesWindow` (S). Save slots: each town is `Saves/<slot>.json` (+ `.bak`), slot ids `town`, `town-2`, … (`Persistence/SaveSlots.cs`: `SaveSlotIndex.IsValidSlotId`/`NewSlotId`, names in `Saves/slots.json` so a copied file is the town exactly; `SaveSummary.Read` gives the list's date and residents without a full load). `GameController.CurrentSlot` (remembered in `PlayerPrefs`) receives autosaves, F5 and the exit save; `LoadSlot`, `NewTownSlot` and copying the current town save it first; the town being played can't be deleted. It pumps `AiService.Update` every frame with real time. `BuildScript` sets `insecureHttpOption = AlwaysAllowed` so the player can reach Ollama over http. Keyboard input uses IMGUI events, not the Input Manager or Input System.
  - `TinyCity.Editor` (`Assets/Scripts/Editor`): `BuildScript.BuildWindows`, the CI build entry point.
  - Tests: `Assets/Tests/EditMode` (plain NUnit, must not use `UnityEngine`, also compiled by `DotNetTests/` together with the Simulation and Art sources) and `Assets/Tests/PlayMode` (Unity-only). Unity bundles NUnit 3.5, so avoid newer constraint features (e.g. `Has.Count` on arrays fails there; prefer `List<T>` or LINQ + `Is.True`). Under .NET the tests compile into the same assembly as the simulation, but in Unity they are separate, so `internal` members need the `InternalsVisibleTo` in `Assets/Scripts/Simulation/AssemblyInfo.cs`.
- Player settings live in `Assets/StreamingAssets/Configuration/settings.json` (Ollama `enabled`, `endpoint`, `model`, `timeoutSeconds`, `maxRetries`, `decisionWaitMinutes`, `decisionWaitSeconds`, `parallelRequests`; `simulation.romance`, `simulation.communityEvents`, `simulation.newcomers` and `simulation.babies`, read through `GameSettings.Simulation` so older files without the section get the defaults); `GameSettings.ValidOrDefault` falls back to defaults on invalid input. Player-facing AI setup is in `Documentation/OllamaSetup.md`. Ollama cannot be installed in the cloud container (ollama.com is blocked), so real-model checks are manual on the user's PC.
- `Tools/StressTest` (`dotnet run -c Release -- <report.md> [days] [aiDays] [bigDays] [districts] [parallel] [waitSeconds] [v2Days]` in that folder; the stand-in Ollama gets `parallel` slots, each reply 30% slower per other request beside it, an assumed figure) runs a long town with autosaves, the game loop at every speed against a slow, unreliable stand-in Ollama (GPU-like and CPU-like reply times) and an Ollama outage, then a big city of `districts` side-by-side copies of the starter town (`BigCity.cs`, ten residents each), and writes the numbers as Markdown; the latest results are in `Documentation/StressTest.md`. Its numbers are .NET; Unity's Mono is roughly 2–3 times slower. Tool `.csproj` files are committed (`!/Tools/*/*.csproj` in `.gitignore`).
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