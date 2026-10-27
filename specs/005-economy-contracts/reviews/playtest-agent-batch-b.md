# Play-Tester batch-B report (T343, issue #408, WU52)

**Status: proposal for the owner.** Two channels: a deterministic balance-runner run and scripted player sessions in a Windows development build with the DevTools time scale. Neither is a human playtest. This report approves no content revision, sets no band and turns no feel statement into a threshold. Every finding is open for owner decision (AGENTS.md; `docs/agents/roles-and-reviews.md` Play-Tester). Feel statements are marked **[inference]**: an agent cannot feel boredom or delight.

| Item | Value | Source |
|---|---|---|
| Build commit | `f728a6945a0d8018172861ea9d91f86847afc617` (worktree HEAD = `origin/main` after PR #487) | `git rev-parse HEAD` |
| Batch under test | T325-T328, T330 (auto-deliver, bonus, production objectives, contract UI), T332/T333 (goal line), T334-T336 (Save V3, drift), T337 (live gap), T338/T364 (session log writer and raise sites), T339/T340 (Android rules, session-log panel, overlay move), T361 (DevTools overlay, Mine hold), T362 (shell focus, labels, Sell rows), T363 (content slice) | `git log 77e04b0..f728a69` |
| `contentRevision` | 2, still transitional (schema v3 with revision-2 values); T363 added a 60 s Plate cooldown, `contract.bonus-plate` (ratio 1.5, 15 min window) and `contract.produce-plate` (20 Plates, ratio 0.25, cooldown 300 s) | `Assets/IdleFactory/Content/Iron/iron-factory-content.json` lines 3, 359-440; session-log header `"contentRevision":2` |
| Channel A | balance runner, `StrategyFrameworkTests`, category `BalanceStrategies`; reference policy r1 (T341 policy r2 does not exist yet) | `strategy-baseline-r2.md` section 1 (regenerated, then restored) |
| Channel B | Windows win64 development player with `IDLEFACTORY_DEV` (`PlatformBuildCommands.BuildWindows64DevelopmentDevTools`), Mono, windowed 1280x720 and 390x844, real window, input by synthetic Win32 mouse events | `build.log`: `StandaloneWindows64 result Succeeded, 0 errors, 6 warnings` |
| Simulated / wall time | A: 21,600 s limit per run, 93 run-plays in 22.1 s wall. B: session 1 = run 1 plus run 2 on one development save, 22 min wall (23:28 to 23:50 local), of which run 1 = 14 min wall (23:28:55 to the prestige at 23:43:04) at x1/x10/x60/x600/Max; session 2 = clean save at x1, about 4 min wall; portrait check about 2 min | file times of screenshots and session-log exports |
| Strategy (B) | scripted "tinkerer": hand mining, Ore auto-sell, 2 to 3 Miners, Smelter and Press chain, factory expansion to level 5, Assembler, accepted every offer type, then an "upgrade-all" macro (click each slot, press Upgrade ten times, three rounds) at x600 until the sale threshold. Not a casual-pace player and not calibrated | section 4 |
| Evidence | 21 PNG (game client area only, about 1.9 MB) in `playtest-agent-batch-b/` next to this file | section 5 |

## 1. Channel A: strategy runs

- Command, run from this worktree: `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1` then `scripts/test-unity.ps1 -Suite EditMode -Category BalanceStrategies`. Output: `UNITY_TEST_PASS suite=EditMode tests=10 passed=10 skipped=0`. The wrapper shell did not capture an exit code (the exit-code file stayed empty); the pass line is the evidence. Conservation violations across 25 histories: 0 (section 5 of the regenerated file).
- The regenerated `strategy-baseline-r2.md` was compared with `HEAD` (that file was written at `4ae6a7a37`, before batch B) and with batch A, then restored with `git checkout`. `ProjectSettings` noise was restored too. Nothing from the run is committed except this report and its screenshots.
- **What the runner exercises of batch B.** It plays content revision 2 as it now stands, so the bonus offer, the production objective and the 60 s and 300 s cooldowns are in the content it runs (and the golden trace changed because of the live bonus, wiki `f005-batch-b-auto-deliver-and-production`). I did not diagnose which of those offers policy r1 ever accepts. **It never issues `Mine`, never calls `SetAutoDeliver`, reads no session log and has no live-gap or Save V3 path** (`ReferencePolicy.cs` and `BalanceTraceRunner.cs` contain only `SetAutoSell`, `AcceptContract`, `DeliverContract` among these verbs). So T326 (auto-deliver), T337 (live gap), T338/T364 (log) and the T358 mining are measured by channel A not at all; only engine drift and the content slice are.

### 1.1 Deltas (medians over 5 offsets; batch A = `playtest-agent-batch-a.md` at `72d196f`; HEAD file = committed baseline at `4ae6a7a37`)

| Strategy / metric | HEAD file | Batch A | Now (`f728a69`) | Delta vs batch A |
|---|---|---|---|---|
| chain run 1 eligibility | 6,548 s | 6,528 s | **6,508 s** | -20 s |
| chain runs 2 / 3 / 4 / 5 | 4,648 / 4,648 / 4,328 / 4,328 s | 4,668 / 4,668 / 4,408 / 4,408 s | 4,528 / 4,528 / 4,408 / 4,408 s | -140 / -140 / 0 / 0 s |
| chain first Gear (run 1 / 2 / 4) | 728 / 328 / 168 s | 688 / 328 / 168 s | 688 / 328 / 168 s | 0 |
| chain FV at 900 s, run 1 | 12,874 | 13,879 | 13,557 | -2.3 % |
| chain contracts delivered, run 1 | 189 | 195 | 193 | -2 |
| ore-only eligibility ratio to chain, runs 1 to 5 | 0.92 / 0.64 / 0.64 / 0.54 / 0.54 | 0.92 / 0.64 / 0.64 / 0.53 / 0.53 | 0.93 / 0.66 / 0.66 / 0.53 / 0.53 | about +0.01 to +0.02 in runs 1 to 3 (absolute ore-only times unchanged: 6,028 / 2,968 / 2,968 / 2,328 / 2,328 s) |
| contracts-only | not reached, FV 1,037 | same | same | unchanged |
| idle-heavy run 1 / 2 / 4 eligibility | 11,208 / 9,108 / 9,048 s | 9,608 / 9,068 / 8,188 s | 11,188 / 9,088 / 7,208 s | +1,580 / +20 / -980 s |
| casual run 1 eligibility (ratio) | 17,520 s (2.68x) | 17,228 s (2.64x) | 17,213 s (2.64x) | -15 s |
| casual FV ratio at 900 s (limit 0.40, provisional L = 73 s) | 0.365 | 0.339 | 0.347 | casual unchanged at 4,701; chain 13,557 |
| casual run 2 | not reached | not reached, FV@1,800 25,229 | not reached, FV@1,800 25,229 | still not reached |

Reading (measurement): chain run 1 is now **92 s below** the ECO-2 band floor (6,600 s); it was 72 s in batch A and 52 s in batch 1 (the F1 finding persists and drifts the wrong way by 20 s). Ore-only still beats the chain in every run index. Contracts-only still deadlocks with auto-sell off by construction. The idle-heavy run-1 move of +1,580 s (+16 %) back to its batch-1 value and the idle-heavy run-4 move of -980 s (-12 %) are the largest changes; cause not diagnosed (inference: the bonus trigger and the cooldowns change how often a look at an idle policy can use offers). Casual run 2 slowness is not resolved and the casual policy still has no mining click. Wall time of the suite: 22.1 s (batch A 27.1 s, batch 1 42.5 s; machine load, no conclusion).

## 2. Channel B: player sessions (Windows development build, `IDLEFACTORY_DEV`)

Method: input by user32 `SetCursorPos` and `mouse_event` (also mouse-down/up for a real hold), screenshots by `CopyFromScreen` of the player's client area with the window topmost. The development save folder `IdleFactory-Dev` (the build writes there, not to the real player save) was moved aside for the sessions and restored afterwards; the two session folders remain next to it as `IdleFactory-Dev.t343-session1` and `-session2` (parent may delete). The process was suspended with `NtSuspendProcess` to create a live gap. The player was stopped at the end.

### 2.1 Timeline of session 1 (wall time; game time is in brackets where the time scale was not x1)

| Wall (local) | Event | Evidence |
|---|---|---|
| 23:2x | First launch loaded a leftover development save (the T361 gate save): "Welcome back, away for 13 h 24 min, credited 8 h, production ran 6 h 24 min at 80 %". The overlay (x1 to Max) was on screen, top-left | `01-...png` |
| 23:28:55 | Fresh start: Cash 37, 4 empty slots, locked slots L2 to L5, overlay top-left **below the top strip**, Mine at the bottom right, Overdrive "Locked" | `02-...png` |
| 23:3x | One Miner built (Cash 7). Real-input hold on Mine for 8 s: Store 8 to 26 (+18 in 8.9 s). Pulse "+0.1 x84" | `03-...png` |
| 23:3x | x60 marker "ACCELERATED x60" visible; Cash 513 after about 3 s wall | `04-...png` |
| 23:3x | Factory expansion ($320) from the Market panel, Smelter and Press chain, Contracts unlocked: standing Plate (100 Plates, 1,400 Cash, 4 RP, Item), **bonus Plate** (100 Plates, 1,200 Cash, 12 RP, Item, "Bonus, ends in 1:58"), production objective (Produce 20 Plates, 40 Cash, 2 RP, Item), Gear locked, Timed bar (100 Bars, 4:00) | `05-...png` |
| 23:3x | At x60 the bonus window (15 min game time) ran out before I accepted: card stays with "Bonus ended" and a red warning | `06-...png` |
| 23:3x | Plate contract accepted with 998 Plates in stock; Auto-deliver turned on: delivered at once, +1,400 Cash, +4 RP; next Plate offer "Deliver 101, 1,414 Cash, **Available in 0:59**, That contract is cooling down" | `07-...png` |
| 23:3x | Production objective accepted: "Produced 6 of 20", then completed: +40 Cash, +2 RP, 2 Items; next offer "Produce 21, 42 Cash, **Available in 4:44**" | `08-...png` |
| 23:3x | Assembler built (factory level 3), Gear contract (100 Gear, 3,999 Cash, 8 RP) accepted and auto-delivered; next Gear offer 101 Gear, 4,039 Cash | session log |
| 23:3x | Session log export: "Exported 58 lines", path shown | `13-...png` |
| 23:3x to 23:43 | Factory level 5 (12 slots, $48K), storage level 3, Bargaining and Miner Tuning researched, 12 slots filled, upgrade macro at x600 (Max = x597 to x600) until Factory Value 7.12 M, then 10.6 M | `14-...png` |
| 23:43:04 | Factory sale (+10 IP, "Run 2 starts with 37 Cash"); run 2: Cash 37, Items (3) and IP kept | session log |
| 23:45 | Run 2 at x10: expansion, bonus offer **back** ("Bonus, ends in 13:45"), accepted, auto-delivered: +1,200 Cash, +12 RP, Items; card now "Bonus used" | `16-...`, `17-...png` |
| 23:48 | Process suspended 95 s: "Welcome back: The device clock shows a time earlier than when you left, so no offline production was credited ... Away for 0 s" | `18-...png` |
| 23:5x | Portrait 390x844 on the same save: bottom tab bar, Contracts as a bottom sheet with Close, active contract card with Deliver, Auto-deliver and Abandon fits | `20-...`, `21-...png` |

Session 2 (clean save, never accelerated): one Miner, 30 s at x1, then the process was suspended for 90 s: "Welcome back ... Away for 1 min 30 s, credited 1 min 30 s, production ran 1 min 12 s at 80 %, Produced 72 Iron Ore, Ore 26 to 98" (`19-...png`). After killing the player and relaunching 66 s later the same dialog showed "Away for 1 min 6 s ... Produced 52 Iron Ore, Completed cycles 26, Iron Ore 98 to 150".

### 2.2 Measurements (session-log export, `session-log-20261021T214737Z.jsonl`, 706 lines)

| Item | Value | Source |
|---|---|---|
| Factory Value in run 1 | 600 s: 1,191; 900 s: 1,791; 1,800 s: 3,867; 3,600 s: 11,067; 5,401 s: 24,298; 7,204 s: 34,004 | `sample` rows |
| Same points, chain policy r1 (channel A) | 600 s: 5,090; 900 s: 13,557; 1,800 s: 142,073; 3,600 s: 1,487,624; 5,400 s: 5,267,696 | `strategy-baseline-r2.md` section 3 |
| Mining | 84 counted clicks (hold) gave 8 Ore (0.1 per click) | `mining` row |
| Manual sales | 998 Plates for $7,984; 1,836 Gear for $44,064; 1,237 Gear for $29,688 (Gear sold for $24 then $25 after one Bargaining level) | `sale` rows |
| Prestige | Factory Value 10,663,434, threshold 10,000,000, +10 IP, `secondsSinceEligible` 0, `industryPurchases` empty | `prestige` row |
| Contract events | `accept`/`autoDeliver` for Plate (ratio 1,750,000, reward 1,400), `accept`/`produceComplete` for the objective (ratio 250,000, reward 40), `accept`/`autoDeliver` for Gear (ratio 1,666,666, reward 3,999) | `contract` rows |
| Clock events | `backward` deltaMs -127,647,730 (-35.5 h, `reclassified:false`) then `liveGap` deltaMs 95,006 (`reclassified:true`); clean session: `liveGap` deltaMs 90,020 plus `offlineReturn` absenceMs 90,079, creditedMs 90,079, `reclassifiedLiveGap:true`; after relaunch `offlineReturn` absenceMs 66,135 | `clock` and `offlineReturn` rows, session-2 export |
| Header rows | 308 of 706 lines are `header` rows with `timeScale` between 1 and 597 | `header` rows |
| Player log | no `ArgumentException`; one warning `Unknown pseudo class "last-child" in StyleSheet ResearchPanel`; `ResumeAndReconcile ms=4` and `ms=46` | `Player.log` of the last launch (earlier launches' logs are overwritten) |

## 3. Observations by batch-B feature

**DevTools overlay and acceleration (T361, T340).** The overlay renders (x1, x10, x60, x600, Max) top-left below the top strip; "ACCELERATED xN" appears in a red marker at every scale except x1 (`04-...png`). Max reads x597 to x600, so it is not faster than x600 (Max is frame-budget-limited: 10 h of game time takes about 60 s wall). **Mine is reachable at 1280x720 with the overlay visible** (hold test in `03-...png`; the bottom-right control is clear of the overlay). The overlay still covers the top edge of slot row 1 (status tags) at x1 and the whole slot-1-to-3 row (y 113 to 217) when accelerated (`04-...png`, `10-...png`); its buttons also pass the click to the slot beneath (F-B2).

**Real-input Mine hold (F6, FR-042).** An 8 s hold with one Miner running added about 8.4 Ore ("+0.1 x84", `mining` row 84 counted, 8 Ore) on top of the Miner's 9 (Store 8 to 26 in 8.9 s). That matches the parent's gate result (about +1 Ore/s). Closed at the player level.

**Auto-deliver (T326, T330).** Off after accept, with the hint "Applies to this contract only. Its stock is kept out of auto-sell." Turning it on with the stock already present delivered immediately. With the Gear contract (stock built up by an Assembler) it delivered without a click. The auto-sell reserve itself was not tested: Plate auto-sell was off while the Plate contract was active, and I did not check the Gear auto-sell state during the Gear contract. The "Contracts" tab shows a dot when something is deliverable. Not exercised: expiry of a timed contract (so the short-stock expiry line and `AutoDeliverWasOn` of T340 were not seen).

**Cooldown (T363).** After a Plate delivery the next Plate offer shows "Available in 0:59" with the red warning triangle "That contract is cooling down." and re-enables at 0:00 ("Available now"). The production objective's own cooldown is longer (4:44 after completion, 300 s in content).

**Bonus offer (T327, T363; the #466 follow-up).** It appears when the factory reaches level 2 with "Bonus, ends in m:ss", pays 1,200 Cash for 100 Plates (ratio 1.5), 12 RP and an Item, and has no cooldown of its own. After use it shows "Bonus used" and returned later in run 2 ("ends in 4:13" in `21-...png`), so it re-triggers; run 1's window passed unseen because I was at x60 (15 min of game time = 15 s wall), which is a limit of the accelerated channel, not a finding. **Judgement of appeal, with the #466 note in mind:** it pays $200 less Cash than the standing Plate offer for the same 100 Plates (-14 %), but 12 RP against 4 RP (3x), and it is a separate offer: only one contract can be active, so it competes for time, not for the standing offer's slot. In my session the RP bought a Bargaining level (+5 % sale price, Gear $24 to $25) and a Miner Tuning level, which is worth more than $200 early. **[inference]** A player who reads "1,200 Cash" next to "1,400 Cash" may read the bonus as the worse offer; the 12 RP is in the line but nothing says that RP is the point, and a "Bonus" label usually promises more. Proposal to the owner, not a decision: either name the RP gain in the card or revisit the ratio of 1.5 against the transitional 1.75 of the standing offer (T345).

**Production objective (T327, T363).** "Produce 20 Iron Plate" counts new production only (stock already in the store did not count) and completes automatically ("Completes automatically."); it pays 40 Cash (0.25x of the Plates' sell value) plus 2 RP and an Item. Plates stay in the store, so the objective is a free addition rather than a trade (visibility, as the follow-up says). It is easy to miss: three different cards are titled "Plate contract" (standing, bonus, objective) and differ only in the second line (F-B7).

**Goal line and Market (T333, T362).** "Next goal $10.0M: $63 of $10.0M (0 %)" with an "Exact value" button sits at the top of Market; later "$155K of $10.0M (1 %)". Locked resources show "Locked: unlocks after the factory reaches level 3." Sell rows carry Amount (1, 10, 100, All) and Sell; the result line "Sold 998 Iron Plate: +$7,984" stays. The Market also shows "The store is full. Machines that cannot add to it wait." (`12-...png`). "Cash", "Store" and "Overdrive" labels are present (F9 of batch A is fixed).

**Prestige and run 2.** The panel gives the live sale award (+8 IP at 7.12 M, +10 IP at 10.6 M), "Missing Factory Value", what resets and what stays, and a confirmation dialog; after the sale run 2 starts at Cash 37 with IP 10, Items kept, "Factory Sale #2" and "Missing Factory Value 9.99M". I found no panel where the 10 IP can be spent (Industry purchases were not visible in the Prestige tab's visible parts; not searched further).

**Offline return and live gap (T334 to T337).** Clean clock: a 90 s suspension and a 66 s kill-and-relaunch both produced correct "Welcome back" summaries (production at 80 % efficiency; Ore 26 to 98, then 98 to 150; session log `offlineReturn` and `clock liveGap` rows). The first launch loaded a leftover save of the previous session and credited the 8 h cap for a 13 h 24 min absence ("Offline credit is capped at 8 h; time beyond the cap earned nothing"), so an older development save loaded without error in this build. After acceleration the clock check fails: see F-B5.

**Session log (T338, T340, T364).** On by default; Export shows "Exported 58 lines" and a selectable "Last export" path; the file holds header, lifecycle, purchase, mining, sale, sample, bottleneck, contract, prestige, clock and offlineReturn rows. It carried the T364 raise sites (`contract` accept/autoDeliver/produceComplete, `prestige`, `bottleneck`, `clock`). No contract Abandon, Expire, Bonus or Blocked rows appeared because I did not trigger them.

**Portrait 390x844.** Bottom tab bar, Contracts as a bottom sheet with a Close button, active card with Deliver, Auto-deliver and Abandon on one row; no clipping found in Factory or Contracts. The DevTools overlay sits over the world below the top strip; the dev watermark covers the Prestige tab label (dev build only).

## 4. Findings

Batch-A carry-overs first, then new findings (F-B...). "Disposition" is my proposal; the owner decides.

| ID | Observed behaviour | Evidence | Affected FR/SC | Severity | Proposed disposition |
|---|---|---|---|---|---|
| A-F6 | Real-input Mine hold works: 84 counted clicks, 8 Ore, +1 Ore/s beyond the Miner | `03-...png`; `mining` row | FR-042, US10 | Closed (positive) | None |
| A-F7 | Overlay renders and marks acceleration; Max is about x600 | `04-...png` | FR-026, long-horizon channel | Closed (positive) | None |
| A-F8 | No `ArgumentException` seen on screen or in the last `Player.log` (earlier launches' logs are overwritten) | `Player.log` | SC-009 | Closed (positive, partial evidence) | None |
| A-F9 | "Cash", "Store", "Overdrive" labels present | `02-...png` | SC-009, #382, #417 | Closed (positive) | None |
| A-F10 | Research cards still clip on the right at 1280x720 ("Deep Vei", "Gear Mark"; "Scroll sideways to see more Research.") | Research tab in session 1 (not kept as PNG) | SC-009 | Low | Idea / named later task (Research UI polish) |
| A-F3 | Standing rewards still 1.75x (Plate), 1.70x (Timed bar), 1.67x (Gear) above the 1.5 cap of FR-004/SC-001; the new bonus is 1.5x and the objective 0.25x | contract rows; content lines 366, 395, 417, 439, 462 | FR-004, SC-001, D-D | High, likely transitional | Named later task T345 (content revision 3); owner confirms transitional status |
| A-F1 | Chain run 1 eligibility 6,508 s is 92 s below the ECO-2 floor (6,600 s); ore-only beats the chain in every run | section 1.1 | ECO-2, SC-002, FR-024 | High (ore-only), Medium (band) | Named later tasks T341, T345 (#410) |
| A-F2 | The runner has no Mine, no `SetAutoDeliver`, no log, no live-gap path | section 1 | SC-002, FR-040 to FR-044, FR-026 | Medium (coverage) | Named later task T341 (policy r2) |
| A-F4 | Cooldown, bonus and objective are now player-visible (60 s, 300 s, 15 min window) | `05`, `07`, `08`, `16` | FR-004 to FR-006, US2 | Closed (positive) | Re-run after T345 tunes the values |
| F-B1 | An ended or used bonus offer stays pinned at the top of the Offers list with a red warning triangle "That bonus contract is not available."; cooling-down offers use the same red triangle for a normal state | `06-...png`, `16-...png` (before accept); portrait `20-...png` | SC-009, US2 | Low to Medium (clutter and alarm styling; the first card of the list is dead for most of the session) | Named later task: UI/UX review T352 (sort ended offers last, calmer styling) |
| F-B2 | DevTools overlay buttons pass the click to the slot under them (clicking x600 selected slot 2); the overlay covers slot row 1 centres when accelerated and the sale/toast banner at the top | `10-...png`, `04-...png` | OPS-1, UX of the dev channel | Low (dev builds only) | Defect, DevTools: small current-feature fix (consume pointer events on the overlay) or idea |
| F-B3 | Stale or wrong guidance on a selected machine: a running Smelter at 1 % store showed "Storage is full: one cycle needs room for 2 more units, only 1 free." with "Auto-sell Iron Ore" and "Upgrade storage" buttons for at least 2 s; a running Miner showed "Missing Iron Plate: needs 2, has 1. Build a Press to make Iron Plate." | `09-...png`, `11-...png` | SC-009, FR on blocked reasons (US1/US3) | Medium (misleading text next to action buttons) | Defect: current feature (T362 follow-up or T352 review) to assign; I did not isolate whether it is a stale panel from the previously selected slot |
| F-B4 | In accelerated runs the session log gets a new `header` row every time the effective time scale changes in Max (593 to 599): 308 of 706 lines. The wiki and T338 describe the header `TimeScale` as a snapshot at session start | `header` rows of the export | SC-005 (analysis of accelerated logs), SEC-1 irrelevant | Low (dev builds only) but it pollutes the only long-horizon logs and may push real events out of the ring | Defect, DevTools/log: current feature or named later task; or rate-limit headers to real changes (owner/Architect) |
| F-B5 | After an accelerated session the game timeline runs ahead of the device clock (-35.5 h here): the log records `clock backward -127,647,730 ms`, and every later start shows "The device clock shows a time earlier than when you left, so no offline production was credited ... Away for 0 s", even in the portrait relaunch. A clean save does not show it. So offline return cannot be tested after acceleration in the same run | `18-...png`; `clock` rows | T335/T337 clock rules, US7 | Low (dev builds only; the product rule itself behaves as written) | Idea / doc note for the DevTools recipe: test offline return on a never-accelerated save, or clamp the timeline offset at quit |
| F-B6 | A store-full stall is easy to cause and costly: unsold Gear filled a 2,000 store (1,835 Gear = $44,064) and stopped every Miner and Smelter; the Market banner and the machine tags explain it, but only the machine panel offers a fix and it suggests Ore, not Gear | `12-...png`; sale rows | US1, SC-009 | Info (explained well; no fix link at resource level) | Idea: a "sell this" or "auto-sell this" hint on the store-full banner |
| F-B7 | Offers are hard to tell apart: standing Plate, bonus Plate and the Plate production objective all use the title "Plate contract"; Item rewards disappear from the reward line after several deliveries and return after the sale ("Reward: 1,484 Cash, 4 RP"), with no explanation (per-run item cap, D-M open `perRunCap`) | `05-...png`, `16-...png`, session 1 contracts list | US2, SC-009, #387 | Low | Named later task: UI/UX review T352; `perRunCap` stays the owner's open decision |
| F-B8 | Welcome-back wording: the 13 h 24 min dialog opens with "No production while away." and then says "Production ran for 6 h 24 min at 80 % efficiency"; all machines were blocked by a full store, so both are true but read as a contradiction | `01-...png` | US7, FR-016 | Low | Idea / T352 |
| F-B9 | `Unknown pseudo class "last-child" in StyleSheet ResearchPanel` is logged at start | `Player.log` | clean-console gate | Low | Defect: current feature (USS fix) or accept as noise; owner decides |
| F-B10 | A scripted tinkerer is far below the chain reference: Factory Value at 900 s 1,791 against 13,557, at 3,600 s 11,067 against 1,487,624; it reached the threshold only after an upgrade macro at x600. One scripted run, not a pace calibration | section 2.2 | SC-005 (human playtest still needed) | Info | No action; a human run is still needed before T345 |

Feel statements, all **[inference]**: the first three minutes (hand mining, first Miner, first sale) are clear and quick; the jump from about $120 to $2,867 at the first Plate delivery is large against the $1 Ore trickle; a store that fills with an unsold product is a hard stop for a new player; the bonus offer is easy to read as the worse of two offers; the production objective is pleasant (a free reward) but anonymous. None of these is a measurement.

## 5. Evidence index (`playtest-agent-batch-b/`)

| File | Shows |
|---|---|
| `01-old-dev-save-offline-return-13h24.png` | leftover save, 13 h 24 min return, 8 h cap (F-B8) |
| `02-fresh-start-1280x720-overlay-top-left.png` | overlay placement, labels |
| `03-mine-real-hold-8s-plus-0.1x84.png` | real hold, Mine reachable (A-F6) |
| `04-accelerated-x60-marker-market.png` | acceleration marker, overlay over slot row |
| `05-contracts-offers-standing-bonus-objective.png` | standing, objective, Gear locked, Timed bar |
| `06-ended-bonus-offer-pinned-with-error.png` | F-B1 |
| `07-auto-deliver-reward-and-cooldown.png` | auto-delivery, cooldown line |
| `08-production-objective-completed.png` | objective done, next in 4:44 |
| `09-stale-storage-full-warning-on-running-smelter.png` | F-B3 |
| `10-overlay-click-through-selects-slot.png` | F-B2 |
| `11-stale-missing-plate-text-on-miner.png` | F-B3 |
| `12-store-full-market-banner.png` | F-B6 |
| `13-session-log-export.png` | export UI |
| `14-prestige-panel-7m-fv.png` | prestige preview |
| `15-items-tab.png` | Items |
| `16-run2-bonus-offer-ends-in-13-45.png` | bonus in run 2 |
| `17-bonus-delivered-12-rp.png` | bonus reward |
| `18-clock-earlier-dialog-after-acceleration.png` | F-B5 |
| `19-live-gap-clean-clock-90s.png` | live gap on a clean clock |
| `20-portrait-contracts-sheet.png`, `21-portrait-active-contract.png` | portrait, later bonus "ends in 4:13" |

## 6. What I could not observe

- Timed contract expiry (Timed bar 4:00): the short-stock expiry line and `AutoDeliverWasOn`; contract Abandon, Expire, Blocked and Bonus rows in the log; the Items activation effects (none activated); the production Item cap boundary beyond what the reward lines showed.
- Where IP is spent (Industry); run 3 and later; prestige index above 2 for mining (SC-011).
- Save V3 content drift and the V2 migration beyond one old save loading (no content-revision change was possible without editing content).
- Android (not in scope; no `adb` call; the emulator lease was not used).
- Casual-pace play and a human first-time experience: the scripted player is not casual and the sessions ran partly at x10 to x600.
- Accessibility (contrast, text scale, screen reader) and empty/error states beyond those visible; SC-009 stays with the UI/UX reviewer.
- Which of the new offers policy r1 accepts in channel A.

## 7. Open owner questions

1. F-B1/F-B7: sort ended and cooling-down offers below live ones, vary titles, explain the item cap (T352)?
2. F-B3: who owns the stale or wrong machine-panel guidance text (T362 follow-up or a new fix)?
3. Bonus: keep ratio 1.5 with 12 RP, or name the RP gain in the card, or change with T345? (the "#466 appeal" question)
4. F-B4/F-B5/F-B2: fix the DevTools log header flood, the clock offset after acceleration and the overlay click-through now, or record them in the DevTools recipe only?
5. A-F1/A-F3: the ECO-2 floor miss (92 s) and the 1.67x to 1.75x standing ratios stay transitional until T345?
6. Where should the 10 IP of the first sale be spent, and is that panel part of this feature?
