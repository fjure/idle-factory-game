---
title: Deterministic production
type: concept
sources: [S001]
updated: 2026-09-13
---

# Deterministic production

Factory geometry uses signed integer cell coordinates, explicit positive bounds and complete-footprint validation. A factory owns buildings by persistent instance ID and derives an occupancy index by cell. Placement validates every cell before committing the instance and occupancy together. (S001)

Resources and capacities are nonnegative whole `Int64` units. Production duration and progress use checked `Int64` 100 ns ticks with `0 ≤ progress < duration`. Duplicate normalized resource rows are rejected. Each machine owns separate input and output buffers; Feature 001 has no shared inventory or implicit transfer. (S001)

A cycle advances only while the machine is active, every complete input is available and every complete output fits. Inputs remain present during partial progress. At completion, aggregate input removal and output addition commit together. Inactivity or blocking preserves partial progress and discards stopped time without backlog. (S001)

Long intervals use cycle-boundary arithmetic: determine time-, input- and output-permitted cycles, then perform checked aggregate exchange. `AdvanceSimulation` builds and validates a complete factory candidate before replacing live state, so an arithmetic failure in a later machine cannot leave earlier machines advanced. (S001)

The D001 acceptance fixture uses a 16×16 area, a 2×2 machine, four initial input units, output capacity one and a `2 input → 1 output / 5 s` recipe. These values validate behavior and do not establish final balance or future factory limits. (S001)

Offline time invokes this same operation through [local checkpoint reconciliation](./local-checkpoints-and-offline-return.md). (S001)
