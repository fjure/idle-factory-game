---
title: F005 session log writer and raise sites
type: component
sources: [S022]
updated: 2026-11-06
---

# F005 session log writer and raise sites

T338 (#403, PR #480) built the writer; T364 (#483, PR #486) added the Core raise sites. Plan: [session log and DevTools](./f005-session-log-and-devtools.md). (S022)

## Writer (T338)
- Hand-written JSON lines, in-memory ring, two rotating segments, export via temp file and rename. The sink is fixed at `RunSession` construction, so the Unity adapter always passes a non-null sink and applies the PlayerPrefs toggle inside it: toggle off returns silently without counting a drop. Raise and write on the main thread only; `DroppedEvents` is per session. (S022)
- **Privacy allowlist (SEC-1):** every `...Id` field is validated against the catalog ID set or a strict pattern (`SessionLogIds.IsValid`); a failing value, such as a GUID, is dropped and counted. `sessionId` comes from `RandomNumberGenerator` (128-bit, width tested). Header `TimeScale` is the DevTools `DisplayFactor` clamped to [1, int.MaxValue] at header time (never cast `Infinity`); it is a snapshot at session start. (S022)
- **Lock order:** serialization goes through `_ioGate`, the only path to the file; the writer's `_gate` is taken inside it, never the reverse. The UI export path is shown but never logged. (S022)
- **Run neutrality:** a run with logging on, off and failing produces byte-identical saves (`RunNeutrality` tests). Mutation checks: ID validation off gave 14 failures, recording-off guard removed gave 1. (S022)
- **PF-7:** SESSIONLOG-FLUSH max 3.7 ms player and 3.8 ms editor with 140 GC.Alloc samples at calibration (QA re-measure 4.7-5.5 ms); budget 25 ms and 256 samples. Android calibration deferred to T351 ([Android notes](./f005-batch-b-ui-and-android.md)). (S022)
- Offline outcomes (auto-delivery, production completion, expiry) are raised from the offline summary because `OfflineSegmenter` calls `Advance` directly; `Blocked` is mapped. The live-gap flag is wired through `RunLifecycleAdapter.IsLiveGapReturn`. (S022)

## Raise sites (T364, owner decision 2026-10-21)
- Reviewers found the T338 scope had no Core raise sites, so the US8 Independent Test was unmet; T364 was added before T343. Core raises Contract Accept/Deliver/Abandon (after the mutation; rejections raise nothing), Purchase (Build, Upgrade, Storage, Factory, Research, Industry) with `secondsSinceAffordable`, Prestige once on publish with `secondsSinceEligible`, Bottleneck, and Clock `liveGap` and `forwardJump` (5 s surplus). Synchronous flush after prestige, offline return and live gap. (S022)
- **Bottleneck is derived from the read model per advance step**, counting blocked machines in machine-seconds (blocked for at least 2 consecutive live steps), not from simulator counters; public `RunSession.RaiseBottlenecks()`. Values are lower bounds, which is what SC-005 needs. (S022)
- **`IRunLogRecordingState`:** an optional interface the sink implements so Bottleneck accrual (and its forced `RefreshViews`) runs only while recording is on. A decorating sink must forward it or accrual silently stops. API baseline +2 lines. (S022)
- **Meters survive same-run returns:** `ReplaceLoaded` for the same run (ordinary offline return or load) raises the open Bottleneck window first and keeps eligibility and the affordability history; only a new run or successor resets. `secondsSinceAffordable` is therefore a lower bound across returns. (S022)
- QA round 1 failed on: a spurious `forwardJump` after an unreadable-clock frame (raise only with a prior reference), a `double` in Core (integer cross-multiplication for trace decimation), and the affordability pop keeping the new time instead of the earliest (50@0, 150@10, 120@20, cost 100 must give 20 s, not 10 s). Raise Bottleneck also at departure and quit. (S022)
- Perf: SESSIONLOG-FLUSH max 4.6 ms player and 5.5 ms editor with 140 samples; Advance with a no-op sink allocates 0 bytes. (S022)

## Known gap
Cold-start durable recovery without a pending candidate raises no Prestige record (T364 re-review note). (S022)

Related: [run persistence](./run-persistence.md), [live gap](./f005-batch-b-live-gap.md).
