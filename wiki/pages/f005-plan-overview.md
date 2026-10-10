---
title: Feature 005 plan overview (planned)
type: decision
sources: [S022]
updated: 2026-10-10
---

# Feature 005 plan overview (planned, not shipped)

Everything on this page is **planned (F005)**. The plan passed three review waves (Architect, Test/QA, Gameplay, Game Systems, UI/UX, Performance, Security, DevOps). Every finding has a disposition in `specs/005-economy-contracts/reviews/plan-reviews.md`; research entries are `F5-R01` to `F5-R18`. (S022)

## Owner decisions

- **D-A** one feature; the offline value redesign (80 % effectiveness, 8 h cap) stays out until the session log gives real return data.
- **D-B** order: behaviour-preserving #356 refactor first, then dev-only time acceleration and strategy policies (so the Play-Tester can play), then gameplay.
- **D-C** the Factory Sale threshold rises per prestige; Industry loses hard caps for diminishing returns (practical ceiling at least 4.0x the IP of runs 1-4).
- **D-D** contract Cash = ratio of the base market value of the delivered stock; standing offers at most 1.5, 1.5-2.0 reserved for bonus offers.
- **D-E** the chain must earn more per slot than Ore-only, inside the whole-number content format, guarded by a static check.
- **D-G** Save V3 for new state; V2 loads with defaults; downgrade unsupported.
- **D-H** gate = realistic late-game workload W005-L against the existing return budgets; W003-B is measured on the pre-F005 base first.
- **D-I** accelerated sessions are marked only in the session log.
- **D-J** starting Cash grows per prestige. **D-K** the threshold stops rising at `maxIndex`, prestige continues. **D-L** auto-sell reserves stock for every active delivery contract.
- **D-M** Android exports the log by path (`adb pull`), no share intent or FileProvider; backup exclusion mandatory. **D-N** dev builds use `IdleFactory-Dev/` as save root. (S022)

## Design decisions in brief

- One shared sale price and one reward formula, in Domain (F5-R04). See [run contracts](./run-contracts.md) and [run economy and prestige](./run-economy-and-prestige.md).
- Content schema v3, strict, reader evolved in place, F003 offer IDs kept (F5-R03). Save side: [Save V2 document](./save-v2-document.md).
- Timeline mapping and in-simulator auto-delivery: [F005 timeline mapping and auto-delivery](./f005-timeline-mapping-and-auto-delivery.md).
- Session log and DevTools: [F005 session log and dev time acceleration](./f005-session-log-and-devtools.md).
- Large live gap (F5-R10): [desktop focus lifecycle](./desktop-focus-lifecycle.md).
- Refactor shape and API gate: [core architecture](./core-architecture.md). Persistence changes: [run persistence](./run-persistence.md).
- Balance gates and policy r2: [run balance trace](./run-balance-trace.md).

## Workload and budget rules

Quiet host, median of 5 runs, 10 % spread limit, 10 % regression guard. W005-L gate: 8 h return at most 500 ms (Windows Mono player) and 2 s (emulator), 60 fps. W003-B is re-measured after the units that touch the settle path. IL2CPP on Windows is a recorded open item. (S022)
