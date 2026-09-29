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
  - The effective speed of a machine is `speedPpm = clamp(level × milestones × research × industry × active items × Overdrive)`. It is folded left to right with `MulDivFloor(acc, factor, 1_000_000)`, in the fixed order listed in the contract, and clamped to the content's `maxCombinedSpeedPpm`.
  - `cycleDuration = MulDivCeil(recipe.durationTicks, 1_000_000, speedPpm)`, and never less than 1. Rounding the duration up means the displayed speed is never overstated.
  - An output effect (`machineOutput`) is an **integer factor** that multiplies output amounts only. Inputs are unchanged. Combined factors are clamped to `maxCombinedOutputFactor`.
  - Level growth (`1.08^(L−1)`) and milestones (×2/×2/×3/×5 at levels 10/25/50/100) are **speed** effects. Throughput is the same as for an output effect, and the atomic exchange stays at recipe granularity **(interpretation of "rate")**.
  - Whenever a boundary changes `cycleDuration` from `D` to `D′`, progress becomes `p′ = MulDivFloor(p, D′, D)`. A waiting machine (`p == D`) stays waiting (`p′ = D′`). This preserves the completed fraction exactly as clarified. Only the boundary timestamp is involved, so all partitions agree.
- **Rationale**: Integer ppm arithmetic is identical on Mono and IL2CPP. The same rescale formula serves upgrades, effect start and expiry, Research purchases, loading a checkpoint whose content-tuned duration changed, and the V1 import (`floor(old × 2/5)` is `MulDivFloor(old, 2 s, 5 s)`).
- **Alternatives rejected**: `double` math, which risks platform and compiler differences. Rational progress fractions, which need unbounded denominators.

## R04 — Event-count bound and offline cost (FR-025, SC-008)

- **Decision**: The content validator rejects a document when any recipe at `maxCombinedSpeedPpm` would have a cycle shorter than **`MinEffectiveCycleTicks` = 1,000,000 (0.1 s)** (`speed.bound.too-fast`). This is a parser and engine guard like the R04 limits of Feature 002, not a gameplay value. The worst case is 12 machines × 23,040 effective seconds / 0.1 s ≈ 2.8 M completions for an eight-hour absence.
- **Rationale**: The bound caps the event loop without changing semantics. Measured budgets are in plan.md › Performance (W003). If a budget fails, the Performance Engineer must review exact accelerations before any semantic change. The candidates are (a) batching uncontended machines, whose inputs no other machine consumes, in closed form, and (b) detecting steady-state periods.
- **Alternatives rejected**: Clamping speed silently, which hides player-visible upgrades. Coarser simulation steps (see R01).

## R05 — Real-time timeline, deadlines and offline segmentation (FR-014, FR-015, FR-025, FR-026)

- **Decision**:
  - **Online**: the session keeps a *run timeline* in UTC ticks: `timelineUtc = anchorUtc + activeTicksSinceAnchor`. The anchor is re-bound at every return and at every load. Effects, Overdrive and contracts store absolute UTC end instants in Unix milliseconds. They expire when the timeline reaches their boundary. A contract's deadline is inclusive, so its expiry boundary is `deadlineUtc + 1 ms`, and a delivery command at `timelineUtc == deadlineUtc` succeeds.
  - **Offline**: for departure `d`, return `r` and policy revision 1 (4/5, 8 h cap), define `E(x) = floor(min(max(x − d, 0), cap) × 4 / 5)`. `E` maps real instants to the effective simulation timeline. Every real boundary `b` in `(d, r]` becomes a simulation event at `E(b)`, with the expiry kinds ordered by `(kind, id)` for determinism. Production advances by `E(r)` in total. A boundary beyond `d + cap` is processed at the end of production, because it has no production effect. The summary reports measured, credited and effective durations.
  - A backward clock or a missing clock keeps Feature 001 behaviour: zero credit, a nondecreasing departure, and a blocked return with retry. Deadlines and effects are never un-expired, because the timeline never moves backwards.
- **Rationale**: `E` telescopes, `Σ(E(bᵢ₊₁) − E(bᵢ)) = E(r)`. Segmenting at boundaries therefore credits exactly what the unsegmented Feature 001 formula credits, and an effect covers precisely its active portion of reduced production. That portion is 4/5 of its real duration, the same compression as all offline production.
- **Alternatives rejected**: Flooring each segment separately, which loses ticks and depends on the partition. Remaining-duration counters driven by active time, which would freeze real-time effects while the app is suspended and contradict the clarification.

## R06 — Exact numbers (FR-035, clarification "Numeric scope")

- **Decision**:
  - Resource units, Cash, RP, IP and Item counts are `long` values in the closed range `[0, ExactBound]` with `ExactBound = 10^18`. Every add, multiply and sum goes through `CheckedMath`, whose operations return `bool` without allocating.
  - Exceeding the bound or `Int64` rejects the whole command or advance atomically with `ArithmeticOverflow`, and the player sees "value limit reached".
  - `MulDivFloor` and `MulDivCeil` use an allocation-free 128-bit intermediate (two `ulong` halves). They serve ppm composition, progress rescale, prices and the IP curve. `ISqrt` uses Newton's method on `ulong`.
  - Cash is whole dollars. Sale income is `MulDivFloor(units × basePrice, salePricePpm, 10^6)`.
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
  - The format-independent generation protocol moves out of `JsonCheckpointStore` into `CheckpointGenerationFiles`, which is parameterized by a codec (serialize, deserialize and validate bytes). The Save V1 store becomes a **read-only** adapter over the same protocol code for `v1/`, and its file names and bytes are unchanged. The Save V2 store uses the full protocol under `v2/`.
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
  2. Otherwise, when a V1 active checkpoint exists, it is imported.
  3. Otherwise a fresh run starts.

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

  A retry after a failed promotion re-promotes the same candidate. After a crash, step 1 of the source selection sees the promoted V2, so the V1 interval is never credited a second time. Any failure leaves `v1/` untouched, publishes nothing and names the failed rule.
- **Rationale**: This is the clarified procedure, made exact. The V1 directory is read-only, which makes the V1 generation recoverable.
- **Recovery UX after a failed import** is Retry or a confirmed new run keeping the V1 files **(OD-3, approved)**.

## R11 — Factory Value and IP (FR-020, FR-021)

- **Decision**:
  - `FV = cash + Σ stock[r] × salePrice(r) + Σ machine.investedCash + storageInvestedCash + factoryInvestedCash`. The sum is checked, and `salePrice` includes active run modifiers.
  - `investedCash` records what was actually paid, including discounts, and is stored because it is history, not a derivable value.
  - Removal refunds `MulDivFloor(investedCash, removalRefundPpm, 10^6)`. The refund is at most `investedCash`, so FV strictly decreases for any build and remove cycle with a positive cost.
  - `IP = ISqrt(MulDivFloor(c², FV, divisor))` with `c = 10` and `divisor = 10^7`, which equals `floor(10 × sqrt(FV / 10^7))`. It gives 10, 20, 30 and 100 at $10M, $40M, $90M and $1B, and tests pin these values.
- **Preview**: live values that update with the session revision. Confirm computes FV and IP at the command timestamp, re-checks eligibility and shows exactly the values it awarded.

## R12 — Contracts, Items, Research, Overdrive, Industry (FR-014–FR-019, FR-022, FR-023)

- **Contracts**:
  - Content offers have kinds `plate`, `gear` and `timedBar`. At most one contract is active.
  - Accepting requires the offer to be unlocked and no contract to be active. Delivering consumes exactly the required stock and grants every reward in one checked transaction.
  - The scaling index is the number of completions of that offer in the current run. Expiry does not advance it **(interpretation; expiry grants nothing either way)**.
  - Item rewards are `{ itemId, onCompletionIndices[] }`, which gives deterministic milestone grants.
  - Abandoning an untimed active contract is allowed without reward or penalty **(OD-4, approved)**.
- **Items**:
  - Activation requires an owned Item, a valid target and no active effect with the same **conflict key** `(effectKind, targetKey)`. It consumes exactly one Item and creates an effect over `[t, t + duration)` in real UTC.
  - Targets are machine *types* or the whole factory **(OD-1)**.
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

## R13 — Balance evidence (SC-003, SC-004, clarification "Balance")

- **Decision**:
  - A deterministic `BalanceTraceRunner` in the Edit Mode test assembly (`Tests/EditMode/Balance/`) drives the **real application commands** with a manual clock through a documented `ReferencePolicy`. The policy is a written rule list owned by Game Systems/Economy. It records state at 10, 15, 30, 60, 90 and 120 minutes, time to first Gear, time to eligibility, the first IP purchase, and a second run under the same policy.
  - The output is `Tests/EditMode/Fixtures/Balance/trace-r<contentRevision>.csv`, with a Markdown summary copied into `specs/003-first-factory-prestige/reviews/balance-trace-r<N>.md`.
  - A normal run regenerates the trace in memory and compares it byte for byte with the committed file (the Save V1 fixture pattern). Writing the files requires `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`.
  - Sensitivity variants (±20 % prices, contract cadence, Overdrive disabled) are extra policy runs.
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
  - Production reuses the F001 `T1` method (repository-authored SVG rasterized with Inkscape CLI 1.4.2) unless the Art Director approves another workflow. ComfyUI or Blender are not assumed.
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
  - There are no secrets, no network and no personal data. Diagnostics never echo file-system paths (the Feature 002 T084 rule).
- **Migration and rollback**: the V1 to V2 import is non-destructive, because `v1/` is never written. Downgrading to a Feature 002 build is not supported: it would read the stale V1 and ignore `v2/`. This is documented in the player-facing release notes of the feature.
- **DevOps**: no CI, service or runner change. The existing `scripts/test-unity.ps1`, the StandalonePlayer category and the Android journey script are extended only with new tests and scenarios.
