---
title: Run content dispatch
type: component
sources: [S018, S019]
updated: 2026-11-06
---

# Run content dispatch

T136 completes the content load path that the bootstrap (T110) and the V1 import use. It builds on the v2 reader and validator in [gameplay content validation](./gameplay-content-validation.md) and the content model in [gameplay content v2](./gameplay-content-v2.md). (S018)

- `RunCatalogBuilder` runs the v2 validator first and builds a `RunCatalog` only with zero violations, so there is never a partial catalog. The catalog holds no DTO; a Core constructor exception maps to one static `content.internal` diagnostic with no message, and fatal exceptions are not caught. (S018)
- `GameplayContentDispatcher` peeks `contentSchemaVersion` with the same lexical rules as the v2 reader (shared `TryDecode`): version 1 goes to the unchanged Feature 002 loader as the legacy import catalog, version 2 to the v2 reader and builder, anything else is `content.version.unsupported`, and a duplicate version member is `member.duplicate`. The peek only routes; trailing content after the root is rejected by the version reader. (S018)
- The v2 reader no longer echoes Newtonsoft exception messages, which can carry member names and fragments (SEC-5). T141 cleaned up the v1 route too: `GameplayContentReader` returns static text for `json.malformed` and `json.depth`, and clips file-supplied member names and JSON-pointer segments to 128 code units without splitting a surrogate pair. (S018, S019)
- The test seam `RunCatalogBuilder.Build(doc, resolver, Func<>)` and the `GameplayContentLoadResult` factories are `internal`, visible to `IdleFactory.EditModeTests` through `InternalsVisibleTo` in the Unity assembly (T141). (S019)
- The bootstrap passes a sprite-catalog resolver, so an unknown presentation key fails start-up; see [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md). (S018)
- Catalog identity under permutation holds for ID-indexed families. Level tables, milestones, rewards and completion indices keep their order, `ResearchInContentOrder` keeps the author's order (OD-8), and lists without semantic order (recipe sides, prerequisites, allowed machines, initial unlocks) are equal only as sets. (S018)
- A `machineType` speed Item has the inert effect target `allMachines`; the machines it affects come from `ItemDef.TargetKind` and the activation's target, so UI must read the Item definition, not the effect target. (S018)
