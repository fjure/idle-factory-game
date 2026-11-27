# Quickstart: validating Feature 007

This guide covers running and checking each milestone's evidence. Details live in the [contracts](./contracts/) and the [data model](./data-model.md), and are not repeated here.

## Prerequisites

- Unity `6000.6.0f1` and a clean worktree.
- Leases, using `scripts/resource-lease.ps1`:
  - `unity` for any Unity CLI run;
  - `unity,emulator` when the active target may be Android, and for **any** `adb` use, including the physical phone (research R16);
  - `gpu` for Blender, ComfyUI and frame-time runs.
- The physical phone is connected through `adb`. Its record is in `reviews/devices-f007.md`.
- `uv` and the pinned art-pipeline toolchain (`scripts/test-art-pipeline.ps1` must not return exit 3).

## Suites (every task)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1
```

Expected: Edit Mode and Play Mode pass, with a non-zero Play Mode test count (workstation pitfall: a run that reports 0 tests is invalid).

```powershell
powershell -NoProfile -File scripts/test-art-pipeline.ps1
```

Expected: exit 0.

```powershell
powershell -NoProfile -File scripts/check-settings-noise.ps1 -Commit <sha>
```

Expected: OK.

## M1 — audit

1. Build the F006 look on `main` (development APK and Windows player) and run the capture rig for the four classes and both factory states.
2. Run the dead-area and machine-area scripts on the captures. They produce the baseline values for SC-004 and SC-006 in `reviews/audit-f007.md`.
3. Run the phone baseline (ABA, plus the 10-minute sustained run) and record it in `reviews/baseline-device.md`.

**Expected**: every FR-006 part is present, and every F004 rule has a per-direction disposition.

## M2 — prototype and comparison

1. Build a development APK with `IDLEFACTORY_DEV`. In the DevTools overlay, select each look: `F006`, `F007-A`, `F007-B`, `F007-C`.
2. **Determinism.** Run `LookProfileDeterminismParityTests`. Expected: identical simulation snapshots for every profile.
3. **Release isolation.** Build a release APK and run the build-report check. Expected: no file under `Assets/IdleFactory/Art/F007/Prototype/` in the build.
4. **Captures.** Run the comparison capture for every matrix cell. The sheet generator must report **0 missing cells**.
5. **Trials and rubric.** Independent sessions run blind naming, silhouettes and material prompts. The owner runs the timed trial. Reviewers score the rubric against the frozen anchors.
6. **Performance.** Measure on the phone, the emulator and the Windows player per R17, including the page-cap re-measurement for compressed pages (R13).

**Expected**: `reviews/performance-m2.md` labels every number as measured or estimated, and the C feasibility spike result is recorded before the full C prototype.

## M3 — decision

Review `reviews/direction-brief.md`. The owner's decision is recorded in `reviews/direction-decision.md`. No M5 Issue exists before that file's commit (SC-013); check with `gh issue list --label maqa --state all` and git history.

## M4–M6

These steps are defined after M3 (research R01). Each M5 family merge must show:

- its before/after sheet;
- green suites;
- the emulator journey;
- the budget check against budget set v2 (FR-035).

M6 reruns the comparison matrix on the final build. The owner records a verdict per surface and class in `reviews/final-acceptance.md`.
