---
title: F005 batch B performance measurements
type: reference
sources: [S022, S027, S029, S032]
updated: 2026-10-27
---

# F005 batch B performance measurements

All Windows Mono development player, quiet host, round-robin launches, fixture `hashOk` checked. Record: `reviews/w003b-measurement.md` (S027), `reviews/w005l-measurement.md` (S029). Windows only (PF-13). (S022)

## W003-B re-measure (T318 #381, T329 #394; PRs #476, #477)
- One shared session (owner, 2026-10-15). Baseline is the post-Option-1 T360 reference measured in the same session (201.12 / 200.12 ms; recorded 199.61 ms), not the 962.5 ms T300 figure. (S022, S027)
- T317 (`78f2a25`): 204.50 ms, +1.7 %, 10 % guard met. T325 +2.0 %; T326 216.10 ms (+7.4 %); T327 221.82 ms (+10.3 %), 222.28 ms with auto-deliver pending (+10.5 %). The 500 ms gate holds throughout. (S027)
- Auto-deliver itself costs 0.2-0.6 %; growth sits on the common simulator path (+5.3 % at T326, +2.6 % at T327). The T326 worker's +2 % per completion estimate was not reproduced. PF-1: realized `LastMaxPassesPerTimestamp` 2 (3 with early delivery), budget machines + 2 = 14, `LastPassBudget` reads 0 unless exceeded. (S027)
- **PF-10 re-baselined at 222.28 ms (owner, 2026-10-21, applied on #414):** the 10 % guard was exceeded by 0.3-0.7 points; options were optimise, waive or re-baseline, and the owner chose re-baseline. T349 evaluates PF-10 against 222.28 ms. Cost: the fixed F005 anchor is gone. (S022, S027)
- Deviation PF-9: the host was not strictly idle (about 1.9 of 16 threads busy, constant across series), no Unity or adb during timed runs. (S022)

## W005-L (T336 #401)
Median of run medians over 10 launches (2026-10-21 09:23Z): algorithm 233.8 ms, full return 274.1 ms, spreads 5.4 % and 6.7 %, budget 500 ms met. V3 write mapper 0.35 ms (noise), Promote 17.6 ms. (S029)
- **Revision 3 risk (2026-10-22):** on a loaded host the regenerated W005-L full return measured 548.5 ms (algorithm 449.4 ms) against 500 ms; indicative only; the batch-C end player suite measured 376.8 ms full / 332.3 ms algorithm (inside budget), and T349 records the quiet-host value. See [content revision 3](./f005-batch-c-content-revision-3.md). (S029, S032)
- **Probe-player method:** the tagged `PlayerWithTests.exe` cannot run standalone, so timing uses a Mono development probe player launched repeatedly, with the fixture hash logged each launch. (S022)

## Session log
SESSIONLOG-FLUSH budget 25 ms and 256 samples against 3.7-5.5 ms and 140 measured; details in [session log writer](./f005-session-log-writer.md). (S022)

Related: [factory run bounds](./factory-run-bounds-and-offline-segmentation.md), [W003 player timing baseline](./w003-player-timing-baseline.md), [Save V3 drift](./f005-batch-b-save-v3-drift.md).

## Final values (batch D)
T349 measured W005-L revision 3 at 273.5 ms algorithm / 312.1 ms full return and PF-10 at 219.24 ms against 222.28 ms: both met. See [batch D final evidence](./f005-batch-d-final-evidence.md). (S029)
