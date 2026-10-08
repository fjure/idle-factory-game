# Role Lenses, Review Waves, and Lens Matrix

This file is part of the agent working agreement in [`AGENTS.md`](../../AGENTS.md). References to the role lenses, role activation, review waves, implementation lens matrix, or per-role tool boundaries "in `AGENTS.md`" resolve here. The mandatory review gates are restated in `AGENTS.md` and bind every agent.

Read this file before writing an assignment, dispatching a reviewer, or planning review waves. A delegated agent receives its own role section in the assignment and does not need the rest.

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

## Tools by role

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
