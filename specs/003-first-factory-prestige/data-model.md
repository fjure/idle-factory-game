# Data Model: First Iron Factory and Prestige Loop

**Spec**: [spec.md](spec.md) · **Research**: [research.md](research.md) · **Contracts**: [contracts/](contracts/)

New runtime types live in `IdleFactory.Core` (engine-free) under the namespaces `IdleFactory.Numerics`, `IdleFactory.Run.Content`, `IdleFactory.Run.Domain`, `IdleFactory.Run.Simulation` and `IdleFactory.Run.Application`. The Feature 001 types in `IdleFactory.Domain`, `IdleFactory.Simulation` and `IdleFactory.Core.Application` remain unchanged and serve only as legacy V1 import support (R16).

## Units and bounds

| Quantity | Type | Range | Rounding |
|---|---|---|---|
| Resource units, Cash ($), RP, IP, Item count | `long` | `0 … ExactBound` (10^18) | Integer only. Overflow rejects atomically (R06) |
| Multipliers | `long` ppm | `1 … maxCombinedSpeedPpm` for speed; an integer factor for output | Folded with `MulDivFloor` |
| Durations / progress | `long` 100 ns ticks | `1 ≤ cycleDuration`; `0 ≤ progress ≤ cycleDuration` (equality means *waiting*) | `cycleDuration` uses `MulDivCeil`. Rescale uses `MulDivFloor` |
| Real instants | `long` Unix ms (UTC) | Representable `DateTimeOffset` | Clock values are floored to ms (Feature 001) |

## Ownership

| State | Owner (sole writer) | Readers | Persisted | Reset on prestige |
|---|---|---|---|---|
| `RunCatalog` (immutable content) | `RunCatalogBuilder`, once per start | Everyone | No (IDs and `contentRevision` referenced) | — |
| `RunState` (below) | `RunSimulator` for production, sales and expiry; `RunCommands` for player operations | Read models | Save V2 `run` | Yes (replaced) |
| `PermanentState` (IP, Industry levels, Item stacks, run counter) | `RunCommands` (Industry, Items) and `PrestigeTransition` | Read models | Save V2 `permanent` | No |
| Run timeline anchor | `RunSession` | Simulator | Implicit through the checkpoint `departureUtc` | Re-anchored |
| Priority order | `RunCommands` | Simulator | Yes | Yes |
| Presentation state (selection, open panel, camera) | Unity presenters | — | Never | — |

## Content definitions (`IdleFactory.Run.Content`)

- **RunCatalog**: `ContentRevision`; resources, recipes and machines keyed by ID; `StorageLevels[]`, `FactoryLevels[]`, `ContractOffers`, `Items`, `Research`, `IndustryUpgrades`; `Overdrive`, `Prestige`, `RunStart`, `OfflinePolicy` (revision 1 only); precomputed per-level tables (R07).
- **ResourceDef**: `Id`, `Label`, `PresentationKey`, `BasePrice ($ ≥ 0)`, `Unlock`.
- **MachineRecipe**: `Id`, `Inputs[]` (0–256, unique, amount ≥ 1), `Outputs[]` (1–256), `DurationTicks ≥ 1`.
- **MachineDef**: `Id`, `Label`, `PresentationKey`, `RecipeId`, `CostTable[level]` (build cost = `CostTable[0]`; upgrade L→L+1 costs `CostTable[L]`), `SpeedTable[level]` (ppm, including milestones), `MaxLevel`, `Milestones[] {level, speedPpm}`, `Unlock`.
- **StorageLevelDef / FactoryLevelDef**: `Level`, `Capacity` / `SlotCount`, `Cost` (level 1 costs 0). Slot counts never decrease, and the highest slot count is at most 12 (guard 64).
- **ContractOfferDef**: `Id`, `Kind` (`plate`, `gear`, `timedBar`), `ResourceId`, `QuantityCurve`, `DurationMs?` (required exactly for `timedBar`), `Rewards[]` (`cash` curve, `rp` curve, `item {itemId, onCompletionIndices[]}`), `Unlock`.
- **ItemDef**: `Id`, `Tier` (1–3), `Label`, `PresentationKey`, `DurationMs`, `Target` (`factory`, or `machineType` with `AllowedMachineIds[]`), `Effect`.
- **ResearchDef**: `Id`, `Label`, `MaxLevel`, `RpCostCurve`, `Prerequisites[] {researchId, level}` (acyclic), `Effect` (value per level).
- **IndustryUpgradeDef**: `Id`, `Label`, `MaxLevel`, `IpCostCurve`, `Effect` (value per level).
- **EffectDef**: `Kind` ∈ {`machineSpeed`, `machineOutput`, `salePrice`, `upgradeCost`, `startingCash`, `startingStorageLevel`}, `Target` (all machines, machine ID, all resources, resource ID, or none), `Value`.
- **OverdriveDef**: `DurationMs`, `CooldownMs`, `SpeedPpm`, `Unlock`. **PrestigeDef**: `ThresholdFactoryValue`, `IpCurve {coefficient, divisor}`. **RunStartDef**: `StartingCash`, `InitialUnlocks[]`, `RemovalRefundPpm` (0–10^6), `MaxCombinedSpeedPpm`, `MaxCombinedOutputFactor`.

Validation rules and codes are listed in [contracts/gameplay-content-v2.md](contracts/gameplay-content-v2.md).

## Run state (`IdleFactory.Run.Domain`)

- **RunState**: `RunId` (GUID), `RunNumber ≥ 1`, `PreviousRunId?`, `Cash`, `Rp`, `StorageLevel`, `StorageInvestedCash`, `FactoryLevel`, `FactoryInvestedCash`, `Inventory`, `Machines` (by slot), `PriorityOrder`, `AutoSell` (set of resource IDs), `Unlocked` (set of IDs), `Contracts`, `ResearchLevels`, `ActiveEffects`, `Overdrive`.
- **GlobalInventory**: `resourceId → units`. Invariant: `Σ units ≤ Capacity(StorageLevel)` (including industry), every entry is ≥ 0, and only unlocked or mapped resource IDs appear. Its only mutations are the atomic exchange, a sale, a delivery and an import.
- **MachineInstance**: `InstanceId` (GUID), `MachineId`, `SlotIndex` (unique, `< SlotCount`), `Level` (1…MaxLevel), `IsActive`, `ProgressTicks`, `CycleDurationTicks` (derived; also persisted for the load-time rescale), `InvestedCash`. There are no buffers.
- **PriorityOrder**: an ordered list that is a permutation of the current machine instance IDs. A new machine is appended, a removed one is deleted, and reordering is a permutation command.
- **ContractBoard**: per offer `CompletionCount`; `Active? { OfferId, AcceptedUtcMs, DeadlineUtcMs?, RequiredUnits }`. `RequiredUnits` is fixed when the contract is accepted.
- **ActiveEffect**: `SourceId` (Item ID), `ConflictKey (effectKind, targetKey)`, `StartUtcMs`, `EndUtcMs`. At most one effect per `ConflictKey`.
- **OverdriveState**: `ActiveUntilUtcMs?`, `CooldownUntilUtcMs?`.
- **PermanentState**: `Ip`, `IndustryLevels`, `ItemStacks` (`itemId → count`), `PrestigeCount`.

## State transitions

```text
Machine:   (none) --Build(slot, type, cost paid)--> Paused/Running(progress 0, priority last)
           Running --event: progress reaches D--> Due --complete fits--> Running/Paused (progress 0)
                                                    \--does not fit--> Waiting(progress = D) --later fit--> complete
           Running <--run predicate at each event/settle--> Paused   (progress kept, no backlog)
           any --Upgrade / effect or research boundary--> same state, progress rescaled (R03)
           any --Remove(confirmed)--> (none): refund floor(invested × refundPpm), slot freed, priority closed

Contract:  Offered --Accept (none active)--> Active --Deliver (stock ≥ required, t ≤ deadline)--> Offered(count+1, reward granted once)
                                               \--timeline > deadline--> Offered(count unchanged, outcome "expired", no reward)

Effect:    Item stack --Activate(valid target, no conflict)--> Active[start, end) --timeline ≥ end--> Expired (removed)
Overdrive: Ready --Activate--> Active --end--> Cooldown --cooldownUntil--> Ready

Run:       Running --ConfirmPrestige (FV ≥ threshold)--> PrestigePending --durable promotion verified--> New Running(runNumber+1, ip+award)
                                                               \--failure--> Running(old)   [retry is idempotent via previousRunId]
Startup:   V2 active? --> Load+OfflineReturn | V1 active? --> Import(Validate, Reconcile, Map, Promote) | FreshRun
           Import failure --> ImportBlocked(rule shown; V1 untouched) --> Retry | (OD-3) Start new run
```

## Derived values (never stored)

- The effective `speedPpm`, output factor, sale price, costs and capacity of each machine.
- Per-resource production, consumption and net rates. These come from `units × 10^7 / cycleDuration` for running machines, and each blocked machine lists its blocker (`Inactive`, `MissingInput(resource, required, available)` or `StorageFull(netRequired, free)`).
- Factory Value and its breakdown, the IP preview and eligibility (R11).
- The offline summary is derived once per return from the pre- and post-return states plus the events processed, and it is kept only in memory (it is never re-derived after a load).
