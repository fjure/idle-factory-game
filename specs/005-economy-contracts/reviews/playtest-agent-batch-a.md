# Play-Tester batch-A report (T342, issue #407, WU47)

**Status: proposal for the owner.** Two channels: a deterministic balance-runner run and one short scripted player session in a Windows development build. Neither is a human playtest. This report approves no content revision, sets no band and turns no feel statement into a threshold. Every finding is open for owner decision (AGENTS.md; `docs/agents/roles-and-reviews.md` Play-Tester). Feel statements are marked **[inference]**: an agent cannot feel boredom or delight.

| Item | Value | Source |
|---|---|---|
| Build commit | `72d196f72f7eff3ad6c85abb1f10a7e963213d1d` (worktree HEAD = `origin/main` after PR #458) | `git rev-parse HEAD` |
| Batch under test | US1 (T319 sell UI), US10 (T359 ore vein UI, T358 mining), US2 (T322 production objectives), US4 (T324 build price UI), plus T321 cooldown/bonus state and T320 rewards | `git log 53ceb54..HEAD` |
| `contentRevision` | 2, transitional (schema v3, revision 2 values; `iron-factory-content.json` has no value change since batch 1, only the schema file) | `Assets/IdleFactory/Content/Iron/iron-factory-content.json` line `"contentRevision": 2` |
| Channel A | balance runner: `StrategyFrameworkTests`, category `BalanceStrategies`, strategies chain, ore-only, contracts-only, idle-heavy, casual; reference policy r1 (T341 policy r2 does not exist yet) | `strategy-baseline-r2.md` section 1 |
| Channel B | Windows win64 development player with `IDLEFACTORY_DEV` (`PlatformBuildCommands.BuildWindows64DevelopmentDevTools`), Mono, windowed 1280x720 then 390x844; real window screenshots; input by synthetic Win32 mouse events | `build.log`: `StandaloneWindows64 result Succeeded, 0 errors, 5 warnings` |
| Simulated / wall time | A: 21,600 s limit per run, 93 run-plays (about 27 s wall). B: 12.7 min wall at x1 in session 1 (process start 16:03:45, stopped 16:16:28), about 1 min in session 2 (portrait, loaded save, offline return) | process start time; generator |
| Strategy (B) | a scripted "chain, tinkerer" player: mine and sell by hand, 2 Miners, 2 Smelters, 2 Presses, one Plate contract, expand factory once. Not a casual-pace player; not calibrated | section 4 |

## 1. Channel A: strategy runs

- Command, run from this worktree: `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1 powershell -NoProfile -Command "& ./scripts/test-unity.ps1 -Suite EditMode -Category BalanceStrategies"`. Exit code 0, `UNITY_TEST_PASS suite=EditMode tests=10 passed=10 skipped=0`. Conservation violations across 25 histories: 0 (`strategy-baseline-r2.md` section 5 of the regenerated file).
- The regenerated `strategy-baseline-r2.md` was compared with `HEAD` and then restored with `git checkout`. `ProjectSettings` noise was restored too. Nothing from the run is committed except this report.
- **The runner does not exercise the batch-A verbs.** Policy r1 issues no `Mine`, no manual `Sell`/`SellAll`, and content revision 2 contains no production-objective offer and sets `cooldownMs` to 0 on all three contracts, no bonus offer. So batch-A features T358, T319, T321 and T322 are not measured in channel A at all. Channel A shows only the engine drift since batch 1 (the other merges in `53ceb54..HEAD`: T316 sale pricing, T323 build pricing, T331 prestige threshold, T320 rewards). Attribution to batch A is not possible; the deltas below are labelled "since batch 1".

### 1.1 Deltas since batch 1 (medians over 5 offsets; batch 1 = `playtest-agent-batch-1.md`, this run = regenerated baseline at `72d196f`)

| Strategy / metric | Batch 1 | Now | Delta |
|---|---|---|---|
| chain run 1 eligibility | 6,548 s (109.1 min) | 6,528 s (108.8 min) | -20 s |
| chain runs 2 / 3 / 4 / 5 | 4,648 / 4,648 / 4,328 / 4,328 s | 4,668 / 4,668 / 4,408 / 4,408 s | +20 / +20 / +80 / +80 s |
| chain first Gear (run 1 / 2 / 4) | 728 / 328 / 168 s | 688 / 328 / 168 s | -40 / 0 / 0 s |
| chain FV at 900 s, run 1 | 12,874 | 13,879 | +7.8 % |
| chain contracts completed, run 1 | 189 | 195 | +6 |
| ore-only eligibility, runs 1 to 5 vs chain | 0.92 / 0.64 / 0.64 / 0.54 / 0.54 | 0.92 / 0.64 / 0.64 / 0.53 / 0.53 | unchanged (ore-only absolute times unchanged: 6,028 / 2,968 / 2,968 / 2,328 / 2,328 s) |
| contracts-only | not reached, FV flat 1,037 | not reached, FV flat 1,037 | unchanged |
| idle-heavy run 1 eligibility (ratio to chain) | 11,208 s (1.71x) | 9,608 s (1.47x) | -1,600 s |
| idle-heavy runs 2 / 4 | 9,108 / 9,048 s | 9,068 / 8,188 s | -40 / -860 s |
| casual run 1 eligibility (ratio) | 17,520 s (2.68x) | 17,228 s (2.64x) | -292 s |
| casual FV ratio at 900 s (limit 0.40, provisional L = 73 s) | 0.365 (4,701 / 12,874) | 0.339 (4,701 / 13,879) | casual unchanged, chain rose |
| casual run 2 | not reached; FV at 1,800 s 15,990 | not reached; FV at 1,800 s 25,229 | still not reached, FV higher |

Reading (measurement): chain run 1 stays 72 s below the ECO-2 band floor (6,600 s), the F1 finding of batch 1 persists. Ore-only still beats the chain in every run index. Contracts-only still deadlocks with auto-sell off by construction. Idle-heavy moved the most (up to -860 s, 14 % in run 1), which suggests it benefits most from whatever changed (inference about cause: not diagnosed here, since several engine changes merged). Casual run 2 slowness is not resolved and the casual policy still has no mining click, so the 2.5x owner calibration is not re-tested by this run. The wall time of the whole suite fell from 42.5 s to 27.1 s (machine load, no conclusion).

## 2. Channel B: player session (Windows development build)

Evidence screenshots are in `playtest-agent-batch-a/` next to this file (15 PNG, about 1.5 MB). Input was synthetic mouse events through user32 (`SetCursorPos`, `mouse_event`); screenshots were `CopyFromScreen` of the player's client area with the window set topmost. Coordinates were read from earlier screenshots. A first capture showed a foreign window (the player was not on top) and was deleted and retaken; no other window content is in the evidence.

### 2.1 Timeline of the scripted run (wall time at x1, rough, from screenshots)

| t (min) | Event | Evidence |
|---|---|---|
| 0:00 | Fresh run: Cash 37, 4 empty slots, 8 locked slots (L2 to L5), Mine button, 6 tabs | `01-factory-start-1280x720.png` |
| 0:30 | First Miner built (Cash 37 to 7; cost 30). Starting Cash already covers the first Miner | screenshots s03 to s05 (not kept), text in section 2.2 |
| about 2 | Ore stock 56 to 99; sold with "Sell all": +$99, Cash 107 | `03-sell-all-result.png` |
| about 5 | Second Miner, Auto-sell on: Cash 77 to 316 in about 2 min | s23 to s27 (not kept) |
| about 6 | Factory expansion $320 bought (slots 4 to 6, factory level 2). Smelter ($9) and Press ($21) become buildable; overdrive button appears ("Ready") | `04-build-list-smelter.png` |
| about 7 | Smelter and Press idle: "IN!" tag, with the explanation "auto-sell is selling it before this machine can use it", and a fix button | `05-smelter-auto-sell-starves-machine.png` |
| about 8 | Contracts unlocked after the first Iron Bar. Plate contract accepted (100 Plates, 1,400 Cash, 4 RP, Item) | `06-contracts-offers.png` |
| about 8 to 11.5 | 2 Smelters + 2 Presses fill the contract: stock 9 to 62 to 101 of 100 | `07-active-contract-ready.png` |
| about 11.5 | Delivered: +1,400 Cash, +4 RP, Diamond Drill Head. Next Plate offer is immediately available: 101 Plates, 1,414 Cash, different Items | `08-delivered-next-offer.png` |
| about 12 | Overdrive used: "On 0:29" then "Cool 5:00" | `09-overdrive-on.png` |
| 12.7 | Prestige panel: Cash 1,488, stock value 613, machines 120, expansion 320, **Total Factory Value 2,541**, needed 10.0M, sale award +0 IP, "Run 2 starts with 37 Cash" | `11-prestige-panel.png` |

Measured, session 1: Factory Value 2,541 at 12.7 min (762 s). For orientation only, channel A at 600 s / 900 s: chain 5,090 / 13,879, casual (runner) 2,769 / 4,701. A linear read of those at 762 s gives about 9,000 (chain) and about 3,700 (casual runner). This scripted player was therefore below the runner's casual policy, with three dead stretches (waiting for Cash, the auto-sell starvation, filling the first contract) and no optimisation. It is one run, not a pace calibration. **[inference]** A real first-time player may be at least as slow, which would be consistent with the 2.5x lag of the 2026-10-09 owner playtest, but this session cannot establish that.

### 2.2 Observations by batch-A feature

**Manual mining (T358, T359, FR-040 to FR-044).**
- The Mine control is visible from the first frame at bottom right with a progress bar. A click adds a small visible bar step; the spec value is 0.1 Ore per counted click (D-P). 50 synthetic clicks (about 5 s) were followed by about +13 Ore in about 6.5 s with one Miner running (1 Ore/s), so mining added roughly +5 on top of the Miner, which matches 50 x 0.1. 150 clicks in 21.5 s were sent later; the Ore stock was not read before and after, so that run adds no measurement.
- A pressed-and-held Mine button for 8.0 s produced +10 Ore in 9.6 s with one Miner running (1 Ore/s): no yield beyond the Miner was visible (FR-042 expects about 1 Ore/s extra). I could not tell whether the hold failed in the game or my synthetic press does not produce the pointer-hold events. **Not verified; see finding F6.**
- The starting Cash (37) already buys the first Miner (30) at once, so manual mining was not needed to start; the spec says it is for later prestige indices, which this session did not reach (SC-011 is not testable here).
- The Mine bar is the only mining feedback I found: there was no "+0.1" or full-store reason shown during the run (the store was never full, so FR-040's full-store reason is not observed). **[inference]** At about 1 Ore/s of capped mining next to a 60/min Miner, mining is a small additive boost that I would not expect to feel decisive after the first Miner.

**Sell UI (T319, FR-001 to FR-003).**
- Market tab shows per-resource Stock, Net rate, Sells for, an Amount cycler (1, 10, 100, All, as FR-001) and Sell. The label updates ("Sell 1", "Sell 100", "Sell all"). A disabled Sell shows the reason ("Need 100", "Nothing to sell"). A result line stays below ("Sold 99 Iron Ore: +$99") and a transient banner shows at the top.
- Selling by hand at $1 per Ore and Amount 1 is slow; All makes it one click. **[inference]** The default Amount of 1 is the least useful setting at these prices and resets to 1 after each cycle completes.
- Prices observed: Iron Ore $1, Iron Bar $3, Iron Plate $8 per unit. A Miner pays back its $30 in about 30 s of selling Ore.
- Auto-sell vs machine inputs is explained well: a machine that cannot run shows "Missing Iron Ore: auto-sell is selling it before this machine can use it (needs 2, has 0)." with a "Stop auto-selling Iron Ore" button (`05-...png`). The first automatic Ore sale did starve my Smelter until I read that message.
- The top-left bar next to the Cash number is the store fill (56 / 1,000 gave 5 to 6 %), not progress toward a goal. It has no label. **[inference]** A new player may read it as goal progress.

**Build price UI (T324).** Each build card shows cost, Owned count, rates, Uses, and the unlock reason ("Unlocks at factory level 2/3"). Costs observed: Miner $30 (second Miner also $30), Press $21, Smelter $9, Assembler $78. The cheapest buildings are unlocked first, but the second and later copies did not rise in price (Miner stayed $30 after buying two; Smelter $9 after two), which I could not reconcile with the `cost.floor` / build-pricing notes of T323 in this session (owned counts 0 to 2 only). Flagged as question Q3 for the Game Systems lens, not as a defect.

**Contracts, cooldown, bonus, production objectives (T321, T322, T320).**
- Offers: Gear (locked until Gear is produced), Plate (100 Plates, "No time limit"), Timed bar (100 Bars, 4:00). Only one contract can be active ("Finish or abandon the active contract first"). The active card shows a progress bar, "In stock: 9 of 100", "Missing 91 Iron Plate." and a Deliver button that reads "Deliver (missing 91)" while disabled. These are clear. [`06`, `07`]
- **No cooldown was observable.** After delivery the same Plate contract could be accepted again at once and its next copy scaled (101 Plates, 1,414 Cash). This is content, not a UI gap: all three contracts have `cooldownMs: 0` in revision 2 (see F4).
- **No production-objective offer and no bonus offer exist in content revision 2**, so T322 and the bonus part of T321 are not reachable from the player UI. The player sees none of FR-005/FR-006 yet.
- Reward premium over selling (measured from the UI): Plate 1,400 Cash vs 100 x $8 = 800 sell value = **1.75x**; Timed bar 510 vs 100 x $3 = 300 = **1.70x**. Content ratios are `rewardRatioPpm` 1,750,000 (Plate), 1,700,000 (Timed bar), 1,666,666 (Gear), all with class "standing". FR-004 and SC-001 require standing offers to be 1.0x to 1.5x.
- Delivery rewarded Cash + 4 RP + an Item; the notice said "Delivered 100: +1,400 Cash, +4 RP, Diamond Drill Head x1." [`08`]

**Other panels seen (not batch A, for orientation).** Overdrive (30 s, 5 min cooldown) works and shows "On 0:29" / "Cool 5:00" with a lightning marker on every machine (`09`). Research tab cards are clipped on the right at 1280x720 with a "Scroll sideways to see more Research." hint (`10`). Prestige panel is clear (what resets, what is kept, "Needed to sell: 10.0M", sale award, "Run 2 starts with 37 Cash") (`11`).

**Offline return (session 2).** After the player was killed and restarted about 52 s later, a "Welcome back" dialog listed Away for 52 s, Credited 52 s, "Production ran for 42 s at 80 % efficiency", Cash +0, Produced 43 Iron Bar / 84 Iron Ore / 21 Iron Plate, Used 42 Iron Bar / 86 Iron Ore, per-item stock changes. [`12`] The save persisted Cash 1,488 and all machines. It never ran for long enough to test an offline return of hours.

**Portrait 390x844.** Bottom tab bar and a bottom sheet for Market and Contracts; Close button on the sheet; machine grid scales to 3 columns; no clipping found in Factory, Market and Contracts. The dev-build watermark ("Development Build") draws over the Prestige tab label at the bottom right (dev builds only). [`13`, `14`, `15`]

**Console / player log.** 10 `ArgumentException: Cannot scroll to a VisualElement that's not a child of the ScrollView content-container.` in session 1, thrown from `IdleFactory.Unity.UI.RunShellDocument.OnFocusIn` (stack in `player.log`), none in session 2. The in-game Development Console showed them and stayed open over the Factory grid and the bottom of the Market panel (`02`). The game kept working.

**Time acceleration.** The `IDLEFACTORY_DEV` overlay (`DevToolsOverlay`, x1/x10/x60/x600/Max) was not visible in the Windows development player. The log contains `No Theme Style Sheet set to PanelSettings DevToolsPanelSettings, UI will not render properly` at `DevToolsOverlay.cs:67`. Keys 1 to 5 had no visible effect (the row only handles keys while it has focus; no marker appeared). Long player sessions are therefore not possible yet; see F7.

## 3. Findings

| ID | Observed behaviour | Evidence | Affected FR/SC | Severity | Proposed disposition |
|---|---|---|---|---|---|
| F1 | Chain run 1 eligibility 6,528 s is 72 s below the ECO-2 band floor (6,600 s); the situation of batch-1 F1 persists. Ore-only is still faster than the chain in every run index (0.92x to 0.53x) | section 1.1; `strategy-baseline-r2.md` section 3 and 4 | ECO-2, SC-002, FR-024 single-lever gate | High (ore-only), Medium (band) | Named later tasks T341 (gates on policy r2) and T345 (tuning, #410). The owner decides whether the band stays. Not measured against batch-A verbs (no mining in policy r1) |
| F2 | The balance runner exercises none of the batch-A verbs: no Mine, no manual Sell, no production objective, no bonus offer, cooldown 0 | section 1; content `cooldownMs: 0`; no production-objective offer in the JSON | SC-002, FR-006, FR-040 to FR-044, SC-010 | Medium (coverage gap) | Named later task T341 (policy r2, chain+mining and mining-only strategies). Content with objectives and cooldowns is a T345 / content task (owner decides which) |
| F3 | Standing contract rewards are 1.75x (Plate), 1.70x (Timed bar) and 1.67x (Gear) of the sell value, above the 1.5x cap, observed in the player UI (1,400 Cash for 100 Plates worth $800) | `06-contracts-offers.png`; content lines 366, 388, 411 | FR-004, SC-001, D-D | High (against a Must-hold rule), likely transitional | Named later task T345 (content revision 3). The owner confirms the rev-2 content stays transitional until then, or whether a validator should already refuse it |
| F4 | Contract cooldown and bonus (T321) and production objectives (T322) cannot be seen by a player: the delivered Plate contract is accepted again at once | `08-delivered-next-offer.png`; `cooldownMs: 0` | FR-004, FR-005, FR-006, US2 | Medium | Named later task: content revision 3 (T345). Re-run the Play-Tester after content sets a cooldown, an objective and a bonus. Not a defect of the engine here |
| F5 | The offline return dialog and the portrait layout work; the first Miner is affordable from starting Cash; the auto-sell vs machine-input explanation and fix button are clear | `12`, `13`, `14`, `05` | FR-016, SC-009 (partial) | Info (positive) | No action |
| F6 | A held Mine button for 8 s gave no yield beyond the Miner (+10 Ore in 9.6 s at 1 Ore/s); a plain click series did add about 0.1 Ore per click. It might be my synthetic hold, not the game | section 2.2, mining paragraph | FR-042, US10 scenario 3 | Medium until confirmed | Needs a check with real input (human or Play Mode test): current feature (T359/T358 follow-up) or drop if it was the harness. I did not run a real hold |
| F7 | The Windows dev time-scale overlay does not render (no theme stylesheet on its `PanelSettings`) and no key operates it, so a long accelerated player session is impossible | `player.log` warning at `DevToolsOverlay.cs:67`; no marker or buttons in any screenshot | FR-026, Play-Tester long-horizon channel | High for playtesting (blocks long player sessions), none for players (dev builds only) | Defect: current feature (the time-acceleration task, before T343 batch-B). Needs a fix by the Gameplay Developer; Play-Tester only reports |
| F8 | `RunShellDocument.OnFocusIn` throws `ArgumentException` ("Cannot scroll to a VisualElement that's not a child of the ScrollView content-container") 10 times in 12.7 min, shown in the in-game Development Console;; first seen after a tab switch and scrollbar clicks, so it is tied to focus changes | `02-market-sell-and-devconsole-exception.png`; `player.log` (10 matches) | SC-009 (operable controls), clean-console completion gate | Medium (no visible gameplay effect found) | Defect: current feature (T319/T359 shell code); owner or Software Architect lens to assign. Trigger might involve my synthetic clicks focusing elements outside the scroll content (not verified) |
| F9 | Unlabelled or ambiguous UI: the bar next to Cash is store fill (unlabelled); the overdrive control shows only a lightning icon plus "Ready" / "On 0:29" / "Cool 5:00" with no "Overdrive" word; the currency icon carries no "Cash" word in the top bar | `01`, `09`, `11` | SC-009; owner question #382 (UI), #417 (currency label) | Low | Named later tasks / open owner questions #382 and #417. The owner decides |
| F10 | Research panel cards are clipped at the right edge at 1280x720 (hint says "Scroll sideways"); the dev-build watermark covers the Prestige tab label in portrait | `10-research-panel-clipped.png`, `13`, `14` | SC-009 | Low (Research clipping); Info (watermark is dev build only) | Idea / named later task (Research UI polish); no action for the watermark |
| F11 | Second and later copies of Miner, Smelter and Press did not rise in price (Miner $30 twice, Smelter $9 twice) | section 2.2, build price paragraph | T323 build pricing (FR for build price; the spec text was not re-read for the exact FR id) | Low until checked | Question Q3 below: the Game Systems lens confirms whether a flat price for early copies is intended |
| F12 | A scripted tinkerer run reached Factory Value 2,541 at 12.7 min, with three dead stretches (cash wait, auto-sell starving a machine, contract fill) and about +1,400 Cash only at minute 11.5 | section 2.1 | SC-005 (human playtest still needed), ECO-2 pacing | Info | No action: one scripted run, not calibrated. Owner may want a human run before T345 |

Feel statements, all **[inference]**: the first minutes are clickable and clear but slow once Ore selling stops being a click; the jump from about $120 to $1,488 at the first contract delivery would likely feel large compared with the earlier $1 per Ore trickle; waiting about 3 minutes for 100 Plates with nothing to do except watch the bar would likely feel like dead time. None of these is a measurement.

## 4. Strategy used in channel B

Scripted "chain, tinkerer" (no policy file; the Play-Tester wrote no code): fresh start, one Miner, hand-sell Ore, second Miner with Auto-sell, expand the factory once, Smelter then Press (twice), accept the Plate contract, deliver, one Overdrive, then inspect the Research and Prestige panels. No Items were activated, no Research was bought, no storage upgrade was bought, the Gear contract was never reached (no Assembler: it needs factory level 3). Prestige was not reached (needs 10.0M Factory Value).

## 5. What I could not observe

- Anything past minute 13: first prestige, run 2 and later, the Industry panel, Gear and the Assembler, the bonus offer, production objectives, cooldown, and an offline return of more than about 1 minute. Reason: no working time acceleration (F7) and no content for the later features (F4).
- A trustworthy hold-to-mine reading (F6) and the full-store reason next to the vein (store never filled).
- Mining at a prestige index above 1 (SC-011).
- Android emulator behaviour (not in scope; the emulator lease was not used for `adb`).
- A casual-pace player calibration: the scripted player is not casual.
- The sale price `floor` rule and sale-price modifiers (no Research bought).
- Contrast, text scale, touch target size and screen-reader behaviour (not assessable from screenshots; SC-009 stays with the UI/UX reviewer).

## 6. Open owner questions

1. F3: Is revision 2 content (ratios 1.67x to 1.75x) accepted as transitional until T345, or should validation refuse it before then?
2. F1: Keep the ECO-2 run-1 band (6,600 s) or amend it (still 72 s below)? Is ore-only faster than chain a gate failure for this content (T341/T345, #410 `chain.dominance` stays the owner's call)?
3. F11: Is a flat price for the first copies of Miner, Smelter and Press intended?
4. F7: Which task owns the dev time-scale overlay fix, and does it block T343 batch B?
5. F9: Label the store bar, the overdrive control and the currency icon (#382 UI questions, #417 currency label)?
6. F2/F4: Should a content task add one production-objective offer, one cooldown and one bonus offer before the Play-Tester batch-B report (T343)?
7. #387 `perRunCap` was not observable (no mining cap reading beyond clicks); the owner decision stays open and is not touched here.
