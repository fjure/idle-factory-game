---
title: F006 batch 6 copy conformance and polish
type: decision
sources: [S062]
updated: 2026-11-19
---

# F006 batch 6 copy conformance (T547) and pre-T542 polish (T560)

Issues #718 (PR #777) and #776 (PR #780). Records: `reviews/world-bible-conformance.md` Parts 2 and 3, `reviews/test-rebaseline.md` T560, `reviews/p3b-followups.md`. (S062)

## T547 World Designer conformance and World Bible v0.2

Part 2 verdict was CONFLICTS (4 open: CF-2 "slot" versus "location" at 16 sites, CF-3 hub title "Factory Sale", CF-4 "system(s)", CF-5 "Locked."). CF-1 was settled by owner decision (option A): `F006-WB v0.2` (MINOR) makes **Store** the capacity term and **Depot** the world structure; no string was renamed. String coverage is proved mechanically (888 literal lines in 72 files, 0 unaccounted), because keyword sweeps had missed strings. T560 applied CF-2..CF-5, B-4 and B-6, and the Part 3 re-read says CONFORMS with one new string ("Selected", N-47). (S062)

## T560 pre-T542 polish

- Code carries that were named for read-only roles (T542 Test/QA, T547) were collected into one implementation task; those roles may not repair. T542 only verifies. (S062)
- Behaviours accepted by the owner: Mine control hidden while a phone card is open; one Back clears the banner and the sheet or card under it; milestone banners queue; no welcome-back modal under 60 s; a dim tint marks occupied and locked locations while the build card is open (no sprite-mark art task). (S062)
- Fixes: `MachineCard` and `BuildCard` refresh only on a changed key, with 600-frame zero-allocation tests; the marker check compares `LayoutId` (a 0.3 s expiry stays as safety net); the Details sheet is covered by the C-28 guard; the B-1 audit found a reachable reason at every soft-disabled call site (27 call sites, grouped by control in the 17-row table of `test-rebaseline.md`). (S062)
- Not done, carried: C-73 (two text clips at 200 %, a 120 px recentre, shortage-marker arrow), C-74 (player-compatible two-write autosave test C-65, prestige banner end-to-end test, duplicate sheet and the shim), C-75 (22 px store-full marker art, dim tint 0.5 versus 0.6), C-76 (stale cause and rates when only a rate changes). `ProductionPerMinuteMicro` is the potential rate, not the effective one (C-72, pinned by a test). (S062)

Related: [verification and polish (T531, T541)](./f006-batch-6-verification-and-polish.md), [batch 6 pitfalls](./f006-batch-6-workflow-pitfalls.md), [plan overview](./f006-plan-overview.md).
