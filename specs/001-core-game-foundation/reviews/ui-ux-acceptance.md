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
- **Independent UI/UX reviewer:** Pending review of the exact committed implementation. The implementation author does not self-approve this required gate.
- **Device-level visual/input acceptance:** Not performed; no physical-device claim is made.
