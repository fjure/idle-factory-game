# Feature: Core Game Foundation

## Purpose

Establish the foundational game systems required for future development of the idle incremental factory game.

This feature represents the technical and functional groundwork of the game, but must remain independent of specific gameplay balancing, progression content and final game-design values.

The goal is to create a minimal but functional foundation upon which later feature specifications can safely introduce:

* factory construction,
* production chains,
* logistics,
* offline progression,
* research,
* power,
* regions,
* prestige,
* expeditions,
* monetization,
* and long-term progression.

This feature must not attempt to implement the complete game.

The project constitution is authoritative.

---

# Scope

The foundation should establish the basic concepts required to represent and manipulate a factory world.

At the end of this feature, it should be possible to create a minimal playable development scenario in which:

1. a player can navigate a factory area,
2. a building can be placed onto a logical 2D grid,
3. placed buildings persist as part of game state,
4. resources can exist within the game,
5. a simple production process can consume and produce resources,
6. production can advance over time,
7. production can stop when required conditions are not satisfied,
8. the game state can be saved and restored,
9. simulation behavior can be validated independently from visual presentation.

The purpose of this scenario is architectural and behavioral validation.

It is not intended to represent the final early-game experience.

---

# Factory World

The game is based around freely buildable two-dimensional factory areas.

The foundation must represent a logical factory grid.

The grid must allow the game to determine whether a location can contain a building.

The grid must support future factory elements such as:

* buildings,
* logistics infrastructure,
* resource deposits,
* terrain restrictions,
* and supporting infrastructure.

Not all of these systems need to be implemented during this feature.

The foundation must not make assumptions that prevent buildings from occupying multiple grid cells.

The foundation must not make assumptions that prevent factory areas from becoming significantly larger in future progression.

Exact region dimensions and expansion rules are outside the scope of this feature.

---

# Building Placement

The player must be able to place a development/test building onto the factory grid.

Placement must respect the logical occupancy of the factory.

A building cannot be placed where its required space is unavailable.

The game must clearly distinguish between:

* a type of building,
* and an individual placed instance of that building.

Placed buildings must have persistent identity.

At minimum, a placed building needs enough information for the game to determine:

* what it is,
* where it exists,
* how it is oriented if orientation is relevant,
* and its current operational state.

Removing and rebuilding structures will be specified in greater detail in a later factory-building feature.

---

# Resources

The foundation must introduce the concept of resources.

Resources represent materials that can later be:

* extracted,
* transported,
* stored,
* processed,
* consumed,
* produced,
* researched with,
* or used for progression.

Resources must have stable identities.

The foundation may introduce simple test resources to validate production.

These resources are development content only and must not establish final resource names, recipes or balancing.

---

# Resource Storage

The foundation must support storing resource quantities.

Storage must support finite capacity.

The system must be able to answer questions such as:

* How much of a resource is currently available?
* Can another amount be added?
* Can a requested amount be removed?
* Is available capacity exhausted?

This capability must be reusable for future concepts such as:

* machine input buffers,
* machine output buffers,
* storage containers,
* logistics systems.

Exact inventory and stacking rules remain outside the scope of this feature.

---

# Production

The foundation must demonstrate a minimal production process.

A production process may:

1. require one or more input resources,
2. require a period of production,
3. produce one or more output resources.

The game must be able to determine whether production can currently proceed.

At minimum, production must distinguish between relevant states such as:

* able to produce,
* missing required input,
* unable to store output,
* inactive.

Exact terminology may be refined later.

Production rules must be defined separately from an individual placed machine so that multiple machine instances can follow the same production behavior.

The production foundation must allow future expansion toward:

* different recipes,
* multiple inputs,
* multiple outputs,
* production-speed modifiers,
* machine upgrades,
* power requirements,
* and production bonuses.

Those advanced systems are not part of this feature.

---

# Time-Based Simulation

Factory production must advance based on elapsed game time.

Game simulation must not fundamentally depend on how frequently the game is visually rendered.

The foundation must support advancing production by an explicitly known amount of time.

For example:

> Given the current factory state and an elapsed period of time, determine the resulting factory state.

This capability is essential for future:

* offline progression,
* testing,
* speed modifiers,
* simulation tools,
* balancing tools.

The exact simulation frequency and optimization strategy are implementation concerns and must not be decided in this specification.

---

# Deterministic Behavior

Foundational factory simulation should produce predictable results.

Given equivalent starting state and equivalent inputs, the resulting state should be equivalent.

This is necessary so future systems can reliably support:

* automated testing,
* offline progression,
* save/load,
* debugging,
* balancing,
* and long-running simulations.

Any future mechanics involving randomness must make that randomness explicit.

Random gameplay is outside the scope of this feature.

---

# Production Blocking

Production must respect resource limitations.

A production process must not create resources when required input is unavailable.

A production process must not continue creating output indefinitely when available output storage has reached capacity.

This establishes an important future gameplay principle:

> Factory systems may become blocked because of resource-flow constraints.

The foundation must therefore allow production to stop and later resume when its blocking condition is resolved.

This behavior is necessary for future factory bottlenecks and logistics gameplay.

---

# Logistics Preparation

The final conveyor and logistics system is outside the scope of this feature.

However, the foundation must not assume that resources instantly teleport between arbitrary buildings.

Future specifications will introduce material transport with finite throughput.

The foundational production model must therefore allow future systems to control how quickly resources become available to consumers.

Future logistics will include concepts such as:

* source,
* destination,
* material flow,
* transport capacity,
* blocked routes,
* splitters,
* mergers.

Do not define their final behavior during this feature.

---

# Offline Progression Preparation

Full offline progression is outside the scope of this foundation.

However, the game state and production systems must support being advanced by elapsed time without requiring the player to actively view the factory.

This should make a later feature capable of evaluating scenarios such as:

> The player was away for eight hours.

> Production operated normally for two hours.

> Output storage then became full.

> Production remained blocked for the remaining six hours.

The foundation does not need to solve complex offline production networks yet.

It must only ensure that future offline progression can be implemented without replacing the core simulation model.

---

# Save and Restore

The player must be able to save the current foundational game state and restore it later.

The saved state must preserve enough information to reconstruct at least:

* placed buildings,
* building positions,
* building runtime state,
* stored resources,
* current production state,
* and other foundational game state introduced by this feature.

Loading a save must restore an equivalent playable state.

The save system must explicitly identify the format/version of stored game data.

The project is expected to evolve for a long period of time.

Future game updates must therefore be able to transform older save formats into newer formats.

Only the initial save format needs to exist now.

Detailed migration behavior belongs to future implementation planning.

---

# Visual Representation

The player must be able to visually identify:

* the factory area,
* placed buildings,
* valid and invalid placement,
* selected or relevant factory objects where required for testing.

Final production art is not required.

Placeholder assets are acceptable.

Visual representation must not redefine or silently alter authoritative factory state.

The same logical factory should remain valid even if its visual representation is recreated.

This requirement exists to support future:

* large factories,
* view culling,
* saving/loading,
* offline production,
* and multiple platforms.

The exact technical separation is an implementation concern.

---

# Factory Navigation

The foundational factory area must be navigable.

The player must be able to:

* move the view across the factory area,
* zoom in,
* zoom out.

The interaction must be suitable for the game's mobile-first direction while also remaining usable with desktop input during development and a potential future PC release.

The exact control implementation belongs to planning.

---

# Mobile-First Interaction Principle

Factory interactions must be designed around touch screens.

The foundation should not depend on interactions that require:

* pixel-perfect pointer precision,
* keyboard shortcuts,
* right-click interactions,
* or desktop-only input.

Future factory-building features are expected to support concepts such as:

* tapping,
* dragging,
* pinch-to-zoom,
* contextual controls,
* grid snapping.

This feature only needs enough interaction to validate the foundational grid and placement behavior.

---

# Factory State Inspection

During development, foundational simulation state must be understandable.

A developer should be able to determine why a production process is currently operating or stopped.

For a production entity, it should be possible to inspect information such as:

* operational state,
* available inputs,
* available output capacity,
* production progress,
* current recipe or process.

This is important because future factory systems will contain many interacting bottlenecks.

Visual observation alone must not be the only way to understand simulation behavior.

---

# Validation Scenario

The feature should provide a minimal development scenario demonstrating the foundation.

The exact placeholder content is not game-design canon.

A representative validation scenario may include:

1. A small factory grid exists.
2. The player places one or more test buildings.
3. A resource is available to a production building.
4. The building processes that resource over time.
5. Output accumulates in finite storage.
6. Production stops when output capacity is exhausted.
7. Removing some output allows production to continue.
8. The game is saved.
9. The game is closed or state is reset.
10. Loading the save restores an equivalent factory state.

The scenario exists to prove foundational behavior.

It must not define final recipes, factory progression or balancing.

---

# Quality Requirements

The foundation must support a game expected to grow significantly over time.

The implementation resulting from this specification must therefore prioritize:

* predictable behavior,
* maintainability,
* extensibility,
* testability,
* clear ownership of state,
* reliable persistence,
* and support for large future factories.

Future gameplay features should be able to build on the foundation without rewriting existing concepts unnecessarily.

At the same time, the implementation must remain proportional to current needs.

Do not build systems solely for hypothetical future requirements.

Follow the constitution's principles regarding simplicity and abstractions.

---

# Acceptance Scenarios

## Scenario 1 — Valid Building Placement

Given an empty factory grid,

when the player places a valid development building onto available cells,

then the building appears at the requested logical location,

and those cells become occupied.

---

## Scenario 2 — Invalid Building Placement

Given cells already occupied by another building,

when the player attempts to place an overlapping building,

then placement is rejected,

and existing factory state remains unchanged.

---

## Scenario 3 — Production With Available Input

Given a production building with sufficient required input and available output capacity,

when enough simulation time passes,

then the appropriate input is consumed,

and the appropriate output is produced.

---

## Scenario 4 — Missing Input

Given a production building without sufficient required input,

when simulation time advances,

then the building produces no output,

and its state indicates that required input is unavailable.

---

## Scenario 5 — Full Output

Given a production building whose output capacity is full,

when simulation time advances,

then additional output is not created,

and the production process enters an appropriate blocked state.

---

## Scenario 6 — Resume Production

Given a production building blocked because its output is full,

when sufficient output capacity becomes available again,

then production is able to resume.

---

## Scenario 7 — Time Advancement

Given identical factory states,

when both are advanced by the same amount of simulation time under identical conditions,

then both produce equivalent resulting states.

---

## Scenario 8 — Save and Restore

Given a factory containing placed buildings, resources and production state,

when the game state is saved and later restored,

then the restored factory represents an equivalent gameplay state.

---

## Scenario 9 — Factory Navigation

Given a factory area larger than the visible screen,

when the player navigates the area,

then the player can pan and zoom sufficiently to inspect and interact with relevant areas of the factory.

---

# Explicitly Out of Scope

Do NOT attempt to fully implement or define:

* final game art,
* final audio,
* final resource catalogue,
* final machine catalogue,
* final production ratios,
* complete conveyor logistics,
* splitters and mergers,
* electricity,
* research,
* resource deposits,
* multiple regions,
* inter-region logistics,
* expeditions,
* roguelite mechanics,
* prestige,
* premium currency,
* monetization,
* achievements,
* events,
* cloud saves,
* online accounts,
* multiplayer,
* analytics,
* leaderboards,
* final early-game progression,
* final factory-building UX.

These require separate feature specifications.

---

# Human-in-the-Loop Requirements

This feature establishes foundations, not game-design decisions.

If implementation requires choosing behavior that materially affects future gameplay, stop and produce a structured Human-in-the-Loop decision brief.

In particular, do not silently decide:

* final grid dimensions,
* maximum factory dimensions,
* final building footprints,
* exact simulation speed,
* final resource values,
* production balancing,
* final storage capacities,
* conveyor behavior,
* offline progression limits,
* final input gestures,
* final building placement UX,
* region structure,
* power behavior,
* or progression rules.

Temporary values required solely for the validation scenario may be chosen as clearly identified development placeholders.

Placeholder values must not become implicit game-design decisions.

---

# Success Definition

This feature is complete when the project has a small but functional factory simulation foundation that demonstrates:

> A factory world can exist.

> Buildings can occupy that world.

> Resources can exist and be stored.

> Production can transform resources over time.

> Production can become blocked.

> State can persist.

> State can be restored.

> The simulation can later support offline progression and logistics.

The result should provide a stable starting point for the next feature specifications without prematurely defining the complete game.
