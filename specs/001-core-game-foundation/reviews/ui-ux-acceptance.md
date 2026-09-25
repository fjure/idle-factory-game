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
