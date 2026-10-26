# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 0. Current Repository State (update as the project grows)

As of the initial commit, the repository contains only `README.md`, this file and `GameDesignDocument.md`. There is no Unity project, no C# source, no tests and no build pipeline yet, so there are no build, lint or test commands to run. Phase 0 (section 19) creates them.

The cloud container used so far has no Unity editor, no Unity licence, no .NET SDK and no Mono. Do not assume any of them exists; re-check with `which dotnet unity mono` at the start of a session. Until a Unity-capable runner exists (for example a GitHub Actions workflow using a Unity licence), only static checks and pure C# logic tests runnable with a .NET SDK can be executed here.

When Phase 0 lands, replace this section with the real commands: how to open the project, run Edit Mode and Play Mode tests (including a single test), run any plain .NET tests, and produce a Windows build.

# The Tiny Town

## 1. Your Role

You are the lead engineer and implementation agent for **The Tiny Town**, a Windows desktop life-simulation game built with Unity 6 and C#.

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

The Tiny Town is a cosy isometric 2D pixel-art life simulation. The full initial town contains ten autonomous residents with needs, personalities, routines, jobs, finances, relationships, memories and life histories.

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
- Warm colours.
- Charming buildings.
- Greenery and parks.
- Ten distinguishable resident appearances.
- Readable walking, sitting, eating, sleeping, working, talking and emotional animations.
- Gentle music and village ambience.
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