# F006 baseline (T502): current build, before any F006 change

**Task**: T502, FR-046, research R02. **Measured on**: `origin/main` c568d24 (Unity 6000.6.0f1, Windows editor Play Mode rigs). **Guidance** thresholds are SC-001, SC-002, SC-003 and SC-010 of `spec.md`; they steer design and are not gates (clarified 2026-11-11).

The numbers below are read from `reviews/baseline/<class>.json` (one file per screen class, both text scales, five states, nine journeys). They are the *before* values of every later comparison. Re-run: `IDLEFACTORY_F006_BASELINE_WRITE=1 scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/PlayMode/F006/BaselineMatrixTests.cs`.

## Method

- **Rig.** `BaselineRig` composes what the bootstrap composes: the real `RunShellDocument`, the real panels (build sheet, machine, Market, Contracts, Items, Research with Industry, Prestige), the `SlotGridView`, `RunShellPresenter` and the production camera framing (`FactoryCameraController`, `RunCameraFraming`), at each `ShellScreens.All()` class and at the emulator's pixel sizes. Settings is not composed (it has no tab).
- **Fixture.** Iron content, factory level 5 (12 slots), Cash 50000000, 3 miners (slots 1-3), 3 smelters (4-6), 1 press (7), 1 assembler (8), slots 9-12 empty, all contract offers unlocked, advanced 45 s. The run is the same in every class and scale.
- **Probe.** `ShellGeometryProbe` (self-tested on synthetic layouts in `ShellGeometryProbeTests`): world rect = `RunShellDocument.TryGetUnobstructedScreenRect`; chrome = union of the persistent elements' `worldBound` (`top-strip`, `overdrive`, `zoom-column`, `factory-ore-vein`, `detail-line`, `notice`, `world-hint`, `tab-bar`) clipped to the safe area, overlaps counted once; label count = visible `Label`s with non-blank text inside those elements (a button's own text is reported as `textElementCount`, not as a label). Shares are fractions of the safe area. `sheet` is the panel host (the open sheet on a phone, the side panel on tablet and desktop).
- **Journeys.** `InteractionCountJourney` dispatches `NavigationSubmitEvent` on the real controls (the event a click and Enter both reach a Button as; device pointer events are dropped in the batch editor) and `OpenSlot` for a tap on the floor. A press that raises a confirmation adds one more interaction. A journey counts only when its goal state was verified.
- **Emulator classes.** `emulator-portrait 1080x2400` and `emulator-landscape 2400x1080` are Play Mode rigs with the authored panel settings and a touch profile at 420 dpi (panel scale 2.6309, i.e. a 410x913 pt panel in portrait). Safe-area insets are 0 in the rig. The `png/` captures are rig renders; see the device section for the real emulator.
- **Screenshots.** `png/<class>-<state>-<scale>.png`, 100 files: five states x two scales x ten classes. Composite of the world (camera render) under the shell (panel cleared to transparent first, per the workstation pitfall about `clearColor`). Evidence only; the numbers come from `worldBound`.
- **Reproducible.** Two full runs produced byte-identical JSON and PNG files.

## SC-001 factory dominance (no panel open)

Guidance: world >= 60 % of the safe area, persistent chrome <= 15 % (<= 20 % at 200 % text). World share is the unobstructed rect the camera is given, not the pixels the 12-slot lattice covers; `lattice` is the lattice's bounding box on screen (the "ground and building area" while no ground art exists).

| Class | World 100 % | Chrome 100 % | Lattice 100 % | World 200 % | Chrome 200 % | Lattice 200 % | Meets 60 / 15 | Meets 60 / 20 at 200 % |
|---|---|---|---|---|---|---|---|---|
| phone-portrait 360x640 | 61.8 % | 22.0 % | 19.1 % | 35.6 % | 42.8 % | 19.1 % | no | no |
| phone-landscape 640x360 | 51.6 % | 35.7 % | 21.1 % | 27.7 % | 62.0 % | 20.9 % | no | no |
| phone-portrait 390x844 | 67.9 % | 17.0 % | 13.9 % | 44.9 % | 33.2 % | 13.9 % | no | no |
| phone-landscape 844x390 | 58.0 % | 32.1 % | 14.5 % | 43.6 % | 43.8 % | 14.5 % | no | no |
| tablet-portrait 768x1024 | 41.4 % | 7.1 % | 5.5 % | 34.5 % | 11.7 % | 5.5 % | no | no |
| tablet-landscape 1024x768 | 50.6 % | 8.9 % | 21.6 % | 44.1 % | 13.7 % | 5.4 % | no | no |
| desktop 1280x720 | 57.2 % | 8.5 % | 17.4 % | 51.1 % | 12.4 % | 17.4 % | no | no |
| desktop 1920x1080 | 70.6 % | 5.3 % | 30.9 % | 66.3 % | 7.1 % | 30.9 % | yes | yes |
| emulator-portrait 1080x2400 | 71.7 % | 14.1 % | 25.5 % | 50.7 % | 27.4 % | 14.4 % | yes | no |
| emulator-landscape 2400x1080 | 62.8 % | 28.3 % | 24.7 % | 49.8 % | 38.4 % | 13.9 % | no | no |

On tablet and desktop the side panel (`sheet`) is always present: at no panel it covers 18.8 % to 46.9 % of the safe area (tablet-portrait, desktop-1920) and is not counted as chrome above. Chrome plus side panel: tablet-portrait 100 % 54.0 %, desktop 1920x1080 100 % 24.0 %.

## SC-002 the machine stays visible (machine sheet open)

Guidance: selected machine 100 % visible; at least 50 % of the world area unobscured on phone portrait, 70 % on phone landscape and tablet. Unobscured = world rect with the sheet open / world rect with no panel.

| Class | Unobscured 100 % | Machine visible 100 % | Unobscured 200 % | Machine visible 200 % | Target | Meets 100 % | Meets 200 % |
|---|---|---|---|---|---|---|---|
| phone-portrait 360x640 | 46.3 % | 100.0 % | 53.3 % | 100.0 % | >= 50 % | no | yes |
| phone-landscape 640x360 | 55.3 % | 100.0 % | 71.4 % | 100.0 % | >= 70 % | no | yes |
| phone-portrait 390x844 | 44.5 % | 100.0 % | 48.5 % | 100.0 % | >= 50 % | no | no |
| phone-landscape 844x390 | 53.6 % | 100.0 % | 67.2 % | 100.0 % | >= 70 % | no | no |
| tablet-portrait 768x1024 | 100.0 % | 100.0 % | 100.0 % | 100.0 % | >= 70 % | yes | yes |
| tablet-landscape 1024x768 | 100.0 % | 100.0 % | 100.0 % | 100.0 % | >= 70 % | yes | yes |
| desktop 1280x720 | 100.0 % | 100.0 % | 100.0 % | 100.0 % | >= 70 % | yes | yes |
| desktop 1920x1080 | 100.0 % | 100.0 % | 100.0 % | 100.0 % | >= 70 % | yes | yes |
| emulator-portrait 1080x2400 | 48.4 % | 100.0 % | 46.4 % | 100.0 % | >= 50 % | no | no |
| emulator-landscape 2400x1080 | 52.8 % | 100.0 % | 65.4 % | 100.0 % | >= 70 % | no | no |

The build sheet and Contracts open the same sheet host, so their world rect equals the machine sheet's except where noted in the JSON (`contracts`, `build-sheet`).

## All five states, world share and sheet share (100 % / 200 % text)

| Class | no panel | machine sheet | Contracts | build sheet | offline summary (overlay card) |
|---|---|---|---|---|---|
| phone-portrait 360x640 | w 61.8 %, sheet 0.0 %<br>200 %: w 35.6 %, sheet 0.0 % | w 28.6 %, sheet 49.3 %<br>200 %: w 18.9 %, sheet 38.8 % | w 28.6 %, sheet 49.3 %<br>200 %: w 18.9 %, sheet 38.8 % | w 28.6 %, sheet 49.3 %<br>200 %: w 18.9 %, sheet 38.8 % | w 61.8 %, sheet 0.0 % / card 87.8 %<br>200 %: w 35.6 %, sheet 0.0 % |
| phone-landscape 640x360 | w 51.6 %, sheet 0.0 %<br>200 %: w 27.7 %, sheet 0.0 % | w 28.6 %, sheet 34.6 %<br>200 %: w 19.8 %, sheet 18.6 % | w 28.6 %, sheet 34.6 %<br>200 %: w 19.8 %, sheet 18.6 % | w 28.6 %, sheet 34.6 %<br>200 %: w 19.8 %, sheet 18.6 % | w 51.6 %, sheet 0.0 % / card 87.0 %<br>200 %: w 27.7 %, sheet 0.0 % |
| phone-portrait 390x844 | w 67.9 %, sheet 0.0 %<br>200 %: w 44.9 %, sheet 0.0 % | w 30.2 %, sheet 51.6 %<br>200 %: w 21.7 %, sheet 43.4 % | w 30.2 %, sheet 51.6 %<br>200 %: w 21.7 %, sheet 43.4 % | w 30.2 %, sheet 51.6 %<br>200 %: w 21.7 %, sheet 43.4 % | w 67.9 %, sheet 0.0 % / card 69.1 %<br>200 %: w 44.9 %, sheet 0.0 % |
| phone-landscape 844x390 | w 58.0 %, sheet 0.0 %<br>200 %: w 43.6 %, sheet 0.0 % | w 31.1 %, sheet 35.6 %<br>200 %: w 29.3 %, sheet 24.8 % | w 31.1 %, sheet 35.6 %<br>200 %: w 29.3 %, sheet 24.8 % | w 31.1 %, sheet 35.6 %<br>200 %: w 29.3 %, sheet 24.8 % | w 58.0 %, sheet 0.0 % / card 64.0 %<br>200 %: w 43.6 %, sheet 0.0 % |
| tablet-portrait 768x1024 | w 41.4 %, sheet 46.9 %<br>200 %: w 34.5 %, sheet 46.9 % | w 41.4 %, sheet 46.9 %<br>200 %: w 34.5 %, sheet 46.9 % | w 41.4 %, sheet 46.9 %<br>200 %: w 34.5 %, sheet 46.9 % | w 41.4 %, sheet 46.9 %<br>200 %: w 34.5 %, sheet 46.9 % | w 41.4 %, sheet 46.9 % / card 30.4 %<br>200 %: w 34.5 %, sheet 46.9 % |
| tablet-landscape 1024x768 | w 50.6 %, sheet 35.2 %<br>200 %: w 44.1 %, sheet 35.2 % | w 50.6 %, sheet 35.2 %<br>200 %: w 44.1 %, sheet 35.2 % | w 50.6 %, sheet 35.2 %<br>200 %: w 44.1 %, sheet 35.2 % | w 50.6 %, sheet 35.2 %<br>200 %: w 44.1 %, sheet 35.2 % | w 50.6 %, sheet 35.2 % / card 30.8 %<br>200 %: w 44.1 %, sheet 35.2 % |
| desktop 1280x720 | w 57.2 %, sheet 28.1 %<br>200 %: w 51.1 %, sheet 28.1 % | w 57.2 %, sheet 28.1 %<br>200 %: w 51.1 %, sheet 28.1 % | w 57.2 %, sheet 28.1 %<br>200 %: w 51.1 %, sheet 28.1 % | w 57.2 %, sheet 28.1 %<br>200 %: w 51.1 %, sheet 28.1 % | w 57.2 %, sheet 28.1 % / card 24.8 %<br>200 %: w 51.1 %, sheet 28.1 % |
| desktop 1920x1080 | w 70.6 %, sheet 18.8 %<br>200 %: w 66.3 %, sheet 18.8 % | w 70.6 %, sheet 18.8 %<br>200 %: w 66.3 %, sheet 18.8 % | w 70.6 %, sheet 18.8 %<br>200 %: w 66.3 %, sheet 18.8 % | w 70.6 %, sheet 18.8 %<br>200 %: w 66.3 %, sheet 18.8 % | w 70.6 %, sheet 18.8 % / card 11.0 %<br>200 %: w 66.3 %, sheet 18.8 % |
| emulator-portrait 1080x2400 | w 71.7 %, sheet 0.0 %<br>200 %: w 50.7 %, sheet 0.0 % | w 34.7 %, sheet 49.1 %<br>200 %: w 23.5 %, sheet 46.1 % | w 31.2 %, sheet 53.0 %<br>200 %: w 23.5 %, sheet 46.1 % | w 31.2 %, sheet 53.0 %<br>200 %: w 23.5 %, sheet 46.1 % | w 71.7 %, sheet 0.0 % / card 55.8 %<br>200 %: w 50.7 %, sheet 0.0 % |
| emulator-landscape 2400x1080 | w 62.8 %, sheet 0.0 %<br>200 %: w 49.8 %, sheet 0.0 % | w 33.2 %, sheet 37.1 %<br>200 %: w 32.6 %, sheet 26.3 % | w 33.2 %, sheet 37.1 %<br>200 %: w 32.6 %, sheet 26.3 % | w 33.2 %, sheet 37.1 %<br>200 %: w 32.6 %, sheet 26.3 % | w 62.8 %, sheet 0.0 % / card 52.6 %<br>200 %: w 49.8 %, sheet 0.0 % |

## SC-010 persistent text

Guidance: at most 50 % of the baseline label count. Baseline persistent labels with no panel open:

| Class | Labels 100 % | Text elements 100 % | Labels 200 % | by owner (100 %) |
|---|---|---|---|---|
| phone-portrait 360x640 | 13 | 15 | 13 | top-strip 6, factory-ore-vein 1, tab-bar 6 |
| phone-landscape 640x360 | 13 | 15 | 13 | top-strip 6, factory-ore-vein 1, tab-bar 6 |
| phone-portrait 390x844 | 13 | 15 | 13 | top-strip 6, factory-ore-vein 1, tab-bar 6 |
| phone-landscape 844x390 | 13 | 15 | 13 | top-strip 6, factory-ore-vein 1, tab-bar 6 |
| tablet-portrait 768x1024 | 13 | 15 | 13 | top-strip 4, overdrive 2, factory-ore-vein 1, tab-bar 6 |
| tablet-landscape 1024x768 | 13 | 15 | 13 | top-strip 4, overdrive 2, factory-ore-vein 1, tab-bar 6 |
| desktop 1280x720 | 13 | 15 | 13 | top-strip 4, overdrive 2, factory-ore-vein 1, tab-bar 6 |
| desktop 1920x1080 | 13 | 15 | 13 | top-strip 4, overdrive 2, factory-ore-vein 1, tab-bar 6 |
| emulator-portrait 1080x2400 | 13 | 15 | 13 | top-strip 6, factory-ore-vein 1, tab-bar 6 |
| emulator-landscape 2400x1080 | 13 | 15 | 13 | top-strip 6, factory-ore-vein 1, tab-bar 6 |

The label count is the same on every class (13); with a phone sheet open it is 12 (one label less). The 50 % target is therefore **<= 6 labels** (floor of 13 / 2). The 13 labels are: "Cash", "49.9M", "Store", "2%", "Overdrive", "Ready", "Mine", "Factory", "Market", "Contracts", "Items", "Research", "Prestige".

## SC-003 interactions (identical on every class and both text scales)

| Journey | Baseline interactions | Steps | SC-003 guidance |
|---|---|---|---|
| upgrade a machine and see the factory again | 3 | select the machine in slot 1; press Upgrade; press Close | <= 2 (baseline 3) |
| learn the limiting cause of one stopped machine | 1 | select the stopped machine in slot 4 | 1 tap |
| learn the limiting cause of every stopped machine | 4 | select the stopped machine in slot 4; select the stopped machine in slot 5; select the stopped machine in slot 6; press Close | 0 taps for the problem category |
| open Market and return to the factory | 2 | press the Market tab; press the Factory tab | <= 2 to reach, <= 1 to return |
| open Contracts and return to the factory | 2 | press the Contracts tab; press the Factory tab | <= 2 to reach, <= 1 to return |
| open Items and return to the factory | 2 | press the Items tab; press the Factory tab | <= 2 to reach, <= 1 to return |
| open Research and return to the factory | 2 | press the Research tab; press the Factory tab | <= 2 to reach, <= 1 to return |
| open Prestige and return to the factory | 2 | press the Prestige tab; press the Factory tab | <= 2 to reach, <= 1 to return |
| build a machine in an empty slot | 3 | select the empty slot 9; press a build card; press Close | <= 3 |

Checked: the counts are identical across all ten classes and both scales.

What the baseline says about the problem journeys:

- Upgrade takes 3 (select, Upgrade, Close), as the spec's baseline states. The upgrade does not raise a confirmation.
- The stopped state is visible without a tap (red badge in the world), but the *cause* is written only in the machine sheet: one tap per machine, so the 3 stopped machines of the fixture take 3 taps plus Close = 4 interactions. The evidence string in the JSON holds the sheet's text for the first stopped machine ("Missing Iron Ore: needs 2, has 0.").
- Every system is one tab press away and the Factory tab is one press back (1 + 1), already inside the SC-003 guidance.
- Building takes select, build card, Close = 3; the build sheet stays open after a build.
- Industry is not a tab (it is a section of Research) and Settings is not composed in the rig, so neither has an open-and-leave journey here.

## Deviations and limits of this baseline

- The rig is not the scene: the batch editor cannot give the scene a per-class Game view, so the shell is composed off-screen with the production classes. The rig applies the camera the way `ApplyRunViewport` does, in a reduced form (fit once, open the sheet on a selection, close it on dismissal).
- The persistent set is a named list of shell elements (see Method). A future element outside that list is not counted until it is added to `ShellGeometryProbe.PersistentNames`.
- The fixture holds 8 machines in 12 slots at Cash 50 M. A different fixture changes text (the strip shows `49.9M`) but not the structure counted here.
- `overdrive` appears as chrome only where it is displayed outside the top strip (side-panel layouts).
- Emulator-class insets are 0 in the rig; the real device has a status bar and a navigation bar (see below).

## Real emulator (evidence only)

- **Build**: `BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect` (log: `profiler autoconnect False`, IL2CPP ARM64, OpenGLES3 only), APK SHA-256 `8a33922d30e61532859b412d2486dbc9798e3c9f705d45c3afdc698e1135fd5a`, installed `base.apk` hash matched. The worktree's active build target was switched back to Standalone (`-buildTarget win64`) afterwards.
- **Device**: AVD `Medium_Phone_API_36.0`, `emulator-5554`, 1080x2400 at 420 dpi, cold boot with host GPU, one unscored launch after the install, no foreign `adb`/`monkey` process beforehand, no Unity editor running during the captures.
- **Install note**: the first install failed with `INSTALL_FAILED_INSUFFICIENT_STORAGE` (`/data` 92 % full, 506 MB free). The previous install's checkpoint folder (6 files, 12 KB) was pulled to the session scratchpad, the old package was uninstalled and the APK installed fresh. The device therefore started from a **fresh run** (Cash 37, four open slots in the first row, the other slots marked with their unlock level, hint "Build a Miner: tap an empty slot."), not from the baseline fixture. The device captures show the factory the first-time player sees; the Play Mode rows above use the fixed fixture.
- **Captures** (`reviews/baseline/device/`): `emulator-{portrait,landscape}-{no-panel,build-sheet,contracts}-100.png`, `adb exec-out screencap`, text scale 100 % (system font scale unchanged). Machine sheet, 200 % text and the offline summary were not captured on the device: a fresh run has no machine, the system font scale was not changed, and a "Welcome back" summary needs a long absence. Those states come from the rig.
- **What the device shows that the rig does not**: the guidance bar ("Build a Miner: tap an empty slot.") and a stacked lock notice are two more persistent elements on a fresh run (portrait: up to two bars of 49 pt each below the strip); in landscape the build sheet is a side panel (about half the width) with the world kept on the left; the "Development Build" watermark sits at the bottom-right in every capture. The notice and hint bars are counted by the probe when displayed (`detail-line`, `notice`, `world-hint`), but the rig run does not raise them because its run already has machines.
- **Cleanup**: `adb -s emulator-5554 emu kill` and `adb kill-server` were run; rotation was restored to auto-rotate (`accelerometer_rotation 1`, `user_rotation 0`).
