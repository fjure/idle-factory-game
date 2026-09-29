# Wiki recovery for Feature 003

**Date:** 2026-09-29  
**Question:** Which existing production, content, persistence, presentation and performance rules constrain the first Iron factory and prestige loop?

The wiki status is healthy: 19 indexed pages, sources S001–S017, and no findings in the 2026-09-29 lint report. This query is **partial**: the wiki records the foundation and the JSON migration, but it has no approved rules for global inventory, contracts, items, research, prestige or the new save version.

| Existing rule | Source |
|---|---|
| Feature 001 uses independent input/output buffers, atomic exchanges at cycle completion, checked whole quantities and deterministic tick arithmetic. Stopped time does not accrue. | [Deterministic production](../../../wiki/pages/deterministic-production.md), S001 |
| Save V1 holds machine buffers and progress. Revision 1 credits at most eight hours at 4/5 efficiency; promotion precedes publication and retry is exactly once. | [Local checkpoints and offline return](../../../wiki/pages/local-checkpoints-and-offline-return.md), S001–S003, S006 |
| Feature 002's single packaged JSON is the gameplay authoring authority. New content families need a new approved content schema version. Stable IDs are save identities. | [JSON content authoring](../../../wiki/pages/json-content-authoring.md), S008, S010; [Core architecture](../../../wiki/pages/core-architecture.md), S001, S008 |
| Core is engine independent; Unity adapters own input, presentation and persistence. Touch/mouse gestures, keyboard focus, non-color status cues and rotation behavior are established. | [Core architecture](../../../wiki/pages/core-architecture.md), S001, S008; [Unity presentation boundary](../../../wiki/pages/unity-presentation-boundary.md), S001 |
| The 60 FPS Android emulator result covers an output-blocked scene; active producing workload remains unmeasured on device. | [Mobile performance targets](../../../wiki/pages/mobile-performance-targets.md), S001 |

**Coverage gaps carried into Feature 003:** inventory replacement and Save V1 policy; offline ordering for new timed systems; content schema evolution for costs/contracts/items/research/prestige; first-run balance and two-hour eligibility; visible slot placement; real producing-factory mobile performance. The owner is being asked for the material gameplay and compatibility decisions. Ingest the approved planning artifacts after the plan stabilizes.
