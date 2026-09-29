# What I Learned: First Iron Factory and Prestige Loop

**Feature**: First Iron Factory and Prestige Loop
**Generated**: 2026-09-29 (T093); updated 2026-09-30 (T094, T095)
**Scope**: T093 / Issue #175 (checkpoint generation extraction); T094 / Issue #176 (immutable run content model); T095 / Issue #177 (run state, invariants, SpeedComposer)
**Implementation status**: 9/53 tasks completed; 7/46 implementation Issues closed

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

---

## Architecture Overview

The Unity persistence layer owns the filesystem protocol, while a format codec owns checkpoint bytes. `JsonCheckpointStore` remains the legacy writer adapter; `SaveV1CheckpointSource` composes the shared reader with the V1 codec and exposes the Core `ILegacyCheckpointSource` port. This keeps migration read-only and leaves the domain model independent of file operations.

The run content (T094) sits in plain C# under `Core/Run/Content/` and has no Unity dependency. `RunCatalog` is the root and indexes all definitions by ID. It checks rules that span several objects, such as references and the output-factor cap. Single objects check their own invariants in their constructors, and each definition precomputes its curves into per-level tables. The later JSON v2 reader will only build these types; simulation and economy only read them.

The mutable run state (T095) sits next to it under `Core/Run/Domain/`. `RunState` is the aggregate root: it owns the inventory, machines, priority, contracts, effects and Overdrive. Operations that span several objects run only through `RunState`, while sub-objects guard only their own invariants. `SpeedComposer` is stateless pure arithmetic. The simulator (T098) and the application commands (T096 and later) will change this state only through the checked methods.
