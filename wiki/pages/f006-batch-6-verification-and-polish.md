---
title: F006 batch 6 verification and polish
type: decision
sources: [S062]
updated: 2026-11-19
---

# F006 batch 6 verification and polish (part A, main 38cc418)

Four Issues merged: T541 #713 (PR #778), T547 #718 (PR #777), T531 #704 (PR #779), T560 #776 (PR #780). This page covers T531 and T541; T547 and T560 are on [copy conformance and T560 polish](./f006-batch-6-copy-conformance-and-polish.md). Records: `specs/006-world-first-factory-experience/reviews/` (`performance-b4.md`, `world-bible-conformance.md` Parts 2 and 3, `test-rebaseline.md`, `p3b-followups.md` batch 6 and T560). All measurements are exploratory workstation evidence (FR-027); no physical-device run. (S062)

## T531 B4a measurement on W006-V

- Passes: budgets 4, 6, 7, 10, 11 (APK 46.8 MB, 356 shader variants, largest atlas page 512x512, world art 3.54 MB). Emulator 61.41 FPS, p95 bucket 19 ms, 0 intervals of 50 ms or more. Budget 9 (guest CPU against a paired B1) was not measured. (S062)
- Budget 5 splits: 0 allocations on the defined paths (600-frame synthetic world), but the whole real-scene host frame allocates about 21 per frame card closed, about 400 with the machine card open and about 280 with the build card. Cause: both cards rebuilt the factory snapshot and rates on every `QuantityRevision` change (C-41). T560 fixed both cards; the card-closed 21 per frame stays unattributed (C-68, a separate deep-profile Issue after the P4 gates). (S062)
- Budget 8 (SetPass): the player method reads about 42 to 44 passes per render, F004 pages alone 34 and the F006 delta +6 to +10. The earlier "one pass per render" figure was an Editor-probe artifact (its batch counters read 0), not a rendering property. The owner restated row 8 on 2026-11-19: F006 delta at most +12 over the F004-only 34, with the `camera.Render()` CPU tier 1 bound binding (see [mobile performance targets](./mobile-performance-targets.md)). (S062)
- Renderer census: 80 to 82 typical, worst case 99 (portrait) and 101 (landscape) with all 12 slots at the overlay cap of 61 renderers. The owner confirmed Art Bible 21.12 with a worst-case ceiling of 110 (v1.1.2); 95 stays the typical-case figure. (S062)
- C-6 is now guarded by `PresentationCatalogInitOrderTests` (declaration order of the placed F006 keys before `Registry`). (S062)

## T541 emulator journey for F006

`scripts/android-emulator-journey.ps1` now targets the Office hub, has an F006 scenario and `-ValidateOnly`, defaults to AVD `IdleFactory_Test_API_36` and has an `F006Autosave` check. Tap fractions are provisional until T542 calibrates them on a device (C-70). See [emulator validation](./android-emulator-validation.md). (S062)

Related: [copy conformance and polish](./f006-batch-6-copy-conformance-and-polish.md), [batch 5 gates](./f006-batch-5-gate-and-owner-decisions.md), [plan overview](./f006-plan-overview.md), [batch 6 pitfalls](./f006-batch-6-workflow-pitfalls.md), [run persistence](./run-persistence.md).
