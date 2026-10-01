# What I Learned: First Iron Factory and Prestige Loop

**Feature**: First Iron Factory and Prestige Loop
**Generated**: 2026-09-29 (T093); updated 2026-09-30 to 2026-10-01 (T094, T095, T096, T098, T099, T137, T140, T100, T101, T102, T103, T104, T138, T105)
**Scope**: T093 / Issue #175 (checkpoint generation extraction); T094 / Issue #176 (immutable run content model); T095 / Issue #177 (run state, invariants, SpeedComposer); T096 / Issue #178 (strict content v2 reader and validator); T098 / Issue #180 (RunSimulator); T099 / Issue #181 (RunSession); T137 / Issue #214 (factory commands and snapshots); T140 / Issue #235 (unlock evaluation); T100 / Issue #182 (contract commands); T101 / Issue #183 (Items, Research, Overdrive, Industry); T102 / Issue #184 (Factory Value and prestige); T103 / Issue #185 (offline segmenter); T104 / Issue #186 (Save V2 mapper, PR #247); T138 / Issue #215 (Save V2 store and lifecycle, PR #249); T105 / Issue #187 (durable offline return and prestige, PR #254)
**Implementation status**: 20/54 tasks ticked (closed Issues) plus T105 in review (PR #254)

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

### 16. Two change counters instead of one "dirty" flag (T099)

**What we did**: `RunSession` keeps a `StructureRevision` (machines, levels, effects, Overdrive, contracts) and a `QuantityRevision` (cash, RP, stock, progress). The quantity counter changes only when an exact comparison against a baseline taken before the operation finds a difference.

**Why**: The UI rebuilds expensive views such as slot lists and panels only on structural changes, but updates numbers every frame. With one counter, every production tick would trigger a full rebuild.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Events per property | Many subscriptions, allocations and ordering questions in the hot path. |
| Rebuild everything every frame | Slow snapshot rebuilds that exceed the plan's frame budget. |

**When you'd choose differently**: With little state and a cheap UI, a single counter is enough. The honest downside: structure is partly signalled by the caller, and a forgotten flag leaves views stale. T137 has to check that per command.

### 17. Stable reason codes as strings, separate from the domain enum (T099)

**What we did**: `RunCommandResult<T>` carries the 24 string codes from the contract, such as `cash.insufficient` and `value.limit`. `RunReasons.From` maps the domain's `RunStateResult` and the simulator's `RunAdvanceResult` onto them. Internal failures without a contract code become `command.invalid`.

**Why**: UI copy, tests and later telemetry depend on these codes, so they must not change when the domain renames or splits its enum.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Pass the domain enum up directly | Couples UI and persistence to internal names. |
| Exceptions for rejections | Expensive, and rejections are a normal part of the game, not an error. |

**When you'd choose differently**: If there is only one consumer in the same module, the enum is simpler. Once there are external consumers (UI texts, analytics), a stable contract is worth it.

### 18. Pre-check the follow-up instead of rolling back (T137)

**What we did**: Before every command, `SettleFits` computes an upper bound for the auto-sale income of the settle that follows. If the cash could overflow, the command rejects with `value.limit` before it changes anything.

**Why**: The settle only runs after the command's own change. Without a pre-check, a command could fail after it had already changed the state. `RunState` has no clone, so a rollback would have required an inverse-operation log per command.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Snapshot and rollback | Needs a complete copy or undo log per command; expensive and error-prone. |
| Allow partial success | Breaks the contract rule "a rejection never changes state". |

**When you'd choose differently**: If the bound is hard to compute or often too pessimistic, a dry run on a copy is better. Here the bound over-rejects only within one sale of 10^18 cash, which no real game reaches.

### 19. Close gaps between tasks explicitly (T137)

**What we did**: The review found that no task evaluates the unlock conditions (FR-013). T137 claimed FR-013 in its coverage list but did not deliver an evaluator. On the owner's decision, the new task T140 now closes the gap before T100, and the FR-013 mapping and the work-unit ledger were updated.

**Why**: Without unlocks, Press and Assembler never become available in a real run. That would only have surfaced in the balance trace (T107) or at playtest, long after the cause.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Deliver the evaluator later inside T137 | Would have broadened T137's scope after a PASS review and needed new reviews. |
| Leave it to the UI or T101 | Unlocks at simulator instants belong in the core, not in the presentation. |

**When you'd choose differently**: For small gaps with no dependencies, a note on the next task is enough. Here several stories depend on it, so it gets its own task in the critical path.

### 20. Evaluate unlocks inside the simulator, not afterwards (T140)

**What we did**: The unlock pass runs inside `RunSimulator.ProcessTimestamp`, after completions and sales. When it unlocks something, completions and sales run again, and the pass repeats until nothing unlocks.

**Why**: An unlock can allow a new resource, so a waiting machine completes at the same instant. If the evaluation ran only after the whole advance, the result would depend on how the time was split, which would break online/offline equivalence.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Evaluate in `RunSession` after each advance | Simpler, but an unlock would only take effect at the end of the interval, making the result depend on frame and segment length. |
| Let the UI decide what is visible | Unlocks affect production, so they belong in the deterministic core. |

**When you'd choose differently**: If unlocks were purely cosmetic (menu entries) and never affected production, evaluating in the session after each advance would be enough.

### 21. An optimization that rests on an invariant must name it (T140)

**What we did**: The pass evaluates every candidate only at the first timestamp and afterwards only after a first-time production. That is exact because research and factory level change only in commands that end with `EndCommand`. The code comment and the handoff to T101 name this invariant.

**Why**: Checking all conditions at every timestamp would cost time on every event, while practically nothing changes between events.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Evaluate at every timestamp | Always correct, but unnecessary work on the hot path. |
| Optimize without documenting the invariant | A later command outside `EndCommand` would silently miss unlocks. |

**When you'd choose differently**: If the invariant is hard to guarantee (many mutation paths), the simple, always-correct variant is safer. Measure first, then optimize.

### 22. Keep a derived outcome in memory instead of persisting it (T100)

**What we did**: An expired contract stays in `ContractBoard.LastExpired` as a value type until the next acceptance, so the UI can show the outcome card. The value is not saved.

**Why**: The outcome is presentation state that can be derived. Persisting it would have extended Save V2 with a field whose meaning after a load or an offline absence would be unclear anyway. The value type keeps the expiry path in the simulator allocation-free.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Save the outcome in Save V2 | More save format and migration for a single card. |
| An event from the simulator to the UI | Event objects in the hot path cost allocations, and they are lost on reload too. |

**When you'd choose differently**: If the player must see the card after an app restart, the outcome belongs in the save. The honest downside: the value is sticky, so the offline summary (T105) must compare before and after instead of reading it.

### 23. Trial on the live state instead of a second calculation path (T101)

**What we did**: Item, Research and Industry apply their change briefly as a trial, check with `ChangeFits`/`SettleFits` whether speeds and the follow-up settle stay within bounds, and undo the change inside `try/finally` before the real command.

**Why**: Speeds, cycles and sale income are derived from the live state. A trial reuses exactly the same code as the later settle, so the check and the execution cannot diverge.

**Alternatives considered**:
| Approach | Why it wasn't chosen (for now) |
|----------|---------------------|
| Pure calculation with a hypothetical effect or level | A second input path into `RunModifiers`; cleaner, but more code, and it has to be kept in sync with the real fold. |
| Snapshot and rollback of the whole state | Allocations and a complete copy per command. |

**When you'd choose differently**: With several threads or asynchronous access, a trial on the live state is out of the question. Then the pure calculation is mandatory. The review recommends it as a later refactor. The `try/finally` was added only after the review, because without it an exception would have left the trial state behind.

### 24. Order is data (T101)

**What we did**: `ResearchSnapshot` sorts by prerequisite depth and then by content order. `RunCatalog` now keeps that order in `ResearchInContentOrder`, because the ID dictionary loses it.

**Why**: The UI contract (OD-8) requires the authors' order. The first version sorted by ID, which is deterministic but not what the author intended.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Sort by ID and amend the contract | Content authors would lose control over the arrangement. |
| An explicit order field in the content | A schema change for information the list order already carries. |

**When you'd choose differently**: If order is also needed across files or merges, an explicit order field is more robust than the list position.

### 25. Build the prestige candidate first and publish it later (T102)

**What we did**: `ConfirmPrestige` builds the complete next run as a separate, settled candidate and keeps the old run unchanged. Only `PromotePrestige` replaces it. In between, the session blocks every other command.

**Why**: Prestige must survive a crash in the middle of the transition (R09). Writing and checking the checkpoint belongs exactly between building and publishing, and T105 slots in there. If the write fails, discarding the candidate is enough; nothing has to be rolled back.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Reset the live run directly and save afterwards | A crash in between loses the old run or grants IP twice. |
| Save first, then build the run from the save | Couples prestige to the save format and duplicates the rebuild logic. |

**When you'd choose differently**: If a transition does not have to survive a crash (for example a purely cosmetic UI switch), changing the state directly is enough. Here IP is at stake, so idempotency via `previousRunId` is required.

### 26. Value stock with the sale formula, not a similar one (T102)

**What we did**: Factory Value values each resource with exactly `MulDivFloor(units × basePrice, salePricePpm, 10^6)`, the same function as the auto-sale.

**Why**: If the two formulas differed, selling just before prestige would either gain or lose value. A test compares FV directly against the income of a real sale.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Unrounded valuation (rounding only at the end) | Differs from sales by up to $1 per resource; it invites optimizing sales before prestige. |
| Base price without modifiers | Research and Industry sale bonuses would be worth nothing at prestige. |

**When you'd choose differently**: If valuation were meant to be deliberately more conservative than a sale, for example as a "liquidation discount", that would be a documented game design decision, not an accident of rounding.

### 27. On an invalid clock, do nothing at all (T103)

**What we did**: If the return time is missing, before the departure, or unrepresentable, `OfflineSegmenter.Reconcile` changes nothing. It does not even process events that are already due, and it reports `IsRetryable = true`.

**Why**: A partial state change on a broken clock would move the game without a valid timeline. An untouched state lets the player retry after correcting the clock, and nothing is expired twice or un-expired.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Process already-due boundaries at 0 | Moves state on an invalid basis, and the next valid return would process them again. |
| Treat the clock as "now" | Hands out free time or loses it, depending on how far the clock is off. |

**When you'd choose differently**: With a trusted server clock you could correct the local clock yourself. Offline without a server, "do nothing and retry" is the safest option.

### 28. A summary claims only what happened in the window (T103)

**What we did**: The offline summary reports only effects and Overdrive phases that ended in `(departure, return]`. Older ones are still processed, but not claimed.

**Why**: The review found that a boost that ended before the departure was reported as "ended during the absence", which is wrong information for the player. The simulation was correct; only the report was wrong.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Report every expiry processed | Technically true, but misleading to the player. |
| Stop processing old expiries | Would break the state that was carried over by a load. |

**When you'd choose differently**: For a debug log, "everything processed" is right. For player text, "what happened while you were away" is right.

### 29. A save is hostile input: lexical pre-pass plus token walk (T104)

**What we did**: `SaveV2Mapper` first checks the bytes lexically: UTF-8 without BOM, size, depth, canonical integers, no escapes. It then walks the tokens and records per object which members it has seen. Duplicate, unknown and missing members are rejected, and error messages carry only static codes.

**Why**: Newtonsoft accepts duplicate keys and lets the last one win. A save could then contain two values, and each check would see a different one. Static codes make sure no file content ends up in logs.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| `JsonConvert.DeserializeObject` into DTOs | Last-wins for duplicates, lenient number parsing, error messages containing file content. |
| JSON schema validation | Would not see duplicates either and would need an extra dependency. |

**When you'd choose differently**: For data that only your own server produces and signs, a normal deserializer is enough. Local save files can be edited by anyone.

### 30. Do not break balance changes through the save format (T104)

**What we did**: On the owner's decision, a contract's required quantity counts from the value fixed at acceptance. The stricter check against the current content table was removed again.

**Why**: Content is retuned regularly. A rule that compares saves against the current balance table would make every save with an active contract unloadable after a balance change.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Compare strictly against the current table | Blocks tamper attempts, but also blocks every legitimate retune. |
| Migrate saves on every retune | Effort per balance change, for data the player cannot tamper with meaningfully anyway (local risk SEC-4). |

**When you'd choose differently**: For values that grant direct value, an upper bound makes sense. The open question is whether the duration rules that still check exactly should be relaxed the same way.

---

### 31. Install what the store returns, not what you handed it (T138)

**What we did**: After a promotion, `RunPersistence` reads the generation back from the store and installs that fresh copy into the session. The candidate object it promoted is never installed or settled.

**Why**: The first version installed the candidate's own objects. When the install failed after the settle had already changed them, a retry re-promoted mutated bytes, and the generation protocol rejected them as a same-generation mismatch. Reading back makes the durable bytes the single source for both publication and retry.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Deep-copy the candidate before install | Needs a copy routine for every domain type, which can drift from the codec. |
| Keep the candidate's serialized bytes and rebuild on retry | Equivalent, but duplicates what the store already does on read. |

**When you'd choose differently**: If reading back were expensive on a hot path, a snapshot of the bytes would be the cheaper choice. Here it runs only on return, reload and recovery.

---

### 32. A lone pending file is not a checkpoint (T138)

**What we did**: At cold start, only data or manifest files in `v2/` count as an existing Save V2 state. A lone pending file, left when the very first promotion stopped before any data slot, counts as no files, so a V1 import may run again (owner decision; contract rules 2 and 3 amended).

**Why**: The contract contradicted itself: rule 2 blocked every import when any file existed, while the OPS-2 row recomputes the import after an unpromoted pending file. Nothing was ever published in that state, so importing again cannot credit anything twice, while blocking would strand the player in front of an empty store.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Any file blocks the import (literal rule 2) | Leaves the player blocked after an interrupted first save, without any data to recover. |
| Delete the pending file at probe time | Mutates storage in a read-only probe and hides the evidence of the interruption. |

**When you'd choose differently**: If the pending file could contain a published state, for example with a protocol that promotes by renaming the pending file, it would have to count as a checkpoint.

---

### 33. The durable successor is the idempotency key (T105)

**What we did**: Before committing a prestige, `RunPersistence` reads the durable active checkpoint. If its `previousRunId` equals the live `runId`, the transition already happened and is only published; otherwise the same candidate bytes with the same checkpoint ID are promoted again.

**Why**: A crash or I/O error can strike after the manifest swap became durable but before the session saw success. Asking the store "is my successor already there?" turns every retry and restart into a safe re-check, so the IP award can never be paid twice.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| A separate "transition done" flag file | A second durable write with its own failure window. |
| Comparing IP or prestige count | Ambiguous for run 2 → 3 and breaks if Industry purchases change IP. |

**When you'd choose differently**: With a transactional database, one atomic transaction would make the re-check unnecessary.

---

### 34. When you cannot know, fail closed (T105)

**What we did**: Cancelling a pending prestige is rejected with `persistence.failed` when a promotion was issued and the store cannot be read. It stays permissive only if no promotion was ever attempted.

**Why**: The first version let cancel succeed on an unreadable store. If the promotion had become durable, a restart showed the cancelled prestige, or the next autosave silently overwrote it. The review caught this; a fault-injection sweep then showed 30 failure points where the new run was already durable.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Always allow cancel | Contradicts "a durable promotion cannot be cancelled". |
| Always reject cancel on read failure | Traps the player behind a dead store even when nothing could be durable. |

**When you'd choose differently**: If the store reported "manifest written" reliably before any later step could fail, the flag would be unnecessary.

---

### 35. Forfeit instead of blocking at the value limit (T105)

**What we did**: When the offline simulation hits the 10^18 value limit, the credit for that absence is forfeited. The state stays as it was, the departure moves to the return instant, and the summary reports `offline.value-limit` (owner decision).

**Why**: The rejection is deterministic, so a retry would fail forever and block the player. Moving the departure makes the forfeit exactly-once like any other return.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Block the return | A dead end that only reload or recovery could leave. |
| Credit up to the limit | Needs a partial simulation with a second budget, for a case that only extreme or forged saves reach. |

**When you'd choose differently**: If normal play could reach the limit, partial credit would be worth its complexity.

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

### Settle after a command (T099)

**What it is**: After every player action, the session runs the simulator for zero time. Auto-sales happen first, then pending completions, then the check of which machines are running.

**Where we used it**: `RunSession.BeginCommand`/`EndCommand`, which calls `RunSimulator.Advance` with 0 ticks.

**Why it matters**: The state after a command matches exactly what the next frame would show, and offline and online do not diverge. The pitfall is that this runs after the command's own change, so a settle overflow must be ruled out before the change (T137).

### Caller-provided buffers (T099)

**What it is**: Read methods write into an array or `Span` that the caller passes in, and return the full count, instead of returning new lists.

**Where we used it**: `CopyInventory` and `CopyMachineStates` in `RunSession`.

**Why it matters**: The UI reads every frame. New lists would create garbage every frame and cause GC pauses on mobile.

### Read models separate from commands (T137)

**What it is**: Commands change state and return only a result with a reason code. The UI gets its data from separate, immutable snapshots.

**Where we used it**: `RunCommands.Factory.cs` (write path) and `RunSnapshots.cs` (`RunFactorySnapshot`, `RatesSnapshot`, `EconomySnapshot`).

**Why it matters**: Every screen can get exactly the view it needs (for example, blockers with corrective actions) without the commands becoming bloated. Snapshots allocate, so the UI builds them only when a revision changed.

### Fixed-point iteration (T140)

**What it is**: A step is repeated until it no longer changes anything, so the state is "stable" under the rule.

**Where we used it**: the unlock pass in `RunSimulator` (completions, sales and unlocks alternate until no unlock occurs), and in the tick-by-tick oracle `TickOracle.UnlockRound`.

**Why it matters**: Chains such as "Bar produced unlocks Press, Press produces Plate, which unlocks Assembler" resolve completely at one instant. Because every published state is a fixed point, the "produced" set does not need to be persisted.

### Index-scaled rewards (T100)

**What it is**: A contract's quantity and rewards are read from precomputed tables at the current completion count, not computed on the fly.

**Where we used it**: `AcceptContract` fixes `requiredUnits` from `QuantityTable[count]`, and `DeliverContract` reads cash and RP from `ValueTable[count]` in `RunCommands.Contracts.cs`.

**Why it matters**: The values are deterministic and validated by the content validator (reward ratio ECO-5). Accepting and abandoning again cannot improve them, because only a delivery raises the count.

### Conflict keys for effects (T101)

**What it is**: Each effect has a key `(effect kind, target)`. At most one effect with the same key may be active at a time. Different keys stack multiplicatively.

**Where we used it**: `ActivateItem` in `RunCommands.Items.cs`. Factory Items use the target `factory`, and machine-type Items use the machine type ID.

**Why it matters**: The rule is simple enough for the UI ("already active on this target") and prevents unbounded stacking of the same boost.

### One source for derived values (T102)

**What it is**: A value that several places need, such as the starting cash of the next run, is computed by exactly one function.

**Where we used it**: `NextRunStart.Compute`, used by both `IndustrySnapshot` (display) and `PrestigeTransition` (the real start).

**Why it matters**: The display and the real behavior cannot diverge. The T101 review flagged the risk, and T102 extracted the formula before it existed twice.

### Time mapping instead of simulating the time (T103)

**What it is**: Real absence time is mapped with a monotonic function `E(x)` onto effective simulation time: 4/5 of the time, at most 8 h. Every real event is placed at `E(event)`.

**Where we used it**: `OfflineSegmenter.CreditedTicks`/`EffectiveTicks` and `MapBoundaries`.

**Why it matters**: Because `E` is monotonic and telescopes, one run over `E(r)` gives the same result as any split into segments. Offline progress therefore uses the same simulator as online play.

### Golden fixtures with a hash table (T104)

**What it is**: Reference files are committed. A test regenerates them in memory and compares them byte for byte. They are regenerated only on an explicit environment variable, and the README lists a SHA-256 for each file.

**Where we used it**: `Tests/EditMode/Fixtures/SaveV2/` and `SaveV2FixtureTests` with `IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES=1`.

**Why it matters**: Any unintended change to the save format fails immediately, and an intended one shows up in review as a hash change. The test never skips, so drift cannot slip through silently.

---

### Probe before you read (T138)

**What it is**: A cheap presence check (`ProbeFiles()`: no files, pending only, published files) that runs separately from the strict read. It answers "is there anything here?" without deciding whether it is valid.

**Where we used it**: `IRunCheckpointStore.ProbeFiles()` and the cold-start decision in `RunPersistence` and the future bootstrap (T106).

**Why it matters**: A strict read that fails cannot tell "empty" from "corrupt". Without the probe, a corrupt save would look like a fresh start and could trigger a V1 import over prestige progress (SEC-1).

---

### Fault-injection sweeps (T105)

**What it is**: Run the operation once cleanly and record every backend call, then repeat it N times, each time failing exactly call i. After each failure, retry and restart, and assert one of the allowed outcomes, for example "old run, no award" or "new run, exactly one award".

**Where we used it**: `PrestigeTransitionTests`, `OfflineReturnDurabilityTests` and `SaveV2StoreTests`, with the `DurabilityRig` and the scripted storage backend.

**Why it matters**: Exactly-once bugs hide in specific crash points. A sweep tests all of them, and asserting that each allowed outcome actually occurs keeps the sweep from passing vacuously.

---

## Architecture Overview

The Unity persistence layer owns the filesystem protocol, while a format codec owns checkpoint bytes. `JsonCheckpointStore` remains the legacy writer adapter; `SaveV1CheckpointSource` composes the shared reader with the V1 codec and exposes the Core `ILegacyCheckpointSource` port. This keeps migration read-only and leaves the domain model independent of file operations.

The run content (T094) sits in plain C# under `Core/Run/Content/` and has no Unity dependency. `RunCatalog` is the root and indexes all definitions by ID. It checks rules that span several objects, such as references and the output-factor cap. Single objects check their own invariants in their constructors, and each definition precomputes its curves into per-level tables. The later JSON v2 reader will only build these types; simulation and economy only read them.

The mutable run state (T095) sits next to it under `Core/Run/Domain/`. `RunState` is the aggregate root: it owns the inventory, machines, priority, contracts, effects and Overdrive. Operations that span several objects run only through `RunState`, while sub-objects guard only their own invariants. `SpeedComposer` is stateless pure arithmetic. The simulator (T098) and the application commands (T096 and later) will change this state only through the checked methods.

The content v2 pipeline (T096) sits at the Unity boundary under `Unity/Content/`: reader, then DTOs, then validator. It does not know the Core state; it only uses the Core arithmetic (`SpeedComposer`, `MulDiv`) for worst-case checks. T136 will build the `RunCatalog` from validated DTOs, so the direction stays Unity/Content → Core/Run/Content.

The simulation (T098) sits in `Core/Run/Simulation/`. `RunSimulator` reads the catalog and `RunModifiers`, which fold the factors in fixed ordinal order. It changes `RunState` only through a staged publish at the end of `Advance`. Online session (T099) and offline segmentation (T103) use the same entry point and only supply the sorted boundary events.

The application layer (T099) starts in `Core/Run/Application/`. `RunSession` owns the timeline and the change counters, derives the boundary events and calls the simulator. The Unity presentation only reads through the hot reads and reacts to the two revisions. Commands (T137) go through `BeginCommand`/`EndCommand`, and persistence and offline segmentation (T103 and later) through `Advance`.

The command layer (T137) sits on top of `RunSession` as a `partial class RunCommands`, and the read models as `static partial class RunSnapshots`. Later features (contracts, Items, prestige) add their own partial files without changing the existing ones.

Unlocks (T140) live in the simulator because they affect production. `RunState.Unlocked` is the only persisted authority. Commands, snapshots and later tasks read it with `IsUnlocked`, and Overdrive uses the reserved id `overdrive`.

The contracts (T100) are a further partial file of `RunCommands` and `RunSnapshots`. Delivery uses the atomic domain composite `RunState.TryDeliverContract` and, before it, the settle pre-check that subtracts the delivered stock.

The boosts and progression (T101) are three more partial files of `RunCommands` plus `RunSnapshots.Items`. Industry lives in `PermanentState`, and Items, Research and Overdrive live in `RunState`. That split is exactly the one the prestige reset (T102) needs.

Prestige (T102) splits into `FactoryValuation` (pure calculation), `PrestigeTransition` (building the candidate) and `RunCommands.Prestige` (preview, confirm, promote, cancel). The session holds the pending candidate. T105 adds the durable write between Confirm and Promote.

The offline return (T103) lives in `OfflineSegmenter` (simulation layer) and `RunOfflineSummary` (application layer), and only adds new files. The integration into `RunSession.ResumeAndReconcile` with "count exactly once" and rebinding is the job of T105.

Save V2 (T104) sits at the Unity boundary under `Unity/Persistence/`. The mapper knows the Core domain only through its constructors and restore paths. It does not rescale or settle: loading, rescaling, reconciling and the durable promotion belong to T138 and T105.
