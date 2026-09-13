# Implementation Plan Request — Feature 001: Core Game Foundation

Create the implementation plan for **Feature 001 – Core Game Foundation**.

The existing feature specification and project constitution are authoritative.

The goal of this planning step is to translate the functional requirements into a concrete technical architecture and implementation strategy.

Do not expand the feature scope beyond the existing specification.

---

# Technology Stack

Use the following technology stack and constraints:

* Unity 6
* C#
* 2D game
* Mobile-first
* Android and iOS as primary targets
* Future Windows/Steam support must remain possible
* Git for version control
* Unity Test Framework / NUnit for automated tests

Do not introduce a backend for this feature.

Do not introduce multiplayer.

Do not introduce DOTS/ECS unless there is a demonstrated requirement that cannot reasonably be satisfied with a simpler architecture.

Do not introduce a dependency injection framework unless the plan identifies a concrete problem that requires one.

Prefer Unity built-in functionality and plain C# over unnecessary third-party dependencies.

---

# Primary Architectural Goal

The authoritative game simulation must be clearly separated from Unity presentation.

Unity GameObjects, MonoBehaviours, Transforms, Sprites, animations and views must not become the authoritative representation of factory state.

The preferred architecture should conceptually separate:

```text
Domain
Simulation
Application
Persistence
Presentation
```

The exact number of assemblies, folders or namespaces should be chosen pragmatically.

Avoid creating architectural layers that currently provide no value.

The core requirement is clear dependency direction and separation of responsibilities.

---

# Dependency Direction

Prefer dependencies conceptually similar to:

```text
Presentation
     ↓
Application
     ↓
Simulation / Domain
```

Persistence may translate between durable save representations and runtime state but must not own gameplay rules.

The Domain and Simulation layers should avoid direct dependencies on Unity-specific presentation APIs wherever practical.

Core simulation behavior should be testable without requiring a rendered Unity scene.

---

# Domain Model

Plan a minimal domain model covering the concepts required by Feature 001.

At minimum consider:

* Factory
* Factory Grid
* Grid Position
* Building Definition
* Building Instance / State
* Resource Definition
* Resource Quantity
* Inventory / Resource Buffer
* Recipe
* Production State
* Production Progress

Use stable identifiers for content that may be referenced by:

* savegames,
* recipes,
* building definitions,
* future progression systems.

Avoid hard-coding gameplay content directly into generic simulation logic.

---

# Building Definitions vs Runtime State

The plan must explicitly separate static content definitions from mutable runtime state.

Static definitions may describe:

* building ID,
* building size,
* category,
* available recipes,
* presentation metadata,
* capabilities.

Runtime state may describe:

* unique building instance ID,
* grid position,
* orientation,
* current inventory,
* production progress,
* operational status.

Unity ScriptableObjects may be used for editor-friendly static content authoring if appropriate.

However:

> ScriptableObjects must not be used as mutable authoritative runtime state for placed factory buildings.

Clearly describe how static definitions are resolved by stable ID at runtime.

---

# Grid Architecture

Plan a logical 2D factory grid independent from visual world coordinates.

The grid should support:

* logical X/Y positions,
* occupancy queries,
* placement validation,
* buildings occupying multiple cells,
* future extensions for terrain,
* future resource deposits,
* future logistics infrastructure.

Do not use Unity Transform positions as authoritative logical grid state.

The plan should explain how logical grid coordinates map to presentation coordinates.

Do not hard-code a small maximum map size unless technically necessary.

If chunking is not required for Feature 001, avoid implementing it prematurely, but do not design the grid in a way that prevents future chunking.

---

# Placement Flow

Plan building placement through an explicit application-level operation rather than allowing views to directly mutate the grid.

A conceptual flow may be:

```text
Player Input
↓
Placement Request
↓
Placement Validation
↓
Game State Mutation
↓
Presentation Update
```

The plan should cover:

* valid placement,
* invalid placement,
* occupancy,
* multi-cell footprints,
* orientation preparation,
* deterministic resulting state.

The final production-grade building UX is outside scope.

---

# Resource Model

Resources must be data-driven and identified through stable IDs.

Do not create generic production logic based on explicit checks such as:

```text
if resource == IronOre
```

The plan should establish a reusable representation for:

* resource identity,
* resource quantity,
* future presentation metadata.

Placeholder resources may be created exclusively for validation and tests.

---

# Inventory Model

Plan a reusable inventory/resource-buffer abstraction.

It must support:

* finite capacity,
* querying current quantity,
* querying available capacity,
* adding resources,
* removing resources,
* rejecting invalid operations.

The abstraction should be suitable later for:

* machine input buffers,
* machine output buffers,
* storage buildings.

Do not overgeneralize into a complex universal inventory system unless required.

---

# Recipe Model

Production must be data-driven.

Plan a recipe representation supporting:

* multiple inputs,
* multiple outputs,
* production duration or production rate,
* stable recipe identity where useful.

A machine should execute a recipe rather than contain hard-coded transformations.

For example, prefer:

```text
Machine
→ Recipe
→ Input Requirements
→ Output Results
```

over machine-specific conditional production code.

---

# Production Simulation

Plan a minimal deterministic production simulation.

The initial implementation must support:

* checking required input,
* tracking production progress,
* consuming input,
* producing output,
* detecting full output capacity,
* stopping when blocked,
* resuming after the blocking condition disappears.

The plan must clearly define when input is consumed.

For example:

* at production start,
* progressively,
* or at cycle completion.

If this decision affects gameplay semantics and is not defined by the existing feature specification, flag it as a Human-in-the-Loop decision rather than silently choosing arbitrary behavior.

---

# Simulation Timing

Rendering and simulation must be independent.

Do not implement authoritative production directly through per-frame MonoBehaviour logic.

The simulation must support explicit advancement by elapsed logical time.

Conceptually:

```text
AdvanceSimulation(deltaTime)
```

The exact internal strategy may use:

* fixed simulation ticks,
* event-based advancement,
* or another deterministic approach.

For Feature 001, prefer the simplest approach that:

* is deterministic,
* is testable,
* supports future offline advancement,
* and does not require one Unity Update call per building.

Document why the selected strategy is appropriate.

---

# Simulation Performance

Do not prematurely optimize.

However, avoid designs where:

* every resource item becomes a simulated GameObject,
* every belt item would require independent physical simulation,
* every future factory building must run its own Unity Update method.

The simulation should operate on logical state.

Visual resource movement will later be presentation only.

Include likely future scaling risks in the plan, but do not solve hypothetical problems without evidence.

---

# Offline Progression Compatibility

Full offline progression is not part of Feature 001.

However, the chosen simulation architecture must support future calls conceptually similar to:

```text
Advance factory by 8 hours.
```

Do not design the simulation such that offline progress would require replaying rendered frames.

Document how the chosen simulation model can later evolve toward:

* storage-fill events,
* blocked production,
* long elapsed-time advancement,
* production network calculations.

Do not implement the full future solver now.

---

# Persistence

Plan an explicit savegame architecture.

Do not use PlayerPrefs as primary gameplay persistence.

The save format must include a version.

Runtime domain objects and persistent representations should be separated when that improves migration safety and maintainability.

Plan support for future save migrations.

For Feature 001 only the first save version and basic load/save functionality are required.

The plan should cover persistence of at least:

* building instances,
* grid positions,
* orientation if relevant,
* inventories,
* production progress,
* production status where necessary,
* foundational factory state.

Avoid serializing Unity scene hierarchy as the authoritative save.

---

# Save Migration Preparation

Plan a migration entry point so future versions can follow a pattern such as:

```text
V1
→ Migration
→ V2
→ Migration
→ V3
```

Do not build speculative migrations now.

Only establish the structure needed so migration support does not require replacing the persistence architecture later.

---

# Presentation Layer

Plan a thin presentation layer that reflects domain state.

Conceptually:

```text
BuildingState
→ BuildingView
```

Presentation objects may:

* show sprites,
* animate production,
* show selection state,
* show placement previews,
* display debugging information.

Presentation objects must not become the source of truth for:

* building existence,
* inventories,
* production,
* placement,
* saved state.

It should be possible to recreate views from logical state.

---

# Input

Use Unity's current input solution suitable for:

* touch,
* mouse,
* future desktop controls.

Do not embed raw input handling into domain logic.

Plan a translation such as:

```text
Raw Input
↓
Interaction / Input Layer
↓
Application Command
↓
Game State
```

Feature 001 only needs enough interaction for:

* camera movement,
* zoom,
* basic placement validation/testing.

Final building UX belongs to a later feature.

---

# Camera

Plan a simple mobile-friendly 2D camera supporting:

* pan,
* zoom,
* touch,
* mouse during development.

Do not allow camera state to influence gameplay simulation.

Keep the implementation replaceable enough for future UX iteration.

---

# Testing Strategy

Testing is mandatory.

The plan should define which tests belong in:

* pure domain/unit tests,
* simulation tests,
* Unity integration or Play Mode tests.

Prefer Edit Mode / plain C# tests for domain behavior where possible.

At minimum plan automated coverage for:

* valid placement,
* invalid overlapping placement,
* multi-cell placement,
* inventory addition,
* inventory removal,
* capacity enforcement,
* insufficient resource removal,
* production with sufficient input,
* production without input,
* production with full output,
* production resume,
* deterministic time advancement,
* save/load equivalence,
* save version handling.

The plan should make it easy to run simulation tests without launching a full playable scene.

---

# Debugging / Developer Visibility

Plan lightweight observability for foundational systems.

Developers must be able to inspect why a machine is:

* running,
* waiting for input,
* blocked by output,
* inactive.

The plan may include:

* inspector/debug views,
* structured logging,
* debug overlays,
* development-only tooling.

Do not build a large debug framework unless justified.

---

# Project Structure

Propose a clear Unity project structure.

A possible conceptual direction is:

```text
Assets/
  Game/
    Domain/
    Simulation/
    Application/
    Persistence/
    Presentation/
    Content/
    Tests/
```

This is only a starting suggestion.

Choose a structure appropriate for Unity and explain meaningful deviations.

Also determine whether Assembly Definitions should be introduced now.

If used, explain their dependency relationships.

If they would add unnecessary friction at this stage, defer them explicitly.

---

# Content Authoring

Plan how placeholder content definitions for:

* resources,
* recipes,
* buildings

will be authored and referenced.

The solution should support future balancing without requiring gameplay-code changes for every content adjustment.

Placeholder content must be clearly separated from finalized game-design values.

---

# Asset Strategy for Feature 001

Use placeholders.

Do not delay implementation waiting for production art or audio.

Feature 001 should use simple temporary assets sufficient to validate:

* placement,
* visibility,
* selection,
* machine state.

The broader AI-assisted art/audio pipeline is not required for this feature.

ComfyUI, audio-generation tools and final Technical Art workflows should not become implementation dependencies for Feature 001.

---

# MCP / Agent Tooling

The development workflow may use an available Unity MCP for editor interaction, test execution and project inspection.

However:

* MCP tooling is development infrastructure,
* it is not part of runtime architecture,
* the game must never depend on an MCP being available to run.

Agents must respect their assigned roles and the project constitution.

Tool convenience must not override architectural quality.

---

# Human-in-the-Loop Decisions

Before finalizing the plan, identify any unresolved decision that could materially affect future gameplay or architecture.

Do not silently invent game-design rules.

In particular review whether clarification is required for:

* when production inputs are consumed,
* orientation requirements for buildings,
* whether inventories have global capacity or per-resource capacity,
* how partial production progress behaves when blocked,
* what happens when a save occurs during an active production cycle,
* whether rotation is required already in Feature 001.

For each unresolved material decision:

1. explain the decision,
2. present realistic alternatives,
3. describe consequences,
4. provide a recommendation,
5. request human input if required.

Routine implementation details do not require Human-in-the-Loop approval.

---

# Explicit Non-Goals

Do not include implementation of:

* final conveyor system,
* belt routing,
* individual visual belt items,
* resource deposits,
* regional gameplay,
* electricity,
* research,
* prestige,
* expeditions,
* roguelite systems,
* monetization,
* premium currency,
* cloud saves,
* user accounts,
* achievements,
* analytics,
* production balancing,
* final resources,
* final machine catalogue,
* final art,
* final sound,
* backend services.

Architecture may anticipate these features only where necessary to avoid obvious dead ends.

Do not build speculative frameworks for them.

---

# Planning Deliverables

The resulting plan should clearly describe:

1. proposed project structure,
2. domain concepts,
3. dependency boundaries,
4. grid representation,
5. building definition/state model,
6. resource model,
7. inventory model,
8. recipe model,
9. production simulation strategy,
10. time advancement strategy,
11. persistence strategy,
12. save-version and migration preparation,
13. input and presentation boundaries,
14. testing strategy,
15. placeholder validation scene,
16. expected implementation sequence,
17. identified architectural risks,
18. Human-in-the-Loop decisions,
19. Constitution compliance.

Include diagrams or pseudocode where they materially improve understanding.

Prefer concrete architecture over generic recommendations.

Do not implement code during the planning step.

---

# Plan Success Criteria

The plan is successful when it provides a clear path to implementing Feature 001 such that:

* factory state exists independently from visuals,
* buildings can occupy logical grid space,
* resources and finite storage exist,
* recipes define production,
* production advances deterministically over time,
* production can become blocked and resume,
* game state can be saved and restored,
* the system can later support offline progression,
* the system can later support large factories,
* foundational behavior is automatically testable,
* and future gameplay systems can build upon the foundation without unnecessary rewrites.

Prefer the simplest architecture that satisfies these requirements and the project constitution.
