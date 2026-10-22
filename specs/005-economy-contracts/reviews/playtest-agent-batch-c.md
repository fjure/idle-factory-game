# Play-Tester batch-C report (T346, issue #411, WU57)

**Status: proposal for the owner.** Two channels: a deterministic balance-runner run (policy r2) and one scripted player session in a Windows development build with the DevTools time scale. Neither is a human playtest (T353 stays open). This report approves no content revision, sets no band and turns no feel statement into a threshold. Every finding is open for owner decision (AGENTS.md; `docs/agents/roles-and-reviews.md` Play-Tester). Feel statements are marked **[inference]**: an agent cannot feel boredom or delight. Measured values name their source.

| Item | Value | Source |
|---|---|---|
| Build commit | `d4c20f697aa4207446cc3891debfbc356f6931bc` (worktree HEAD = `origin/main` after PR #496, T345) | `git rev-parse HEAD` |
| Batch under test | T345 (content revision 3), T341 (policy r2), T365 (machine-panel guidance), T366 (DevTools overlay consumes pointer input; log header only on step change); T344 is a test-only audit and has nothing player-visible | `git log`, `tasks.md` WU53, WU54, WU55 |
| `contentRevision` | 3 (session-log header `"contentRevision":3`) | `session-log-20261022T123300Z.jsonl` row 0 |
| Channel A | balance runner, `scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull` with `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1`, policy r2, 13 strategies x 5 offsets x up to 5 runs | section 1 |
| Channel B | win64 development player with `IDLEFACTORY_DEV` (`PlatformBuildCommands.BuildWindows64DevelopmentDevTools`), windowed 1280x720 and 390x844, input by synthetic Win32 mouse events, development save folder `IdleFactory-Dev` (the real player save was never used) | `build.log`: `StandaloneWindows64 result Succeeded, 0 errors, 7 warnings` |
| Simulated / wall time | A: 21,600 s limit per run, 265 run-plays, matrix wall 125.1 s (test duration 131.4 s). B: landscape session 14:09:35 to 14:32:44 local (23 min wall): run 1 = 14 min 45 s wall (x1 for the first 4 min 41 s, then x10, x60, x600), run 2 = 8 min 24 s wall (x10, x60, x600); portrait about 4 min; Industry check about 3 min | file times, my wall clock; the session log has no per-event time (finding C-F7) |
| Game time of run 1 and 2 | x1 for 281 s, then about 2,840 s at x10, 9,600 s at x60 and 96,000 s at x600 for run 1 (about 108,000 s, 30 h); about 113,000 s (31 h) for run 2. **Estimate** from wall time x scale: the game kept running while I read screenshots, so each segment is off by up to 20 %. Only the log samples at 600, 900, 1,800, 3,600, 5,400 and 7,200 s are exact | section 2.2 |
| Strategy (B) | scripted "tinkerer": hand mining and four Miners at x1, then Smelter, Press, Assembler, accepted bonus, standing and Gear offers, Ore and Gear auto-sell, then an "upgrade-all" macro (click each slot, press Upgrade N times) at x60 and x600 until the sale threshold. It never spent Research points and never activated an Item. Not a casual-pace player and not calibrated (F-B10) | section 2 |
| Evidence | 21 PNG (game client area only, 1.95 MB) in `playtest-agent-batch-c/` next to this file | section 5 |

## 1. Channel A: strategy runs on revision 3

- Command, run from this worktree: `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`. Result line: `UNITY_TEST_PASS suite=EditMode tests=7 passed=7 skipped=0`. The default `BalanceStrategies` category was not run separately (BalanceFull plays the whole matrix).
- The regenerated `strategy-baseline-r3.md` was compared with `HEAD` and then restored with `git checkout`. **The diff contains only the `Build commit` line (`c299e8483 (+ uncommitted changes)` in the committed file, `d4c20f697` now) and the runtime and projection lines (wall time per run-play, 2.08 min against 1.93 min, projected 4.3 min against 3.9 min). Every measured number and every gate is identical.** `ProjectSettings` noise was restored too. Nothing from the run is committed except this report and its screenshots.
- Gates: all 16 asserted, none REPORTED (`strategy-baseline-r3.md` section 4): conservation 0 violations over 65 histories, `ratio-bounds` effective ratio 1.498 to 1.800, `ratio-content` highest standing 1.5 and lowest bonus 1.8, `single-lever` none faster than the chain, `chain-within-6h` slowest 7,156 s, `ip-4x` ceiling 213 IP against highest 20 IP (10.7x).

### 1.1 Medians (5 offsets) against batch B (revision 2, policy r1, `playtest-agent-batch-b.md` section 1.1)

| Metric | Batch B (rev 2, policy r1) | Now (rev 3, policy r2) | Source (now) |
|---|---|---|---|
| chain run 1 eligibility | 6,508 s | **7,148 s** (ECO-2 orientation band 6,600 to 7,800 s: inside) | `strategy-baseline-r3.md` section 3 chain |
| chain runs 2 / 3 / 4 / 5 | 4,528 / 4,528 / 4,408 / 4,408 s | 4,588 / 3,808 / 3,608 / 3,408 s (run 2 band 4,200 to 4,800 s: inside) | same |
| chain first Gear (run 1 / 2 / 4) | 688 / 328 / 168 s | 748 / 468 / 388 s (run 1 band 600 to 900 s: inside) | same |
| chain Factory Value at 900 s, run 1 | 13,557 | 11,156 | same |
| chain contracts delivered, run 1 | 193 | 163 | same |
| ore-only ratio to chain, runs 1 to 5 | 0.93 / 0.66 / 0.66 / 0.53 / 0.53 | **2.44 / 2.30 / 2.07 / 1.91 / 1.81** (slower than the chain in every run) | section 5 |
| idle-heavy ratio, runs 1 / 2 / 4 | 11,188 / 9,088 / 7,208 s | 1.21x / 1.35x / 1.57x (8,668 / 6,208 / 5,668 s) | sections 3 and 5 |
| casual run 1 eligibility (ratio) | 17,213 s (2.64x) | 12,012 s (1.68x); casual FV ratio at 900 s 4,190 / 11,156 = 0.376 | sections 3 and 5 |
| contracts-only | not reached | not reached (FV 1,037 at 1,800 s) | section 5 P4c |
| contract share of income, chain run 1 to 5 | not reported | 5.7 % to 1.9 % | section 3 |
| items granted per run, chain run 1 | not reported | 3 / 3 / 3 per tier (9 Items), ladder of 10, `perRunCap` 10 | section 5 |

Reading (measurement): the A-F1 finding of batch B (chain 92 s below the band, ore-only faster than the chain) is gone on revision 3: the chain is inside all three ECO-2 orientation bands and ore-only is 1.8x to 2.4x slower. The casual strategy is closer to the chain than before (1.68x against 2.64x in run 1) but still 2.26x to 2.95x slower in runs 2 to 5; the cause is not diagnosed here. The runner still never exercises the log, the live gap or Save V3; policy r2 does issue `Mine`, auto-deliver, bonus, cooldown and objective commands (`strategy-baseline-r3.md` section 1 "Policy").

## 2. Channel B: player session (Windows development build, `IDLEFACTORY_DEV`)

Method as in batch A section 2 and batch B section 2: user32 `SetCursorPos` and `mouse_event`, screenshots by `CopyFromScreen` of the client area with the window topmost. UI Toolkit scroll views did not react to a synthetic wheel; I scrolled by dragging the scrollbar thumb at the right edge. The development save folder was moved aside before the session and restored afterwards (the session folder remains next to it as `IdleFactory-Dev.t346-session`; parent may delete it). The player was stopped after each segment; no `IdleFactory.exe` is running now.

### 2.1 Timeline (wall time; run 1 starts 14:09:35)

| Wall | Event | Evidence |
|---|---|---|
| 14:08 | Fresh launch: Cash 37, 4 empty slots, 8 locked slots, overlay (x1 to Max) top-left, Mine bottom right, Overdrive "Locked", "Store" label present | (not kept) |
| 14:09 | Overlay buttons over a slot: a click on the x600 button where it overlaps slot 2 set x600 and did not select slot 2 (T366 check) | `01-overlay-click-consumed-x600.png` |
| 14:09:35 | First Miner (30 Cash). Hand mining in four holds of 20 to 30 s | log `mining` rows |
| 14:09 to 14:11 | Miners 2, 3, 4 at 35, 40, 46 Cash (log `purchase`: waited 31, 41 and 8 s after affordable) | log rows 5, 7, 8 |
| 14:12:46 | Cash 495 (480 reached), factory level 2 bought at 14:13:01 (log: 16 s after affordable). **3 min 11 s to affordable, 3 min 26 s to bought, at x1** | log row 14 |
| 14:13:2x | Smelter and Press built; a Smelter paused with the auto-sell explanation, then running after "Stop auto-selling Iron Ore" | `02-...`, `03-...png` |
| 14:13:54 | Contracts: bonus Plate (1,440 Cash, 12 RP, Item, "Bonus, ends in 14:07"), Gear locked, standing Plate (1,200 Cash, 4 RP, Item), production objective (64 Cash, 2 RP, Item) | `04-offers-bonus-standing-objective.png` |
| 14:14:16 | x10. Bonus delivered by auto-deliver: Cash 213 to 1,653, 12 RP | `05-...png` |
| 14:15:56 | Factory level 3 (3,000 Cash) after selling 652 Ore by hand (store was full) | log row 28 |
| 14:15 to 14:18 | Assembler, Gear offers (100 Gear = 3,600 Cash, 101 = 3,636 Cash), store full twice, Gear sold by hand (400 for 9,600 Cash) | log rows 34 to 40, `07-...png` |
| 14:18:38 | Factory level 4 (8,500 Cash), storage level 2 | log rows 41, 42 |
| 14:19 to 14:22 | x60 then x600, upgrade macro, factory level 5 (48,000 Cash), storage levels 3 to 5 (10,000, 40,000, 160,000 Cash) | log rows 116, 263 to 265 |
| 14:23:58 | Prestige panel "Ready to sell": Factory Value 15.1 M, +12 IP (Cash 14.5 M of it) | `09-run1-prestige-ready.png` |
| 14:24:20 | Factory sale #1 at Factory Value 18,350,606 (threshold 10 M), +13 IP | log `prestige` row 388 |
| 14:24:20 to 14:32:44 | Run 2: four Miners, level 2 within the first 600 game seconds, bonus delivered again (1,440 Cash, 12 RP, Item), level 3 between 900 and 1,800 s, level 4 and 5, x600 macro | log rows 389 to 646 |
| 14:32:44 | Factory sale #2 at Factory Value 15,213,341 (threshold 11 M), +12 IP; "Next sale needs 12.1M" | log `prestige` row 736, `10-run2-start-after-sale.png` |
| 14:33 | Session-log export "Last export" shown; 737 lines | `14-session-log-export.png` |
| 14:34 to 14:37 | Portrait 390x844 on the same save: clock dialog, locked contracts message, a few Miners, Smelter guidance, contracts sheet with offers | `15-` to `18-...png` |
| 14:38 to 14:40 | Landscape relaunch: Industry section found at the bottom of the Research tab, 8 IP spent on Calibration 3, Market Contacts 1; Seed Capital 1 and 2 (13 IP) | `20-`, `21-...png` |

### 2.2 Measurements (session-log export `session-log-20261022T123300Z.jsonl`, 737 lines, schema 1)

| Item | Value | Source |
|---|---|---|
| Row counts | purchase 652, bottleneck 34, contract 12, sample 12, mining 9, header 8, sale 7, prestige 2, lifecycle 1 | `t` field of every row |
| **Header rows** (T366) | **8 of 737 lines (1.1 %)**; `timeScale` of the headers 1, 1, 10, 60, 600, 10, 60, 600. Batch B: 308 of 706. A header is written once per sustained change (the first two are the start snapshot around `lifecycle start`); a first quick x600 and back to x1 without an event in between wrote none. **[inference]** the header is emitted lazily before the next event row | header rows |
| Factory Value in run 1 | 600 s: 3,797; 900 s: 5,960; 1,800 s: 12,318; 3,600 s: 33,610; 5,400 s: 66,268; 7,200 s: 95,915 | `sample` rows |
| Same points, chain policy r2 (channel A, run 1) | 600 s: 3,844; 900 s: 11,156; 1,800 s: 95,687; 3,600 s: 1,331,076; 5,400 s: 4,302,316; 7,200 s: 10,169,882 | `strategy-baseline-r3.md` section 3 |
| Factory Value in run 2 | 600 s: 1,623; 900 s: 3,217; 1,800 s: 11,557; 3,600 s: 24,494; 5,400 s: 38,762; 7,201 s: 59,662 | `sample` rows 397 to 437 |
| Factory level at samples | run 1: level 2 at 600 and 900 s, level 3 at 1,800 s; run 2: level 2 at 600 and 900 s, level 3 at 1,800 s, level 4 at 5,400 s | `sample.factoryLevel` |
| Samples stop | no sample after 7,200 s in either run, although each run went on for about 30 h of game time | last `sample` row per run |
| Mining | run 1: 1,054 counted clicks gave 105 Ore (0.1 Ore per click) in six holds; run 2: 481 clicks gave 48 Ore | `mining` rows |
| Contract Cash against manual sales, run 1 to the 3,600 s sample | contracts 11,088; manual sales 10,252 (auto-sales are not logged) | `contract` and `sale` rows |
| Contract rows | run 1: bonus, Plate, Gear, Plate, Gear (5 completions; Items granted at completion 1, 2 and 4 as Diamond Drill Head, Flux Catalyst, Overclock Core); run 2: bonus only. Rewards 1,440 (1.8x), 1,200 (1.5x), 3,600 and 3,636 (Gear 1.5x), 1,212 | `contract` rows, UI reward lines |
| Store-full seconds | run 1 `iron.ore` 39,127 (last bottleneck block), run 2 `iron.ore` 29,268; earlier blocks 144, 756, 834 | `bottleneck` rows |
| Prestige | run 1 Factory Value 18,350,606 against 10,000,000, +13 IP; run 2 15,213,341 against 11,000,000, +12 IP; `industryPurchases` empty in both; 25 IP held after run 2 | `prestige` rows |
| Player logs | no `Exception` in any of the three player logs (about 30 min of play in total); one warning per launch: `Unknown pseudo class "last-child" in StyleSheet ResearchPanel` | `player1.log`, `player2.log`, `player3.log` |

## 3. Observation list from the T345 Product Owner review

1. **Time to factory level 2 (480 Cash).** At x1 with hand mining: 3 min 11 s to affordable and 3 min 26 s to bought (section 2.1). Hand mining supplied 105 of roughly 520 Cash earned in that time (about 20 %). Waiting between purchases (log `secondsSinceAffordable`): second Miner 31 s, third 41 s, fourth 8 s, level 2 16 s. I was never idle in the sense of having nothing to do (Mine hold, build): from the fourth Miner to 480 Cash was about 95 s. **[inference]** Without holding Mine the same path is about 40 s longer, and the screen shows nothing to do in that stretch except wait. Run 2 was at x10 and gives no x1 time (level 2 was bought inside the first 600 game seconds).
2. **Time to level 3 (3,000 Cash) and idle waiting.** Not measured at x1. In both runs (x10) level 3 was bought between the 900 s and the 1,800 s sample (`factoryLevel` 2 at 900 s, 3 at 1,800 s), 172 s (run 1) and 28 s (run 2) after it became affordable. The chain policy has its first Gear at 748 s (median), so my scripted player was later than the chain (as in batch B, not a pace calibration). The wait after level 2 was filled by the first Plate delivery: Cash 213 to 1,653 (see point 6).
3. **Cooldown messages on first sight.** After a delivery every standing offer shows "Available in 1:45" (Plate, 120 s cooldown) or "Available in 0:43" (Gear, 60 s), a red warning triangle and "That contract is cooling down", with a disabled Accept; the line reads "Available now" when ready (`06-autodelivered-cooldown-on-standing-offer.png`). **[inference]** The text is understood at once; the red triangle is the colour of an error for a normal state (F-B1 still open).
4. **1.8x bonus against standing offers.** Observed: bonus 1,440 Cash, 12 RP, Item, "Bonus, ends in 14:07", first card in the list; standing Plate 1,200 Cash, 4 RP, Item (`04-...png`). The bonus pays more in both Cash (+20 %) and RP (3x). **[inference]** It reads as the better offer on first sight (batch B: 1.5x read as worse). Not yet clear: only one contract can run at a time, so a player who takes the standing offer first (my run 1 started with the bonus because it was first) loses the bonus window; and a used bonus card stays first in the list (C-F3).
5. **Items received and long gaps.** The ladder 0, 1, 3, 6, 12, 25, 50, 100, 150, 200 is visible in the reward lines of the offers: Items on completion 1, 2 and 4, none on 3 and 5 ("Reward: 3,600 Cash, 8 RP", `05-...png`), and nothing says when the next one comes. In run 1 three Items (one per tier) arrived within the first 4 deliveries; the next is the 7th completion. Chain-policy completions per run are 163 (run 1), so the ladder gaps are 6, 13, 25, 50 and 50 completions (ladder differences; deliveries from `strategy-baseline-r3.md` section 3). **[inference]** At the 44 s average between deliveries of the chain, the last two gaps are about 37 min of play each. I activated no Item.
6. **First-Plate Cash jump and hard stops.** The bonus delivery took Cash from 213 to 1,653 (7.8x) a minute or two after the Press was built, and the second delivery from 1,653 to 2,853. **[inference]** Large against the $1 Ore trickle, and it makes level 3 (3,000) feel one sale away. Hard stops observed: the store (1,000 at level 1) filled with unsold Ore within about a minute of wall time at x10 after I turned Ore auto-sell off to feed the Smelter; the machines showed "FULL" tags and Cash 2,853 could not reach 3,000 until I sold Ore by hand (`07-store-full-tags-gear-sale.png`). In run 2 Cash fell to 4 with the store full of 997 unsold Gear (about 23,900 Cash) because Gear auto-sell is off by default (C-F2).
7. **How long hand mining feels useful.** 1,054 clicks gave 105 Ore in run 1 (about 20 % of the first 3.5 minutes' Cash) and 481 clicks gave 48 Ore in run 2. At 4 Miners one hold adds 1 Ore/s against 4 Ore/s (+25 %); with 6 Miners at level 7 (about 8.5 Ore/s) it is +12 %. **[inference]** Useful until about the first factory expansion, after that I stopped using it. `chain+mining` is 0.6 % to 2.2 % faster than the chain in channel A (MIN-9 gate).
8. **Industry purchase choices and Seed Capital.** The Industry section exists, but it sits at the bottom of the Research tab, below the research graph (C-F1). Once found: five upgrades with level, effect, "Next level: N IP", an "Applies next run" tag on start-of-run effects and a running line "Next run starts with 962 Cash and storage level 1" after Seed Capital level 1 and 2 (5 IP then 8 IP, +500 and +425 Cash, 13 IP for +925). **[inference]** The choices are clear in the card text. 962 Cash is 26x the 37 Cash start and covers factory level 2 (480) and four Miners (about 150) at second 0, which is the first 3.5 minutes I measured at x1 in run 1. The model gain of Seed Capital level 1 is 324 s of 7,148 s (`balance-trace-r3.md` section 4.1), the smallest per IP of the five. I did not play a third run, so how it feels in a run is not observed.
9. **Contracts beyond Cash.** Run 1: five deliveries gave 11,088 Cash and 36 RP against 10,252 Cash of manual sales to the 3,600 s sample, so early contracts are about half of my Cash. Chain policy: 5.7 % (run 1) falling to 1.9 % (run 5) of Cash. I spent no RP (36 RP unspent at the first sale, 12 at the second, RP reset with the sale) and used no Item, so I cannot say whether RP or Items make contracts worth doing late. **[inference]** After the first five or so deliveries the only remaining reason is RP and Items, and the offers do not say what they buy.
10. **F-B10 reminder.** My run is not a pace calibration: Factory Value at 900 s of run 1 was 5,960 against 11,156 for the chain, at 3,600 s 33,610 against 1,331,076, and I reached 10 M only after about 30 h of accelerated game time with Cash hoarded (14.5 M of 15.1 M Factory Value at the first "Ready to sell" was Cash). The human playtest (T353) is the only pace evidence.

## 4. Verification of the merged follow-ups

| Item | Result | Evidence |
|---|---|---|
| T366 overlay buttons do not select the slot beneath | **Closed.** A click on x600 where the button overlaps slot 2 changed the scale and left slot 1 selected; later clicks that hit buttons over slot tops also selected nothing. Residual: in an accelerated state the buttons and the red marker cover the top of row 1 including its "+" build targets, so I had to click the lower edge (y about 235) of those slots | `01-overlay-click-consumed-x600.png`, `04-...png` |
| T366 log header only on step changes | **Closed.** 8 header rows of 737 lines, 6 of them real scale changes (section 2.2) | header rows |
| T365 guidance never on a running machine | **Closed for the cases I saw.** A running Miner (levels 1 to 7), Smelter, Press and Assembler showed only status and the upgrade block, never a blocked reason or a fix button; a paused Smelter showed "Missing Iron Ore: auto-sell is selling it before this machine can use it (needs 2, has 0)" with "Stop auto-selling Iron Ore", and after the button the same machine was running with no guidance; a paused Press showed "Missing Iron Bar: needs 2, has 1". Not observed: the panel of a machine tagged "FULL". Residual wording: C-F4 | `02-...`, `03-...`, `17-...png` |
| T344 | Test-only audit; nothing player-visible to verify | `tasks.md` T344 |
| Revision 3 values in the UI | Factory level 2 costs 480, level 3 3,000, level 4 8,500, level 5 48,000; Miner 30, 35, 40, 46, 53 (growth per owned copy), 33 after two prestiges (1.04 per prestige); standing Plate 1,200 (1.5x of 800), Gear 3,600 (1.5x of 2,400), Timed bar 450 (1.5x of 300), bonus 1,440 (1.8x), objective 64 (0.4x of 160); cooldowns 120 s (Plate), 60 s (Gear) seen as 1:45 and 0:43 after delivery | `04-...png`, `06-...png`, `18-...png` |

## 5. Findings

Batch-A and batch-B findings first (status now), then new findings (C-F...). "Disposition" is my proposal; the owner decides.

### 5.1 Status of earlier findings

| ID | Status on this build | Evidence |
|---|---|---|
| A-F1 chain band and ore-only | **Closed by measurement.** Chain run 1 7,148 s (band 6,600 to 7,800 s), run 2 4,588 s (4,200 to 4,800 s), first Gear 748 s (600 to 900 s); ore-only 2.44x to 1.81x slower. Approval stays with the owner (T354) | section 1.1 |
| A-F2 runner coverage | **Closed for Mine and auto-deliver** (policy r2); the log, live gap and Save V3 remain outside the runner | `strategy-baseline-r3.md` section 1 |
| A-F3 ratios above 1.5 | **Closed in content.** Standing 1.5, bonus 1.8, objective 0.4 observed in the UI and `ratio-content` | section 4 |
| A-F4 cooldown, bonus, objective visible | **Closed (positive)** with the revision 3 values | `04-`, `06-...png` |
| A-F10 Research cards clip on the right ("Deep Vei", "Gear Mark", "Scroll sideways to see more Research.") | **Open, Low** | `12-research-clipped.png` |
| F-B1 ended or used bonus card pinned first with a red triangle; cooling-down offers use the same triangle | **Open, Low to Medium** | `05-...png`, `18-...png`, `06-...png` |
| F-B2 overlay click-through | **Closed** for clicks; residual covering of row 1 in the accelerated state, Low (dev only) | section 4 |
| F-B3 stale guidance on running machines | **Closed** for the tested cases (T365) | section 4 |
| F-B4 log header flood | **Closed** (T366) | section 2.2 |
| F-B5 clock "earlier than when you left" after acceleration | **Open, Low (dev only).** The first launch after the run showed "The device clock shows a time earlier than when you left, so no offline production was credited ... Away for 0 s" in landscape and again in portrait | `15-portrait-clock-earlier-dialog.png` |
| F-B6 store-full stall | **Open, Medium** (C-F2) | `07-...png` |
| F-B7 offers hard to tell apart; Item line comes and goes | **Open, Low.** Three cards titled "Plate contract" (bonus, standing, objective); the Item disappears from the reward line on completion 3 and 5 without explanation | `04-`, `05-...png` |
| F-B8 welcome-back wording | **Not re-observed** (needs a long absence; T349) | none |
| F-B9 `last-child` USS warning | **Open, Low.** One `Unknown pseudo class "last-child" in StyleSheet ResearchPanel` per launch in all three player logs | `player1.log` line 45 |
| F-B10 scripted run is not a pace calibration | **Stands** (observation 10) | section 3 |
| Batch-B question 6, where 10 IP are spent | **Answered:** at the bottom of the Research tab (C-F1) | `20-...png` |

### 5.2 New findings

| ID | Observed behaviour | Evidence | Affected FR/SC | Severity | Proposed disposition |
|---|---|---|---|---|---|
| C-F1 | The Industry section (IP balance, five permanent upgrades) is the last block of the **Research** tab, below the research graph and its detail card. Nothing on the Prestige panel, the sale dialog or the "Factory Sale complete" banner points there; the panel says only "You keep ... Industry upgrades" and "Run 3 starts with 37 Cash". I did not find it in two prestiges (25 IP unspent) and batch B did not find it either | `13-prestige-panel-no-pointer-to-industry.png`, `10-run2-start-after-sale.png`, `20-industry-section-bottom-of-research-tab.png` | FR-023, US6 (the player buys an Industry upgrade and starts stronger), SC-009 | Medium (the reward of the prestige loop is hidden) | Named later task: UI/UX review T352 (pointer in the sale dialog and banner, dot on the Research tab, or a section header at the top) |
| C-F2 | The store-full stall is still the main hard stop. The only automatic remedy is auto-sell, which sells a machine's input before the machine can use it (Smelter and Press paused, `02-`, `08-`), and auto-sell is off for every resource at the start of each run. So Gear that nobody sells fills the store (997 Gear, about 23,900 Cash, Cash 4 in run 2) and Ore that nobody sells fills it too; income is zero until the player sells by hand. The log counts 39,127 and 29,268 store-full seconds for Ore in the last block of runs 1 and 2. The runner never meets this because policy r2 dumps surplus by `Sell` | `07-store-full-tags-gear-sale.png`, `08-ore-autosell-starves-chain.png`, `bottleneck` rows | US1, SC-009, FR-001 to FR-003 | Medium | Idea / named later task: an auto-sell mode that keeps what machines and the active contract need, or auto-sell default on for the terminal product; owner decision |
| C-F3 | The bonus card shows three different states for the same used offer: "Bonus used" and "That bonus contract is not available" when no contract is active (`05-`), "Bonus, ends in 7:36" with "Finish or abandon the active contract first" while another contract is active (`19-`), then "Bonus used" again after the delivery | `05-...png`, `19-...png` | FR-005, US2, SC-009 | Low | T352 (one state label per offer) |
| C-F4 | Under a correct blocked reason the panel adds a generic hint that does not match the cause: a Smelter paused by auto-sell says "Build a Miner to make Iron Ore." while four Miners stand (`02-`, `17-`); a Press missing Bars with a Smelter present says "Build a Smelter to make Iron Bar." (seen at slots 6 and 8 in run 1 and 2; screenshots not kept) | `02-smelter-paused-autosell-guidance.png`, `17-portrait-smelter-guidance.png` | SC-009, T365 | Low | Defect: current feature (T365 follow-up) or T352 |
| C-F5 | Industry cards state the strength of the first level "per level" ("Speed x1.22 on all machines per level", "Sale price x1.12 ... per level"), but the tuned levels decay (Calibration 22.0 %, 19.4 %, 17.0 % ... in `balance-trace-r3.md` section 4). **[inference]** A player may expect the same gain from every level | `20-...png`; `balance-trace-r3.md` section 4 | FR-021, SC-009 | Low | T352 / idea: show the next level's own gain |
| C-F6 | The Research detail card shows two red warning triangles with one message ("Not enough Research points.") and an empty second line | `21-...png` (also visible under the card in the Research scroll) | SC-009 | Low | Defect: current feature or T352 |
| C-F7 | The session log has no time on its event rows and samples only at 600, 900, 1,800, 3,600, 5,400 and 7,200 s. Level times, purchase times and the length of an accelerated run cannot be read from it; I used my wall clock and the scale for run lengths (estimates) | section 2.2 | FR-040 to FR-044 (log), SC-005 | Info (measurement limit) | Idea / named later task: T349 or the DevTools recipe; owner decides whether the log should carry run seconds |
| C-F8 | Locked Contracts text: "Contracts unlock after you produce Iron Plate or you produce Gear or you produce Iron Bar." (order and repeated "you produce") | `16-portrait-contracts-locked.png` | SC-009 | Low | T352 wording |

Feel statements, all **[inference]**: the first three minutes at x1 are quick and clear (Miner, hold Mine, sell, expand); the first Plate delivery is a large step and the bonus is the first thing a player wants; a store that fills while you read a panel is a stall the game explains well on the machines ("FULL" tag, "Missing ... auto-sell is selling it") and badly at resource level; the Industry choices are understandable once found, and Seed Capital at 962 Cash makes the start of a run something other than the opening I played.

## 6. Evidence index (`playtest-agent-batch-c/`)

| File | Shows |
|---|---|
| `01-overlay-click-consumed-x600.png` | overlay button over a slot: scale set, slot not selected (T366) |
| `02-smelter-paused-autosell-guidance.png` | paused Smelter, blocked reason and fix, generic hint (C-F4) |
| `03-smelter-running-no-guidance.png` | same Smelter running, no guidance (T365) |
| `04-offers-bonus-standing-objective.png` | bonus 1,440 / 12 RP, Gear locked, standing 1,200 / 4 RP, objective 64 / 2 RP |
| `05-bonus-delivered-used-card-pinned.png` | Cash 1,653, 12 RP, "Bonus used" card first, reward lines without Item |
| `06-autodelivered-cooldown-on-standing-offer.png` | "Available in 1:45 / That contract is cooling down" |
| `07-store-full-tags-gear-sale.png` | store FULL, machines "FULL" tag, Gear stock 379 |
| `08-ore-autosell-starves-chain.png` | Ore auto-sell on: Smelters and Presses "IN!" |
| `09-run1-prestige-ready.png` | Ready to sell, Factory Value 15.1 M, +12 IP, Cash 14.5 M |
| `10-run2-start-after-sale.png` | after sale: Cash 37, "Needed to sell 11.0M, next 12.1M", no pointer to Industry |
| `11-items-tab.png` | three Items with effect lines |
| `12-research-clipped.png` | Research cards clipped on the right (A-F10) |
| `13-prestige-panel-no-pointer-to-industry.png` | bottom of the Prestige panel |
| `14-session-log-export.png` | export UI |
| `15-portrait-clock-earlier-dialog.png` | clock dialog in portrait (F-B5) |
| `16-portrait-contracts-locked.png` | locked Contracts sheet, portrait (C-F8) |
| `17-portrait-smelter-guidance.png` | portrait machine panel with guidance |
| `18-portrait-contracts-offers.png` | portrait contracts sheet, "Bonus ended", tab marker "Contracts !" |
| `19-bonus-card-label-while-other-contract-active.png` | "Bonus, ends in 7:36" while another contract is active (under the abandon dialog; C-F3) |
| `20-industry-section-bottom-of-research-tab.png` | Industry upgrades at the bottom of Research |
| `21-industry-next-run-starts-with-962-cash.png` | IP 4, "Next run starts with 962 Cash and storage level 1"; two warning triangles under Bargaining (C-F6) |

## 7. What I could not observe

- Run 3 and later (Industry effect in a run, Seed Capital feel in play), SC-011 mining at prestige index 2 or more.
- Research purchases and Item activation (I spent no RP and used no Item); Research cards beyond the first two columns (clipped).
- Timed contract expiry, contract Abandon and Expire log rows (the Abandon dialog appeared once by a mis-click and was kept), the production objective (never accepted), a Gear offer cooling down in the log.
- The machine panel of a "FULL" machine; the 13 h offline wording (F-B8); offline return on a never-accelerated save (T349).
- Android (no `adb` call; the emulator lease was held but not used); casual-pace play; accessibility (contrast, text scale, screen reader), SC-009 stays with the UI/UX reviewer (T352).

## 8. Open owner questions

1. C-F1: where should the player learn that Industry exists (sale dialog, banner, tab marker), and should the section move above the research graph?
2. C-F2: should auto-sell be able to keep what machines and the active contract need, or should it default on for the terminal product?
3. C-F7: should the session log carry run seconds, so that long accelerated runs can be timed from it?
4. F-B1, F-B7, C-F3, C-F4, C-F5, C-F6, C-F8, A-F10: collect them in the T352 UI/UX review, or fix the cheap ones (C-F4, C-F6) now?
5. F-B5: leave the clock dialog after acceleration as a DevTools recipe note, or clamp the offset at quit?
6. Is 962 Cash of Seed Capital level 2 (13 IP) the size you want against a 3.5 minute opening? The human playtest should look at it.
