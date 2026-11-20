# UI/UX review re-run, F006 P3 batch A (T543, issue #715)

**Status: independent UI/UX Reviewer report (read-only, Medium tier).** I did not write the reviewed UI. This re-runs the batch-A U-10 interaction checkpoint of `uiux-f006-A.md` (REJECT: U-A1 blocker, U-A2 major) after T552 (#750) and T553 (#751). Findings are proposals for the owner.

## 1. Header

| Item | Value |
|---|---|
| Build commit | `fa0f1ab6788c6c8a48cc89b69a89ed099453a029` (detached `origin/main`), worktree `.maqa-worktrees/f006-p3a-playtest`, clean tree |
| Build | `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, Unity 6000.6.0f1, "Build Finished, Result: Success", about 3 min, output `test-results/f006-p3a/rerun/rerun.apk` (ignored). The file was 82.0 MB although its entries sum to 46.3 MB (dead zip data from the build), and `adb install` failed with `INSTALL_FAILED_INSUFFICIENT_STORAGE` at 650 MB free `/data`. I repacked the same entries (no content change), zipaligned and signed with the debug key into `rerun-signed.apk` (42.1 MB), uninstalled only `com.DefaultCompany.idlefactorygame`, and installed that. No other app was touched. See finding R-8 |
| Device and classes | AVD `Medium_Phone_API_36.0`, cold boot, host GPU (RTX 5070 Ti, GLES 3.1), 1080x2400 at 420 dpi. Phone portrait (about 410x914 pt after the 7 mm raise), phone landscape (2400x1080, about 914x410 pt), and `wm size 640x360` with `wm density 160` (640x360 pt, compact landscape) |
| Text scales | 100 % (default), 150 % and 200 % through Android `settings put system font_scale` (what `ShellTextScale` reads). Restored to 1.0 |
| Inputs | Touch only, through `adb shell input tap/swipe`. Back by `KEYCODE_BACK` and by the edge-swipe gesture (immersive mode, see R-6). No keyboard, no pinch |
| Fixtures | Fresh data (shortfall, empty slots) and the committed `w003-s-shipped` set (12 machines, 5.7 M cash) pushed with `android-emulator-journey.ps1 -Scenario F003 -Steps W003Push` after a first launch |
| Evidence | 20 PNG, 0.97 MB, in `uiux-f006-A-rerun/` (index in section 5). The PNGs are 720 px high contact sheets of several captures |
| Load time (item 1) | Logcat: graphics device `Creating OpenGL ES 3.1 graphics device` 18:39:15.268, `SetGameState: isLoading: false ... label: 0` 18:39:19.633. **Graphics device to playable shell: 4.4 s** (journey script: 4 s). Process start to graphics device 4 to 6 s. Cold launch with the saved fixture reached the Welcome back sheet in about 12 s from `monkey`. Compare U-A1: never loaded in 8 minutes |
| Leases | `unity,emulator` held by `issue-715`, renewed by the journey script, never released by me. UnityMCP not used |
| Cleanup | After the Unity build: `ProjectSettings/` and the URP global settings asset restored with `git checkout`, untracked `.utmp/` deleted, no `Assets/UI Toolkit/`. `wm size`, `wm density`, `font_scale` and rotation restored; `adb emu kill` and `adb kill-server` run; the Unity editor is not running. Tracked tree is clean except the new report and PNG folder (uncommitted, for the parent). Fixture state on the AVD: Overdrive was started once by a stray tap (R-9); the fixture save is not part of the repository |

## 2. Verdict for the batch-A surfaces

**ACCEPT_WITH_NOTES.**

- **U-A1 (blocker) is gone.** The APK reaches the playable shell in 4.4 s after the graphics device, with the F004 atlas pages at 512 px, on a fresh install and with the fixture.
- **U-A2 (major) is gone**, on touch, in portrait and landscape: an empty-world tap closes the card, and with Details open it closes the Details view and the card (`r02`, `r10`).
- Back order is correct on touch (Details, then card, then closed, with focus returning to the Details button), the Remove confirmation is cancelled by Back and returns focus to Remove, Upgrade in place keeps focus, rotation keeps the card, Details, selection and scroll, the card never moves Upgrade, and every card control measures about 48 dp.
- Large text works at 150 % and 200 % for the card (Upgrade always visible without scrolling) and for the portrait Details sheet (capped at about 46 % of the height, hits the 45 % cap plus border).
- What remains are minor items from the first report (R-1 to R-5, carried forward) and two new defects found at extreme classes (R-1 and R-2 below), neither blocking.
- **No blocker remains. No new major from the batch-A surfaces except the 640x360 / 200 % Details control (R-1), which I rate major only for that class.**

## 3. Findings

Severity: blocker = the gate cannot be passed; major = a spec acceptance scenario or MUST not met; minor = rough edge or copy; info = observation. IDs `R-` are new; `U-A` are the first report's ids with their state on Android touch.

### New findings

| ID | Sev | Class and state | Expected vs actual | Evidence | Proposed carrier |
|---|---|---|---|---|---|
| R-1 | major (this class only) | 640x360 pt landscape, text 200 %, Smelter card with the cause line | Expected: the card keeps its primary controls reachable at the supported worst case (C1 never clip, `RunTextScaleTests` 200 %). Actual: the `Details ›` button is pushed to the right edge of the side card and clipped to a sliver of about 6 px (x 636 to 640); the cause text wraps to two lines ("Short on Iron / Ore"). Upgrade stays visible. I could open Details only by tapping the sliver. The 200 % case with a cause line is the worst case; phone portrait and phone landscape at 200 % are fine | `r16-640x360-200-card.png`, `r17-640x360-200-details.png` | T535 (hub replaces the tab bar; fix the card row at 200 % compact: stack the cause above Details, or wrap the row) and add a `MachineCardTests` case at 640x360 / 200 % with a cause line |
| R-2 | minor | Phone portrait 100 %, any Details; open a machine, scroll its Details, close, select another machine | Expected: a new selection opens Details at the top (name, status, cause). Actual: the scroll offset of the previous Details survives; in `r11` and `r07` the next Details opens with "Makes: ..." at the top and the machine name and Status line scrolled off | `r07`, `r11` (first panel), `r14-text-200-portrait-details.png` shows the correct top case | T535 follow-up: reset the Details scroll on selection change |
| R-3 | minor | Phone landscape 2400x1080 (about 914x410 pt) at 100 %, Details open | Expected: the selected machine and its neighbours stay readable (C1). Actual: the Details sheet takes 50 % of the width and covers the fourth machine column, which is cut at the sheet edge; the camera does not shift. Selected machine stays visible. The 33 % card does not clip. Continues U-A10 | `r10-landscape-details-empty-tap.png` (first panel) | T535 camera/band pass, or accept as pannable; owner note |
| R-4 | minor | Phone portrait 150 % and 200 % text; top strip and tab bar | Expected: no clipped labels. Actual: at 200 % the second tab row (Items, Research, Prestige) touches the screen edge and the descenders of "Research" and "Prestige" are cut; the Development Build overlay sits on "Prestige" (development-only). At 200 % the Mine control also overlays the last machine column (slot 12 partly covered). Both are cosmetic and bounded; tabs still work | `r20-tabbar-200-clipped.png`, `r13-text-200-portrait-card.png` | T535 (hub replaces the tab bar), T537 (Mine placement) |
| R-5 | minor | Phone portrait 150 %, camera after text-scale change and a Details close | Expected: the factory stays framed like the default. Actual: after the font scale changed while Details was open and I closed it, the camera stayed shifted so the third machine column was cut at the right edge (not reproduced from a clean launch at 150 %, where the framing was fine). Possible cause: the world band shrinks and the camera is not re-fitted on a runtime font-scale change | not kept (observed while stepping through 150 %, q0); contrast `r12-text-150.png` | T535 (camera restore), low priority |
| R-6 | info | Android Back, immersive mode | The player runs in immersive mode, so the first edge swipe only reveals the system bars (known T247 behaviour). In the Details test (`r04`) swipes 1 and 2 stepped back Details to the card; swipe 3 was delivered to the world as a drag and panned the camera (card stayed open); swipe 4 closed the card. `KEYCODE_BACK` always acted once per press and gave the exact order Details, card, closed (`r03`) | `r03`, `r04` | none (platform behaviour); note for the owner's real-device pass |
| R-7 | major (owner decision), pre-existing | TalkBack, whole game | Expected (U-A9): Close, Details and a disabled Upgrade have accessible names. Actual: the Android accessibility tree of the running game, read with `uiautomator dump`, is a single `android.view.SurfaceView` with no text and no content description, in portrait with the card open. TalkBack would announce nothing for any control, not only Close. There is no accessibility bridge; this is not a T534 regression (F003 has the same shape). Disabled Upgrade is distinguishable on screen by text ("Upgrade: $36 / Need $29") and the second "Need $29 more" line, not by dimming alone | `uiautomator dump` output (not kept, 3 node classes) | Owner decision on an accessibility bridge (spec or constitution scope), not a T535-T538 task |
| R-8 | minor | Android build artifact | The Unity build wrote an 82.0 MB APK whose entries compress to 46.3 MB (the first review's builds were 44.5 MB). Dead zip data made the install fail on this storage-tight AVD. Not investigated; may be a local builder artifact | build log in `test-results/f006-p3a/rerun/` (ignored) | Parent: check the build in `scripts/` for APK size; the F004 budget is at most 47.3 MB |
| R-9 | info | Android, top strip after `wm size reset` | After restoring the display size the Settings gear and the Overdrive button had swapped places on screen compared with the first launch (gear left of Overdrive, once Overdrive ran and the label wrapped). A stray tap started Overdrive on the fixture save. The strip changes order when its label changes width | `r18-show-zoom-buttons-setting.png` (strip) | T537 (top strip), cosmetic |
| R-10 | info (positive) | Phone portrait and landscape, 100 % | Touch targets, measured in device pixels on a full-resolution screenshot at 2.625 px/dp: card Close 126x124 px (48x47 dp), Upgrade 130 px high (49.5 dp portrait, 126 px = 48 dp landscape), `Details ›` 227x126 px (86x48 dp), Remove dialog buttons about 133 px. All at about 48 dp (the shell's 7 mm rule gives 115 px = 44 dp minimum). Upgrade in place keeps focus on the button, level 25 to 26 to 27 on two taps (`r06`) | `r05-card-touch-targets-crop.png`, `r06` | none |
| R-11 | minor | Phone portrait, "Show zoom buttons" (T538, first report A-F8) | FR-008a: on touch profiles zoom buttons MUST NOT be permanently visible; the setting shows them. Actual: the Settings sheet row exists, toggles On and Off, shows a text state ("On", "Off") and persisted visually, but the `+` and `-` buttons are visible in both states, at 100 % and at 150/200 %. The hiding is evidently the T537 step; T538 delivered only the preference and the toggle | `r18-show-zoom-buttons-setting.png` | **T537** (hide zoom buttons on touch; make the toggle drive them); add a player check that the buttons disappear with the toggle Off |

### First-report findings, status on Android touch

| ID | First report | State on Android touch | Evidence |
|---|---|---|---|
| U-A1 | blocker, APK hangs on load | **Gone.** Loads in 4.4 s after the graphics device (T552) | `r01-android-playable-shell.png`, logcat times above |
| U-A2 | major, empty tap leaves Details open | **Gone** in portrait (`r02`, panels 3 to 4) and landscape (`r10`, panel 2) and with the card (`r02` panels 1 to 2) (T553) | `r02`, `r10` |
| U-A3 | minor, keyboard order (Tab to the card) | **Not re-checked**: touch pass, no keyboard (still open for T535 `KeyboardTraversalTests`) | none |
| U-A4 / A-F2 | minor, card cause hides auto-sell | **Still present.** The card says "Short on Iron Ore"; Details says "auto-sell is selling it before this machine can use it" and offers "Stop auto-selling Iron Ore" | `r06` (panel 4), `r07` |
| U-A5 / A-F4 | minor, `Details ›` moves | **Still present.** Left without a cause line, right beside a cause line (`r06` panel 1 vs 4); Upgrade does not move | `r06`, `r05` |
| U-A6 / A-F3 | minor, duplicate shortfall | **Still present.** "Upgrade: $36 / Need $29" inside the button and "Need $29 more" below it on a fresh save | `r05-card-touch-targets-crop.png` |
| U-A7 / A-F5 | minor, stale "Tap an empty slot" hint in the side panel | **Gone on touch.** Not shown in the portrait band or in the landscape side card (`r09`). I did not recheck the pointer landscape panel | `r09` |
| U-A8 / A-F6 | minor, two "Switch on" buttons | **Still present.** The Details of a switched-off machine shows "Switch on" twice; the card says "Switch it on in Details" and has no switch | `r19-switched-off-duplicate-switch-on.png` |
| U-A9 / A-F9 | minor, Close icon unlabelled, no TalkBack names | **Still present, and worse than stated**: the whole game has no accessible nodes (R-7). The Close control shows a chevron icon on the card, plus the text "Close" in the Details sheet | `r05`, `r07` |
| U-A10 | minor, compact landscape clips a column | **Partly present.** Card (33 %) clips nothing at 2400x1080; Details (50 %) covers the fourth column (R-3); 640x360 at 200 % clips Details (R-1) | `r10`, `r16` |
| U-A11 | info, Escape (harness artefact) | not applicable on touch; Back confirmed (R-6) | `r03` |
| U-A12 | info, positive | **Confirmed on touch** (R-10) | `r05`, `r06`, `r08` |

## 4. Interaction counts (Android touch, phone portrait)

| Journey | Count | Note |
|---|---|---|
| Select machine, Upgrade | 2 taps | `r06` (SC-003 at most 2) |
| Select, Details | 2 | |
| Select, Details, Remove, confirm | 4 | Back at the dialog cancels (`r08`) |
| Close card or Details | 1 | empty-world tap, or one `KEYCODE_BACK` per level |
| Large text card | 1 | Upgrade visible without scrolling at 100 %, 150 %, 200 % |

## 5. Evidence index (`uiux-f006-A-rerun/`)

| File | Shows |
|---|---|
| `r01-android-playable-shell.png` | Fresh launch, playable shell (U-A1 gone) |
| `r02-empty-tap-closes-card-and-details-portrait.png` | card, empty tap closes it; Details, empty tap closes Details and card (U-A2 gone) |
| `r03-back-keycode-details-card-closed.png` | Back order Details, card, closed (first panel also shows the immersive system bars) |
| `r04-back-gesture-immersive-pan.png` | gesture swipes: step back, one swipe pans the world (R-6) |
| `r05-card-touch-targets-crop.png` | full-resolution card crop for the 48 dp measurement and the duplicate shortfall (U-A6) |
| `r06-upgrade-in-place-cause-line.png` | level 25 to 26 to 27 by two taps, Smelter card with cause line, `Details ›` at the right (U-A4, U-A5) |
| `r07-smelter-details-portrait.png` | Smelter Details, blocker and fix below the actions (scrolled) |
| `r08-remove-confirm-back-cancels.png` | Remove confirmation, Back cancels, focus on Remove |
| `r09-rotation-card-open.png` | card kept across portrait to landscape rotation, side card in landscape |
| `r10-landscape-details-empty-tap.png` | landscape Details (50 % width, column cut), empty tap closes it |
| `r11-rotation-details-open.png` | Details kept, with scroll and focus, across both rotations; scroll offset left over (R-2) |
| `r12-text-150.png` | 150 % card; tab bar in two rows |
| `r13-text-200-portrait-card.png` | 200 % card, Upgrade visible, Mine over the grid (R-4) |
| `r14-text-200-portrait-details.png` | 200 % Details at the 45 % cap, Upgrade below the fold, scrolled view |
| `r15-text-200-landscape.png` | 200 % landscape Details and card |
| `r16-640x360-200-card.png` | 640x360 at 200 %, Details button clipped (R-1) |
| `r17-640x360-200-details.png` | 640x360 at 200 %, Details open via the sliver, header wraps |
| `r18-show-zoom-buttons-setting.png` | Settings sheet, toggle Off and On, zoom buttons visible in both (R-11) |
| `r19-switched-off-duplicate-switch-on.png` | two "Switch on" buttons, card text (U-A8) |
| `r20-tabbar-200-clipped.png` | 200 % tab bar bottom row (R-4) |

## 6. Anything blocking batch B

**No.** The U-10 blocker (U-A1) and the major (U-A2) are resolved on the reference emulator with touch. Nothing in R-1 to R-11 blocks batch B. Carry R-1 and R-11 into the batch-B tasks that already own the surfaces (T535 for the card row at 200 % compact, T537 for hiding the zoom buttons on touch), and re-check them on those builds. The two UX defects that spec language already names, U-A5 (Details moves) and U-A8 (duplicate Switch on), remain open minors and should be fixed in the same T535 pass.

## 7. Not covered, and why

- **TalkBack itself.** Not enabled: turning it on changes the emulator's accessibility security settings, which is outside this assignment. TalkBack is installed (`com.google.android.marvin.talkback`). I read the accessibility tree with `uiautomator dump` instead: one `SurfaceView` and no nodes (R-7), which already answers the question for Close and a disabled Upgrade.
- **Pinch zoom, long press and drag input**: `adb shell input` has no multi-touch here (a pinch script exists only as `pinch.sh` in the AVD's `/data/local/tmp`, not run).
- **Keyboard** (U-A3, Tab order, focus rings from the keyboard): touch pass only, no keyboard on the AVD.
- **Pointer profile on Android**, the Windows pointer cases, and U-A7 on the pointer landscape panel: not re-run.
- **A real device or a real font-scale user.** Font scale 1.5 and 2.0 came from `settings put system font_scale`; the 7 mm raise at 160 dpi (640x360) is computed, not measured with a ruler. Physical size claims remain emulator-only.
- **Play Mode `MachineCardTests`** (200 % worst-case copy) were not re-run; this review is evidence from the player.
- **Camera restore after rotation with a different framing** and the world art (T526, not placed): out of the batch-A surface or not yet built. R-5 is an observation that I could not reproduce from a clean state.
- **Engine-measured 48 dp targets (C4)**: I measured screenshot pixels at 2.625 px/dp, not engine layout rects. I did not measure targets at 150 % and 200 %.
