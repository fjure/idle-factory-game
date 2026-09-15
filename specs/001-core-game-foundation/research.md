# Research: Core Game Foundation

**Date**: 2026-09-13
**Scope**: Resolve technical choices required by [plan.md](plan.md) without adding gameplay beyond [spec.md](spec.md).

## R01 — Authority conflict in the supplied plan draft

**Decision**: Plan the bounded offline-production behavior in FR-022 and FR-032–037. Treat the draft's broader offline exclusion as applying to production networks, final balance and a general offline solver.

**Rationale**: The owner clarified the specification after writing `Plan-Draft.md`; the constitution makes the active spec authoritative for observable behavior.

**Alternatives considered**: Defer all offline calculation as the draft says. Rejected because it would knowingly omit approved requirements and acceptance scenarios.

## R02 — Production assembly boundaries

**Decision**: Create one engine-free `IdleFactory.Core` assembly containing Domain, Simulation and Application namespaces, plus one `IdleFactory.Unity` adapter assembly. Create separate Edit Mode and Play Mode test assemblies.

**Rationale**: The Core/Unity boundary enforces the required dependency direction and headless testability. Splitting every conceptual layer into its own assembly would add references and compilation friction before independent deployment or reuse exists.

**Alternatives considered**:

- One assembly for everything: rejected because Unity presentation could become an accidental dependency of domain tests.
- Five production assemblies: deferred until a real compile-time or ownership boundary appears.
- DOTS/ECS or a DI framework: rejected because the current independent-machine fixture has no measured need.

## R03 — Stable content definitions and runtime state

**Decision**: Author development resource, recipe, building and offline-policy definitions as ScriptableObjects, validate and copy them into an immutable plain-C# `DefinitionCatalog` at bootstrap, and keep every placed instance's mutable state in Core objects. Offline policy revision 1 supplies the `4/5` ratio and eight-hour cap; existing ID/revision semantics remain immutable within Save V1.

**Rationale**: This keeps content editor-friendly without allowing shared assets to become mutable runtime truth. Stable string IDs survive serialization and do not bind saves to asset instance IDs.

**Alternatives considered**: Hard-coded recipes/resources would make every tuning change a code edit; mutable ScriptableObjects would share state across instances and saves.

## R04 — Grid and occupancy

**Decision**: Use integer `GridPosition`, explicit rectangular bounds, a building map by instance ID and a derived occupancy dictionary by cell. Expand and validate the full footprint before an atomic placement mutation.

**Rationale**: Dictionary occupancy is simple for D001, permits sparse growth and can later be hidden behind the factory operation if chunking becomes measured work. Rebuilding occupancy from buildings during load prevents two persisted sources of truth.

**Alternatives considered**: A fixed 2D array hard-codes a maximum; chunking is premature; Transforms or colliders cannot be authoritative logical occupancy.

## R05 — Resource and time representation

**Decision**: Use checked nonnegative Int64 whole units for resources and Int64 100 ns units for durations/progress. Require positive recipe duration/amounts and maintain `0 ≤ progress < duration`.

**Rationale**: F001 uses discrete materials. Integers make conservation, partition equivalence and save roundtrips exact. A TimeSpan-compatible unit represents 0.8 seconds and 6.4 hours exactly and gives ample range.

**Alternatives considered**:

- Floating-point resources/time: rejected as domain authority because accumulation and partitioning can drift.
- Decimal/fractional resource abstraction: deferred until a specified resource needs it.
- Milliseconds: viable, but 100 ns units compose directly with UTC/TimeSpan values while remaining integer.

## R06 — Production advancement algorithm

**Decision**: Advance independent machines directly to cycle boundaries and bulk-complete the minimum of time-permitted, input-permitted and output-permitted cycles. Prevalidate aggregate transfers per machine, build and validate a complete factory candidate, then commit the candidate once so a later-machine failure cannot leave partial advancement.

**Rationale**: Runtime cost depends on machine count rather than elapsed duration or rendered frames. This is semantically equivalent while Feature 001 has private buffers and no events/actions inside one advance call.

**Alternatives considered**:

- Per-frame or fixed-tick replay: rejected because long offline intervals scale with time.
- Loop once per cycle: easier to read but can be unbounded for short recipes and long intervals.
- Event queue/network solver: deferred until logistics introduce dependencies between machines.

## R07 — Offline-time arithmetic

**Decision**:

```text
policy = catalog.Resolve(checkpoint.offlinePolicyRevision)
creditedTicks = min(validPositiveUtcDifferenceTicks, policy.maxCreditedTicks)
effectiveTicks = floor(creditedTicks × policy.efficiencyNumerator / policy.efficiencyDenominator)
```

Revision 1 is `{ numerator: 4, denominator: 5, maxCreditedTicks: 288_000_000_000 }`. Validate numerator/denominator/cap at bootstrap and record the revision in every checkpoint. Round down less than one tick once per return; preserve a ratio remainder only when subdividing that same return. Separate returns are independently capped. A policy change requires a human-approved new revision and compatibility/migration decision.

**Rationale**: The 4/5 ratio implements the approved 80% rule without floating-point error. Eight credited hours become exactly 230,400,000,000 ticks, or 6.4 hours. Negative/invalid intervals yield zero; positive clock changes receive no protection beyond the approved cap.

**Alternatives considered**: Server time violates the local/no-backend scope; additional tamper detection was explicitly declined; applying 80% to output quantities would break recipe atomicity and differs from the owner's effective-time rule.

## R08 — Active-time boundary

**Decision**: One Unity runtime host observes cumulative active elapsed time, converts cumulative differences to Core ticks and calls the explicit simulation operation. No building has its own `Update`.

**Rationale**: One boundary isolates Unity time and avoids rounding each frame independently. Simulation results depend on total logical time and ordered external actions, not render frequency.

**Alternatives considered**: A fixed simulation tick is unnecessary for independent production and would require catch-up loops; direct float delta per machine creates drift and coupling.

## R09 — Input and UI stack

**Decision**: Pin Input System `1.20.0`, select the new input backend, and use UI Toolkit as the only development overlay. Use SpriteRenderers for the orthographic world. Raw input maps to application commands through one adapter.

**Rationale**: Unity calls the Input System its recommended solution. Bootstrap verification showed that `1.17.0` no longer compiles under Unity `6000.6.0f1` because editor APIs removed by that editor are still referenced. Unity's official registry lists `1.20.0` for Unity 6000.0 and its later editor-compatibility fixes cover the affected API generation, so the repository pins the verified compatible release instead of patching package-cache code. UI Toolkit is already a built-in module and supports the Input System event path. One stack avoids uGUI package/event-system duplication. [Unity Input overview](https://docs.unity3d.com/6000.0/Documentation/Manual/Input.html), [official Input System package registry](https://packages.unity.com/com.unity.inputsystem), [runtime UI input](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Event-System.html)

**Alternatives considered**: Legacy Input Manager is less suitable for new touch/mouse work; uGUI remains possible if a later concrete world-space/prefab requirement justifies it; mixed UI stacks are excluded now.

## R10 — Save serializer and strict validation

**Decision**: Promote already-resolved `com.unity.nuget.newtonsoft-json` `3.2.2` to a direct runtime dependency, use separate required-member DTOs/arrays, reject duplicate or unknown members and duplicate normalized resource IDs, and validate all domain invariants before mapping. Save V1 parser guards are 16 MiB input, 100,000 buildings, 256 entries per buffer and 128 UTF-16 code units per ID. Numeric validation is field-specific so signed grid coordinates remain valid while quantities/progress/generation remain nonnegative.

**Rationale**: JSON is inspectable and versionable, while Newtonsoft required-member handling distinguishes absent fields from valid default values. The direct pin prevents runtime persistence from depending on the MCP package's transitive graph.

**Alternatives considered**:

- `JsonUtility`: lightweight, but absent primitive fields can become defaults and dictionaries are awkward; a custom presence-validation layer would be more code and easier to get wrong.
- Binary serialization: harder to inspect/migrate and unnecessary.
- PlayerPrefs: intended for preferences and not suitable as the authoritative gameplay document; Unity also notes it is unencrypted. [Unity PlayerPrefs](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerPrefs.html)

## R11 — Checkpoint files and recovery

**Decision**: Use a fixed application-owned location under `Application.persistentDataPath`, with active, previous-valid and same-directory temporary generations. Verify the temporary document, retain the previous complete generation, and then promote. Make backup recovery explicit to the user.

**Rationale**: Unity exposes `persistentDataPath` for cross-session files on target platforms. A whole-generation protocol can preserve the previous usable state when writing or validation fails. File replacement semantics must still be proven on Windows, Android and iOS. [Unity persistent data path](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-persistentDataPath.html)

**Alternatives considered**: In-place overwrite violates failure preservation; database storage is disproportionate; cloud storage is excluded. A two-slot generation manifest is the fallback if replace/rename cannot satisfy platform tests.

## R12 — Exactly-once return reconciliation

**Decision**: Bind an in-process return to the exact checkpoint ID written by its matching departure, validate that source, compute a candidate, durably promote the candidate with a new generation/checkpoint ID and nondecreasing departure timestamp, then publish the world and success summary. If the departure write failed, FR-037 requires the last successfully written active checkpoint and its timestamp as the explicit fallback; any later unsaved live changes are not restored. If no successful checkpoint exists, retain the in-process live state with zero credit. A representable backward clock promotes a zero-credit candidate with the source timestamp; a missing/unrepresentable clock enters `ReturnBlocked` without promotion. Retry rereads durable state first. It reuses an already-captured representable return instant for deterministic promotion retries; when none was valid, it reads exactly one new clock value and binds it for that retry.

**Rationale**: If a crash occurs after durable promotion but before display, the next boot reads the new timestamp/state and cannot replay the old interval. If promotion fails, the old checkpoint remains authoritative and no candidate becomes live.

**Alternatives considered**: Updating a timestamp separately from factory state creates a duplication or lost-credit window. Idempotency tokens in a backend are unavailable and unnecessary for this local boundary.

## R13 — Lifecycle normalization

**Decision**: Normalize Unity callbacks through explicit `Bootstrapping`, `Loading`, `Running`, `Checkpointing`, `Suspended`, `Reconciling` and `ReturnBlocked` states. Treat pause/focus as the mobile departure signal, quit as supplementary, ignore the initialization `OnApplicationPause(false)`, and deduplicate pause→quit and repeated signals. Manual save/development-load failures return to `Running`; reconciliation uncertainty blocks gameplay until retry or authoritative reload resolves it. Backup recovery is explicit and promotes the validated previous generation as a new active generation. Disable background simulation.

**Rationale**: Unity documents that pause callbacks occur on pause/resume and an initial false callback is emitted; it also states iOS commonly suspends rather than reporting quit. A state machine prevents active time and offline time from covering the same interval. [Unity pause callback](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnApplicationPause.html), [Unity quit limitations](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnApplicationQuit.html)

**Alternatives considered**: Quit-only saving misses mobile termination; reacting independently to every callback resets boundaries or duplicates reconciliation.

## R14 — Test runner and MAQA readiness

**Decision**: Before feature implementation, run a separate infrastructure bootstrap: add direct Test Framework `1.8.0`, create non-empty Edit/Play smoke assemblies, and implement `scripts/test-unity.ps1`. Run suites sequentially, require fresh XML with discovered tests, inspect logs/errors, and support reliable `-TestFile` focus before setting MAQA commands. Verify exact arguments and exit behavior against the installed 1.8 package/editor source and an actual run. Configure a tested writable worktree root, then use only the MAQA lane for Feature 001. The installed coordinator bases workers on `origin/<default-branch>`, so reviewed specs/tasks and the harness must be merged/pushed there before first dispatch; accepted prerequisites must likewise reach it before dependent dispatch.

**Rationale**: The project lock resolves Test Framework 1.8.0 but the manifest, test assemblies and commands are absent, so the repository is not implementation-ready yet. The public Unity 6 page establishes Edit/Play support, while the exact 1.8 CLI contract must be proven locally because 2.x documentation is not authoritative for this pin. [Unity Test Framework](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html)

**Alternatives considered**: Treating an empty suite as green violates the constitution. Using an unverified local-only command before MAQA would strand worker branches.

## R15 — Responsive and device validation

**Decision**: Keep current autorotation for F001 and validate `360×640`, `390×844`, `768×1024` plus rotated equivalents and desktop `1280×720`/`1920×1080`. Record actual named Android/iOS devices when available; require macOS/Xcode for native iOS evidence.

**Rationale**: The repository currently permits all rotations. Multiple viewports exercise responsive/safe-area behavior without claiming final shipping device support. Windows emulation cannot prove mobile lifecycle, filesystem or multi-touch behavior.

**Alternatives considered**: Selecting one final orientation would be a product decision not supplied by this feature; claiming mobile acceptance from Game View alone would be false evidence.

## R16 — Performance workload

**Decision**: W001 contains 1,000 independent machines using D001's `2→1/5 s` recipe: 500 active/unblocked, 250 output-blocked and 250 inactive. Each active machine receives input 9,216 and output capacity 4,608, exactly enough for 6.4 hours. Run 100 untimed warmups and 500 independent timed samples, reset outside the timed region, and checksum afterward. Measure plain-Core active/offline `Advance`; report serialization, file and UI costs separately. Initial Windows budgets are p95 ≤10 ms active, p95 ≤100 ms offline and zero steady-state allocations inside `Advance`.

**Rationale**: This is large enough to catch per-cycle/per-object mistakes while remaining an engineering fixture rather than a game-size promise. Record environment, p50/p95, allocations and a full-state checksum. Mobile results are separate baselines.

**Alternatives considered**: No workload would make performance claims unverifiable; a huge speculative logistics factory would test systems outside scope.

## R17 — Placeholder visual production

**Decision**: Create and review `F001-Development-Placeholders v0.1`. Before production its inventory names each asset ID/type, required states, pixel target, file format, pivot/cell mapping, source/import path, provenance and version; implementation supplies simple repository-owned geometric sprites/symbols and text badges.

**Rationale**: The feature needs readable state and non-color cues but not production art. A compact brief gives Art Direction and Technical Art objective import/readability checks without creating an Art Bible or generation pipeline.

**Alternatives considered**: Waiting for final art blocks behavioral validation; ad hoc untracked assets undermine reproducibility; ComfyUI/Blender/audio are unnecessary here.

## Resolved unknowns

No `NEEDS CLARIFICATION` remains. Package pins, architecture, time/resource units, rounding, persistence format, lifecycle model, UI/input stack, viewport matrix, test harness and performance workload are resolved for task generation. Native iOS execution, named physical devices and measured baselines are required evidence during implementation rather than unresolved design choices.
