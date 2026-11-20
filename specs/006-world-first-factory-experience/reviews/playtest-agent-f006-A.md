# Play-Tester batch-A report (T543, issue #715)

**Status: proposal for the owner.** Two channels: the deterministic balance runner (reviewed policy r2, unchanged) and a short player session in a Windows development build. Neither is a human playtest. This report approves no content revision or balance number and turns no feel statement into a threshold. Every finding is open for owner decision (AGENTS.md; `docs/agents/roles-and-reviews.md`, Play-Tester). Feel statements are marked **[inference]**: an agent cannot feel boredom or delight. Measured values name their source. The independent UI/UX review of the same batch is a separate report (`uiux-f006-A.md`) and is not covered here.

| Item | Value | Source |
|---|---|---|
| Build commit | `810507dbc61821fe4e620901901d11e9f3b672f2` (detached HEAD at `origin/main`, merge of PR #749) | `git rev-parse HEAD` in `.maqa-worktrees/f006-p3a-playtest` |
| Batch under test | T532 (attention and presentation models), T533 (PanelScaffold), T534 (docked MachineCard, PR #748), plus T526 (F006 art import, K4 cue tail, PR #749) and T538 (display preferences v2). The F006 world art is imported but not placed in the scene (T527) | task brief, `tasks.md` |
| `contentRevision` | 3 (session-log header `"contentRevision":3`, `"platformClass":"desktop"`, `"buildKind":"development"`) | `segment-0.jsonl` row 0 |
| Channel A | `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull` (policy r2, 13 strategies x 5 offsets x up to 5 runs) | section 1 |
| Channel B | win64 Mono development player with `IDLEFACTORY_DEV`, built by `PlatformBuildCommands.BuildWindows64DevelopmentDevTools` (`StandaloneWindows64 result Succeeded, 0 errors, 2 warnings`, 36 s), windowed 1280x720 (client area confirmed) and 390x844 (client area confirmed). Input by user32 `SetCursorPos` and `mouse_event`/`keybd_event`, screenshots by `CopyFromScreen` of the client area only. Development save folder `IdleFactory-Dev` (the real player save was never used) | `test-results/f006-p3a/build.log` (untracked, ignored) |
| Strategy (B) | hand-played "tinkerer": hand mining and four Miners, Ore auto-sell on then off, factory level 2 and 3, Smelter, Press, Assembler, Plate auto-sell. A manual player, not calibrated to the owner's pace | section 2 |
| Simulated / wall time | A: 21,600 s limit per run, 265 run-plays, matrix wall 111.2 s (committed file: 116.1 s). B: landscape 14:32:23 to 14:39:26 (7 min), portrait 14:39:46 to 14:41:11, landscape again 14:41:36 to 14:44:04. About 10.5 min at x1 in total, about 1.8 min at x10 (about 18 game minutes). **Short of the 10 to 15 minute target at x1 only by a few minutes; no long horizon was run in B** (batch A changes no economy) | file times, my wall clock |
| Strategy runs for longer horizons | Not run in the player. The runner covers them (section 1) | - |
| Evidence | 20 PNG (game client area only, 1.1 MB) in `playtest-agent-f006-A/` next to this file; `build.log`, `player1-3.log`, `balance.out` stay untracked under `test-results/f006-p3a/` | section 5 |
| Leases | `unity,emulator` held by `issue-715` (not touched or released by me). The emulator and `adb` were not used. UnityMCP was not used | `resource-lease.ps1 -Status` |

## 1. Channel A: strategy runs, economy unchanged

- Result line: `UNITY_TEST_PASS suite=EditMode tests=7 passed=7 skipped=0` (`test-results/unity/20261113T132937764Z/EditMode-results.xml`).
- `git diff` of the regenerated `specs/005-economy-contracts/reviews/strategy-baseline-r3.md` against `HEAD`: **only the `Build commit` line (`c299e8483 (+ uncommitted changes)` committed, `810507dbc` now), the per-strategy wall-time cells (ms per run-play), the "Measured" line (111.1 s against 116.0 s of history time, 419 ms against 438 ms mean per run-play, worst strategy `casual` 771 ms against 790 ms) and the two projection lines (2.1 min against 2.2 min, ceiling 30.7 min against 32.1 min) differ.** Every measured number and every gate is identical, so batch A changed no economy value. I confirmed this by filtering the diff for every line that does not contain `ms (offset`.
- Gates: all asserted gates pass, none REPORTED (`strategy-baseline-r3.md` section 4): `conservation` 0 violations over 65 histories, `eco-3` chain run 1 to 5 at 7,148 / 4,588 / 3,808 / 3,608 / 3,408 s, `ratio-bounds` effective ratio 1.498 to 1.800, `start-affordability` smallest margin 65 Cash, `single-lever` none faster than the chain (ore-only 2.44 / 2.30 / 2.07 / 1.91 / 1.81), `chain-within-6h` slowest 7,156 s, `ip-4x` ceiling 213 IP against highest 20 IP (10.7x), `chain-plus-mining` advantage 2.2 % / 0.8 % / 0.8 % / 0.6 % / -3.5 %.
- Restored with `git checkout -- ProjectSettings/ specs/005-economy-contracts/reviews/strategy-baseline-r3.md Assets/IdleFactory/Rendering/UniversalRenderPipelineGlobalSettings.asset`; no untracked `Assets/UI Toolkit/` appeared. `git status --short` is empty apart from this report and its screenshots.
- Not covered by the runner: the card, the camera, the notices and the settings; those are channel B.

## 2. Channel B: player session

### 2.1 Timeline (wall time, my clock; the session log carries no per-row time, as in F005 C-F7)

| Wall | Event | Evidence |
|---|---|---|
| 14:32:23 | Fresh launch at 1280x720. Cash 37, four empty slots, eight locked slots, DevTools overlay top left, Mine bottom right, side panel "Factory" with the old tab row, zoom +/- bottom left. A Windows firewall dialog for the development player appeared on first launch; I declined it ("Abbrechen") | `00-fresh-start.png` |
| about 14:32:50 | First Miner built (30 Cash). The compact card opens docked in the side panel, "Miner - level 1 / Running", `Upgrade: $36 / Need $29`, "Need $29 more", `Details ›` | `02-first-miner.png` |
| 14:33 to 14:35 | Ore auto-sell turned on in Market, Miners 2, 3, 4 built (35, 40, 46 Cash), hand mining in holds of 15 to 25 s (about 70 s in total) | session log purchase rows |
| about 14:36:40 | Factory level 2 bought for 480 Cash, **about 4 min 15 s after launch at x1** (F005 batch C scripted run: 3 min 26 s). **[inference]** my pace is slower because I read screenshots between actions; this is not a pace calibration | `Get-Date` after the purchase 14:36:55 |
| 14:37 | Smelter built: card shows "Paused", cause line "Short on Iron Ore" (Ore auto-sell is on, four Miners stand); Details shows the full blocker with its fix; the fix ran and the card went to "Running" | `20-smelter-built.png`, `22-smelter-details.png` |
| 14:38 | Press built, switched off in Details and read back on the card; Settings sheet opened, Reduced motion set to On, Show zoom buttons set to On | `27-card-of-off-press.png`, `29-settings-toggled.png` |
| 14:39 to 14:41 | Portrait 390x844: welcome-back dialog ("Away for 23 s"), card as a bottom band, Details sheet, shortfall state, empty-world tap | `42-` to `49-...png` |
| 14:41 to 14:44 | Landscape again: Ore and Plate sold, Plate auto-sell on, x10 for about 100 s, factory level 3 bought (3,000 Cash), Assembler built (card: "Short on Iron Plate" because Plate auto-sell sells the input), store at 99 % gave "Store is full" on a Miner | `70-assembler-card.png`, `71-miner-store-full.png` |

### 2.2 Measurements

| Item | Value | Source |
|---|---|---|
| Session log | 7 header, 6 lifecycle, 16 purchase, 6 mining, 6 bottleneck, 2 offlineReturn, 2 sale, 2 sample rows in the last segment | `segment-0.jsonl` (T538 session folder `IdleFactory-Dev.t543-session`) |
| Player logs | No `Exception` in `player1.log`, `player2.log`, `player3.log`. One warning on the first launch: `[IdleFactory.Preferences] preferences.display.invalid` (finding A-F7) | `player1.log` line 46 |
| Display preferences | After the two Settings changes the file is `{"version":2,"reducedMotion":"on","showZoomButtons":true}` (57 bytes, `Preferences/display-v2.json`); it was read back correctly after a relaunch (the Settings sheet and the zoom column agree) | file contents |
| Card, landscape 1280x720 | The card sits in the existing 360 px side panel; the world column and all machine positions do not move when the card opens or closes. Interactions: select a machine and Upgrade = **2** (SC-003 target at most 2); cause of a stopped machine on the card = **0 taps** at category level, full cause plus fix = **2 taps** (select, Details); Remove = select, Details, Remove, confirm = **4** interactions | `06-`, `20-`, `22-`, `09-...png` |
| Card, portrait 390x844 | Compact card is a bottom band above the tab bar: y 683 to 802 = 119 px = **14 %** of the window height for a running machine, y 667 to 802 = 135 px = **16 %** with the shortfall line (FR-016: at most 30 % at 100 to 140 % text). Details sheet y 505 to 802 = 297 px = **35 %** (the FR-016 cap applies to the compact card; no cap is stated for Details). The world is reframed **up** by about 60 px when the card opens (grid row 1 from y 322 to 262), by about 25 px more when the card grows with the shortfall line, and is restored **exactly** (row 1 back at y 322) after an empty-world tap. The selected machine stayed fully visible above the band in every portrait capture | `42-`, `46-`, `48-`, `49-...png` |
| Upgrade in place | Click on `Upgrade: $36` raised the machine to level 2, the button became `Upgrade: $43` in place, the card header showed a light-blue highlight for about 1 s (visible at 250 ms, gone at 1.5 s), the focus ring stayed on the Upgrade button, the card did not close | `06-upgraded-in-place.png` |
| Shortfall | Disabled button reads `Upgrade: $61 / Need $23` and a separate yellow line "Need $23 more" under it | `07-upgrade-shortfall.png` |
| Details parity with the F005 panel | Level "4 of 100", status, Makes and Uses rates, cycle progress %, next-level cost and rate, Upgrade, Switch off/on, Remove, the blocker text with its fix button ("Missing Iron Ore: auto-sell is selling it before this machine can use it (needs 2, has 0)" with "Stop auto-selling Iron Ore"). Not seen (no such machine state reached): Item/boost entry points | `08-`, `22-...png` |
| Remove | Remove button in Details opens the shared confirmation ("Remove Miner? You get $88 back. Factory Value changes by -88 ... costs $46 ... cycle progress are lost."); focus is on "Keep machine"; Escape cancels it. After "Remove" the notice "Removed Miner. Refunded $88." appears, the empty slot is selected and the build list opens | `09-`, `13a-...png` |
| Reduced motion | With Reduced motion On, the upgrade highlight on the card is a static block for about 1 s and the miners keep their static glint (I cannot tell loop from still in single frames). The "Stops looping machine animation" copy is unverified by me | `30a/30b` (not kept) |
| Zoom/pan | At minimum zoom the 12-slot grid fits and a drag does not pan. At maximum zoom a drag pans until about one grid cell of empty margin is visible. No depot or vein exists in the scene (T527 not done), so the ViewBounds change toward them is **not testable** | `31c-`, `33c-...png` |

## 3. Observations on the batch-A surface (what a player meets)

1. **The docked card is clear on the first view.** Name, level, state in words, and one big Upgrade button with the cost; the shortfall shows inside the button. **[inference]** The first Upgrade tap needs no explanation.
2. **Upgrading from the card is the most direct path I saw in F006 so far** (2 interactions, no panel swap). The factory stays in view in landscape because the card lives in the existing side panel.
3. **The cause line is shorter than the F005 panel, and that is mostly good**: no generic hints any more ("Build a Miner to make Iron Ore" on a Smelter paused by auto-sell, F005 C-F4, is gone). The card says what is short, with the resource icon; the reason (auto-sell) is in Details.
4. **"Switch off" and "Switch on" live only in Details.** The card of a switched-off machine says "Switch it on in Details" with an info icon.
5. **A removed machine is handled well** (notice and refund line), but the notice bar shifts the whole world column and the speed row down by about 34 px for the notice's duration (13a, and the locked-slot notice in my session).
6. **Settings sheet** has two controls: Reduced motion (Follow system, On, Off) and Show zoom buttons (a checkbox with an "On/Off" tag). Both persisted across a relaunch.
7. **Portrait is readable at 390x844**; **[inference]** the bottom-band card fits the thumb zone, the Details sheet covers a third of the screen but leaves the selected machine visible.

## 4. Findings

Severity: Medium = a spec acceptance scenario or SC is not met on this build, Low = rough edge or copy, Info = limit of this run or a positive result. "Disposition" is my proposal; the owner decides.

| ID | Observed behaviour | Evidence | Affected FR/SC | Severity | Proposed disposition |
|---|---|---|---|---|---|
| A-F1 | **Escape does nothing for the docked card or the Details view**; it only cancelled the Remove confirmation. Two further Escape presses left "Details · Location 4" open, and one Escape on the compact card left it open. In the landscape build an **empty-world tap does not close the Details view** (the card closes on an empty-world tap, the Details view does not; a tap on another machine switches to its card). The only ways out of Details are the in-panel Close (portrait, back to the card), the Factory tab, or selecting another machine. Code search: no `Escape` handling outside the offline summary, startup and priorities panels | `10b-after-esc2.png` (Details still open after two Escapes); empty tap in Details: `11a` (not kept, Details unchanged, Cash 240) | US3 scenario 5, FR-017, FR-045 | **Medium** | Escape/Back belongs to T535 (`BackOrderTests`, hub); the empty-tap behaviour inside Details is a T534 defect to confirm with the UI/UX reviewer. Verify at the batch-B report |
| A-F2 | The card's cause line is category level ("Short on Iron Ore", "Short on Iron Plate", "Store is full"). With four Miners standing, "Short on Iron Ore" on the Smelter is not the real cause (auto-sell sells the Ore first). The real cause and the fix button ("Stop auto-selling Iron Ore") need Details, i.e. **2 taps**; SC-003 asks for a "specific limiting cause in 1 tap". **[inference]** a new player reads "Short on Iron Ore" as "build more Miners" and builds one | `20-smelter-built.png`, `22-smelter-details.png`, `70-assembler-card.png` | FR-016, FR-018, SC-003 | Medium | Owner decision: should the card name the cause ("auto-sell is selling it") or carry the one-tap fix? Current feature copy (T534 follow-up or T535 copy review); not a balance matter |
| A-F3 | Under a shortfall the same fact appears twice: `Need $23` inside the disabled button and a yellow "Need $23 more" line below it | `07-upgrade-shortfall.png`, `48-portrait-shortfall.png` | FR-016, SC-009 | Low | Current feature (T534 follow-up) or the UI/UX review; the portrait band grows by 16 px because of it |
| A-F4 | **The `Details ›` button moves** between layouts: left of the card when there is no cause line, right of it (next to the cause line) when there is one. The card also changes state live (Paused to Running as the store drains), so my tap on the right-hand `Details` landed on empty space after the state flipped between screenshot and click. The notice bar adds a second layout shift (about 34 px) for the whole world column | `71-miner-store-full.png` then `72` (not kept: card back to "Running", Details at the left) | FR-016 | Low | T534 follow-up or UI/UX review; **[inference]** a thumb tap during a stall that clears may miss in the same way |
| A-F5 | The stale hint "Tap an empty slot to build, or a machine to inspect it." stays in the lower part of the side panel under every open card and under the empty slot list in landscape | `02-`, `06-`, `07-`, `13a-...png` | SC-010 (text count) | Low | Current feature (T534/T535); the hint is only meaningful with nothing selected |
| A-F6 | Details shows **two "Switch on" buttons at once** for a switched-off machine (one in the action list, one under "This machine is switched off."). Switching on or off is possible only in Details, never on the card | `26` (not kept; text above) and `27-card-of-off-press.png` | FR-018 | Low | Duplicate: defect, current feature. Card switch: owner question (US3 independent test lists "switch it off and on") |
| A-F7 | The first launch with no preferences file logs the warning `[IdleFactory.Preferences] preferences.display.invalid`. `ReadFiles` falls through to `UseDefaults()` for a missing v2 and v1 file, and `UseDefaults()` reports the invalid code | `player1.log` line 46; `DisplayPreferencesStore.cs` `ReadFiles`/`UseDefaults` | T538 | Low | Defect in T538: a missing file should not report "invalid". Log noise only; relevant if a release gate scans for console warnings |
| A-F8 | "Show zoom buttons" persists (`display-v2.json`) but changes nothing on Windows: the zoom +/- column is visible with the setting Off and stays visible with it On, because FR-008a keeps the buttons on pointer profiles. No code outside the Settings presenter reads `ShowZoomButtons` yet | `28-settings` (not kept), `29-settings-toggled.png`; `grep ShowZoomButtons` | FR-003, FR-008a | Info | Consumer is T537 (batch B). **Not testable here**: the touch-profile behaviour needs the emulator or a touch device |
| A-F9 | The compact card's close control is a chevron-with-bar icon without a label (tooltip "Close"); the Details sheet in portrait shows the same icon with the word "Close" and that button returns to the card, not to the factory. **[inference]** the unlabeled icon may read as "collapse" | `06-upgraded-in-place.png`, `46-portrait-details.png` | FR-016, SC-009 | Low | UI/UX review (U-10 checkpoint); copy only |
| A-F10 | Depot and vein are not in the scene, so I could not pan to them and could not test the ViewBounds change. The pan bound at maximum zoom reaches about one cell of empty margin around the grid; at minimum zoom the world occupies about a quarter of the 920 px world column and does not pan | `31c-zoom-out3.png`, `33c-pan-far-lr.png` | FR-009 to FR-011 (world), FR-017 | Info | Re-run after T527 places the art; T543 batch B |
| A-F11 | A 17 to 23 s absence (closing and relaunching the player) opens the full "Welcome back" dialog (11 lines of Time, Cash, stock and cycle counts). **[inference]** heavy for a short absence | `40-portrait-boot` (not kept) and `60-relaunch` (not kept) | none (F005 behaviour) | Low | Idea for the human playtest or a later feature; not a batch-A defect |
| A-F12 | On the card the focus ring stays on the Upgrade button after a click (landscape and portrait), which is what T534 asks. In portrait the machine is reframed above the band, grows again with the shortfall line and is restored exactly on close (pixel rows equal). **Positive result** | `06-`, `42-`, `48-`, `49-...png` | FR-016, FR-017, SC-002 | Info (positive) | none |
| A-F13 | Task state: T534 is merged (PR #748) but its checkbox in `tasks.md` is still `[ ]` | `tasks.md` line 592 | none | Info | Parent mirror step (`scripts/maqa-sync-task-checkboxes.ps1`) |

Counts by severity (table rows): **Medium 2** (A-F1, A-F2), **Low 7** (A-F3, A-F4, A-F5, A-F6, A-F7, A-F9, A-F11), **Info 4** (A-F8, A-F10, A-F12 positive, A-F13), total 13.

Feel statements, all **[inference]**: the docked card makes the first Upgrade feel immediate and the factory stays calm because nothing in the world moves in landscape; in portrait the world moves, then moves back, which is easy to follow; the card is quieter than the old panel but the shortened cause line replaces a clear instruction with a vague one, and that is where a new player could lose a minute (A-F2).

## 5. Evidence index (`playtest-agent-f006-A/`)

| File | Shows |
|---|---|
| `00-fresh-start.png` | fresh launch 1280x720, Cash 37, old tab row, zoom column, Mine |
| `02-first-miner.png` | first compact card, shortfall `Upgrade: $36 / Need $29` plus "Need $29 more", stale hint below |
| `06-upgraded-in-place.png` | after Upgrade: level 2, `$43`, highlight, focus ring on the button |
| `07-upgrade-shortfall.png` | shortfall state (A-F3) |
| `08-details.png` | Details view: rates, cycle progress, Upgrade, Switch off, Remove |
| `09-remove-confirm.png` | Remove confirmation, focus on "Keep machine" |
| `10b-after-esc2.png` | Details still open after two Escape presses (A-F1) |
| `13a-after-remove.png` | notice "Removed Miner. Refunded $88.", build list for the freed slot, layout shift |
| `20-smelter-built.png` | Smelter card: "Paused", "Short on Iron Ore", world cue (A-F2) |
| `22-smelter-details.png` | Details of the same Smelter with the full blocker and fix |
| `27-card-of-off-press.png` | switched-off Press: "Switch it on in Details" (A-F6) |
| `29-settings-toggled.png` | Settings: Reduced motion On, Show zoom buttons On |
| `31c-zoom-out3.png` | minimum zoom, "Minimum zoom" tag |
| `33c-pan-far-lr.png` | maximum zoom, pan bound, selected machine at the edge |
| `42-portrait-card-miner1.png` | portrait bottom-band card, world reframed up |
| `46-portrait-details.png` | portrait Details sheet (35 %), machine visible |
| `48-portrait-shortfall.png` | portrait card with the shortfall line (16 %) |
| `49-portrait-empty-tap-restore.png` | after an empty-world tap: world back at its original rows |
| `70-assembler-card.png` | Assembler "Short on Iron Plate" (Plate auto-sell) |
| `71-miner-store-full.png` | Miner "Store is full" (first sighting of a FULL-store card) |

## 6. What I could not observe

- **Android, touch behaviour and the emulator**: no `adb` call was made. The touch-profile hiding of the zoom buttons (FR-008a), pinch zoom and thumb reach are untested.
- **Escape/Android Back ordering** is a T535 deliverable; I recorded the present state only (A-F1).
- **Depot, vein, world art, K4 cue tail (T526)**: not placed in the scene (T527). No visual verification of the imported art in play.
- **Text scales at 100 to 140 % and above, 640x360, rotation during an open card, keyboard Tab order, screen reader**: not reachable from the player or not run; those belong to the UI/UX reviewer and the Play Mode suites.
- **Item and boost entry points in Details**, "stale action" inline line and the success glint/effect pool (could not tell a glint from the running animation in single frames).
- **Long horizons and a first prestige in the player**: not run; batch A changes no economy and channel A shows it unchanged.
- **The "removed machine closes the card with a notice" case from outside the card** (for example a sale or a priority change): only the player-initiated Remove path was exercised.
- **Casual-pace calibration**: my run is not one (the owner's 2026-10-09 run remains the only human pace evidence).

## 7. Open owner questions

1. A-F2: should the card name the cause ("auto-sell is selling it") or carry the one-tap fix, or is category level plus Details enough for SC-003?
2. A-F1: is Escape/Back for the card and Details acceptance for T534, or only for T535? And should an empty-world tap close Details as well as the card?
3. A-F6: should Switch on/off be reachable on the compact card (US3 independent test)?
4. A-F3, A-F4, A-F5, A-F9: collect them in the UI/UX report for batch A, or fix the cheap ones (A-F3, A-F5) now?
5. A-F7: fix the first-launch "invalid" warning in T538 before batch B?

## 8. Environment notes

- Save folder `IdleFactory-Dev` was moved aside as `IdleFactory-Dev.t543-aside` before the first launch and restored afterwards; the session folder remains next to it as `IdleFactory-Dev.t543-session` (the parent may delete it, together with the older `t343/t346/t453` leftovers).
- The development player triggered a Windows Firewall prompt on first start; it was declined. No network access was granted.
- After the last run no `IdleFactory`, `PlayerWithTests` or `UnityCrashHandler64` process remained; the player build lives in `test-results/f006-p3a/player/` (ignored path, nothing tracked).
