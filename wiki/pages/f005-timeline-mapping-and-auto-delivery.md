---
title: F005 timeline mapping and auto-delivery (planned)
type: decision
sources: [S022]
updated: 2026-10-10
---

# F005 timeline mapping and auto-delivery (planned)

**Planned (F005), not shipped.** Source: research F5-R05, F5-R17, F5-R18 and review items AR-1..4, GD-1..3, PF-1/2. (S022)

## TimelineMapping

One value maps simulated ticks to real UTC milliseconds: `OriginUtcMs`, `OriginTickPhase` (0 to 9,999, the sub-millisecond remainder) and `RealPerSim` as `num/den` (1/1 live, 5/4 for 80 % offline). `RealUtc(t) = OriginUtcMs + floor((phase + t*num/den) / 10,000)`, checked integer math, the only rounding rule. Session, `OfflineSegmenter` and `RunSimulator.Advance(activeTicks, mapping, ...)` share it, so any live partition yields identical stamps. It is called per advance and per stamp, never per completion. (S022)

## Per-instant order

1. Boundary events (deadline expiry at deadline + 1 tick, Item and Overdrive ends).
2. Settle fixed point: priority completions (also count production objectives), auto-delivery (at most once per timestamp), auto-sell keeping the reserve. Pass bound N + 2, fail closed at the bound.
3. Unlock pass, which also records the bonus-offer trigger. (S022)

## Rules worth remembering

- Cooldowns and bonus windows are stored UTC stamps evaluated lazily; they are **not** boundary events and change no simulated state.
- The simulation order decides the deadline race; there is no real-time compare for delivery.
- The simulator keeps scratch copies (`_stock`, `_cash`, `_rp`, contract state, Item grants) and commits at the end of the advance. An overflow (`value.limit`) blocks auto-delivery without change; it retries each instant.
- Reserve (D-L): while any delivery contract is active, auto-sell keeps `min(stock, required)`, also while delivery is blocked.
- `SetAutoDeliver(true)` uses the `DeliverContract` worst-case pre-check; the next settle delivers at t = 0.
- `ContractBoard.Version` goes into publish tokens. The session raises `IRunEventSink` events by comparing `LastOutcome` before and after an advance; the simulator never calls the sink. (S022)

Related: [F005 plan overview](./f005-plan-overview.md), [run session](./run-session.md), [factory run simulation](./factory-run-simulation.md), [factory run bounds and offline segmentation](./factory-run-bounds-and-offline-segmentation.md).
