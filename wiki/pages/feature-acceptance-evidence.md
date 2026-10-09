---
title: Feature acceptance evidence
type: howto
sources: [S001, S018]
updated: 2026-10-10
---

# Feature acceptance evidence

Evidence hierarchy for Feature 001's T041 acceptance and later review files. (S001)

## Reconciling requirements

- Every FR and SC row cites a concrete source: a review section, a named test, or a GitHub Issue comment. A row without a cited source is not PASS. (S001)
- "Fresh confirms" means that the cited test ran green in the fresh complete suite. It does not mean each test was retraced line by line. T041 ran Edit Mode 569/569, Play Mode 147/147 and the Windows player suite 2/2 on `8c28d69`, after a forced recompile. (S001)
- Grep the test suites before citing a test name, and check that a fixture matches the requirement's literal values. SC-002 looked covered but was composed from two fixtures (T041-F2); see [Player-level test gaps](./player-level-test-gaps.md). (S001)
- Headline counts must match the row statuses. The T041 headline said "all 47 pass", while one row was PARTIAL; QA flagged the mismatch as non-blocking. (S001)

## Which record wins

| Question | Authoritative record | Stale-looking record |
|---|---|---|
| Android runtime journey | T057 rerun in `windows-android-validation.md` | the original T038 FAIL/UNRESOLVED header |
| Persistence security | The dated T051 successor in `persistence-security-review.md`, with Issue #71's exact-commit verdicts | The original T029 FAIL/P2 section is retained as history, not the current verdict (T068) |
| Zoom readability | T066 captures and the appended independent Art Director re-review in `f001-placeholder-final-review.md` | The earlier zoom-readability FAIL before the 15 px/cell evidence (T041-F1) |
| Profiling infrastructure (T040-F2) | T064 section later in `mobile-performance.md` | the T040 disposition paragraph |
| Task status | closed GitHub Issues | `tasks.md` checkboxes (mirrored data) |

(S001)

When a fix lands, amend or supersede the original review file. Otherwise every later reader has to rediscover the successor verdict. (S001)

## Scope of fresh platform evidence

T041 re-confirmed Android only with a lightweight build, install, launch and logcat smoke run, because no Android, presentation-sync or performance path had changed since T065. The full T057 journey and the T040–T065 performance protocol were cited, not repeated. On-device responsive coverage is a single emulator profile (1080×2400); the other quickstart device sizes exist only as the editor-simulated matrix. All Android evidence is emulator-only. Physical devices, release builds and iOS (Issue #39, deferred) are unverified. (S001)

## Open after T041

- T041-F1: zoom readability at 640×360 and orthographic size 12 (15 px/cell) was never evidenced (T037 FAIL). Art Director and Technical Art must resolve it. (S001)
- T041-F2: SC-002 has no literal D001 end-to-end test. (S001)
- T041-F3: the persistence security review file has not been amended. (S001)
- No mobile measurement covers an actively producing factory; see [Mobile performance targets](./mobile-performance-targets.md). (S001)

## Phase 10 closure (2026-09-28)

T066 renders showed the 640x360 factory readable (15 px/cell) and the Art Director passed. T067 added the D001 two-cycle test; T068 recorded the T051 successor PASS while retaining T029 FAIL/P2. Android stays emulator-only; physical-device, release and iOS (#39) qualification stay unverified. (S001)

## Evidence currency (Feature 003)

Review verdicts rest on a commit; before accepting, compare the newest complete run with every later merge that touches `Assets/`, `ProjectSettings/` or `Packages/`. A run that predates them is supporting, not final, and an editor pass does not prove the player. See [F003 feature review](./f003-feature-review.md). (S018)

An acceptance run that finds a regression restarts on the fix commit: T133 found the Android Back regression on `8a8e518`, the owner chose to fix first (T251), and the whole run was repeated on `ac4256c`; the first run is kept as history, not as evidence. A test-player property can be masked by the harness (the test player already sets `runInBackground` true), so assert an observable the production code sets, and show the test fail first. See [F003 final acceptance](./f003-final-acceptance.md) and [Android Back and runInBackground](./f003-android-back-run-in-background.md). (S018)
