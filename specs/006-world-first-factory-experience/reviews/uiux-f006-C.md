# UI/UX review, F006 P3 batch C (T543, issue #715)

**Status: independent UI/UX Reviewer report (read-only, Medium tier).** I did not write the reviewed UI. I read `playtest-agent-f006-C.md` first and re-checked its Medium findings on the device. Findings and dispositions are proposals for the owner. Screenshots are in `uiux-f006-C/`. Files named `view-*` are half-size contact sheets. The other PNGs are full-size captures. Frame strips are left to right in time.

## 1. Header

| Item | Value |
|---|---|
| Build | main `15deea2c` (detached gate worktree), the Play-Tester's APK already installed on the AVD (no rebuild, no Unity run) |
| Device | AVD `IdleFactory_Test_API_36`, headless, host GPU. Input only through `adb shell input`, captures only through `adb exec-out screencap`. No host mouse, keyboard or window use. No UnityMCP |
| Classes | Phone portrait 1080x2400 at 420 dpi (100 % and 200 % text). `wm size` 640x360 at density 160 (200 % text). Tablet landscape 2560x1600 at density 240. Portrait "large" 1600x2400 at density 240. 360x640 and 844x390 not re-captured (I measured the Play-Tester's captures, marked "PT") |
| Fixtures | The Play-Tester's L2 fixture (factory level 2, 3 Miners, 1 Smelter, 5.0M Cash, store full) and three variants I derived from it (scratch `test-results/f006-p3c-uiux/mk2.py`): `SM` (Smelter only, empty store), `SM3` (Smelter, Press, Assembler, empty store) and `LR` (rejected by the save invariants, not used). The committed fixtures were not modified |
| Cleanup | `font_scale` 1.0, `wm size` and `wm density` reset (1080x2400, 420), `user_rotation` 0, auto-rotate on, `animator_duration_scale` deleted (it was unset before). `adb emu kill`, `adb kill-server`, a leftover `emulator.exe` stub killed. No emulator, qemu or adb process remains. The game stays installed. Leases untouched. No tracked file edited, nothing committed |

## 2. Verdict

**ACCEPT_WITH_NOTES for batch C proceeding to batch D.** No blocker. The U-10 interaction flows work on the emulator with touch: build in 2 taps, ghost and selection frame always visible, hub, Market, factory expansion, milestone banner (appears, dismisses on tap, gone by about 3 s), stage progression, offline return and Back order. Two of the Play-Tester's five Medium findings are refuted as stated (C-F3, C-F4), one is downgraded (C-F1), and two are confirmed in a refined form (C-F5, C-F2 not re-tested). The one item I recommend doing before batch D is the closed-state camera fit that hides the depot and the primary markers (U-C1 below).

## 3. Confirmation or refutation of C-F1 to C-F5

### C-F1 (banner covers the top machine row): confirmed as occlusion, downgraded to Minor

Banner height as a share of the screen height and of the world column (the area between the strip and the open sheet):

| Class | Banner / screen | Banner / world column | What it covers | Evidence |
|---|---|---|---|---|
| Portrait 100 %, Market open | about 5 % | about 12 % | tops of the top-row badges only | `view-e-strip` |
| Portrait 200 %, Market open | about 8 % | about 24 % | the whole top machine row and its badges | `view-g-strip` |
| 640x360, 200 % | about 29 % | about 46 % | the whole top row (4 machines) and its markers | `view-p-strip` |
| 360x640, 200 % (PT) | about 17 % | about 55 % | the top two rows | `pt-g04-f04-banner-ref` |
| 844x390, 200 % (PT) | about 19 % | about 28 % | the upper half of the top row | `pt-g04-f04-banner-ref` |

- **The banner is never over anything the player can have selected.** A level-up comes from Expand factory (or Prestige) inside a system sheet. Opening a sheet replaces the machine card and the build card, so no selected machine and no ghost exists at that moment. The mandatory C1 condition (selected top-row machine or build ghost under the banner) is therefore not reachable by player action. What is left is a passive 2.5 s hiding of the top row and its badges on compact screens.
- Banner lifetime: visible at +0.6 s and +1.9 s, gone at +3.4 s (`view-g-strip`, `view-z-strip`).
- Dismiss by tap works. In `view-r-strip` the banner is gone 0.3 s after a tap on it, and nothing was selected by that tap.
- Two level-ups in a row (levels 3, 4, 5) each replaced the previous banner without stacking (`view-q-strip`, `view-r-strip`).
- Two details: the Level 5 banner has one line ("Level 5. 2 new locations open.", no "Deck extended."), and the banner also hides the top row's slot badges.

### C-F3 (store-full markers per machine, uncapped): refuted as a T557 defect. Two layers draw different things, and the same sprite is shared

- **Per-machine pins.** The yellow store-full pins and the red down-arrow pins on every machine are the T528 slot badges (`SlotVisualModel.BadgeKeyOf`, `MachineVisualState.StorageFull` and `MissingInput`). There is one per machine and no cap, by design. They are large (at least 27 pt).
- **Attention markers.** The T557 markers are drawn by `AttentionMarkerView`. It owns `AttentionModel.MaxMarkers` = 3 renderers: at most 2 shortage markers (resource icon, trunk junction of the first affected row) plus 1 store-full marker at `layout.DepotAnchor`. They are icon-only and 22 art px, so they are small.
- Seen in `b02` and `b02-crop-marker`: with an Assembler short of Iron Plate the world shows three large red badges (one per machine) and one small grey plate icon on the trunk. The chip says "1 +2". The cap holds.
- The rank-0 shortage marker and the store-full marker are not on screen in that capture (see U-C1).
- **Real issue (U-C2):** the store-full attention marker uses the same sprite key as the machine badge (`SlotGridModel.BadgeStorageFullKey` in `AttentionMarkerView.Render`). That is exactly the "mistaken for a machine badge" risk of checklist item 6. Hierarchy is also inverted: the aggregate signal is the smallest thing in the world, and the per-machine badges dominate.

### C-F4 (marker tap selects nothing): refuted. The Play-Tester tapped the slot badge, not the marker

- Taps on the red pin did nothing (`b03`). A tap on a yellow badge on an unselected top-row machine closed the open card, i.e. it counted as an empty world tap (`view-y12`, right half).
- A tap on the real marker, the grey plate icon at the trunk end, selected the Assembler and opened its card with "Short on Iron Plate" (`b04`). That matches `WorldTapResolver.Resolve`: marker icon first, then machine shape, then the marker's 48 pt halo, then empty world.
- Markers are meant to select the first affected location.
- **Residual (U-C3):** nothing in the art says that the small icon is the tap target while the large badge is not. A player taps the big red badge and nothing happens.
- Not tested: the 15 to 20 pt halo tap (checklist 7c) and the far-world close (the Play-Tester passed it).

### C-F5 (world does not re-fit with a sheet open): confirmed in a different form, see U-C1

## 4. Findings

Severity: Major = a spec or checklist item fails and the player loses information; Medium = a clear usability or visual defect on a supported class; Minor = rough edge; Info = observation or positive result. "PT" = Play-Tester ID. Dispositions are proposals.

| ID | Sev | Observed | Evidence | Proposed disposition |
|---|---|---|---|---|
| U-C1 (PT C-F5, C-F3 part) | Medium | **The closed-state camera does not frame the depot column at factory level 2 in portrait.** The vein and the depot are clipped at the screen edges. The collector, the rank-0 shortage marker and the store-full marker (at `DepotAnchor`) are off-screen, so the Smelter-only state ("Short on Iron Ore") shows no marker at all in the world (`c02`, `c02-crop-junction`). With the hub or Market open the world re-fits and the depot, the collector and the Iron Ore marker appear (`view-d01`). At level 3 the closed state fits everything (`view-bb-strip`). On tablet landscape the depot is half under the side panel (`view-v2`). In 1600x2400 at 240 dpi the world is zoomed so the depot is out of view (`u-tablet-1600x2400-d240-view`). The Play-Tester saw the opposite for an Established fixture with the Market open, so the re-fit depends on state | `a02`, `b02`, `c02`, `view-d01`, `view-bb-strip`, `view-v2` | **Fix before batch D, minimal:** include `DepotAnchor` plus the marker box in the horizontal fit rectangle, or drop to the next integer scale when the depot column would be clipped. Otherwise T542 |
| U-C2 (PT C-F3) | Medium | The store-full attention marker reuses the machine store-full badge sprite, and the aggregate marker is much smaller than the per-machine badges | `b02-crop-marker`, `AttentionMarkerView.Render` | T542 or owner: a distinct marker glyph, or hide the per-machine store-full badges while the store is full |
| U-C3 (PT C-F4) | Minor (PT Medium) | The large slot badge looks tappable and is not. The marker works. A badge tap on an unselected machine counts as an empty-world tap | `b03`, `b04`, `view-y12` | T542: extend the machine tap shape by the badge box so a badge tap selects its machine. Alternatively accept and record |
| U-C4 (PT C-F1) | Minor (PT Medium) | The banner hides the top row for 2.5 s on compact classes (table in section 3). Nothing selectable is hidden. Tap dismiss works. Lifetime, no stacking and static reduced motion hold | `view-e-strip`, `view-g-strip`, `view-p-strip`, `view-r-strip` | T542 or T547 (not blocking): primary line only on compact height or large text, B-04 stays in the accessible text and the offline summary |
| U-C5 | Medium | **Build card at 640x360 and 200 % text: "Cancel" and "Confirm" break mid-word ("Canc/el", "Confir/m").** The title takes 3 lines, only about 1.3 type rows are visible, and Confirm collides with the Development Build label (dev only). The ghost and the selected slot are fully visible with a clear frame, and both buttons are at least 48 px high | `view-t1`, `t2-640-buildcard-view` | T547: stop the buttons flex-shrinking below the label width (min-width, `white-space: nowrap`) or shorten the title on compact height |
| U-C6 (PT C-F7) | Minor | The Mine button covers a neighbouring slot while a card is open: the third bottom-row slot at portrait 200 % (`view-y12`, `view-y34`), and the lattice at 640x360 (`t2`). It never covers the selected machine or the ghost | `view-y12`, `view-y34`, `t2-640-buildcard-view` | T547: keep-out for Mine against the lattice |
| U-C7 | Minor | On tablet the build card leaves the idle hint "Tap an empty slot to build, or a machine to inspect it." below Cancel and Confirm. It is stale while the card is open | `view-v3` | T547 copy or state |
| U-C8 | Minor | After Expand factory, the first Back left the Market open. The second Back closed to the factory. A clean hub, Market, Back sequence returned to the factory in one Back. I could not isolate the cause (a focus-pinned button, or the banner's first-back frame) | `view-aa-strip`, `view-bb-strip` | T542: reproduce with a unit-level Back test |
| U-C9 (PT B-F9, B-F10) | Info (regressions) | **Back order is unchanged.** One Back from a hub-opened Market goes to the closed factory, not to the hub (FR-021, the T535 order, disputed in batch B). **Research clip:** the right column is still cut at rest, but the view now says "Scroll sideways to see more Research." and shows a chevron, so the B-U5 discoverability gap is closed | `view-bb-strip`, `view-cc1` | Closed. Owner only if the hub Back order is to change |
| U-C10 | Info (positive) | **Tablet landscape** (side panel): the build card is the best case, with all four type rows visible, Cancel and Confirm large, the ghost and first column unclipped, and nothing under the panel. **Reduced motion** (`animator_duration_scale` 0): the banner is static, with no fade, for the same time. The deck highlight is visible at +0.6 s and gone by +1.9 s (spec: static 2 s, borderline). The strip chip, the build flow (2 taps) and the hub rows read well. **First- and last-unlocked-row selection:** the selection frame and the machine stay fully above the card band at portrait 200 % (`view-y12`, `view-y34`) | `view-v3`, `view-z-strip`, `view-y12`, `view-y34` | Closed |
| U-C11 (PT C-F8) | Minor | 360x640 at 200 %: "Overdrive Ready" runs into the screen bottom and the Development Build label (confirmed on the PT capture; I did not rerun it) | PT `g02` | T547 |
| U-C12 (PT C-F6) | Minor (art call) | The hard-edged deck and black void above and beside it are still visible at fit-all (`a02`, `view-bb-strip`). I did not assess it further | `a02`, `view-bb-strip` | Art Director or owner |

## 5. Walk of the T539 build card checklist (my captures)

| # | Result |
|---|---|
| 1 | Portrait 100 % and 200 % (PT) pass. My 200 % capture (`view-y34`): the ghost is visible above the card, the selected type row is visible and a third row is clipped (scroll works) |
| 2 | 640x360 at 200 %: Cash, store, chip, Overdrive stay visible and the ghost is visible, but the card is cramped and mid-word breaks occur (U-C5). 2400x1080 (PT) pass |
| 3 | **Tablet pass** (`view-v3`) |
| 4 | Ghost reads teal at reduced opacity with a blue ring on the selected slot (PT and mine) |
| 5 to 9 | Not covered. Shortfall, build with Cash (dust, hand-over), Back with a card open, hub with a card open, TalkBack and contrast were not exercised by me |

## 6. T540 banner and stages (my captures)

| # | Result |
|---|---|
| 1 | Measured above: occludes, but no selectable object is hidden (U-C4) |
| 2 | Banner below the strip in portrait, 640x360 and the tablet class. Two in a row do not stack. No camera jump on show or hide |
| 3 | B-01 wording wraps at 200 % without truncation. Tap dismiss works. Back is not tested while the banner is shown |
| 4 | Gone by about 3 s. Reduced motion static (U-C10) |
| 5 | TalkBack not covered. The Android accessibility tree is empty (PT C-F9); I did not re-verify it |
| 6 | Offline milestone line not exercised |
| 7 | Stages: the L3 and L4 views show lit deck tiles, wall pieces and gantry props at the top (`view-s1` style captures; the gantry crates at the top of the 640x360 view look fine and not floating). The full triptych check was not redone |

## 7. Not covered

- Windows player (owner rule).
- Rotation (C-F2 camera stuck after rotating with the card open): not re-tested, relying on the Play-Tester. Recommend a re-test after any fix.
- 360x640 and 844x390 captures (PT's used), 150 % text, and 640x360 first-row ghost (the fixture's top row was full; I used the portrait first row).
- Marker checklist items 1, 4 (grayscale, n_min), 5 (Overdrive draw order), 9 (rotation timing), 10 (recovery), 12 (pixel shimmer), the halo tap 7c, and `uiautomator` accessible names (item 11).
- Prestige banner, offline return with a level-up, build with Cash (dust and hand-over), the in-game Settings reduced-motion route (the Android system animator scale was used), retarget of the build card, keyboard and TalkBack.
- The second shortage marker: with three shortage resources only one marker was visible. I infer that the rank-0 marker is off-screen to the right and the shifted rank-1 marker is the visible one. Not verified on the lattice.

## 8. Environment

Emulator headless, killed; `adb kill-server`; the only leftover `emulator.exe` stub was ended. Settings restored as listed in section 1. Scratch: `test-results/f006-p3c-uiux/` (helper script `h.sh`, `mk2.py`, fixtures `SM`, `SM3`, `LR`, half-size views).
