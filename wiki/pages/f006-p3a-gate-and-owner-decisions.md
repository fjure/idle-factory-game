---
title: F006 batch-A gate and owner decisions
type: decision
sources: [S060]
updated: 2026-11-13
---

# F006 batch-A gate (T543) and owner decisions of 2026-11-13

## Gate outcome

- **Play-Tester** (`reviews/playtest-agent-f006-A.md`): channel A showed batch A changed no economy value (every strategy-baseline gate identical); channel B (Windows development player) measured card interactions: select plus Upgrade 2 taps, cause 0 taps at category level and 2 for the full cause, Remove 4; portrait card 14-16 % of height. Proposal for the owner only, not a human playtest. (S060)
- **UI/UX** `uiux-f006-A.md`: REJECT (U-A1 Android load hang, U-A2 empty tap). After T552 and T553, `uiux-f006-A-rerun.md`: ACCEPT_WITH_NOTES; touch targets about 48 dp, Back order correct, 150 and 200 % text works. (S060)
- A synthetic scan-code-0 Escape is ignored by the Input System, so the first report's A-F1 (Escape does not close) was a false finding. (S060)

## Owner decisions (`reviews/p3a-followups.md`)

| Decision | Result |
|---|---|
| P2-F9 | new Office glyph (T554) |
| P2-F7(b) | front-only wall, conditional (P2-F23, T527 proofs) |
| Gate REJECT | fix first: T552, T553 added and merged |
| Batch B | go after the re-run |
| R-7 no Android accessibility nodes (the game is one `SurfaceView`, TalkBack announces nothing) | deferred to a later accessibility feature; pre-existing in the whole game |

## Carried items (A-1..A-13)

A-5 card cause is category-level and hides auto-sell (T537 copy, T528 model); A-6 Details keeps old scroll offset, landscape Details covers a column, tab labels clip at 200 % (T536, T537); A-7 "Show zoom buttons" does not hide the buttons on touch (T537); A-8 missing `display-v2.json` logs `preferences.display.invalid` on first launch (T537); A-9 Android dev APK 82 MB with 46 MB of entries, install fails on the full emulator, budget 47.3 MB (T541); A-10 SetPass re-measure; A-11 move `IMachineEffects` to Presentation, split `RunShellDocument.cs`; A-12 no leader from card to machine, Close icon has no accessible name; A-13 T527 catalog and C8 items. A-1 to A-4 closed by T535. (S060)

Related: [card and hub](./f006-batch-p3a-card-hub-and-gate-fixes.md), [Android load hang](./f006-android-load-hang-atlas-page-cap.md), [plan overview](./f006-plan-overview.md), [workflow pitfalls](./f006-batch-p3a-workflow-pitfalls.md).
