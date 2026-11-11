---
title: F005 batch B live gap credit (T337)
type: decision
sources: [S022]
updated: 2026-11-06
---

# F005 batch B live gap credit (T337)

T337 (#402, PR #479). A live gap is a long real-time stall inside a running app (suspend without a lifecycle event, debugger, slow frame) that is credited like an offline return. Contract: `contracts/run-application-f005.md`. (S022)

## Departure formula
The literal plan formula made the passed-in `atUtcMs` a no-op. The implemented rule is (S022)
`departure = max(max(timelineEnd, activeDeparture), min(atUtcMs, clock))`. (S022)
The architect accepted it: a manipulated clock gains nothing beyond what quit-and-restart already allows, because `min(atUtcMs, clock)` never exceeds the real clock read. (S022)

## Where the check sits
- In `FactoryRuntimeHost`, after the real delta is accounted (`accountedCumulativeTicks = cumulativeTicks`) and before `ScaleAndClamp`. Detection keys on the UTC/real delta, never the scaled delta, because a dev-accelerated frame is a synthetic delta (review of #370). (S022)
- The autosave interval and the gap threshold read one accessor (`RunStart.LiveGapThresholdMs ?? 60_000`) so they cannot drift apart. `RecordSuspension(atUtcMs)` still goes through `SaveCheckpoint` and stamps `lastCheckpointUtcMs` from the real clock, not from `atUtcMs`. (S022)

## Departure retry
`RunLifecycleAdapter.RecordDeparture` used to set `departureRecorded` before `RecordSuspension`; if that threw, `Guard` swallowed it and nothing retried. Now a failed departure while still Running clears the flag, so a later pause or focus loss retries. Known residual: if `RecordSuspension(long)` throws while still Running, that one gap is dropped uncredited (no corruption, low risk). (S022)

## Tests
`LiveGapTests` (19), `LiveGapHostTests` (10, later 15), and equivalence case (p), which had been a passing placeholder in T328 and now asserts the real behaviour (`AutoDeliverEquivalenceTests` 54). (S022)

Related: [desktop focus lifecycle](./desktop-focus-lifecycle.md), [session log writer](./f005-session-log-writer.md) (liveGap and forwardJump records), [timeline mapping](./f005-timeline-mapping-and-auto-delivery.md).
