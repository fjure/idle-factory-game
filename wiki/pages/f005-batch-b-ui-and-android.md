---
title: F005 batch B UI, DevTools overlay and Android backup rules
type: component
sources: [S022, S030]
updated: 2026-11-06
---

# F005 batch B UI, DevTools overlay and Android backup rules

T332/T333 (#397/#398), T361 (#464), T362 (#465), T339 (#404), T340 (#405), gate #408. (S022)

## Goals and shell
- Goals snapshot (T332): `AllReached` is true once all content milestones pass, even before the threshold, so the "All goals reached" text must also require `Reached`/`FactorySaleAvailable`. (S022)
- Goals line (T333): the state-bearing accessible name belongs on the focusable `goals-exact`, not the non-focusable label. Owner: "Factory Sale #(completed prestiges + 1)", rising past the maximum index, "Next sale needs B". (S022)
- T362: `ScrollIntoView` scrolls only a ScrollView whose content container holds the target and `FocusWhenReady` aborts once focus moved (F8); visible "Cash", "Store", "Overdrive" labels (F9; 360 px phone portrait needed reduced strip padding); locked Sell rows "Locked: unlocks after ..."; focus moves to a visible sibling before `goals-exact` hides. (S022)

## DevTools overlay (T361, T340)
- F7: the overlay `PanelSettings` needs a runtime theme style sheet (created once, destroyed in `OnDestroy`, no serialized asset); x1/x10/x60/x600/Max with an "ACCELERATED xN" marker. (S022)
- F6: batch-mode UI Toolkit drops device pointer events, so the automated hold test sends panel pointer events through the real session. The device leg closed only by a manual real-input hold: **gate passed 2026-10-21 (#408)**, 8 s on Mine gave about +1 Ore/s beyond two Miners (FR-042). (S022)
- **Finding from that gate:** in 16:9 windows (1280x720, 1920x1080) the bottom-centre overlay row covered `factory-ore-vein`, making Mine unreachable. T340 moved it top-left below the top strip with placement tests at every QA-12 screen class and 200 % text. (S022)
- DevTools test recipe: add a temporary `Assets/csc.rsp` with `-define:IDLEFACTORY_DEV`, run Unity `-runTests -testPlatform PlayMode -assemblyNames IdleFactory.DevToolsTests`, then delete `csc.rsp` and its `.meta` and restore `ProjectSettings`. (S022)

## Session-log section and F1 (T340)
Fourth Market section via `ISessionLogControls` and a bootstrap accessor: toggle, export with a selectable "Last export" line, clear with a rotation-cancelled confirmation, empty/error/disabled states. `AutoDeliverWasOn` lives on the Core `ExpiredContract` snapshot, set from the persisted `autoDeliver` at expiry, so the short-stock line survives restart and offline return; the expired card itself stays unpersisted (no Save V3 member, no schema bump). (S022)

## Android backup rules (T339)
- Unity 6 rejects `Assets/Plugins/Android/res`; rules ship as `Assets/Plugins/Android/IdleFactoryBackupRules.androidlib` (library manifest, res/xml, minimal build.gradle). Owner accepted the deviation; no FileProvider and no main/launcher Gradle template (D-M). Risk: SDK levels hard-coded in the library build.gradle. (S022, S030)
- Persistence root is `<persistentDataPath>/IdleFactory`; rules exclude `SessionLog` and `Exports` (also `IdleFactory-Dev/`) in file and external domains. `check-release-build.ps1` reads the merged manifest with aapt2 and fails closed without it. (S030)
- Open: the Android test player uses Vulkan and crashes in the emulator, so PF-7 Android calibration and an overlay smoke are deferred to T351 (needs a GLES3 test-player path); the journey script read a stale `Checkpoints/v1` path (game writes `IdleFactory/Checkpoints/v2`), failing 4 Home/Lock checks (#416). T351 fixed the path and ran the journey; PF-7 calibration and the overlay smoke stay open because no DevTools plus GLES3 Android variant exists, see [batch D journeys](./f005-batch-d-journeys.md). (S022, S030, S037)

Related: [run panels](./run-panels.md), [Android emulator validation](./android-emulator-validation.md), [session log writer](./f005-session-log-writer.md), [batch C UI and DevTools fixes](./f005-batch-c-ui-and-devtools-fixes.md).
