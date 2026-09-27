---
title: Core architecture
type: decision
sources: [S001]
updated: 2026-09-27
---

# Core architecture

Idle Factory Game places authoritative factory state and rules in one engine-free `IdleFactory.Core` assembly. Domain, Simulation and Application remain namespaces inside that assembly until a measured ownership or reuse boundary justifies further splitting. `IdleFactory.Unity` references Core and owns Unity APIs, content authoring, input, persistence, lifecycle, UI and views. Edit Mode and Play Mode tests use separate assemblies. (S001)

The dependency direction is one-way: Unity adapters issue application commands and render immutable snapshots; Core never reads scene hierarchy, transforms, frame rate or presentation state. Simulation alone writes production progress and exchanges, while the application layer serializes use cases and durable publication. (S001)

The original Core Game Foundation authors resources, recipes, buildings and offline policies as ScriptableObjects, then validates and copies them into an immutable plain-C# catalog. Its acceptance is a prerequisite for the later technical migration to versioned JSON authoring. The migration will validate JSON structure and references, map fresh DTOs into the same catalog, and leave Unity-specific presentation references in ScriptableObjects where useful. It adds no new mechanics or balance values. (S001)

Mutable runtime state belongs to placed instances. Existing normalized, case-sensitive ordinal string IDs are save identities; Unity instance IDs, localized names and asset paths are not. Existing ID and policy-revision semantics stay immutable within Save V1 unless an explicit migration is approved. (S001)

Platform glue follows the same rule. The Android activity that owns runtime rotation holds no gameplay decision, and the rotation classification sits in the Unity lifecycle adapter, not in Core (see [Android runtime rotation](./android-runtime-rotation.md)). (S001)

This boundary supports [deterministic production](./deterministic-production.md), while Unity-specific interaction remains in the [presentation boundary](./unity-presentation-boundary.md). (S001)
