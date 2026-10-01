---
title: Core architecture
type: decision
sources: [S001, S008, S018, S019]
updated: 2026-10-01
---

# Core architecture

Idle Factory Game places authoritative factory state and rules in one engine-free `IdleFactory.Core` assembly. Domain, Simulation and Application remain namespaces inside that assembly until a measured ownership or reuse boundary justifies further splitting. `IdleFactory.Unity` references Core and owns Unity APIs, content authoring, input, persistence, lifecycle, UI and views. Edit Mode and Play Mode tests use separate assemblies. (S001)

The dependency direction is one-way: Unity adapters issue application commands and render immutable snapshots; Core never reads scene hierarchy, transforms, frame rate or presentation state. Simulation alone writes production progress and exchanges, while the application layer serializes use cases and durable publication. (S001)

The original Core Game Foundation authors resources, recipes, buildings and offline policies as ScriptableObjects, then validates and copies them into an immutable plain-C# catalog. Its acceptance is a prerequisite for the later technical migration to versioned JSON authoring. The migration will validate JSON structure and references, map fresh DTOs into the same catalog, and leave Unity-specific presentation references in ScriptableObjects where useful. It adds no new mechanics or balance values. (S001) Feature 002 plans that migration: the ScriptableObjects are replaced by one JSON `TextAsset`, while `F001SpriteCatalog` keeps presentation. See [JSON content authoring](./json-content-authoring.md). (S008)

Mutable runtime state belongs to placed instances. Existing normalized, case-sensitive ordinal string IDs are save identities; Unity instance IDs, localized names and asset paths are not. Existing ID and policy-revision semantics stay immutable within Save V1 unless an explicit migration is approved. (S001)

Platform glue follows the same rule. The Android activity that owns runtime rotation holds no gameplay decision, and the rotation classification sits in the Unity lifecycle adapter, not in Core (see [Android runtime rotation](./android-runtime-rotation.md)). (S001)

This boundary supports [deterministic production](./deterministic-production.md), while Unity-specific interaction remains in the [presentation boundary](./unity-presentation-boundary.md). (S001)

Feature 003 plans the gameplay run as new namespaces inside the same Core assembly: `IdleFactory.Numerics` and `IdleFactory.Run.{Content,Domain,Simulation,Application}`. The Feature 001 domain, simulator and Save V1 path stay only as legacy V1 import support, and the Feature 001 runtime composition is retired once the new journey passes. See [factory run simulation](./factory-run-simulation.md) and [Save V2 and V1 import](./save-v2-and-v1-import.md). (S018)

Feature 004 keeps this boundary for the 2.5D restyle: presentation-only changes, Core untouched; see [visual identity and art direction](./visual-identity-art-direction.md). (S019)
