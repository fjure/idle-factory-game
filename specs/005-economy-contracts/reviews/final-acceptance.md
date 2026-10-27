# Feature 005: Final acceptance (T355)

**Task:** T355 (#420, work unit WU67), after T347–T354 and T367. Covers SC-001 to SC-011.
**Accepted commit:** `4af2389` (origin/main, merge of PR #508, T349). It is code-identical to `cc0afd3`: no change under `Assets/`, `ProjectSettings/` or `Packages/` between them. Every suite of record ran on `cc0afd3`.
**Date:** 2026-10-27. **Parent:** Claude Code orchestrator (High tier). The independent verdicts are in `feature-review.md`.
**Host:** Windows 11 Pro 10.0.26200, AMD Ryzen 7 7800X3D, Unity `6000.6.0f1`. Emulator `emulator-5554`, AVD `Medium_Phone_API_36.0` (T351 only).

## 1. Verdict

**ACCEPTED WITH RECORDED DEVIATIONS.** Eight independent reviewers accept with follow-ups and find no blocker. Every suite is green on the accepted code with 0 skips. The console holds no relevant error (F-1 and F-2 are compile warnings, section 5). The release scans of a Windows and an Android release build pass. Every performance budget is met on Windows. The owner played revision 3 and approved it. The deviations in section 4 are recorded, not hidden. The `after_implement` hooks, the hook gate and convergence follow in T356; the feature is complete only after them.

## 2. Owner decisions recorded for batch D

| Date | Issue | Decision |
|---|---|---|
| 2026-10-21 | #414 | PF-10 re-baseline: W003-B with auto-deliver is guarded against 222.28 ms (T327). |
| 2026-10-27 | #417 | T352 review: Q8 yes, one bundled fix task T367 before T353–T356; Q1–Q7 as the reviewer recommended (Q3 pre-stall warning and Q7 store-full hint to later features; Q6 tooltip-only accessible names accepted). |
| 2026-10-27 | #418 | T353: owner playtest reached the first prestige and a second run; SC-005 answer 5; no session log. |
| 2026-10-27 | #419 | T354: `contentRevision` 3 approved as shipped (blob `5f7420b`); open #419 items accepted; validator follow-ups to a later feature. "Wenn irgendwann wieder was angepasst werden muss, werde ich das als Feature aufnehmen lassen." |
| 2026-10-27 | #414 | T349: TFT closed; Steam, Riot, Docker and Chrome stay open and are recorded as PF-9 host load. |

## 3. Evidence on the accepted code

| Evidence | Record | Result |
|---|---|---|
| Edit Mode, full | `full-suite-evidence.md` (T347) | 2323/2323, 0 skipped |
| Play Mode, full | `full-suite-evidence.md` | 2230/2230, 0 skipped |
| Windows test-player suite, once for batch D | `full-suite-evidence.md` | 39/39 (`[Category("StandalonePlayer")]`) |
| DevTools assembly | `full-suite-evidence.md` | 68/68 |
| BalanceFull | `full-suite-evidence.md` | 7/7 |
| Console | `full-suite-evidence.md` | no `Unknown pseudo`, no `[Error]`; errors only test-provoked or licensing; F-1, F-2 warnings |
| StandalonePlayer journey, V2→V3 round trip, fixture parity, log-failure isolation | T348 (#413) | 6 new tests in the 39 |
| W005-L 8 h return, Windows | `w005l-measurement.md` (T349) | algorithm 273.5 ms (spread 4.4 %), full return 312.1 ms (3.6 %); budget 500 ms |
| PF-10 W003-B with auto-deliver | `w005l-measurement.md` (T349) | 219.24 ms, −1.4 % vs 222.28 ms (limit +10 %) |
| Frame time with UI at W005-L | `w005l-measurement.md` (T349) | avg 16.68 ms, p95 16.87–16.89 ms at the 60 fps cap |
| Acceleration (development player) | `w005l-measurement.md` (T349) | host-step p95 0.17–0.20 ms at ×600, 0.20–0.21 ms at Max; budget 1 ms |
| Session-log flush (PF-7), Windows | `SessionLogAllocationTests` (StandalonePlayer, green in T347) asserts flush ≤ 25 ms and ≤ 256 samples; measured 3.7–5.5 ms and 140 samples (wiki `f005-session-log-writer.md`) | met on Windows; Android unmeasured |
| Emulator journey (evidence only) | `emulator-journey-f005.md` (T351) | PASS; W005-L return median 813 ms (≤ 2 s); 60.6 fps; export path shown and pulled |
| Release scan | `release-scan.md` (T350) | Windows non-development and Android release APK: 0 findings |
| UI/UX | `ui-ux-review.md` (T352 and §8 T367 re-review) | accept with follow-ups; R-01, R-06, R-20 fixed |
| Owner gates | `playtest-flo-20261027.md`, `content-approval-r3.md` | released |

## 4. Success criteria

| SC | Status | Basis |
|---|---|---|
| SC-001 | met | ratio bounds asserted; `balance-trace-r3.md` |
| SC-002 | met, recorded deviation | contracts-only stalls by design; no bootstrap exception (owner) |
| SC-003 | met | T347 suites |
| SC-004 | met | IP in runs 1–4 = 20 % of the ceiling, limit 25 % |
| SC-005 | met, recorded deviation | owner answer 5 (target ≥ 4); no session log, so the human-to-trace ratio is not measured; build and date not recorded; owner veto, as in F003 |
| SC-006 | met | T347 suites |
| SC-007 | met on Windows (Mono) | T349; IL2CPP open (section 5) |
| SC-008 | met | session-log export on Windows and the Android emulator |
| SC-009 | met synthetically, recorded deviation | Play Mode suites over all screen classes and 100/200 % text with touch and pointer cases; real-device touch, screen reader and OS font scale unverified |
| SC-010 | met | Play-Tester reports for batches 1, A, B, C; owner reviewed each |
| SC-011 | met | MIN-x gates in the trace |

## 5. Open items and follow-ups (all non-blocking)

Each item below was dispositioned by the reviewers in `feature-review.md`. None blocks completion. Per the owner statement of 2026-10-27, they become later features.

| Area | Item | Disposition |
|---|---|---|
| Platform | IL2CPP Windows player not measured (PF-13); Mono headroom 1.83× algorithm, 1.60× full return | later performance follow-up |
| Android | PF-7 flush calibration and DevTools overlay smoke need a GLES3 test-player or DevTools+GLES3 build path; the T040 Profile step still reads `Checkpoints/v1`; journey taps are portrait-only; emulator PF-9 load unmeasured | later DevOps/Test feature |
| Devices | real touch, TalkBack/VoiceOver, notch and rotation, OS font scale 2.0, the T352 §5 checks C-1 to C-9 and the T367 device list; Android backup via a real transport (`bmgr`) | device pass before the next phone release (gates SC-009 on phones) |
| UI (T367 follow-ups) | R-23 matches machine type, not resource; R-20 focus target (Upgrade safer than Active); "+$500 starting Cash"; no reason line for other non-acceptable offers; "could work for 0 s"; focus restoration on offer re-sort untested | later UI feature (F004 polish) |
| Architecture | `ItemRewardWithheld` re-derives the per-run Item cap in the view → expose a flag on `ContractOfferSnapshot`, with the `perRunCap` decision (#387) | later feature |
| Code hygiene | F-1 obsolete `EventBase.PreventDefault()` (CS0618) in `ResearchPanel.cs:550, 651, 663`; F-2 five test-code warnings (CS0184 at `SessionLogPanelTests:269` is an always-false check) | fix with the next UI feature or Unity upgrade |
| Tests | `EconomyJourneyPlayerTests.cs:87` magic `Cash < 9` guard; `WithoutRunId` leaves `prev=`; zero-allocation assertion with logging on reads `≤ without` rather than 0 | later test hygiene |
| Content | validator follow-ups #379 q4, #396 `minGainPpm` units, the bonus-ratio message text; ECO-7 stock-bound headroom 0.02 pp and single-lever margin 1.21× (offset any future buff) | later feature (owner) |
| Build | `IdleFactoryBackupRules.androidlib/build.gradle` hard-codes SDK 36/26 (header note only) | guard later |
| Product | smart auto-sell, reserve mode C-F1/C-F2, pre-stall warning, store-full hint | later features (owner decisions #411, #417) |

## 6. Completion gate (AGENTS.md)

| Item | Status |
|---|---|
| Acceptance criteria pass | yes (sections 3, 4) |
| Edit Mode and Play Mode tests pass | yes (T347) |
| Windows test-player suite once for batch D | yes (T347, 39/39) |
| Unity console without relevant errors | yes (F-1/F-2 warnings recorded) |
| QA passed on each committed result | yes: `maqa:event:qa-pass` on #412–#417 and #501 |
| Independent UI/UX acceptance | yes (T352, T367 re-review, T355 UI/UX) |
| Performance measured against declared workloads | yes (T349) |
| Owner gates | yes (T353, T354) |
| Wiki ingest, learn review, wiki lint | done (T356, 2026-10-27): four `f005-batch-d-*` pages, `learn.md` batch-D section, lint clean apart from two pre-existing over-length pages and one recorded source conflict |
| `after_implement` hook gate | PASS (`scripts/check-after-implement-hooks.ps1 -FeatureDir specs/005-economy-contracts`, 2026-10-27) |
| Convergence (`speckit-converge`) | **converged**: 45 FR, 11 SC, US1–US10 scenarios, 9 edge-case groups, about 16 plan decisions and 9 constitution principles checked; 0 actionable findings; `tasks.md` unchanged. 14 accepted deviations, all listed in section 5. Borderline item: `ItemRewardWithheld` (view-side display predicate, Constitution II/V) stays a later-feature item tied to #387, as every reviewer dispositioned it |
| Stale-worktree report | no MAQA worktrees left; the coordination worktree (kept for the next session) and one worktree of another session are reported removable and left in place |
