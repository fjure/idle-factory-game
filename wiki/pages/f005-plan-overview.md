---
title: Feature 005 plan overview
type: decision
sources: [S022, S026, S027, S028]
updated: 2026-11-06
---

# Feature 005 plan overview (feature closed)

F005 is complete (batches 0, 1, A to D merged; closeout T356 on 2026-10-27, owner gates T353/T354 released the same day). The decision and design sections below are the plan as written; the status sections at the end record what shipped. The plan passed three review waves (Architect, Test/QA, Gameplay, Game Systems, UI/UX, Performance, Security, DevOps). Every finding has a disposition in `specs/005-economy-contracts/reviews/plan-reviews.md`; research entries are `F5-R01` to `F5-R18`. (S022)

## Owner decisions

- **D-A** one feature; the offline value redesign (80 % effectiveness, 8 h cap) stays out until the session log gives real return data.
- **D-B** order: behaviour-preserving #356 refactor first, then dev-only time acceleration and strategy policies (so the Play-Tester can play), then gameplay.
- **D-C** the Factory Sale threshold rises per prestige; Industry loses hard caps for diminishing returns (practical ceiling at least 4.0x the IP of runs 1-4).
- **D-D** contract Cash = ratio of the base market value of the delivered stock; standing offers at most 1.5, 1.5-2.0 reserved for bonus offers.
- **D-E** the chain must earn more per slot than Ore-only, inside the whole-number content format, guarded by a static check.
- **D-G** Save V3 for new state; V2 loads with defaults; downgrade unsupported.
- **D-H** gate = realistic late-game workload W005-L against the existing return budgets; W003-B is measured on the pre-F005 base first.
- **D-I** accelerated sessions are marked only in the session log.
- **D-J** starting Cash grows per prestige. **D-K** the threshold stops rising at `maxIndex`, prestige continues. **D-L** auto-sell reserves stock for every active delivery contract.
- **D-M** Android exports the log by path (`adb pull`), no share intent or FileProvider; backup exclusion mandatory. **D-N** dev builds use `IdleFactory-Dev/` as save root. (S022)

## Design decisions in brief

- One shared sale price and one reward formula, in Domain (F5-R04). See [run contracts](./run-contracts.md) and [run economy and prestige](./run-economy-and-prestige.md).
- Content schema v3, strict, reader evolved in place, F003 offer IDs kept (F5-R03). Save side: [Save V2 document](./save-v2-document.md).
- Timeline mapping and in-simulator auto-delivery: [F005 timeline mapping and auto-delivery](./f005-timeline-mapping-and-auto-delivery.md).
- Session log and DevTools: [F005 session log and dev time acceleration](./f005-session-log-and-devtools.md).
- Large live gap (F5-R10): [desktop focus lifecycle](./desktop-focus-lifecycle.md).
- Refactor shape and API gate: [core architecture](./core-architecture.md). Persistence changes: [run persistence](./run-persistence.md).
- Balance gates and policy r2: [run balance trace](./run-balance-trace.md).

## Workload and budget rules

Quiet host, median of 5 runs, 10 % spread limit, 10 % regression guard. W005-L gate: 8 h return at most 500 ms (Windows Mono player) and 2 s (emulator), 60 fps. W003-B is re-measured after the units that touch the settle path. IL2CPP on Windows is a recorded open item. (S022)

## Status: batch 0 merged (2026-10-10)

- **T301** policy r2 and strategies are designed: [balance policy](./run-balance-policy-and-f005-matrix.md). Owner answered Q9 yes. (S026)
- **T302-T304** refactor groundwork is real: public API snapshot, `RunPersistence` and `RunSnapshots` splits, command-path mutators internal. See [run persistence](./run-persistence.md) and [core architecture](./core-architecture.md). (S022)
- **T300** W003-B measured on the pre-F005 base: 1.92x the 500 ms reference. Decision Option 1 plus a re-measure gate, see [bounds](./factory-run-bounds-and-offline-segmentation.md). (S027)
- **Planning change (#426):** new task T360 "Option 1 hot loop" (Issue #429, ledger WU18) sits after T315 and T300 and before T316 and T326; later units were renumbered by one. T300 now gates T316, T325 and T326, not T315. (S022)
- **T303:** `RunSnapshots.cs` is split into Factory, Economy and `RunDerivation.cs`; Contracts and Items were already separate. The Rates types stay in the main file because F5-R01 names no Rates partial. A `RunSnapshots.Goals.cs` seam is needed at plan step 9. (S022)
- Per F5-R14, W003-B is "recorded, not gated"; 500 ms and 2 s are the W005-L budgets used as the reference. (S027)

## Status: batch 1 merged (2026-10-13)

T305-T314, T310, T311 and T357 are merged (Issues #367-#378). Gate T315 on main 53ceb54: Edit Mode 1662, Play Mode 1553, player 24/24. Details: [session log and DevTools](./f005-session-log-and-devtools.md) (T307-T309), [run persistence](./run-persistence.md) (T306), [content v3](./f005-content-v3-transitional.md) (T312-T314), [strategy baseline and Play-Tester findings](./run-balance-policy-and-f005-matrix.md) (T311, T357), [workflow pitfalls](./f005-batch-1-workflow-pitfalls.md) (T310). T305 added `docs/contracts/run-reason-codes.md` (35 codes) pinned by `ReasonCodeContractTests`. Open: owner decisions on the content constants and the Play-Tester questions. (S022)

## Status: batch A merged (2026-10-14)

T360, T323, T316, T331, T317, T324, T358, T319, T320, T359, T321, T322 and the Play-Tester report T342 are merged (Issues #429, #383, #379, #396, #380, #384, #385, #382, #387, #386, #388, #389, #407; main 77e04b0). Mechanics: [batch A economy and contracts](./f005-batch-a-economy-and-contracts.md). Owner questions and the Play-Tester proposals: [batch A open decisions](./f005-batch-a-open-decisions.md). The transitional reward adapter is removed. Next: T326/T327 production counting and auto-deliver, T334 (#399) persistence, T341/T345 tuning. (S022, S028)

## Status: batch B merged (2026-10-21)

T325-T327, T328, T330-T340, T318, T329, T361-T364 are merged (Issues #381, #390-#405, #464-#466, #483; PR #482 recorded the owner decisions of 2026-10-21 and added T364). Pages: [auto-deliver and contract UI](./f005-batch-b-auto-deliver-and-production.md), [Save V3 drift](./f005-batch-b-save-v3-drift.md), [live gap](./f005-batch-b-live-gap.md), [session log writer](./f005-session-log-writer.md), [measurements](./f005-batch-b-performance-and-measurements.md), [UI and Android](./f005-batch-b-ui-and-android.md), [workflow pitfalls](./f005-batch-b-workflow-pitfalls.md). PF-10 re-baselined at 222.28 ms. Next: T343 Play-Tester, T341/T345 tuning, T351 Android. (S022)

## Status: batch C merged (2026-10-22)

T341 (policy r2, strategies, FR-024 gates), T345 (content revision 3 and W005-L), T346 (Play-Tester batch C report) and T365/T366 (UI and DevTools fixes) are merged. Pages: [content revision 3](./f005-batch-c-content-revision-3.md), [policy r2 and gates](./f005-batch-c-policy-r2-and-gates.md), [playtest and decisions](./f005-batch-c-playtest-and-decisions.md), [UI and DevTools fixes](./f005-batch-c-ui-and-devtools-fixes.md). (S022)

## Status: batch D merged, F005 closed (2026-10-27)

Final suites, release scan, performance and emulator evidence, the UI/UX review (T352, fixes T367) and the owner gates are recorded in [final evidence](./f005-batch-d-final-evidence.md), [journeys](./f005-batch-d-journeys.md) and [UI review and fixes](./f005-batch-d-ui-review-and-fixes.md). The owner approved content revision 3 as shipped at T354 (2026-10-27, `reviews/content-approval-r3.md`); any later tuning becomes a new feature. All 68 tasks are done. (S022)
