# What I Learned: First Iron Factory and Prestige Loop

**Feature**: First Iron Factory and Prestige Loop
**Generated**: 2026-09-29 (T093); updated 2026-09-30 to 2026-10-01 (T094, T095, T096, T098, T099, T137, T140, T100, T101, T102, T103, T104, T138, T105, T106, T136); 2026-10-07 (T097, T107, T108, T109, T142, T143, T110, T111, T112, T141, T139, T191, T193, T192); 2026-10-08 (T113, T114, T115, T116, T117, T118, T194, T195, T196, T119, T243, T120, T244, T123, T121, T122); 2026-10-09 batch (T246, T247); 2026-10-09 batch 2 (T126, T127, T248, T249, T250); 2026-10-09 review batch (T128, T129, T130, T131, T132); 2026-10-10 closeout (T251, T133)
**Scope**: T093 / Issue #175 (checkpoint generation extraction); T094 / Issue #176 (immutable run content model); T095 / Issue #177 (run state, invariants, SpeedComposer); T096 / Issue #178 (strict content v2 reader and validator); T098 / Issue #180 (RunSimulator); T099 / Issue #181 (RunSession); T137 / Issue #214 (factory commands and snapshots); T140 / Issue #235 (unlock evaluation); T100 / Issue #182 (contract commands); T101 / Issue #183 (Items, Research, Overdrive, Industry); T102 / Issue #184 (Factory Value and prestige); T103 / Issue #185 (offline segmenter); T104 / Issue #186 (Save V2 mapper, PR #247); T138 / Issue #215 (Save V2 store and lifecycle, PR #249); T105 / Issue #187 (durable offline return and prestige, PR #254); T106 / Issue #188 (V1 import and cold start, PR #257); T136 / Issue #213 (catalog builder and content dispatcher, PR #261); T097 / #179 (content sheet r1, PR #264); T108 / #190, T109 / #191, T142 / #268, T143 / #269 (placeholder art and reviews, PRs #263, #265, #270, #272); T107 / #189 (balance trace, PR #271); T110 / #192 (V2 bootstrap, PR #266); T141 / #259 (Save V2 hardening, PR #273); T111 / #193 (sprite import, PR #274); T112 / #194 (run shell, PR #275); T139 / #216 (shell gate review, PR #277); T191 / #278 (device profile and text scale, PR #281); T193 / #283 (Play Mode test assembly compiles for the Windows player, PR #284); T192 / #279 (shell polish and robustness, PR #286); T113 / #195, T114 / #196, T115 / #197, T116 / #198, T117 / #199, T118 / #200 (panels, PRs #291, #290, #293, #295, #292, #294); T194 / #288 (panel composition, PR #299); T195 / #296 (cold-start recovery, PR #298); T196 / #300 (panel icons and stale Item target, PR #305); T119 / #201 (Feature 001 runtime retirement, PR #308); T243 / #307 (Market warning icons, PR #309); T120 / #202 (full-suite evidence, PR #311); T244 / #315 (retired GUID assertion, PR #316); T123 / #205 (emulator journey script, PR #318); T121 / #203 (player journey and transition tests, PR #319); T122 / #204 (W003 player timing, PR #320); T124 / #206 (Android emulator evidence, PR #325); T245 / #323 (shipped-catalog device sets, PR #324); T125 / #207 (content revision 2 and balance trace r2, PR #329; player compile fix ac4d719); T251 / #350 (Android Back regression, PR #351); T133 (final acceptance, PR #352)
**Implementation status**: 70/72 tasks ticked (closed Issues mirrored 2026-10-10; T126, T127 and T133 ticked by the parent; T134 and T135 are the closeout in progress)

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

### 36. A failed read is not an empty store (T106)

**What we did**: When the cold start cannot read or probe the V2 store, it blocks with `persistence.failed` and offers a retry of the whole decision. It no longer hands over to the generic resume path.

**Why**: The generic path read again, saw no checkpoint and started a fresh run. The first autosave then made that fresh run authoritative, so one transient I/O error silently and permanently skipped the player's V1 save. Review and QA found it independently.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Retry the read a few times automatically | Hides the problem but still falls through to a fresh run if the error persists a little longer. |
| Treat any read failure as "no save" | Exactly the bug: an unknown state is decided as the most destructive one. |

**When you'd choose differently**: If a fresh run could never become authoritative over an older save (for example with a merge step), falling through would be harmless.

---

### 37. A recovery action needs a precise eligibility rule (T106)

**What we did**: "Discard damaged save and restart" is offered only from the cold start, when no valid V2 generation exists and the active read is invalid or has no manifest. An unsupported save version and storage failures are refused (owner decision).

**Why**: Each review round sharpened the rule. The first version would have overwritten a valid save from another app version, and the next one was so strict that an interrupted first save stayed a dead end. The final rule follows from the store's semantics: a data slot without a manifest was never published.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Offer discard in any blocked state | Loses valid progress after a downgrade or mid-session. |
| No discard, only retry | Leaves a permanently bricked start after a torn first save. |

**When you'd choose differently**: With a manifest-repair operation, repair would be the first choice and discard only the fallback.

---

### 38. Peek with the same rules you parse with (T136)

**What we did**: The dispatcher reads `contentSchemaVersion` through the same strict byte and token checks as the v2 reader (shared `TryDecode`), and rejects a duplicate version member before it routes the document.

**Why**: A version peek is a trust boundary. A looser peek (for example a regex or a lenient parser) could route a document as v1 while a strict reader would see v2, or accept a BOM or a second version member that last-wins parsing resolves differently.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Regex or substring search for the version | Matches inside strings or nested objects and ignores duplicates. |
| Parse fully twice with a lenient parser first | Doubles the work and still lets the two parsers disagree. |

**When you'd choose differently**: If versions were carried outside the document (a file name or a container header), the peek would not need to parse JSON at all.

---

### 39. Reject unreachable content statically, but optimistically (T097)

**What we did**: The content validator gained `id.reserved` (the ID `overdrive` belongs to `RunState`) and `unlock.unreachable`, a fixpoint from the initial unlocks plus always-true conditions. The fixpoint treats factory levels as attainable and ignores affordability.

**Why**: A chain that can never fire leaves content locked forever without any error. An exact check would need the economy simulation, so the validator proves only what it can prove cheaply and leaves pacing to the balance trace.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| No static check, rely on playtests | Dead content is found late, if ever. |
| Simulate affordability in the validator | Duplicates the economy and makes validation slow and fragile. |

**When you'd choose differently**: When content is authored by players or modders, a stricter, pessimistic check is worth its cost.

---

### 40. A balance trace reports bands; it does not assert them (T107)

**What we did**: `BalanceTraceRunner` drives the real commands under a written reference policy and compares the output byte for byte with a golden CSV. The tests assert determinism, structure and the SC-001 chain, and only report pass/fail of the pacing bands. Revision 1 misses every band, and a scaled scenario (threshold divided by 100) proves the chain.

**Why**: Asserting the bands would make every retune a red build and would tempt someone to tune the policy instead of the content. Reporting them honestly gives T125 a baseline.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Assert the bands now | The first content hypothesis fails; the test would be skipped or weakened. |
| A separate economy model | It can disagree with the real commands (the author's fluid model said 61 min; reality is more than 6 h). |

**When you'd choose differently**: Once content is approved (T127), the bands become assertions so a regression is caught.

---

### 41. Start-up validates everything before touching a save (T110)

**What we did**: `Initialize()` checks references, lays out the HUD, loads content through the dispatcher with a sprite-key resolver, and only then opens the Save V2 store. The legacy catalog is read only if a `v1/` directory exists. Every failure disables start-up with a coded, path-free diagnostic and creates no store.

**Why**: Opening the store first could write or migrate files for a build whose content is broken, and a later fix could no longer read them.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Load the save first, content lazily | A broken build could still write a checkpoint. |
| Always load the legacy catalog | Costs time and couples every start to V1 data that most players no longer have. |

**When you'd choose differently**: With content downloaded at runtime, start-up would have to show an offline state instead of failing hard.

---

### 42. Validate world size and pivot, not pixel size (T110, T111)

**What we did**: `PresentationSpriteCatalog` checks each sprite's world size (pixels divided by PPU) and normalized pivot, and entries may hold frame lists. T111 imported the 78 F003 sprites at 64 PPU and registered every key.

**Why**: Feature 004 will replace the art at 32 PPU. The gameplay grid cares about world units, so a pixel check would force a code change for a pure art swap.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Exact pixel dimensions per key | Breaks the planned presentation-only restyle. |
| No validation | A wrong pivot shows up only as a misplaced machine on a device. |

**When you'd choose differently**: For pixel-perfect UI icons rendered at fixed sizes, a pixel check is the more direct contract.

---

### 43. Re-derive safety preconditions inside the store (T141)

**What we did**: `SupersedeDamaged` re-reads the manifest and both generations under the store's operation gate and refuses when any generation is valid, on a storage failure, an unsupported version or an unresolved ID. A save from another build gets its own outcome, `UnresolvedContent`, and is never discardable.

**Why**: A caller's earlier `CanDiscardDamagedSave` answer can be stale by the time it acts, and a transient I/O error must never be mistaken for damage. Overwriting the only copy of a player's save is irreversible.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Trust the caller's eligibility check | Time-of-check/time-of-use gap on an irreversible action. |
| Repair the manifest | Out of scope; the owner chose a confirmed discard. |

**When you'd choose differently**: If saves were also backed up in the cloud, a less defensive local discard would be acceptable.

---

### 44. Extend the shell through a registry, not by editing it (T112)

**What we did**: Panels T113–T118 mount through `ShellPanelRegistry.Register(IShellPanel)`, which returns a disposable handle. The shell owns layout, navigation, the shared confirmation dialog and the exact-value line.

**Why**: Six panel tasks run in parallel. If each edited the shell or the scene, they would conflict and each would re-implement focus and rotation rules.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Each panel adds its UXML into `FactoryRun.uxml` | Merge conflicts and no single place for focus rules. |
| One panel per scene object | Scene edits per panel, each with a Unity re-save. |

**When you'd choose differently**: For a UI with two or three fixed screens, a static layout is simpler than a registry.

---

### 45. Scene edits go through Unity, even when the diff is noisy (owner decision 2026-10-07)

**What we did**: After three tasks (T110, T111, T112) had inserted scene fields as YAML, the owner ruled that scenes and prefabs change only through Unity tooling. In a worktree that is a temporary `-executeMethod` editor script. The T112 re-save serialized the new catalog fields into all 99 entries, which was accepted as a one-time migration.

**Why**: Hand-written YAML bypasses Unity's own serialization and validation; a small diff is not proof of a correct one. The "noise" was a real consequence of the T110 schema change.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Hand-edit YAML for minimal diffs | Not trusted by the owner, and it hides pending migrations. |
| Unity MCP in the worktree | MCP is bound to the editor on the main checkout. |

**When you'd choose differently**: Never for scenes; for plain text assets (JSON, USS) a text edit is the normal tool.

---

### 46. Review the shell once before six panels fan out (T139)

**What we did**: Before the panel Issues T113–T118 started, an independent UI/UX reviewer gated the T112 shell across all eight screen classes. The verdict was PASS with ten non-blocking findings F-1 to F-10. Two of them, F-4 (text scale) and F-10 (panel scale), became owner decisions and the new task T191, which every panel waits for.

**Why**: Six panels inherit every shell rule. A defect found after they fan out has to be fixed six times, and the panels would each add their own fixed font sizes.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Review each panel on its own | Shell defects would be found late and repaired per panel. |
| Fix F-4/F-10 inside the first panel | Hides a shared contract change in one panel's diff. |

**When you'd choose differently**: If only one consumer builds on a base, review base and consumer together.

---

### 47. Layout by panel points, touch behaviour by device profile (T191, F-10)

**What we did**: The layout class still comes from the panel-point size, so a landscape phone may get the desktop layout. A separate `ShellDeviceProfile` decides input behaviour: tap-to-pin and long-press for touch, hover only for a pointer.

**Why**: Size and input are independent. A large landscape phone needs a wide layout, but it still has no hover. Inferring touch from "phone class" breaks exactly there.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Infer touch from the layout class | Wrong for landscape phones and touch laptops. |
| Force the phone layout on every phone | Wastes width the owner wanted to use (owner decision 2026-10-07). |

**When you'd choose differently**: On a single-platform app with one input method, one class can carry both.

---

### 48. Raise the panel scale at runtime instead of editing the asset (T191)

**What we did**: `ShellPanelScale` raises the live `PanelSettings` scale so that 44 pt ≥ 7 mm on touch devices with known DPI, and restores it on teardown. Pointer devices and unknown DPI keep the authored scale, and the fallback is recorded.

**Why**: The authored `ScaleWithScreenSize` settings are right for desktop and are shared with the Feature 001 HUD. A physical-size rule needs the DPI, which only exists at runtime.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Change the asset to `ConstantPhysicalSize` | Changes desktop and HUD scale too, and breaks on unknown DPI. |
| Per-device asset variants | Many assets with no device data to pick them. |

**When you'd choose differently**: If every UIDocument wanted the same physical rule, set it once in the asset. The cost of the runtime raise: it mutates a shared object, so every document on it sees the raise (to be checked on a device in T124).

---

### 49. One shell-owned text scale through USS variables (T191, F-4)

**What we did**: `ShellTextScale` turns the Android font scale into one factor from 100 to 200 % and puts a `run-text-scale--NNN` class on the shell root. Every font size uses `var(--run-fs-*)`, and the panel rule in `contracts/ui-surfaces.md` forbids fixed sizes.

**Why**: Panels inherit the variables, so a new panel scales correctly without code. One class on the root also lets layouts adapt at large text with plain descendant selectors.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Each panel reads the font scale itself | Six copies of clamping and rounding that drift apart. |
| Scale the whole panel | Enlarges controls and spacing too, and squeezes the layout twice. |
| An in-game setting | Out of scope for Feature 003; the system setting is the accessibility path. |

**When you'd choose differently**: With a design system that has a token pipeline, generate these variables from tokens instead of hand-writing them.

### 50. Guard the Editor-only callers instead of splitting the test assembly (T193)

**What we did**: In the Play Mode test assembly, every caller of an Editor-only helper sits inside the same `#if UNITY_EDITOR` as the helper. A helper that needs no Editor API, such as `RunSceneRun.Stop`, moved out of the guard. A helper that loads assets through `AssetDatabase`, such as `WriteV1Checkpoint`, moved in. Three shell layout fixtures are guarded as whole files, because `ShellRig` loads the authored UXML only in the Editor. `SlotGridTests` and `RunDeviceProfileTests` guard only their asset-dependent sections.

**Why**: The player build compiles the whole `IdleFactory.PlayModeTests` assembly, even though it runs only the `StandalonePlayer` tests. Guards keep the assembly, the test names and the runner filters unchanged. The counts proved that: Edit Mode 1585 and Play Mode 513 before and after, and the player suite at 6/6. Pure tests still compile in the player, so they could be tagged later.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| A separate Editor-only Play Mode assembly | Moves files and `.meta`, changes assembly names in runner filters and evidence, and splits the shared fixtures across two assemblies. The owner chose guards on 2026-10-07. |
| Make the helpers work in the player (Resources or StreamingAssets copies) | Duplicates content assets only for tests, and the shell rig would still need the authored UXML. |
| Guard each failing call site one by one | Scatters guards through helpers like `ApplyWorstCaseStrip`, which many tests share. A whole-file guard is clearer when no test in the file can run in a player. |

**When you'd choose differently**: Once Editor-only Play Mode fixtures clearly outnumber the player-capable ones, a dedicated Editor-only assembly (`includePlatforms: ["Editor"]`) is cleaner than many guards.

### 51. Notices that know when they stop being true (T192)

**What we did**: `RunShellDocument.ShowNotice` now takes a kind (Info, Warning, Error, Lock) and a `sticky` flag. A transient notice clears after `NoticeSeconds`, on a tab, sheet or slot change, on any UI press and on a world press. The presenter calls `ReconcileLockNotice` on every render, so a locked-slot notice disappears once the slot unlocks. A sticky notice such as "gameplay unavailable" comes back after a clear.

**Why**: The T139 gate (F-1) found notices that never expired, so stale text such as "Slot locked" stayed after the unlock. The shell cannot know why every notice was raised. It therefore clears notices on the interaction events it does see, and the presenter, which knows the slot state, reconciles the one notice that depends on state.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Timeout only | A wrong "locked" notice could stay for six seconds after the unlock, and a quick next action would still see it. |
| Every notice owner clears its own notice | Spreads clearing logic over all panels T113–T118, and any one that forgets brings the F-1 bug back. |
| A notice queue with priorities | Too much machinery for one notice line, and the spec asks for one visible notice. |

**When you'd choose differently**: When several panels raise long-lived, state-bound notices, give each notice a predicate ("still true?") that the shell evaluates on every render instead of one special-case reconcile.

### 52. Compose into locals, publish only on success (T192)

**What we did**: `ComposeRunShell` builds the shell and slot grid into local variables and assigns the fields only after `Configure` succeeds. On failure it destroys the component and records `runShellError`. `RunShellDocument.Dispose` cancels a pending confirmation once through `OnCancel`.

**Why**: Before, a failed `Configure` left a half-ready shell in the field. A start-up retry then saw "shell exists" and skipped composing (F-7). Publishing only a complete object makes retry safe without extra state flags. Cancelling on dispose means a caller waiting for a confirmation always gets an answer.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| An `IsReady` flag checked by the retry | Every reader of the field must also check the flag, and one missed check uses a broken shell. |
| Reset the fields in a `catch` | Easy to miss a field, and the object is visible to others between assignment and reset. |

**When you'd choose differently**: If building is expensive and partly reusable (for example, an asset load), keep the finished parts in a cache and retry only the failed step.

### 53. Panels prove themselves through the registry, wiring comes later (T113–T118, T194)

**What we did**: The six panel units ran in parallel. Each built its surfaces as `IShellPanel` classes (or, for T118, as modal `StatusSurface` overlays), mounted them through `ShellPanelRegistry` only in their own tests, and reported a construction entry point. A separate task, T194, wires all of them into the bootstrap and scene once.

**Why**: Every panel needs a registration line in `CoreGameFoundationBootstrap` and asset fields in the scene. Three parallel workers editing those two shared files would have produced merge conflicts and serialized-scene diffs that are hard to review. Testing through the registry still proves each panel's behaviour against real run state.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Each panel worker registers its own panel | Six edits to one bootstrap method and one scene, in parallel worktrees, mean conflicts and scene churn. |
| Fold the wiring into T119 | T119 already retires the Feature 001 path. Mixing both makes one large, hard-to-review scene change. |
| A static self-registration list | Hides composition order and failure handling from the bootstrap, which owns start-up diagnostics. |

**When you'd choose differently**: With a single panel or a sequential lane, let the panel task wire itself; the extra task only pays off when several units would touch the same composition root.

### 54. One host per tab instead of one panel per surface (T114, T116)

**What we did**: The registry has one ID per tab. T114 stacks Inventory, Priorities and Expansion in a `MarketPanelHost` on the Market tab; T116 hosts the Industry section under the Research graph. Each section still mounts alone for tests.

**Why**: The contract lists surfaces, not tabs, and adding registry IDs would change the shell, which the panel units must not edit. A host keeps each surface independent and testable while the shell stays fixed.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| New registry IDs and tabs for Industry and Expansion | Changes the shell and the six-tab phone bar; needs a design decision and a shell task. |
| Merge the surfaces into one large panel class | Loses independent tests and makes 200 % layouts harder to reason about. |

**When you'd choose differently**: If players need direct navigation to Industry, add a shell ID in a shell task and move the section; the section classes already mount alone.

### 55. Compose panels behind a fail-soft gate (T194)

**What we did**: `ComposeRunPanels` builds each panel in `TryComposePanel` and registers it only when construction succeeded. A failure records `panel.<id>.failed` with the exception type and leaves the shell's "not available" placeholder for that ID; the other panels still mount. Registration handles are kept and disposed together on recompose and in `OnDestroy`.

**Why**: Start-up already fails hard for content, persistence and shell errors, because the run cannot work without them. A single broken panel only removes one surface, so failing the whole start would be worse for the player than a placeholder. Keeping the handles makes a shell rebuild idempotent.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Fail start-up on any panel error | One missing USS reference would block the whole game. |
| Let the exception propagate into `ComposeRunShell` | Would leave a half-composed shell, which T192 F-7 had just removed. |
| Lazy construction on first tab tap | Moves construction errors to play time and hides them from start-up diagnostics. |

**When you'd choose differently**: If a surface is required for the core loop (for example the build sheet in a build-only prototype), treat its failure as a start-up failure instead of a placeholder.

### 56. Recover over a torn active generation only under a strict gate (T195)

**What we did**: `RecoverPreviousCheckpoint` may now continue when the active read fails, but only for a cold-start block reached before any run was live, only when the active read is a plain `InvalidCheckpoint`, and only after the previous generation verifies. It reuses the store's unchanged `RecoverPrevious` protocol.

**Why**: The contract always required previous-generation recovery for this case, but the code read the active generation first and gave up. The four-part gate keeps the risky cases out: a save from another build reads as an unsupported version or unresolved content, a storage failure is transient, and a mid-session block holds an in-memory run that is newer than any file.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Allow discard when the active is torn | Would throw away a valid previous generation and break SEC-1. |
| A new store operation that preserves the torn bytes | With two data slots there is no third place to keep them; the old manifest already protects every crash point. |
| Leave the dead end and document it | The player would have no way out but deleting files by hand. |

**When you'd choose differently**: With more than two data slots, or a requirement to keep forensic copies of damaged saves, write the recovery generation to a fresh slot and keep the torn one.

---

### 57. Retire the old runtime behind an authority test, not just a deletion (T119)

**What we did**: T119 deleted the Feature 001 session, commands, placement, views, lifecycle adapter and HUD panels, kept the legacy types the V1 import still needs, and added `RunAuthorityTests`. It asserts one gameplay content input, no `JsonCheckpointStore` in the bootstrap, none of the 32 retired GUIDs under `Assets/`, and none of the 16 retired types in the assemblies.

**Why**: A deletion only proves the code is gone today. Unity assets reference scripts by GUID, so a scene or prefab can keep a dangling reference that compiles fine, and a later change could quietly compose the old store again. The test turns the decision into a check that fails on regression.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Delete only and rely on the compiler | Missing-script references in scenes and a re-added legacy store would compile. |
| Delete `JsonCheckpointStore` too | Retained V1 suites and the V1 import fixture still need a V1 writer. |
| Keep the old runtime behind a flag | Two runtimes in one bootstrap is the coupling R16 removes. |

**When you'd choose differently**: If no legacy format had to stay readable, delete the whole layer and drop the "kept" half of the test.

---

### 58. An icon is the label's background, set by one helper (T196, T243)

**What we did**: Lock and warning icons are drawn as the background image of the label that holds the text, inside a 30 pt left gutter, through `PanelIcons.Mark`. Each panel sets a rejection through one `SetReason` helper, so text and icon change together.

**Why**: The icon must never stand alone (FR-032: meaning never depends on colour or an icon alone), and a UI Toolkit label with children is no longer measured as text. A background image appears and disappears with the text, and a single setter means no code path can leave a stale icon.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| A separate `Image` element next to the label | Two elements can drift apart, and children break text measurement. |
| An icon glyph inside the string | Depends on the font and is read aloud by screen readers. |
| Set `.text` and the icon at each call site | Easy to forget the icon on one clear path. |

**When you'd choose differently**: For an icon that is interactive or animated on its own, a separate element is the right tool.

---

### 59. Re-check a consuming action against the current state (T196)

**What we did**: `ItemsPanel.OnTarget` reads the current target from the latest snapshot. If the shown or the current target is unavailable, it never calls `ActivateItem`: it re-renders and shows the reason. A target that became available is only re-rendered, so the player chooses it again through the confirmation.

**Why**: The button reflects an older snapshot. Activating an Item consumes it, so acting on stale availability could use up an Item without the confirmation the player expects.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Rely on the command to reject | The rejection is correct, but a target that became available would skip the confirmation. |
| Disable the button until the next refresh | Hides the reason and still races the snapshot. |

**When you'd choose differently**: For a free, reversible action, sending the command and showing its rejection is enough.

---

### 60. Confirm an absence through the authoritative store, not the cache (T244)

**What we did**: `RunAuthorityTests.NoAssetReferencesARetiredGuid` no longer asserts that `AssetDatabase.GUIDToAssetPath` is empty for each retired GUID. A returned path counts as an offender only when `AssetDatabase.AssetPathExists` or `File.Exists` finds the asset. The text-asset scan and the per-file `File.Exists` checks stay.

**Why**: The T120 full suite (#202) failed this case once in a worktree whose `Library/` was seeded from a pre-T119 checkout. Unity still resolved a deleted asset's path although the file and `.meta` were gone, so the guard reported a false positive that passed on the next session.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Drop the `GUIDToAssetPath` check | A retired GUID that a restored asset reuses would then be caught only by the file checks, losing the engine-side view. |
| Force a fresh `Library/` or reimport before the test | Slow, hides the cause and makes the test depend on the runner's setup. |

**When you'd choose differently**: If the engine database is the thing under test, assert on it directly. For a guard whose real question is "does the asset exist", ask the file system.

---

### 61. Ship test fixtures as `Resources` copies with a parity test (T121, T122)

**What we did**: The player journey, transition and W003 timing tests load their Save V1/V2 fixtures from `Resources` copies (`RunPlayerFixtures/`, `W003PlayerFixtures/`). `SaveV1PlayerFixtureParityTests` and `SaveV2PlayerFixtureParityTests` compare SHA-256 hashes against the originals, including a negative case that proves the check can fail.

**Why**: A scene-less player has no asset database, and a `Resources` folder is the only scene-free way to package a TextAsset into it. A copy can drift, so the hash test turns drift into a failure. The cost is accepted: the copies also ship in release builds (T121-F1), and excluding them is a follow-up.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Reference the originals through a scene or prefab | Needs a scene, which these player tests deliberately avoid. |
| Embed the bytes in C# | Hard to review and no longer the same artifact the Edit Mode tests read. |

**When you'd choose differently**: If the build pipeline can strip test-only `Resources` (or StreamingAssets is acceptable), use that and keep the parity test.

---

### 62. Prove the journey on a scaled scenario, and say what it does not prove (T121)

**What we did**: `RunJourneyPolicy` drives the real commands with a scaled prestige threshold ($100,000, IP divisor ÷ 100). Interruption is simulated at the store boundary with `FailAfterPromoteStore`.

**Why**: Content r1 does not reach $10M within 6 h (see decision on the balance trace), so an unscaled journey cannot finish. Run 2 is only 1.7 % faster (2340 vs 2380 s): deterministic, but brittle under balance changes (T121-F3). SC-001 on unscaled content stays with T125.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Retune content inside T121 | Balance is T125's scope and needs a human playtest. |
| Fake time or state to skip ahead | Would bypass the commands the journey exists to prove. |

**When you'd choose differently**: Once content is tuned, run the unscaled journey and drop the scaling.

---

### 63. Measure and record a miss instead of loosening the test (T122)

**What we did**: `W003PlayerTimingTests` records per-workload medians in `reviews/w003-measurements.md`. W003-B (about 950-1050 ms against 500 ms) is reported as a MISS and routed to T130, and SC-008 is not recorded as passing.

**Why**: The budget is a performance claim, not a regression guard. About 2.76 M completions at roughly 350 ns each explain the number, so the fix is a Performance question (fewer passes per timestamp), not a bigger threshold. Frame and allocation numbers are lower bounds: the host frame excludes UI Toolkit writes, and `GC.GetAllocatedBytesForCurrentThread` returns 0 in the player, so sample counts are used.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Raise the W003-B budget to fit | Hides the cost on slower devices. |
| Drop W003-B from the suite | Loses the worst-case workload. |

**When you'd choose differently**: If a budget was a guess without a user-facing consequence, revisit it with the owner, but record that as a decision.

---

### 64. Build device fixtures from the shipped catalog and load them through the player (T124, T245)

**What we did**: The first emulator run (T124) was blocked because the real player rejected the committed `w003-s` device set with `save.v2.invariant`. T245 added `device/w003-s-shipped` and `device/prestige-ready`, generated from the shipped catalog, and `DeviceSetShippedCatalogTests`, which loads them through the player cold-start composition. A negative test names the slot-count invariant.

**Why**: Every W003 fixture used the test-local `W003Workload` catalog: 12 slots at factory level 1, while the shipped content allows 4 (`SlotUnavailable`). Edit Mode and the Windows player timing tests passed because they used the same test catalog, so the mismatch stayed invisible until a real device run loaded the set through the real composition.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Relax the invariant for the emulator build | Hides the very check that protects real saves. |
| Hand-edit the committed set | Not reproducible; the set must be regenerated from the catalog. |

**When you'd choose differently**: For pure simulation timing, a test-local workload is fine, provided no claim about the shipped player rests on it.

---

### 65. An evidence task records a product FAIL instead of failing itself (T124, T246, T247)

**What we did**: T124 reported budgets PASS (8 h return median 746 ms against 2 s; producing FPS 61.1 against 60), but recorded the OS back edge swipe as FAIL: the Remove confirmation stays open, although `KEYCODE_BACK` cancels it. QA reproduced it. The finding became T247 (hypothesis: `androidPredictiveBackSupport: 1` routes the gesture through `OnBackInvokedCallback`, past the Escape/Cancel path). A second finding, no modal Welcome-back summary for returns under the 60 s threshold, became T246. Both are docs-only tasks.

**Why**: The evidence task's job is to measure, not to repair. Skipped steps and "same process, nothing spent" results were not accepted as PASS. The owner decided to keep system gestures enabled: the bottom home and recents gestures cannot be disabled, and back-gesture exclusion is capped at about 200 dp per edge, which would contradict the UX-2 requirement.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Fix the gesture inside T124 | Mixes evidence and product change; the evidence would no longer describe a clean commit. |
| Report the item as "not tested" | Hides a reproduced defect. |

**When you'd choose differently**: If a defect blocks the measurement itself, as the fixture rejection did, fix that in a separate task first (T245) and rerun.

---

### 66. Derive test expectations from the shipped catalog (T125)

**What we did**: Content revision 2 changed the pacing numbers (first Gear 12.0 min, first prestige 115.7 min, second run 77.3 min under reference policy r1). QA failed the first commit because 87 Play Mode panel tests hard-coded revision-1 values; the fix (390966d) reads expectations from the shipped catalog. The record states that r2 is tuned to policy r1 and its artifacts, that later-run targets are not met, and that scratch-harness sections cannot be reproduced from the commit.

**Why**: A tuning pass should change data, not tests. A test that copies a number from content duplicates the content and fails on every revision. The honesty notes keep the trace from being read as more than one scripted player.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Update the hard-coded numbers to r2 | Repeats the break at r3. |
| Pin tests to a frozen r1 catalog | Panels would no longer be tested against what ships. |

**When you'd choose differently**: Where a number is itself the contract (a save-format constant), hard-code it on purpose.

---

### 67. Editor-green is not player-green: prove test-support changes in the Windows player (T125)

**What we did**: The T125 fix commit 390966d added `RunShellFixtures.ShippedCatalog` to `RunShellTestSupport.cs`. It calls `RunSceneRun.IronBytes()`, which uses `AssetDatabase` (editor-only), and sat outside `#if UNITY_EDITOR`. The batch-end `scripts/test-unity.ps1 -StandaloneWindowsPlayer` run failed to compile the player build (exit code 3, no result XML), although Edit Mode (1516/1516), editor Play Mode (1492/1492) and QA were green. Commit ac4d719 moved the guard (one line); PlayerPlayMode then passed 23/23 and an independent review passed.

**Why**: The Play Mode test assembly has no platform restriction, so every editor API it touches must sit inside the editor guard. The editor never compiles for a player, so only a player build shows the break.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Rely on the batch-end player run alone | It found the defect, but one batch late; a test-support change should be proven when it is made. |
| Restrict the assembly to the Editor | Would remove the StandalonePlayer-tagged tests. |

**When you'd choose differently**: Never skip the player run for test-support changes; for plain test bodies the batch-end run is enough.

---

### 68. Playtest findings become small fixes: scroll, stable layout, desktop focus (T126, T127, T248, T249, T250)

**What we did**: The owner's playtest (T126, `reviews/playtest-flo-20261009.md`) found no bugs but three usability faults, fixed as three Issues. T248 (#336, `5bfa6b1`): the Research graph hides its scrollers, which also turns off a `ScrollView`'s own wheel handling in UI Toolkit, so the panel takes `WheelEvent` itself (wheel scrolls the sheet, Shift+wheel the graph) and adds drag-to-pan for pointer devices. T249 (#337, `fb1a29f`): the Machine panel jumped at a full store because the "Storage full" blocker (151 pt, up to 360 pt at 200 %) appeared above the action buttons. The owner chose no reserved blank space: blockers move below Upgrade, Switch off and Remove, small text reserves keep the status lines steady, and the sheet scrollbar stays visible while the panel is open so it cannot narrow and rewrap the content. T250 (#338, `ed49ca9`): a new `LifecyclePlatform` seam makes desktop focus loss a non-departure (one plain safety checkpoint, because the game has no periodic autosave; `runInBackground: 1`), while mobile keeps FR-037. T127 approved content revision 2 and the owner ruled that the SC-003 and SC-004 minute bands are orientation only.

**Why**: Moving a control under the player's pointer is a bug even when every rule is right, so transient content goes after the controls. Lifecycle behaviour depends on the runtime platform (`Application.isMobilePlatform`), not the input profile: a touch laptop is still a desktop. An editor-only `[SetUpFixture]` keeps the older Play Mode tests on the mobile class, so only the new tests exercise the desktop class.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Reserve blank space for the blocker | The owner rejected empty space in the panel. |
| Choose the lifecycle class from the input profile | Wrong for touch laptops and keyboard tablets. |
| Periodic autosave instead of a focus-loss checkpoint | A larger persistence change than the finding needed. |

**When you'd choose differently**: A real wheel over the graph, a clamp for the large live frame after OS sleep while unfocused (outside offline efficiency and the 8 h cap), and checkpoint throttling per Alt-Tab are open follow-ups for T128 or a Game Systems decision.

---

---

### 69. Fix a regression before acceptance, and bisect it with one setting (T251, T133)

**What we did**: The first T133 emulator run on `8a8e518` showed Android Back on an open confirmation closing the app, which T250's `runInBackground` Player setting had caused. The owner chose to fix before accepting (D8), so T251 (#350, PR #351) turned the setting off for every platform and made `LifecyclePlatform` apply `Application.runInBackground = true` at runtime on desktop players only (editor skipped, because there it writes the Player setting). T133 was then rerun in full on `ac4256c`; the first run stays in `final-acceptance.md` as history. The follow-ups are #353 to #358.

**Why**: Accepting with a known regression would have shipped a false claim ("mobile behaviour is unchanged"). The cause was found by building the base with only `runInBackground: 0` (variant A) and comparing it with the base itself, its inverse: one setting differs, so the fault is that setting and the T248 and T249 changes need no builds of their own. The cause mechanism inside GameActivity was not investigated; isolation was enough to choose the smallest fix.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Accept with a recorded deviation | A Back key that closes the app on a confirmation breaks FR-033; the owner preferred a small fix. |
| Handle Back in the activity | T247 already disproved activity-side handling, and R19 keeps the activity pure glue. |
| Keep the setting and special-case Android | `PlayerSettings.runInBackground` has no per-platform value. |

**When you'd choose differently**: If the fix needed an engine-level investigation (why the setting changes the GameActivity input path), accepting with a tracked risk could beat a long unexplained detour. Here the bisection made the fix cheap.

---

### 70. A player test must observe what the production code sets (T251, T133)

**What we did**: `RunInBackgroundPlayerTests` (StandalonePlayer) asserts `LifecyclePlatform.BackgroundExecutionApplied` and `Application.runInBackground == !Application.isMobilePlatform`. The second check alone proves nothing: the test-player harness already sets `runInBackground` true, so it read true even with the production call disabled. Only the flag set by the initializer fails when the call is removed, which was shown fail-first.

**Why**: A harness can satisfy the property you assert. Before trusting a green test, disable the production line and confirm the test goes red.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Assert the property only | Passed with the call disabled (independent QA warning W1). |
| Guard by a source pin in Edit Mode only | Proves the text exists, not that the initializer ran in a built player. It stays as an additional guard. |

**When you'd choose differently**: When the property is not preset by the harness, assert it directly.

---

### 71. Control the measurement host before you call a number a regression (T133)

**What we did**: The first run measured the 8 h reconcile 13 to 18 percent slower (W003-B 1,154.8 ms) with the Android emulator running on the same host than the rerun with it shut down (965.7 ms), although `Assets/IdleFactory/Core` was identical. The accepted figures use the quiet host. The same lesson held for the journey script: `powershell -File` turns a tap array into one string, so the run of record uses `-Command "& ./scripts/android-emulator-journey.ps1 ... @('x,y',...)"`. SC-008 is reported as met for the shipped workloads, with the W003-B miss accepted (D1, follow-up #353), and emulator figures (8 h return 735 ms, 61.4 fps) are labelled emulator-only.

**Why**: Noise of this size is as large as many optimisations; without the controlled rerun the team could chase a regression that was the emulator or accept a number that was inflated.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use the first run's figures | They were taken with the emulator running and mislead the budget comparison. |
| Optimise W003-B now | The shipped workloads pass, and the owner chose to measure first (#353). |

**When you'd choose differently**: If the measurement must reflect the contended conditions players have, measure there on purpose and say so.

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

### Pure builder, impure coordinator (T106)

**What it is**: The import mapping is a pure function (`V1Import.Build`) that returns a settled candidate or a static code. All I/O, lifecycle and retry logic lives in one coordinator (`RunPersistence.ColdStart`).

**Where we used it**: `V1Import.cs` and the cold-start section of `RunPersistence.cs`.

**Why it matters**: The mapping can be tested with plain fixtures, and exactly-once only has to be proven in one place. It reuses the promotion path, so there is no second protocol to get wrong.

---

### Set equality versus order equality (T136)

**What it is**: When you claim two structures are "identical", decide per list whether its order carries meaning. Level tables and reward lists are compared in order; lists like recipe inputs or prerequisites are compared as sets.

**Where we used it**: `RunContentOrderTests` and the `ResearchInContentOrder` exception (OD-8).

**Why it matters**: A test that sorts every list before comparing can hide a real order difference, and a test that compares everything in order makes harmless author reordering a failure. The review caught that the test name promised more than it checked.

---

### Golden output plus honest bands (T107)

**What it is**: A deterministic run is compared byte for byte with a committed output, while judgement metrics are reported, not asserted.

**Where we used it**: `Tests/EditMode/Balance/BalanceTraceTests.cs`, `Fixtures/Balance/trace-r1.csv`, `reviews/balance-trace-r1.md`.

**Why it matters**: Determinism is binary and must stay green; balance is a judgement that changes with every revision.

---

### Time-of-check/time-of-use (T141)

**What it is**: A check that is separate from the action it guards can be outdated when the action runs.

**Where we used it**: `CheckpointGenerationFiles.SupersedeDamaged` repeats every precondition inside the store lock.

**Why it matters**: For irreversible operations such as overwriting a save, the guard must sit where the write happens.

---

### Independent measurement in reviews (T109, T143)

**What it is**: A reviewer reproduces numbers with their own method instead of repeating the author's.

**Where we used it**: The Art Director reviews measured gaps on rendered masks; the delta review withdrew a T109 figure (0.80 px) that no method reproduced.

**Why it matters**: A QA pass that copies the author's numbers adds no evidence.

---

### Extension points with disposable registration (T112)

**What it is**: A host exposes `Register(x)` returning an `IDisposable`; disposing undoes the registration.

**Where we used it**: `Unity/UI/ShellPanelRegistry.cs` for the panels T113–T118.

**Why it matters**: Features can be added and removed without editing the host, and tests can mount throwaway panels.

---

### Measure the engine instead of trusting the formula (T191)

**What it is**: Before a rule depends on an engine calculation, measure what the engine actually produces at the target size.

**Where we used it**: `RunDeviceProfileTests` builds a real panel from the authored settings in a 1080×2400 render texture. It showed that `ScaleWithScreenSize` blends linearly: 2.09 px/pt in portrait, 1.69 px/pt in landscape, not the 1.68 px/pt recorded earlier for both orientations.

**Why it matters**: A size rule built on the wrong number silently misses its target on half the orientations. A test that pins the measured baseline turns an engine change into a failing test.

---

### Physical size from DPI, with an honest fallback (T191)

**What it is**: Points become millimetres only through the DPI: `px/pt = dpi × 7 / 25.4 / 44` makes 44 pt equal 7 mm. Where DPI is unknown, keep the old behaviour and record why.

**Where we used it**: `Unity/Presentation/ShellPanelScale.cs` (`ScaleDecision.Reason`).

**Why it matters**: `Screen.dpi` can be 0. A guess would look like a guarantee; a recorded fallback stays visible in tests and logs.

---

### Editor-only test helpers break the player build (batch end, T191)

**What it is**: A tagged player suite filters tests at runtime, but the player still compiles the whole PlayMode test assembly. Helpers inside `#if UNITY_EDITOR`, called from unguarded tests, then fail compilation in the player.

**Where we used it**: The batch-end run `scripts/test-unity.ps1 -StandaloneWindowsPlayer` on 841b1f1 stopped before any test ran. The errors were CS0117/CS1061 for `RunSceneRun.Stop`, `RunShellFixtures.NewSession`/`OverdriveStatusIn`, `DevelopmentCatalogBytes` (T110/T112) and `ShellRig.CreateAuthored` (T191).

**Why it matters**: Editor test runs stay green while the player build is broken, so this only shows at the batch-end gate. T193 resolved it by guarding the callers (decision 50). A new Play Mode test that calls an Editor-only helper therefore needs the same guard. Run `-StandaloneWindowsPlayer` before you rely on the player suite.

---

### Overlays that do not steal the world (T192)

**What it is**: A UI overlay that lies over the game world has two jobs. It must not catch pointer presses meant for the world, and the camera must treat it as covered space. In UI Toolkit, `picking-mode="Ignore"` handles the first, and shrinking the unobstructed rectangle handles the second.

**Where we used it**: The detail, notice and "Build a Miner" hint bars in `FactoryRun.uxml` are `picking-mode="Ignore"`. `RunShellDocument.TryGetUnobstructedScreenRect` starts below the lowest bar shown (F-8). World presses never reach UI Toolkit, so the bootstrap forwards them to the shell with `NotifyWorldPress()` to unpin values and clear notices.

**Why it matters**: Without both, a slot hides under a bar, or a tap on the world just above it does nothing, and nothing in the logs shows why. The F-8 test asserts both: `panel.Pick` never returns a bar, and no slot reaches under a bar.

---

### Soft-disabled controls with command re-validation (T113–T118)

**What it is**: A control that cannot act now stays focusable and shows why ("Need 40 more Cash"). A press still sends the command, which re-validates and returns a rejection that appears next to the control.

**Where we used it**: Build cards, upgrades, Accept and Deliver, Research nodes, Industry upgrades and the Prestige Confirm.

**Why it matters**: Keyboard and screen-reader users can reach the control and hear its reason, and a stale view can never change state, because the command is the only authority. The cost is that the view must show rejections well, not hide them.

---

### Architecture tests (T119)

**What it is**: A test that checks structure rather than behaviour: which types exist, what a class may reference, which asset GUIDs may appear.

**Where we used it**: `Assets/IdleFactory/Tests/EditMode/RunAuthorityTests.cs`, with reflection over the assemblies and a text scan of assets for retired GUIDs.

**Why it matters**: Architectural decisions decay silently; a structural test makes a reintroduced dependency fail CI instead of a code review.

### Same-session control (T123)

**What it is**: A measurement that runs its baseline and its subject in one device session, so host load, thermal state and cache warmth cancel.

**Where we used it**: `scripts/android-emulator-journey.ps1 -Scenario F003`, step ProducingFps, with OfflineReturnTiming taking the median of three cold starts.

**Why it matters**: Emulator numbers vary between sessions; only a within-session comparison is interpretable. Steps that wipe state (`Checkpoints/v1`, `v2`, `pm clear`) must be ordered deliberately.

---

### Stale-cache false positives (T244)

**What it is**: An index or cache can keep an answer after the thing it describes is gone. An absence check built on that answer fails although nothing is wrong.

**Where we used it**: `AssetDatabase.GUIDToAssetPath` in `RunAuthorityTests`; the fix asks `File.Exists` and `AssetDatabase.AssetPathExists` as well.

**Why it matters**: A test that fails once per fresh environment trains people to rerun it. Check existence against the source of truth, and keep the assertion strict enough that a restored asset still fails.

---

### Fixtures versus the shipped catalog (T245)

**What it is**: Test data built from a local stand-in definition instead of the content that ships. It passes everywhere the stand-in is used and fails only where the real definition is enforced.

**Where we used it**: `W003Workload` versus the shipped Iron content; `DeviceSetShippedCatalogTests` closes the gap by loading through the player composition.

**Why it matters**: A fixture proves only what its source proves. Load device data through the real composition once, with a negative case that names the violated invariant.

---

### Suppress by what is reported, not by time alone (T246)

**What it is**: A modal that interrupts the player should open only when it has something to say. A short absence under 60 s with a clean, applied return has nothing to report, but a time threshold alone would also hide a fallback, a backward clock or an expired contract.

**Where we used it**: `OfflineSummaryPanel.ShouldOpenSummary` and `ShortAbsenceThresholdMs`. The reconcile still runs exactly once and the result stays available as `LastGenuineSummary`, so only the presentation changes, never the accounting. QA noted the expired-effect and fallback branches are proven at `ShouldOpenSummary` level only.

**Why it matters**: Putting the rule in one pure static predicate keeps the bad news visible and makes every branch testable without a scene. Suppress the interruption, not the information.

---

### Control the measurement before you call it a defect (T247)

**What it is**: "The back gesture does nothing" was reported as a product bug. Controls showed otherwise: `KEYCODE_BACK` cancels, a second swipe cancels, a swipe from `x=0` is ignored by the system, and SystemUI reports `backGestureDisabled=true` while the immersive player is on top. The first swipe only reveals the system bars.

**Where we used it**: `scripts/android-emulator-journey.ps1` step `BackGesture` (`-BackInput`, `-BackSwipes`, a pixel-count check of the dialog's danger button instead of "same process, nothing spent", which also holds when the input did nothing); `AndroidBackGestureTests` pins the decision that the activity adds no back handling.

**Why it matters**: Both obvious fixes (predictive back off, a back callback in the activity) were measured and changed nothing, because SystemUI never delivers the swallowed swipe. Measuring first saved a code change that could not work. The remaining choice, an immersive-mode change versus accepting standard Android behaviour, is a product trade-off, so the owner decided it (2026-10-09, option a), with physical-device confirmation still open. Know when a finding is a decision and not a bug.

---

### What independent reviews find that green tests miss (T128 to T132)

**What it is**: Five read-only review records (UI/UX, art integration, performance, security/DevOps, feature) ran against a suite that was green. They still found: the W003-B 8 h reconcile at 0.95 to 1.02 s against 500 ms (SC-008 has no asserting test, the W003 player tests only record), a once-per-second allocation on the HUD path, a Research stylesheet rule USS silently ignores (`:last-child`), a texture memory figure 17 % above the recorded one (GPU block padding), a world background that deviates from the Art Bible, and an unguarded host flush inside a Unity focus callback.

**Where we used it**: `reviews/performance-review.md`, `art-direction-review.md`, `ui-ux-review.md` (Final review), `security-devops-review.md`, `feature-review.md`. Four verdicts (Product, Architect, Game Systems, Test/QA) came from separate sessions that did not read each other, and all four landed on accept with conditions.

**Why it matters**: A test proves the property someone thought to assert. Reviews look for properties nobody asserted: budgets that are only recorded, rules nobody renders, callbacks nobody fails. The cheap fix is to turn each finding into either an asserting test or a named owner decision. Keep the decision open in the record rather than guessing it: SC-008 (options A, B, C), the 44 pt exemption (F-9), and restarts showing the summary (N-5) stay unresolved until the owner answers.

---

### Evidence currency (T132)

**What it is**: Evidence is only as good as the commit it ran on. The last complete Edit, Play and Player run (T122) predates eight merges that touch `Assets/` (content revision 2, a player compile fix, T246 to T250). The T124 emulator APK predates content revision 2. Targeted reruns by the UI/UX reviewer are current only for those files.

**Where we used it**: the currency table and rerun list in `feature-review.md` (Test/QA verdict); T133 reruns everything on the accepted commit or labels figures carried forward. The player compile break after T125 is the standing proof that an editor pass does not imply a player pass.

**Why it matters**: A reviewer who quotes an old green run as final evidence reports a risk as a fact. Compare the newest complete run with every later merge, call older runs supporting, and name the rerun list. Honest labels (emulator-only, owner judgment for SC-003 and SC-004, scripted prestige with a scaled threshold) cost nothing and prevent claims nobody measured.

---

### Read-only reviewers still need a way to run (T129)

**What it is**: The art reviewer needed real captures but may not change the repository. The harness was a temporary, uncommitted Play Mode test that set `PlayerSettings.productName` to a throwaway name, so every run wrote under a different `persistentDataPath`. It refused to start if the path contained `idle-factory-game`, reverted `ProjectSettings` afterwards, and listed every grant it made beyond public commands (two Items added, a machine toggled, auto-sell off).

**Where we used it**: `art-direction-review.md`, "Method, build and evidence limits".

**Why it matters**: Isolation by name plus a refusing guard protects the owner's real save from an experiment, and disclosing the harness grants tells readers which states were produced by play and which were staged.

---

### Bisection by a single-variable variant and its inverse (T251)

**What it is**: Change exactly one thing from a failing build and rebuild; if it passes, the failing build is its inverse, and the one change is the cause.

**Where we used it**: `reviews/android-emulator-evidence.md` T251 table (variant A against the base).

**Why it matters**: It identifies a cause without understanding the mechanism, and it keeps the fix to that one change.

---

### The harness masks the property (T251)

**What it is**: A test environment that already sets the value under test makes an assertion on that value pass vacuously.

**Where we used it**: `RunInBackgroundPlayerTests` observes the initializer's own flag instead of only `Application.runInBackground`.

**Why it matters**: Show a test failing with the production line disabled, or it may guard nothing.

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

The run shell's device profile and text scale (T191) sit in `Unity/Presentation/` as plain values (`ShellDeviceProfile`, `ShellTextScale`) plus one boundary helper that touches `PanelSettings` (`ShellPanelScale`). `RunShellDocument` probes Unity once at build and then periodically. It applies the scale and the text-scale class, and panels only consume the USS variables, so no panel reads `Screen` or the device itself.
