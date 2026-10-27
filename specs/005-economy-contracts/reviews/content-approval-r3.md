# Content approval: revision 3

**Task**: T354 (#419, owner gate, parent-only). **Owner**: Flo. **Date**: 2026-10-27.
**Approved**: `Assets/IdleFactory/Content/Iron/iron-factory-content.json` and its Play Mode copy `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/iron-factory-content.json`, `contentRevision` 3 (both blob `5f7420b`, last changed in `2176f51`), as they stand on `origin/main` at `2b6b46e`.
**Inputs**: `balance-trace-r3.md` (T345), `balance-policy-r2.md` and `policy-r2-record.md`, `strategy-baseline-r3.md`, `playtest-agent-batch-c.md` (T346), the owner playtest record `playtest-flo-20261027.md` (T353), and the open owner questions from the T345 reviews on #419.
**Owner decision**: `maqa:decision:#419:owner-2026-10-27` on #419. Owner's words: "Ich gebe die Owner-Gates frei, wenn irgendwann wieder was angepasst werden muss, werde ich das als Feature aufnehmen lassen."

## Decision

**Approved as shipped.** No content change is requested, so T345 is not reopened. FR-027 is satisfied: the r3 trace exists and a named human playtest reached the first prestige and a second run (T353, with the deviations recorded there).

| Open item on #419 (T345 reviews) | Decision |
|---|---|
| 1. Approve only after the human playtest T353 | Met: T353 is recorded first (`playtest-flo-20261027.md`). |
| 2. A first run of about 2 h (trace 7,148 s) and the widening casual gap (1.68× in run 1 to 2.95× in run 5) | Accepted as shipped. Minute bands stay orientation, as in F003; the owner's feel judgment decides. |
| 3. Seed Capital size (+500 per level, `maxLevel` stays 10 for save compatibility) | Accepted. |
| 4. `contracts-off` +350 ‰ and `returns` as added wall time (released provisionally on 2026-10-22) | Confirmed. |
| 5. Three documented test-tolerance loosenings (Plate cooldown 3 %, casual diagnostic 1.5×/1.25×, price-variant order within 10 %) | Accepted. |
| 6. Deferred validator follow-ups: recipe cycle as a reported error and a rule rejecting content without an input-free recipe (#379 q4), `minGainPpm` unit unification (#396), the inverted bonus-ratio message text (`RunContentValidator.cs` ~1605) | Deferred to a future feature, not F005. |

## Consequences

- SC-005 is met on the owner's answer (5 of 5), with the session-log and timing gaps recorded in T353.
- Any later tuning or change requested by the owner becomes a new feature (owner statement, 2026-10-27).
- T355 checks that the shipped content on the accepted commit equals this approved revision (blob `5f7420b`).
