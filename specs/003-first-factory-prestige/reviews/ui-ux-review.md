# Feature 003: Independent UI/UX review record

This file holds the independent UI/UX Reviewer records for Feature 003. T139 wrote the "Shell gate" section below. T128 appends a "Final review" section after the panels (T113 to T118) are integrated. The sections are independent: a PASS here does not carry over to the panels.

## Shell gate (T139)

**Role**: Independent UI/UX Reviewer (read-only), Issue #216, task T139, UX-4 (early shell gate). Covers FR-032, FR-033, SC-007.

**Independence**: the reviewer did not author T112 (Issue #194) and does not author T113 to T118. No production, test, scene or settings file was edited. A temporary, uncommitted Play Mode harness produced the captures and was deleted before the commit.

**Commit reviewed**: `18448b1` (origin/main, contains the T112 merge, PR #275). The reviewed implementation is the QA-approved T112 commits `4ded10a` and `bd06c7e` (`FactoryRun.uxml/uss`, `RunShellDocument`, `RunShellPresenter`, `ShellPanelRegistry`, `SlotGridView`, `RunCameraFraming`, bootstrap wiring). Unity `6000.6.0f1`. Authorities: `contracts/ui-surfaces.md` (Global, Evidence required), `docs/art/F003-provisional-art-bible-v0.1.md` section 8 and section 9 (ART-4), `spec.md` FR-032, FR-033, SC-007, `reviews/plan-reviews.md` UX-1 to UX-10.

### Verdict

**Shell gate verdict: PASS** (no blocking finding). Panels T113 to T118 may start. Ten non-blocking findings are recorded below, each with an owner. F-1, F-2 and F-4 should be resolved before the final review (T128); the panel authors should read F-6 and F-7 before they build.

### Test evidence

All three suites ran on the committed code in this worktree through `scripts/test-unity.ps1` (Play Mode, batch). Each run discovered tests (no 0-test run).

| Command | Result | Results file (local, git-ignored) |
|---|---|---|
| `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/PlayMode/RunHudLayoutTests.cs` | PASS, 80 of 80, 0 skipped | `test-results/unity/20261007T091539526Z/PlayMode-results.xml` |
| `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/PlayMode/RunKeyboardFocusTests.cs` | PASS, 46 of 46, 0 skipped | `test-results/unity/20261007T091615820Z/PlayMode-results.xml` |
| `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/PlayMode/SlotGridTests.cs` | PASS, 25 of 25, 0 skipped | `test-results/unity/20261007T091633518Z/PlayMode-results.xml` |

Beyond the suites, a temporary harness (deleted) rendered the real `FactoryRun.uxml/uss` shell with the real F003 sprites into off-screen panels at all 8 screen classes, composited the world camera under the UI, and ran `RunShellLayoutAssertions.AssertShellLayout` on two additional panel-point classes the authored `PanelSettings` produces on a real phone (see F-10): 1431x644 (landscape, classifies as Desktop) and 644x1431 (portrait). Both passed (sheet 759 of 1371 pt high in portrait, 55 percent; side panel 360 pt in landscape).

### Captures

Directory: `specs/003-first-factory-prestige/reviews/ui-ux-review-captures/shell-gate/` (29 PNG files, each under 300 KB). Panel points 1:1 with the stated size; the 1280 and 1920 captures are downscaled to 1100 px. Naming: `<class>-<WxH>-<state>.png`.

| State | Content |
|---|---|
| `empty` | Fresh run: Cash 25, RP hidden, 4 empty slots, 8 locked slots (L2 to L5), locked-slot notice, no sheet |
| `worst` | Cash `9.00Qi`, RP `1.23M`, store 96 percent, Overdrive cooling down, exact-value detail line pinned by focus, rotation notice, 12 occupied slots with all 6 badges and both markers, Build sheet (slot 8 selected) with worst-case panel content |
| `dialog` | `worst` plus the shared destructive confirmation dialog open on Cancel |

Screen classes captured (all three states each): phone-portrait 360x640 and 390x844, phone-landscape 640x360 and 844x390, tablet-portrait 768x1024, tablet-landscape 1024x768, desktop 1280x720 and 1920x1080. Extra captures: `phone-portrait-360x640-tabfirst`, `-tablast` and `desktop-1280x720-tablast` (focus ring on the edge tabs, F-3), `phone-portrait-360x640-z24` and `-z16` (fit-all floor and camera minimum, handoff item on badge readability).

### Check table

Verdict key: PASS = verified by test and capture. NB = non-blocking finding. DEF = deferred to T124 (device) with reason.

| # | Item (task text or handoff) | Screen class and state | Expected behaviour | Observed | Verdict | Acceptance impact |
|---|---|---|---|---|---|---|
| 1 | Density: top strip contents | All 8 classes, closed and worst-case strip | Phones: Cash, RP once unlocked, store bar, Overdrive in one row; tablet and desktop: Overdrive in the side-panel header | `PhoneDensity...` and `SidePanelDensity...` pass at every class. Captures show one row at 360 pt with `9.00Qi`, `1.23M`, bar, `Cool 4:59`. RP cell hidden in `empty`. | PASS | UX-1, FR-032, SC-007 |
| 2 | Density: tab bar | Phones, all 4 classes | Six tabs, each at least 44 pt, labels at least 12 pt, no overlap | 60 pt per tab at 360 wide; tabs sit on the bottom edge of the safe area; labels 12 pt; no overlap asserted | PASS | UX-1, SC-007 |
| 3 | Density: sheet height | 360x640, 390x844 (and 644x1431) | Bottom sheet at most 60 percent of the safe-area height, above the tab bar; selected slot stays in view | Sheet 296 of 600 pt (49 percent) at 360x640, 394 of 763 at 390x844; `TheSelectedSlotStaysInViewAboveAnOpenSheet` passes for all 12 slots at every class; world band at least 198 pt tall | PASS | UX-1, FR-032 |
| 4 | Density: phone-landscape side sheet and notch inset (handoff) | 640x360 (insets 44/44), 844x390 (47/47) | Side sheet at most 50 percent of the width, right edge at the safe edge, content inside the inset | Sheet 276 of 552 pt (640 class) and 375 of 750 pt (844 class); right edge equals the safe edge; strip, tab bar and sheet begin and end at the inset in the captures; world band at least 238 pt tall | PASS (geometry). Real notch: DEF | UX-1, FR-033. DEF to T124: a physical cutout cannot be reproduced off-device; the simulated insets pass |
| 5 | Rotated classes | 640x360, 844x390, 1024x768 (rotations of the 3 base classes) | Same rules after rotation, grid 4 columns by 3 rows | All pass; fit-all at least 29.8 pt per cell in the tightest class (640x360 closed) | PASS | UX-3, FR-033 |
| 6 | Shared dialog focus: trap, initial focus, return | 360, 640x360, 768, 1280 | Opens on Cancel; Tab, arrows and Shift+Tab stay inside; focus returns to the invoker on close; Escape and Back cancel | `RunKeyboardFocusTests` pass (46); captures show the focus ring on Cancel; dialog fits the safe area with worst-case text at all 8 classes | PASS | UX-2, FR-032 |
| 7 | Rotation with an open dialog (handoff) | All 8 classes, Prestige-style destructive dialog open, then rotate | Dialog cancels with reason `Rotation`, nothing confirmed, notice shown, focus returns to the invoking control, no overlap after relayout | `ARotationRelayoutsAnOpenSheetAndCancelsAnOpenDialogWithoutOverlap` passes at all 8 classes: `confirmed == 0`, invoker focused, notice equals `Cancelled by rotation`. Bootstrap registers the dialog as a transient action. A sheet stays open and the slot stays selected across a rotation by design (no spend or confirm is pending). | PASS (shell). Real Android configuration change: DEF | UX-2, UX-9, FR-033. DEF to T124: the emulator `Rotation` step drives the real configuration change |
| 8 | Android Back versus OS gesture (handoff) | Dialog open; sheet open; nothing open | Back acts as Escape: closes the dialog, then the pinned detail, then the sheet; with nothing open it is not consumed (the OS handles it) | `HandleBack` order and once-per-frame idempotence verified by test with Escape events. The device path (Input System maps Back to Escape; predictive-back and 3-button navigation) is not testable here. | DEF | UX-2, FR-033. DEF to T124 with reason: needs a device or emulator. T124 should check Back and the back gesture with a dialog open (dialog closes, app stays) and with nothing open (app leaves normally) |
| 9 | Tab-bar navigation | 360, 640x360, 768, 1280 | Left and right arrows move along the tabs, clamped; Enter and Space activate; tapping the open tab closes the phone sheet | Tests pass; focus order covers every control including `panel-close` and the sheet controls | PASS | UX-2, FR-032 |
| 10 | Selected tab marked beyond color (ART-4, handoff) | All phone and side-panel captures | Selected tab differs by more than hue | 3 pt accent top bar, raised fill, bold label and accent-tinted icon. Visible in grayscale terms (bar, weight and fill change). | PASS | ART-4, FR-032 |
| 11 | Number detail line: exact value on focus and hover | Cash, RP and store cells at all classes | Exact value shown in the detail line; updates live; hides on blur unless pinned | `ACompactNumberShowsItsExactValue...` passes; capture shows `Cash: 9,000,000,000,000,000,000` under the strip | PASS | UX-8, FR-032 |
| 12 | Tap-to-pin discoverability and long-press versus world pan (handoff) | Touch, phone portrait | A tap pins the detail line; long press (500 ms) only on non-draggable, non-world elements | Tap pins, second tap unpins, Back unpins first. Long press is attached only to Cash, RP and store cells in the strip, which are neither draggable nor world, so it cannot compete with pan or drag-to-reorder. The cells have no visual hint that they are tappable, and a world press does not unpin. | PASS with NB | UX-8. See F-5 |
| 13 | Touch targets | All classes, shell and dialog | Every actionable element at least 44 x 44 pt | `AssertTextAndTargets` passes: strip cells, Overdrive, zoom, close, tabs, dialog buttons, panel buttons. Zoom and close buttons are exactly 44 pt. | PASS (points). Physical size: see F-10 | SC-007, FR-032 |
| 14 | Rotation notice readability, duration and position (handoff) | All classes, after a rotation cancel | One-line notice "Cancelled by rotation", readable, positioned next to the status it explains, gone when no longer true | Text is one line at every class; 14 pt on `--surface` with `--text-primary` (11.86:1). It sits directly under the detail line inside the world column and is clearly visible. It never expires: it stays until another notice replaces it. It is styled exactly like the detail line, so two identical boxes stack. It has no icon. | PASS with NB | UX-9, FR-033. See F-1 |
| 15 | Locked-slot notice copy and position (handoff) | Fresh run, tap a locked slot | The requirement is named, with a shape cue on the slot | Copy "Slot 5 unlocks at factory level 2." is clear; the slot itself carries a padlock and `L2` to `L5`. The notice is plain text without the lock icon of Art Bible 9.4 and stays after the player reaches level 2. | PASS with NB | FR-032. See F-1 |
| 16 | Placeholder copy while panels are pending (handoff) | Phone and side panel, unregistered panel | The shell says the panel is not there yet and stays usable | "The build sheet for slot 1 is not available yet." and "This panel is not available yet." appear inside the sheet; tabs, close and focus keep working (`AnUnregisteredPanelShowsTheNotAvailableStateInsteadOfNothing`). Copy is acceptable as interim text; T113 to T118 must replace it. | PASS | FR-032 (empty and error states) |
| 17 | Rotation notice vs detail line stacking | Phones, worst-case captures | Notice and detail line stay inside the world column and do not overlap each other or controls | `NoticeAndDetailLineStayInsideTheWorldColumn...` passes. In captures the two bars cover the top 56 pt of the world band; the camera framing ignores them (F-8). | PASS with NB | UX-1, SC-007 |
| 18 | ART-4 hierarchy: destructive confirm | All `dialog` captures | Fill `--danger`, label `--on-accent` (6.84:1), warning icon and text | `Remove` is `#F87171` with `#0B1020` label; warning icon tinted `--danger` sits in the dialog header with the title, not on the button. Not color alone. | PASS with NB | ART-4, FR-032. See F-6 |
| 19 | ART-4 hierarchy: focus ring contrast | `dialog`, `worst`, tab captures | 2 pt `--on-accent` inner ring and 2 pt `--text-strong` outer ring (14.03:1 on `--surface`, 16.96:1 on `--bg`) | Both rings are visible on Cash, on Cancel (surface-raised) and on the tabs. On controls at the screen edge the outer 4 pt ring is cut off on that edge (F-3). | PASS with NB | ART-4, UX-2, SC-007 |
| 20 | ART-4 tokens in `FactoryRun.uss` | Whole shell | Only the palette roles of section 9.1; 12 pt minimum; disabled look; no color-only state | All 13 tokens defined with the Art Bible hex values; minimum text 12 pt (tab labels, captions); disabled controls use surface fill, muted label and `--border` (solid, not dashed: USS has no dashed border; the lock icon and reason text carry the state); warning and danger text appears without an icon in notices (F-1) | PASS with NB | ART-4, FR-032 |
| 21 | Slot-badge readability at the fit-all floor (handoff) | 360x640 closed: fit-all 39.0 pt per cell. `z24` and `z16` captures force 24 and 16 | Badge at least 27 x 12 pt; markers visible from 24, hidden below; labels recognizable only from Z32 (Art Bible 8.1, 8.2) | The floor of 24 is not reached at any class (smallest fit-all is 29.8). At Z24 the badge glyphs and both markers are readable and the badge text is about 4.5 pt, which Art Bible 8.1 does not require to be legible below Z32. At Z16 markers are hidden as specified and the badge glyph and color remain. `TheFitAll...` and `SlotGridTests` pass. | PASS with NB | FR-032, ART-2. See F-9 |
| 22 | Scroll affordance for clipped content on phone landscape (handoff) | 640x360 and 844x390, worst-case sheet | A visible scroll cue; focus scrolls into view | A 6 pt thumb (70 percent white) is visible on the sheet edge in the captures; `WorstCaseSheetContent...` asserts clipping is contained, every focused control scrolls into view and stays in the safe area. The cue is thin but present. | PASS | FR-032, SC-007, UX-3 |
| 23 | Empty state of the slot grid | Phone portrait closed, fresh run | "4 empty slots in a fresh run, with a 'Build a Miner' hint" | Four empty slots with a plus glyph. No text hint on a phone while the sheet is closed. Tablet and desktop show "Tap an empty slot to build, or a machine to inspect it." in the permanent panel. | PASS with NB | FR-032 (empty state). See F-2 |
| 24 | UI pointers stay UI-owned | Shell controls and the world | A world gesture never activates a panel control; a panel press never pans | `IsOverRuntimeUi` picks the shell; layout containers use `picking-mode: Ignore`, the dialog layer blocks the world while open. The notice and detail bars take pointers when shown (F-8). | PASS with NB | FR-033 |
| 25 | Text scaling (Global, Art Bible 9.3) | Not tested | Text scales without clipping or truncating meaning, up to 200 percent | No scaling hook and no test exists. Tab labels use `white-space: nowrap` in a 60 pt cell (`Contracts` is about 51 pt wide at 12 pt), so any scale above about 115 percent clips. | NB | FR-032. See F-4 |

QA non-blocking notes (consider for UX impact only): the "not before 500 ms" check relying on one frame has no player impact. `ComposeRunShell` not clearing `runShell` after a failed `Configure` can leave a broken shell after a start-up retry (F-7). `RunShellDocument.Dispose` not clearing dialog state or calling `OnCancel` has no visible effect today because the tree is dropped, but a panel awaiting a confirmation result would never be told (F-7).

### Findings

No blocking finding. Owner "T112 follow-up" means the T112 implementer in a small fix task that does not hold T113 to T118.

| ID | Class | Finding | Owner and timing |
|---|---|---|---|
| F-1 | Non-blocking | Notices never expire or clear. "Cancelled by rotation", the locked-slot notice and an Overdrive rejection stay until another notice replaces them, so "Slot 5 unlocks at factory level 2." remains after the player reaches level 2. Notices look identical to the detail line and carry no icon, although Art Bible 9.1 and 9.4 pair warning, danger and lock states with an icon. | T112 follow-up before T128: clear or time out a notice (for example on the next interaction, a sheet or tab change, or after a few seconds), add the info, warning and lock icons, and give the notice a distinct look. Panels must not rely on the shell notice for inline rejection copy; the contract places that copy next to the control used. |
| F-2 | Non-blocking | On a phone with the sheet closed, a fresh run shows no "Build a Miner" hint (contract surface table, slot grid empty state). Only the tablet and desktop panel shows build guidance. | T112 follow-up or T113 (build sheet author); needed for US1 discoverability before T128. |
| F-3 | Non-blocking | The focus ring is drawn 4 pt outside the control, so on controls at the panel edge (Factory tab, Prestige tab, Cash cell, desktop Prestige tab) the outer edge of the ring falls off screen. Three sides stay visible and the tab is also marked, so focus is still identifiable. | T112 follow-up: inset the ring on edge controls or pad the edge. Re-check in T128. |
| F-4 | Non-blocking | No text-scale mechanism or test exists although FR-032, the contract and Art Bible 9.3 require scalable text up to 200 percent. Six nowrap tab labels in 60 pt cells cannot absorb it. Panels T113 to T118 will each add fixed font sizes. | Owner decision (Game Designer/Product plus Architect) before panels grow: either a single font-size variable in the shell USS with a scale test, or a recorded decision that scaling is the platform panel scale. Then T112 follow-up. |
| F-5 | Non-blocking | The tap-to-pin exact value has no visual hint; a world press does not unpin the line; the detail line always appears under the top strip, so for a number inside a phone sheet it shows about 300 pt away from the tapped element. | Panel authors (T113 to T118) when attaching numbers; T128 to confirm. T112 follow-up may add a small info glyph and unpin on world press. |
| F-6 | Non-blocking | The warning icon of a destructive confirm is in the dialog header, not on the Confirm button (Art Bible 9.2 names icon and text on the button). The destructive state is still not color-only. | T112 follow-up (cosmetic). Panels reuse the shared dialog as is. |
| F-7 | Non-blocking | `ComposeRunShell` keeps a non-null, not-ready `runShell` after a failed `Configure`, so a start-up retry skips composition and later dereferences a missing grid or presenter. `Dispose` drops a pending confirmation without calling `OnCancel`. Both need a corrupt scene or a re-entrant start-up, not normal play. | T112 follow-up (robustness). |
| F-8 | Non-blocking | The world area given to the camera ignores the notice and detail bars, so they overlay the first slot row on phones, and while shown they absorb world pointers beneath them. The selected slot stays visible in every captured state (at least 198 pt band at 360x640), but the margin shrinks with two bars. | T112 follow-up: subtract the bars from `TryGetUnobstructedScreenRect`, or move them into the strip. |
| F-9 | Non-blocking | Slots are 32 pt at the camera minimum (Z16) with a 40 pt pitch, below 44 pt, and badge text is about 4.5 pt at Z24 and illegible at Z16. Both are sanctioned by Art Bible 8.1 (glyph and color carry the state; labels from Z32) and the shell always starts at fit-all of at least 29.8 pt per cell. The 44 pt rule is not applied to world slots at user zoom. | Game Designer/Product and Art Director: record the exemption or raise the zoom minimum to 22. Panels (T113 to T118) must state the machine status in text. |
| F-10 | Risk, pre-existing | The authored `CoreGameFoundationPanelSettings` use `ScaleWithScreenSize` (1280x720 reference, match 0.5). A 1080x2400 phone gets about 1.68 px per point: portrait is 644x1431 pt (Compact), landscape is 1431x644 pt and classifies as Desktop, so the phone-landscape side sheet of at most 50 percent never occurs on such a device. Both real-scale classes pass the shell layout assertions. The same scale makes 44 pt about 74 px, roughly 4.5 mm on a 420 dpi screen, below the 7 to 10 mm commonly recommended for touch targets, and 12 pt text roughly 1.2 mm tall. Feature 001 already states that Game View is layout evidence only. | Product and Architect decision, then T124 measurement on the emulator and a named device; not a defect of T112. |

### Deferred to T124 (device evidence) with reasons

- Android Back and the OS back gesture (check 8): needs a device or emulator; the shell contract (Escape equals Back, idempotent per frame) is proven in Play Mode.
- Real rotation and a physical cutout (checks 4 and 7): off-device layouts use simulated insets that pass; T124 `Rotation` step must show the dialog cancelling with the notice and focus return.
- Physical touch-target and text size (F-10) and the real landscape class (Desktop layout at about 644 pt high).

### Conclusion

The committed shell meets the density rules at every screen class including rotations, the shared dialog traps and returns focus correctly, the tab bar and number detail line work by keyboard, pointer and touch, every actionable element measures at least 44 pt, and the Art Bible UI tokens are used faithfully. The open findings are polish, robustness and decision items that do not invalidate the panels' work.

**Shell gate verdict: PASS**


## Final review (T128)

**Role**: Independent UI/UX Reviewer (read-only), Issue #208, task T128. Covers FR-032, FR-033, SC-007.

**Independence**: the reviewer did not author T112 to T118, T191, T192, T196, T243 or T246 to T250. No file under `Assets/`, `Packages/`, `ProjectSettings/`, a scene, prefab or USS/UXML was edited (Unity re-serialized `ProjectSettings.asset` during the runs; that noise is reverted and not committed).

**Commit reviewed**: `872984a` (origin/main, includes T246 to T250). Unity `6000.6.0f1`.

### Verdict

**Final review verdict: PASS WITH FINDINGS.** No blocking finding. F-1 to F-8 and F-10 of the shell gate are resolved, F-9 is still open (no owner decision recorded). Eleven new non-blocking findings (N-1 to N-11) are listed below. Three of them (N-5, N-8, N-9) want an owner decision before the feature closes; none blocks T132.

### Test evidence

All runs on this worktree through `scripts/test-unity.ps1 -Suite PlayMode -TestFile ...`, each discovered tests (no 0-test run), 0 skipped.

| Test file | Result |
|---|---|
| `RunHudLayoutTests.cs` | PASS, 178 of 178 |
| `RunKeyboardFocusTests.cs` | PASS, 51 of 51 |
| `SlotGridTests.cs` | PASS, 28 of 28 |
| `RunTextScaleTests.cs` (100 to 200 percent, every screen class) | PASS, 150 of 150 |
| `OfflineSummaryPanelTests.cs` (T246) | PASS, 62 of 62 |
| `ResearchPanelScrollTests.cs` (T248) | PASS, 10 of 10 |
| `MachinePanelTests.cs` (T249) | PASS, 89 of 89 |
| `RunDesktopLifecycleTests.cs` (T250) | PASS, 6 of 6 |
| `RunLifecycleTests.cs` (mobile lifecycle, unchanged) | PASS, 7 of 7 |

Not re-run by me (taken from the QA records of their tasks): the panel suites other than the machine and Research panels, Edit Mode, the tagged Windows test-player suite.

### Captures and method

Directory: `specs/003-first-factory-prestige/reviews/ui-ux-review-captures/final/` (PNG, each under 180 KB).

* **Android emulator** (`emulator-5554`, API 36 host GPU, gesture navigation, 1080x2400 at 420 dpi, driven with `adb input` and `screencap`): installed the T247 build `baseline-pbon.apk` (an ARM64 Development build from the T247 worktree; it contains T246 but **not** T248 to T250). Captures `android-phone-*`.
* **Windows player** built from this worktree (`BuildWindows64`, includes T248 to T250), run windowed at 1920x1080, 1280x720, 768x1024, 390x844 and 844x390 and driven with synthetic mouse events. Captures `desktop-1920x1080-*`, `win-*`, `desktop-focus-*`. Windows windows use the pointer profile and the authored `ScaleWithScreenSize` scale, so 390x844 and 844x390 show the layout class for that window size, not a phone's physical size.
* Not produced: tablet and phone captures at 100 to 200 percent text and every rotated class (covered by the 150 `RunTextScaleTests` and the 178 `RunHudLayoutTests` assertions, which pass), real phone-landscape, a physical device. This review makes no physical-device claim.

### Shell gate findings F-1 to F-10

| ID | Status | Evidence |
|---|---|---|
| F-1 notices never expire, no icon | **Resolved** | Notice has an icon and a left accent bar, distinct from the detail line (`android-phone-1080x2400-fresh.png`: "Build a Miner: tap an empty slot."); T192 clears after 6 s and on interaction; `RunHudLayoutTests` 178 pass. The "Locked" Overdrive strip keeps its reason. |
| F-2 no Build hint on a phone | **Resolved** | Same capture; hint is gone once a machine exists (`android-phone-machine-sheet.png`). |
| F-3 focus ring cut off at the edge | **Resolved** | Ring fully visible on the right-edge Research tab (`desktop-1920x1080-research-0.png`) and on dialog buttons; `RunKeyboardFocusTests` 51 pass. |
| F-4 no text scale | **Resolved** | `RunTextScaleTests` 150 of 150 at every screen class; see N-9 for the 200 percent small-screen consequence. Real Android font scale not measured by me. |
| F-5 pinned value hint | **Resolved (shell)** | Info glyph above Cash and the store bar; see N-6 for its size. |
| F-6 warning icon off the Confirm button | **Resolved** | `android-phone-home-return-1-after-remove-tap.png`: warning icon inside the red Remove button, Cancel focused. |
| F-7 compose retry / dispose | **Resolved per T192 tests** | Not re-verified by me beyond the green suites. |
| F-8 notice bars overlap the grid | **Resolved** | The grid starts below the hint bar (`android-phone-1080x2400-fresh.png`). |
| F-9 slot below 44 pt at the camera minimum | **Still open** | No decision recorded (`tasks.md` line 41). Same evidence as the shell gate; still non-blocking: the shell starts at fit-all, the 44 pt rule is not applied to world slots at user zoom, and machine status is in text in the panel. Needs the Product and Art Director exemption or a raised zoom minimum. |
| F-10 physical size of 44 pt | **Resolved on the emulator** | At 420 dpi the zoom and Close buttons measure 115 px = 7.0 mm (7 mm = 115.7 px), tab bar cells 180x124 px; the raise to 2.63 px per pt works on the device. Physical device: not measured. |

### New findings

Impact column: **NB** = non-blocking. Owner decision items are marked.

| ID | Screen class, state | Observed | Expected | Impact | Evidence |
|---|---|---|---|---|---|
| N-1 | Phone portrait 1080x2400, sheet open | The camera centres on the selected slot; the third grid column is cut at the right edge and the grid is about 60 px right of centre (closed grid: 155 px left margin, 50 px right). The selected slot is fully visible. | Whole columns or a clear pan cue; symmetric framing. | NB (cosmetic; selection always visible) | `android-phone-build-sheet.png`, `android-phone-1080x2400-fresh.png` |
| N-2 | Desktop 1920x1080, Research, Bargaining detail | Under "Not enough Research points." a second warning icon sits alone with no text. | One icon per reason line. | NB (visual defect) | `desktop-1920x1080-research-wheel.png` |
| N-3 | Desktop, Research graph | The second graph column is cut off until the player scrolls or drags. The hint "Scroll sideways to see more Research." sits below the graph, near or below the fold at 1080 p, and says sideways although a plain wheel scrolls the sheet vertically. | Hint next to the graph; wording matches the gestures (drag, Shift+wheel). | NB | `desktop-1920x1080-research-0.png` |
| N-4 | Desktop, Research | **T248 verified**: a left-drag on empty canvas pans the graph in both axes; a plain wheel scrolls the sheet; the focus ring stays. Shift+wheel was **not** verified by hand (the injected key did not reach an unfocused window); it is covered by `ResearchPanelScrollTests` (10 of 10). | OD-8 | Resolved, with that gap | `desktop-1920x1080-research-drag-pan.png`, `-wheel.png` |
| N-5 (owner decision) | Windows player 1280x720, 768x1024, 390x844, 844x390 | A cold start after a short unclean exit (18 s, 33 s, 56 s away) opens the modal "Welcome back" summary. T246 applies the 60 s threshold only to lifecycle returns; a cold start credits and shows the summary at any length. | Owner's intent for T246 was no interruption for a brief absence; for a restart after 18 s the same reasoning applies, or the decision that restarts always show it should be recorded. | NB | `win-1280x720-start.png`, `win-768x1024-start.png`, `win-390x844-start.png` |
| N-6 | All, Cash and RP cells | The pin info glyph is about 10 px (about 6 pt) at 1920x1080 and 1080x2400, smaller than the 12 pt text minimum and not a 44 pt target by itself (the cell is). | Visible but decorative; acceptable, recorded. | NB | `android-phone-1080x2400-fresh.png`, `desktop-1920x1080-fresh.png` |
| N-7 | Phone portrait, Machine sheet | Two stacked separator lines and a blank band under "Cycle progress" are the reserved last-batch row (see N-8). | | NB | `android-phone-machine-sheet.png` |
| N-8 (item e) | Desktop 1920x1080 Machine panel, T249 build | Reserved rows: an empty line between Status and "Makes", and a blank line under "Cycle progress". The sheet scrollbar track is always visible although the content fits. See "Items e and f" below. | Stable layout, no false scroll affordance. | NB, acceptable | `desktop-1920x1080-machine-panel.png` |
| N-9 (item f, owner decision) | Small screen at 200 percent text | The blocker section is below the actions and needs scrolling; see "Items e and f". | | NB, recommend | `RunTextScaleTests`, `MachinePanelTests` |
| N-10 | Test gap (item b) | No end-to-end test of a lifecycle return with an expired effect; only `OnlyACleanAppliedReturnBelowTheThresholdIsSuppressed` (a pure `ShouldOpenSummary` check) covers the expired-effect and expired-contract cases. | A short return with an expired effect passes `Present`, shows the summary and reconciles once. | NB | `OfflineSummaryPanelTests.cs` |
| N-11 | Windows 844x390 and 390x844 windows | Tab labels are about 8 px high at those window sizes (pointer profile, authored scale, no physical-size raise). | Not a phone case: on a touch phone the raise to 7 mm applies (F-10). | NB, informational | `win-844x390-start.png` |

### T246 / T247 handoff items

| Item | Result |
|---|---|
| (a) 60 s threshold is one named constant and fits the intent | **Yes.** `OfflineSummaryPanel.ShortAbsenceThresholdMs = 60_000L` is the only definition (one reference in `ShouldOpenSummary`, one pinned by test). Fits the owner's text for HOME and focus returns. On the emulator a HOME and return after a few seconds opened no summary and left the open Machine sheet unchanged (`android-phone-home-return-*-before-remove.png`). It does not cover a cold start (N-5). |
| (b) a test covers a full return with an expired effect | **Partly.** Returns at and above the threshold are tested end to end (`AReturnAtAndAboveTheThresholdShowsTheSummaryWithIdenticalAccounting`). A short return with an expired effect is tested only at `ShouldOpenSummary` level; no end-to-end test (N-10). The above-threshold case with an expired effect is trivially covered since every return at 60 s or more shows the summary. |
| (c) camera drag at the edges versus the Android back gesture | **A swipe starting at x=60 px (about 23 dp) is taken by the system, not the camera**: after it the grid x did not change and the gesture pill appeared (`android-phone-edge-1-x60.png`); an x=3 swipe is also system-owned. The T247 evidence records that x=0 panned the camera by about 50 px. With the fullscreen immersive start the first edge swipe only reveals the bars, so a drag that starts at the edge and is meant for the camera is lost once and then works. No exclusion region is warranted; finding for the owner only: a real finger just inside the system edge region can still pan the camera. NB. |
| (d) Remove tap ignored once after HOME and return | **Not reproduced.** Three trials on the T247 build: select the Miner, HOME, relaunch, wait 1.5 s, tap Remove: the "Remove Miner?" dialog opened every time with Cancel focused (`android-phone-home-return-1..3-after-remove-tap.png`, identical red-button pixel counts). No Welcome-back modal intervened. The earlier ignore remains unexplained; a likely cause is a tap that arrived while the summary was still being suppressed or before the activity regained focus (not proven). Recommendation: keep it open as a low-risk note, do not block. |

### Owner playtest notes (T248 to T250)

* **Research scroll (T248)**: drag-pan and wheel verified in the Windows player at 1920x1080 (N-4); 10 PlayMode tests pass.
* **Stable Machine panel (T249)**: the layout reserves its text rows and moves the blocker section below the actions (N-8); 89 tests pass. I could not run the player long enough to drive the store to 100 percent (a Miner takes minutes per percent), so the visual stability at a full store is evidenced by the tests only.
* **Desktop focus (T250)**: with the Windows player minimized for 40 s the store fill advanced from 6.0 to 9.9 percent (about the rate before) and on restore no Welcome-back modal appeared (`desktop-focus-before.png`, `desktop-focus-after-40s-minimized.png`); `RunDesktopLifecycleTests` 6 of 6. Alt-Tab itself was not driven, the minimize path shares the focus-loss handler. Runs on the desktop platform seam; mobile classes unchanged (`RunLifecycleTests` 7 of 7).

### Items e and f

**(e) Reserved lines and always-visible scrollbar.** Acceptable. The blank rows are one caption line each and the panel does not jump. The permanently visible scroll track is a mild false affordance when nothing scrolls, but it prevents the content from rewrapping when a scrollbar appears (the T249 cause) and is a positive cue when the blocker section needs scrolling. No accessibility harm: reserved space contains no focusable element and does not change tab order. Recommend, non-blocking: show a thumb-less track in a quiet colour and make the reserved rows no larger than the measured worst case (already the design).

**(f) Blocker below the fold at 200 percent text on small screens.** Non-blocking for SC-007 (no control is clipped or obscured: everything is reachable by scroll and every focused control scrolls into view) and the status line near the top already says "Waiting: ..." with the cause. It does weaken the corrective hint (UX-10), which now sits after Remove. Recommendation: keep the T249 layout and add a one-line pointer in the status row when the blocker section is below the visible area, for example "Waiting: see below", or move only the one-line blocker headline above the actions in a reserved row and keep the hint text below. Owner decision, not required for acceptance.

### Unresolved risks

* F-9 stays open (no decision).
* All device evidence is emulator-only; the APK used predates T248 to T250 (mobile behaviour unaffected by them). No physical phone, tablet notch or real font scale was tested.
* Alt-Tab and a full store were not driven by hand (see above).
* The synthetic window-size captures are not a phone-size claim.

**Final review verdict: PASS WITH FINDINGS**
