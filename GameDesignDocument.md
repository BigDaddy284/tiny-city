# The Tiny Town
## Game Design Document (GDD)

**Document version:** 1.0  
**Project status:** Pre-production / initial implementation  
**Platform:** Windows desktop  
**Engine:** Unity 6, C#  
**Visual style:** Isometric 2D pixel art  
**AI:** Local Ollama API, initially Qwen2.5 7B  
**Development workflow:** Claude Code on the web, GitHub repository and cloud-based implementation  
**Project principle:** Build a small, complete, reliable simulation before expanding the world.

---

# 1. Game Overview

## 1.1 Concept

The Tiny Town is a cosy isometric life simulation in which autonomous residents live their own lives in a small fictional village.

Every resident has individual needs, a personality, preferences, a home, a job or daily activities, relationships, finances, memories and a personal history. Residents make their own decisions, develop friendships, experience disagreements, form romantic relationships, attend events, become ill, grow older and eventually die.

A locally hosted language model provides contextual decision-making and natural conversations. The game simulation remains responsible for the actual rules of the world, ensuring that the town continues to function when the AI is slow, unavailable or produces an invalid response.

The player is an observer rather than a controller. They can watch residents through a flexible camera system, inspect their lives, read conversations, follow significant events and observe how the community develops.

The long-term vision is a living world that can eventually grow from one village into multiple connected towns and, ultimately, a larger city. The initial implementation must remain focused on one town and ten residents.

## 1.2 Design Pillars

1. **Autonomous lives:** Residents make their own decisions without constant player intervention.
2. **Persistent identity:** Every resident retains their memories, relationships and personal history across sessions.
3. **Observable behaviour:** Players can understand residents' needs, activities, decisions and significant life events.
4. **Local-first AI:** The game uses Ollama and a configurable local model without requiring paid cloud AI services.
5. **Reliable simulation:** Deterministic game systems govern needs, health, movement, finances, aging and death.
6. **Believable social life:** Conversations and relationships evolve from personality, shared experiences and circumstances.
7. **Cosy presentation:** A warm storybook village, expressive pixel characters, readable animations, seasons and gentle audio.
8. **Incremental development:** Each milestone produces a playable and tested build.
9. **Long-term extensibility:** Data and services are designed to support future population growth and multiple towns without implementing those systems prematurely.

## 1.3 Player Experience

The player should feel like an observer watching a small community gradually develop its own stories.

They can:
- Watch the whole village or follow individual residents.
- View up to ten camera panels in one application window.
- Inspect needs, health, relationships, memories, finances and activities.
- Read recent conversations and important historical events.
- Pause the simulation and adjust its speed.
- Save, load, duplicate and manage independent towns.
- Export town histories and resident life stories to readable files.

The game must remain interesting without requiring the player to issue commands to residents.

---

# 2. Scope and Development Priorities

## 2.1 First Playable Milestone

The first milestone is a complete one-resident life simulation, not merely a character moving around a map.

It must include:
- One resident with persistent identity and personality.
- One small exterior map and one home interior.
- Basic needs and deterministic need updates.
- A daily routine.
- Movement between valid locations.
- At least one work or scheduled activity.
- Eating, sleeping and other basic activities.
- A basic inspection interface.
- A real Ollama connection and validated AI-assisted decisions.
- A simple natural-language conversation.
- Persistent memories and conversation history.
- Save and load.
- Safe operation when Ollama is unavailable.
- Automated tests and a playable Windows build, subject to verified cloud build capability.

Use simple placeholder art during this milestone.

## 2.2 Full Initial Release

The intended first complete town contains ten designed residents and approximately 20–30 hand-designed buildings, including homes, shops, workplaces, community buildings and green spaces.

The initial release includes the core simulation, social life, finances, jobs, hobbies, moderate health simulation, aging, optional romance, multiple camera layouts, a town journal, resident timelines, seasons and save management.

## 2.3 Explicitly Deferred Features

Do not implement these in the first release:
- Multiple connected towns.
- A central city.
- Resident migration between towns.
- Family generations and children.
- A complex economic simulation.
- Player-controlled building construction.
- A dedicated in-game mod editor.
- Unrestricted model-generated world modifications.

The architecture should permit sensible future expansion, but these features must not delay the initial playable game.

---

# 3. Technical Foundation

## 3.1 Engine and Platform

- Unity 6.
- C#.
- Windows desktop release.
- Isometric 2D presentation.
- Tile-based world maps.
- Version-controlled project.
- Automated Edit Mode and Play Mode tests where appropriate.

The project should use the Unity version and packages confirmed to be available in the repository and cloud build environment. Do not silently change Unity versions or introduce incompatible packages.

## 3.2 Cloud Development

Claude Code on the web is the chosen development environment, connected to a GitHub repository.

The repository is the source of truth for:
- The GDD and persistent project instructions.
- Source code and Unity assets.
- Tests and build configuration.
- Development notes and decisions.
- Release and troubleshooting documentation.

Cloud implementation must distinguish between code that can be inspected or tested in a general cloud environment and code that requires the Unity editor, licensed build tools or a Windows runner.

Never claim that a Unity project builds, launches or passes Play Mode tests unless the relevant operation actually ran successfully.

## 3.3 Local AI

The game connects to an independently installed Ollama service through its configurable local HTTP API.

Initial model: Qwen2.5 7B.

Requirements:
- Configurable endpoint and model name.
- No bundled Ollama application or model.
- No mandatory paid API.
- Connection status and useful diagnostics.
- Mock AI responses for automated testing.
- Graceful fallback if Ollama is unavailable.
- Real-model integration tests when the model service is available.

The game must not require the cloud development environment to access the user's PC or local Ollama instance. Cloud tests should use mocks or controlled test responses.

---

# 4. World Design

## 4.1 Village

The village is hand-designed, finite and expandable through development. It is not procedurally generated in the initial version.

The full village should contain recognisable areas such as:
- Residential streets.
- Village centre.
- Shops and food businesses.
- Workplaces.
- Parks and gardens.
- Community spaces.
- A future cemetery.

The visual style should feel like a cosy storybook village, with warm colours, greenery, charming buildings and readable paths.

## 4.2 Buildings and Interiors

Buildings use separate interior maps rather than a complicated cutaway rendering system.

Entering a building transitions the resident from the exterior map to the relevant interior map. Leaving returns them to the correct exterior entrance.

Interior maps have fixed furniture and objects in the initial release. There is no in-game furniture placement or building editor.

The first prototype requires only one home interior and a small exterior map.

## 4.3 Navigation

Residents walk. No driving or vehicle simulation is required.

The navigation system must:
- Use valid tile-based routes.
- Respect obstacles and walls.
- Prevent movement through inaccessible tiles.
- Handle valid entrances and exits.
- Support transitions between exterior and interior maps.
- Preserve a resident's logical location through saving and loading.

Residents should not teleport as a substitute for navigation, except for explicitly designed recovery behaviour that is logged and tested.

---

# 5. Residents

## 5.1 Population

The initial full town contains ten designed residents.

Residents begin without established family ties. Relationships emerge through play.

The simulation must not hard-code its architecture to exactly ten residents. Ten is the initial content target, not a limit in the underlying systems.

## 5.2 Resident Data

Each resident requires:
- Stable unique ID.
- Name and identity.
- Appearance and clothing.
- Age, birthday and life stage.
- Home and workplace.
- Job, wages and personal finances.
- Personality traits and preferences.
- Hobbies and interests.
- Needs and health.
- Current activity and goal.
- Relationships with other residents.
- Structured memories.
- Conversation and event history.
- Living or deceased status.
- Cause and date of death, when applicable.

Names must not be used as permanent database keys.

## 5.3 Personality

Each resident has a distinct base personality and background.

Personality influences choices, dialogue, interests, social behaviour and reactions to events. Traits can evolve gradually through experience.

The AI must not be able to arbitrarily rewrite a resident's core identity or personality without a validated game-side process.

## 5.4 Character Customisation

Before the simulation starts, the player can customise the ten residents using simple controls such as tick boxes, sliders and appearance selections.

Provide sensible defaults so the player can start quickly. Do not require lengthy forms or a complicated character editor.

## 5.5 Life Stages and Aging

Use compressed lifespans so meaningful life events happen within a manageable play period.

Track exact age and in-game birthday. Support visible life stages:
- Young adult.
- Adult.
- Middle-aged.
- Elderly.

Life-stage transitions should be deterministic and testable.

## 5.6 Death and Historical Records

Residents can die from old age or serious illness caused by extreme neglect.

Health and death decisions belong to deterministic game logic. An LLM cannot directly declare a resident dead.

A deceased resident:
- Is marked permanently deceased in the save.
- Has a recorded death date and cause.
- No longer runs living routines or consumes AI requests.
- Remains in town history, memory records and relationship history.
- Can be referenced by living residents through relevant stored memories.

A future cemetery system should add a tombstone for each death and permit residents to visit graves and remember deceased residents.

Do not delete deceased residents from persistent history.

---

# 6. Needs and Daily Life

## 6.1 Core Needs

Implement eight needs:
1. Hunger.
2. Thirst.
3. Energy and sleep.
4. Hygiene.
5. Toilet.
6. Social contact.
7. Exercise.
8. Fun and recreation.

Needs change according to time, activity, health and circumstances.

Need calculations must be deterministic and tested. The LLM should not calculate need values.

## 6.2 Routines

Residents follow flexible daily routines.

Typical weekdays include sleep, personal care, meals, work and social activities. Weekends allow more leisure, hobbies, visits and gatherings.

Routines adapt to:
- Urgent needs.
- Work schedules.
- Illness.
- Social invitations.
- Relationships.
- Current location.
- Recent experiences.
- Unexpected events.

The game identifies valid actions and important needs. The AI can select among suitable options when useful.

## 6.3 Priority and Interruptions

Critical needs take priority over low-priority activities.

Examples:
- A resident needing urgent sleep should leave an ordinary conversation.
- A seriously ill resident should prioritise treatment.
- A resident whose work shift has started should normally stop an optional hobby.
- A resident cannot continue an activity whose location is no longer accessible.

Prevent infinite conversations and endless attempts to perform invalid actions.

## 6.4 Health

Use moderate health simulation.

Illness can result from neglect, exhaustion or circumstances. Symptoms affect activities, mood, work and relationships.

Everyday illnesses may improve through rest and self-care. Serious illnesses may require clinic visits. Recovery takes time.

Health progression, recovery and death are controlled by deterministic rules.

---

# 7. Work and Economy

## 7.1 Jobs

Residents have assigned jobs and visible work activities.

Examples include:
- Serving customers.
- Stocking shelves.
- Cooking and preparing food.
- Repairing items.
- Other tasks appropriate to the workplace.

Job progression should remain relatively stable. Job loss or changes occur only in significant circumstances rather than as a constant random event.

## 7.2 Finances

Residents earn wages and spend money on rent, food, shopping and leisure.

The economy is simple and comprehensible. The first release does not need detailed production chains, investment systems or complex inventories.

## 7.3 Businesses

The full village should contain a small variety of businesses, including a general shop, café or pub, bakery or restaurant, workshop and clinic.

Residents can visit businesses, make simple choices, pay for purchases and visibly consume or use what they buy.

Businesses have basic prices and stock. Purchases and financial changes are recorded by game logic, not invented by the AI.

---

# 8. Social Simulation

## 8.1 Relationship Model

Relationships track structured attributes such as:
- Friendship and closeness.
- Trust.
- Respect.
- Dislike.
- Romantic interest where appropriate.
- Significant shared experiences.
- Recent interactions and conflicts.

Relationship changes must be based on validated events and interaction outcomes.

## 8.2 Conversations

Residents can begin spontaneous conversations when they meet or deliberately seek others out.

Conversation content supports:
- Everyday topics.
- Humour and questions.
- Gossip.
- Different opinions and disagreements.
- References to memories and shared experiences.
- Natural subject changes and endings.

Ordinary conversations typically contain several back-and-forth turns. Important conversations may be longer.

Both residents should contribute. Conversations end when either resident has a reason to leave, including work, sleep, urgent needs or incompatible activities.

Conversation length, request frequency and turn limits are controlled by the game.

## 8.3 Romance

Romance is optional and restricted to compatible adults.

Possible developments include attraction, friendship, dating, commitment, moving in together, disagreements, reconciliation and breakups.

Romance is not compulsory and must not be guaranteed by the simulation.

## 8.4 Hobbies

Residents can enjoy reading, gardening, cooking, music, art, games, exercise, collecting and other personality-appropriate activities.

Support both individual hobbies and shared activities. Residents can invite others to participate, form group interests and develop memories through shared experiences.

## 8.5 Community Events

Combine calendar events with organic social activity.

Calendar events include seasonal celebrations and community gatherings. Organic events can include dinners, outings, invitations and spontaneous meetings influenced by availability, interests and relationships.

---

# 9. Memory and History

## 9.1 Three Distinct Data Layers

**Current state:** needs, health, finances, location, activity and relationships.

**Persistent memories:** important conversations, promises, arguments, opinions, shared experiences and life events.

**History:** searchable chronological conversation records and town events.

Do not rely on a large, ever-growing LLM conversation prompt as the only memory system.

## 9.2 Memory Records

A memory record should contain:
- Stable memory ID.
- Resident ID.
- In-game date and time.
- Memory type.
- Importance.
- Participants.
- Concise summary.
- References to relevant events or conversations.

Memory retrieval should select relevant records for the current situation. Memory creation should prioritise significant events instead of recording every trivial line as a permanent memory.

## 9.3 Journals

Provide:
- A town-wide chronological journal.
- Individual resident life timelines.
- Search and filtering.
- Significant event notifications.
- Exportable resident life stories.
- Exportable town history.

Minor events should be logged quietly. Important events such as romance milestones, serious illness, job loss and death may trigger notifications.

Historical records should remain intact for as long as the save exists.

---

# 10. AI Architecture

## 10.1 Fundamental Rule

**The game owns simulation truth. The LLM supplies contextual judgement and language.**

The LLM proposes decisions and produces dialogue. The game validates proposals and executes permitted actions.

## 10.2 Game Responsibilities

The game handles:
- Need calculations.
- Health and death.
- Aging.
- Movement and pathfinding.
- Map transitions.
- Finances and transactions.
- Work schedules.
- Activity validation.
- Relationship data changes.
- Saving and loading.
- Request scheduling and failure recovery.

## 10.3 AI Responsibilities

The AI may:
- Choose among valid actions.
- Propose a plan based on personality and circumstances.
- Generate dialogue.
- Choose conversational topics.
- Produce character-appropriate explanations.
- Suggest potentially significant memories for game-side validation.

The AI cannot bypass simulation rules, invent completed transactions, move residents through walls or change persistent state without validation.

## 10.4 Central Request Queue

Use one central AI request queue instead of continuous independent loops for every resident.

The queue should support:
- Priority classes.
- Request IDs.
- Resident IDs.
- Deduplication.
- Timeouts.
- Bounded retries.
- Stale-request rejection.
- Queue diagnostics.
- Fair scheduling.
- Cancellation or invalidation when circumstances change.

Start with one active request at a time. Measure performance before increasing concurrency.

Urgent needs and important decisions should receive higher priority than optional flavour dialogue. Prevent starvation of lower-priority residents.

## 10.5 Structured Actions

AI action responses must follow a defined schema.

Illustrative response:

    {
      "action": "go_to_work",
      "target_id": "workplace_bakery",
      "reason": "My shift is starting"
    }

This is illustrative, not a complete action list.

The game must validate the action type, target, resident state, location, accessibility, current needs and relevant restrictions before applying it.

Dialogue responses may use natural language, but action decisions must be machine-readable.

## 10.6 Failure Handling

If Ollama is unavailable, residents use built-in routines.

For invalid AI responses:
1. Parse and validate the response.
2. Retry a small, bounded number of times when appropriate.
3. Record useful diagnostics.
4. Use a safe built-in action if retries fail.
5. Continue the simulation.

Do not allow endless retries or let AI failures halt critical simulation updates.

Display connection status in the interface. Persistent errors should be diagnosable without interrupting play for every temporary failure.

## 10.7 Explanations

Keep separate:
- Current game state.
- Chosen action.
- Recorded memories.
- AI-generated explanation.

An AI-generated explanation is not guaranteed to be a complete or accurate description of the model's internal reasoning.

---

# 11. Time and Simulation

## 11.1 Game Clock

One in-game day should take approximately 20–60 real minutes at normal simulation speed, subject to performance testing and balancing.

Allow adjustable simulation speed and pause/resume.

Track in-game dates, birthdays, seasons, work schedules, life stages and event timestamps.

## 11.2 Offline Behaviour

The simulation stops when the game closes. Reopening a save resumes from the last saved state.

Do not simulate offline progress in the initial release.

## 11.3 Seasons

Support spring, summer, autumn and winter with changing scenery, ambience, decorations and seasonal events.

Seasonal features should be introduced after the core simulation works.

---

# 12. Observer Interface

## 12.1 Camera Panels

One application window supports between one and ten camera panels.

Each panel can:
- Follow a selected resident.
- Use free-camera mode.
- Follow a resident through interior/exterior transitions.
- Be reassigned to another resident.

Layouts automatically adapt to the number of active panels.

All panels observe the same shared simulation.

## 12.2 Resident Inspection

The resident inspector should show:
- Needs and health.
- Current location and activity.
- Goal and intended next action.
- Personality and preferences.
- Relationships.
- Memories and recent conversations.
- Finances and job.
- Life stage and history.

Distinguish current facts from AI-generated explanations.

## 12.3 Town Controls

Include:
- Pause and resume.
- Adjustable simulation speed.
- Resident roster.
- Camera layout controls.
- Resident inspection.
- Searchable journal and timelines.
- Notifications.
- Save/load and save duplication.
- Configuration and AI connection status.

---

# 13. Persistence and Save Management

## 13.1 Save Features

Provide:
- Multiple independent save slots.
- Manual saves.
- Periodic autosaves.
- Save before normal application shutdown.
- Save duplication.
- Validation and sensible recovery from incomplete or invalid data where feasible.
- Versioned data and migration support.

## 13.2 Data Integrity

Use stable IDs for residents, towns, relationships, memories, events and world maps.

Save logical state rather than transient scene-object references, active pathfinding calculations or incomplete AI requests.

When loading, restore consistent state and reconstruct runtime objects. Discard or safely reconcile in-flight AI work.

Save migrations must preserve resident identity, relationships, memories and historical records.

---

# 14. Recommended Project Architecture

Use modular services with explicit responsibilities.

Suggested folders:

    Assets/
      Scenes/
      Art/
        Characters/
        Buildings/
        Environment/
        UI/
      Audio/
      Scripts/
        Core/
        Residents/
        Social/
        World/
        Economy/
        AI/
        Persistence/
        UI/
      Tests/
        EditMode/
        PlayMode/
      StreamingAssets/
        Configuration/
        Content/

Suggested services include:
- Simulation clock and coordinator.
- Resident and needs systems.
- Health and life-stage systems.
- Routine and activity systems.
- Navigation and map-transition services.
- Relationship, conversation and memory systems.
- Job, finance and business systems.
- Ollama client and central AI queue.
- Prompt builder and response validator.
- Fallback behaviour.
- Save service and migrations.
- Town journal and export service.
- Camera layout and resident inspector.

Do not create every planned class in advance. Implement components as required by each playable milestone.

## 14.1 Core Persistent Records

Define stable data models for:
- Town save.
- Resident record.
- Relationship record.
- Memory record.
- Conversation record.
- World location.
- AI request.
- Town event.

Keep persistent records independent from Unity scene objects.

## 14.2 Future Expansion

Use stable town and resident IDs. Avoid hard-coded assumptions that prevent the population from growing beyond ten.

Keep town data, resident identity and simulation services independent of individual scene layouts.

Future systems may connect multiple towns, migrate residents, preserve their memories and relationships, support family generations and introduce a central city.

These are future architecture considerations, not initial implementation requirements.

---

# 15. Development Roadmap

Every phase must result in a tested checkpoint.

## Phase 0 — Foundation
- Inspect the repository and Unity project.
- Establish project structure and documentation.
- Configure source control and automated tests.
- Create a minimal boot scene.
- Verify the available build environment.

**Acceptance:** project opens, the available build process works, and a basic automated test passes.

## Phase 1 — One-Resident Simulation
- One resident.
- Small exterior map and home interior.
- Needs, clock and daily routine.
- Movement and valid map transitions.
- Eating, sleeping and scheduled activity.
- Basic resident inspection.
- Save/load.
- Safe behaviour without AI.

**Acceptance:** the resident lives a basic day, and saved state can be restored.

## Phase 2 — AI and Memory
- Ollama integration.
- Structured action responses.
- Validation, timeouts and bounded retries.
- Safe fallback.
- Basic dialogue.
- Persistent memories and conversation history.
- Save/load tests including memory.

**Acceptance:** real AI-assisted decisions work when available, and the simulation survives invalid responses or service failure.

## Phase 3 — Social Simulation
- Two or three residents.
- Relationships.
- Multi-turn conversations.
- Shared experiences and memories.
- Social needs and interruptions.

**Acceptance:** residents interact without player control and retain significant history.

## Phase 4 — Full Town
- Ten residents.
- Jobs, finances and businesses.
- Shopping and hobbies.
- Health and aging.
- Optional romance.
- Community events.
- Prioritised AI queue.

**Acceptance:** extended simulation tests complete without corrupted saves, uncontrolled queues or blocked critical needs.

## Phase 5 — Observer Interface
- One-to-ten camera layouts.
- Resident inspection.
- Town journal and individual timelines.
- Notifications and search.
- Save slots, duplication and export.

**Acceptance:** all residents can be observed and inspected across maps.

## Phase 6 — Art and Audio
- Replace placeholders with polished isometric pixel art.
- Character animations.
- Environment animation and visual feedback.
- Music and ambience.
- Seasonal visuals.

**Acceptance:** major activities are visually readable and regression tests pass.

## Phase 7 — Release
- Standalone Windows build.
- Setup and troubleshooting documentation.
- Save recovery and migration tests.
- Performance and long-running simulation tests.
- Final packaging.

**Acceptance:** the game launches on Windows, works without AI when necessary, connects to a separately installed Ollama service and preserves saved towns through normal updates.

---

# 16. Testing and Quality Standards

Automated tests must cover:
- Need updates.
- Critical need priorities.
- Valid and invalid actions.
- Navigation constraints.
- Map transitions.
- Relationship changes.
- Memory creation and retrieval.
- Save/load round trips.
- Save migrations.
- Malformed AI responses.
- Ollama timeouts and disconnection.
- Fallback behaviour.
- AI queue priorities and deduplication.
- Deceased resident handling.

Manual tests must cover:
- Actual playable builds.
- Camera behaviour.
- UI readability.
- Resident activity.
- Interior/exterior transitions.
- AI disconnection and recovery.
- Extended simulation sessions.
- Save duplication and restoration.

Performance measurements must record simulation update time, AI request latency, queue length, memory usage and behaviour at the target population.

Never invent test results. If the required Unity editor or build tools are unavailable in the cloud, document the limitation and provide a reproducible alternative verification procedure.

---

# 17. Art and Audio Direction

The visual style is a polished isometric pixel-art storybook village.

Use:
- Warm colours.
- Charming buildings.
- Greenery, paths, parks and gardens.
- Ten distinguishable resident appearances.
- Readable silhouettes and expressive animations.
- Consistent perspective, scale and lighting.

Required everyday animation categories include walking, sitting, eating, sleeping, working, talking and emotional gestures.

Audio should be cosy and unobtrusive, with gentle music, village ambience, footsteps, doors, work sounds and activity effects.

Placeholder graphics are explicitly acceptable in early milestones. Establish a coherent art direction before the final art-production phase, but do not delay core simulation work for polished sprites.

---

# 18. Modding and Content Configuration

No dedicated mod editor is required initially.

Keep ordinary content configuration in documented files or folders where practical. Avoid embedding all resident, business and activity definitions into giant scripts.

Future content customisation may include resident appearances, clothing, bike-like or object-style custom skins where relevant to future content, and map or content additions. These are not first-release priorities.

Do not expose arbitrary code execution through content files.

---

# 19. Definition of a Successful First Release

The first full release is successful when:
- Ten residents live in one coherent village.
- Their needs and activities progress reliably.
- Local AI contributes to decisions and natural conversations.
- Relationships and memories persist across sessions.
- Residents work, shop, socialise, pursue hobbies and age.
- Illness and death follow deterministic rules.
- The player can observe and inspect residents through flexible cameras.
- The town journal and individual histories remain accessible.
- Saves can be created, restored and duplicated safely.
- The game continues functioning when Ollama is unavailable.
- A verified Windows build can be launched and played.
- Automated tests cover the important simulation, persistence and AI-failure rules.

**Final development principle:** prove one complete life simulation, then scale it. Do not trade a working, testable game for a large collection of unfinished features.
