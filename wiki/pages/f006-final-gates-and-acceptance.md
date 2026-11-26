---
title: F006 final gates, owner decisions and acceptance
type: decision
sources: [S063]
updated: 2026-11-26
---

# F006 final gates (T543, T548, T546, T549) and closeout

Records: `reviews/final-acceptance.md` (sections 1 to 5), `readability-f006.md`, `visual-acceptance-f006.md`, `playtest-agent-f006-final.md`, `uiux-f006-final.md`, `p3b-followups.md` C-102..C-111. (S063)

## Gates and verdicts

| Gate | Issue / PR | Verdict |
|---|---|---|
| Final T543 (Play-Tester proposal plus independent UI/UX) | #715 / #790 | UI/UX ACCEPT_WITH_NOTES, no Major. QA failed the first head because the UI/UX report mistook plate width for cell width. Play-Tester: economy baseline unchanged; the first upgrade comes about 2 to 3 minutes in, but only after the player finds Auto-sell in a long Market scroll (inference). |
| T548 final lens reviews | #719 / #791 | Ten lenses, all ACCEPT WITH CONDITIONS, no blocker. Majors: PERF-1, TESTQA-3, PERF-9, TESTQA-4, AD-1. QA failed the first head for seven errors in the parent's summary text. |
| T546 readability | parent | SC-005 blind naming 22/22 on all 16 sheets; P3 cues 110/112, then 16/16 after a hub-dot re-test; SC-006 ordering 10/10 and 10/10. Timed trials (SC-004, SC-005 timed) skipped by owner decision. |
| T549 owner visual acceptance | parent | PASS on every surface and captured class, with accepted gaps (no 48 pt overlay crop, no live stage change). |

(S063)

## Owner decisions 2026-11-26

- **SC-008 accepted as a deviation**: zero allocations on `Refresh` and the defined paths, about 15 to 21 per steady real-scene frame, emulator FPS budgets 1 to 3 missed by every build, no physical-device, battery or thermal data. A follow-up Issue covers the C-68 deep profile plus a device measurement. (S063)
- **AD-1 accepted**: the reused UI glyph stays as the upgrade marker; a world-only outlined marker on the markers atlas follows after P4 and also removes about 4.5 SetPass at 12 markers. It was named correctly in all 16 P3 sheets. (S063)
- Guidance thresholds (SC-001, 002, 003, 004, 006, 010) are accepted as measured, per the spec clarification that they are guidance. Misses include SC-003 upgrade 3 steps vs 2 and persistent labels 10 vs 6. (S063)
- Store-full markers stay uncapped. The deck hard edge and black void become an art task after P4. The Auto-sell hint, offline summary copy and an Overdrive confirm go to a polish list. The first-tap-ignored report was not reproduced in 12 taps and is not filed. (S063)
- A-9 (82 MB development APK) is resolved: the `ac49f6c8` development build is 46.72 MB. No release-build size is recorded (C-109). (S063)

## Closeout facts

- Final counts: Edit Mode 3187, Play Mode 3526, tagged Windows player 85/85 on main `ac49f6c8`. `final-test-names.txt` is the T542 manifest (C-107). (S063)
- Post-P4 Issues still to create: C-68 deep profile plus device measurement; outlined upgrade marker and its Art Bible entry (C-104); deck edge and void art; store-full scale and cap note (C-75). (S063)
- Sheets over 4000 px lose small cues when viewed downscaled (C-102); lay P3 sheets out per cue size. (S063)

Related: [part B fixes and B4b](./f006-batch-6b-fixes-captures-and-performance.md), [batch 5 gates](./f006-batch-5-gate-and-owner-decisions.md), [part B pitfalls](./f006-batch-6b-workflow-pitfalls.md), [plan overview](./f006-plan-overview.md).
