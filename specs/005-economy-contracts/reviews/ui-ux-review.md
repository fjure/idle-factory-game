# F005 UI/UX review (T352, issue #417)

| | |
|---|---|
| Commit reviewed | `297ccfb` (origin/main after #498; base of branch `maqa/issue-417-t352-uiux-review`) |
| Reviewer | Independent UI/UX Reviewer, Medium tier (sonnet). Did not author T319, T324, T330, T333, T340, T359, T362 or T365. Read-only towards the product. |
| Date | 2026-10-22 |
| Authority | `contracts/ui-surfaces-f005.md`, QA-12 table (`tasks.md` lines 720-735), spec FRs/SCs, `wiki/pages/run-shell-device-profile-and-text-scale.md`, owner decisions on #417, #408, #411 |
| Verdict | **Accept with follow-ups** (section 6) |

## 1. Method and its limits

What this review did:

- Read the contract and QA-12 rows, then the shipped UI code (`Assets/IdleFactory/Unity/UI/**`, USS) at `297ccfb` for every control and for every #417 follow-up.
- Read the Play Mode panel suites to see which QA-12 cells they assert (screen class, 100/200 %, touch/pointer, focus). Suites: `MarketSellPanelTests`, `OreVeinPanelTests`, `OreVeinRealPointerHoldTests`, `BuildPricePanelTests`, `ContractsPanelTests`, `ContractsAutoDeliverPanelTests`, `GoalsLinePanelTests`, `PrestigePanelTests`, `SessionLogPanelTests`, `MachinePanelGuidanceTests`, `IndustryPanelTests`, `ResearchPanelTests`, `OverdriveControlTests`, `DevToolsOverlay*Tests`.
- Read the three Play-Tester reports (batch A, B, C) and their screenshots as evidence of real rendering.

What this review could not do (no editor, no CLI, no player, no emulator, no device; no lease held):

- **Tests were read, not run.** "Verified (test)" below means a named test asserts the cell and I checked its assertions by reading; green results come from the merged Issues' QA, not from this review.
- No live capture, no screen reader, no real touch on a phone, no notch, no font scale from the OS. Those cells say "Not verifiable" and section 5 says exactly what to check.
- Screenshots exist only for the Windows desktop (1280x720) and a portrait window; they show no 200 % text and no phone class.

Legend: **T** = asserted by a Play Mode test (by reading); **S** = visible in a Play-Tester screenshot; **C** = verified by reading code; **NV** = not verifiable here (section 5); **R-n** = finding.

Screen classes (`ShellScreens.All()`): P1 360x640, L1 640x360, P2 390x844, L2 844x390, TP 768x1024, TL 1024x768, D1 1280x720, D2 1920x1080.

## 2. Coverage matrix

Every row is expected at all eight classes, at 100 % and 200 % text, with touch and pointer. "All 8 x 2" means the suite loops `ShellScreens.All()` (or a named subset) times `{100, 200}`.

| Control (contract) | Classes x scale (layout, containment, 44 pt) | Touch / pointer exact value | Focus and keyboard | Accessible name with state | Findings |
|---|---|---|---|---|---|
| `factory-ore-vein` (+progress, pulse, reason) | T: `OreVeinPanelTests` loops screens at 100/200 (3 `All()` loops, one phone loop); real-input hold on Windows only (`OreVeinRealPointerHoldTests`, gate #408 passed 2026-10-21). Rotation, hold end, dialog: T. Phone 844x390 at 200 %: NV | T (pointer capture, second pointer, no peek on touch hold; 14 touch / 37 pointer cases). Real touch: NV | T (KeyDown/KeyUp) | C: tooltip-only, platform limit | R-17, R-18, R-19 |
| `inventory-sell-step-*` and `inventory-sell-*` | T: `SellControlsStayContained...AtEveryScreenClassAndTextScale`, `TheSellAllConfirmation...`, locked rows at every class and scale | T: hover shows exact value (pointer), tap pins (touch). Real touch: NV | T: step -> Sell -> Auto-sell order, Enter/Space, focus kept on stale tap and empty stock | C: `SellName` / `StepName` follow the contract; tooltip-only on the element | R-03, R-04, R-05, R-16 |
| `contracts-autodeliver`, hint, notices | T: `ContractsAutoDeliverPanelTests` (17 `All()` loops, 8 scale cases) | No exact-value gesture on these texts (plain text, no number to inspect) | T: focus asserted (17 cases) | C: "Auto-deliver, on/off" | none |
| `contracts-offer-cooldown-*`, bonus, Accept rejection copy | T: `ContractsPanelTests` (18 `All()` loops), countdown tick, "Bonus ended" without layout jump (reserved height, C) | n/a (text) | T | C | R-06, R-07, R-08, R-09, R-10 |
| `contracts-produce-progress` | T (ContractsAutoDeliver/ContractsPanel suites) | n/a | T | C | R-08 (titles) |
| `tab-contracts-badge` and tab label | T via shell suites; label "Contracts (n)" / "Contracts !" seen in `playtest-agent-batch-c/18` (S) | n/a | T | C: "Contracts, n deliverable" | none |
| `build-price-*`, Remove confirmation | T: `BuildPricePanelTests` (3 `All()` loops at 100/200) | T: touch 5 / pointer 9 cases | T | C | R-02, R-11 |
| `goals-next` | T: `GoalsLinePanelTests` (7 `All()` loops, 4 scale cases) | T: touch 10 / pointer 11 | T (21 focus cases) | C: name on focusable `goals-exact` | none (known limit: Market only) |
| `prestige-threshold` | T: `PrestigePanelTests` (worst case, below threshold, failed sale, at every class and scale) | `PrestigePresenter` attaches exact values (C); no touch/pointer-specific test in the suite | T (25 focus cases) | C | R-01, R-12 |
| `session-log-*` | T: `SessionLogPanelTests` (2 `All()` loops at 100/200). Notch, rotation, 200 % card on a device: NV | none in the suite (the "Last export" line is selectable text, not an exact value); real touch/copy: NV | T (18 focus cases) | C: tooltips; screen reader NV | R-13 |
| `dev-timescale`, `dev-accelerated-marker` | T: `DevToolsOverlayLayoutTests`, `...PlacementTests` (top-left below the strip, no overlap with Mine at QA-12 classes) | T: `DevToolsOverlayPointerTests`, real input on Windows (batch C, S) | T | C: `MarkerAccessibleName` + tooltip only | R-14, R-15 |
| Machine panel guidance and fix buttons (T365, not a contract row; added because it is player-visible F005 work) | T: `MachinePanelGuidanceTests` (1 `All()` loop, 200 %) | n/a | **no focus test; no focus handling in code** | no announcement | R-20, R-21, R-22, R-23 |
| Research / Industry (T341 content, T116 surface; added because prestige loop reward lives here) | T: `ResearchPanelTests`, `IndustryPanelTests` (9 `All()` loops) | n/a | T | C | R-01, R-24, R-25, R-26, R-27 |

Test gaps against the QA-12 sentence "every row is checked with touch and pointer exact-value inspection": the suites for contracts, prestige, session log, Industry and Overdrive contain no touch or pointer case (counted by `grep`), while the Sell, Ore vein, Build price and Goals suites do. Only Prestige attaches exact values among them (`PrestigePresenter`, 5 hits), so the gap is a missing test, not missing behaviour. See R-28.

## 3. Findings

Severity scale: **blocker** (breaks a journey or an FR), **major** (a player will plausibly get lost or misled), **minor**, **polish**. Nothing here is a blocker. "Where" gives screen class, text scale and input; findings seen in a Play-Tester report carry its evidence path under `reviews/playtest-agent-batch-*/`.

| ID | Control / screen | Class, scale, input | Observed | Expected | Severity | Acceptance impact | Proposed disposition |
|---|---|---|---|---|---|---|---|
| R-01 | Prestige panel, sale dialog, "Factory Sale complete" banner; Research tab (C-F1) | D1, 100 %, pointer (`batch-c/10, 13, 20`); same text on every class | The Industry section (IP balance, five permanent upgrades) is the last block of the Research tab, under the graph and the detail card (`ResearchPanel.cs` hosts `IndustryPanel` as a section). `PrestigePresenter.cs:489` says "Items and Industry upgrades stay." and nothing points to it. A Play-Tester missed it across two prestiges (25 IP unspent) | The prestige reward is findable: US6 "the player buys an Industry upgrade and starts stronger", SC-009 | **major** | US6, FR-023, SC-009. The loop's reward is invisible; a human playtest will fail the same way | **Defect in F005 now (owner decision)**: smallest fix is a line plus a button "Spend N IP in Industry" on the Prestige panel and in the post-sale banner that opens Research scrolled to Industry, and an IP dot on the Research tab. Moving the section above the graph is the alternative (owner question Q1) |
| R-02 | Currency wording across panels (#384 follow-up) | all, text only | Visible copy mixes "Cost: 69 Cash" (`BuildSheetPresenter.cs:315`), "Reward: 1,440 Cash, 12 RP" (`ContractsPanel.cs`), "You get 34 Cash back" (`MachinePanelPresenter.cs:634`) with "$" in tooltips, exact lines and "Building another X costs $68" (`MarketPanelHost.cs:187`; `InventoryRatesPanel.Sell.cs:353`) | One form per meaning; the contract writes prices as "$" ("Build Assembler, $69"; "Sold N X: +$Y") | minor | Consistency (SC-009); no FR | **Owner question Q2**. Recommendation: "$" for every amount, "Cash" only as the label of the balance (HUD, goals, "Cash 1,653"). It is shorter at 200 % on 360 pt phones and matches the contract and the accessible names already written. One mechanical pass in F005 is cheap; F004 restyling would otherwise inherit the mix |
| R-03 | Sell: Amount control (#382 d) | P1 and L1, 200 %, touch | A single cycling button "Amount: 10" (`InventoryRatesPanel.Sell.cs:200`). The four steps 1 / 10 / 100 / All (`iron-factory-content.json:15`) are hidden; reaching "All" from 1 takes three taps and the player cannot see the choices. Accessible name "Sell amount, 10" does not say it cycles | A "selector" (contract wording) whose options are visible | minor | FR-038 clarity; not a command change | **Recommend a segmented selector** (four 44 pt segments: 4 x 44 = 176 pt fits 360 pt; wraps to two rows at 200 %), name "Sell amount, 10, selected", arrow keys within the group. The contract invariants (command, soft-disable, exact value, accessible name with state) are untouched, so it can ship in F004 restyling. Owner decides; F005 acceptance does not depend on it |
| R-04 | Sell block layout (#382 a) | all classes, 100/200 % | `.inv-sell__controls` is a column and every control is stretched full width (`InventoryRatesPanel.uss:164-192`): step, Sell, result, hint each take a line | Contract: "beside price and Auto-sell". Today they stack at every size | minor | Contract wording not met literally; no FR | **No action in F005; amend the contract wording** ("below the row") and let F004 add a wide-width row layout for TP/TL/D1/D2 where there is room. Phones keep the column (needed for 44 pt at 200 %) |
| R-05 | Sell row after a sale (#382 b) | P1, L1, 200 %, touch | Rows are taller at 200 %, so more scrolling per sale; focus returns to Sell, which can sit at the viewport edge. `ScrollIntoView` exists (T362) but the "edge" claim needs a capture | Focus visible and not clipped after the action | minor | none | **NV** (section 5, check C-3). If confirmed: F004 (scroll margin). No F005 change |
| R-06 | Offers list (F-B1) | D1 100 % pointer (`batch-b/06, 16`; `batch-c/05, 18`); portrait (`batch-b/20`) | Offers keep the static content order (`ContractsPanel.cs:171`, rows never re-sorted). The bonus offer is first, so once ended or used it stays on top of the list. `OfferRow.Render` marks the reason line with the **warning** icon for every unlocked offer that cannot be accepted (`ContractsPanel.cs:886`), so a cooldown ("That contract is cooling down") and "That bonus contract is not available" both show the red alarm | Live offers first; a normal state (cooldown, used) must not look like an error (information hierarchy; colour is never the only signal, but the icon is the signal here) | **major** | US2, SC-009: a dead card tops the list for most of the session and teaches the player to ignore warnings | **Defect in F005 now (small)**: sort `Accept`-able offers first, then cooling down, then ended/used/locked (stable inside a group, no layout jump on the once-per-second tick); use a neutral clock icon (or none) for cooldown and used; keep the warning icon for real rejections after a tap |
| R-07 | Bonus card state (C-F3) | D1, 100 %, pointer (`batch-c/05, 19`) | One used offer reads "Bonus used" (and "That bonus contract is not available"), then "Bonus, ends in 7:36" with "Finish or abandon the active contract first" while another contract is active, then "Bonus used" again (`ContractsPanel.cs` `CooldownText`: the used test is `CooldownRemainingMs > 0 || AcceptReason == ContractBonusUnavailable`, which flips with the active contract) | One state label per offer: Available (ends in m:ss), Used, Ended | minor | US2, FR-005 | **Defect in F005 now, with R-06**: derive the label from the offer's own state; the blocked-by-active-contract reason is a separate line |
| R-08 | Offer / active card titles (F-B7) | D1 and portrait (`batch-b/05, 16, 20`; `batch-c/04`) | `KindLabel` (`ContractsPanel.cs:673`) gives the standing Plate offer, the bonus Plate offer and the Plate production objective the same title "Plate contract"; they differ in the second line only. Item rewards drop out of the reward line after several deliveries with no explanation (`perRunCap`, #387 open) | Titles that tell the three apart ("Plate contract", "Bonus: Plate contract", "Produce Plate"); a reason when an Item is capped | minor | US2 readability | Titles: **defect in F005 now (with R-06, content labels only)**. Item explanation: **owner question Q5** because it depends on the open `perRunCap` decision (#387); until then a one-line "Item reward limit reached this run" is the honest copy |
| R-09 | Bonus appeal (#466) | all | The reward line already names the gain: "Reward: 1,440 Cash, 12 RP" (`RewardText`, shown in `batch-c/04`). RP gain is named | RP gain named on the card | polish | none | **Fixed** (named). Making it stand out is balance/visual emphasis with the T345 ratios, not a defect |
| R-10 | Locked Contracts empty state (C-F8) | portrait, 100 % (`batch-c/16`) | "Contracts unlock after you produce Iron Plate or you produce Gear or you produce Iron Bar." `UnlockSummary` (`ContractsPanel.cs:709-721`) joins full clauses with " or " | "Contracts unlock after you produce Iron Plate, Gear or Iron Bar." | minor | SC-009 wording | **Defect in F005 now (copy)**: group conditions of the same kind, join the labels. Mixed kinds (factory level, research) keep "or" between groups |
| R-11 | Remove confirmation body (#384) | 640x480 and small phones, 200 %, touch | The dialog body scrolls at 200 % (shared dialog, `RunShellDocument.cs:1371`); the sentence about the rebuild price is the third in `MachinePanelPresenter.cs:634` and can sit below the fold. 640x480 is not in `ShellScreens.All()` | The contract: "The Remove confirmation states the rebuild price". It does, but it can be unseen | minor | Visibility of a price the player must know before a destructive action | **No code change needed for acceptance**. Optional polish: put the rebuild price directly after the refund sentence. NV on the smallest supported class P1/L1 at 200 % (check C-4) |
| R-12 | Prestige threshold copy | all | After a sale the panel and banner say "Needed to sell 11.0M, next 12.1M" (`batch-c/10`). Compact numbers floor and the exact value is attached | Contract met | polish | none | No action (listed as Verified for the contract row). Pointer to Industry is R-01 |
| R-13 | Session-log section (T340 follow-up) | notch phone, 200 %, real touch / screen reader | Not checkable here | Contract: section below the fold at 200 % is a known limit; "Last export" selectable on Windows; rotation cancels the Clear dialog | minor | FR-040-044 UI | **NV** (checks C-5 to C-7). Windows desktop evidence exists (`batch-b/13`, `batch-c/14`) |
| R-14 | Dev marker accessible name (#370) | all, screen reader | `DevToolsOverlay.MarkerAccessibleName` plus tooltip only; Unity 6000.6 UI Toolkit has no per-element accessibility API | "Accelerated, factor N" announced | minor (dev builds only) | none for release | **Named later feature F004 (accessibility-mapping adapter)**; no F005 action |
| R-15 | DevTools overlay vs slot row 1 when accelerated | D1, 100 %, pointer (`batch-c/01, 04`) | The marker and buttons cover the top of row 1 including "+" targets; click-through itself is closed by T366. Placement tests prove no overlap with `factory-ore-vein` only | Contract says the marker "does not overlap safe areas or the HUD" (it does not); slot overlap is not in the contract | polish (dev only) | none | Owner already said dev watermark needs no action (#417 decision F10). **No action**; if wanted, F004 moves it into the top strip |
| R-16 | Accessible names are tooltip-only | all, screen reader | `SellName`, `StepName`, ore vein `AccessibleName` and goals names are strings and tooltips; Unity 6000.6 cannot expose them | Accessible name with state (contract invariant, UX-1) | minor (platform limit) | FR-038 and VEIN-8 are met only within what the platform exposes. The state-bearing strings are present, tested and stable, so an adapter can map them | **Named later feature F004** (adapter hook when an accessibility API exists). Accept the limit explicitly (owner question Q6) |
| R-17 | Ore vein on short landscape (#386 b) | L2 844x390, 200 %, touch | About 100-120 pt of slot area above the vein with the zoom column beside it | A play-feel check, not a contract rule | minor | none | **F004 / view owner**; NV (check C-8) |
| R-18 | Ore vein progress refresh (#386 c) | any, with a second miner | Progress is refreshed per counted click or run change, not per frame; a future auto-miner could leave it stale | Progress matches the run | polish (latent) | none today (no auto-mining in F005) | **Named later feature** that adds auto-mining; record as a precondition |
| R-19 | Shell chrome in captures (#384, #386 d) | P1, P2, 640x480 at 100/200 % | Doubled tab bar, HUD strip and zoom column in RenderTexture captures. QA #386 confirmed by live layout rects that this is a stale-frame capture artifact | Single chrome | none | none | **Closed: capture artifact.** Still tick check C-1 on the first device run |
| R-20 | Machine panel fix buttons: focus orphaned (#489 1) | all classes, keyboard / switch / screen reader | `RenderBlockers` clears and rebuilds the guidance rows (`MachinePanelPresenter.cs:345-390`) with no focus handling; after a fix button is activated, or when guidance clears on selection or phase change, the focused element is removed. `MachinePanelGuidanceTests` has no focus case | Shared rule: "After a control disappears, focus moves to a visible sibling" | **major** (keyboard and switch users lose their place on every fix) | FR-038 shared rule; QA-12 "focus on first, middle, last control" | **Defect in F005 now (small)**: before `blockers.Clear()`, if focus is inside it, `FocusWhenReady` the panel's stable next control (the Upgrade or Remove button) and add the focus cases to `MachinePanelGuidanceTests` |
| R-21 | Guidance appears / disappears silently (#489 2) | all, screen reader | No live region; state names are tooltips (see R-16) | Changes announced | minor (platform limit) | none | **F004 adapter** with R-16 |
| R-22 | Pre-stall warning (#489 3) | machine panel | A Running machine shows no guidance until the cycle actually blocks (T365 deliberate). Players can miss that a full store is about to stall | design question | n/a | none | **Owner question Q3** (my view: the HUD store bar already warns; a one-line "Store nearly full" on the machine would be enough, as a later feature) |
| R-23 | Generic "Build a Miner" hint (C-F4) | D1 and P1 (`batch-c/02, 17`) | Under the correct reason "Missing Iron Ore: auto-sell is selling it ..." the presenter adds "Build a Miner to make Iron Ore." (`MachinePanelPresenter.cs:428`) for every `BuildProducer` action, even with four Miners standing. The action is emitted by the Core diagnosis; the view prints it unconditionally | The hint appears only when no producer exists | minor | SC-009; T365 residual | **Defect in F005 now**: suppress the hint when a producer of that resource is owned (view-side filter or Core action condition; Architect decides which side) |
| R-24 | Industry wording (C-F5) | D1 (`batch-c/20`) | "Speed x1.22 on all machines per level" states the first level strength though later levels decay (`ResearchEffectCopy.cs:14-18`) | The next level's own gain | minor | FR-021 accuracy | **Defect in F005 now (copy)**: "Next level: speed x1.19"; or drop "per level" for decaying upgrades |
| R-25 | Research detail card (C-F6) | D1 (`batch-c/21`) | After a rejected purchase two warning triangles show for one message: `detailReason` (`ResearchPanel.cs:366`) and `feedback` (`:152-153`) carry the same `RunRejectionCopy`; the second line can be empty beside its icon | One message, one icon | minor | SC-009 | **Defect in F005 now (small)**: show the feedback only when it differs from the standing reason; hide its icon when the text is empty (verify with a capture, check C-9) |
| R-26 | Research graph clipping (A-F10) | D1, 100 % | Cards at the right edge are cut ("Deep Vei", "Gear Mark") with "Scroll sideways to see more Research." (`ResearchPanel.uss:105-135`, a horizontal scroller by design) | Cards fully visible or an obvious scroll affordance | minor | none | **Owner already decided: F004 UI polish** (#417 decision F10). Closed for F005 |
| R-27 | USS `last-child` (F-B9) | all | `ResearchPanel.uss:125` `.research-column:last-child` is an unsupported pseudo class in Unity 6000.6: logged once per launch as `Unknown pseudo class "last-child"`. The 40 px right margin of the last column stays | Clean console (completion gate) | minor | Completion gate "no relevant errors" (T355) | **Defect in F005 now (trivial)**: drop the rule or set a `research-column--last` class in C# |
| R-28 | Test coverage of QA-12 touch/pointer | n/a | See end of section 2: no touch/pointer cases in the contracts, prestige, session-log, Industry and Overdrive suites | QA-12: every row checked with touch and pointer | minor | QA evidence gap, not a behaviour defect | **Owner question Q4**: add one `ShellDeviceProfile.Touch/Pointer` case each (cheap) or accept the existing shell-level exact-value tests (`RunDeviceProfileTests`, `RunKeyboardFocusTests`) as covering the shared mechanism |
| R-29 | Build price card affordances (#384) | all | "Capped" line has no icon; the price value button looks actionable but only shows the exact detail; `value.limit` "Cost: too large" is untested and likely unreachable (`BuildSheetPresenter.cs:315`) | Consistent state icons; non-actionable look for an inspect-only value | polish | none | F004 restyle; no F005 action |
| R-30 | Store-full banner (F-B6) | D1 (`batch-b/12`) | Banner says "The store is full. Machines that cannot add to it wait." (`InventoryRatesPanel.cs:99`) and nothing about what to do | A hint toward selling or auto-selling the item filling the store | minor (information, explained well) | none | **Named later feature**: pair with the C-F2 auto-sell design (owner question Q7). The cheap copy fix "Sell or auto-sell the biggest stock below" can go in with R-06 if the owner wants it now |
| R-31 | Welcome-back text (F-B8) | D1 (`batch-b/01`) | Headline "No production while away." (idle, `OfflineSummaryPanel.cs:358`) is followed by "Production ran for 6 h 24 min at 80 % efficiency" (`:385-388`), because durations are always printed | One consistent story | minor | US7 clarity | **Defect in F005 now (copy)**: for the idle headline say "Your factory was blocked while you were away" and word the efficiency line "Machines were able to work for ..."; or skip the efficiency line when idle. Not re-observed after T365 (T349 covers a long absence) |
| R-32 | Goal line only in Market | all | Known limitation in the contract | decided in contract | polish | none | F004 decides |

Counts by the Severity column (32 rows): **blocker 0, major 3 (R-01, R-06, R-20), minor 21, polish 6, none 1 (R-19, capture artifact), n/a 1 (R-22, design question)**. R-09 is Fixed and R-12 is Verified; both are counted as polish.

## 4. Disposition of every follow-up on #417

Status is at `297ccfb`. "Fixed" cites the code that shows it.

| Follow-up (source) | Status | Evidence | Disposition |
|---|---|---|---|
| Dev marker accessible name tooltip-only (#370) | Open, platform limit | R-14 | F004 adapter |
| Safe-area inset for the overlay (#370 / #414) | Fixed for placement (T340: top-left below the strip, placement tests at QA-12 classes); notch placement NV | wiki `f005-batch-b-ui-and-android` | Check C-6 on a notched device |
| Currency wording "$" vs "Cash" (#384) | Open | R-02 | Owner question Q2, recommend "$" for amounts |
| Tab bar / zoom doubled in captures (#384) | Closed: capture artifact (QA #386 live rects) | R-19 | Tick check C-1 on device |
| Remove dialog scroll at 640x480 / 200 % (#384) | Open, minor | R-11 | No code change needed; optional reorder |
| Capped line icon, price button affordance, `value.limit` (#384) | Open, polish | R-29 | F004 |
| Core test gaps (overflow-to-cap, prestige `PriceCapped`, ECO-7 under a changing discount, `RebuildPrice` doc note) | Open (Architect items, not UI) | #384 follow-up | Not a UI/UX item; hand to T351/T353 Architect review |
| Sell controls stack full width (#382 a) | Open | R-04 | Amend contract wording; F004 wide layout |
| Taller rows at 200 %, focus at viewport edge after sale (#382 b) | Open, NV | R-05 | Check C-3 |
| `FocusWhenReady` retry abort (#382 c) | **Fixed** (T362; wiki: "FocusWhenReady aborts once focus moved (F8)") | `f005-batch-b-ui-and-android.md` | Closed |
| Locked rows hidden vs "Locked" (#382 d) | **Fixed** (T362): locked row shows `UnlockCopy.LockedLine` "Locked: unlocks after ..." | `InventoryRatesPanel.cs:261`, test `ALockedResourceShowsALockedRowWithItsUnlockCondition...` | Closed |
| "Need N" = step vs shortfall (#382 d) | **Fixed**: `Need (step - units) more` (`InventoryRatesPanel.Sell.cs:102`) | test `ShortfallTextIsTheStepMinusTheStockForEveryStep...` | Closed |
| Cycling Amount vs segmented selector (#382 d) | Open, T352 decides | R-03 | Recommend segmented in F004 |
| F9 labels "Store", "Overdrive", "Cash" (#417 decision) | **Fixed** (T362); seen in `batch-b/02` | wiki, `playtest-agent-batch-b.md` A-F9 | Closed |
| Accessible names tooltip-only (#386 a) | Open, platform limit | R-16 | F004 adapter; owner accepts limit (Q6) |
| Vein area at 844x390 / 200 % (#386 b) | Open, NV | R-17 | Check C-8; F004 |
| Vein progress per click, not per frame (#386 c) | Open, latent | R-18 | Precondition for auto-mining |
| T340 device-only checks (screen-reader tooltips, real touch on "Last export", 200 % session-log card, notch, rotation cancels Clear) | NV | R-13 | Checks C-5 to C-7 |
| F-B1 ended / used offers pinned, red triangle on a normal state | Open | R-06 | Defect in F005 now |
| F-B7 duplicate "Plate contract" titles, vanishing item rewards | Open | R-08 | Titles now; item copy waits for `perRunCap` (Q5) |
| #466 name the RP gain on the bonus card | **Fixed** (reward line "12 RP") | R-09 | Closed; ratios stay with T345 |
| F-B8 welcome-back text | Open | R-31 | Copy fix now |
| F-B6 store-full banner has no sell hint | Open | R-30 | Q7 |
| A-F10 Research cards clip at 1280x720 | Open, owner-deferred | R-26 | F004 (decided) |
| F-B9 `Unknown pseudo class "last-child"` | Open | R-27 | Trivial fix now |
| T365 focus orphaned after fix button / guidance clears | Open | R-20 | Defect in F005 now |
| T365 guidance not announced | Open, platform limit | R-21 | F004 adapter |
| T365 pre-stall warning question | Open | R-22 | Q3 |
| C-F3 bonus card used / ends / used flip | Open | R-07 | Defect in F005 now |
| C-F4 generic "Build a Miner" hint | Open | R-23 | Defect in F005 now |
| C-F5 Industry "per level" | Open | R-24 | Copy fix now |
| C-F6 two red triangles | Open | R-25 | Small fix now |
| C-F8 locked-Contracts text | Open | R-10 | Copy fix now |
| Overlay covers slot row 1 when accelerated (dev only) | Open, dev only | R-15 | No action (owner decision) |
| C-F1 Industry hidden (batch C, assigned to T352) | Open | R-01 | Defect in F005 now (Q1) |

## 5. Not verifiable without a device or capture

Run once on the T351 emulator/device pass or the human playtest. Each item names the class, scale and input.

| ID | Check | How |
|---|---|---|
| C-1 | Chrome is single, not doubled, at P1 360x640 and P2 390x844, 100 % and 200 % | Non-batchmode Game view or device screenshot after two frames of settle; compare tab bar and zoom column counts |
| C-2 | 7 mm touch targets: a touch profile with known DPI raises the scale (`ShellPanelScale`); Sell, Amount, Accept, fix buttons, tabs measure 44 pt after the raise | Device, ruler or `adb shell wm density` plus a screenshot; confirm with `Configuration.fontScale` 2.0 |
| C-3 | After a sale on P1/L1 at 200 %, the focus ring on Sell is fully inside the viewport (R-05) | Device with a keyboard or switch; sell on the last Inventory row |
| C-4 | Remove dialog at P1/L1, 200 %: is the rebuild-price sentence visible without scrolling, and is a scroll cue shown (R-11) | Device or 640x360 window with text scale 200 % |
| C-5 | Screen reader announces session-log tooltips, status and reason lines (R-13, R-16) | TalkBack on Android; expect that nothing is announced (platform limit) and record it |
| C-6 | DevTools overlay and the session-log section on a notched phone, rotated both ways, over a populated factory (first slot row) | Development build on a device with a cutout |
| C-7 | Rotation cancels an open Clear confirmation; real touch on the inert "Last export" line; desktop text selection and copy of the path | Device (rotation, touch); Windows player (selection) |
| C-8 | Mine reachable and comfortable on L2 844x390 at 200 % with the zoom column beside it (R-17) | Device or window at 844x390, text scale 200 %, hold Mine with real touch |
| C-9 | Second warning triangle with an empty line under the Research detail card (R-25) | Research tab, buy without enough RP, screenshot |
| C-10 | Real touch hold on Mine: no peek, second finger ends the hold, hold survives no focus loss (`OreVeinPanelTests` simulates; only a Windows real-mouse hold is evidenced) | Device, two fingers |
| C-11 | 200 % text of Offers, Industry cards, Prestige panel on a 360x640 phone with real `fontScale` (the tests set the factor directly) | Device with system font size at maximum |

## 6. Owner questions

| # | Question | My recommendation |
|---|---|---|
| Q1 | How should the player learn Industry exists (C-F1): a line and a button on the Prestige panel and banner, an IP dot on the Research tab, and/or moving the section above the graph? | Panel line with a "Spend IP" button plus the tab dot, in F005 (R-01). Moving the section is not needed if the button scrolls to it |
| Q2 | Currency wording: "$" or "Cash" for amounts? | "$" for amounts, "Cash" only as the balance label |
| Q3 | Should a Running machine warn before it stalls (low input, nearly full store)? | Later feature; one line on the machine, not a banner |
| Q4 | QA-12 touch/pointer cells for contracts, prestige, session log, Industry, Overdrive: add one test each, or accept the shared shell-level tests? | Add the five cases (cheap), or accept and record it |
| Q5 | What does the Offers card say when an Item reward is withheld by the per-run limit, while `perRunCap` (#387) is open? | "Item reward limit reached this run" until #387 is decided |
| Q6 | Accept that accessible names are tooltip-only until Unity exposes an accessibility API (R-14, R-16, R-21), with the strings kept stable for an F004 adapter? | Accept |
| Q7 | Store-full banner hint (R-30) now, as copy only, or together with the C-F2 auto-sell design? | Together with C-F2 |
| Q8 | Which "defect in F005 now" findings go into one fix task before T353-T356: R-01, R-06, R-07, R-08 (titles), R-10, R-20, R-23, R-24, R-25, R-27, R-31? | One bounded UI-polish task; all are local to ContractsPanel, ResearchPanel, MachinePanelPresenter, OfflineSummaryPanel, PrestigePresenter and one USS rule, with the existing suites extended. Re-review afterwards by the same reviewer (the author may not self-approve) |

## 7. Verdict: accept with follow-ups

Reasons:

- Every F005 control in `ui-surfaces-f005.md` is implemented with its command, soft-disabled reason, exact-value attachment and state-bearing accessible name. The Play Mode suites assert layout at all eight screen classes and at 100 % and 200 % for the control rows, plus keyboard focus and empty/error states, and the Windows real-input checks (Mine hold, DevTools click-through) are evidenced. No blocker was found.
- Three majors remain and none is a regression of a contract invariant: the hidden Industry reward (R-01), the dead and alarm-styled offers list (R-06), and orphaned focus after a machine fix button (R-20). Each is small and local, so I recommend one bounded fix task (Q8) before the closeout tasks T353-T356, not a rejection.
- The rest are wording, polish and platform limits already owned by F004 or by an open owner decision.
- Cells that need a device (real touch, notch, screen reader, OS font scale, capture) are not verified here and are listed in section 5. A human playtest should treat C-2, C-8, C-10 and C-11 as the gating ones for SC-009 on phones.
- Because this review read tests rather than running them, it does not replace the completion gate's own test runs.
