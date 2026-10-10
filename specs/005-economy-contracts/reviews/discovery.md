# Feature 005 discovery record

**Date**: 2026-10-10. **Base**: `origin/main` at `a41c53f`. **Parent**: Claude Code orchestrator (Opus). All reviewers were read-only and independent of each other.

## Wiki recovery (`before_specify` hook)

The `speckit.wiki.query` hook ran with the F005 domain question. Pages cited: run-economy-and-prestige, run-contracts, run-commands-and-snapshots, run-balance-trace, run-boosts-and-progression, desktop-focus-lifecycle, f003-final-acceptance, run-persistence. Coverage was **partial**. The wiki did not record:

- the market price and auto-sell mechanics;
- build-cost scaling, the Toolmaker cap and the ECO-3 caps;
- how contracts are generated and their cadence;
- the seven r2 trace decisions;
- the content of #356 and #358.

These gaps were filled from primary sources: `specs/003-first-factory-prestige/reviews/balance-trace-r2.md` §6–§8, `content-approval.md`, `feature-review.md` ("Data gaps for F005", E-1..E-8, F-A1..F-A7) and Issues #353–#358. The next wiki ingest should take in balance-trace-r2, content-approval and balance-policy.

## Inputs

- Owner playtest 2026-10-09, notes 4–16 (`specs/003-first-factory-prestige/reviews/playtest-flo-20261009.md`).
- `docs/product/ideas.md`, F005 table.
- The F003 final acceptance follow-ups #353 (W003-B), #354 (safety checkpoint), #355 (reason codes), #356 (RunPersistence/RunSnapshots split), #357 (strategy probes) and #358 (telemetry and playtest list; this spec cites it).

## Wave 1: product discovery

| Lens | Model | Verdict | Key findings |
|---|---|---|---|
| Game Designer / Product Owner | sonnet | NEEDS-OWNER-INPUT | The player problem is "no verb for earning by hand, so the loop collapses to contracts". The draft was too large: split it or cut offline value and tooling. It lists probe-based success criteria (contracts-only slower than mixed, Ore-only gap reduced). |
| Game Systems / Economy | sonnet | NEEDS-OWNER-INPUT | At base prices the chain earns about 17 % less per slot-second than Ore-only, and slots bind, not Cash. Contract rewards are fixed dollars and ignore sale-price modifiers. About 9 of 10 first-prestige IP buy nearly all Industry caps, so runs 3–5 are flat. Build cost is flat (`CostTable[0]`). Further items: the reference policy r2 fixes, a strategy matrix for runs 1–5, an ensemble on 5 phase offsets, rule 8 for large deltas, and the session-log field list. |
| UI/UX | sonnet | READY-TO-SPECIFY | Journeys J1–J7 with normal, soft-disabled, empty, rejection, confirmation and recovery states. A step selector per inventory row, an auto-deliver toggle on the active card, a minimal text badge on the Contracts tab, and goals at the top of Market. It also defines a hand-off contract to F004. |

Play-Tester probe: **omitted**. Only the reference policy exists and there is no time acceleration, so a probe would add nothing beyond trace r2. F005 delivers both.

## Owner decisions, round 1

- **D-F** (2026-10-10, before wave 1): a local session log, no network. #356 is the first work unit. #353, #354, #355 and #357 are in scope. Smart auto-sell is out (it stays an idea).
- **D-A**: one feature. The offline-value redesign (efficiency and cap) is out until real return data exists.
- **D-B**: order of work is #356, then time acceleration and strategy policies, then gameplay. The Play-Tester is mandatory from the first gameplay batch.
- **D-C**: the prestige threshold rises per prestige, and ECO-3 is restated. Industry sinks have diminishing returns instead of hard caps.
- **D-D**: contract reward is a ratio to the base price under the same sale-price modifiers. Standing offers have a ratio of at most 1.5, and bonus offers use up to 2.0.
- **D-E**: chain value per slot is fixed within the integer content format, guarded by a static dominance gate. There is no price-unit migration.

## Wave 2: technical feasibility

| Lens | Model | Verdict | Key findings |
|---|---|---|---|
| Software Architect | sonnet (high reasoning) | FEASIBLE-WITH-CONDITIONS | **#356**: behaviour-preserving and strictly first. RunPersistence splits into partial files by responsibility, and RunSnapshots splits per read model. Command-path RunState mutators become internal. The gate is a public-API diff. **Auto-deliver**: must run inside the simulator per completion instant (completions → deliver → auto-sell), with shared reward arithmetic. **Save format**: the strict reader means an additive field needs Save V3. **Rising threshold**: the index is the persisted PrestigeCount. **Large delta**: a "record departure at" operation plus the standard reconcile. **Dev time scale**: an assembly-gated seam, proven by a build scan. **Session log**: a Core port with an isolated Unity adapter, outside the save. |
| Security / Privacy | haiku | CONDITIONS | **Session log**: allowlisted typed fields, no PII, paths or device identifiers. Export goes only to app-owned storage (Android FileProvider, no storage permission), and the log is excluded from Android Auto Backup. A log failure is isolated from the save and lifecycle callbacks. **Time acceleration**: absent from release, proven by a build check. **Clocks and saves**: a negative delta clamps to 0, the threshold is not editable by the player, and new save fields are validated strictly. **Clock risk**: E-5/S1-6 stays accepted, and rule 8 narrows it. |
| Performance | haiku | CONDITIONS | Declare a time-acceleration cap S_max (proposal 10 simulated s per frame, p95 ≤ 1 ms on Windows). The session log does no work and no allocation inside the simulation advance and flushes at most every 30 s. Measure #353 Option 0 on the pre-F005 base. Measure W003-B and a realistic late-game fixture with auto-deliver active. Cache build-cost and goal values on revision. |

Conflict resolved by the parent: Performance asked for auto-deliver to be checked only at boundaries. The Architect showed that segment invariance needs per-completion evaluation. Resolution: evaluate inside the simulator with an O(1) check per pass, and meet Performance condition C3 by measuring W003-B and the realistic fixture with auto-deliver active.

## Owner decisions, round 2

- **D-G**: Save V3 (`schemaVersion` 3). The reader still accepts V2 with defined defaults and keeps rejecting unknown members. There is no downgrade.
- **D-H**: W003-B stays a documented bound. The F005 gate is a realistic late-game fixture held to the SC-008 budgets. #353 Option 0 is measured on `a41c53f`, and the hot-path decision comes before the settle and auto-sell change.
- **D-I**: accelerated dev sessions are marked only in the session log.

## Omitted lenses

- **Art Direction and Technical Art**: F005 adds no new visual assets. Restyling belongs to F004.
- **Sound and Composer**: not activated in this phase.
- **Online Services**: none in scope.
- **DevOps**: not needed for specify. It is required in planning for the dev-build define, the release build scan and the Android Auto Backup rule.
