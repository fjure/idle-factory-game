# What I Learned: Core Game Foundation

**Feature**: A playable development slice. It covers grid placement of a D001 machine, deterministic production, strict local checkpoints with exactly-once 80%/8 h offline return, and a Unity presentation layer validated on Windows and an Android emulator.
**Generated**: 2026-09-28
**Scope**: Full feature (MAQA lane)
**Implementation status**: 52/64 tasks completed. GitHub Issues are the MAQA status authority, and `tasks.md` checkboxes mirror them through `scripts/maqa-sync-task-checkboxes.ps1`. Closed: T001–T038, T040 and T051–T063. Open: T064–T065 (performance follow-ups from T040), T041 (acceptance), T042–T050 (JSON content migration) and T039 (iOS, deferred, outside the checklist).
**Updated**: 2026-09-28, merged T040 (Issue #40).

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

### 10. Purpose-Built Allocation-Free Queries for Polling and Per-Frame Callers

**What we did**: T063 took `MachineInspectorPresenter`'s poll and the runtime host's per-frame advance off the general snapshot APIs and gave them narrow application queries instead. `CurrentRevision` returns the revision, `AnyMachineCanProduce()` reads committed counts, `GetMachineInspection` captures a single building, and `AdvanceActiveTime` returns only the outcome. The presenter re-renders only the selected machine, and only when the revision changed.

**Why**: The general APIs are correct but copy the factory. When they are called every 0.1 s or every frame, the cost multiplies. At W001 scale a poll made 47 million allocations and took about 5.3 s whenever no machine could produce, and every tick allocated about 100,000 times for results the host never read. The narrow queries answer exactly the question asked and allocate nothing.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Cache the snapshot per revision in the bootstrap | One copy per changed revision is still O(N) per producing frame, and it adds invalidation state. |
| Poll less often | It lowers the rate, not the O(N²) shape, and it slows live inspector refresh. |
| Make `AdvanceSimulation` lazy | That changes a result contract that tests and future callers rely on; a second lean method keeps both honest. |

**When you'd choose differently**: If many consumers need the full picture every frame, as a mini-map or statistics panel would, maintain one shared, incrementally updated read model instead of adding a query per caller.

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

**Where we used it**: In T040, `dumpsys meminfo` triggered a GC that became the CPU maximum, and the attached Profiler slowed frames about 15×. The same probe exposed T040-F1: `CoreGameFoundationBootstrap.Snapshot` looks like a property but copies the whole factory on every read. `MachineInspectorPresenter` read it three times per 0.1 s poll, plus one full copy per building, which is O(N²) in an idle factory. T063 fixed it (Decision 10). T063 then found that the measuring tool itself was broken: `GC.GetAllocatedBytesForCurrentThread()` returns 0 in the Unity Editor, even for `new object[1024]`, so every zero-allocation test passed vacuously. The tests now count allocations through Unity's `GC.Alloc` recorder and include a live control that must allocate.

**Why it matters**: Zero-allocation tests on the Core (W001) and the view sync (T062) did not cover the presenter's polling path. A cost is only covered where a test or profile actually runs it, and an assertion only protects you if it can fail. Prove that once with a known allocation.

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
| `GC.Alloc` recorder | Unity's profiler sample for managed allocations; `GcAllocationCounter` reads it per call, because the .NET byte counter reads 0 in the editor (T063). |
