# What I Learned: Core Game Foundation

**Feature**: A playable development slice. It covers grid placement of a D001 machine, deterministic production, strict local checkpoints with exactly-once 80%/8 h offline return, and a Unity presentation layer validated on Windows and an Android emulator.
**Generated**: 2026-09-27 (updated 2026-09-28 for T063–T068 and final acceptance)
**Scope**: Full feature (MAQA lane)
**Implementation status**: 58/67 tasks completed. GitHub Issues are the MAQA status authority, and `tasks.md` checkboxes mirror them through `scripts/maqa-sync-task-checkboxes.ps1`. Closed: T001–T038, T040, T041 and T051–T068. Open: T042–T050 (JSON content migration, gated on foundation completion); T039 remains a deferred iOS follow-up outside the checklist. Phase 10 closed T041-F1/F2/F3, and convergence found no remaining work in the Core Game Foundation scope. Completion verification is recorded in `reviews/completion-verification.md`.
**Updated**: 2026-09-28, merged T066–T068, re-reviewed ART-F1, completed the scoped convergence pass and recorded the parent verdict.

---

## Key Decisions

### 1. Integer Ticks and Whole Units Instead of Floats

**What we did**: Resources are checked nonnegative `Int64` units. Durations and progress are `Int64` 100 ns ticks with `0 ≤ progress < duration` (research R05).

**Why**: Offline return multiplies time by 4/5 and caps it at 8 h. With integers, 0.8 s and 6.4 h are exact, and "advance 1 h" gives exactly the same state as "advance 3,600 × 1 s". That property is what makes the determinism tests in T031 possible at all.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| `float`/`double` time and resources | Partitioned advances drift, so saves and replays stop matching. |
| Milliseconds | Viable, but 100 ns composes directly with `TimeSpan`/`DateTimeOffset`. |

**When you'd choose differently**: Use floats for purely visual interpolation, such as progress bars or camera easing, where drift is invisible and never persisted. The trade-off surfaced in T052: Save V1 stores milliseconds, so the clock boundary must floor explicitly.

---

### 2. Jump to Cycle Boundaries, Commit the Whole Factory Once

**What we did**: `ProductionSimulator` computes per machine how many cycles time, input and output permit. It books them in bulk, builds a complete factory candidate and publishes it once (R06).

**Why**: The cost scales with machine count, not with elapsed time. Seven offline days cost the same as one second. Committing once means an arithmetic overflow in machine 900 cannot leave machines 1–899 advanced.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Per-frame or fixed-tick replay | Long offline intervals would replay millions of ticks. |
| One loop iteration per cycle | Unbounded for short recipes and long gaps. |
| Event queue or network solver | Machines share no buffers yet, so there are no interactions to order. |

**When you'd choose differently**: When logistics connect machines (belts, shared inventories), one machine's output changes another's permitted cycles mid-interval. You then need event ordering or a network solver, and the closed-form jump stops being exact.

---

### 3. Durable Before Published: Exactly-Once Offline Return

**What we did**: A return binds to the checkpoint its departure wrote. It computes a candidate and promotes that candidate to disk through pending → verify → active (keeping previous). Only then does it show the world and the reward (R11, R12; `PersistenceCommands.cs`, `JsonCheckpointStore.cs`).

**Why**: If the game crashes after the reward is shown but before it is saved, the next launch would grant the same offline interval again. Ordering "durable, then visible" makes a crash at any point either replay nothing or show the already-saved result.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Save the timestamp separately from the factory | Creates a window for double or lost credit. |
| Overwrite the save in place | A failed write destroys the only good save. |
| Server-side idempotency | There is no backend in scope. |

**When you'd choose differently**: With a trusted backend, the server owns time and idempotency keys. The client then only displays results, and local generations become a cache rather than the authority.

---

### 4. A Reusable Simulation Session to Keep Zero Warmed Allocations

**What we did**: The owner kept W001's zero-allocation budget (HD-018, R18). The team added `ProductionSimulationSession` with prepared state graphs and explicit result capture. The allocating static API remains as an adapter over the same calculation.

**Why**: The first implementation rebuilt catalogs and copied graphs on every advance, and warm-up cannot remove those allocations. Moving ownership copies to named boundaries (prepare, capture) keeps atomic publication while making the hot path allocation-free and measurable.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Relax the allocation budget | The owner rejected it. |
| Mutate the published graph, or return borrowed snapshots | Breaks atomicity and snapshot retention. |
| ECS, threading or global pools | No measured need; they add complexity without a proven gain. |

**When you'd choose differently**: If the hot path runs only on rare events, such as a save, simple allocating code is easier to reason about and fine. Pay for a session or pool design only where a benchmark shows the cost.

---

### 5. Strict, Fail-Closed Save Parsing With Privacy-Safe Diagnostics

**What we did**: Save V1 uses Newtonsoft required-member DTOs. It rejects unknown, duplicate and missing members, enforces size guards and validates every invariant before mapping (R10). After T052, collapsed failures report the exception type, stage and `member:line` through `CheckpointDiagnostics`, never messages, IDs or paths.

**Why**: A save is untrusted input, and a half-valid world is worse than a clear error. Fail-closed has a cost: when the device clock produced sub-millisecond instants, every save in the running game failed with a generic "InvalidCheckpoint". The diagnostics found the throw site in one run and disproved the code-stripping hypothesis.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| `JsonUtility` | Missing fields silently become defaults. |
| Binary format | Hard to inspect and migrate. |
| `PlayerPrefs` | Meant for preferences and unencrypted. |
| Log full exception messages | Could leak player data or paths into logs. |

**When you'd choose differently**: For a developer-only config file, lenient parsing with defaults is friendlier. Keep strictness for anything that becomes authoritative game state.

---

### 6. Android, Not Unity, Owns Screen Rotation

**What we did**: `Assets/Plugins/Android/IdleFactoryGameActivity.java` maps every orientation request to `fullUser`, and a custom manifest makes it the launcher (R19, T058).

**Why**: Unity's Android Auto Rotation reads the legacy Input Manager accelerometer, which the Input System-only backend (R09) disables. Rather than re-enabling a second input stack, the OS handles rotation, the lock and platform rules. The activity holds no gameplay logic.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Enable both input backends | Contradicts R09. |
| JNI handoff at runtime | Unity re-requests its stale orientation after each change. |
| Rotation logic in C# from the Input System accelerometer | Duplicates OS rules such as the rotation lock. |

**When you'd choose differently**: If a future Unity version fixes Auto Rotation with the Input System, delete the override. The Unity-upgrade obligation in R19 exists so that someone checks.

---

### 7. Rotation Stays a Lifecycle Event; Only the Presentation Changes

**What we did**: A rotation still writes a departure and a return checkpoint (FR-037). `ApplicationLifecycleAdapter` labels the return `ConfigurationChange`, and `PersistencePresenter` withholds that return's summary (R20, T060).

**Why**: The rule stays in one place: accounting remains exactly-once and unchanged, and the fix is presentation-only. Classifying at the Unity boundary, which can see `Screen`, keeps Core free of platform signals.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Treat rotation as no lifecycle event | Changes FR-037, and a real departure can coincide with a rotation. |
| Hide every focus-only return | Also hides genuine notification-shade returns. |

**When you'd choose differently**: If misclassification reports appear, for example on foldables or desktop resize plus Alt-Tab, add platform gating or a maximum duration behind the same `Func<bool>` seam.

---

### 8. Revision-Gated Presentation Sync Instead of Per-Tick Rebuilds or Events

**What we did**: `FactoryView.SyncPresentedStates` runs in `LateUpdate`. It checks the session revision, copies a small `MachinePresentationState` per machine into a reused buffer and swaps only the badges whose state changed (T062).

**Why**: Simulation advances every frame, but a badge changes only when a machine starts, blocks, unblocks or switches on or off. The state deliberately excludes progress, so a comparison filters out almost every tick. At W001 scale, an unchanged tick costs a revision check and an allocation-free copy. A full rebuild would recreate 1,000 renderers per frame.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Rebuild the view every tick | Allocates and recreates renderers at W001 scale, and T040 measures exactly this path. |
| Core raises per-machine change events | Needs event storage and ordering inside the allocation-free simulation hot path, and it couples Core to subscribers. |
| Keep refreshing only on commands and selection | This was the T057-F1 bug: the badge shows `RUN` for a blocked machine. |

**When you'd choose differently**: With many more view types or sparse changes across huge factories, a dirty set produced by the simulation beats a full comparison pass. Polling stays simpler while one comparison per machine per changed revision is cheap.

### 9. Matching-Start Emulator Profiling Instead of One-Off Numbers

**What we did**: T040 profiled the emulator player with a script (`reviews/t040-evidence/capture-protocol.ps1`). For three runs it restores a fixed checkpoint, waits 30 s with no input and captures 60 s. It hashes the factory contents at each capture start and compares only runs whose hashes match. Frame pacing comes from SurfaceFlinger TimeStats, and a single Unity Profiler probe is labelled qualitative.

**Why**: An emulator with ARM64 translation is noisy, so a number without an identical starting state and a documented method can't be repeated or trusted. The review found three instrumentation traps. `gfxinfo` is empty for Unity's `SurfaceView`. `top`'s first sample lands on the GC that `meminfo` triggers. The attached in-editor Profiler stalled the player. Each would have produced a confident wrong number.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Report whatever one launch shows | Differing restore states, first-launch costs and host load make single numbers incomparable. |
| Profile only through the in-editor Unity Profiler | On one host it slowed startup 2–3× and stalled the player; profiled frames ran about 15× slower. |
| Mark frame time "unavailable" when `gfxinfo` is empty | SurfaceFlinger measures the Unity layer directly; "unavailable" was true only for the first tool tried. |

**When you'd choose differently**: On a physical device with a separate profiling host, the Unity Profiler (Standalone Process) becomes the primary source, and T064 sets that up. The protocol still matters, because matching starts and same-session controls are what make before/after claims such as T063's allocation fix and T065's 60 FPS target defensible.

---

### 10. Purpose-Built Allocation-Free Queries for Polling and Per-Frame Callers

**What we did**: T063 gave the inspector poll and runtime host narrow queries: `CurrentRevision`, `AnyMachineCanProduce()`, single-building inspection and `AdvanceActiveTime`. The presenter re-renders only the selected machine when the revision changes; the host reads only the advance outcome.

**Why**: At W001 scale, the old no-producer poll made 47,253,857 allocations and took 5.1–5.3 s; each runtime tick made 105,907 allocations for results the host never read. After the change, unchanged polls and the host advance allocate zero times, while a selected-machine revision change costs 89 allocations at both 1 and 1,000 machines. T064's standalone profiler later confirmed no game-code allocations in the idle D001 player; these editor counts do not measure a producing device.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Push events from Core to the presenter | It adds a subscription lifecycle across the Unity boundary when a revision query already answers whether the view is stale. |
| Cache snapshots or poll less often | Caching still copies O(N) state per changed revision, and slower polling leaves the O(N²) work and delays refreshes. |
| Make `AdvanceSimulation` lazy | That changes a result contract used by other callers; a separate `AdvanceActiveTime` keeps the full-result API intact. |

**When you'd choose differently**: If many views need the full factory state every frame, use one shared, incrementally updated read model instead of adding more narrow queries.

---

### 11. A Profiling Build That Does Not Call Home (T064)

**What we did**: Emulator profiling uses the Unity Profiler (Standalone Process) and a development build made with `-noProfilerAutoconnect`. The standalone profiler is driven through MCP for Unity with committed snippets (`reviews/t064-evidence/`). Every run is checked against T040's capture-start hash before its numbers count.

**Why**: The autoconnect build opened its own profiler connection to the main editor at startup. The standalone profiler is the editor's child process, so the editor can't be closed, and the player streamed its frames to the editor. The standalone profiler received nothing. `netstat` showed the emulator's socket owned by the editor process, which ended days of guessing about "stalls".

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Keep the T040 autoconnect build and retry until the profiler "catches" the player | Four of six attempts got no frames; which process accepted the player was not controllable. |
| Remove the editor's `adb reverse tcp:34999` | The player then connected directly through the emulator NAT to the same editor. |
| Close the editor during capture | Closing the editor kills the standalone profiler with it. |
| Change the default build | The autoconnect default is right for ordinary development; the switch is opt-in, so nothing else changes. |

**When you'd choose differently**: With a second profiling machine or a physical device over USB, only one profiler listens, and autoconnect is convenient again.

### 12. One Windows Player Check at the End of Each Batch

**What we did**: `scripts/test-unity.ps1 -StandaloneWindowsPlayer` explicitly builds for `win64` and runs only Play Mode tests tagged `[Category("StandalonePlayer")]`. The parent runs it once after a completed batch, while holding both the Unity and emulator leases.

**Why**: A built Player catches runtime-loop and player-build problems that Editor Play Mode cannot. The full Play Mode suite contains fixtures that depend on Editor-only APIs, so a category marks tests that are intentionally portable to a standalone Player. The target is explicit because a checkout may have been used with another platform, and the emulator lease stays held because Unity still logged an ADB shutdown on this host.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Run every Play Mode test in the Player | Editor-only fixtures fail there even when the player-safe smoke test passes. |
| Run a Player build for every Issue | It repeats the expensive build for changes that can be checked together at the batch boundary. |
| Inherit the active build target | The checkout may carry Android as its last target, and the batch contract should be deterministic. |

**When you'd choose differently**: If the Play Mode suite is made fully Player-compatible, run the complete suite instead of maintaining a tag. Add a separate device-specific check when acceptance depends on Android behavior; a Windows Player run cannot prove it.

---

### 13. One Explicit Frame-Rate Policy at the Bootstrap, Player-Only VSync Change (T065)

**What we did**: `CoreGameFoundationBootstrap.ApplyFrameRateTarget()` sets `Application.targetFrameRate = 60` in `Awake`, and in players only (`#if !UNITY_EDITOR`) sets `QualitySettings.vSyncCount = 0`. The emulator went from 30.3 FPS (p50 33 ms) to 61.4 FPS (p50 16 ms) for about +12.5 points of guest CPU.

**Why**: Unity silently caps Android at 30 FPS, and on desktop VSync overrides `targetFrameRate`, so one explicit policy is needed for both players. Setting `QualitySettings` during Editor Play Mode would change the Editor's quality level, which test runs can write back into `ProjectSettings/`, so the Editor keeps its settings and a `StandalonePlayer` test checks the player path. Production is unaffected because it follows cumulative active time, not frames.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Keep VSync on Windows | The player would follow the monitor (e.g. 144 Hz) instead of the 60 FPS target. |
| Set the target in Project Settings or a quality level | Android's frame cap is a runtime API, and a code policy is testable and reviewable in one place. |
| Also set VSync in the Editor | It leaks into serialized project settings and shows up as unexplained noise. |

**When you'd choose differently**: Once players get a graphics settings screen or a non-development Windows build ships, expose VSync (VSync-off tears on high-refresh displays) and route that choice through the same single method. On battery-sensitive targets, weigh a lower or adaptive target against the measured CPU cost.

---

### 14. Final Acceptance Reconciles Evidence Instead of Re-Running Every Journey (T041)

**What we did**: T041 re-ran the complete Edit Mode (569), Play Mode (147) and Windows player (2) suites and did an Android build plus install/launch/logcat smoke run. It then mapped each of the 47 FR/SC requirements to a cited record: a review section, a named test or an Issue comment. The full Android journey (T057) and the T040–T065 performance protocol were cited, not repeated, because no code on those paths had changed since they were accepted. Phase 10 later closed the three carried findings with targeted work: T066 paired rendered zoom evidence with an independent Art Director re-review, T067 tested the literal D001 two-cycle sequence, and T068 added a dated security-review successor while retaining the original T029 finding.

**Why**: The fresh suites prove that nothing regressed. The citations prove what each requirement rests on. Re-running every manual emulator journey would cost hours of exclusive device time and would add no information when the relevant code is unchanged.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Repeat every device journey and profile | It needs the single emulator for hours and measures unchanged code again. |
| Accept on the green suites alone | Suites cannot show gaps such as a requirement proven only on a different fixture (SC-002). |
| Block T041 until every pre-existing gap is fixed | That mixes acceptance with remediation. The Completion Procedure's specialist step and convergence are where the fixes are scheduled. |

**When you'd choose differently**: If code on a platform path changed after its last journey, or the evidence came from a different build configuration, re-run that journey. Do not cite it.

---

## Concepts to Know

### Functional Core, Imperative Shell

**What it is**: The pure, deterministic logic lives in a core with no I/O or engine calls. A thin shell does time, files, input and rendering, and calls into the core.

**Where we used it**: The engine-free `IdleFactory.Core` (Domain, Simulation, Application) versus `IdleFactory.Unity` (Bootstrap, Input, Persistence, Presentation, UI). `FactoryRuntimeHost` is the only place that turns Unity time into Core ticks.

**Why it matters**: More than 550 Edit Mode tests run without a scene in seconds. Every player defect found so far sat in the shell, which tells you where to aim Play Mode and player tests.

### Candidate-Then-Commit Atomicity

**What it is**: Build the complete next state off to the side, validate it, and swap it in with one assignment. There are no partial writes.

**Where we used it**: `FactoryPlacement` (full footprint), `ProductionSimulator` (whole factory), `JsonCheckpointStore` (pending → verify → active), and offline return (promote before publish).

**Why it matters**: A failure anywhere leaves the old state intact, so there is no rollback code to get wrong.

### Test Doubles Can Hide the Bug

**What it is**: Each fake, such as a clock, store or composition override, removes one real behaviour from the test. If the fake is "too nice", the tests stay green while production fails.

**Where we used it**: The failure cases were millisecond-aligned fake clocks (T052), an in-memory store in scene tests (T061) and an explicitly sized UI root (T053). The fixes were real-component tests: `PlayerCheckpointRoundTripTests`, which also runs inside test players, and `ColdStartRestoreRenderTests`.

**Why it matters**: For every double, keep at least one test that uses the real thing on the real path.

### A Test Category Can Declare Runtime Portability

**What it is**: A test category can identify the subset of a broader suite that is valid in a particular runtime, such as a built standalone Player.

**Where we used it**: `HarnessSmokeTests.DiscoversRunningPlayerLoop` carries `[Category("StandalonePlayer")]`; `scripts/test-unity.ps1 -StandaloneWindowsPlayer` selects that category after building `StandaloneWindows64`.

**Why it matters**: Editor tests may rely on APIs unavailable in a built Player. An explicit portable subset lets the batch validate the real runtime without misclassifying those Editor-only cases as Player regressions.

### Views Render State; Something Must Tell Them When

**What it is**: A view derived from snapshots is correct only if it refreshes after every state change, not just after the changes the view itself caused.

**Where we used it**: T061 added a refresh after startup reconciliation in `CoreGameFoundationBootstrap`. T062 added the revision-gated `LateUpdate` sync in `FactoryView.cs` for simulation-driven changes (Decision 8).

**Why it matters**: Stale-but-plausible UI, such as "running" on a blocked machine, misleads players more than an obvious error. Tie refresh to the state revision, not to the call sites.

### Units at Boundaries: Pixels Versus Points, Ticks Versus Milliseconds

**What it is**: Each subsystem has its own unit. Values that cross a boundary need an explicit conversion.

**Where we used it**: `FactoryHudDocument.ApplyScreenLayout` converts screen pixels to panel points (T053). `DeviceUtcClock.FloorToMilliseconds` converts 100 ns ticks to the milliseconds of Save V1 (T052).

**Why it matters**: Both bugs passed type checks, because everything was a number, and failed only on a device.

### Measure the Measurement, and Look for Hidden Per-Poll Costs

**What it is**: Every measuring tool changes what it measures. Every "cheap" property read can hide a full copy. Check both before trusting a number or a loop.

**Where we used it**: In T040, `dumpsys meminfo` triggered a GC that became the CPU maximum, and the attached Profiler slowed frames about 15×. The same probe exposed T040-F1: `CoreGameFoundationBootstrap.Snapshot` looks like a property but copies the whole factory on every read. `MachineInspectorPresenter` read it three times per 0.1 s poll, plus one full copy per building, which is O(N²) in an idle factory. T063 fixes it.

**Why it matters**: Zero-allocation tests on the Core (W001) and the view sync (T062) did not cover the presenter's polling path. A cost is only covered where a test or profile actually runs it.

T063 added two more lessons. First, `GC.GetAllocatedBytesForCurrentThread()` returns 0 in the Unity Editor even for `new object[1024]`, so four "zero allocation" assertions could never fail. `GcAllocationCounter` (the `GC.Alloc` recorder) replaced it. Always prove a measuring tool can report a positive result before trusting its zero. Second, T064 showed the profiler itself distorting per-frame numbers: an Input System allocation runs per `FixedUpdate`, so it looks bigger per frame the more the profiler slows each frame. Compare per-call sizes and per-window totals, not only per-frame values. The ~20 MiB of "render textures" in T040 turned out to be the profiler's own screenshot buffers.

---

### Find Out Who Owns the Connection

**What it is**: When data "doesn't arrive", identify both ends of the socket before changing timing or retrying. On Windows, `Get-NetTCPConnection` or `netstat -ano` names the process on each end.

**Where we used it**: In T064, several theories (attach too early, recording toggled too late) each explained one failed run and broke on the next. The netstat line `qemu-system-x86_64 → 192.168.2.172:34999 (main editor)` explained all of them at once. See `reviews/t064-evidence/discarded-attempts.txt`.

**Why it matters**: Intermittent success makes timing theories look plausible. A structural cause, such as two listeners on one port, is found only by looking at the connection itself.

---

### Sanity-Check the Control Before Trusting the Comparison

**What it is**: A same-session control only helps if it first reproduces a value you already know. If the control is off, the measuring setup is broken, and control and treatment are equally worthless.

**Where we used it**: T065's first session measured about 4.5 FPS for both the 30 FPS control and the 60 FPS target, while T040 had measured about 30 FPS for the same control. The long-running emulator (with editors on the host) was degraded and later stopped answering `adb`. After a cold boot the control reproduced 30.3 FPS and the target reached 61.4 FPS. The discarded set is kept in `reviews/t065-evidence/discarded-session1-metrics.csv`.

**Why it matters**: "Both variants look the same" can mean "no effect" or "the instrument is broken". Anchoring the control to a known baseline tells the two apart.

---

### Superseded Evidence Needs a Forward Pointer

**What it is**: When a later task fixes what a review failed, the original file still reads FAIL. Unless it is amended, or points to its successor, every future reader has to rediscover which record wins.

**Where we used it**: T068 appended a dated T051 PASS successor to `reviews/persistence-security-review.md` while preserving the original T029 FAIL/P2 record. T066 appended its Art Director re-review to `f001-placeholder-final-review.md`, and T067 put the literal D001 two-cycle proof in `ProductionSimulationTests`. The T038 header in `windows-android-validation.md` is superseded by the T057 rerun; the T040-F2 disposition in `mobile-performance.md` is resolved by T064 further down. The wiki page `feature-acceptance-evidence.md` tracks which later record wins.

**Why it matters**: Evidence you can only find in the GitHub history is fragile. Headlines that disagree with row statuses ("all 47 PASS" while one row was PARTIAL) erode trust in the whole report.

---

## Architecture Overview

Core owns every authoritative rule. Domain holds immutable definitions, factory state, geometry and placement. Simulation holds cycle-boundary advancement. Application holds `FactorySession`, the command classes and the `IUtcClock` and `ICheckpointStore` ports. Unity adapters sit around it and depend only inward. Content assets are copied once into an immutable `DefinitionCatalog`. Input becomes semantic commands. Persistence implements the store and clock ports. Presentation renders immutable snapshots. Platform glue (the Android activity) and lifecycle classification stay at the outer edge.

```text
 Unity shell ─────────────────────────────────────────────────────────┐
 │ Input ──commands──▶ ┌──────────── IdleFactory.Core ─────────────┐  │
 │ RuntimeHost ─ticks─▶│ Application: FactorySession, *Commands    │  │
 │ Lifecycle ─events──▶│   ├─ Simulation: ProductionSimulator      │  │
 │                     │   └─ Domain: FactoryState, Catalog, Grid  │  │
 │ Presentation ◀─snapshots── ports: IUtcClock, ICheckpointStore   │  │
 │ Persistence (JsonCheckpointStore, DeviceUtcClock) implements ports │
 │ Plugins/Android (rotation glue, no gameplay)                       │
 └────────────────────────────────────────────────────────────────────┘
```

---

## Glossary

| Term | Meaning |
|------|---------|
| D001 | The development acceptance fixture: a 16×16 grid, a 2×2 machine and a `2 input → 1 output / 5 s` recipe. |
| W001 | The performance workload: 1,000 D001 machines (500 active, 250 output-blocked, 250 inactive) over 6.4 h. |
| FR-037 fallback | When a departure save failed, return from the last successful checkpoint and its timestamp. |
| Generation | One complete, validated checkpoint write. Active and previous generations are kept. |
| Configuration-change return | A focus-only return caused by a rotation or resize. It is accounted normally, and its summary is withheld. |
| MAQA | The multi-agent lane: one GitHub Issue and worktree per task, with independent QA before merge. |
| Matching start | Two profiling runs whose capture-start factory hashes are equal. Only these may be compared (T040). |
| SurfaceFlinger TimeStats | Android's per-layer record of presented frames. Here it measures frame pacing of Unity's `SurfaceView`, where `gfxinfo` sees nothing. |
| Autoconnect Profiler | `BuildOptions.ConnectWithProfiler`: the development player connects to the build host's profiler at startup. Omitted for standalone-profiler builds (T064). |
| Profiler (Standalone Process) | A second Unity process (`-ump-process-role profiler`) that hosts the Profiler window outside the editor; it closes with the editor. |
