# Data Model: World-First Factory Experience (Feature 006)

All entities are **presentation-only and never saved**. Exceptions:

- `CompletedCycles` is a session-only domain read value (R08).
- `DisplayPreferences` lives in the local preferences file, outside the save (R20).

Values come from existing read models: `MachineStateView`, `RunFactorySnapshot`, `MachineSnapshot`, `MachineBlockerInfo`, `ResourceRates` and `UpgradePreview`.

## Ownership and writers

| State | Owner | Writer | Observers | Persistence | Determinism |
|---|---|---|---|---|---|
| `MachineStateView.CompletedCycles` | `RunSimulator` | Simulator per completed cycle (+batch) | `MachinePresentationModel` | None (0 at construction/restore) | Deterministic from the simulation |
| `FactoryWorldLayout` | `LatticeLayoutBuilder` | Rebuilt on location-count or orientation change | Views, camera, hit-test, card | None | Pure function of (locationCount, orientation) |
| `MachinePresentation[]` | `MachinePresentationModel` | On revision change | Slot view, effects, card | None | Pure function of snapshot + previous counters |
| `AttentionState` | `AttentionModel` | On revision change | Depot/link view, persistent attention, hub | None | Pure function of snapshot |
| `DevelopmentStageValue` | `DevelopmentStage` (static) | — | Deck view, depot | None | Pure |
| `PendingMilestone` queue | `MilestoneWatcher` | On revision change | Banner, deck reveal | None | Derived from snapshot diff |
| Selection, card, details, hub, preview | `RunShellDocument` + `ShellNavigationStack` | UI events | Camera, views | None | Cancelled on world replacement |
| `EffectInstance` pool | `EffectPool` | Spawn/coalesce on model deltas | Clock | None | Clock-driven frames |
| `DisplayPreferences` v2 | `DisplayPreferencesStore` | Settings sheet | Clock (reduced motion), shell (zoom buttons) | `display-v2.json` (reads v1) | — |

## Entities

### BuildingLocationId
- `Value: int`. Today it equals the slot index, `0 ≤ Value < LocationCount`.
- It is a stable identity. A location's position is looked up from it and never used as its identity (FR-038).

### FactoryWorldLayout (R04)
- `LocationCount: int`, `Orientation: Portrait|Landscape`
- `Bounds: Rect` (world units; content plus the deck margin)
- `AnchorOf(id) → Vector2`, `FootprintOf(id) → Rect`, `HitShapeOf(id) → diamond (centre, half-extents)`, `SortKeyOf(id) → int`
- `DeckCells: IReadOnlyList<Vector2Int>` (all locations plus the margin), `IsUnlockedCell(cell, unlockedCount)`
- `DepotAnchor: Vector2`, `DepotHitShape`
- `LinkPathOf(id) → IReadOnlyList<Vector2Int>` (lane cells from the location to the depot; straight and L segments)

**Validation**
- Footprints never overlap.
- Hit shapes are disjoint.
- Every location has a link path that reaches the depot without crossing another location's footprint.
- `Bounds` contains all footprints, the depot and the links.
- The lattice builder reproduces today's `SlotGridLayout` pivots and rects exactly for 12 locations in 3 and 4 columns (parity test).

### MachinePresentation (R08)
- `Location: BuildingLocationId`, `MachineTypeIndex`, `Level`
- `VisualState: MachineVisualState`, from the existing `SlotGridModel.StateFor` (the single precedence rule)
- `UpgradeAvailable: bool` = `NextUpgrade.Available && NextUpgrade.Affordable`
- `ActivityPace: Slow|Normal|Fast`, from `MachineRates.CycleTicks` (bucket bounds defined in the contract)
- `CompletionsDelta: int`, the counter difference since the last refresh, clamped at 0. A decrease or restore resets the baseline and reports no completion.
- `LimitingResourceId: string?`, from `MachineBlockerInfo`, when the blocker is MissingInput

**State transitions:** none of its own. It mirrors the authoritative transitions. A delta is reported once.

### AttentionState (R09)
- `SharedCauses: list of (ResourceId, AffectedLocations[])`, groups of 2 or more MissingInput machines.
- `Bottleneck: (ResourceId, CoveragePpm, AffectedCount)?`. The lowest coverage below 1,000,000 among consumed inputs. Ties are broken by the affected count, then by the content order.
- `StoreFull: bool`
- `SystemFlags`: Contracts (claimable or expiring), Research (affordable node), Prestige (ready), Overdrive (ready), Market (= StoreFull)
- `Aggregate: bool`, true when any system flag is set (drives the hub entry's dot)

### DevelopmentStageValue (R15)
- `Stage: Early|Partial|Established` = `clamp(floor(3·unlocked/total), 0, 2)`
- `ActivityDensity: 0..2`, from `active / unlocked`
- `LevelDensity: 0..2`, from `meanLevel / maxLevel`

**Inputs:** the current and maximum factory-level slot counts, the active machine count and the machine levels. Prestige count is never an input.

### PendingMilestone (R14)
- `Kind: FactoryLevelUp|PrestigeCompleted`
- `NewLevel: int?`, `NewlyUnlocked: BuildingLocationId[]`
- `ArisenOffline: bool`. When true, the milestone is listed in the offline summary and is not animated.

### EffectInstance (R12)
- `Kind: CompletionPop|Spark|Steam|BuildDust|UpgradeGlint`
- `Location`, `StartMs`, `FrameCount`, `Fps`
- Pool capacity 8, at most 3 active world effects, coalescing window 250 ms per location for CompletionPop. Effects are never spawned when reduced motion is on.

### NavigationFrame (R11)
- `Kind: Dialog|PinnedValue|MachineCard|MachineDetails|BuildCard|Hub|SystemView|Settings`
- `Invoker: VisualElement` (focus-return target)
- `Close(): void`

**Stack rule:** Back and Escape pop exactly one frame per frame-tick. An empty stack means the factory screen.

### DisplayPreferences v2 (R20)
- JSON `{ "version": 2, "reducedMotion": "followSystem"|"on"|"off", "showZoomButtons": true|false }`
- A v1 file reads as `{reducedMotion: v1 value, showZoomButtons: false}`.
- An unknown version or a malformed file falls back to the defaults and emits the once-per-session diagnostic (the F004 behaviour).
- Writes are atomic, using the existing store protocol.
