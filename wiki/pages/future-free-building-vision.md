---
title: Future free-building vision versus today's factory
type: concept
sources: [S056]
updated: 2026-11-11
---

# Future free-building vision versus today

Closes the F006 wiki-query gap "Core-Idea free-building vision not ingested". The summary is taken from the F006 spec table, not from `Core-Idea.md` directly. (S056)

| Area | Exists today | Future (separate gameplay features) |
|---|---|---|
| Building | Fixed slots unlocked by factory level, one machine per slot | Free 2D grid placement, rotation, undo |
| Flow | One global store | Conveyors, storage, splitters, mergers |
| Bottlenecks | Per-machine blockers, per-resource supply coverage | Spatial bottlenecks (belt throughput, routing) |
| World | One factory | Regions, inter-region logistics |
| Systems | Market, Contracts, Items, Research/Industry, Overdrive, Prestige | Power, projects, expeditions |

F006 must not implement any of these (FR-039); it keeps identity independent of position (FR-038) and documents the migration needs (per-machine position in the save, slot-to-position map). Core-Idea themes "transparent production systems" and "identify and solve bottlenecks" drive the truthful depot-link design. (S056) See [F006 world presentation design](./f006-world-presentation-design.md) and [plan overview](./f006-plan-overview.md).
