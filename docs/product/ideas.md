# Product backlog and ideas

This file holds product work that the owner has approved as direction but that no feature specification covers yet. It is not workflow status: once a feature is specified, its `spec.md` is authoritative and the matching entries here point to it. Agents read this file before `speckit-specify` for Features 004 and 005.

Source of the current entries: the owner playtest of 2026-10-09 (`specs/003-first-factory-prestige/reviews/playtest-flo-20261009.md`). The owner fixed the order of work on 2026-10-09:

1. Feature 003 closes with its playtest fixes T248–T250 and the reviews T128–T135.
2. A Play-Tester agent role (process work).
3. Feature 005: economy and contracts (gameplay).
4. Feature 004 extended from the visual restyle to a UI/UX and graphics feature, built on Feature 005's mechanics so that menus are designed once.

## Feature 005: economy and contracts (gameplay; not yet specified)

| Item | Owner note | First thoughts (not decisions) |
|---|---|---|
| Manual selling | Selling 1×, 10×, 100× (for example) is missing; today the only ways to earn are auto-sell and contracts. | A new application command `Sell(resourceId, quantity)` with whole-unit, atomic, overflow-checked semantics; the quantity steps are content or UI data. F004 designs the control. |
| Contracts are not a selling substitute | One could deliver contracts permanently and never sell. | Redesign rewards and cadence (balance-trace-r2 §6 items 2, 4, 5: reward ratios have no headroom; Ore-only dominance). |
| Monotonous progression | "I just hand in contracts all the time." | More varied goals between unlocks; review the contract loop and intermediate milestones. |
| Auto-deliver | A checkbox per contract so a deadline is not missed while the goods are in stock. | Player-selected, off by default; its interaction with priorities and auto-sell must be defined. |
| Build prices scale with progress | Prices should scale with IP; $69 for an Assembler is practically free. | Cost scaling by prestige progress or by owned count; re-check Toolmaker (cost ×0.5 cap) and the ECO-3 caps (balance-trace-r2 §6 item 3). |
| Distance to the goal | After 15 minutes 1 million Factory Value feels far away (the human was about 2.5× behind the trace at minute 15). | Visible intermediate goals; a corrected reference policy and balance revision 3 (content-approval item 7). |
| Balance test debt | — | Replace `var.price-80` with a non-rounding price modifier (content-approval item 1); re-tune the contracts-off and returns bounds (item 2). |
| Live time after OS sleep | — | From the T250 QA (#338): the desktop run now keeps simulating while unfocused, so a large single-frame time delta after a laptop sleep or hibernate could be simulated live, outside offline efficiency and the 8 h cap (a focused window had this exposure before). Clamp or reclassify implausibly large frame deltas as offline credit; decide with the Game Systems lens because it touches accounting. Also consider a minimum interval between desktop safety checkpoints. |
| Time acceleration for agent playtests | — | A development-build-only time scale so the Play-Tester role can play long sessions; never in release builds. |

### Idea: smart auto-sell (late unlock)

Recorded at the owner's request on 2026-10-09. An auto-sell option that sells every resource **not** needed by a follow-up process in the current factory. It is not available from the start: it is unlocked later in the game as a skill (for example a Research or Industry node). Open questions for the specification: how "needed" is decided (current recipes of built machines, or of all unlocked machines), whether stock reserved for an active contract counts as needed, and how it interacts with per-resource auto-sell and priorities.

## Feature 004: UI/UX and graphics (extends `feat/004-visual-identity`)

Feature 004 is currently specified as a visual restyle only, and its FR-001 forbids gameplay changes. The owner wants it to become the UI/UX and graphics overhaul. The entries below are added to its specification by a new clarify, plan and tasks pass. The existing tasks T193–T242 are extended, not discarded.

| Item | Owner note |
|---|---|
| Navigation | List the menus and mechanics in a dedicated menu, similar to Idle Obelisk Miner. |
| Stats view | A Stats tab with net profit per resource and clear production amounts; the Market's net rate moves there. |
| Research and IP trees | Split the Research tree and the Industry (IP) tree into separate views. |
| Priorities | Keep the function, but it takes too much space and may not belong in Market. |
| Offline summary | Works, but too much text and too little color. |
| Contract cue | Show whether a contract is deliverable, i.e. the materials are already in stock. |
| Selling control | The UI for Feature 005's manual selling (1×, 10×, 100×). |

## Process: Play-Tester role

The owner wants an agent role that plays the game along different strategies (for example Ore only, contracts only, the full chain, idle-heavy) and reports feedback to the parent. The owner reads the reports, may adjust them and keeps a veto. Tools: the deterministic balance runner with additional strategy policies, and sessions in a built player with screenshots (needs the time acceleration above for long sessions). The role is defined in `docs/agents/roles-and-reviews.md` before Feature 005 is specified.
