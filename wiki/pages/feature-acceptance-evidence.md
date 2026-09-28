---
title: Feature acceptance evidence
type: howto
sources: [S001]
updated: 2026-09-28
---

# Feature acceptance evidence

How Feature 001's final acceptance (T041, 2026-09-28) was reconciled, and which records outrank which. Use it when you write or read a later acceptance or review file. (S001)

## Reconciling requirements

- Every FR and SC row cites a concrete source: a review section, a named test, or a GitHub Issue comment. A row without a cited source is not PASS. (S001)
- "Fresh confirms" means that the cited test ran green in the fresh complete suite. It does not mean each test was retraced line by line. T041 ran Edit Mode 569/569, Play Mode 147/147 and the Windows player suite 2/2 on `8c28d69`, after a forced recompile. (S001)
- Grep the test suites before citing a test name, and check that a fixture matches the requirement's literal values. SC-002 looked covered but was composed from two fixtures (T041-F2); see [Player-level test gaps](./player-level-test-gaps.md). (S001)
- Headline counts must match the row statuses. The T041 headline said "all 47 pass", while one row was PARTIAL; QA flagged the mismatch as non-blocking. (S001)

## Which record wins

| Question | Authoritative record | Stale-looking record |
|---|---|---|
| Android runtime journey | T057 rerun in `windows-android-validation.md` | the original T038 FAIL/UNRESOLVED header |
| Persistence security | Issue #71 exact-commit Security/Privacy PASS for T051 | `persistence-security-review.md` "Verdict: FAIL" (pre-T051, T041-F3) |
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
