---
title: Core architecture
type: decision
sources: [S001]
updated: 2026-09-13
---

# Core architecture

Idle Factory Game places authoritative factory state and rules in one engine-free `IdleFactory.Core` assembly. Domain, Simulation and Application remain namespaces inside that assembly until a measured ownership or reuse boundary justifies further splitting. `IdleFactory.Unity` references Core and owns Unity APIs, content authoring, input, persistence, lifecycle, UI and views. Edit Mode and Play Mode tests use separate assemblies. (S001)

The dependency direction is one-way: Unity adapters issue application commands and render immutable snapshots; Core never reads scene hierarchy, transforms, frame rate or presentation state. Simulation alone writes production progress and exchanges, while the application layer serializes use cases and durable publication. (S001)

Resources, recipes, buildings and offline policies are authored as ScriptableObjects, validated at bootstrap and copied into an immutable plain-C# catalog. Mutable runtime state belongs to placed instances. Stable string IDs are save identities; Unity instance IDs, localized names and asset paths are not. Existing ID and policy-revision semantics stay immutable within Save V1 unless an explicit migration is introduced. (S001)

This boundary supports [deterministic production](./deterministic-production.md), while Unity-specific interaction remains in the [presentation boundary](./unity-presentation-boundary.md). (S001)
