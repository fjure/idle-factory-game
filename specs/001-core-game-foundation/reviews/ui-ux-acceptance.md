# UI/UX Acceptance — T036

- **Date:** 2026-09-25
- **Unity:** 6000.6.0f1
- **Environment:** Unity Editor Play Mode test runner on Windows 11; automated viewport and safe-area simulation.

## Evidence and scope

The automated evidence exercises the authored HUD UXML/USS and actual UI Toolkit controls. It calls the HUD layout operation with each viewport size and a simulated asymmetric safe area, verifies the responsive class and resolved flex direction, and checks the resulting inset padding. It verifies focus and `NavigationSubmitEvent` activation on the Build, Confirm, Cancel, and Zoom buttons, and minimum dimensions/focusability for every HUD, inspector, and persistence control carrying `factory-hud__control`.

No physical Android or iOS device, operating-system safe area, touchscreen, or hardware keyboard was used. Viewport sizes, safe areas, focus, and submit events are automated editor simulations; this evidence does not claim device-level visual or input acceptance.

## Viewport and safe-area matrix

Every row uses a simulated safe-area rectangle with 17 px left, 25 px right, 23 px bottom, and 39 px top insets. The test asserts that the resolved HUD padding matches those values and verifies the responsive class's applied flex direction.

| Viewport | HUD layout | Applied flex direction | Result |
|---|---|---|---|
| 360×640 | Compact portrait | Column | PASS |
| 640×360 | Compact landscape | Column | PASS |
| 390×844 | Compact portrait | Column | PASS |
| 844×390 | Compact landscape | Column | PASS |
| 768×1024 | Tablet portrait | Column | PASS |
| 1024×768 | Tablet landscape | Row | PASS |
| 1280×720 | Desktop | Row | PASS |
| 1920×1080 | Desktop | Row | PASS |

## Interaction, target size, and state evidence

- All 14 current `factory-hud__control` buttons (Build/placement/zoom, machine inspector, and persistence) are focusable, have non-empty text, and resolve to minimum width and height of 44 UI units.
- Keyboard-style navigation submit events on the actual Build, Confirm, Cancel, Zoom In, and Zoom Out buttons activate the expected callbacks exactly once. The actual UXML `persistence-reset` and `persistence-reload` buttons are queried, temporarily enabled in the isolated HUD fixture, focused, and each invokes a `clicked` probe exactly once. This probes input activation only; the integrated persistence load test separately sends submit to the actual Load button.
- Focused UI Toolkit controls have a visible focus rule in the attached USS (white border plus changed background). Runtime focus ownership is asserted on the actual controls.
- Normal/recovery, empty, disabled, and error states expose explanatory text in the HUD labels; disabled state also disables Confirm. These cues remain available without interpreting color.
- Loading is covered by `PersistenceUiTests.LoadButtonShowsLoadingBeforeReadingAndApplyingTheSavedFactory`: the loading label and feedback render, Load disables, and the summary stays hidden until the operation completes. `PersistenceUiTests.RecoveryButtonShowsLoadingBeforeReadingAndPromotingThePreviousGeneration` covers the corresponding recovery loading feedback.

## Test runs

Unity version is read from `ProjectSettings/ProjectVersion.txt` and confirmed by the Editor log. The repository runner ran sequentially:

| Command | Result | Evidence |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -TestFile "Assets/IdleFactory/Tests/PlayMode/NavigationPlacementTests.cs"` | PASS, 9/9 passed, 0 skipped | `test-results/unity/20260925T144244816Z/PlayMode-results.xml` and `PlayMode.log` |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1` | PASS, Edit Mode 545/545 and Play Mode 74/74, 0 skipped in either suite | `test-results/unity/20260925T144303392Z/EditMode-results.xml`, `EditMode.log`, `PlayMode-results.xml`, and `PlayMode.log` |

The successful runs used an approved process-scoped elevated invocation because Unity could not start its local Package Manager IPC in the normal sandbox. The project and persistent proxy configuration were not changed.

## Verdicts

- **Automated responsive/accessibility evidence:** PASS for the simulated viewport, safe-area, control, and state checks listed above.
- **Independent UI/UX Reviewer:** PASS — independently reviewed commit `0b537456ae53bec05172855d3e46af6dec29b496` on 2026-09-25; the reviewer was not the implementation author. The review confirmed the actual UXML `persistence-reset` and `persistence-reload` buttons receive focus and each produces exactly one click from `NavigationSubmitEvent` (`NavigationPlacementTests.cs:397–412`), alongside the viewport/safe-area, target-size, non-color state, and responsive evidence above.
- **Independent Game Designer/Product Owner:** PASS — independently reviewed the same exact commit on 2026-09-25 and confirmed the fix satisfies Quickstart keyboard-activation acceptance.
- The Reset/Reload probe proves submit-to-click activation for those controls; it does not test persistence command execution. Device-level evidence remains outside this review.
- **Device-level visual/input acceptance:** Not performed; no physical-device claim is made.

## T053 — portrait-phone HUD overlap remediation (Issue #82, finding F2)

- **Date:** 2026-09-25
- **Unity:** 6000.6.0f1, Editor Play Mode test runner via Unity MCP; Game view `t052 640x480`.
- **Traces:** FR-004, FR-005, SC-005 (partial).
- **Status:** implementation evidence only. Independent UI/UX Reviewer, Test/QA and Game Designer/Product Owner verdicts are pending.

### Cause

The Browse-mode, Machine inspector and Save-and-return panels were drawn on top of each other for two reasons.

1. **The UIDocument root had zero height.** In Unity 6000.6 the `UIDocument.rootVisualElement` resolves to the full panel width but zero height. The absolutely positioned `.factory-hud` (`top: 0; bottom: 0`) therefore collapsed to its padding. The side-panels and action bar were squeezed to 0 px, and every card overflowed at the same top edge. The information, inspector and persistence cards started about 20 points apart, with their text drawn over the next card. This happened in the editor too (Game view 640x480: document root `1097x0` points, `factory-hud` 17 points tall). The T036 checks did not catch it because they sized the document root explicitly.
2. **Screen pixels were treated as panel points.** `CoreGameFoundationBootstrap` passed `Screen.width`, `Screen.height` and `Screen.safeArea` in pixels to `FactoryHudDocument.ApplyLayout`, which chooses the screen class and insets in panel points. On the phone, 1080x2400 px was classified as *tablet portrait*, and pixel insets were applied as points.

A third, smaller defect showed up once the HUD had its real size. At `sw411dp` and compact-landscape heights, `max-height: 40%` squeezed the information panel below its wrapped text height, so the labels overflowed into the inspector.

### Fix

- `FactoryHudDocument.Build` pins the document root to the panel (`position: absolute`, all insets `0`).
- New `FactoryHudDocument.ApplyScreenLayout(screenWidth, screenHeight, screenSafeArea)` converts screen pixels to panel points with the panel's `scaledPixelsPerPoint`. It then applies the unchanged T036 layout rules (`ApplyLayout`, in points). The bootstrap's safe-area call now uses it. This one-line change is in the non-camera `ApplySafeAreaLayout` method.
- USS changes:
  - The information panel and its labels no longer shrink.
  - The inspector scroll view absorbs the height shortage and keeps a minimum height of 44 points.
  - The compact-landscape side-panel cap is raised from 60% to 72%.
  - No new colours, fonts, tokens or visual language.

Every declared screen class, the safe-area inset handling, the focus order, the 44-point targets and the text (non-colour) state cues from T036 are unchanged. The T036 tests pass without modification.

### Automated layout assertion

`HudLayoutOverlapTests.HudPanelsDoNotOverlapAndStayInsideTheSafeArea` renders the authored UXML/USS into an off-screen panel. The panel has the real pixel size and pixels-per-point for each class, and the test drives it through `ApplyScreenLayout` with pixel safe areas, the same way the bootstrap does. The test checks both Browse mode and Placement preview mode, with a long selection string and rejection feedback. It asserts the following:

- the expected screen class;
- the information panel, inspector/persistence scroll panel and action bar are non-empty and pairwise non-intersecting;
- all three panels are inside the safe area;
- information and action-bar content is inside its own panel;
- no label or button is squeezed below its measured wrapped-text height;
- the Machine inspector and Save-and-return cards do not intersect;
- the scroll viewport is at least 44 points tall;
- every visible `factory-hud__control` resolves to at least 44x44 points.

| Screen class case | px/pt | Safe-area insets px (L/R/T/B) | Class | Overlap |
|---|---|---|---|---|
| phone-portrait `sw411dp` 1080x2400 @420 dpi | 4.375 | 0/0/136/63 | Compact portrait | PASS |
| phone-landscape `sw411dp` 2400x1080 @420 dpi | 4.375 | 136/63/0/0 | Compact landscape | PASS |
| phone-portrait `sw411dp` 1080x2400, emulator-observed scale | 2.09 | 0/0/136/63 | Compact portrait | PASS |
| phone-landscape `sw411dp` 2400x1080, emulator-observed scale | 2.09 | 136/63/0/0 | Desktop (1148 pt wide) | PASS |
| 360x640 | 1 | 17/25/39/23 | Compact portrait | PASS |
| 640x360 | 1 | 17/25/39/23 | Compact landscape | PASS |
| 390x844 | 1 | 17/25/39/23 | Compact portrait | PASS |
| 844x390 | 1 | 17/25/39/23 | Compact landscape | PASS |
| 768x1024 | 1 | 17/25/39/23 | Tablet portrait | PASS |
| 1024x768 | 1 | 17/25/39/23 | Tablet landscape | PASS |
| 1280x720 | 1 | 17/25/39/23 | Desktop | PASS |
| 1366x768 | 1 | 17/25/39/23 | Desktop | PASS |
| 1920x1080 | 1 | 17/25/39/23 | Desktop | PASS |

**Red before the fix:** on the unchanged layout, all 11 initial cases failed. The phone cases failed on the screen class (TabletPortrait and Desktop instead of the compact classes). Every other case failed on "Machine inspector / Save-and-return scroll panel is collapsed" (for example, 360x640: HUD root 62 pt tall, side-panels 0 pt, cards at y = 39, 59 and 79). After the root and pixel fixes, four cases were still red because the information panel's "Browse mode" label was squeezed (phone portrait 14.6 < 18.1 pt; phone landscape 8.5 pt; 640x360 13 pt; 844x390 15 pt). The USS change turned them green.

### Emulator evidence (emulator only — no physical-device claim)

The Android ARM64 IL2CPP OpenGLES3-only Development APK was built with `BuildPipeline.BuildPlayer` from the working tree of this change (`test-results/android/t053/CoreGameFoundation-t053-gles3.apk`, SHA-256 `491569f2…c519c`, not committed). It ran on AVD `Medium_Phone_API_36.0` (1080x2400, 420 dpi, `sw411dp`, portrait) after `pm clear`.

| State | Result | Screenshot |
|---|---|---|
| Cold start, Browse mode | PASS: Browse-mode, Machine inspector and Save-and-return cards are stacked without overlap. All text is readable, and the action bar (Build, Zoom −, Zoom +) is separate at the bottom. | [`t053-evidence/emulator-sw411dp-portrait-browse.png`](t053-evidence/emulator-sw411dp-portrait-browse.png) |
| Build tapped, Placement preview mode | PASS: the "Cannot place…" feedback is shown as text, and the disabled Confirm is visually de-emphasised as well as described in text. Confirm, Cancel and Zoom share one row without overlap. Cancel returned to Browse mode. | [`t053-evidence/emulator-sw411dp-portrait-placement.png`](t053-evidence/emulator-sw411dp-portrait-placement.png) |
| Landscape | Not verified. Setting `user_rotation` with autorotation off did not rotate the app (same limitation as T038). The landscape classes are covered only by the automated layout assertion. | — |

The emulator player resolved about 2.09 px per point, estimated from the 460-point side-panel width. The device has no status-bar inset in this fullscreen player. Both the 4.375 (420/96) and 2.09 scales are covered by the automated cases.

### Suites

Full suites were run through Unity MCP `run_tests` with the Game view at `t052 640x480` and StandaloneWindows64 as the active build target, after the Android build target and Graphics API override were restored: Edit Mode **546/546** passed and Play Mode **92/92** passed, 0 skipped. The Play Mode count includes the 13 new `HudLayoutOverlapTests` cases. The console had no relevant errors, only MCP transport messages.

The `CoreGameFoundationSceneTests.AuthoredSceneRunsPlacementInputAndViewRecreationWithoutChangingWorldOwnership` failure seen at Game views of 1366 px or wider during #81 ("Visible HUD controls must retain UI pointer ownership") was this HUD defect. The collapsed, `overflow: hidden` action bar clipped the Build button, so it could not be picked in the desktop row layout. At 1366x768 that assertion now passes. The same test then fails on a later, camera-only assertion ("Desktop drag input must pan the authored factory camera"). At 16:9 with orthographic size 9, `FactoryCameraController.ClampPosition` centres X, because the viewport is wider than the 16-cell factory plus padding. That is camera framing, owned by Issue #83/T054, and it is not changed here.

### Review remediation: duplicated status line (2026-09-26)

The independent UI/UX review of `4cc3d5a` rejected the Browse-mode evidence above. The screenshot draws "Development-only D001 factory is ready." twice in the information panel. This is a real presentation defect, not a capture artefact. `FactoryHudDocument.SetState` writes the state message to both the `status` and the `feedback` label. The defect predates T053; it was hidden while the panel was collapsed.

- **Fix:** `FactoryHudDocument` hides the `feedback` label (`display: none`) whenever its text is empty or identical to the status line, after both `SetState` and `SetPlacementMode`. Both labels keep their text, so the T036 text-cue assertions are unchanged. Placement feedback that differs from the status line (for example "Cannot place…") stays visible.
- **Test:** `NavigationPlacementTests` now asserts that a state message leaves the `feedback` label hidden and the `status` label visible, and that differing placement feedback is shown.
- **Suites:** Edit Mode **546/546** and Play Mode **92/92** passed, 0 skipped, through Unity MCP with the Game view at 640x480. The console had no errors.
- **Emulator:** Android ARM64 IL2CPP OpenGLES3-only Development APK `test-results/android/t053-fix/CoreGameFoundation-t053-fix-gles3.apk` (SHA-256 `ab8b5003…29eb0`, not committed). It was built with `BuildPipeline.BuildPlayer`, and the Android Graphics API override was restored afterwards. It ran on the same AVD after a cold boot and `pm clear`.

| State | Result | Screenshot |
|---|---|---|
| Cold start, Browse mode | PASS: the status line is drawn once. The three cards are stacked without overlap, and all text is readable. | [`t053-evidence/emulator-sw411dp-portrait-browse-fix.png`](t053-evidence/emulator-sw411dp-portrait-browse-fix.png) |
| Build tapped, Placement preview mode | PASS: "Cannot place: Footprint overlaps an existing building." is shown once as text above the single status line. There is no overlap. | [`t053-evidence/emulator-sw411dp-portrait-placement-fix.png`](t053-evidence/emulator-sw411dp-portrait-placement-fix.png) |

The earlier screenshots are kept as the record that the UI/UX review rejected.

## T054 — initial camera framing of the D001 scenario (Issue #83, finding F3)

- **Date:** 2026-09-26
- **Unity:** 6000.6.0f1, Editor test runner via Unity MCP. Active build target StandaloneWindows64.
- **Traces:** FR-004, FR-005, FR-028, SC-005 (partial).
- **Status:** implementation evidence only. Independent UI/UX Reviewer, Test/QA, Software Architect and Game Designer/Product Owner verdicts are pending.

### Precondition: the machine is present, not missing

A Development APK built from `main` at `4813562` (T053 merged) was cold-started on AVD `Medium_Phone_API_36.0` (1080x2400, 420 dpi, `sw411dp`, portrait) after `pm clear`. At launch, only the empty grid is visible ([`t054-evidence/emulator-sw411dp-portrait-before-launch.png`](t054-evidence/emulator-sw411dp-portrait-before-launch.png)). After two touch pans to the left, the canonical D001 2×2 `dev.machine.basic` appears at grid (0,0) with its RUN badge ([`t054-evidence/emulator-sw411dp-portrait-before-panned.png`](t054-evidence/emulator-sw411dp-portrait-before-panned.png)). F3 is therefore off-frame content, not a missing machine.

### Cause

The bootstrap centred the orthographic camera (size 9) on the 16×16 factory, at (8, 8). In portrait, the visible width is only about 8 cells (x 3.95–12.05), so the machine at x 0–2 was outside the frame. The pan limits were measured against the full viewport and ignored the HUD. A corner of the factory could therefore stay under the side panels or the action bar even at the pan limit. On wide screens the whole factory width fits, so the camera was fixed in X. That is why the desktop drag assertion in `CoreGameFoundationSceneTests` failed at 1366 px and wider.

### Fix (camera and HUD presentation only)

- `FactoryHudDocument.TryGetUnobstructedScreenRect` returns the screen-pixel rectangle inside the safe area that the side panels and the action bar leave uncovered. It tries both removal orders and prefers the rectangle with the longest shorter side, because a thin strip cannot show a building. Element references are cached, so there is no per-frame allocation.
- `FactoryCameraController.SetUnobstructedScreenRect` records that area. `ClampPosition` then lets every padded factory edge reach it, and it centres the factory in that area when the factory is smaller. `FrameContent` centres a world rectangle in the uncovered area as far as the pan limits allow. The zoom is unchanged.
- The bootstrap passes the uncovered area to the camera whenever it changes. Once the HUD layout has resolved, it frames the occupied cells of the current factory one time. Factory state is only read (`GetFactorySnapshot`), never mutated.
- `CoreGameFoundationSceneTests` now drags diagonally away from the lower pan limit. A purely horizontal drag has no room when the factory width already fits the uncovered area. The assertion still proves that mouse and touch drags pan the camera.

### Automated evidence

- `InitialCameraFramingTests.InitialFramingShowsTheD001MachineOutsideTheHud` renders the authored HUD and a camera at the real pixel size for all 13 declared screen-class cases (the T053 matrix). It asserts that the D001 machine footprint is drawn completely inside the uncovered area and that the zoom stays at the authored size 9. Before the pan-limit change, all 13 cases were red. The machine sat under the action bar or the side panels, for example 360x640 with the machine bottom at 36 px and the uncovered area starting at 71 px. The phone-landscape case also stayed red while the uncovered area was chosen by area (a 73 px strip).
- `InitialCameraFramingTests.PanLimitsLetEveryFactoryCornerReachTheUncoveredArea` checks all four corner cells in all 13 cases.
- `CoreGameFoundationSceneTests.AuthoredSceneFramesTheD001MachineOutsideTheHudWithoutChangingFactoryState` loads the authored scene and checks four things:
  - the machine is present at (0,0);
  - it is drawn inside the uncovered area at launch;
  - with production paused, re-running the initial framing leaves the `SemanticFactorySnapshot` checksum and the snapshot revision unchanged;
  - only the camera moves.
- Suites at a 640x480 Game view: Edit Mode **546/546** and Play Mode **119/119**, 0 skipped. Play Mode also passed 119/119 at a 1468x714 Game view, including the formerly failing desktop drag assertion. The console had no errors, only MCP transport messages.

### Emulator evidence (emulator only — no physical-device claim)

Android ARM64 IL2CPP OpenGLES3-only Development APK `test-results/android/t054/CoreGameFoundation-t054-gles3.apk` (SHA-256 `c7951641…0dae3f`, not committed). It was built from the working tree of this change. Afterwards, the Graphics API override was restored and the active build target was switched back to StandaloneWindows64. It ran on the same AVD after `pm clear`.

| State | Result | Screenshot |
|---|---|---|
| Cold start, no input | PASS: the D001 machine with its RUN badge is fully visible at launch, below the HUD cards and above the action bar. | [`t054-evidence/emulator-sw411dp-portrait-launch.png`](t054-evidence/emulator-sw411dp-portrait-launch.png) |
| One tap on the machine | PASS: the machine is selected, with a blue outline. The Machine inspector shows its state (Blocked, output full), recipe, progress, buffers and commands. | [`t054-evidence/emulator-sw411dp-portrait-inspect.png`](t054-evidence/emulator-sw411dp-portrait-inspect.png) |
| Landscape | Not verified on the emulator, which does not rotate (T055). Covered by the automated screen-class cases. | — |

The machine sits at the lower-left of the uncovered area, not in its centre. It is a corner building, and the pan limits allow one cell of padding beyond the factory edge. This is the intended limit.

Out of scope, for the Art Director: the state badges are wider than the 2×2 sprite and are clipped ("RUN", "SLE", "OUT!").

## T059 — Landscape HUD column overflow (Issue #97, T058-F1)

- **Date:** 2026-09-27
- **Unity:** 6000.6.0f1. Editor test runner via Unity MCP (Game view 640x480, active build target StandaloneWindows64) and the configured CLI runner `scripts/test-unity.ps1`.
- **Traces:** FR-004, FR-005, SC-005 (partial).
- **Status:** implementation evidence only. Independent UI/UX Reviewer and Test/QA verdicts are pending.

### Reproduction

T058 recorded the finding on the Android player at `sw411dp` in landscape (2400x1080): with the Machine inspector and the Durable offline return summary both shown, the left column covered the Browse-mode header and ran past the bottom edge ([`t059-evidence/emulator-sw411dp-landscape-before-t058-f1.png`](t059-evidence/emulator-sw411dp-landscape-before-t058-f1.png)).

The same state reproduced in the Editor at a 2400x1080 Game view. The panel resolved 1.6875 px per point, and the HUD used the Desktop class. The resolved layout was correct: the inspector scroll view was clamped to 311.7 pt below the 104.9 pt information panel. The content inside it, however, was drawn at its full height. Once scrolled, it drew over the Browse-mode header and past the scroll view's lower edge. The overlap therefore came from rendering, not from the layout, and it was not limited to landscape. It occurs at every screen class whenever the inspector content is taller than its viewport. Landscape phones show it first because their viewport is the shortest.

### Cause

`CoreGameFoundationPanelSettings` loads no theme style sheet (`themeUss` is empty). The ScrollView therefore gets none of Unity's default theme layout. Its content viewport does not clip (`overflow: hidden` comes from the theme), and the vertical scroller is laid out below the viewport at zero height. The project rule meant to handle this, `.factory-hud__inspector-scroll > .unity-scroll-view__content-viewport`, never matched. In Unity 6000.6 the viewport is a grandchild of the ScrollView, inside `unity-content-and-vertical-scroll-container`, so neither its `min-height: 0` nor any clipping ever applied.

The existing T053 assertions compare layout rectangles only, so they passed while the content escaped its viewport. They also covered the 4.375 and 2.09 px/pt scales, but not the scale the authored panel settings actually produce. `ScaleWithScreenSize` with a 1280x720 reference and match 0.5 gives about 1.68 px/pt on the `sw411dp` phone in either orientation.

### Fix (HUD presentation only)

- `CoreGameFoundation.uss`: descendant selectors now give the inspector scroll view the layout that the missing theme would supply. The content-and-scroller container lays out as a row, and the viewport grows, shrinks and clips with `overflow: hidden`.
- A slim vertical scroll indicator shows that more content follows: a 6 px track with a proportional thumb, and the arrow buttons hidden. It is a visual cue, not a required touch target. Scrolling is by touch drag, mouse wheel or focus. Non-color cue: the thumb's length and position show how much content there is and where the view is.
- `FactoryHudDocument` scrolls the inspector to a control that receives keyboard or gamepad focus while clipped (`FocusInEvent` → `ScrollView.ScrollTo`). Focus order is unchanged.
- No layout percentages, safe-area insets, touch-target sizes, focus order, domain state or persistence behavior changed.

### Automated evidence

`HudLayoutOverlapTests` gains a **maximum content** state: the longest simultaneous HUD content. It shows a selected, blocked machine with 36-character instance IDs and multi-line blockers, placement-rejection feedback, READY persistence with the checkpoint actions enabled, and a Durable offline return summary with limitation reasons. The test runs this state for every declared screen class and applies these checks:

- all T053 assertions still apply: no panel intersections, all panels inside the safe area, no squeezed labels, the inspector and Save-and-return cards do not stack, the viewport is at least 44 pt tall, and every control is at least 44x44 pt;
- **new clipping assertion:** at scroll positions 0 %, 50 % and 100 %, a hit test at each information-panel label reaches that label. A hit test at any inspector or Save-and-return label or button outside the viewport must not reach it. Hit testing uses the same clip rectangles as rendering, so escaping content fails the test;
- **new focus assertion:** focusing `persistence-reset` from the top and `inspector-supply-input` from the end scrolls the focused control fully into the viewport;
- **new reachability assertion:** scrolling brings the last inspector action, the last persistence action and the offline summary into the viewport, or brings the summary's end into view when it is taller than the viewport.

`MaximumContentStaysReadableAndReachableAfterRuntimeRotation` lays out every phone case in the opposite orientation, fills in the maximum content and then rotates into the declared class. That is the order in which the player receives a configuration change. It then applies the same assertions.

New scale cases: the phone in portrait and landscape at the authored ScaleWithScreenSize scale (1.677 px/pt). Landscape resolves to the Desktop class (1431 pt wide).

| Stage | Result |
|---|---|
| New assertions on the unchanged USS | **Red, 21/21.** For example, landscape at 1.677 px/pt: "persistence-description escapes the inspector viewport (x:81.1, y:134.8, w:400.1, h:314.3)"; 640x360: "inspector-process escapes the inspector viewport". |
| Clipping fix, before focus scrolling | Clipping green. The focus assertion was **red in 19/21 cases**, for example "focused 'persistence-reset' is not scrolled into view" (compact-landscape 640x360: control at y 530, viewport ends at 254). |
| Final | **Green, 21/21** (`HudLayoutOverlapTests`, 15 screen classes plus 6 rotated phone cases). |
| Configured suites, CLI `scripts/test-unity.ps1` | Edit Mode **557/557**, Play Mode **131/131**, 0 skipped. Unity MCP runs at a 640x480 Game view gave the same totals, and the console showed no errors. |

### Emulator evidence (emulator only — no physical-device claim)

Android ARM64 IL2CPP OpenGLES3-only Development APK `test-results/android/t059/CoreGameFoundation-t059-gles3.apk` (SHA-256 `0844efae…8ce0be`, not committed). It was built from the working tree of this change with `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3`. It ran on AVD `Medium_Phone_API_36.0` (1080x2400, 420 dpi, `sw411dp`) after a cold boot with `-gpu host -no-snapshot-load` and `pm clear`. Rotation used the T055 method: `cmd window user-rotation free`, then `emu sensor set acceleration`. The app process (PID 3932) stayed the same through both rotations.

| State | Result | Screenshot |
|---|---|---|
| Portrait, machine selected | PASS: the Browse-mode header is fully readable. The inspector is clipped at the viewport's lower edge, and the scroll indicator shows more content below. | [`t059-evidence/emulator-sw411dp-portrait-inspector-selected.png`](t059-evidence/emulator-sw411dp-portrait-inspector-selected.png) |
| Portrait, touch-drag scroll | PASS: the content scrolls under the header without drawing over it, and Save and return is reachable. "Save now" was then tapped (READY, "Checkpoint saved."). | [`t059-evidence/emulator-sw411dp-portrait-touch-scrolled.png`](t059-evidence/emulator-sw411dp-portrait-touch-scrolled.png) |
| Runtime rotation to landscape (2400x1080), inspector + Durable offline return | PASS: this is the T058-F1 state. The header is not overlapped, the column ends at the viewport and nothing is clipped at the screen edge. Selection and inspector state are kept. | [`t059-evidence/emulator-sw411dp-landscape-after-rotation.png`](t059-evidence/emulator-sw411dp-landscape-after-rotation.png) |
| Landscape, scrolled to the end | PASS: the full Durable offline return summary, including "Limiting reasons", is readable inside the viewport. | [`t059-evidence/emulator-sw411dp-landscape-summary-end.png`](t059-evidence/emulator-sw411dp-landscape-summary-end.png) |
| Runtime rotation back to portrait, scrolled to the end | PASS: the header, persistence actions and the full summary are readable. No overlap, and the action bar stays separate. | [`t059-evidence/emulator-sw411dp-portrait-after-rotation-summary-end.png`](t059-evidence/emulator-sw411dp-portrait-after-rotation-summary-end.png) |

Observations:

- At the viewport's lower edge, a sub-pixel antialiasing sliver (about 1 px) of the next clipped glyph row can show. No text is readable there, and it does not take taps.
- The logcat Unity errors are the known Development-build messages (the Play asset-pack class is absent, and the profiler falls back to listen mode). No exception came from project code.
- The UI/UX question from T058 remains a Product/UI decision outside T059: should the Durable offline return summary appear after rotation-induced returns in player-facing builds?
