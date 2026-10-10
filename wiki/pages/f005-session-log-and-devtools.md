---
title: F005 session log and dev time acceleration (planned)
type: decision
sources: [S022]
updated: 2026-10-10
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
