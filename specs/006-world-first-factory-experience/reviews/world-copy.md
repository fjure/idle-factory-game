# World-facing copy inventory

**World Bible**: `F006-WB v0.1` ([docs/world/world-bible-v0.1.md](../../../docs/world/world-bible-v0.1.md)), Frozen v0.1, approved by the owner 2026-11-11 (OD-9a, T504). The hub label choice is OD-9b (T509).
**Amended by**: `F006-WB v0.2` ([docs/world/world-bible-v0.2.md](../../../docs/world/world-bible-v0.2.md)), MINOR, approved by the owner 2026-11-19 (CF-1, T547): "Store" is the capacity term in player text and "Depot" is the world structure; no string is renamed. Rows in sections 1 to 6 cite v0.1 and stay valid; section 7 cites v0.2.
**Spec**: FR-047, SC-013; research R22, R14, R15, R23.
**Ownership**: tone and wording by the World Designer; length and fit by UI/UX; wiring by the assigned worker.

## Fit method and caveat

At 200 % text scale body text (14 pt) becomes about 28 pt (`--run-fs-body`; wiki page run-shell-device-profile-and-text-scale). Estimate used: average glyph width 0.55 em, so about 15 pt per character at 200 %. A phone-portrait safe width of about 360 pt holds about 24 characters per line; a chip or button with padding holds about 20. All widths below are **estimates from character counts, not measurements**. Existing HUD strings such as "Factory Sale available" (22 chars) and "Contracts !" already wrap or crowd the compact strip at 200 %, so persistent labels here are kept short. UI/UX must measure each string on its real element through `ShellRig` at 200 % for every `ShellScreens.All()` class.

## 1. Site name and tagline

| Id | String | Owner | WB version | Where shown | Chars | Fit at 200 % |
|---|---|---|---|---|---|---|
| S-01 | Meridian Works | World Designer (tone), UI/UX (fit) | v0.1 | Title surface, about line | 14 | One line (about 210 pt). Fits. |
| S-02 | Build the line. Feed the depot. | World Designer, UI/UX | v0.1 | Title surface only, not the HUD | 31 | Two lines. Acceptable on the title surface; not for any persistent label. |

## 2. Hub label candidates (OD-9b chose H-02 Office; none is "Systems")

| Id | String | Owner | WB version | Chars | Fit at 200 % (chip with icon) | Note |
|---|---|---|---|---|---|---|
| H-01 | Works | World Designer, UI/UX | v0.1 | 5 | About 75 pt. Fits beside an icon. | Matches the site name. Short. Not chosen (OD-9b). |
| H-02 | Office | World Designer, UI/UX | v0.1 | 6 | About 90 pt. Fits. | Reads as management and contracts. **Chosen by the owner (OD-9b, 2026-11-11).** |
| H-03 | Operations | World Designer, UI/UX | v0.1 | 10 | About 150 pt. Tight beside an icon in a compact strip. | Clearest meaning, longest. Wrap risk at 200 %. |

All three pass the forbidden-term rule (no "Systems", no bare "Menu").

## 3. Milestone banner texts (R14; at most 2.5 s, non-blocking)

`{n}` and `{k}` are integers. A banner states the change and the unlock, with no hype words.

| Id | String | Trigger | Owner | WB | Chars (n=12, k=2) | Fit at 200 % |
|---|---|---|---|---|---|---|
| B-01 | Level {n}. {k} new locations open. | Factory level up with unlocks | World Designer | v0.1 | 31 | Two lines. Acceptable for a banner; must not truncate. |
| B-01s | Level {n}. 1 new location opens. | Factory level up with exactly one new location (provisional, T540 fix round; UI/UX U1; World Designer to confirm in T547) | Developer | draft | 29 | Two lines at most. Fits. |
| B-02 | Level {n} reached. | Factory level up, no unlock | World Designer | v0.1 | 17 | One line. Fits. |
| B-03 | New run begins. | Prestige completion (Factory Sale) | World Designer | v0.1 | 15 | One line. Fits. |
| B-04 | Deck extended. | Stage change (R15), with B-01 or B-02 | World Designer | v0.1 | 14 | One line. Fits. |

Offline milestones appear only in the offline summary (R14), with the same strings and no banner.

## 4. Stage names (R15; edges railing, wall, gantry)

| Id | String | Stage | Owner | WB | Chars | Fit at 200 % |
|---|---|---|---|---|---|---|
| G-01 | Open deck | Early (railing) | World Designer | v0.1 | 9 | One line. Fits. |
| G-02 | Walled yard | Middle (wall) | World Designer | v0.1 | 11 | One line. Fits. |
| G-03 | Gantry line | Late (gantry) | World Designer | v0.1 | 11 | One line. Fits. |

Shown only if UI/UX decides to show stages on the machine card or details. Stages reset with prestige (R15).

## 5. Depot and vein names (only if shown)

| Id | String | Owner | WB | Where | Chars | Fit at 200 % |
|---|---|---|---|---|---|---|
| D-01 | Depot | World Designer | v0.1 | Output-destination line on the machine card | 5 | Fits. |
| D-02 | Ore vein | World Designer | v0.1 | Accessible name and optional caption of the vein marker | 8 | Fits. |
| D-03 | Trunk | World Designer | v0.1 | Not shown in F006; accessible-name use only | 5 | n/a |

## 6. Existing strings kept (no change in v0.1)

"Factory Sale", "Contracts", "Market", "Research", "Overdrive", "Items", "Mining" and (from v0.2, CF-1) "Store" stay as they are (naming rule 2). They belong to earlier features and are listed so SC-013 finds no unowned world-facing string.

## Self-check read-throughs (producer self-check, not independent approval)

**Art Director lens (self-check).** The bible and strings set no palette, shape, perspective or material. "Dark base" is a mood and ground constraint only; the Art Director decides the exact value. Stage names map to the R15 edge sequence without specifying art. Open note for the Art Director: confirm that railing, wall, gantry is a fair visual reading of Open deck, Walled yard, Gantry line.

**UI/UX lens (self-check).** Persistent labels are at most 10 characters. Banners are at most 31 characters and may take two lines at 200 %; they stay static under reduced motion (R14). No string depends on color, an icon alone or a tooltip. Widths are unmeasured estimates; a measured 200 % pass on every screen class is still required. The tagline is kept off persistent surfaces because of its length.

Neither read-through replaces the independent Art Director or UI/UX review.

## Owner decision OD-9a (T504, 2026-11-11)

The owner approved World Bible v0.1 and froze it as `F006-WB v0.1`. QA PASS on 72f0ecf (#679, merged in #721).

- Site name "Meridian Works" and tagline "Build the line. Feed the depot.": approved as written.
- Stage names Open deck, Walled yard, Gantry line: approved as written.
- Vocabulary: "plate" and "bay" are removed from the tone vocabulary, because "Plate" already names an item ("Iron Plate", "Plate contract"). A machine stands on a **location**, matching the spec. The bible gains naming rule 6, and §1 now reads "machines sit at their locations".
- B-01 now reads "Level {n}. {k} new locations open." (31 characters at n=12, k=2; two lines at 200 %, must not truncate).
- Character counts corrected: S-02 is 31 characters.
- Everything else: approved and frozen. The hub label (H-01 to H-03) is still open and is decided in OD-9b (T509).
- Still open: the Art Director confirms "dark base" against the Art Bible v1.0.x ground value in the T506 briefs, and UI/UX measures the 200 % fit on real elements.

## Owner decision OD-9b (T509, 2026-11-11)

The hub label is **H-02 "Office"**. The decision and the chosen direction (A, with B's wall as the stage-2 candidate) are recorded in [direction.md](direction.md).
- UI/UX condition C3 applies: test "Office" at 200 % text in the compact strip for phone portrait and the 640x360 class.

## T537 copy changes for the World Designer (T547)

Player-facing strings changed in T537 (#710). All are in code; T547 should confirm them against naming rules:

- Contracts locked state: title "Contracts are locked" (was the generic "Nothing here yet" above a lock message).
- Items empty state: message "Earn them from contracts." under the title "Nothing here yet" (was "No Items yet — earn them from contracts.", which repeated the title).
- Overdrive card with no unlock condition: "Overdrive is not unlocked yet" (was "Overdrive unlocks after it unlocks.").
- Locked resource line with no condition: "Locked: not unlocked yet."
- Production attention indicator accessible name: "Short on <resource>, N machines, M more causes. Select the first one." (the visible text is only the count and "+N").
- Hub entry: visible word "Office" (unchanged), accessible name "Office, N systems need attention".

## T555 copy changes for the World Designer (T547)

Player-facing strings changed in T555 (#760), the gate-B fixes. Both are in code; T547 should confirm them against naming rules:

- Hub Contracts row while no contract offer is unlocked and none is active: "Locked: unlocks after <condition>" (the condition in the shared `UnlockCopy` wording, for example "Locked: unlocks after you produce Iron Plate or Gear or Iron Bar"); with no offers in the catalog: "Locked: unlocks after you build more of the factory". It replaces "Nothing ready to deliver", which stays for an unlocked, idle Contracts system. The row's accessible name carries the status, so it names the lock.
- Research view: no new copy. The existing hint "Scroll sideways to see more Research." moved above the graph, and the graph edge cue is an arrow glyph (no words).

## 7. Shipped-string ownership and disposition (T547, final, `b44b87d`)

**World Bible**: strings are checked against `F006-WB v0.2` (MINOR, owner 2026-11-19, CF-1: "Store" is the capacity term, "Depot" the structure; see [world-bible-v0.2.md](../../../docs/world/world-bible-v0.2.md) section 8). Owner of every row: the World Designer (tone and vocabulary), UI/UX (fit), the assigned worker (wiring). Verdicts follow [world-bible-conformance.md](world-bible-conformance.md) Part 2; "CHANGE" rows list the exact new text there and stay open until T560 (#776) applies them.

**Sweep method and exact claim.** Scope: every `*.cs` and `*.uxml` file under `Assets/IdleFactory/Unity/UI/` (including `Panels/` and `Panels/Settings/`) and `Assets/IdleFactory/Unity/Presentation/` (including `World/`), including every `*Copy.cs`. The method and its mechanical result are in "Mechanical coverage proof" at the end of this section. **Claim: every player-visible string literal in that scope that the extraction finds is covered by exactly one N-row, L-row or CF row, or is class D (not player-visible).** F006 and F006-adjacent surfaces are N-rows (strings named by file and line); F001 to F005 panels that no F006 task changed are L-rows (one row per file, term-swept, not tone-read); class D is listed. Limits are in the proof.

### N-rows: F006 and F006-adjacent strings

Rows N-01 to N-25 are as first recorded in T547; N-26 to N-46 were added by the fix rounds (N-42 is split into N-42a to N-42i, lines 46-749 of `OfflineSummaryPanel.cs`).

| Row | String (shipped) | File:line | Surface | Verdict |
|---|---|---|---|---|
| N-01 | Milestone reached. | `UI/MilestoneCopy.cs:33` | Banner fallback on a copy gap only | CONFORMS |
| N-02 | Short on an input | `UI/MachineCard.cs:74` | Card cause line, no limiting resource | CONFORMS |
| N-03 | Store is full | `UI/MachineCard.cs:71` | Card cause line | CONFORMS (v0.2) |
| N-04 | The finished cycle cannot be delivered yet | `UI/MachineCard.cs:72` | Card cause line | CONFORMS |
| N-05 | Switch it on in Details | `UI/MachineCard.cs:73` | Card cause line | CONFORMS |
| N-06 | Cause detail: Auto-sell is selling it; No machine makes it; The store is full, so its makers cannot deliver; Its makers are not keeping up | `UI/MachineCard.cs:88-91` | Card cause line (B-4) | CHANGE: the last two become `Store full, so makers cannot deliver` and `Makers cannot keep up`; shown as a visible second line |
| N-07 | Close (visible) and its accessible name | `UI/MachineCard.cs:77,193,202`; `UI/RunShellDocument.cs:2935` | Card and sheet Close | CHANGE (B-6): accessible names `Close machine card` and `Close panel` |
| N-08 | Details | `UI/MachineCard.cs:78` | Card button | CONFORMS |
| N-09 | That machine is gone. | `UI/MachineCard.cs:75` | Notice | CONFORMS |
| N-10 | Confirm; Cancel | `UI/Panels/BuildCard.cs:14,15`; `UI/Panels/BuildCard.uxml:9,10` | Build card | CONFORMS |
| N-11 | No machine types are unlocked yet. | `UI/Panels/BuildCard.cs:16` | Build card empty state | CONFORMS |
| N-12 | You cannot afford a machine yet. Earn Cash, then build here. | `UI/Panels/BuildCard.cs:17` | Build card empty state | CONFORMS |
| N-13 | That location is no longer free. | `UI/Panels/BuildCard.cs:18` | Build card notice | CONFORMS |
| N-14 | That location is not unlocked yet. | `UI/Panels/BuildCard.cs:19` | Build card notice | CONFORMS |
| N-15 | Selected: {type} at location {n}; Build at location {n} | `UI/Panels/BuildCard.cs:22,25` | Build card | CONFORMS (the model for CF-2) |
| N-16 | Need ${x} more | `UI/Panels/BuildCard.cs:28` | Build card | CONFORMS |
| N-17 | Cost: ...; Affordable; {n} free location(s) are outlined. | `UI/Panels/BuildCard.cs:314,335,348,363,393-432` (range extended at the T560 re-read) | Build card rows and tooltips | CONFORMS |
| N-18 | Price changed to ${x}. You were not charged. | `UI/Panels/BuildCard.cs:589` | Build card notice | CONFORMS |
| N-19 | Something went wrong; The Office could not read the factory. Try again.; Nothing here yet | `UI/Panels/HubPanel.cs:419-427` | Hub error and empty states | CONFORMS |
| N-20 | No system is available right now. | `UI/Panels/HubPanel.cs:428` | Hub empty state | CHANGE (CF-4): `Nothing is available in the Office right now.` |
| N-21 | • Attention; Needs attention; Unavailable: {reason} | `UI/Panels/HubPanel.cs:511,648,652` | Hub rows | CONFORMS |
| N-22 | Hub status lines: Store {n}% full; {n} owned; RP {n}; Ready; On, m:ss left; Cooling down; Locked; Not ready yet: needs Factory Value {x}; Display and comfort; Not available right now; ready to deliver; Nothing ready to deliver | `UI/Panels/HubPanel.cs:545-637` | Hub rows | CONFORMS (v0.2 "Store" term); "This system could not be opened" at `:637` is CF-4 |
| N-23 | Hub card title "Prestige"; sheet tab name "Prestige" | `UI/Panels/HubPanel.cs:39`; `UI/RunShellDocument.cs:38` | Hub row | CHANGE (CF-3): `Factory Sale` |
| N-24 | Show zoom buttons | `UI/Panels/Settings/SettingsPanelPresenter.cs:24` | Settings (P2-F20) | CONFORMS |
| N-25 | Adds zoom buttons to the factory view. Pinch zoom still works. | `UI/Panels/Settings/SettingsPanelPresenter.cs:25` | Settings | CONFORMS |
| N-26 | Locations. Arrow keys move between locations, Enter opens one. | `UI/RunShellDocument.cs:213` | Accessible name of the keyboard stop for the world locations | CONFORMS (says "location"; the model for CF-2) |
| N-27 | Mine; Max rate; Store full (the vein reason) | `UI/OreVeinCopy.cs:13,16,19`; `UI/FactoryRun.uxml:58,63` | Vein control caption, rate-cap cue and full-store reason | CONFORMS ("Mine" is the verb of the existing term Mining, rule 2) |
| N-28 | Accessible name "Mine {resource}, +{yield} per click" and its suffixes ", store full", ", limit reached", ", unavailable"; "Close the dialog first."; "{n} tenths of the next Ore" | `UI/OreVeinCopy.cs:22,63-73`; `UI/OreVeinControl.cs:407`; `UI/RunOreVeinSource.cs:28` (fallback resource label "Ore") | Vein accessible name, reasons and progress tooltip | CONFORMS (v0.2: "store" is the capacity term; "Ore" is a vocabulary word) |
| N-29 | Expansion; Storage; Upgrade storage; Factory; Expand factory; Capacity; units; Capacity now and next; Next level costs; Level {n} of {m}; {value} (max); the exact-value tooltips "... next level ..." and "..., the maximum level"; There is no next level.; Storage is now level {n}: capacity {x}.; The factory is now level {n}: {x} slots. | `UI/Panels/ExpansionPanel.cs:57,59,60,64,108,127,128,157,163,169,170,172,187-189`; `UI/Panels/ExpansionPanel.uxml:2` | Expansion section of the Market sheet | CONFORMS except the three "Slots"/"slots" strings at `:109`, `:127`, `:189` (CF-2). "Storage" is the older sibling of "Store"; not forbidden, recorded in v0.2 section 8. |
| N-30 | Nothing stored yet; The store is full. Machines that cannot add to it wait.; {n} units in the store; {a} of {b} units stored; Store {a} / {b}; Inventory and rates; row labels Stock, Net rate, Sells for, "{x} /min", "per minute (production ..., use ...)", "{price} each", "per unit; the whole stock is worth ...", Auto-sell: On/Off | `UI/Panels/InventoryRatesPanel.cs:20,100,109,284-286,320,324-335,348,349`; `UI/Panels/InventoryRatesPanel.uxml:2,8,10` | Market sheet inventory | CONFORMS (v0.2) |
| N-31 | HUD Store pill: caption "Store"; value "0%", "N%" or "FULL"; tooltip "Store full" or "Store N percent used"; exact value "Store: a of b ..." or "Store: no capacity" | `UI/FactoryRun.uxml:18,23`; `UI/RunShellDocument.cs:918-920,2953-2955` | Top strip | CONFORMS (v0.2) |
| N-32 | Other static HUD text: Cash; Settings (tooltip); + and − (zoom); Factory (default sheet title); Close; Office (label and tooltip); Overdrive; Locked; Confirm; Cancel (dialog) | `UI/FactoryRun.uxml:10,31,50,51,68,72,81,83,89,90,99,107,108` | HUD and dialog | CONFORMS |
| N-33 | Shell notices and names: Cancelled by rotation; Cash {value} tooltip; Overdrive captions (Locked, On m:ss, Cool m:ss, Ready) and "Overdrive is ready."; Not available right now.; This panel is not available yet.; Confirm; Cancel; Settings; Details · Location {n} (sheet title); Cash: {exact}; Zoom in / Zoom out and their limit names and reasons | `UI/RunShellDocument.cs:30,903,952-983,1600,2056,2088,2105,2174,2175,2951,2961-2979`; `UI/ShellPanelRegistry.cs:201-204` (dialog defaults Confirm, Cancel) | Shell | CONFORMS |
| N-34 | Overdrive is on for {t}.; Locked; Ready; Cool {t}; and the reason sentences built from `RunRejectionCopy` | `UI/Panels/OverdriveControl.cs:171-200` | Overdrive control | CONFORMS |
| N-35 | Location stop labels "Slot {n}", ", locked", ", locked until factory level {n}", ", {machine} level {n}, {state word}", ", empty"; state words inactive, missing input, storage full, waiting, paused, running | `Presentation/World/SlotVisualModel.cs:110-137,209-214` | World cell accessible labels | CHANGE at `:110` only (CF-2: `Location {n}`); the state words conform. The badge text cues OFF, IN!, FULL, WAIT, PAUS, RUN at `:209-214` have no reader outside the model (`BadgeText` is read nowhere else) and are not player-visible. |
| N-36 | Card remainder: Running; Waiting; Paused; Switched off; Maximum level; Machine; {label} - level {n}; Upgrade: ${x}; Need ${x}; Need ${x} more; Upgrade; Details › | `UI/MachineCard.cs:67-70,76,211,362,391-406`; `UI/MachineCard.uxml:12,16` | Machine card | CONFORMS |
| N-37 | Build (card title); older build sheet: Build {type}; Owned; Cost; Makes; Uses; Capped: the price does not rise further.; Unlocks at factory level {n}.; Unlocks with a Research.; Unlocks once {r} has been produced.; Price changed to ... ; No machine types unlocked. | `UI/Panels/BuildCard.cs:136`; `UI/Panels/BuildSheetPresenter.cs:43,68-80,225,325-360,380,441`; `UI/Panels/BuildSheetPanel.uxml:3` | Build card and the older sheet | CONFORMS |
| N-38 | Retry; i; Industry (sheet title) | `UI/Panels/HubPanel.cs:305`; `UI/Panels/HubPanel.uxml:4,8`; `UI/RunShellDocument.Hub.cs:143` | Hub state controls | CONFORMS |
| N-39 | Short on {resource} | `Presentation/World/AttentionModel.cs:216` | Card cause line and strip cause text | CONFORMS |
| N-40 | it unlocks; you produce {r}; the factory reaches level {n}; you research {x}; its requirement is met; Locked: unlocks after {condition}.; Locked: not unlocked yet. | `UI/UnlockCopy.cs:19-28,43-111` | Shared unlock wording | CONFORMS except `:111` no-condition string (CF-5: `Locked.`). The implementer of CF-5 confirms that `:19` cannot produce "unlocks after it unlocks". |
| N-41 | Rejection sentences (Not enough Cash.; The store is full. Sell some stock or expand storage.; and the rest) and the Overdrive lock sentences | `UI/RunRejectionCopy.cs:17-65` | Notices and tooltips | CONFORMS (v0.2); `:17` and `:18` are CF-2 |
| N-42a | Offline constants: Welcome back; No production while away.; Continue; Retry; the three "no offline production was credited" messages; The return could not be completed; the blocked-return hint; The return still could not be completed. | `UI/Panels/OfflineSummaryPanel.cs:46-65` | Offline summary | CONFORMS |
| N-42b | Headline and lead lines: Here is what your factory did while you were away.; The offline credit could not be applied. Your factory is as you left it. | `UI/Panels/OfflineSummaryPanel.cs:357-390` | Offline summary | CONFORMS |
| N-42c | Time and economy: Time; Away for {t}.; Credited: {t}.; {t} at {n} % efficiency lines ("Machines could work for ... but finished no cycle." and "Production ran for ... at ..."); Cash and stock; Cash +/-${x} (from ... to ...); Sales income; Sold; Produced; Used; Completed cycles | `UI/Panels/OfflineSummaryPanel.cs:402-459` | Offline summary | CONFORMS |
| N-42d | Stock lines "{resource}: {a} to {b} ({change})" | `UI/Panels/OfflineSummaryPanel.cs:466-468` | Offline summary | CONFORMS |
| N-42e | While you were away; Item effect ended: {item} on {target}. | `UI/Panels/OfflineSummaryPanel.cs:482-488` | Offline summary | CONFORMS |
| N-42f | Overdrive ended.; Overdrive is ready again.; Contract expired: {contract}. No reward, no penalty.; Contract still active: {contract}.; Contract auto-delivered: {amount}.{reward}; Production contract completed: {amount} produced.{reward}; Newly unlocked: {names}. | `UI/Panels/OfflineSummaryPanel.cs:494-527` | Offline summary events | CONFORMS (plain, present or past tense, no hype; "Newly unlocked" names the change, in the spirit of rule 5) |
| N-42g | Milestones (section title); the milestone lines are `MilestoneCopy.Full`, rows B-01 to B-04 | `UI/Panels/OfflineSummaryPanel.cs:538-541` | Offline summary milestones | CONFORMS |
| N-42h | Limits (section title); Offline credit is capped at {t}; time beyond the cap earned nothing.; {n} cycle(s) made fewer units than a full batch because inputs or storage ran short.; "{Slot} {n} {machine} is waiting: the store is full / an input is missing." | `UI/Panels/OfflineSummaryPanel.cs:552-579` | Offline summary limits | CONFORMS (v0.2) except "Slot {n}" at `:570` (CF-2) |
| N-42i | Helpers: durations (0 s, less than 1 s, h, min, s); item lists "{n} {resource}"; " Reward: +${x}, +{n} RP, {item} x{n}."; "deliver {n} {resource}"; the factory; {resource} contract; the section and line builders at `:597` (class names only) | `UI/Panels/OfflineSummaryPanel.cs:597-749` (lines 571-694 and 695-749 read line by line) | Offline summary helpers | CONFORMS |
| N-43 | Reduced motion (group label, Follow system, On, Off, (selected), Not saved. Choose again., status lines); Stops looping machine animation. Nothing else changes.; Settings | `UI/Panels/Settings/SettingsPanelPresenter.cs:21-32,104,211-235`; `UI/Panels/Settings/SettingsPanel.uxml:4,7` | Settings | CONFORMS ("system" in "Follow system" means the operating system, not a game noun) |
| N-44 | Goal line: Factory Sale available; All goals reached; The goal line is not available for this run.; Next goal ...; Exact value; Goal reached ...; contract feed: Auto-delivered ...; Produced ...: contract complete; +$, RP, Item(s) | `UI/Panels/GoalsLinePanel.cs:29-221`; `UI/ContractsShellFeed.cs:179-213` | Goal line and contract notices | CONFORMS |
| N-45 | Back; Loading; Something went wrong; Retry; Notice: {text}, Warning: {text}, Error: {text}; Dismiss; Dismiss notice; Unavailable: {reason} | `UI/PanelScaffold.cs:95,200,209,225,228,229,278,296,297`; `UI/PanelScaffold.uxml:4` | Panel scaffold | CONFORMS |
| N-46 | The factory is starting.; HUD unavailable | `UI/CoreGameFoundation.uxml:6`; `Presentation/FactoryHudDocument.cs:101,236` | Startup HUD status and its fallback label | CONFORMS |
| N-47 | Selected | `UI/Panels/BuildCard.cs:18,393,412-413,432` (T560) | Build card type row status; the spoken form is the reason text | CONFORMS |

### Rows already owned above, verified in the shipped source

| Row | Shipped at | Verdict |
|---|---|---|
| H-02 Office | `UI/Panels/HubPanel.cs:30` | CONFORMS; shown beside the glyph in captures c07, c17, c23. |
| B-01, B-01s, B-02, B-03, B-04 | `UI/MilestoneCopy.cs:37-41` | CONFORMS. B-01s is confirmed and promoted from draft to `F006-WB v0.1` copy (T547, C-56); valid under v0.2. |
| T537 hub accessible name "Office, N systems need attention" | `UI/RunShellDocument.Hub.cs:81` | CHANGE (CF-4): `Office, N need attention` (`Office, 1 needs attention`). |
| T537 attention accessible name "Short on <resource>, N machines, M more causes. Select the first one." | `UI/PersistentLayer.cs:184-187` | CONFORMS. |
| T537 "Overdrive is not unlocked yet" and "Locked: not unlocked yet." | `UI/Panels/HubPanel.cs:610`; `UI/UnlockCopy.cs:111` | CHANGE (CF-5): `Overdrive is locked.` and `Locked.` |
| T555 hub Contracts "Locked: unlocks after <condition>" | `UI/Panels/HubPanel.cs:477,563,564` | CONFORMS (captured, c07). |
| T537 "Contracts are locked", Items empty state, "Nothing here yet" | `UI/Panels/ScaffoldEmpty.cs:13` and the panels | CONFORMS. |
| G-01 to G-03, D-01 to D-03, S-01, S-02 | no build string | Not shipped; unchanged; no text in the world. |

### L-rows: F001 to F005 panels that no F006 task changed

Owner: the feature of origin (F001 to F005) for wording; the World Designer for the vocabulary rules only. Each file was term-swept (slot, store, storage, plate, bay, system, warehouse, hub, menu, hype, exclamation). Findings are the only ones listed; nothing else matched.

| Row | Files | Sweep result |
|---|---|---|
| L-01 | `UI/Panels/ContractsPanel.cs` | Clean. "Plate contract" (`:794`) uses "Plate" as the item word, which rule 6 allows. |
| L-02 | `UI/Panels/PrestigePresenter.cs`, `UI/Panels/PrestigePanel.uxml` | Clean. Title "Factory Sale" is the model for CF-3. "Stored resources" and "Storage and factory upgrades" (`:415,419`) are "storage" sibling wording, not forbidden. |
| L-03 | `UI/Panels/ItemsPanel.cs` | Clean. |
| L-04 | `UI/Panels/ResearchPanel.cs`, `UI/Panels/ResearchEffectCopy.cs` | Clean. "starting storage level" (`:27`) is "storage" sibling wording. |
| L-05 | `UI/Panels/StartupStatusPanel.cs`, `UI/Panels/StartupStatusPanel.Surface.cs` | Clean of slot and hype. "Checkpoint storage" is a save-store term; "first store level" (`:807`) uses Store as the capacity term (v0.2). |
| L-06 | `UI/Panels/SessionLogPanel.cs` | Clean. "storage access of the app" (`:32`) means device storage permission, not the game Store. |
| L-07 | `UI/Panels/IndustryPanel.cs` | Clean. "storage level" (`:156`) is "storage" sibling wording. |
| L-08 | `UI/Panels/PrioritiesPanel.cs`, `UI/Panels/PrioritiesPanel.uxml` | `:172` "Slot {n}" is CF-2. "store space" (`:70`, uxml `:3`) uses Store as the capacity term (v0.2). |
| L-09 | `UI/Panels/MarketPanelHost.cs`, `MarketPanelParts` | Clean. |
| L-10 | `UI/Panels/InventoryRatesPanel.Sell.cs` | Clean. |
| L-11 | `UI/Panels/MachinePanelPresenter.cs`, `UI/Panels/MachinePanel.uxml` | `MachinePanel.uxml:3` "There is no machine in this slot." is CF-2. "Storage is full" (`:365,584`) and "the store was full" (`:389`) are Store/storage wording (v0.2). |
| L-12 | `UI/ShellPanelRegistry.cs`, `UI/Panels/ScaffoldEmpty.cs`, `UI/OreVeinControl.cs`, `UI/RunOreVeinSource.cs`, `UI/MilestoneBanner.cs`, `UI/QuantityFormatter.cs` | Only identifiers, formats and the strings already in N-rows. |

### Class D: not player-visible (no ownership row needed)

Added at the T560 re-read (QA W-1, C-71): `Presentation/FactoryCameraController.cs`, `Presentation/GridWorldMapper.cs`, `Presentation/StateCueSet.cs` (exception messages and key constants) and `Presentation/World/AttentionMarkerView.cs` (GameObject names, exception message, sprite key) are class D, whole file.

Developer diagnostics and identifiers, outside tone and vocabulary rules: HUD and shell setup errors (`Presentation/FactoryHudDocument.cs:51,68,74,93`; `UI/RunShellDocument.cs:416-507` `Fail(...)`), validator "problems" text (`FactoryWorldLayout.cs`, `WorldCompositionSettings.cs`), log lines (`CameraScaleModel.cs`, `ShellPanelScale.cs`, `ShellDeviceProfile.cs`), `ToString` overrides (`DevelopmentStage.cs`, `MilestoneWatcher.cs`, `BuildingLocationId.cs`, `DeckPlan.cs`), GameObject and sorting-layer names, sprite keys (`PresentationSpriteCatalog.cs`, `F006-*`, `F003-*`), USS class names, reason codes (`slot.occupied`), and the badge text cues in N-35.

### Vocabulary changes owed (not new rows)

"Slot" to "location" on 15 player-visible sites (CF-2), CF-3, CF-4, CF-5, B-4 and B-6 are listed with exact old and new text in Part 2 of the conformance review and are applied by T560 (#776). "Store" needs no change (v0.2). Class L strings that stay: Cash, Store, Contracts, Market, Research, Industry, Overdrive, Items, Mining, Mine, Max rate, Welcome back, Settings, Reduced motion, and the F001 to F005 notice, rejection and offline-summary sentences not listed in CF-2 to CF-5.

### Mechanical coverage proof (T547 fix round 2)

**Method.** (1) Extraction: `cand.py`, a script run from the repository root, lists in each `.cs` and `.uxml` file under `Assets/IdleFactory/Unity/UI` and `Presentation` every non-comment line that holds a string literal with at least three letters that is not an identifier-shaped value (kebab or dotted lower-case, `F00x-` keys, bare constants), skipping lines of `Debug`, `throw`, `Fail(`, validator `problems.Add`, `nameof`, class-list calls and element lookups, plus every UXML `text=` or `tooltip=` attribute with letters. It found **888 lines in 72 files**. (2) Citation check: the file:line citations of the N-rows above and of the CF tables in Part 2 of the conformance review were parsed into line ranges, and every extracted line in an N-covered file was tested against them. (3) The lines outside every cited range (the class D lines below) were each read and are element names, USS class names, asset paths or developer diagnostics, never player text. (4) L-files and class D files are covered as whole files by the rule stated in their rows. **Result: 356 lines are inside cited N/CF ranges, 406 are L-row whole-file lines, 52 are class D whole-file lines, 74 are class D lines inside N-covered files; 0 lines are unaccounted for.** The extraction cannot see a literal that is assembled from constants or built with `string.Format` unless a row names the constant; rows name `MilestoneCopy`, `OreVeinCopy`, `MachineCardCopy`, `BuildCardCopy`, `UnlockCopy`, `RunRejectionCopy` and the Settings constants explicitly. The L-rows are term-swept, not read line by line for tone.

| File | Literal lines found | Coverage |
|---|---|---|
| `Presentation/CameraScaleModel.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/FactoryHudDocument.cs` | 13 | 2 inside cited N/CF row ranges; class D lines: 33, 34, 35, 36, 37, 51, 272, 274, 276, 278, 280 (element, class and asset names, paths, dev diagnostics) |
| `Presentation/PresentationSpriteCatalog.cs` | 8 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/ShellDeviceProfile.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/ShellPanelScale.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/ShellTextScale.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/AttentionModel.cs` | 1 | 1 inside cited N/CF row ranges |
| `Presentation/World/BuildingLocationId.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/DeckPlan.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/DepositView.cs` | 4 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/DepotLinkView.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/DevelopmentStage.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/EffectPoolView.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/FactoryDeckView.cs` | 6 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/FactoryWorldLayout.cs` | 2 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/LatticeLayoutBuilder.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/MilestoneWatcher.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/SlotGridView.cs` | 1 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/SlotRenderers.cs` | 8 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `Presentation/World/SlotVisualModel.cs` | 8 | 8 inside cited N/CF row ranges |
| `Presentation/World/WorldCompositionSettings.cs` | 7 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `UI/ContractsShellFeed.cs` | 3 | 3 inside cited N/CF row ranges |
| `UI/CoreGameFoundation.uxml` | 1 | 1 inside cited N/CF row ranges |
| `UI/FactoryRun.uxml` | 15 | 15 inside cited N/CF row ranges |
| `UI/MachineCard.cs` | 23 | 22 inside cited N/CF row ranges; class D lines: 249 (element, class and asset names, paths, dev diagnostics) |
| `UI/MachineCard.uxml` | 2 | 2 inside cited N/CF row ranges |
| `UI/MilestoneBanner.cs` | 2 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `UI/MilestoneCopy.cs` | 7 | 6 inside cited N/CF row ranges; class D lines: 93 (element, class and asset names, paths, dev diagnostics) |
| `UI/OreVeinControl.cs` | 4 | 1 inside cited N/CF row ranges; class D lines: 50, 51, 52 (element, class and asset names, paths, dev diagnostics) |
| `UI/OreVeinCopy.cs` | 8 | 8 inside cited N/CF row ranges |
| `UI/PanelScaffold.cs` | 15 | 9 inside cited N/CF row ranges; class D lines: 35, 37, 43, 44, 45, 46 (element, class and asset names, paths, dev diagnostics) |
| `UI/PanelScaffold.uxml` | 1 | 1 inside cited N/CF row ranges |
| `UI/Panels/BuildCard.cs` | 21 | 16 inside cited N/CF row ranges; class D lines: 506, 507, 508, 509, 510 (element, class and asset names, paths, dev diagnostics) |
| `UI/Panels/BuildCard.uxml` | 2 | 2 inside cited N/CF row ranges |
| `UI/Panels/BuildSheetPanel.uxml` | 1 | 1 inside cited N/CF row ranges |
| `UI/Panels/BuildSheetPresenter.cs` | 30 | 17 inside cited N/CF row ranges; class D lines: 118, 119, 120, 324, 372, 373, 374, 375, 376, 377, 378, 379, 381 (element, class and asset names, paths, dev diagnostics) |
| `UI/Panels/ContractsPanel.cs` | 74 | L-row L-01, whole file |
| `UI/Panels/ExpansionPanel.cs` | 21 | 14 inside cited N/CF row ranges; class D lines: 115, 117, 125, 129, 133, 137, 147 (element, class and asset names, paths, dev diagnostics) |
| `UI/Panels/ExpansionPanel.uxml` | 1 | 1 inside cited N/CF row ranges |
| `UI/Panels/GoalsLinePanel.cs` | 13 | 13 inside cited N/CF row ranges |
| `UI/Panels/HubPanel.cs` | 35 | 29 inside cited N/CF row ranges; class D lines: 488, 490, 494, 505, 507, 518 (element, class and asset names, paths, dev diagnostics) |
| `UI/Panels/HubPanel.uxml` | 2 | 2 inside cited N/CF row ranges |
| `UI/Panels/IndustryPanel.cs` | 20 | L-row L-07, whole file |
| `UI/Panels/InventoryRatesPanel.Sell.cs` | 27 | L-row L-10, whole file |
| `UI/Panels/InventoryRatesPanel.cs` | 24 | 15 inside cited N/CF row ranges; class D lines: 254, 257, 260, 272, 274, 290, 296, 299, 310 (element, class and asset names, paths, dev diagnostics) |
| `UI/Panels/InventoryRatesPanel.uxml` | 3 | 3 inside cited N/CF row ranges |
| `UI/Panels/ItemsPanel.cs` | 38 | L-row L-03, whole file |
| `UI/Panels/MachinePanel.uxml` | 4 | L-row L-11, whole file |
| `UI/Panels/MachinePanelPresenter.cs` | 51 | L-row L-11, whole file |
| `UI/Panels/MarketPanelHost.cs` | 1 | L-row L-09, whole file |
| `UI/Panels/OfflineSummaryPanel.cs` | 54 | 54 inside cited N/CF row ranges |
| `UI/Panels/OverdriveControl.cs` | 6 | 6 inside cited N/CF row ranges |
| `UI/Panels/PrestigePanel.uxml` | 10 | L-row L-02, whole file |
| `UI/Panels/PrestigePresenter.cs` | 54 | L-row L-02, whole file |
| `UI/Panels/PrioritiesPanel.cs` | 17 | L-row L-08, whole file |
| `UI/Panels/PrioritiesPanel.uxml` | 3 | L-row L-08, whole file |
| `UI/Panels/ResearchEffectCopy.cs` | 12 | L-row L-04, whole file |
| `UI/Panels/ResearchPanel.cs` | 20 | L-row L-04, whole file |
| `UI/Panels/ScaffoldEmpty.cs` | 1 | 1 inside cited N/CF row ranges |
| `UI/Panels/SessionLogPanel.cs` | 29 | L-row L-06, whole file |
| `UI/Panels/Settings/SettingsPanel.uxml` | 2 | 2 inside cited N/CF row ranges |
| `UI/Panels/Settings/SettingsPanelPresenter.cs` | 18 | 12 inside cited N/CF row ranges; class D lines: 84, 86, 88, 159, 161, 165 (element, class and asset names, paths, dev diagnostics) |
| `UI/Panels/StartupStatusPanel.Surface.cs` | 6 | L-row L-05, whole file |
| `UI/Panels/StartupStatusPanel.cs` | 40 | L-row L-05, whole file |
| `UI/PersistentLayer.cs` | 3 | 3 inside cited N/CF row ranges |
| `UI/RunOreVeinSource.cs` | 1 | 1 inside cited N/CF row ranges |
| `UI/RunRejectionCopy.cs` | 37 | 37 inside cited N/CF row ranges |
| `UI/RunShellDocument.Hub.cs` | 2 | 2 inside cited N/CF row ranges |
| `UI/RunShellDocument.cs` | 42 | 36 inside cited N/CF row ranges; class D lines: 199, 400, 1032, 1036, 1040, 1045 (element, class and asset names, paths, dev diagnostics) |
| `UI/ScaffoldScope.cs` | 3 | class D, whole file (identifiers, logs, validator text, sorting-layer and object names) |
| `UI/ShellPanelRegistry.cs` | 3 | 3 inside cited N/CF row ranges |
| `UI/UnlockCopy.cs` | 7 | 7 inside cited N/CF row ranges |
