---
title: F007 batch B0 gates and tooling
type: decision
sources: [S064]
updated: 2026-11-27
---

# F007 batch B0: gates, lease rules and test-id tooling

Batch B0 (merged to main c8f56707) re-verified the gates and landed two tooling Issues. Plan context: [F007 plan overview](./f007-plan-overview.md). (S064)

## T600 gate re-verification (parent)

- All bootstrap gates pass; evidence is in `specs/007-stylized-industrial-visual-reboot/reviews/gates.md` (commit 2020151b). Analyze found 0 CRITICAL, 0 HIGH, 3 MEDIUM, 4 LOW; the MEDIUM and LOW items are non-blocking and go to the next tasks regeneration (T637). The orchestration ledger is 45/45. (S064)
- The ledger is binding where task text and ledger disagree on ordering (M1, M2): for example T604 waits for T603. (S064)
- Unity MCP was not connected (ECONNREFUSED), so Unity work ran through `scripts/test-unity.ps1` under the leases. (S064)

## T602 (#798, PR #832): lease and art-path rules

- The Pixel 11 Pro falls under the `emulator` lease. `-Serial` is required when two devices are attached. Phone performance runs hold `unity,emulator,gpu`. adb starts from the main checkout. See [Android emulator validation](./android-emulator-validation.md). (S064)
- The art-path triggers (art-pipeline test chaining and the after-implement gate) now also cover `Assets/IdleFactory/Art/F006/` and `Art/F007/`. Before this, commits touching only Art/F006 went undetected (live gap, for example 137da8e4). (S064)
- New manual diff-review rule for URP, graphics and quality settings, because `check-settings-noise` does not cover them. (S064)

## T638 (#799, PR #833): test-id comparison

- `tools/test-ids/compare_test_ids.py` scans 3758 ids; its pytest suite has 19 tests. (S064)
- `reviews/rebaseline-map.md` holds the QA-5 candidates. Only exact-id rows with status `moved` or `removed` satisfy a missing id; class-level `.*` rows never do. (S064)
- `scripts/test-unity.ps1 -CheckTestIds` is a new optional switch. (S064)
- Parser limits: no `#if` evaluation and no TestCaseSource expansion, so such ids are not seen. (S064)

## Deferred

T601 and T604 wait for the physical Pixel (owner connects it 2026-11-28). T603 is blocked: see [baseline capture lessons](./f007-baseline-capture-lessons.md). (S064)
