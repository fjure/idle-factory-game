# Playtest: Flo (owner), recorded 2026-10-27

**Task**: T353 (#418, human gate, parent-only). **Playtester**: Flo, the project owner. The owner did not author the game code or content; agents built both, so the "builder cannot be the playtester" rule holds.
**Build**: the owner's own build of `main` with `contentRevision` 3 (`Assets/IdleFactory/Content/Iron/iron-factory-content.json`, blob `5f7420b`, last changed in `2176f51`, shipped by PR #496 on 2026-10-22). The exact build commit, platform and play date were not recorded; the owner reported the playtest on 2026-10-27 as done "recently" (before T367, PR #504).
**Scope actually played**: the first prestige and a second run were reached (owner's answer, 2026-10-27).
**Source of this record**: the owner's statements in the Claude Code session on 2026-10-27, quoted below. The parent wrote this file; the owner did not dictate its wording.

## Verdict

Owner's words: "Ich habe den Playtest schon letztens durchgeführt. Ich finde die Umsetzungen von dir super. Ich gebe die Owner-Gates frei, wenn irgendwann wieder was angepasst werden muss, werde ich das als Feature aufnehmen lassen."

The owner is satisfied with the revision-3 build and releases the owner gates T353 and T354. Further tuning or changes are future feature work, not F005.

## SC-005 question

"Did both selling and contracts matter to your income?" (1–5): **5** (owner's answer, 2026-10-27). The SC-005 target is 4 or higher, so it is met.

## Times and deviation from the trace

| Item | Value |
|---|---|
| Session log export | **None.** The owner did not export a session log. |
| Human-to-trace ratio at the timeline samples | **Not measured.** Without a session log and without stopwatch times, the ratio against `balance-trace-r3.md` cannot be computed. |
| First prestige reached | Yes (owner). Time not recorded; the r3 trace places it at 7,148 s. |
| Second run reached | Yes (owner). Time not recorded. |

## Deviations from SC-005 and T353

- SC-005 and T353 ask for the session log and the human-to-trace ratio. Both are missing. As in F003 (`specs/003-first-factory-prestige/reviews/playtest-flo-20261009.md`), the owner's feel judgment is the acceptance basis, and the owner keeps the veto. This record makes no claim about human timings.
- The T345 Product Owner watch list on #418 (early walls at factory level 2 and 3, cooldown messages, the 1.8× bonus, the Item ladder, Seed Capital in runs 3+) was not answered item by item. The owner's overall verdict covers it; any finding later raised goes into a future feature.
- The play date and build commit are unknown, so it is not established whether the build included T348–T352 (tests and docs only) or the T367 UI fixes (it predates PR #504).
