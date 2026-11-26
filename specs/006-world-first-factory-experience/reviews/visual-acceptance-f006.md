# F006 visual acceptance: before/after sheets (T549)

Evidence only. This record contains no verdict: the owner gives the visual acceptance per surface and screen class (T549, FR-042, SC-012). Task T549 is parent-owned; nothing in `tasks.md` or any Issue was changed.

- **Before** = the T502 baseline of `origin/main` c568d24, unchanged: `reviews/baseline/png/` (rig captures, 10 classes, 100 % and 200 %) and `reviews/baseline/device/` (real emulator, fresh run, 100 %, portrait and landscape).
- **After** = fresh captures of `ac49f6c8` (final F006 main, merge of PR #791), Unity 6000.6.0f1.
- Every sheet is at most 4000 px on its long side (C-102) and saved as a 256-colour PNG (the raw captures under `visual-acceptance-f006/raw/` are lossless).

## Method and commands

| Family | What | How |
|---|---|---|
| Rig (Windows and desktop classes, emulator-size classes) | The T502 `BaselineRig` (real shell, real panels, real slot grid and camera framing) with the F006 surfaces composed as the bootstrap composes them: machine card, build card with the ghost preview, Details, Office hub (the rig of T542 had no card; it is composed here exactly like `BuildCardWorldTests.Make`). 10 classes x 100/200 % x 7 states. | Temporary capture-only Play Mode test `T549RigCaptureTests` (env `IDLEFACTORY_T549_OUT`), run with `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/PlayMode/F006/T549RigCaptureTests.cs -TimeoutSeconds 3000` (10 tests passed). The test file is NOT committed (see Risks); its source is kept as `tools/art-pipeline/trials/f006/T549RigCaptureTests.cs.txt`. The rig has no world dressing, deck or stage art in the camera render beyond what the slot grid draws; the real scene is covered by the Windows player and the device captures below. |
| Windows player, real scene | Stage triptych (Early / Partial / Established per FR-036) and the P3 stills (docked card, upgrade marker, ghost, hub dot) at the 8 Windows classes, by the T545 driver (`F006TrialCapturePlayerTests.CaptureAll`, forced per-slot visuals on the packaged scene, `ScreenCapture` read-back, `Screen.SetResolution`, no host input). | `python tools/art-pipeline/trials/f006/t549_spec.py <spec.json>` then `IDLEFACTORY_T545_SPEC=<spec> IDLEFACTORY_T545_OUT=<dir> scripts/test-unity.ps1 -StandaloneWindowsPlayer -TimeoutSeconds 3000` (85 player tests passed). Windows runs at 96 dpi (1 px per point). |
| Emulator, real APK | Real game on AVD `IdleFactory_Test_API_36` (native 1080x2400 at 420 dpi), driven by `adb input` only, `adb exec-out screencap`. Offline summary = a real return: the committed W003-S shipped set (departure far in the past, credit capped at 8 h) pushed by `scripts/android-emulator-journey.ps1 -Scenario F006 -Steps Preflight,W003Push`, then a cold start. Landscape via `adb emu sensor set acceleration 9.81:0:0`, 200 % via `settings put system font_scale 2.0`. | `BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect` after deleting `Library/Bee/Android`: APK 46,723,142 B, SHA-256 `697fcaf0e0e1aec97129559a44059ba7485a5fdcefc1cab953a25e5d26721c9d`, log `profiler autoconnect False`, `Build Finished, Result: Success.`. The journey script ran the F006 steps (portrait, 100 %): select, upgrade, hub, Contracts, build, rotation. The remaining taps were scripted by hand from screenshots (coordinates are per screenshot; the raw files are the record). |
| Emulator, capture APK | Stage triptych and P3 stills on the device, portrait and landscape. | `IdleFactory.Editor.F006EmulatorCaptureBuild.Build` (T562 capture build, test assemblies, scripting define `IDLEFACTORY_T562_CAPTURE`), APK 48,636,807 B, SHA-256 `e183ad89c1116374d37e67897a77b63c327c877abe2c9d74e0b0d29ef88d1ffd`; spec `t549_spec.py --emu` pushed with adb after launch, images pulled from `files/t562-out`. Class "emulator" in the spec; the window is the device's own (rotation by sensor). |

Sheets are built by `tools/art-pipeline/trials/f006/t549_sheets.py` (rig and triptychs) and `t549_device_sheets.py` (device and look points). The art-pipeline chain of `scripts/test-unity.ps1` ran as part of the player run and passed (`378 passed`, `ART_PIPELINE_PASS`).

## State mapping (before -> after)

| State | Before (T502) | After |
|---|---|---|
| no panel | `no-panel` | no panel (world view, nothing selected) |
| card | `machine-sheet` | machine card docked next to its machine (stopped smelter, location 4 on the rig; location 4/5 on the device) |
| details | `machine-sheet` | the card's Details expanded (Details view) |
| building | `build-sheet` | empty-slot build flow: build card with a type selected and the ghost preview in the world |
| hub | `contracts` | Office hub (persistent entry opened) |
| system view | `contracts` | the Contracts view as reached from the hub. On the W003-S fixture and in the rig Contracts is locked or on offer (see the sheet); a populated Contracts view with offers and a delivery is in `sheets/moments-contract.png` (W005-L checkpoint, portrait) |
| offline summary | `offline-summary` | offline summary ("Welcome back") |
| reduced motion | `no-panel` / `machine-sheet` | same state with reduced motion On; a still where motion differs: `sheets/reduced-motion-emulator-portrait.png` and `reduced-motion-full-frames.png` (device) |
| 200 % text | the `*-200` baseline images | the same states at 200 %, rows 2 of each rig sheet and `device-sheet-emulator-*-200-*` |
| stage triptych | no before (new in F006) | Early / Partial / Established side by side per class (`triptych-*.png`) |

## Class x state matrix: rig and Windows (before left, after right; top row 100 %, bottom row 200 %)

| Class | no panel | card | details | building | hub | system view | offline summary | overview | stage triptych (real scene, Windows player) | P3 stills |
|---|---|---|---|---|---|---|---|---|---|---|
| phone-portrait-360x640 | [sheet](sheets/sheet-phone-portrait-360x640-no-panel.png) | [sheet](sheets/sheet-phone-portrait-360x640-card.png) | [sheet](sheets/sheet-phone-portrait-360x640-details.png) | [sheet](sheets/sheet-phone-portrait-360x640-building.png) | [sheet](sheets/sheet-phone-portrait-360x640-hub.png) | [sheet](sheets/sheet-phone-portrait-360x640-system-view.png) | [sheet](sheets/sheet-phone-portrait-360x640-offline-summary.png) | [overview](sheets/overview-phone-portrait-360x640.png) | [triptych](sheets/triptych-phone-portrait-360x640.png) | [P3 stills](sheets/p3-stills-phone-portrait-360x640.png) |
| phone-landscape-640x360 | [sheet](sheets/sheet-phone-landscape-640x360-no-panel.png) | [sheet](sheets/sheet-phone-landscape-640x360-card.png) | [sheet](sheets/sheet-phone-landscape-640x360-details.png) | [sheet](sheets/sheet-phone-landscape-640x360-building.png) | [sheet](sheets/sheet-phone-landscape-640x360-hub.png) | [sheet](sheets/sheet-phone-landscape-640x360-system-view.png) | [sheet](sheets/sheet-phone-landscape-640x360-offline-summary.png) | [overview](sheets/overview-phone-landscape-640x360.png) | [triptych](sheets/triptych-phone-landscape-640x360.png) | [P3 stills](sheets/p3-stills-phone-landscape-640x360.png) |
| phone-portrait-390x844 | [sheet](sheets/sheet-phone-portrait-390x844-no-panel.png) | [sheet](sheets/sheet-phone-portrait-390x844-card.png) | [sheet](sheets/sheet-phone-portrait-390x844-details.png) | [sheet](sheets/sheet-phone-portrait-390x844-building.png) | [sheet](sheets/sheet-phone-portrait-390x844-hub.png) | [sheet](sheets/sheet-phone-portrait-390x844-system-view.png) | [sheet](sheets/sheet-phone-portrait-390x844-offline-summary.png) | [overview](sheets/overview-phone-portrait-390x844.png) | [triptych](sheets/triptych-phone-portrait-390x844.png) | [P3 stills](sheets/p3-stills-phone-portrait-390x844.png) |
| phone-landscape-844x390 | [sheet](sheets/sheet-phone-landscape-844x390-no-panel.png) | [sheet](sheets/sheet-phone-landscape-844x390-card.png) | [sheet](sheets/sheet-phone-landscape-844x390-details.png) | [sheet](sheets/sheet-phone-landscape-844x390-building.png) | [sheet](sheets/sheet-phone-landscape-844x390-hub.png) | [sheet](sheets/sheet-phone-landscape-844x390-system-view.png) | [sheet](sheets/sheet-phone-landscape-844x390-offline-summary.png) | [overview](sheets/overview-phone-landscape-844x390.png) | [triptych](sheets/triptych-phone-landscape-844x390.png) | [P3 stills](sheets/p3-stills-phone-landscape-844x390.png) |
| tablet-portrait-768x1024 | [sheet](sheets/sheet-tablet-portrait-768x1024-no-panel.png) | [sheet](sheets/sheet-tablet-portrait-768x1024-card.png) | [sheet](sheets/sheet-tablet-portrait-768x1024-details.png) | [sheet](sheets/sheet-tablet-portrait-768x1024-building.png) | [sheet](sheets/sheet-tablet-portrait-768x1024-hub.png) | [sheet](sheets/sheet-tablet-portrait-768x1024-system-view.png) | [sheet](sheets/sheet-tablet-portrait-768x1024-offline-summary.png) | [overview](sheets/overview-tablet-portrait-768x1024.png) | [triptych](sheets/triptych-tablet-portrait-768x1024.png) | [P3 stills](sheets/p3-stills-tablet-portrait-768x1024.png) |
| tablet-landscape-1024x768 | [sheet](sheets/sheet-tablet-landscape-1024x768-no-panel.png) | [sheet](sheets/sheet-tablet-landscape-1024x768-card.png) | [sheet](sheets/sheet-tablet-landscape-1024x768-details.png) | [sheet](sheets/sheet-tablet-landscape-1024x768-building.png) | [sheet](sheets/sheet-tablet-landscape-1024x768-hub.png) | [sheet](sheets/sheet-tablet-landscape-1024x768-system-view.png) | [sheet](sheets/sheet-tablet-landscape-1024x768-offline-summary.png) | [overview](sheets/overview-tablet-landscape-1024x768.png) | [triptych](sheets/triptych-tablet-landscape-1024x768.png) | [P3 stills](sheets/p3-stills-tablet-landscape-1024x768.png) |
| desktop-1280x720 | [sheet](sheets/sheet-desktop-1280x720-no-panel.png) | [sheet](sheets/sheet-desktop-1280x720-card.png) | [sheet](sheets/sheet-desktop-1280x720-details.png) | [sheet](sheets/sheet-desktop-1280x720-building.png) | [sheet](sheets/sheet-desktop-1280x720-hub.png) | [sheet](sheets/sheet-desktop-1280x720-system-view.png) | [sheet](sheets/sheet-desktop-1280x720-offline-summary.png) | [overview](sheets/overview-desktop-1280x720.png) | [triptych](sheets/triptych-desktop-1280x720.png) | [P3 stills](sheets/p3-stills-desktop-1280x720.png) |
| desktop-1920x1080 | [sheet](sheets/sheet-desktop-1920x1080-no-panel.png) | [sheet](sheets/sheet-desktop-1920x1080-card.png) | [sheet](sheets/sheet-desktop-1920x1080-details.png) | [sheet](sheets/sheet-desktop-1920x1080-building.png) | [sheet](sheets/sheet-desktop-1920x1080-hub.png) | [sheet](sheets/sheet-desktop-1920x1080-system-view.png) | [sheet](sheets/sheet-desktop-1920x1080-offline-summary.png) | [overview](sheets/overview-desktop-1920x1080.png) | [triptych](sheets/triptych-desktop-1920x1080.png) | [P3 stills](sheets/p3-stills-desktop-1920x1080.png) |
| emulator-portrait-1080x2400 | [sheet](sheets/sheet-emulator-portrait-1080x2400-no-panel.png) | [sheet](sheets/sheet-emulator-portrait-1080x2400-card.png) | [sheet](sheets/sheet-emulator-portrait-1080x2400-details.png) | [sheet](sheets/sheet-emulator-portrait-1080x2400-building.png) | [sheet](sheets/sheet-emulator-portrait-1080x2400-hub.png) | [sheet](sheets/sheet-emulator-portrait-1080x2400-system-view.png) | [sheet](sheets/sheet-emulator-portrait-1080x2400-offline-summary.png) | [overview](sheets/overview-emulator-portrait-1080x2400.png) | gap (see Gaps 3) | none (emulator classes: device sheets) |
| emulator-landscape-2400x1080 | [sheet](sheets/sheet-emulator-landscape-2400x1080-no-panel.png) | [sheet](sheets/sheet-emulator-landscape-2400x1080-card.png) | [sheet](sheets/sheet-emulator-landscape-2400x1080-details.png) | [sheet](sheets/sheet-emulator-landscape-2400x1080-building.png) | [sheet](sheets/sheet-emulator-landscape-2400x1080-hub.png) | [sheet](sheets/sheet-emulator-landscape-2400x1080-system-view.png) | [sheet](sheets/sheet-emulator-landscape-2400x1080-offline-summary.png) | [overview](sheets/overview-emulator-landscape-2400x1080.png) | gap (see Gaps 3) | none (emulator classes: device sheets) |

All 70 rig sheets, 8 Windows triptychs and 8 Windows P3 still sets are done. The stage triptych has no before. Tablet classes were captured as well (the T502 baseline has them).

## Class x state matrix: emulator (real APK; BEFORE rig, BEFORE device where T502 captured it, AFTER device, AFTER rig)

| Scale | no panel | card | details | building | hub | system view | offline summary | overview |
|---|---|---|---|---|---|---|---|---|
| emulator portrait 100 % | [sheet](sheets/device-sheet-emulator-portrait-100-no-panel.png) | [sheet](sheets/device-sheet-emulator-portrait-100-card.png) | [sheet](sheets/device-sheet-emulator-portrait-100-details.png) | [sheet](sheets/device-sheet-emulator-portrait-100-building.png) | [sheet](sheets/device-sheet-emulator-portrait-100-hub.png) | [sheet](sheets/device-sheet-emulator-portrait-100-system-view.png) | [sheet](sheets/device-sheet-emulator-portrait-100-offline-summary.png) | [overview](sheets/device-overview-emulator-portrait-100.png) |
| emulator portrait 200 % | [sheet](sheets/device-sheet-emulator-portrait-200-no-panel.png) | [sheet](sheets/device-sheet-emulator-portrait-200-card.png) | [sheet](sheets/device-sheet-emulator-portrait-200-details.png) | [sheet](sheets/device-sheet-emulator-portrait-200-building.png) | [sheet](sheets/device-sheet-emulator-portrait-200-hub.png) | [sheet](sheets/device-sheet-emulator-portrait-200-system-view.png) | [sheet](sheets/device-sheet-emulator-portrait-200-offline-summary.png) | [overview](sheets/device-overview-emulator-portrait-200.png) |
| emulator landscape 100 % | [sheet](sheets/device-sheet-emulator-landscape-100-no-panel.png) | [sheet](sheets/device-sheet-emulator-landscape-100-card.png) | [sheet](sheets/device-sheet-emulator-landscape-100-details.png) | [sheet](sheets/device-sheet-emulator-landscape-100-building.png) | [sheet](sheets/device-sheet-emulator-landscape-100-hub.png) | [sheet](sheets/device-sheet-emulator-landscape-100-system-view.png) | [sheet](sheets/device-sheet-emulator-landscape-100-offline-summary.png) | [overview](sheets/device-overview-emulator-landscape-100.png) |
| emulator landscape 200 % | [sheet](sheets/device-sheet-emulator-landscape-200-no-panel.png) | [sheet](sheets/device-sheet-emulator-landscape-200-card.png) | [sheet](sheets/device-sheet-emulator-landscape-200-details.png) | [sheet](sheets/device-sheet-emulator-landscape-200-building.png) | [sheet](sheets/device-sheet-emulator-landscape-200-hub.png) | [sheet](sheets/device-sheet-emulator-landscape-200-system-view.png) | [sheet](sheets/device-sheet-emulator-landscape-200-offline-summary.png) | [overview](sheets/device-overview-emulator-landscape-200.png) |

Emulator stage triptychs (capture APK): [portrait](sheets/triptych-emulator-portrait.png), [landscape](sheets/triptych-emulator-landscape.png). All 28 device state sheets are done (4 scale/orientation combinations x 7 states).

## Look points (final-acceptance.md section 2 rows 2 and 8, C-108)

| Look point | Where | Notes |
|---|---|---|
| Smallest phone 360x640 and 640x360, upgrade marker | [sheets/lookpoints-smallest-phone.png](sheets/lookpoints-smallest-phone.png) (real APK, `wm size 360x640` / `640x360`, density 160, so 1 pt = 1 px) and the P3 upgrade-marker tiles of `p3-stills-phone-*` (Windows, 1 px per point) | 360x640 100 %: the upgrade chevrons over the miners are drawn at this size on the device. The marker-per-24-pt cutoff of C-98 is a property of pixels per point; the device captures use 1.0 (density 160) and the emulator's native class is 2.6 px per point. |
| Deck edge / void on each class | the no-panel and triptych sheets of every class (the deck and the void around it are in the real scene captures: triptych, device sheets) | The rig sheets have no deck art. |
| Development label over Confirm at 640x360 200 % | [sheets/lookpoints-smallest-phone.png](sheets/lookpoints-smallest-phone.png), panel "640x360 200 %: build card, Confirm" | Observed: the "Development Build" watermark (development APKs only) is drawn over the lower right of the Confirm button in that frame; it also overlaps the Overdrive control at 360x640 (card open) and the lower right corner of the Welcome back card. Release builds do not carry it. |
| Tablet hint wrap | [sheets/lookpoints-tablet-hint.png](sheets/lookpoints-tablet-hint.png) (fresh run, `wm size 768x1024` and `1024x768`, density 160) | The hint reads "Tap an empty location to build, or a machine to inspect it." in the side panel. |
| 48-pt touch-target overlay crop | not derived | No per-control size dump from the device was taken; the rig JSON holds `worldBound` of the persistent elements only. Gap. |

Play-through stills for the GD/PO moments:

| Moment | Sheet | Notes |
|---|---|---|
| First upgrade | [moments-upgrade.png](sheets/moments-upgrade.png) | Real Upgrade tap on the card of a level-25 smelter (W003-S set), not a first-ever upgrade of a fresh run: the pushed sets are mid-game. |
| A hand-delivered contract | [moments-contract.png](sheets/moments-contract.png) | W005-L checkpoint: Accept the Gear contract, Deliver by hand, "Delivered 167: +$9,898, +8 RP." |
| A stage change | not captured live | A stage change needs a factory unlock in a running session; no fixture steps over a threshold within the capture. The three stages are shown by the triptychs (forced slot states on the real scene, as in T545). Gap. |
| A real offline return | `device-sheet-emulator-*-offline-summary.png`, [moments-contract.png](sheets/moments-contract.png) (first panel: return with a contract auto-delivered) | Real cold starts of pushed checkpoints on the device. The returns credit the 8 h cap (pushed far-past departure), except the landscape 200 % one, a real 2 minute absence. No emulator clock was changed. |

## Guidance figures (T542 / baseline.md next to the sheets)

Measured again on this run by the same probe (`ShellGeometryProbe.Measure` through the rig, no panel open, zoom buttons on, hub composed; before = `baseline/<class>.json` of c568d24, after = `raw/rig/<class>.json`). Guidance values steer design and are not gates; the T542 section of `baseline.md` holds the full tables and the interaction counts (SC-003: upgrade 3, one stopped machine 1, every stopped machine 4, systems reach 3 counted as Office entry + card + Close, build 3), which were not re-run here and are cited from T542.

SC-001 world share / chrome share, SC-010 persistent labels (guidance: world >= 60 %, chrome <= 15 % (20 % at 200 %), labels <= 6). Cells read before / after.

| Class | World 100 % | Chrome 100 % | Labels 100 % | World 200 % | Chrome 200 % | Labels 200 % |
|---|---|---|---|---|---|---|
| phone-portrait-360x640 | 61.8 / 62.4 | 22.0 / 21.3 | 13 / 10 | 35.6 / 41.9 | 42.8 / 34.1 | 13 / 10 |
| phone-landscape-640x360 | 51.6 / 54.6 | 35.7 / 35.3 | 13 / 10 | 27.7 / 36.0 | 62.0 / 55.8 | 13 / 10 |
| phone-portrait-390x844 | 67.9 / 68.4 | 17.0 / 16.5 | 13 / 10 | 44.9 / 50.0 | 33.2 / 26.4 | 13 / 10 |
| phone-landscape-844x390 | 58.0 / 63.9 | 32.1 / 25.8 | 13 / 10 | 43.6 / 46.7 | 43.8 / 40.8 | 13 / 10 |
| tablet-portrait-768x1024 | 41.4 / 41.2 | 7.1 / 4.6 | 13 / 10 | 34.5 / 32.5 | 11.7 / 8.9 | 13 / 10 |
| tablet-landscape-1024x768 | 50.6 / 50.3 | 8.9 / 6.5 | 13 / 10 | 44.1 / 44.0 | 13.7 / 8.3 | 13 / 10 |
| desktop-1280x720 | 57.2 / 56.9 | 8.5 / 6.7 | 13 / 10 | 51.1 / 51.0 | 12.4 / 8.2 | 13 / 10 |
| desktop-1920x1080 | 70.6 / 70.3 | 5.3 / 4.6 | 13 / 10 | 66.3 / 66.2 | 7.1 / 5.3 | 13 / 10 |
| emulator-portrait-1080x2400 | 71.7 / 72.1 | 14.1 / 13.6 | 13 / 10 | 50.7 / 55.0 | 27.4 / 21.8 | 13 / 10 |
| emulator-landscape-2400x1080 | 62.8 / 70.5 | 28.3 / 20.9 | 13 / 10 | 49.8 / 56.2 | 38.4 / 33.0 | 13 / 10 |

SC-002 (machine card open; unobscured world area vs no panel, before = machine sheet, after = card; guidance 50 % phone portrait, 70 % phone landscape and tablet), before / after:

| Class | 100 % | 200 % |
|---|---|---|
| phone-portrait-360x640 | 0.4626 / 0.7361 | 0.5326 / 0.5481 |
| phone-landscape-640x360 | 0.5534 / 0.7948 | 0.7143 / 1.0256 |
| phone-portrait-390x844 | 0.445 / 0.7798 | 0.4846 / 0.6937 |
| phone-landscape-844x390 | 0.5361 / 0.8096 | 0.6717 / 0.8255 |
| tablet-portrait-768x1024 | 1 / 1 | 1 / 1 |
| tablet-landscape-1024x768 | 1 / 1 | 1 / 1 |
| desktop-1280x720 | 1 / 1 | 1 / 1 |
| desktop-1920x1080 | 1 / 1 | 1 / 1 |
| emulator-portrait-1080x2400 | 0.4838 / 0.8224 | 0.4641 / 0.7651 |
| emulator-landscape-2400x1080 | 0.5283 / 0.7455 | 0.6536 / 0.882 |

## Gaps and limits

1. **Rig is not the real scene.** The rig camera render has no deck or stage art; before and after use the same rig, so they compare like with like. The real scene is shown by the Windows player (triptychs, P3 stills) and the device.
2. **Windows classes: only the triptych and P3 stills come from the real scene.** The T502 classes 360x640, 390x844, 640x360, 844x390, 768x1024, 1024x768, 1280x720 and 1920x1080 have no real-scene "after" for the other states, because the T545 driver forces slot visuals and P3 shots only; a Windows player capture of no panel, hub, system view, offline summary or 200 % would need new C# in the capture driver. Not done (task rule: prefer no C# changes).
3. **Emulator-size triptych for the rig classes `emulator-portrait-1080x2400` and `emulator-landscape-2400x1080`:** captured on the device instead (`triptych-emulator-*.png`), so those two rows in the matrix link no Windows triptych.
4. **Device before is a fresh run**, not the baseline fixture (T502 said so). The device after uses the committed W003-S set (11 machines, level 25 smelters). Before/after device frames therefore differ in content; the rig rows are the like-for-like pair.
5. **Contracts on the W003-S device set is locked** ("Contracts are locked"). The system-view device sheets show that real state; the populated view is in `moments-contract.png`. 200 % hub and system view at portrait and landscape are likewise the locked state.
6. **The device set has Overdrive in different states:** the portrait 200 % Details frame was taken with Overdrive on (a stray tap during navigation; the pink markers and "Overdrive On" label are real). Other frames show Overdrive Ready; later portrait 200 % frames show "Cool" in the Overdrive control.
7. **Offline summaries on the device are far-past returns** (credit capped at 8 h, "Away for 26,583 h"), except the landscape 200 % one (2 minutes away).
8. **Reduced motion** is shown on the device only (portrait 100 %, no panel): looping machine animation differs between frames with motion (3,150 and 4,410 changed pixels) and is identical with reduced motion On (0 and 0). Other states with reduced motion were not captured. Settings was left on "Reduced motion On" in the app data, which was uninstalled afterwards.
9. **Window sizes on the device** for the look points used `wm size` and `wm density` overrides (reset afterwards).
10. **Hub label text** differs between the rig (Contracts "Nothing ready to deliver") and the device fixture ("Locked: ..."): content, not layout.
11. **Stage change, 48-pt overlay crop:** see the look point tables.

## Device state after the run

`wm size` and `wm density` reset (1080x2400, 420), `font_scale` 1.0, auto-rotate on, user rotation 0, the game uninstalled from the AVD (no other app touched), the emulator and the adb server stopped. Leases `unity,emulator` were not released (parent).
