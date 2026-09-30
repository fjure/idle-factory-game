# What I Learned: First Iron Factory and Prestige Loop

**Feature**: First Iron Factory and Prestige Loop
**Generated**: 2026-09-29 (T093); updated 2026-09-30 (T094, T095, T096, T098)
**Scope**: T093 / Issue #175 (checkpoint generation extraction); T094 / Issue #176 (immutable run content model); T095 / Issue #177 (run state, invariants, SpeedComposer); T096 / Issue #178 (strict content v2 reader and validator); T098 / Issue #180 (RunSimulator)
**Implementation status**: 11/53 tasks completed; 9/46 implementation Issues closed

---

## Key Decisions

### 1. Share the generation transaction, inject the save format

**What we did**: `CheckpointGenerationFiles<TCheckpoint>` owns the durable pending → read-back → retain-previous → promote → verify protocol. An `ICheckpointGenerationCodec<TCheckpoint>` supplies format-specific serialization and validation, and `JsonCheckpointStore` delegates to the shared protocol.

**Why**: Save V1 and Save V2 need the same crash-safe slot protocol even though their bytes and validation rules differ. Keeping one transaction implementation avoids letting the formats drift on interruption and recovery behavior.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Copy the V1 store into a new V2 store | Two copies could disagree about promotion or recovery after a crash. |
| Put JSON parsing inside the generation store | That would couple durability mechanics to one format and make protocol tests depend on serialization. |

**When you'd choose differently**: Separate stores make sense if formats genuinely require different durability guarantees or storage media. Here they share the same slot protocol.

### 2. Give the legacy runtime source read capability only

**What we did**: `SaveV1CheckpointSource` uses `CheckpointGenerationReader<Checkpoint>` through `ICheckpointStorageReader`. The old `JsonCheckpointStore` keeps its writer for compatibility suites, but Feature 003 runtime import uses the reader-only source.

**Why**: The V1 file is migration input that must remain intact. A narrow interface makes accidental writes unavailable through the import source, while the legacy adapter preserves existing tests and file compatibility.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Pass the full read/write backend to the importer | The importer would have write capability it never needs. |
| Delete the V1 writer during extraction | Existing compatibility suites still characterize the old format and behavior. |

**When you'd choose differently**: A migration that intentionally upgrades files in place would need a writer, but that write should be an explicit migration operation with its own recovery contract.

### 3. Distinguish a missing save from a failed filesystem query

**What we did**: `SystemIoCheckpointStorageReader.Exists` uses `File.GetAttributes`; only file-not-found and directory-not-found mean absence. Access or query errors flow to the persistence-failure result.

**Why**: Absence permits cold start, while an inaccessible save must stop import with a typed failure. Treating every failed existence check as absence could silently skip a recoverable legacy save.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use `Directory.Exists` as an absence check | It can collapse filesystem query failures into `false`, hiding the difference between absent and inaccessible storage. |
| Create the directory while checking it | A read-only import should not mutate legacy storage just to determine whether data exists. |

**When you'd choose differently**: A non-authoritative cache can treat an inaccessible path as a cache miss. Durable player saves need an explicit failure.

### 4. New run definitions instead of relaxing the Feature 001 types (T094)

**What we did**: The run got its own immutable types in `Assets/IdleFactory/Core/Run/Content/` (`RunCatalog`, `MachineRecipe`, `EffectDef` and others). `RecipeDefinition`, `BuildingDefinition` and `DefinitionCatalog` were left unchanged.

**Why**: The Miner has no inputs, and the Feature 001 recipe type and the content v1 rule `recipe.inputs.empty` forbid that. Relaxing them would change what existing v1 content and Save V1 mean, and the v1 import still depends on that meaning (R02).

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Allow zero inputs in `RecipeDefinition` | It silently changes v1 validation and the legacy import catalog. |
| Subclass or wrap the v1 types | It couples the new run to limits and semantics designed for a different simulation. |

**When you'd choose differently**: Extend the old type when nothing persisted or imported depends on its current rules. Some duplication is cheaper than a changed meaning for existing data.

### 5. Validate the effect vocabulary in two layers (T094)

**What we did**: `EffectDef` checks only which targets a kind may use and the widest valid value range. Each owner then checks which kinds it may carry: `ItemDef` allows speed and output effects, `ResearchDef` allows speed, output, price and cost effects, and `IndustryUpgradeDef` rejects output effects. `RunCatalog` checks that no output factor exceeds `RunStart.MaxCombinedOutputFactor`.

**Why**: Each rule lives where its data is. An effect does not know its owner, and it does not know the run's cap. The first version validated targets too loosely, hard-coded a default cap of 4 in `EffectDef`, and failed independent QA on both.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| One big validation pass over the finished catalog | Invalid objects would exist until that pass runs, and the diagnostics would lose the local context. |
| A cap parameter with a default on `EffectDef` | Callers forget to pass it, so content is silently checked against the wrong limit. |

**When you'd choose differently**: If content must be reported with all diagnostics at once, as in the JSON validator, collect the errors in a validator and keep the constructors as the last line of defense.

### 6. Nested lists and top-level families get different limits (T094)

**What we did**: `ContentGuard.List` defaults to 256 elements. Only the top-level families and level tables in `RunCatalog` pass 1,024 explicitly.

**Why**: The contract allows 1,024 per family but only 256 per nested list. The first version had one shared guard with 1,024, which accepted nested lists four times larger than the contract allows.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| One constant for all lists | It hides the difference that the contract makes. |
| No domain limits, only the JSON reader's limits | Content built from code, such as test fixtures, would bypass the guard. |

**When you'd choose differently**: If a limit exists only for the parser (depth, byte size), keep it in the reader. Limits that are part of the domain belong in the domain constructor.

### 7. Composite operations on the aggregate root instead of sequencing sub-objects (T095)

**What we did**: Operations that change several objects, such as a sale (stock and cash) or a delivery (stock, cash, RP, Items and the contract counter), are methods on `RunState`: `TrySell`, `TryBuildMachine`, `TryUpgradeMachine`, `TryRemoveMachine` and `TryDeliverContract`. Each one first computes every step, including all overflow checks, and only then writes.

**Why**: The first version offered only the sub-object operations, and the caller was supposed to chain them. If `TryAddCash` failed after `Inventory.TrySell`, the stock was gone without a credit. The architect review rejected that as a value-integrity risk on the auto-sell hot path.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Pre-checks in the application layer | Every caller has to repeat the same order of checks, and a missed check stays invisible. |
| Snapshot and rollback | It needs copies and allocations per event and contradicts the zero-allocation requirement of T098. |

**When you'd choose differently**: If a change spans several independently persisted aggregates, you need a transaction or saga instead. Here everything sits in one in-memory object, so validate-then-commit is enough.

### 8. Encapsulate membership changes, not just the individual values (T095)

**What we did**: `PriorityOrder.TryAppend` and `TryRemove` became `internal`. Machines and priority can change only together through `RunState`. Only the pure permutation `TryReorder` stays public.

**Why**: Each object guarded its own invariant, but "the priority is a permutation of the machines" spans two objects. A public `TryAppend` could have broken it, and only `Validate()` would have noticed afterwards.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Check it in `Validate()` only | The error is found late and far from its cause. |
| Derive the priority from the machine list | The player sets the order, so it is separate state. |

**When you'd choose differently**: If an invariant belongs to a single object, public methods on that object are fine. As soon as it connects two objects, the change belongs to the common owner.

### 9. Restore paths are part of the domain API (T095)

**What we did**: Every persisted field has a validated setter for loading: `TryRestoreInvestment`, `OverdriveState.TryRestore`, `TrySetIndustryLevel`, `TryAddEffect`, `TrySetResearchLevel`. `Validate()` then checks the loaded state as a whole.

**Why**: Investments, Overdrive instants and Industry levels could first only be reached by spending or activating something. A saved game could not have been reconstructed without granting cash or IP again. The review compared each row of the Save V2 table against the API.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Replay the actions on load | Costs and timestamps come out differently than saved, and the load is no longer exact. |
| Public setters without checks | A corrupt or manipulated save would produce an invalid state. |

**When you'd choose differently**: For event-sourced systems, replay is the restore path. Here the save is a snapshot, so the domain needs checked snapshot setters.

### 10. A separate v2 pipeline instead of extending the v1 reader (T096)

**What we did**: Content v2 gets its own `RunContentReader`, `RunContentDtos` and `RunContentValidator` in `Unity/Content/`. They reuse the v1 envelope, limits and diagnostics type read-only, and the v1 loader stays unchanged.

**Why**: v1 still has to validate Save V1 imports exactly as before. A shared reader with a version switch would put every v2 rule at risk of changing v1 behavior, and the reverse.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Extend `GameplayContentReader` with v2 branches | It changes the tested v1 path and mixes two schemas in one token walk. |
| Reflection deserialization into DTOs | Unknown and duplicate members, number ranges and path-free diagnostics would no longer be under our control. |

**When you'd choose differently**: If the old version no longer has to be read, or v2 is a strict superset, one reader with a version switch is cheaper.

### 11. The rule-code table is a tested contract (T096)

**What we did**: Every code in the contract's rule-code table has exactly one test case that triggers the rule and asserts both the code and its path. A consistency test fails if the contract, the guide or the validator names a code that the other two do not know. The schema, guide and reader constants are compared as well.

**Why**: Documentation and code drift apart unnoticed. The test found three real contradictions: the guide example broke its own reward-ratio rule, the schema allowed an `id` that the domain forbids, and several implemented codes were missing from the table.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Documentation review by hand | Drift comes back with the next edit. |
| Generate the documentation from code | The contract is the authority for players and authors, not the code. |

**When you'd choose differently**: For internal codes that no author reads, a table in the code is enough, with no documentation to compare against.

### 12. Worst-case validation reuses the runtime arithmetic (T096)

**What we did**: `speed.level.dead` and `batch.overflow` compose the worst case with every speed source at its maximum using the Core `SpeedComposer`, the same arithmetic the runtime uses.

**Why**: If content is valid, the runtime must never hit the defensive clamp or an overflow. A second copy of the formula could round slightly differently and certify content that fails at runtime.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Estimate the worst case with a closed formula in the validator | It diverges from runtime rounding (floor at each step). |
| Check only at runtime | The author would find the error only during play. |

**When you'd choose differently**: If the runtime function is expensive or not available in the validation context, a conservative upper bound is fine, with a test that it is never tighter than the runtime. The honest downside here: the effect ranges and curve iteration are still copies, and T136 must pin them with a parity test.

### 13. One ordered procedure per timestamp, staged and published once (T098)

**What we did**: `RunSimulator.Advance` jumps from event to event. At each timestamp it runs the same fixed order: expire effects and deadlines, complete machines in priority order, sell, retry the waiting machines (OD-2), then evaluate the run predicate. It works on private staged buffers and publishes them to `RunState` once at the end, or not at all on an overflow.

**Why**: Online play, offline catch-up and every split of an interval must produce the same state. With one procedure and one entry point, splitting is harmless, because an extra timestamp is a no-op. Staging makes an overflow deep inside an 8-hour catch-up reject atomically.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Fixed tick steps | Too expensive for 8 h and dependent on step size, which R01 rejected. |
| Mutate the state directly with a snapshot for rollback | A snapshot per advance costs allocations and still needs a complete copy. |
| Separate offline and online code paths | Equivalence would have to be tested twice and would drift apart. |

**When you'd choose differently**: If the machines never interact, a closed-form calculation per machine is simpler and exact; Feature 001 did that. As soon as they share stock, you need the event loop.

### 14. Test the simulator against a deliberately naive oracle (T098)

**What we did**: `RunSimHarness.TickOracle` walks tick by tick, mutates `RunState` directly and implements the rules a second time, without staging or event skipping. Twelve seeded scenarios compare the complete state fingerprint plus the produced, consumed and sold flows.

**Why**: Equivalence tests between partitions of the same simulator only prove consistency, not correctness. The oracle is slow but so simple that you can check it by reading.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Hand-written expected values only | Covers few cases; random combinations of priority, capacity and batches go untested. |
| Compare the simulator with itself | Tautological; the QA explicitly checked that the oracle does not share the event logic. |

**When you'd choose differently**: If no simple reference implementation exists, use property tests (invariants such as conservation and the N+1 bound) instead. Here we have both.

### 15. Check a benchmark workload for its intended purpose (T098)

**What we did**: The plan's W003-S workload auto-sold every resource. That starves every machine except the Miners, so the load never reaches the event bound it is meant to measure. The worker implemented S literally and added W003-B, which reaches the bound exactly. The owner then made B the budget workload.

**Why**: A benchmark that looks harder on paper than it is produces false confidence: 691,200 instead of 2.76 million completions.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use S unchanged and extrapolate | The extrapolation is based on Miner-only cost per completion and is misleading. |
| Silently change S | The plan would no longer describe what was measured. |

**When you'd choose differently**: Never. A test that proves a load reaches its target, like `WorkloadShape_IsTheDocumentedTwelveSlotFactory` and the exact completion count for B, belongs next to every performance workload.

---

## Concepts to Know

### Capability interfaces

**What it is**: An interface can describe exactly which operations a component is allowed to perform. `ICheckpointStorageReader` exposes reads and metadata, while write operations live on the wider backend interface.

**Where we used it**: `CheckpointGenerationReader<TCheckpoint>` and `SaveV1CheckpointSource` in `Assets/IdleFactory/Unity/Persistence/CheckpointGenerationFiles.cs` and `SaveV1CheckpointSource.cs`.

**Why it matters**: A read-only migration boundary is easier to audit when the source cannot call write operations at all; a test fake can enforce the same rule.

### Crash-safe promotion

**What it is**: A durable update is staged and verified before the active pointer changes, while the previous valid generation is retained for recovery.

**Where we used it**: `CheckpointGenerationFiles<TCheckpoint>.Promote` and the two-manifest selection logic.

**Why it matters**: A process or device failure at any intermediate step should leave a readable generation or a retryable operation, rather than a half-published checkpoint.

### Characterization tests

**What it is**: A characterization test records behavior that must stay compatible while internals are refactored.

**Where we used it**: `CheckpointGenerationFilesTests.cs` checks manifest selection and byte hashes across the six committed Save V1 fixtures; `SaveV1Mapper.cs` and the fixtures stay untouched.

**Why it matters**: The refactor can change where the protocol lives while still proving that existing V1 data selects and decodes the same way.

### Boundary tests (T094)

**What it is**: A boundary test checks the last allowed value and the first rejected one, such as 256 elements accepted and 257 rejected.

**Where we used it**: `NestedListsAccept256AndReject257`, `EffectTargetsMatchTheClosedMatrix` and `OutputFactorUsesRunStartBoundRatherThanFour` in `Assets/IdleFactory/Tests/EditMode/RunCatalogTests.cs`.

**Why it matters**: Off-by-one errors and wrong constants only show up at the edge. Before these tests existed, the too-high 1,024 limit went unnoticed.

### Test-data builders in code (T094)

**What it is**: A builder creates valid domain objects for tests directly in code, without going through the file format.

**Where we used it**: `RunTestCatalogs.IronLoop()` in `Assets/IdleFactory/Tests/EditMode/Fixtures/Run/RunTestCatalogs.cs`, with the literal FR-002 values and optional parameters for the output cap and an Item factor.

**Why it matters**: Simulation and economy tests do not fail because of JSON reader or balance changes, and each test can vary exactly the parameter it is about.

### Two-pass validate-then-commit (T095)

**What it is**: The first pass checks everything and computes the results without writing. The second pass only writes and cannot fail anymore.

**Where we used it**: `GlobalInventory.Apply`, which replaces the earlier array clone, and the `RunState` composites with their `Check*` helpers.

**Why it matters**: You get atomicity without copies. In the simulator's hot path, a clone per event would have produced garbage collection.

### Allocation freedom as a test (T095)

**What it is**: A test counts allocations, using `GcAllocationCounter` samples rather than bytes, over warmed calls and requires zero.

**Where we used it**: `A4_WarmHotPathsDoNotAllocate` in `RunStateTests.cs`. It covers exchange, sale, delivery, compose, batch, rescale and effect expiry. `RemoveAll(e => …)` was replaced by manual compaction, because the capturing lambda allocates a closure per call.

**Why it matters**: Allocations creep in unnoticed, for example through LINQ, lambdas or clones. A test catches them where they are introduced, not later in the profiler on the device.

### Rounding bounds are ≤, not < (T095)

**What it is**: A quotient that is strictly below a bound can reach the bound after rounding up.

**Where we used it**: In the OD-6 cycle, `MulDivCeil(k × D, 10^6, speed)` can be exactly `2 × Tmin`. R03 was corrected from `<` to `≤`, and the tests assert `≤`.

**Why it matters**: A test with a strict bound would fail sporadically on rare inputs, or tempt someone to "fix" the formula when the specification was the problem.

### Strict token walk (T096)

**What it is**: The reader walks the JSON tokens itself and decides for each member whether it is known, required and in range, instead of letting a serializer fill objects.

**Where we used it**: `RunContentReader.cs`, which enforces size, depth, entry and ID limits, rejects floats and oversized integers before the cast, and translates every exception into a diagnostic.

**Why it matters**: Content is input from outside the code. Only an explicit walk rejects duplicate or unknown members reliably and produces messages without file paths.

### Examples are test fixtures (T096)

**What it is**: An example in the documentation is read by a test and has to validate without any patching.

**Where we used it**: `RunContentExample.Valid()` reads the complete example from `docs/content/json-content-pipeline.md`, and `GuideExampleValidatesWithZeroDiagnostics` asserts that it produces zero diagnostics.

**Why it matters**: Authors copy examples. A broken example spreads its errors into real content; here, `floor` rounding violated the reward-ratio rule.

### Partition equivalence (T098)

**What it is**: One long advance and any split of the same interval into shorter advances must produce the same state.

**Where we used it**: `RunSimulationEquivalenceTests`, which uses random cuts, cuts exactly at event instants and one tick beside them, and reports the failing seed and cut list.

**Why it matters**: The game advances in frames online and in segments offline. Without this property, the result would depend on frame rate or app restarts, which players can exploit.

### Bounded retry passes (T098)

**What it is**: After sales, waiting machines may complete in the same timestamp. Each machine completes at most once per timestamp, so at most N+1 passes are needed.

**Where we used it**: the OD-2 retry in `RunSimulator`, the test-only counter `LastMaxPassesPerTimestamp`, and `ReversedChain_NeedsExactlyMachinesPlusOnePasses`. That test also shows why an initial sale is needed at offset 0.

**Why it matters**: A retry loop without a proven bound can become unexpectedly expensive, or even endless, when rules change.

---

## Architecture Overview

The Unity persistence layer owns the filesystem protocol, while a format codec owns checkpoint bytes. `JsonCheckpointStore` remains the legacy writer adapter; `SaveV1CheckpointSource` composes the shared reader with the V1 codec and exposes the Core `ILegacyCheckpointSource` port. This keeps migration read-only and leaves the domain model independent of file operations.

The run content (T094) sits in plain C# under `Core/Run/Content/` and has no Unity dependency. `RunCatalog` is the root and indexes all definitions by ID. It checks rules that span several objects, such as references and the output-factor cap. Single objects check their own invariants in their constructors, and each definition precomputes its curves into per-level tables. The later JSON v2 reader will only build these types; simulation and economy only read them.

The mutable run state (T095) sits next to it under `Core/Run/Domain/`. `RunState` is the aggregate root: it owns the inventory, machines, priority, contracts, effects and Overdrive. Operations that span several objects run only through `RunState`, while sub-objects guard only their own invariants. `SpeedComposer` is stateless pure arithmetic. The simulator (T098) and the application commands (T096 and later) will change this state only through the checked methods.

The content v2 pipeline (T096) sits at the Unity boundary under `Unity/Content/`: reader, then DTOs, then validator. It does not know the Core state; it only uses the Core arithmetic (`SpeedComposer`, `MulDiv`) for worst-case checks. T136 will build the `RunCatalog` from validated DTOs, so the direction stays Unity/Content → Core/Run/Content.

The simulation (T098) sits in `Core/Run/Simulation/`. `RunSimulator` reads the catalog and `RunModifiers`, which fold the factors in fixed ordinal order. It changes `RunState` only through a staged publish at the end of `Advance`. Online session (T099) and offline segmentation (T103) use the same entry point and only supply the sorted boundary events.
