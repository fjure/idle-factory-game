# Quickstart Validation: Core Game Foundation

This guide defines the runnable evidence expected after Feature 001 implementation. Commands and assets named here are implementation deliverables; their absence before the corresponding task is not a passing result.

## Prerequisites

- Repository checkout on `feat/001-core-game-foundation` or its implementation descendant
- Unity Editor `6000.6.0f1`
- Direct package pins for Input System `1.20.0`, Newtonsoft JSON `3.2.2` and Test Framework `1.8.0`
- Implemented `Assets/IdleFactory/` assemblies and `scripts/test-unity.ps1`
- No interactive Unity Editor process holding the same project while batch tests run
- For current internal Android development evidence: Unity Android Player support, Android SDK/ADB, and the named API 36 x86_64 AVD `Medium_Phone_API_36.0` visible to ADB. First confirm that the AVD advertises `arm64-v8a`, then attempt a Unity ARM64 Development Build and prove install/launch through its ARM64 translation layer. Unity 6 documents ARMv7/ARM64 Android Player targets and x86-64 as Magic Leap 2-specific; Unity also does not support Android emulators as Player hardware. This owner-approved emulator evidence is experimental development evidence only, not physical-device qualification, release readiness, or representative mobile performance. If the Player cannot launch, record the exact build/install/runtime blocker and leave Android runtime acceptance unresolved.
- iOS build/device validation is deferred from current Feature 001 acceptance by the owner and remains tracked as open/blocked GitHub Issue #39; do not claim iOS acceptance from Windows or Android evidence.

The Windows planning environment located Unity at:

```text
C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe
```

The test wrapper resolves the configured/editor path rather than assuming every workstation uses this directory.

## 1. Complete the pre-implementation harness gate

Run each suite separately:

```powershell
./scripts/test-unity.ps1 -Suite EditMode
./scripts/test-unity.ps1 -Suite PlayMode
```

Then run the command intended for MAQA:

```powershell
./scripts/test-unity.ps1
```

Expected evidence:

- Fresh Edit Mode and Play Mode XML and Editor logs beneath ignored `test-results/`
- At least one discovered and executed test in each suite
- Zero failed tests and no compilation/relevant console errors
- Nonzero Unity/test-run error codes propagated by the wrapper
- No `-quit` argument racing the Test Framework's own exit

Prove file-focused selection with a known test path and an intentionally failing temporary/check fixture before enabling TDD:

```powershell
./scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/ProductionSimulationTests.cs
```

The wrapper must reject nonexistent, non-test or unmappable files rather than silently running an empty/full suite.

Once proven, configure:

```yaml
test_command: 'powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1'
test_file_command: 'powershell -NoProfile -ExecutionPolicy Bypass --% -File scripts/test-unity.ps1 -TestFile "{file}"'
tdd: false
```

Keep TDD false until the focused command has demonstrated an observable red/green cycle. This is a separate repository-bootstrap gate before feature task implementation: set a tested writable MAQA worktree base and use the MAQA lane for Feature 001. Since the installed coordinator creates workers from `origin/<default-branch>`, merge and push the reviewed specs/tasks plus harness baseline there before first dispatch, and merge/push each accepted prerequisite before dispatching dependent work.

## 2. Run deterministic Core acceptance

The complete Edit Mode suite must include the following named fixtures and requirement references.

### Placement and identity

1. Place D001's 2×2 building wholly inside the 16×16 bounds; verify four occupied cells and one unique instance.
2. Reject partial out-of-bounds and one-cell overlap without changing occupancy, identities or resources.
3. Place two instances of the same definition; verify separate IDs, buffers and production progress.
4. Repeat valid/invalid checks with a different declared area and footprint; no D001 maximum may be embedded in grid logic.

Expected: FR-001–003/031 and SC-001 pass.

### Buffers and production

For D001 (`4` input, input capacity `4`, output capacity `1`, recipe `2 → 1` in `5 s`):

| Advance / action | Expected authoritative state |
|---|---|
| Active `2.5 s` | input `4`, output `0`, progress `2.5 s` |
| Further `2.5 s` | input `2`, output `1`, progress `0`, output-blocked |
| Further long interval while full | state unchanged; no backlog |
| Remove output, advance `5 s` | input `0`, output `1`, progress `0` |
| Deactivate during partial cycle | progress/resources retained until valid reactivation |

Reject negative/zero-invalid operations, unknown resources, underflow, overflow and aggregate partial exchanges without mutation. Verify every blocker is derived and inspectable.

For the capacity-two variant, a single active `10 s` and ten `1 s` advances both produce input `0`, output `2`, progress `0`.

Expected: FR-007–020 and SC-002/003/006 pass.

## 3. Run save/load and offline acceptance

Use an isolated test checkpoint directory supplied by the persistence port; tests never overwrite a developer's real `persistentDataPath` save.

### Immediate roundtrip

- Use `LoadDevelopmentCheckpoint` to save and restore with zero measured offline time in ready, partial, blocked and inactive states.
- Roundtrip a factory whose origin and at least one valid building anchor are negative coordinates; signed coordinates remain unchanged.
- Compare identities, definitions, positions, buffers, active state and progress.
- Continue original and restored candidates with identical actions/time and compare complete semantic state.

### Strict rejection

Reject each document as a whole:

- missing/duplicate/unknown fields or unsupported schema version;
- truncated/malformed JSON;
- input over 16 MiB, more than 100,000 buildings, more than 256 entries in a buffer, or an ID over 128 UTF-16 code units;
- duplicate instance/resource IDs;
- unknown definitions, unsupported offline-policy revision or incompatible recipe/content semantics;
- negative/overflow quantity or capacity violation while valid signed origin/anchor coordinates are accepted;
- overlapping/out-of-bounds footprint or illegal orientation;
- progress outside `[0, duration)`;
- unrepresentable UTC/generation metadata.

No case may partly replace the live factory or silently clamp data.

### Offline arithmetic

| Valid measured absence | Credited | Effective | D001 expectation from fresh active fixture |
|---:|---:|---:|---|
| `0 s` | `0 s` | `0 s` | unchanged |
| `1 s` | `1 s` | `0.8 s` | input `4`, output `0`, progress `0.8 s` |
| `6.25 s` | `6.25 s` | `5 s` | input `2`, output `1`, progress `0`, blocked |
| `8 h` | `8 h` | `6.4 h` | arithmetic asserted separately; state respects available resources/capacity |
| `24 h` | `8 h` | `6.4 h` | same credited/effective budget as 8 h; no excess backlog |

A representable backward UTC difference produces zero credit, explicit feedback and a promoted checkpoint whose departure boundary remains the source timestamp. A missing or unrepresentable clock value promotes nothing and enters `ReturnBlocked`. Retry rereads the store and reads exactly one new clock value when no representable value was captured; once bound, later promotion retries reuse it. Reload Authoritative Save remains the alternative. Inactive and already-blocked machines retain their state. A machine that reaches a limit during return stops there.

### Exactly-once and failure injection

Inject failures before pending write, during write, after pending validation, during promotion, after durable promotion and before UI publication.

Expected:

- Before durable promotion: old complete checkpoint remains authoritative; no candidate reward is shown as successful.
- After durable promotion: reboot loads the new generation/timestamp and does not replay the source interval.
- Duplicate pause, pause→quit and initial resume callbacks create at most one departure/return transition.
- A matching in-process resume reconciles the checkpoint ID written by its successful departure. If that departure write failed, it explicitly falls back to the last successfully written active checkpoint and its stored departure time; verify later unsaved live changes are absent. If no successful checkpoint exists, retain the live world with zero credit.
- While `ReturnBlocked`, gameplay commands and active advancement are rejected. Retry rereads the store before publishing or recomputing; authoritative reload discards the candidate.
- Abandoned temporary content is never loaded as authoritative.
- Previous-generation recovery is explicit, validates the previous generation and promotes it under a new checkpoint ID before publication.

Expected: FR-022–027/032–037 and SC-004/007/009/010 pass.

## 4. Validate the playable scene

Open `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` after its implementation.

Complete this journey with touch and mouse separately:

1. Pan and zoom a factory larger than the visible viewport.
2. Enter Build, move the complete 2×2 preview, and inspect valid/invalid non-color cues.
3. Confirm a valid placement; reject overlap and out-of-area placement without state change.
4. Select a machine, add development input, observe progress and output blocking.
5. Remove output and verify resume; deactivate/reactivate during a partial cycle.
6. Save, reset/close and restore; verify selection resolves by persistent ID or clears safely.
7. Suspend/lock and return; do not interact with speculative results while reconciliation is pending.
8. Inspect the durable return summary: measured, credited, effective, actual deltas and limitations.

Placeholder visuals must match the Art-Director-approved brief `F001-Development-Placeholders v0.1`. Every inventory row names asset ID/type, logical grid footprint or `N/A`, ports `none/N/A`, power state `N/A`, visual states, pixel target, format, pivot/cell mapping, source/import path, provenance and version; it records `Art Bible: N/A — F001-Development-Placeholders v0.1 governs`. Assets align with logical footprints and include every required state. Edit Mode checks inventory completeness, GUID/reference resolution, dimensions, importer, pixels-per-unit, pivot, sorting and `.meta`; Play Mode checks footprint alignment, every state cue, zoom readability, view recreation and responsive/safe-area behavior. Camera/view recreation may not alter Core checksums.

## 5. Responsive and accessibility checks

Run the complete journey at:

- `360×640`, `390×844`, `768×1024` and rotated equivalents;
- desktop `1280×720` and `1920×1080`.

Verify safe-area fit, readable text, no clipped inspector/return controls, visible focus, keyboard activation for every actual development UI control, non-color status cues, touch target usability and cancellation of gestures during rotation/focus loss. Verify a second world-owned touch converts a pending one-finger action into pinch and that pinch release cannot select or place. Touch needs no keyboard, right-click or pixel-perfect targeting; mouse has click/drag/wheel plus the same visible controls.

For current Android development lifecycle and persistence evidence, use the API 36 x86_64 AVD `Medium_Phone_API_36.0` and record its exact image, API, ABI list, native bridge, emulator version, Unity version, build settings, and commands. Attempt an ARM64 Development Build only after confirming ADB reports `arm64-v8a`; prove installation and launch before verifying touch, autorotation, safe area, suspend/screen-lock/return, and checkpoint promotion/recovery. Capture the relevant Unity Profiler and Android log evidence. These results apply only to this emulator and its translation layer; they do not establish behavior on physical Android hardware or release performance. The same iOS device/build evidence remains deferred under Issue #39; it is not a current Feature 001 pass and must not be inferred from another platform.

Use Unity Build Profiles to include only the authored scene, select Android with the ARM64 target architecture (IL2CPP), enable **Development Build** and **Autoconnect Profiler**, then save the APK beneath ignored `test-results/android/`. The emulator's x86_64 host uses its ARM64 translation layer; if install/launch fails, retain the exact error and do not switch to an undocumented generic x86_64 Android target. Install and launch the APK with the SDK's ADB:

```powershell
New-Item -ItemType Directory -Force 'test-results/android' | Out-Null
$adb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
$device = 'emulator-5554' # use the serial shown by `adb devices -l`
$apk = (Resolve-Path 'test-results/android/CoreGameFoundation.apk').Path
$applicationId = '<application identifier from Android Player Settings>'
& $adb devices -l
& $adb -s $device install -r $apk
if ($LASTEXITCODE -ne 0) { throw 'ADB APK installation failed; do not launch or count a previously installed build as evidence.' }
& $adb -s $device logcat -c
& $adb -s $device shell monkey -p $applicationId 1
if ($LASTEXITCODE -ne 0) { throw 'ADB app launch failed; Android runtime acceptance remains unresolved.' }
$processId = & $adb -s $device shell pidof $applicationId
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($processId -join '').Trim())) { throw 'The app process is not running after launch; Android runtime acceptance remains unresolved.' }
Write-Output "Unity app process: $processId"
# Exercise the app, rotating and locking it with the emulator controls.
$logcat = & $adb -s $device logcat -d 2>&1
if ($LASTEXITCODE -ne 0) { throw 'ADB could not retrieve emulator logcat evidence.' }
$logcat | Set-Content 'test-results/android/emulator-logcat.txt'
```

For each T040 run, restore that checkpoint, warm up for 30 seconds without input, record the post-warm-up starting state, then capture 60 seconds; repeat this sequence three times. Record the checkpoint identity and a reproducible description/hash of machine IDs/count, positions, active/blocked states and buffer contents at each capture start. Compare runs only if those states match. Profile the running Development Build from Unity's Profiler. Record CPU/frame timings, GC allocations, managed/native memory where the available profiler modules expose them, load behavior, render counters and APK/asset size. If a metric is not exposed in this environment, state that explicitly instead of treating it as zero. This is an exploratory emulator baseline with no mobile pass threshold; retain the report and profile/log excerpts as emulator-only development evidence.

## 6. Measure W001

Run the warmed plain-Core Edit Mode performance fixture:

- 1,000 independent machines: 500 active/unblocked, 250 output-blocked, 250 inactive;
- all machines use D001's `2 dev.input → 1 dev.output / 5 s` recipe;
- each active machine starts with input `9,216` and output capacity `4,608`, exactly enough for one 6.4-hour effective return;
- run `100` untimed warmups and `500` independent timed samples for repeated `100 ms` active advances and the capped offline `Advance`;
- prepare/reset each session outside the timed/allocation section and explicitly capture its full resulting-state checksum afterward;
- time only warmed `ProductionSimulationSession.Advance`, including complete prevalidation, calculation, staging and commit. In a separate pass, run 100 untimed warmups and bracket each of 500 calls with `GC.GetAllocatedBytesForCurrentThread()` on the same thread; require every delta to be zero, store samples in preallocated storage and do not format/report inside the measured interval. Report preparation, snapshot capture, serialization, filesystem and UI measurements separately. The allocating static compatibility wrapper is not this boundary.

Record workstation, Unity version, runtime/backend/build mode, fixture seed/configuration, warmups, sample count, p50/p95 time and managed allocations. The allocation counter covers managed bytes on the current thread, not native allocations.

Initial Windows development regression budgets:

- active advance p95 ≤ `10 ms`;
- capped offline advance p95 ≤ `100 ms`;
- zero steady-state managed allocations inside warmed `ProductionSimulationSession.Advance` (HD-018).

Also retain regression evidence that the prepared and static paths are semantically equivalent, later-machine failure leaves committed state unchanged, retry after failed staging is clean, external actions cannot advance stale state, and captured snapshots survive at least two later advances. Never move per-sample arithmetic or prevalidation into setup to achieve the budget.

Failure triggers profiling and a decision brief. It does not automatically authorize ECS, threading, caches, pooling or less readable state.

## 7. Final evidence gate

Before claiming the feature complete, retain:

- passing fresh Edit/Play XML and clean relevant logs;
- focused runner red/green proof and populated MAQA commands;
- acceptance matrix linked to FR/SC IDs;
- strict checkpoint/failure-injection evidence;
- reproducible Windows build plus an ARM64 Android Player install/launch attempt on the API 36 x86_64 AVD; record lifecycle/persistence results if it runs or the exact blocker if it does not, and label all evidence as emulator-only with physical-device and release qualification unverified;
- explicit iOS deferral in the final acceptance record, linked to open/blocked Issue #39 with no iOS PASS claim;
- responsive/accessibility and independent UI/UX review;
- Art Direction/Technical Art approval of F001 placeholders;
- W001 measurements;
- independent QA on the committed result.

Then continue through tasks, analysis, explicit orchestration-ledger preparation/validation, one selected implementation lane and convergence as required by the repository workflow.
