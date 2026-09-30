# Research: First Iron Factory and Prestige Loop

**Date**: 2026-09-29 · **Spec**: [spec.md](spec.md)

The Software Architect verified every decision below against the repository at `369b0c6` (Features 001 and 002 complete). The spec's Clarifications are authoritative and are not reopened here. Decisions that go beyond them are marked **(interpretation)** when they follow directly from an approved rule, or refer to an entry in `plan.md` › Open owner decisions when they need the owner.

## Repository facts that constrain the design

- `IdleFactory.Core` (`noEngineReferences: true`) holds namespaces `IdleFactory.Domain`, `IdleFactory.Simulation` and `IdleFactory.Core.Application`. `IdleFactory.Unity` owns content reading, Save V1, bootstrap, input, presentation and UI Toolkit.
- `ProductionSimulationSession` (`Core/Simulation/ProductionSimulator.cs`) advances each building **independently** in closed form (time-, input- and output-permitted cycles per building). Buildings never interact, so the closed form is exact. It cannot be reused for a shared inventory.
- `RecipeDefinition` requires at least one input (`DefinitionGuard.CopyRequiredList`), and the Feature 002 validator mirrors that throw as `recipe.inputs.empty`.
- `BuildingInstanceState` owns input/output buffers, a footprint anchor and an orientation. `FactoryState` validates occupancy on a cell grid.
- Save V1 lives in `<persistentDataPath>/IdleFactory/Checkpoints/v1/` with A/B manifests, data files and a pending file (`JsonCheckpointStore`). The generation protocol (write pending → read back → retain previous → promote → verify) is coupled to `SaveV1Mapper` and to `Checkpoint(FactoryState)`.
- `FactoryRuntimeHost` advances through one cumulative **active-time** source. It stops while the app is suspended and is gated by `AnyMachineCanProduce`.
- The Feature 002 content document (`contentSchemaVersion: 1`) contains only the `dev.*` IDs that Save V1 references.

## R01 — Global event simulation (FR-003–FR-005, SC-002)

- **Decision**: Replace per-building closed-form advancement for the run with a discrete-event simulator, `RunSimulator`, over one global inventory. Inside an advance to `T`, time moves from event timestamp to event timestamp. An event is a machine completion, a real-time boundary (effect expiry, Overdrive end, cooldown end, contract expiry) or the end of the interval. Every event with a timestamp `≤ T` is processed. At each timestamp `t`:
  1. **Expire** every effect, Overdrive state and contract deadline whose boundary is `≤ t` (see R05 for the inclusive deadline). The effective cycle durations of affected machines change here, and progress is rescaled (R03).
  2. **Complete** every due machine (`progress == cycleDuration`) in the player's priority order. Each completion is an atomic exchange that checks the current stock for inputs and the post-exchange total against capacity. A completion that does not fit leaves the machine **waiting**: it keeps full progress and neither consumes nor produces.
  3. **Auto-sell** the whole current stock of each resource whose auto-sell is enabled, at the effective sale price.
  4. **Retry completions (approved OD-2)**: repeat step 2 for waiting machines and then step 3, until a pass completes nothing. Each machine completes at most once per timestamp, so there are at most *N*+1 passes.
  5. **Run predicate**: each machine with `progress < cycleDuration` runs until the next event exactly when it is active, its complete inputs are present in stock (nothing is reserved) and its net exchange fits the capacity. Otherwise it pauses and keeps its progress. There is no backlog.

  Player commands apply at their timestamp after the preceding interval has been processed. The session then *settles*: it runs steps 3–5 at that timestamp, so enabling auto-sell or building a machine takes effect immediately and deterministically.
- **Rationale**: Stock changes only at event timestamps, and the run predicate is re-evaluated at every one of them. A partition boundary that falls between events therefore re-derives the same predicate and yields the same state, which gives SC-002 exactly. The order implements the clarified sequence of expiry, then completions in priority order, then sales, then subsequent cycles. It also carries forward Feature 001's rules that "inputs remain present during partial progress" and that blocking preserves progress. Frame rate never matters, because the host feeds cumulative ticks.
- **Why step 4**: without it, a factory whose running machines are all waiting on capacity that a sale has just freed would have no future event, and it would stall forever. Step 4 changes the literal clarified order, so it is recorded as **OD-2** and implemented behind a single ordered procedure that tests pin.
- **Cost bound**: machines are limited to the slot count (content maximum 12; parser guard 64). The next event is found by a linear scan of at most 12 machines plus a handful of timers, with no heap and no allocation. The number of completions is bounded by R04.
- **Alternatives rejected**: (a) Reusing the closed-form per-building advance with the buffers redirected to a shared store is incorrect, because consumption order changes availability (discovery review). (b) A fixed simulation quantum, for example 100 ms, changes results with the partition and the frame rate. (c) Reserving inputs at cycle start contradicts "no input or output moves before atomic completion".

## R02 — Input-free Miner and a separate run content model (FR-001, FR-002, FR-029)

- **Decision**: Introduce new immutable definition types for the run (`MachineRecipe` and others; see data-model) in a new `IdleFactory.Run.Content` namespace. `MachineRecipe` allows **zero inputs** and requires at least one output. The Feature 001 `RecipeDefinition`, `BuildingDefinition` and `DefinitionCatalog` stay **unchanged** as the *legacy development catalog*, which only the V1 importer (R10) uses.
- **Rationale**: Relaxing `RecipeDefinition` would break the Feature 002 rule `recipe.inputs.empty` and its domain-parity test. It would also change the meaning of content version 1. Separate types keep content version 1 frozen, as the Feature 002 contract's Evolution section requires.
- **Alternatives rejected**: Adding a nullable input list to the existing type couples the legacy buffer model to the new global model.

## R03 — Cycle time, speed composition and mid-cycle changes (FR-009, FR-010, FR-018, FR-019)

- **Decision**: All multipliers are integer **parts per million (ppm)**, where 1,000,000 means ×1.
  - The effective speed of a machine is `speedPpm = level × milestones × research × industry × active items × Overdrive`. It is folded left to right with `MulDivFloor(acc, factor, 1_000_000)`, in the fixed order listed in the contract. `maxCombinedSpeedPpm` is only an overflow and sanity bound: valid content never reaches it (`speed.level.dead`, R04), and the runtime clamp to it is defensive.
  - **Batching beyond the event bound (approved OD-6)**: with `D₀ = recipe.durationTicks` and `Tmin = minCycleTicks` (content, at least the engine floor `MinEffectiveCycleTicks` = 1,000,000 = 0.1 s), the batch factor is `k = MulDivCeil(speedPpm, Tmin, D₀ × 10^6)`, at least 1, and `cycleDuration = MulDivCeil(k × D₀, 1_000_000, speedPpm)`. `k` is 1 exactly when the unbatched cycle is already at least `Tmin`, so the formula is also the ordinary case. Otherwise `Tmin ≤ cycleDuration ≤ 2 × Tmin`. The exact quotient stays below `2 × Tmin`, but the ceiling can reach it. For example, `D₀ = 20,000,000`, `Tmin = 1,000,000` and a speed of ×20 plus 1 ppm give `k = 2` and `cycleDuration = 2,000,000` (owner decision 2026-09-30, found in T095).
  - A completion consumes `k × inputs` and produces `k × outputs × outputFactor`. The effective rate `k / cycleDuration` equals `speedPpm / (D₀ × 10^6)` except for the ceiling of one tick per cycle (under 1 ppm, never overstated), the same rounding as an unbatched cycle. Nothing carries over between cycles: `k` and `cycleDuration` are pure functions of `(D₀, speedPpm, Tmin)`. Every product is checked or 128-bit; `durationTicks ≤ 10^12` keeps `D₀ × 10^6` inside `Int64`.
  - **Partial batches (approved OD-9)**: a due cycle completes `k′ = min(k, k_in, k_cap)` recipe units in one atomic exchange, where `k_in = min over inputs r of floor(stock[r] / input[r])` (unbounded for an input-free recipe) and `k_cap = floor((capacity − Σ stock) / net)` for a positive net unit change `net = Σ outputs × outputFactor − Σ inputs` (unbounded when `net ≤ 0`). It consumes `k′ × inputs` and produces `k′ × outputs × outputFactor`. If `k′ = 0` the machine waits with full progress, exactly as before. The unprocessed remainder `k − k′` is dropped: nothing carries over, and the next cycle starts fresh with the full `k`. A waiting machine retried in the OD-2 pass after sales uses the same rule. The run predicate lets a machine run when at least one recipe unit could complete (`k′ ≥ 1` at the evaluation instant). Everything is integer, checked and deterministic.
  - Rounding the duration up means the displayed speed is never overstated. `cycleDuration` is never less than 1.
  - An output effect (`machineOutput`) is an **integer factor** that multiplies output amounts only. Inputs are unchanged. Combined factors are clamped to `maxCombinedOutputFactor`.
  - Level growth (`1.08^(L−1)`) and milestones (×2/×2/×3/×5 at levels 10/25/50/100) are **speed** effects. Throughput is the same as for an output effect, and the atomic exchange stays at recipe granularity **(interpretation of "rate")**.
  - Whenever a boundary changes `cycleDuration` from `D` to `D′`, progress becomes `p′ = MulDivFloor(p, D′, D)`. A waiting machine (`p == D`) stays waiting (`p′ = D′`). This preserves the completed fraction exactly as clarified. Only the boundary timestamp is involved, so all partitions agree. The batch factor is re-derived at the same boundary; a completion always exchanges the batch that is current at its timestamp.
- **Rationale**: Integer ppm arithmetic is identical on Mono and IL2CPP. The same rescale formula serves upgrades, effect start and expiry, Research purchases, loading a checkpoint whose content-tuned duration changed, and the V1 import (`floor(old × 2/5)` is `MulDivFloor(old, 2 s, 5 s)`).
- **Alternatives rejected**: `double` math, which risks platform and compiler differences. Rational progress fractions, which need unbounded denominators.

## R04 — Event-count bound and offline cost (FR-025, SC-008)

- **Decision** (revised by approved OD-6): no cycle is shorter than the content's `minCycleTicks`, which may not be below the engine floor **`MinEffectiveCycleTicks` = 1,000,000 (0.1 s)** (`runstart.min-cycle.invalid`). Speed beyond that bound becomes the batch factor of R03, so the event count is bounded by the minimum cycle whatever the speed, and batch sizes grow instead. The worst case is 12 machines × 23,040 effective seconds / 0.1 s ≈ 2.76 M completions for an eight-hour absence. The validator adds:
  - `speed.level.dead`: every machine's `SpeedTable` strictly increases up to `maxLevel`, and its worst-case composed speed (maximum level with every Research, Industry, Item and Overdrive speed source at its maximum, stacked as far as conflict keys allow) is at most `maxCombinedSpeedPpm`. No level, milestone or effect is ever clamped away (ECO-1, GD-5).
  - `batch.overflow`: the worst-case batch at that worst-case composed speed, `k × amount × maxCombinedOutputFactor` for any recipe amount, exceeds `ExactBound`. This is the only batch-size rule: a batch larger than the store is valid content, because it completes partially (**approved OD-9**, which replaced the earlier `batch.capacity.exceeded` rule).
  - A batch that the current store or stock limits completes `k′ < k` units and drops the rest; the machine state reports the last `k′` and its limiting factor, and the balance trace (T107) records how often the reference policy completes partial batches.
- **Rationale**: The bound caps the event loop without dead upgrades. Measured budgets are in plan.md › Performance (W003); the event-bound record uses the W003-B variant, because a workload with auto-sell on every resource starves every machine except the Miners and never reaches the bound (owner decision 2026-09-30, found in T098). If a budget fails, the Performance Engineer must review exact accelerations before any semantic change. The candidates are (a) batching uncontended machines, whose inputs no other machine consumes, in closed form, and (b) detecting steady-state periods.
- **Alternatives rejected**: Clamping speed at the bound, which made every level above about 22 and every later milestone and effect dead (ECO-1). Coarser simulation steps (see R01). Shorter cycles, which make the event count unbounded.

## R05 — Real-time timeline, deadlines and offline segmentation (FR-014, FR-015, FR-025, FR-026)

- **Decision**:
  - **Online**: the session keeps a *run timeline* in UTC ticks: `timelineUtc = anchorUtc + activeTicksSinceAnchor`. The anchor is re-bound at every return and at every load. Effects, Overdrive and contracts store absolute UTC end instants in Unix milliseconds. They expire when the timeline reaches their boundary. A contract's deadline is inclusive, so its expiry boundary is `deadlineUtc + 1 ms`, and a delivery command at `timelineUtc == deadlineUtc` succeeds.
  - **Offline**: for departure `d`, return `r` and policy revision 1 (4/5, 8 h cap), define `E(x) = floor(min(max(x − d, 0), cap) × 4 / 5)`. `E` maps real instants to the effective simulation timeline. Every real boundary `b` in `(d, r]` becomes a simulation event at `E(b)`, with the expiry kinds ordered by `(kind, id)` for determinism. Production advances by `E(r)` in total. A boundary beyond `d + cap` is processed at the end of production, because it has no production effect. The summary reports measured, credited and effective durations.
  - **One entry point** (GD-3): `RunSimulator.Advance` accepts caller-supplied boundary events in simulation time, ordered by `(kind, id)`. Online, the session derives them from the UTC instants on the timeline; offline, `OfflineSegmenter` maps them through `E`. Both call the same method, so segmented and online advances share one code path.
  - A backward clock or a missing clock keeps Feature 001 behaviour: zero credit, a nondecreasing departure, and a blocked return with retry. Deadlines and effects are never un-expired, because the timeline never moves backwards.
- **Rationale**: `E` telescopes, `Σ(E(bᵢ₊₁) − E(bᵢ)) = E(r)`. Segmenting at boundaries therefore credits exactly what the unsegmented Feature 001 formula credits, and an effect covers precisely its active portion of reduced production. That portion is 4/5 of its real duration, the same compression as all offline production.
- **Alternatives rejected**: Flooring each segment separately, which loses ticks and depends on the partition. Remaining-duration counters driven by active time, which would freeze real-time effects while the app is suspended and contradict the clarification.

## R06 — Exact numbers (FR-035, clarification "Numeric scope")

- **Decision**:
  - Resource units, Cash, RP, IP and Item counts are `long` values in the closed range `[0, ExactBound]` with `ExactBound = 10^18`. Every add, multiply and sum goes through `CheckedMath`, whose operations return `bool` without allocating.
  - Exceeding the bound or `Int64` rejects the whole command or advance atomically with `ArithmeticOverflow`, and the player sees "value limit reached".
  - `MulDivFloor` and `MulDivCeil` use an allocation-free 128-bit intermediate (two `ulong` halves). They serve ppm composition, progress rescale, prices and the IP curve. `ISqrt` uses Newton's method on `ulong`.
  - Cash is whole dollars. Sale income is `MulDivFloor(units × basePrice, salePricePpm, 10^6)` per sale, and Factory Value values each resource's stock with the same formula (R11). Because each sale floors separately, many small sales can yield slightly less than one sale of the same units; the difference is below $1 per sale and is tested (ECO-8).
  - A curve with `growthPpm > 10^6` must strictly increase at every step (`curve.stalled`); a flat curve is declared with `growthPpm = 10^6` (ECO-8).
- **Rationale**: The 10^18 bound leaves 9× headroom inside `Int64`, so a sum of two in-range values never overflows before the bound check. The $10M first prestige sits 11 orders of magnitude below the bound. Values up to 1e150 remain an explicit future numeric, save and content migration.
- **Display**: `QuantityFormatter` shows exact values below 10,000 with grouping. Larger values use three significant digits, **floored** so that a displayed value never implies affordability that does not exist, with the suffixes K, M, B, T, Qa, Qi, and scientific notation above that. Inspection (focus, long-press or hover) shows the exact value.

## R07 — Content schema version 2 (FR-029, FR-030)

- **Decision**: A new packaged document `Assets/IdleFactory/Content/Iron/iron-factory-content.json` with `contentSchemaVersion: 2` and an integer `contentRevision` for balance versioning (see [contracts/gameplay-content-v2.md](contracts/gameplay-content-v2.md)). The reader peeks the version and dispatches: version 2 goes to a new `RunContentReader` → `RunContentValidator` → `RunCatalogBuilder`, and version 1 to the unchanged Feature 002 loader.
  - The existing `development-catalog.json` (version 1) stays packaged, renamed in role to the **legacy import catalog**. The bootstrap loads it only when a V1 checkpoint must be imported.
  - The strict-parser rules, limits, diagnostics format and canonical-form review gate carry over unchanged. Only the families and rule codes are new.
- **Effect vocabulary (closed)**: `machineSpeed` (ppm), `machineOutput` (integer factor ≥ 2), `salePrice` (ppm), `upgradeCost` (ppm, at most 10^6), `startingCash` (absolute), `startingStorageLevel` (integer). An unknown kind rejects the document (`effect.kind.unknown`). A new kind needs code (FR-030).
- **Curves**: `{ base, growthPpm, rounding: "ceil" | "floor" }`, evaluated **iteratively per level** (`v₀ = base`, `vₗ₊₁ = round(vₗ × growthPpm / 10^6)`). The catalog precomputes each curve into a table up to the content `maxLevel`. Any table value above `ExactBound` rejects the document (`curve.overflow`).
- **Rationale**: Iterative evaluation is exact integer arithmetic that can be reproduced in a spreadsheet, and a per-level table keeps lookups allocation-free. The closed vocabulary satisfies "ordinary tuning is content-only; new mechanics require code".

## R08 — Save V2 and the generation store (FR-027)

- **Decision**:
  - Save V2 is strict UTF-8 JSON (`schemaVersion: 2`) in `<persistentDataPath>/IdleFactory/Checkpoints/v2/` (see [contracts/save-v2-and-v1-import.md](contracts/save-v2-and-v1-import.md)).
  - The format-independent generation protocol moves out of `JsonCheckpointStore` into `CheckpointGenerationFiles`, which is parameterized by a codec (serialize, deserialize and validate bytes). `JsonCheckpointStore` keeps its V1 API as a thin delegate over that code, so the legacy Save V1 suites that call `Promote` and `RecoverPrevious` stay unchanged (GD-1). The runtime reads V1 only through the new **read-only** `SaveV1CheckpointSource`, which the bootstrap wires; nothing in the Feature 003 runtime references `JsonCheckpointStore` (`RunAuthorityTests`, T119). V1 file names and bytes are unchanged. The Save V2 store uses the full protocol under `v2/`.
  - The Save V2 reader is a `JsonTextReader` token walk that rejects duplicate members at token level (Newtonsoft defaults to last-wins), integers written as `1.0` or `1e3`, a BOM, more than 1 MiB, depth above 16 and integers above 10^18 (SEC-3). The mapper also checks cross-field bounds (SEC-2; see the Save V2 contract).
  - Save V2 stores progress together with the `cycleDurationTicks` it was measured against, so that a content change in duration rescales on load (R03). It stores `contentRevision` only for diagnostics, while ID resolution against the current catalog is authoritative.
  - Unknown, duplicate and missing members, numbers outside bounds, unresolved IDs and invariant violations fail closed.
- **Rationale**: One implementation of the proven durability protocol with a single test surface. V1 is never written again, so its bytes stay identical by construction (SC-005). The existing Save V1 suites and the six committed V1 fixtures guard the extraction.
- **Alternatives rejected**: Extending Save V1 with optional members, which would change V1 meaning and break the fixtures. A second copy of the protocol for V2, which would create two sources of truth for durability.

## R09 — Durable transitions: offline return, prestige, import (FR-024, FR-025, FR-028)

- **Decision**: Only three transitions are **durable before publication**: offline return, prestige and V1 import. Ordinary commands (build, deliver, activate, purchase) mutate the session and are persisted by the existing autosave and suspension checkpoints. A crash rolls back both the spend and the grant together, so it can never duplicate a reward.
  - **Prestige**: every run has a `runId` (GUID). `ConfirmPrestige` builds the complete next-run candidate: content fresh-run state, Industry and preserved Items, `ip += award`, a new `runId`, `previousRunId` set to the current `runId`, and `runNumber + 1`. It promotes the candidate and publishes it only after the promotion has been verified. **Retry and restart** re-read the durable active checkpoint. If its `previousRunId` equals the in-memory `runId`, the transition already happened, and the session publishes it without a second award. Otherwise the retry re-promotes the *same* candidate bytes, with the same checkpoint ID. Cancellation or failure leaves the old run.
  - **Offline return**: reuses the Feature 001 exactly-once rules (captured return instant, `reconciledFromCheckpointId`) with the V2 codec.
  - **V1 import**: see R10.
- **Rationale**: Making these three transitions durable before publication satisfies FR-024, FR-025 and FR-028. Making every command durable would add file I/O to every tap without a stated requirement.

## R10 — V1 import (FR-028, SC-005)

- **Decision**: At cold start, the checkpoint source is chosen as follows:
  1. A valid V2 active checkpoint is loaded, followed by the normal V2 offline return.
  2. When `v2/` contains checkpoint files but none is a valid active checkpoint, the Feature 001 recovery path runs (previous generation, then the blocked state with its actions). **It never falls through to V1 import**, which would replace prestige and Industry progress and credit the V1 interval again (SEC-1).
  3. Otherwise, when `v2/` has no checkpoint files and a V1 active checkpoint exists, it is imported.
  4. Otherwise a fresh run starts.

  A confirmed **Start new run** after a blocked import (OD-3) promotes a fresh-run V2 checkpoint before it publishes the run. From then on step 1 selects V2, so V1 is no longer offered, with no extra marker (QA-8).

  The import steps are:
  1. Load the legacy import catalog (content version 1). Read V1 through the unchanged `SaveV1Mapper`.
  2. Reconcile its offline interval with the unchanged Feature 001 `OfflineReconciler` against a captured return instant `r`, **in memory only**.
  3. Map the stock. Ore is the sum of `dev.input` over all buffers, and Bar is the sum of `dev.output`.
  4. Map the machines. Each `dev.machine.basic` becomes a level-1 Smelter. Slots are assigned in ascending `(anchorY, anchorX, instanceId)` order, and that order is also the priority order. Instance ID and active state are kept, `investedCash` is 0 because placement was free, and progress is `MulDivFloor(old, smelterCycle, 5 s)`.
  5. Validate the mapped state:
     - every ID is mapped;
     - the machine count is at most the level-1 slot count;
     - total stock is at most the level-1 capacity;
     - every checked sum succeeds.
  6. Build a complete V2 candidate with departure `r` and the `import { sourceCheckpointId, sourceGeneration }` block, then promote it once. The remaining run fields take the content's fresh-run defaults, and the mapped resources and the Smelter are marked unlocked **(see OD-5)**.
  7. Publish the candidate.

  A retry in the same session after a failed promotion re-promotes the same candidate bytes. After a restart, a promoted V2 wins at step 1, so the V1 interval is never credited a second time. When the restart finds only an unpromoted pending file, the protocol discards it (Feature 001) and the import is recomputed from the unchanged V1 with a newly captured return instant; nothing had been published, so this credits the interval once (OPS-2). Any failure leaves `v1/` untouched, publishes nothing and names the failed rule.
- **Rationale**: This is the clarified procedure, made exact. The V1 directory is read-only, which makes the V1 generation recoverable.
- **Recovery UX after a failed import** is Retry or a confirmed new run keeping the V1 files **(OD-3, approved)**.

## R11 — Factory Value and IP (FR-020, FR-021)

- **Decision**:
  - `FV = cash + Σ stock[r] × salePrice(r) + Σ machine.investedCash + storageInvestedCash + factoryInvestedCash`. The sum is checked, and `salePrice` includes active run modifiers.
  - `investedCash` records what was actually paid, including discounts, and is stored because it is history, not a derivable value.
  - Removal refunds `MulDivFloor(investedCash, removalRefundPpm, 10^6)` with `removalRefundPpm` in `0 … 999,999` (ECO-7). The refund is therefore strictly less than any positive `investedCash`, so FV strictly decreases for any build and remove cycle with a positive cost, including under cost discounts.
  - A Research `salePrice` bonus raises the stock term of FV, as FR-020 ("at market prices") requires. The effect is bounded by the content's maximum sale-price multiplier and the storage capacity (about $0.4M at the initial tuning) and is recorded in the content record (ECO-7).
  - `IP = ISqrt(MulDivFloor(c², FV, divisor))` with `c = 10` and `divisor = 10^7`, which equals `floor(10 × sqrt(FV / 10^7))`. It gives 10, 20, 30 and 100 at $10M, $40M, $90M and $1B, and tests pin these values.
- **Preview**: live values that update with the session revision. Confirm computes FV and IP at the command timestamp, re-checks eligibility and shows exactly the values it awarded.

## R12 — Contracts, Items, Research, Overdrive, Industry (FR-014–FR-019, FR-022, FR-023)

- **Contracts**:
  - Content offers have kinds `plate`, `gear` and `timedBar`. At most one contract is active.
  - Accepting requires the offer to be unlocked and no contract to be active. Delivering consumes exactly the required stock and grants every reward in one checked transaction.
  - The scaling index is the number of completions of that offer in the current run. Expiry does not advance it **(interpretation; expiry grants nothing either way)**.
  - Item rewards are `{ itemId, onCompletionIndices[] }`, which gives deterministic milestone grants. The finite index lists (at most 256 each) bound Item accrual per run; the persistent stack is not capped beyond `ExactBound`, and the trace records accrual across runs (ECO-6).
  - Reward bounds (ECO-5): for every completion index up to 1,024, the Cash reward is at least the market value of the required units and at most `maxRewardRatioPpm` (content) times it (`contract.reward.ratio`), and an RP reward curve may not grow faster than its offer's quantity curve (`contract.rp.growth`), so RP per delivered unit never rises with re-acceptance.
  - Abandoning an untimed active contract is allowed without reward or penalty **(OD-4, approved)**.
- **Items**:
  - Activation requires an owned Item, a valid target and no active effect with the same **conflict key** `(effectKind, targetKey)`. It consumes exactly one Item and creates an effect over `[t, t + duration)` in real UTC.
  - Targets are machine *types* or the whole factory **(OD-1)**. A type-target and a factory-target effect of the same kind have different conflict keys and stack multiplicatively; the content record lists the maximum combined multiplier per effect kind, which `speed.level.dead` keeps within the bound (ECO-6).
  - Unused Item stacks are permanent state.
- **Research**:
  - A purchase is immediate: it checks prerequisites and `maxLevel`, spends RP in one checked operation, and applies its effects at the command timestamp with a progress rescale.
  - Research resets on prestige.
- **Overdrive**:
  - It has `activeUntilUtc` and `cooldownUntilUtc = start + duration + cooldown`. Activation requires `timeline ≥ cooldownUntilUtc`.
  - Overdrive continues offline (R05) and resets on prestige.
- **Industry**:
  - Purchases are permanent and cost IP.
  - Run-scoped effects (`machineSpeed`, `upgradeCost`, `salePrice`) apply immediately when bought. `startingCash` and `startingStorageLevel` apply at the next run start **(interpretation of "starts again stronger")**.
- **Unlocks**:
  - Conditions come from a closed vocabulary: `always`, `researchPurchased`, `factoryLevelAtLeast` and `resourceProduced`.
  - The unlocked set is stored and grows monotonically within a run. It resets on prestige to the content initial set plus Industry.
  - **Evaluation (owner decision 2026-09-30, found in the T137 review)**: the simulator evaluates every locked condition at each processed timestamp, including the command settle at offset 0, after completions and sales and before the run predicate. It repeats until no further unlock happens, so chained unlocks resolve at the same instant. Online, offline and every partition therefore unlock at the same simulated instant. T140 owns it.

## R13 — Balance evidence (SC-003, SC-004, clarification "Balance")

- **Decision**:
  - A deterministic `BalanceTraceRunner` in the Edit Mode test assembly (`Tests/EditMode/Balance/`) drives the **real application commands** with a manual clock through a documented `ReferencePolicy`. The policy is a written rule list owned by Game Systems/Economy. It records state at 10, 15, 30, 60, 90 and 120 minutes, time to first Gear, time to eligibility, the first IP purchase, and a second run under the same policy.
  - The output is `Tests/EditMode/Fixtures/Balance/trace-r<contentRevision>.csv`, with a Markdown summary copied into `specs/003-first-factory-prestige/reviews/balance-trace-r<N>.md`.
  - A normal run regenerates the trace in memory and compares it byte for byte with the committed file (the Save V1 fixture pattern). Writing the files requires `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`.
  - Sensitivity variants (±20 % prices, contract cadence, Overdrive disabled, an eight-hour absence at minute 30 and at minute 60, repeated short returns) are extra policy runs (ECO-4). A material offline effect would be a new owner decision, never a silent change to offline policy revision 1.
  - `reviews/balance-policy.md` fixes pass/fail bands (first Gear 600–900 s, eligibility 6,600–7,800 s, second run 4,200–4,800 s), sensitivity bounds, the policy's action latency and idle windows, and a no-perfect-timing rule (ECO-2). A run 3–5 projection spends all IP and asserts non-increasing run times above a floor of 25 % of run 1 (ECO-3). The golden CSV proves determinism; the bands test plausibility.
  - The content record states the theoretical maximum income per second and each machine's maximum batch (OD-6). Every telemetry-only value gets a hypothesis, a signal and a step of at most ±20 % per revision; a step that moves eligibility outside its band needs owner approval (ECO-9).
- **Rationale**: The trace drives real commands rather than a copy of the rules, so it cannot drift from the game. The golden comparison makes every change of `contentRevision` visible.
- **Gate**: the 2-hour and second-run claims (SC-003, SC-004) require (1) a trace, (2) a named human playtest record and (3) owner approval of a `contentRevision`. Until then the content is labeled a hypothesis.

## R14 — Presentation architecture (FR-011, FR-032, FR-033)

- **Decision**:
  - Keep the orthographic SpriteRenderer world for the **slot grid**. It shows fixed slot cells (empty, locked and occupied) and machine sprites with state badges. The fixed layout for 12 slots is a pure function of the slot index.
  - Keep the single UI Toolkit overlay with a responsive shell. Phones get a bottom tab bar (Factory, Market, Contracts, Items, Research, Prestige) and bottom sheets. Tablets and desktop get a side panel. The existing `FactoryHudLayout` classes and safe-area handling are reused.
  - Panels are presenters over application **read models**. Pan and zoom navigation stays. Free placement, footprints, orientation and the placement preview are retired.
- **Change detection**: `RunSession` exposes a `StructureRevision` (machines, levels, unlocks, settings, contracts, effects) and a `QuantityRevision` (stock, Cash, RP, progress).
  - Hot HUD values are copied without allocation: `ReadCash`, `CopyInventory` and `CopyMachineStates`. This extends the T063 pattern.
  - Full snapshots are built only on a structural revision or when a panel opens.
  - Countdown labels refresh at 1 Hz.
- **Rationale**: This reuses the proven boundary and the layout fixes recorded in the wiki (zero-height root, pixels versus points, themeless ScrollView). It avoids snapshots proportional to production frequency.

## R15 — Art and provisional Art Bible (FR-034)

- **Decision**:
  - `docs/art/F003-provisional-art-bible-v0.1.md` fixes the top-down orthographic view, the 64 px per cell convention inherited from F001, the palette and non-color state cues, the silhouettes of the four machines, and the slot, resource, Item-tier and UI-icon families.
  - `docs/art/F003-asset-briefs-v0.1.md` lists every asset row in the F001 inventory format: ID and type, logical footprint (1 slot), ports `N/A`, visual states, pixel target, format, pivot, source and import paths, and provenance.
  - Production reuses the F001 `T1` method (repository-authored SVG rasterized with Inkscape CLI 1.4.2) unless the Art Director approves another workflow. ComfyUI or Blender are not assumed. The Art Bible repeats the tool version and the no-silent-substitution rule (ART-6).
  - The Art Bible carries measurable readability rules: pixels per cell at each zoom step, 4.5:1 contrast for badges, grayscale and colour-vision-deficiency checks, numeric noise limits (ART-1); a state and marker precedence table with at most one state badge and two markers per slot, and a unique silhouette plus a text or shape cue per state, including locked slots (ART-2); an enumerated icon ID for every `presentationKey` and UI icon (ART-3); and a short UI token section (palette roles, focus ring, minimum text size, disabled look, contrast) that T112–T118 reference (ART-4).
  - `F001SpriteCatalog` is generalized, with its script GUID kept, into a key→sprite catalog that holds the new `F003-*` keys. The start-up presentation-key check (Feature 002 R08) covers the version 2 content.
- **Gate**: independent Art Director approval of v0.1 precedes asset production. Technical Art integrates only approved sources.

## R16 — Retiring the Feature 001 runtime path

- **Decision**: Once the new journey passes, the parent retires the Feature 001 *runtime* composition:
  - `FactorySession` gameplay use;
  - `PlacementCommands`, `ProductionCommands` and `PersistenceCommands`;
  - `PlacementInputController`, `PlacementPreviewView` and the D001 development scenario;
  - the Feature 001 HUD panels;
  - their Play Mode tests.

  Kept as legacy import support: `IdleFactory.Domain` definitions, `DefinitionCatalog`, `FactoryState`, `ProductionSimulator`, `OfflineReconciler` (Core), the Feature 002 version 1 loader and `SaveV1Mapper` (Unity), and their Edit Mode suites. The spec already approves the replacement of buffers and free placement, and Git history is the rollback path.

## R17 — Security, privacy and operations

- **Trust boundary**:
  - Save files and the system clock are untrusted local input. They get strict parsing, bounds, fail-closed validation and no reflection deserialization.
  - Packaged content is trusted build input that is still validated.
  - Clock manipulation is an accepted local risk. It is bounded by the eight-hour credit cap, and a backward clock grants zero credit and never un-expires anything.
  - Manifest hashes detect corruption and torn writes, not authenticity; editing local saves is an accepted local risk, recorded in the T131 threat register (SEC-4).
  - V2 and import failures surface only static codes and copy, never text derived from file content (SEC-5).
  - There are no secrets, no network and no personal data. Diagnostics never echo file-system paths (the Feature 002 T084 rule).
- **Migration and rollback**: the V1 to V2 import is non-destructive, because `v1/` is never written. Mixing builds loses progress in both directions (**approved OD-7**, development saves only): a Feature 002 build reads the stale V1 and ignores `v2/`, and progress that a Feature 002 build writes to `v1/` after an import is ignored by Feature 003, because an existing V2 is never re-imported. Both directions are documented in the release note and the T131 checklist; there is no detection code.
- **DevOps**: no CI, service or runner change. The existing `scripts/test-unity.ps1`, the StandalonePlayer category and the Android journey script are extended only with new tests and scenarios.
