# Playtest: Flo (owner), 2026-10-09

**Task**: T126 (human gate, parent-only). **Playtester**: Flo, the project owner. The owner did not author the game code or content; agents built both, so the "builder cannot be the playtester" rule holds.
**Build**: Windows 64-bit player (`IdleFactory.Editor.PlatformBuildCommands.BuildWindows64`, Unity `6000.6.0f1`, non-development build), built from `origin/main` at `fced3ea`. The build log reports "Succeeded, 0 errors". Content: `iron-factory-content.json` `contentRevision` 2 (blob `66f41fb`), traced in `balance-trace-r2.md`.
**Setup**: fresh save folder, full screen at 1920×1080, mouse and keyboard.
**Scope actually played**: part of the first run, stopped by the owner at about $40,000 Factory Value. Neither the first prestige nor a second run was reached, and no milestone times were taken with a stopwatch.

## Verdict

The owner is pleased with the base game: no conspicuous bugs, everything worked, and the balancing felt good. The owner stopped the test deliberately and judged that the base is sound and the notes below are the next work.

The owner decided (spec › Session 2026-10-09) that the SC-003 and SC-004 minute bands are orientation only: whether the first prestige takes one, two or three hours is not decisive, and the feel decides. On that basis the owner accepts this partial run as the T126 record instead of the full first and second run that the task text asks for. The 2-hour and second-run figures therefore stay unmeasured by a human; this record makes no claim about them.

## Times and deviation from the trace

| Point | Trace r2 (reference policy) | Playtest | Note |
|---|---|---|---|
| Factory Value at minute 15 | 13,034 | about 5,200 | The human was about 2.5× behind the scripted policy at this point. |
| Factory Value at the stop | — | about 40,000 | Not timed exactly. |
| First Gear, prestige eligibility, second run | 720 s, 6940 s, 4640 s | not measured | Stopped before them. |

Feel at minute 15: "1 million Factory Value seems very far away" (the prestige threshold is 10 million). The owner was curious how this feels later; it remains a feel signal for the next balance iteration, not a measured deviation.

## Notes (owner's words, condensed), with disposition

Dispositions: **F003** = a defect fixed before this feature closes (new task); **F004** = UI/UX and graphics feature; **F005** = new economy and contracts feature; **idea** = recorded in `docs/product/ideas.md`; **process** = agent workflow. The full product backlog with these notes is `docs/product/ideas.md`.

| # | Note | Disposition |
|---|---|---|
| 1 | The Research graph is not scrollable on the desktop; going right works only by clicking a node. | F003 → T248 |
| 2 | With a Miner selected while the store goes from 99 % towards 100 %, the UI keeps jumping back and forth. | F003 → T249 |
| 3 | On Alt-Tab the game may keep running; on the desktop offline progress is only needed after a restart. Phones stay as they are. | F003 → T250 (owner decision) |
| 4 | Selling is technically hard as a player; selling 1×, 10× or 100× (for example) is missing. | F005 (sell command) + F004 (UI) |
| 5 | Contracts need an auto-deliver checkbox so that a deadline is not missed while the resources are in stock. | F005 |
| 6 | Contracts should not replace selling: one could deliver contracts permanently and never sell. | F005 |
| 7 | The contract/progression loop feels monotonous: "I just hand in contracts all the time." | F005 |
| 8 | Machine build prices should scale with progress (IP); $69 for an Assembler is practically free. | F005 |
| 9 | After 15 minutes, 1 million Factory Value feels far away. | F005 (intermediate goals, balance r3) |
| 10 | Optional auto-sell of everything not needed by a follow-up process, unlocked later in the game as a skill. | idea (F005 candidate) |
| 11 | A Stats view with net profit per resource; production amounts are hard to read; the Market's net rate can move there. | F004 |
| 12 | The offline popup works but is too much text and too little color. | F004 |
| 13 | Split the Research tree and the IP (Industry) tree. | F004 |
| 14 | The Priorities function is good but takes too much space; it may not belong in Market. | F004 |
| 15 | List the menus and mechanics in a dedicated menu, similar to Idle Obelisk Miner. | F004 |
| 16 | Contracts need a visual cue whether they are deliverable, i.e. whether the materials are already in stock. | F004 |
| 17 | A "Play-Tester" agent role that tries different strategies and reports to the parent; the owner reads the reports and keeps a veto. | process |
