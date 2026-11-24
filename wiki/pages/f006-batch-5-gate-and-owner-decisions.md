---
title: F006 batch 5 gates B and C and owner decisions
type: decision
sources: [S061]
updated: 2026-11-19
---

# F006 batch 5: gates B and C, owner decisions, carries

Reports: `reviews/playtest-agent-f006-B.md`, `uiux-f006-B.md`, `playtest-agent-f006-C.md`, `uiux-f006-C.md`; carries B-1..B-17 and C-1..C-67 in `reviews/p3b-followups.md`. Player sessions ran on the emulator by `adb` only. (S061)

## Owner decisions

| Date | Decision |
|---|---|
| 2026-11-17 | U-B1: Cash and Overdrive stay visible in landscape; fix list before batch C (T555). |
| 2026-11-17 | Gate agents never inject host input; Windows-player coverage is marked not covered. |
| 2026-11-17 | One `pm trim-caches` on the emulator, then the dedicated AVD `IdleFactory_Test_API_36`. |
| 2026-11-17 | C-10: a new art task (T556). |
| 2026-11-18 | Marker wiring is a new gap task (T557). |
| 2026-11-18 | Stage offset u0 (research R15), see [shell and stages](./f006-batch-5-shell-card-and-stages.md). |
| 2026-11-18 | Fix before batch D: T558 (U-C1, C-F2, C-F1) and T559 (periodic autosave). |
| 2026-11-19 | AR-3 and AR-4 accepted; Art Bible v1.1.1 adopted. |

(S061)

## Gate B (build b386a82)

Channel A: economy unchanged. Channel B findings: landscape columns hidden with a card open (Medium, fixed by T555), Research clip, hub-row copy, Back from a hub-opened Market closing the sheet, 9 dp Mine and Overdrive. The first channel-B run failed with `INSTALL_FAILED_INSUFFICIENT_STORAGE`. (S061)

## Gate C (build 15deea2): ACCEPT_WITH_NOTES for batch D

Six Medium Play-Tester findings. UI/UX confirmed the banner covering the top machine row (C-F1), the rotation re-fit (C-F2) and the clipped depot (U-C1), all fixed by T558, and refuted C-F3 and C-F4. U-B13: there was no periodic autosave, so a force-stop lost the whole run (T559). Not covered: TalkBack, Windows player, prestige banner, build dust and the second shortage marker on device (C-62). (S061)

## Carries to remember

C-25 (R09 note, recorded in [world presentation design](./f006-world-presentation-design.md)); C-61 hard-edged deck rectangle (owner); C-63 machine hit area at n=3 and banner at exactly 20.0 %; C-65 to C-67 autosave on IL2CPP and write rate. The rest belong to T542, T544, T546 and T547. (S061)

Related: [batch 4 gate](./f006-p3a-gate-and-owner-decisions.md), [plan overview](./f006-plan-overview.md), [pitfalls](./f006-batch-5-workflow-pitfalls.md), [run persistence](./run-persistence.md).
