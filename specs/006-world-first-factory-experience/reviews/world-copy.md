# World-facing copy inventory

**World Bible**: `F006-WB v0.1` ([docs/world/world-bible-v0.1.md](../../../docs/world/world-bible-v0.1.md)), Frozen v0.1, approved by the owner 2026-11-11 (OD-9a, T504). The hub label choice is OD-9b (T509).
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

"Factory Sale", "Contracts", "Market", "Research", "Overdrive", "Items" and "Mining" stay as they are (naming rule 2). They belong to earlier features and are listed so SC-013 finds no unowned world-facing string.

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

Owner of every row: the World Designer (tone and vocabulary), UI/UX (fit), the assigned worker (wiring). Strings from F001 to F005 that no F006 surface changed stay under their feature and are checked only against the vocabulary rules (class L). Verdicts follow [world-bible-conformance.md](world-bible-conformance.md) Part 2. "CHANGE" rows list the exact new text there; they stay open until applied.

| Row | String (shipped) | File:line | Surface | Verdict |
|---|---|---|---|---|
| N-01 | Milestone reached. | `UI/MilestoneCopy.cs:35` | Banner fallback on a copy gap only | CONFORMS |
| N-02 | Short on an input | `UI/MachineCard.cs:74` | Card cause line, no limiting resource | CONFORMS |
| N-03 | Store is full | `UI/MachineCard.cs:71` | Card cause line | CONFORMS_WITH_CONDITIONS (CF-1) |
| N-04 | The finished cycle cannot be delivered yet | `UI/MachineCard.cs:72` | Card cause line | CONFORMS |
| N-05 | Switch it on in Details | `UI/MachineCard.cs:73` | Card cause line | CONFORMS |
| N-06 | Cause detail: Auto-sell is selling it; No machine makes it; The store is full, so its makers cannot deliver; Its makers are not keeping up | `UI/MachineCard.cs:88-91` | Card cause line (B-4) | CHANGE: the last two become `Store full, so makers cannot deliver` and `Makers cannot keep up`; shown as a visible second line |
| N-07 | Close (visible) and its accessible name | `UI/MachineCard.cs:77,193,202`; `UI/RunShellDocument.cs:2935` | Card and sheet Close | CHANGE (B-6): accessible names `Close machine card` and `Close panel` |
| N-08 | Details | `UI/MachineCard.cs:78` | Card button | CONFORMS |
| N-09 | That machine is gone. | `UI/MachineCard.cs:75` | Notice | CONFORMS |
| N-10 | Confirm; Cancel | `UI/Panels/BuildCard.cs:14,15` | Build card | CONFORMS |
| N-11 | No machine types are unlocked yet. | `UI/Panels/BuildCard.cs:16` | Build card empty state | CONFORMS |
| N-12 | You cannot afford a machine yet. Earn Cash, then build here. | `UI/Panels/BuildCard.cs:17` | Build card empty state | CONFORMS |
| N-13 | That location is no longer free. | `UI/Panels/BuildCard.cs:18` | Build card notice | CONFORMS |
| N-14 | That location is not unlocked yet. | `UI/Panels/BuildCard.cs:19` | Build card notice | CONFORMS |
| N-15 | Selected: {type} at location {n}; Build at location {n} | `UI/Panels/BuildCard.cs:22,25` | Build card | CONFORMS (the model for CF-2) |
| N-16 | Need ${x} more | `UI/Panels/BuildCard.cs:28` | Build card | CONFORMS |
| N-17 | Cost: ...; Affordable; {n} free location(s) are outlined. | `UI/Panels/BuildCard.cs:314,335,363` | Build card rows and tooltip | CONFORMS |
| N-18 | Price changed to ${x}. You were not charged. | `UI/Panels/BuildCard.cs:589` | Build card notice | CONFORMS |
| N-19 | Something went wrong; The Office could not read the factory. Try again.; Nothing here yet | `UI/Panels/HubPanel.cs:419-427` | Hub error and empty states | CONFORMS |
| N-20 | No system is available right now. | `UI/Panels/HubPanel.cs:428` | Hub empty state | CHANGE (CF-4): `Nothing is available in the Office right now.` |
| N-21 | • Attention; Needs attention; Unavailable: {reason} | `UI/Panels/HubPanel.cs:511,648,652` | Hub rows | CONFORMS |
| N-22 | Hub status lines: Store {n}% full; {n} owned; RP {n}; Ready; On, m:ss left; Cooling down; Locked; Not ready yet: needs Factory Value {x}; Display and comfort; Not available right now; ready to deliver; Nothing ready to deliver | `UI/Panels/HubPanel.cs:545-637` | Hub rows | CONFORMS_WITH_CONDITIONS (the "Store" term, CF-1); "This system could not be opened" at `:637` is CF-4 |
| N-23 | Hub card title "Prestige" | `UI/Panels/HubPanel.cs:39` | Hub row | CHANGE (CF-3): `Factory Sale` |
| N-24 | Show zoom buttons | `UI/Panels/Settings/SettingsPanelPresenter.cs:24` | Settings (P2-F20) | CONFORMS |
| N-25 | Adds zoom buttons to the factory view. Pinch zoom still works. | `UI/Panels/Settings/SettingsPanelPresenter.cs:25` | Settings | CONFORMS |

### Rows already owned above, verified in the shipped source

| Row | Shipped at | Verdict |
|---|---|---|
| H-02 Office | `UI/Panels/HubPanel.cs:30` | CONFORMS; always shown beside the glyph in captures c07, c17, c23. |
| B-01, B-01s, B-02, B-03, B-04 | `UI/MilestoneCopy.cs:38-42` | CONFORMS. B-01s is confirmed and promoted from draft to `F006-WB v0.1` copy (T547, C-56). |
| T537 hub accessible name "Office, N systems need attention" | `UI/RunShellDocument.Hub.cs:81` | CHANGE (CF-4): `Office, N need attention` (`Office, 1 needs attention`). |
| T537 attention accessible name "Short on <resource>, N machines, M more causes. Select the first one." | `UI/PersistentLayer.cs:184-187` | CONFORMS. |
| T537 "Overdrive is not unlocked yet" and "Locked: not unlocked yet." | `UI/Panels/HubPanel.cs:610`; `UI/UnlockCopy.cs:111` | CHANGE (CF-5): `Overdrive is locked.` and `Locked.` |
| T555 hub Contracts "Locked: unlocks after <condition>" | `UI/Panels/HubPanel.cs:477,563,564` | CONFORMS (captured, c07). |
| T537 "Contracts are locked", Items empty state, "Nothing here yet" | `UI/Panels/ScaffoldEmpty.cs:13` and the panels | CONFORMS. |
| G-01 to G-03, D-01 to D-03, S-01, S-02 | no build string | Not shipped; unchanged; no text in the world. |

### Vocabulary changes owed (not new rows)

"Slot" to "location" on 15 player-visible sites, and the "Store" term decision, are listed with exact old and new text in Part 2 of the conformance review (CF-2 and CF-1). Class L strings that stay: Cash, Store (pending CF-1), Contracts, Market, Research, Industry, Overdrive, Items, Mining, Mine, Max rate, Welcome back, Settings, Reduced motion, and the F001 to F005 notice, rejection and offline-summary sentences not listed in CF-2 to CF-5.
