# UI/UX review, F006 P3 batch A (T543, issue #715)

**Status: independent UI/UX Reviewer report (read-only, Medium tier).** I did not write the reviewed UI. This is the UI/UX half of the T543 gate. The Play-Tester report (`playtest-agent-f006-A.md`) is a separate document and I did not repeat its work. Findings are proposals for the owner.

## 1. Header

| Item | Value |
|---|---|
| Build commit | `810507dbc61821fe4e620901901d11e9f3b672f2` (detached `origin/main`, merge of PR #749), worktree `.maqa-worktrees/f006-p3a-playtest` |
| Batch-A surfaces | T532 presentation models, T533 PanelScaffold, T534 docked MachineCard and Details (PR #748), T538 "Show zoom buttons" setting, T526 art (imported, not placed) |
| Windows build | development player with `IDLEFACTORY_DEV` already in `test-results/f006-p3a/player/` (untracked, ignored) |
| Android build | `BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, built twice (`uiux.apk` 44.5 MB, `uiux2.apk` 44.5 MB, different hashes, both from a clean tree) into `test-results/f006-p3a/android/` (ignored). Built in a worktree whose `Library/` was Windows-warm, not bisected (see U-A1) |
| Devices and classes | Windows pointer, 1280x720 (desktop landscape), 640x360 at 100 %, 390x844 (phone-portrait class); Android AVD `Medium_Phone_API_36.0` 1080x2400 at 420 dpi (cold boot, host GPU). The emulator never got past the loading screen with the batch-A build (U-A1) |
| Text scales | 100 % only. 150 % and 200 % were **not** reached (no text-scale source on Windows; emulator blocked) |
| Inputs | Windows: mouse (`SetCursorPos`, `mouse_event`), keyboard (`keybd_event` **with** the scan code set), window launched with `-screen-width/-height`. Android: `adb` only (install, launch, `screencap`, `top`, `logcat`) |
| Fixtures | Windows development save with the committed device set `w003-s-shipped` (12 machines, level 25+, 5.7 M Cash) copied to `IdleFactory-Dev/IdleFactory/Checkpoints/v2`, and a fresh save for the shortfall state. The original `IdleFactory-Dev` folder was moved aside and restored afterwards |
| Evidence | 13 PNG, 0.8 MB, in `uiux-f006-A/` (index in section 5). Play-Tester screenshots were read, not copied |
| Leases | `unity,emulator` held by `issue-715`, renewed once (300 min), never released. UnityMCP not used |
| Cleanup | After each Unity run `ProjectSettings/` and the URP global settings asset were restored with `git checkout`; no `Assets/UI Toolkit/`; untracked `.utmp/` (Unity build temp, created by my builds) deleted. No player, adb or emulator process left running (the emulator was not running when I started; I cold-booted it and shut it down with `adb emu kill`). Note: the worktree's active build target is now Android |

## 2. Verdict for the batch-A surfaces

**REJECT for the U-10 interaction checkpoint as it stands.** Two reasons:

1. **Blocker U-A1**: the Android build of this commit never finishes its first scene load on the reference emulator (at least 8 minutes, two independent builds, fresh data), while the F004 APK loads in under 40 s on the same emulator. The whole touch checkpoint (portrait and landscape touch, Back gesture, rotation, touch targets on a real touch profile, "Show zoom buttons", 150 % and 200 % text) could not be run.
2. **Major U-A2**: an empty-world tap does not close the Details view (US3 scenario 5, FR-021), reproduced on Windows in landscape and portrait.

What the Windows evidence does support: the card, Upgrade in place, Details parity, the Remove confirmation, focus return and Escape for the card all behave as specified, and the card and its camera framing are clear in landscape, 640x360 and portrait. With U-A2 fixed and U-A1 resolved, I expect ACCEPT_WITH_NOTES for the minor items below. Re-run the touch checkpoint (section 6) before batch B.

## 3. Findings

Severity: blocker = the gate cannot be passed or evidence cannot be produced; major = a spec acceptance scenario or MUST is not met; minor = rough edge or copy. IDs prefixed `U-A`; "PT" names the Play-Tester finding that I reproduced or refuted.

| ID | Sev | Class and state | Expected vs actual | Evidence | Proposed carrier |
|---|---|---|---|---|---|
| U-A1 | blocker | Android phone 1080x2400, app start (fresh data and the w003-s-shipped fixture) | Expected: the F004/baseline emulator procedure loads the game within seconds (T411 APK `artifacts/f004/T411-urp-a3ca464.apk`: ready in under 40 s on the same emulator). Actual: both batch-A APKs stay on `SetGameState: isLoading: true, label 0` for 8+ minutes; the `Loading.AsyncRead` thread runs at 100 % CPU (about 7 min CPU time at the last read), no further `Unity` log line, no frame drawn (the screen is the flat dark window colour). Shader variants after stripping: 356 (not the 2262 of the URP-noise pitfall), user assets 8 MB, so the usual suspects are not it. The Windows player of the same commit loads in about 12 s. Not bisected; candidates are P1 (T512 seam), T526 (F006 art import, PR #749), T532 to T534, T538 | `a01-android-stuck-loading.png`; `test-results/f006-p3a/android/build.log`, `build2.log` (untracked) | Parent: bisect (build 810507d^1, i.e. before T526, and the T534 merge) before batch B, then re-run section 6 of this review. Possibly also an owner question if release builds are affected |
| U-A2 | major | Windows landscape 1280x720 and portrait 390x844; Details open | US3 scenario 5: with any open card or detail view, an empty-world tap closes it or switches selection; FR-021 back order. Actual: the card closes (`w12`), the Details view stays open on every empty tap (`w09`; two taps in a row in landscape, one tap in portrait). Cause in code: `RunShellPresenter.HandleWorldTap` closes only `if (shell.IsCardOpen)`, which is false while the Details sheet is shown. Confirms PT A-F1 (second half) | `w09-details-empty-tap-stays-open.png`, `w12-card-empty-tap-closes.png`, `w11-portrait-details.png` | **T534 defect, fix before batch B** (close Details, or step back to the card, on an empty tap; add a Play Mode case) |
| U-A3 | minor | Windows landscape, keyboard, factory with nothing focused | Keyboard order per contract section 2: persistent layer, then locations, then the card. Actual: ten Tab presses from a cold factory (focus on the world after a click) change nothing visible (`tab` series, not kept). After clicking the Factory tab, Tab runs: tab bar (Market, Contracts, Items, Research, Prestige), Cash, Store, Settings, zoom plus, zoom minus, Mine, the single location stop (Location 1, a focus box), then wraps to the Factory tab. The side tab bar sits before the persistent strip because it is still the F003 shell. I could not complete the stop onto the card (my 13-press count drifted and pressed Enter on Overdrive; it started the Overdrive effect in the fixture save) | contact sheets not kept; observation only | T535 (hub replaces the tab bar) and `KeyboardTraversalTests`; re-verify on the T535 build. Partially covered, see section 6 |
| U-A4 | minor | Windows landscape; Smelter paused by auto-sell | PT A-F2 **confirmed.** Card: state "Paused", cause "Short on Iron Ore" (resource icon, category level). Details: "Status: Paused: Missing Iron Ore (auto-sell)" and "auto-sell is selling it before this machine can use it (needs 2, has 0)" with the fix button. The card names the missing resource, which meets "specific limiting cause" at resource level; it hides that auto-sell is the reason, so a player may build more Miners. Details is 1 more tap | `w03-smelter-card.png`, `w04-smelter-details.png` | Copy decision for the owner (card cause "Short on Iron Ore (auto-sell)" or a one-tap fix); T535/T536 copy pass, not a structural defect |
| U-A5 | minor | Windows landscape; any card | PT A-F4 **confirmed.** The `Details ›` button changes place: left (x 933 to 1020, y 215 to 261) when there is no cause line, right (x 1185 to 1271, y 215 to 261) beside the cause line (`w03`), and one row lower (y 235 to 281) in the shortfall state (`w08`). Upgrade does not move (y 163 to 209 throughout). FR-024 asks for a stable layout when blockers appear; the primary action honours it, the secondary action does not. A live state flip (Running to Paused) can move Details between pointer-down and tap | `w03-smelter-card.png` vs `w01`/`w02` (Miner card), `w08-fresh-shortfall.png` | T534 follow-up (fix the Details column or reserve the row) |
| U-A6 | minor | Windows landscape; shortfall | PT A-F3 **confirmed.** The shortfall appears twice: inside the disabled button ("Upgrade: $36 / Need $29") and as a yellow caption "Need $29 more" below it. The disabled state is readable (text, not dimming alone) but redundant; in portrait the band grows by one row (the Play-Tester measured 16 px) | `w08-fresh-shortfall.png` | T534 follow-up: keep one |
| U-A7 | minor | Windows landscape; any open card | PT A-F5 **confirmed.** The hint "Tap an empty slot to build, or a machine to inspect it." stays at the bottom of the side panel under every card (`w03`, `w08`, `w10`); it only makes sense with nothing selected. Not shown in the portrait band | `w03-smelter-card.png`, `w08-fresh-shortfall.png` | T534/T535 follow-up |
| U-A8 | minor | Windows landscape; Details of a switched-off machine | PT A-F6 **confirmed.** Details shows two "Switch on" buttons (one in the action list at y 388 to 432, one under "This machine is switched off." at y 542 to 586). In addition the card of a switched-off machine says "Switch it on in Details" and has no switch of its own (US3 independent test lists switching off and on). Details also keeps the stale blocker "Missing Iron Ore ... Stop auto-selling" for a machine that is switched off | `w06-details-switched-off.png`, `w07-card-switched-off.png` | Duplicate is a T534 defect (cheap); the missing card switch is an owner question |
| U-A9 | minor | Windows landscape and portrait; compact card header | PT A-F9 **confirmed.** The card's close control is an unlabelled chevron-over-bar icon (tooltip only); the portrait Details sheet shows the same icon with the label "Close" and returns to the card, not to the factory. Accessible names exist only as `tooltip` text; the code has no accessibility bridge for TalkBack (grep for `accessibilityLabel`/`AccessibilityRole` finds none in `Assets/IdleFactory/Unity`, only the `OreVeinControl.AccessibleName` string). See section 6 | `w01`, `w10`, `w11` | Owner decision on an accessibility bridge (spec/constitution scope, not T534); label copy for the icon in T535 |
| U-A10 | minor | Windows 640x360 at 100 % (compact landscape class) | C1 (direction): "Never clip a machine column". The docked card takes about 40 % of the width (x 384 to 640); the selected machine is fully visible, but the fourth grid column is cut at the panel edge (x about 384) and the zoom and Mine controls stay on the left. The world can still be panned. Panel scale in a 640x360 window is 0.5, so text is tiny; the real compact class is the Play Mode rig (640x360 dp), which I did not re-run | `w02-640x360-card.png` | T535 camera/band pass or accept as pannable; owner note |
| U-A11 | info (refuted) | Windows landscape, keyboard | PT A-F1 first half **refuted for this build.** Escape closes the compact card, Escape from Details returns to the card with the focus ring on the `Details ›` control, a second Escape closes the card and the focus ring returns to Location 1 (`s7`, `s8`, `s9`, kept as the series description). I reproduced the Play-Tester's "nothing happens" exactly: it happens when `keybd_event` is sent **with scan code 0**. The shell's per-frame poll (`Update`, `Keyboard.current.escapeKey.wasPressedThisFrame`) is the Input System path and ignores such an event; the UI Toolkit key-event path (`OnDialogKeyDown`) only exists for the dialog, which is why the Remove confirmation cancelled for the Play-Tester. With the scan code set (`MapVirtualKey`), all three work. T534's claim ("Back and Escape close the card") holds in the build; the Play Mode tests call `HandleBack()` and so do not cover the Input System poll, which is why only a player run showed the harness difference | evidence by description; `w05-remove-confirm.png` for the dialog | none (harness artefact). Optional: add a player-level Escape check to T535's `BackOrderTests` run |
| U-A12 | info (positive) | Windows landscape and portrait | Upgrade in place keeps focus on the button (PT A-F12). Remove opens the shared confirmation with focus on "Keep machine", Escape cancels, and focus returns to the Remove button (`w05`, `r2`). The portrait band for a running machine is 14 % of the window height (y 683 to 803), the selected machine stays above it and the world returns after the card closes. Touch-target sizes at panel scale 1.0 (1280x720 reference): Upgrade 47 px tall, `Details ›` 88x48, card close 48x48 (all at or above the 48 dp bar, measured on the screenshot, not in the engine) | `w10-portrait-card.png`, `w05-remove-confirm.png`, `s5` measurements | none |

Counts: **blocker 1** (U-A1), **major 1** (U-A2), **minor 8** (U-A3 to U-A10), **info 2** (U-A11 refuted, U-A12 positive). Total 12 rows.

### Play-Tester findings, status

| PT id | Status |
|---|---|
| A-F1 | Split. Escape: **refuted** (harness scan code, U-A11). Empty-world tap with Details open: **confirmed** (U-A2) |
| A-F2 | **Confirmed** (U-A4), minor |
| A-F3 | **Confirmed** (U-A6), minor |
| A-F4 | **Confirmed** (U-A5), minor |
| A-F5 | **Confirmed** (U-A7), minor |
| A-F6 | **Confirmed** (U-A8), minor, plus the card has no switch |
| A-F7, A-F8, A-F10, A-F11, A-F13 | not in my scope; A-F8 (zoom buttons on the touch profile) could not be tested, see section 6 |
| A-F9 | **Confirmed** (U-A9) |

## 4. Interaction counts (Windows, landscape)

| Journey | Count | Note |
|---|---|---|
| Select machine, Upgrade | 2 | `w01`/`s5` (SC-003 at most 2) |
| Select, Details | 2 | cause plus fix (U-A4) |
| Select, Details, Remove, confirm | 4 | `w05` |
| Close card | 1 (empty-world tap or Escape) | Details: Escape only, empty tap fails (U-A2) |

## 5. Evidence index (`uiux-f006-A/`)

| File | Shows |
|---|---|
| `a01-android-stuck-loading.png` | Android screen 8 minutes after launch: flat dark window, no frame (U-A1) |
| `w01-details-landscape.png` | Details of a Miner, 1280x720, focus on "Upgrade to level 26" |
| `w02-640x360-card.png` | compact landscape, card docked right, fourth column cut (U-A10) |
| `w03-smelter-card.png` | Smelter card, cause line, Details moved right (U-A4, U-A5) |
| `w04-smelter-details.png` | same Smelter in Details with the full blocker and fix |
| `w05-remove-confirm.png` | Remove confirmation, focus on "Keep machine" |
| `w06-details-switched-off.png` | two "Switch on" buttons (U-A8) |
| `w07-card-switched-off.png` | card of a switched-off machine, "Switch it on in Details" |
| `w08-fresh-shortfall.png` | fresh save, duplicate shortfall, stale hint (U-A6, U-A7) |
| `w09-details-empty-tap-stays-open.png` | Details still open after an empty-world tap (U-A2) |
| `w10-portrait-card.png` | portrait 390x844 card band, 14 % |
| `w11-portrait-details.png` | portrait Details sheet, still open after an empty tap (U-A2) |
| `w12-card-empty-tap-closes.png` | card closed by an empty-world tap (contrast to w09) |

## 6. Not covered, and why

- **Everything on touch / Android** (card docked in phone portrait and landscape on the emulator, tap machine then Upgrade, Details chevron and cause line, Remove with confirmation, Android Back gesture for the card and Details, empty-world tap by touch, rotation with the card open, touch-target sizes on a 420 dpi profile, "Show zoom buttons" on the touch profile (PT A-F8), pinch). Blocked by U-A1: the batch-A build never leaves the loading screen on the emulator. No `adb` input could be exercised. Procedure ready for the re-run: install fresh (the AVD is at 92 % full; I had to uninstall the game and clear Chrome, Photos and Play Store data to get under the install threshold), launch once, push `w003-s-shipped` into `files/IdleFactory/Checkpoints/v2`, relaunch.
- **150 % and 200 % text, the 45 % portrait cap, Upgrade visible without scrolling at 200 %, 640x360 at 200 %**: the shell takes its text scale only from Android `Configuration.fontScale`; the Settings sheet has no text-scale control and the Windows player runs at 100 %. The emulator route (`settings put system font_scale`) was blocked by U-A1. The 640x360 check I did is at 100 % in a 0.5-scale window, so it is a camera check only, not the compact dp class. Play Mode `MachineCardTests` (200 % worst-case copy) were not re-run.
- **Windows keyboard Tab order to the card and focus return from the card by Tab**: only partly covered (U-A3). The persistent layer and the location stop were seen, the card stop was not reached, because Tab counts drifted after Overdrive was triggered by a misplaced Enter. Escape and focus return were covered (U-A11).
- **Focus ring**: visible on tabs, Upgrade, Details, Remove, "Keep machine" and Location 1 in the captures; checked only with the keyboard on Windows.
- **TalkBack naming of Close and of a disabled Upgrade**: not feasible. There is no TalkBack bridge in the code (U-A9): a UI Toolkit `tooltip` is not an accessible name, and the emulator was not usable anyway. The Close, Details and Upgrade names exist only as tooltips and button text.
- **Camera restore after rotation, Back-stack order with dialogs (T535)**, the world art (T526, not placed), and the hub: out of the batch-A surface or not yet built.
- **Engine-measured 48 dp targets (C4)**: I measured screenshot pixels at panel scale 1.0 only.
