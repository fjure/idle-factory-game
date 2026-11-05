---
title: F004 batch 12 workflow pitfalls
type: howto
sources: [S055]
updated: 2026-11-05
---

# F004 batch 12 workflow pitfalls

- **Give blind namers a reference.** Without a picture key for machine types, one of two reviewers swapped miner and assembler in three sets; add a reference sheet or ask for shape groups only. (S055)
- **Run human timed trials at the target size.** The owner SC-002 trial used an 1864x893 window with a phone-shaped screenshot, so timing is not phone evidence. (S055)
- **An emulator ABA needs a healthy emulator and a quiet host.** Three of four B3 sessions were disturbed by the owner's game client; the fourth ran at about 44 FPS for B1, B3 and empty-run controls, with a 30 FPS control reading 25 to 27 (unexplained throttle). Check that the control reproduces 30 before scoring. (S055)
- **Fail-closed catalogs need a strict flag.** `RequireRegistryRows` lets tests build a strict catalog; the shipped scene asserts it is true. (S055)
- **QA caught worker overclaims.** The journey summary read as PASS despite the failed crispness step, and a p95 claim covered only part of the runs; compare wording against results. (S055; the p95 detail is parent-reported)
- **Check branch base before QA.** Parent report: the T447 branch predated the T445 merge, so QA compared stale files; merge main first. (S055, parent-reported)
- **One flaky test.** Parent report: `SettingsSheetTests.TheLiveLineFollowsTheStateTable` failed once and then passed; rerun once, record it. (S055, parent-reported)
- **Leftover processes block worktree moves.** Finished workers' `adb` servers, wait loops and an orphaned `mcp-for-unity.exe` locked worktrees; see `docs/agents/workstation-pitfalls.md`. (S055)

Related: [batch 12 verification](./f004-batch-12-final-verification.md), [batch 11 pitfalls](./f004-batch-11-workflow-pitfalls.md).
