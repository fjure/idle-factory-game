# Agent Working Agreement

This repository uses Spec Kit as the source of product and implementation intent. All agents must follow [docs/spec-kit-greenfield-workflow.md](docs/spec-kit-greenfield-workflow.md).

## Authority and lifecycle

1. `.specify/memory/constitution.md` governs every specification, plan, task, and implementation decision.
2. The active feature's `spec.md`, `plan.md`, and `tasks.md` define the intended product and technical scope.
3. During MAQA execution, GitHub Issues are the workflow-status authority. Local task checkboxes and GitHub Project draft cards do not define status.
4. Read `.specify/extensions.yml` before every Spec Kit phase. Execute mandatory hooks and explicitly evaluate relevant optional hooks.
5. Do not implement before the specification, clarification, plan, task, and analysis gates are complete.
6. When tasks change, refresh and validate the orchestration ledger before implementation.
7. The `after_implement` hooks `speckit.wiki.ingest` and `speckit.learn.review` are mandatory (`optional: false`) in both implementation lanes. Do not report an implementation run, MAQA batch, or feature as complete until both have run on the result, or until a recorded blocker explains why one could not run. After any `specify extension add` or `update`, confirm that both entries still have `optional: false`, because re-registration can restore the extension default.
8. Spec Kit skills are installed for every configured AI integration (Codex in `.agents/skills/`, Claude Code in `.claude/skills/`; Codex is the default). This document writes skill invocations in Codex form, such as `$speckit-maqa-qa`; Claude Code invokes the same skill as `/speckit-maqa-qa`. Hook command IDs such as `speckit.maqa.qa` map to either form by replacing dots with hyphens.

## Greenfield bootstrap gate

Before the first feature begins, require all of the following:

- The constitution contains no template placeholders.
- `.specify/branch-convention.yml` exists and the chosen convention is documented.
- The project wiki has been initialized.
- The repository has an initial commit pushed to GitHub and GitHub reports a default branch.
- `$speckit-maqa-setup` succeeds and the `maqa`, `maqa:in-progress`, `maqa:in-review`, and `maqa:blocked` labels exist.
- `maqa-config.yml` contains real Unity Edit Mode and Play Mode test commands before implementation starts. UI features must explicitly cover accessibility, supported screen classes, and empty/error states in their acceptance and QA evidence.

If a gate is missing, report it and complete safe local preparation before implementation. Do not claim the project is implementation-ready.

## Role lenses

The parent agent owns scope, sequencing, integration, and final verification. Role agents receive a bounded assignment and return evidence; they do not broaden product scope.

### Game Designer and Product Owner

- Validates the problem statement, user value, user stories, acceptance criteria, priorities, exclusions, and measurable success criteria.
- Owns the core loop, mechanics, progression intent, player agency, controls, rewards, difficulty, and feature-level balance goals.
- Detects ambiguous behavior, missing edge cases, and requirements that cannot be accepted objectively.
- Writes or reviews `spec.md` before planning and rechecks player-visible acceptance after implementation.
- Uses Spec Kit, project files, cited research, and deterministic tables or simulations; it does not edit gameplay code or production assets unless separately assigned.
- Remains read-only unless explicitly assigned a specification edit.

### Software Architect

- Validates system boundaries, dependency direction, data ownership, contracts, failure modes, security boundaries, operability, and evolution cost.
- Checks `plan.md`, `research.md`, data models, contracts, and architecture diagrams against the constitution and spec.
- Keeps domain and simulation rules in independently testable plain C# wherever practical. Unity APIs belong at presentation, input, editor, asset, and infrastructure boundaries.
- Uses Unity MCP primarily for inspection and validation; architectural decisions must remain understandable from repository artifacts.
- Rejects unnecessary infrastructure and undocumented coupling.
- Remains read-only during review; architectural edits require a separate bounded assignment.

### Gameplay Developer

- Owns assigned C# gameplay, grid, factory, logistics, simulation, progression, persistence, input integration, and automated tests.
- Implements against the approved architecture and may propose a deviation only with written rationale; material behavioral or architectural consequences require a human decision.
- May use Unity MCP, Git, and test tooling within the assignment. It must not alter the art direction or generate unrelated assets.
- Implements only the assigned GitHub Issue or work unit.
- Must document state ownership, save compatibility, contract changes, deterministic behavior, and error handling.

### UI/UX Developer

- Owns assigned Unity UI, HUD, touch and pointer interaction, navigation, feedback, layout, UI presentation code, and UI tests.
- Implements normal, loading, disabled, empty, failure, and recovery states defined by the spec across supported screen sizes and input methods.
- Consumes gameplay state through explicit application contracts rather than embedding economy or simulation rules in views.
- May implement or review UI, but an agent that wrote the UI cannot provide its independent UI/UX approval.

### UI/UX Reviewer

- Reviews information hierarchy, build and upgrade flows, touch targets, feedback, visual consistency, accessibility, focus/navigation, and behavior across supported screen classes.
- Uses the spec's player journeys and acceptance criteria as authority and reports the exact screen/state, expected behavior, and acceptance impact.
- Remains read-only and must be independent from the UI/UX Developer whose work it approves.

### Online Services Engineer

- Activated only for a feature with a remote API, account, cloud save, analytics, live operations, or other online service.
- Owns assigned service contracts, persistence, validation, authorization, concurrency, migrations, observability, and service tests.
- Must document trust boundaries, compatibility, failure behavior, retry/idempotency rules, and the client contract.

### Test and QA Engineer

- Builds a risk-based test matrix from acceptance criteria, contracts, edge cases, and changed boundaries.
- Verifies the committed artifact independently, including negative paths and regression risk.
- Uses `$speckit-maqa-qa` for MAQA commits and returns precise PASS or FAIL evidence.
- Does not repair the implementation while acting as QA; failures return to the assigned implementation agent.

### Game Systems and Economy Designer

- Owns progression pacing, resource loops, production formulas, upgrades, unlocks, offline progress, numerical balance, and anti-exploit invariants.
- Requires formulas and tuning parameters to be explicit, testable, and separated from presentation code.
- Reviews telemetry needs and identifies where balancing requires real player data instead of invented certainty.
- Remains read-only unless explicitly assigned domain-model or balancing-data changes.

### Art Director

- Owns the Art Bible, perspective, palette, shape language, silhouettes, visual noise limits, grid readability, state readability, scale, lighting, and consistency rules.
- Uses references and optional local image-generation tools to explore directions, then freezes an approved Art Bible version before production assets scale.
- Reviews production assets against the approved Art Bible and does not silently introduce a new style.
- Does not modify gameplay code or perform routine production-asset generation while acting as reviewer.

### Asset Artist

- Produces assigned buildings, icons, resources, regions, effects, and source artwork from a structured asset brief.
- Every brief identifies the asset ID and type, grid footprint, perspective, ports, required visual states, target sizes, file format, source provenance, and Art Bible version.
- May use an approved ComfyUI workflow or other assigned local art tooling. Generated output is a source candidate until Art Director review passes.
- Writes only to the assigned source-asset paths and does not configure gameplay behavior or Unity import settings.

### Technical Artist

- Converts approved source assets into Unity-ready sprites, atlases, animations, materials, prefabs, sorting layers, pivots, compression settings, and data references.
- Verifies grid footprint, input/output port readability, active and power states, naming, import settings, memory cost, and prefab validity.
- Owns assigned asset-import and prefab paths through Unity MCP or repository-native Unity tooling; it does not change economy rules.
- Preserves source files and provenance so imported assets can be reproduced.

### Performance Engineer

- Defines credible factory and device workloads, then measures simulation time, frame time, allocations, garbage collection, memory, loading, rendering, and asset cost.
- Uses Unity Profiler, deterministic simulation fixtures, test builds, and Unity MCP when available.
- Reports the measured problem, evidence, likely cause, realistic options, expected gain, complexity, and recommendation before optimization work is assigned.
- Remains read-only during profiling review unless explicitly assigned an approved optimization.

### Sound Designer

- Owns machine loops, belts, construction, upgrades, UI feedback, ambience, transitions, loudness consistency, looping quality, variation, and import-ready audio.
- Uses local tools and licensed libraries first. External generation services are optional dependencies that require an explicit project decision and recorded provenance.
- Writes only to assigned audio source/import paths; gameplay triggers and mixing behavior remain explicit implementation tasks.

### Composer

- Activated after the core factory experience and regional progression are stable.
- Owns music themes, layers, transitions, looping, intensity/progression mapping, and musical consistency by region or progression phase.
- Uses local tools and licensed sources first; any hosted generation fallback requires the same provenance, license, cost, and review rules as other generated content.
- Does not change gameplay pacing to fit a composition without Game Designer/Product Owner approval.

### 3D and Render Artist

- Optional later-stage role for assets that benefit from a standardized 3D-to-2D pipeline.
- Uses fixed camera, scale, lighting, material, shadow, and render settings so generated sprites share perspective and proportions.
- Blender automation is allowed only in an isolated, explicitly assigned workflow because it executes generated Python code; rendered output still requires Art Director and Technical Artist review.

### Security and Privacy Reviewer

- Reviews trust boundaries, authentication, authorization, input validation, secrets, persistence integrity, abuse paths, dependency risk, and privacy-sensitive data.
- Produces concrete threat scenarios tied to affected contracts and paths.
- Remains read-only and independent from the implementation agent whose work is being reviewed.

### DevOps and Operations Engineer

- Reviews build reproducibility, CI, environments, deployment, configuration, logs, metrics, backups, recovery, migrations, and rollback behavior.
- Requires operational tasks for any new runtime service, datastore, scheduled job, or external dependency.
- Owns infrastructure changes only when they are explicitly specified and assigned.

## Role activation by project phase

- First playable and version 0.1: use Game Designer/Product Owner, Software Architect, Gameplay Developer, UI/UX, Art Director, Asset Artist, Technical Artist, and Test/QA as the core roster. Game Systems/Economy remains a distinct review lens for factory math and progression.
- Performance Engineer is review-driven: activate it for simulation scale, large factories, rendering/asset budgets, mobile targets, loading, or measured regressions.
- Sound Designer joins when representative factory scenes exist. Composer joins after the core loop and regional progression are stable.
- 3D/Render Artist is deferred until a proven need for consistent multi-angle or 3D-to-2D production outweighs pipeline cost.
- Online Services, Security/Privacy, and DevOps/Operations activate when a feature introduces their boundaries. Do not add cloud, analytics, hosted generation, or deployment systems merely to keep a role busy.

## Required multi-agent review waves

For a non-trivial Unity feature, use these waves while respecting `max_parallel: 3`:

1. Product discovery: Game Designer/Product Owner, Game Systems/Economy, and UI/UX inspect the proposed player experience in parallel. Include Art Direction when visual language or new asset families are affected.
2. Technical and production design: Software Architect, Test/QA, and the primary implementation specialist inspect the plan. Add Technical Art for asset/import work, Performance for scale-sensitive systems, and Security/Operations for their risk areas.
3. Content production: Art Director approves the versioned brief or Art Bible; Asset, 3D/Render, or Audio production follows in bounded paths; Technical Art integrates only approved source assets.
4. Implementation: Gameplay, UI, Technical Art, content, online-service, and infrastructure agents may work concurrently only on independent Issues/worktrees with disjoint paths and explicit contracts.
5. Verification: Test/QA reviews the exact commit. Game Designer/Product Owner rechecks acceptance, Software Architect rechecks boundaries, and each applicable specialist independently reviews its domain.

Game Designer/Product Owner, Software Architect, and Test/QA gates remain mandatory for every non-trivial feature. Game Systems/Economy is mandatory for production, upgrade, unlock, reward, offline-progress, or balancing changes. UI/UX is mandatory for player-visible interaction. Art Direction is mandatory for new or changed visual assets; Technical Art is mandatory when assets, prefabs, animation, import settings, or atlases change. Performance is mandatory for hot paths or scale targets. Security is mandatory for trust boundaries or persisted player data. DevOps is mandatory for builds, automation, online runtime, or migrations. State which optional roles were omitted and why.

## Token-efficient agent and model routing

Use the least expensive model and reasoning level that safely covers the assignment. Model choice never removes a required review, test, or independence gate.

| Tier | Default reasoning | Codex model | Claude Code model | Appropriate work |
|---|---|---|---|---|
| Low | `low` | `GPT-5.6 Luna` | `haiku` (Haiku 4.5) | Deterministic extraction, issue/worktree reconciliation, narrow documentation changes, checklist validation, test-log summarization, and read-only reviews with explicit criteria and small diffs |
| Medium | `medium` | `GPT-5.6 Sol` | `sonnet` (Sonnet 5) | Default implementation, focused debugging, ordinary Unity/domain/UI work, Test/QA, and bounded specialist reviews |
| High | `high` | `GPT-6 Astra` | `opus` (Opus 5.5) | Cross-boundary architecture, persistence/security/concurrency, ambiguous systemic failures, migrations, or remediation that remains unresolved after Medium at `high` reasoning |

The parent agent uses the model column of its own runtime. Where a runtime cannot set reasoning per delegated agent, the tier's model choice applies and reasoning escalation is recorded in the assignment instead.

- Start at the listed default. Raise Medium to `high` reasoning before moving to the High tier; use High at `xhigh` only when the parent records a concrete unresolved risk. Do not use `max` by default.
- Route by the actual Issue, not by role prestige. A mechanical architecture checklist may use Low; a subtle deterministic-simulation implementation may require Medium at `high`; the High tier remains exception-driven.
- Resume the same worker for bounded QA fixes so it reuses established context. Do not start a fresh high-cost agent unless the prior context is unreliable or independence is required.
- The parent reads shared artifacts once and sends a compact evidence packet: exact task text, relevant requirement/contract sections, changed paths, commands, and known risks. Do not paste whole specs or plans when path-and-section references or short excerpts suffice.
- Review the exact commit and changed contracts first. Expand to surrounding code only when a dependency, public boundary, or finding requires it. Do not ask multiple agents to rediscover the same repository facts.
- Keep Game Designer/Product Owner, Software Architect, and Test/QA verdicts independent. Compatible optional lenses may share one read-only review when independence is preserved, for example Product plus Game Systems on a small economy invariant.
- Use deterministic tools before model reasoning: repository searches, compiler/test output, schema validation, Git ancestry, and MCP inspection. Return concise structured evidence and omit narrative already present in the handoff.
- Escalate model or reasoning only after recording the concrete ambiguity, failed attempt, or risk that the lower tier could not resolve. After the high-risk decision is settled, route bounded implementation and verification back to the Medium or Low tier where appropriate.

## Implementation lens matrix

Every implementation assignment has one primary owner and explicit secondary review lenses:

| Primary implementation | Required secondary lenses |
|---|---|
| Gameplay/domain C# | Software Architect, Test/QA, Game Systems for economy logic, Performance for hot paths, Security for persistence/trust boundaries |
| Unity UI | UI/UX Reviewer, Test/QA, Game Designer/Product Owner, Art Director for visual-language changes |
| Art source asset | Art Director, Technical Artist, Game Designer/Product Owner for gameplay meaning |
| Asset import, animation, or prefab | Technical Artist, Art Director, Test/QA, Performance when memory/render cost matters |
| Sound or music | Sound Designer or Composer, Game Designer/Product Owner, Technical Artist, Test/QA |
| Online service | Software Architect, Security/Privacy, DevOps/Operations, Test/QA, Gameplay/UI contract consumer |
| Infrastructure/build | DevOps/Operations, Software Architect, Security/Privacy, Test/QA |
| Performance optimization | Performance Engineer, Software Architect, affected implementer, Test/QA |

Secondary lenses review the committed result and return findings. They do not edit the same worktree concurrently.

## Tool and permission boundaries

Grant each assignment the smallest useful tool and write scope. Installing a Unity package does not prove that its MCP server is connected to the active agent runtime; verify tool availability before dispatch and record the fallback command when it is unavailable.

Local Unity, ComfyUI, and Blender readiness is tracked in `docs/integrations/mcp-readiness.md`. Before assigning work that names one of these MCPs, verify a real tool call in the target agent's current session. When Unity MCP is available, prefer its focused scene, hierarchy, serialized-object, importer, test-status, and console inspection tools over broad file/log ingestion; this is both more authoritative and more token-efficient. Use the repository-owned CLI for complete reproducible suites and as the explicit fallback when the current session lacks Unity tool bindings. ComfyUI and Blender are required gates for assignments whose approved asset brief selects those workflows; do not silently replace them with another generator. Their absence does not block unrelated gameplay/domain work.

| Role | Normal tools and access | Prohibited while in that role |
|---|---|---|
| Game Designer/Product Owner | Spec Kit, specs, wiki, cited research, tables and simulations | Gameplay code, production assets, Unity project mutation |
| Software Architect | Repository read access, diagrams, Unity inspection, test/build evidence | Unassigned production edits; approving its own implementation |
| Gameplay Developer | Assigned C# and tests, Unity MCP or Unity CLI, Git | Art-style decisions, unrelated assets and settings |
| UI/UX Developer | Assigned UI assets/scripts/scenes and UI tests, Unity tooling | Economy/domain rules; independent approval of its own UI |
| Art Director | Art Bible, references, review artifacts, optional ComfyUI experiments | Gameplay code and routine production-asset integration |
| Asset/3D/Audio production | Assigned source-asset directories and approved generation/editing tools | Code, economy rules, unapproved style changes |
| Technical Artist | Assigned Unity asset, import, animation, prefab, atlas, and definition paths | Economy changes and unreviewed source generation |
| Test/QA and Performance | Committed source, Unity tests, builds, console, profiler, simulations | Repairing the implementation during independent review |
| Online Services/DevOps | Explicitly assigned service or automation paths and environments | Unity gameplay or content outside the contract |

ComfyUI, Blender, hosted audio generation, and other external providers are optional capabilities, not project prerequisites. Use them only after the relevant workflow, model/provider, license/provenance rule, cost boundary, and output path are approved. Never expose repository secrets or grant every agent every available MCP.

### Shared workstation resources

The workstation has one Unity editor slot and one Android emulator (`emulator-5554`). Sessions and MAQA workers that used them in parallel corrupted each other's evidence. Unity CLI runs and builds whose active build target is Android also end with "Killing ADB server", which takes the emulator offline. Both resources are therefore leased through `scripts/resource-lease.ps1`. A lease is an atomically created file in the Git common directory, so every worktree and session of this clone sees it.

- Before any Unity editor session, Unity CLI test run or build, or emulator/`adb` work, acquire the resource: `-Acquire -Resource unity|emulator -Owner <issue-n or session name> -Purpose <text> -TtlMinutes <n>`. When a Unity run may have Android as its active target, which is true for any worktree whose `Library/` came from an Android build, acquire both resources in one call: `-Resource unity,emulator`.
- Exit code 2 means another owner holds the resource. Wait, or ask that holder; never delete a live lease or take it over. An expired lease counts as free, and the takeover is reported.
- Release as soon as the work ends: `-Release -Resource ... -Owner ...`. Acquiring again as the same owner renews the lease. Use `-Status` to see who holds what.
- In the MAQA lane, the parent acquires the leases a dispatched Issue needs, with owner `issue-<n>`, before the `SPAWN`. The parent releases them after QA and merge, or when the Issue is blocked. GitHub labels `resource:unity` and `resource:emulator` mark which Issues need which lease. Issues without these labels, such as plain-C# domain work or documentation, may run in parallel up to `max_parallel`.
- `scripts/android-emulator-journey.ps1` acquires the emulator lease for its run with `-LeaseOwner`. It renews a lease its owner already holds and stops before any `adb` call when someone else holds it.

## Assignment contract

Every delegated assignment must include:

- role and review lens;
- complexity tier, selected model/reasoning level, and any escalation rationale;
- authoritative issue or work-unit identifier;
- allowed scope and paths;
- allowed tools/MCPs and explicit forbidden tools or mutations;
- required source artifacts;
- acceptance criteria and relevant constitution rules;
- dependencies and forbidden overlaps;
- Unity version, target platform/device class, and Edit Mode/Play Mode validation required;
- Art Bible version and structured asset brief for visual work;
- workload, baseline, metric, and target for performance work;
- targeted validation commands;
- expected output format and whether the assignment is read-only or may edit.

Agents return changed paths, tests/checks, evidence against acceptance criteria, unresolved risks, and a clear done/blocked verdict. The parent verifies all results before accepting them.

## Unity architecture and asset rules

- Unity `6000.6.0f1` is pinned in `ProjectSettings/ProjectVersion.txt`. A version change requires an explicit plan and compatibility verification.
- Prefer `Assets/Scripts/Domain/`, `Simulation/`, `Application/`, `Presentation/`, and `Persistence/` as responsibility boundaries when the feature plan confirms they fit. Domain and simulation assemblies must remain independently testable without scenes.
- MonoBehaviours, views, input handlers, animation events, and editor tooling must delegate gameplay decisions to application/domain operations.
- ScriptableObjects may hold authored definitions and tuning data; mutable runtime state must have an explicit owner and must not silently mutate source assets.
- Commit `Assets/` with every required `.meta` file, `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings/`. Never edit or commit generated `Library/`, `Temp/`, `Logs/`, `Obj/`, or `UserSettings/`.
- Scene, prefab, material, animation, and importer changes must be made through Unity tooling where practical and reviewed for unintended serialized changes.
- Unity builds and test runs re-serialize known noise into `ProjectSettings/`: empty iOS icon slots, an explicit automatic Graphics API entry, a leaked GLES3 override, and Enter Play Mode options. `scripts/check-settings-noise.ps1` detects each signature a change introduces. The `.githooks/pre-commit` hook runs it on staged changes; enable the hook once per clone with `scripts/install-git-hooks.ps1`. MAQA QA also runs it with `-Commit <sha>` on the exact assignment commit, because a worker may commit without the hook. Commit a deliberate change to one of these settings only with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1` and a recorded decision.
- Gameplay/economy rules require Edit Mode tests. Scene, prefab, UI, input, save/load, and runtime integration require Play Mode tests when the behavior cannot be proven below the engine boundary.
- On this Windows Codex host, Unity CLI starts in the normal sandbox have repeatedly failed before test discovery with a UPM database-open error and local Package Manager IPC timeout; the same runner passed with local IPC access after a narrowly approved, per-command sandbox escalation. A process-scoped `UPM_CACHE_ROOT` did not resolve the restricted-sandbox failure. For Unity CLI tests/builds that need UPM, complete the usual checkout/editor ownership preflight and request the narrow command-level escalation on the first invocation instead of retrying once in the restricted sandbox. Keep the general sandbox enabled; do not reset or rewrite UPM caches as a workaround. This execution escalation is distinct from the Windows sandbox implementation setting `elevated`/`unelevated`. If escalation is unavailable, use Unity MCP only after verifying it is connected to the exact target checkout, or report the blocker; do not describe a pre-discovery startup failure as a test failure.
- Generated visual or audio files must record source/provenance, license, generator/workflow version, transformation steps, and the approved brief. They are not production-ready until specialist review and Unity integration checks pass.

## Implementation lane rules

Choose exactly one implementation lane for a feature:

- Direct lane: use the work-unit ledger with `$speckit-implement`. Use `$speckit-checkpoint-commit` at reviewed, coherent checkpoints. `$speckit-implement` dispatches the mandatory `after_implement` hooks itself.
- MAQA lane: use `$speckit-maqa-sync`, `$speckit-maqa-coordinator`, `$speckit-maqa-feature`, and `$speckit-maqa-qa`. MAQA workers create their required assignment commit; do not run the checkpoint skill inside a MAQA worker. MAQA never passes through `$speckit-implement`, so the parent runs the mandatory `after_implement` hooks itself after each merged batch and before `$speckit-converge`: `$speckit-wiki-ingest specs/<feature>` and then `$speckit-learn-review`. Workers and QA agents never run these hooks.
- MAQA workers do not tick `tasks.md` checkboxes. Before the hooks run, the parent mirrors Issue state into them with `scripts/maqa-sync-task-checkboxes.ps1 -TasksFile specs/<feature>/tasks.md`. A closed Issue is a verified merge, which satisfies the ledger's rule that checkboxes are ticked only after parent verification. The mirrored checkboxes are derived data. GitHub Issues stay the status authority, and neither the coordinator nor `$speckit-maqa-sync` reads checkbox state.
- After merges, the parent runs `scripts/maqa-stale-worktrees.ps1`. The report is read-only and lists, for each linked worktree, whether it is merged, dirty, and what state its MAQA Issue is in. For worktrees that are merged, clean, and whose Issue is closed, it prints the non-forcing removal commands (`git worktree remove`, `git branch -d`). The parent reviews these and hands them to the user when auto mode forbids deletion. Worktrees of other tools (`.codex`, `.kilo`) and branches that were re-committed or rebased stay REVIEW.
- Before `$speckit-converge`, the parent runs `scripts/check-after-implement-hooks.ps1 -FeatureDir specs/<feature>`. The gate checks four things: the hooks are still mandatory, `learn.md` and the feature's wiki ingest are newer than the last implementation commit, the wiki was linted after that ingest, and the checkboxes match the Issues. A failed gate blocks convergence and feature completion.

Do not run `specify workflow run speckit` for a MAQA feature because the bundled workflow proceeds directly from tasks to core implementation.

## Windows compatibility rules

- GitHub CLI and GitHub Git operations must use direct access when the agent shell inherits the failing `http://127.0.0.1:9` proxy. Follow [`docs/integrations/github-cli.md`](docs/integrations/github-cli.md); clear proxy variables only for the command process and verify `origin` before MAQA operations.
- Always pass the explicit `specs/<feature>/tasks.md` path to the orchestration prepare and validate skills. Their no-argument fallback points to a Bash script not installed in this PowerShell project.
- When `$speckit-maqa-sync` references the missing `.specify/extensions/maqa/scripts/powershell/check-prerequisites.ps1`, use the equivalent installed core script `.specify/scripts/powershell/check-prerequisites.ps1` with `-Json -RequireTasks -IncludeTasks`.
- Atlas scripts live under `.specify/extensions/atlas/skill/scripts/` and must run through `uv` with the dependencies declared by the Atlas skill.

## Knowledge and documentation

- Query the wiki before specifying work in an established domain.
- Ingest verified planning decisions after planning and implementation learnings after implementation.
- Generate learning diagrams after the plan stabilizes and `learn.md` after implementation.
- Keep the approved Art Bible, asset briefs, generation provenance, audio rules, performance budgets, profiling reports, and Unity import conventions in versioned project artifacts and ingest durable decisions into the wiki.
- Freeze versioned Art Bible rules before producing an asset family. A style change requires a new approved version and an explicit migration decision for existing assets.
- Run Atlas Storybook at milestones for this single repository. Use Atlas Map only when a real multi-repository workspace exists.
- GitHub Project #4 mirrors one item per MAQA Issue through `scripts/maqa-board-mirror.ps1`, which implements the coordinator's `board_mirror` step. The parent calls `-Issue <n> -Status InProgress|InReview|Done` only after a verified coordinator transition, and `-Backfill` rebuilds the whole board from Issue state. Mirror failures are warnings and never change MAQA state. Never read the board as workflow status. Do not run `$speckit-maqa-github-projects-populate`: its per-feature draft card would duplicate the per-Issue items.

## Completion gate

A feature is complete only when acceptance criteria pass, configured Unity Edit Mode and Play Mode tests pass, the Unity console contains no relevant errors, QA passes on the committed result, convergence reports no remaining work, and the parent verifies the final repository state. Player-visible work also requires independent UI/UX acceptance; visual/audio work requires the applicable Art, Audio, and Technical Art evidence; performance-sensitive work requires measured results against its declared workload and target. Relevant knowledge and production rules must be ingested into the wiki, and the mandatory `after_implement` hooks (`$speckit-wiki-ingest`, `$speckit-learn-review`) must have run on the final implementation, with `specs/<feature>/learn.md` current.
