---
title: F005 session log and dev time acceleration (planned)
type: decision
sources: [S022]
updated: 2026-10-21
---

# F005 session log and dev time acceleration (planned)

**Planned (F005), not shipped.** Source: research F5-R11, F5-R12, F5-R18 (SEC, OPS, PF items), owner decisions D-I, D-M, D-N. (S022)

## Session log

- Core: port `IRunEventSink` with a no-op default and readonly allowlisted event structs. Samples at timeline minutes 10, 15, 30, 60, 90, 120 and at milestones.
- Unity adapter `SessionLogWriter`: in-memory ring buffer, hand-written JSON lines with CRC32 (no reflection JSON, IL2CPP), flush at lifecycle points and at most every 30 s, two rotating ~128 KB segments under `persistentDataPath/IdleFactory/SessionLog/`, try/catch with a drop counter. Injected root, prefs and file layer make it testable.
- Setting in `PlayerPrefs`, default on. Session id from `RandomNumberGenerator`. Every written string is a content ID or enum token (SEC-1).
- Export writes `Exports/session-log-<utc>.jsonl` via temp file and rename, keeps at most 10, shows the path on screen only. Android: `adb pull`, no share intent, no FileProvider; backup rules exclude `SessionLog/` and `Exports/`, verified in the merged manifest by `scripts/check-release-build.ps1`.
- UI home: a fourth `MarketPanelHost` section `session-log-section`, because the shell has no settings surface; F004 relocates it. (S022)

## DevTools

- Assembly `IdleFactory.DevTools` with `defineConstraints: ["IDLEFACTORY_DEV"]`, never referenced back. The define comes from `BuildPlayerOptions.extraScriptingDefines`, never ProjectSettings (no settings noise).
- `IdleFactory.Unity` exposes a null-default `ITimeScaleSource`; the overlay offers 1, 10, 60, 600, Max with a permanent "ACCELERATED xN" marker. The host clamps a scaled delta to 10 simulated seconds per frame.
- Dev builds use `IdleFactory-Dev/` as save root (D-N). Accelerated saves are not comparable and are marked only in the log (D-I). When the accelerated timeline runs ahead of wall time, the next offline credit is 0.
- Release proof: the scan looks for DevTools names in a Windows Managed list and in the Android `global-metadata.dat`. `TimeScalePlayerTests` stay out of the tagged StandalonePlayer suite. (S022)

Related: [F005 plan overview](./f005-plan-overview.md), [desktop focus lifecycle](./desktop-focus-lifecycle.md), [run persistence](./run-persistence.md), [unity player validation](./unity-player-validation.md).

## Batch 1 status (merged 2026-10-13)

- **DevTools seam (T308):** `IDLEFACTORY_DEV` comes only from `extraScriptingDefines` (dev variants in `PlatformBuildCommands`, plus `BuildAndroidArm64Release`). `PersistenceRoots.Resolve` returns the `IdleFactory-Dev/` save root under the define. In `FactoryRuntimeHost` the real delta is accounted, then scaled, then clamped to S_max = 10 s; the clamp applies only at scale > 1, and with no source the path is unchanged. (S022)
- **Release scan (T309):** `scripts/check-release-build.ps1` scans the Windows `Managed/` folder or IL2CPP metadata and the Android `global-metadata.dat` for DevTools names, detects development builds through `boot.config`, and guards ProjectSettings. Exit 0 clean, 1 findings. (S022)
- **Event sink port (T307):** `IRunEventSink` has 11 void methods taking readonly event records; `NoOpRunEventSink` is the default. The sink is wrapped in a guard that swallows sink exceptions and counts them in `DroppedEvents`, but lets OutOfMemoryException propagate. A reflection test enforces the allowlist (content IDs, numbers, bools, enums, immutable lists). The session id is 128-bit. Nothing raises events yet; raise sites and the writer are T338. (S022)
- Workflow notes: [F005 batch 1 workflow pitfalls](./f005-batch-1-workflow-pitfalls.md).

## Batch B status (merged 2026-10-21)

The writer (T338), the Core raise sites (T364), the Market panel section and the DevTools overlay placement (T340/T361) and the Android backup rules (T339) are merged: see [session log writer](./f005-session-log-writer.md) and [UI and Android](./f005-batch-b-ui-and-android.md). (S022)
