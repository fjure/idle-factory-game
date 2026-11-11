# Data Model: World-First Factory Experience (Feature 006)

Revision 2, 2026-11-11, after the plan reviews (see [reviews/plan-reviews.md](reviews/plan-reviews.md)).

All entities are **presentation-only and never saved**, with three exceptions:

- `CompletedCycles` is a session-only read value owned by `RunSession` (R08).
- `DisplayPreferences` is a local file outside the save (R20).
- `WorldCompositionSettings` is an authored presentation asset (R22).

Inputs come from the existing read models: `MachineStateView`, `RunFactorySnapshot`, `MachineSnapshot`, `MachineBlockerInfo`, `ResourceRates` and `UpgradePreview`.

## Ownership and writers

| State | Owner | Writer | Observers | Persistence | Determinism |
|---|---|---|---|---|---|
| Per-advance completion counts | `RunSimulator` | `CompletionPass` (+1 per completed cycle), exposed only after `Commit()` | `RunSession` | None | Deterministic |
| `CompletedCycles` per `InstanceId` | `RunSession` | Accumulates after each successful advance. Pruned on removal, reset on replace, empty on construction, restore and prestige. | `RefreshViews` → `MachineStateView` | None | Deterministic. No observer effect (test). |
| `WorldCompositionSettings` | World Designer (values), Architect (shape) | Unity tooling only | `LatticeLayoutBuilder` | Asset in the repository | Static |
| `FactoryWorldLayout` | `LatticeLayoutBuilder` (or the explicit-cells constructor in tests) | Rebuilt on a location-count or orientation change | Camera model, framing, views, hit-test, card, build card | None | Pure function |
| `MachinePresentation[]` | `MachinePresentationModel` | On a revision change | Slot view, depot/link view, effects, card | None | Pure function of the snapshot and the previous baseline |
| `AttentionState` | `AttentionModel` | On a revision change | World attention marker, strip indicator, hub dot | None | Pure function |
| `DevelopmentStageValue` | `DevelopmentStage` | — | Deck, edges, props, depot | None | Pure function |
| `PendingMilestone` queue | `MilestoneWatcher` | On a revision change | Banner, deck reveal | None | Derived |
| Navigation frames, selection, docked band | `ShellNavigationStack`, `ShellSheetHost` | UI events | Camera (selection-driven), views | None | Cleared on world replacement |
| `EffectInstance` pool | `EffectPool` | Spawn and coalesce on model deltas | Clock | None | Clock-driven |
| `DisplayPreferences` v2 | `DisplayPreferencesStore` | Settings sheet | Clock (reduced motion), shell (zoom buttons) | `display-v2.json` | — |

## Entities

### BuildingLocationId

- **Fields**: `Value: int`. Today it equals the slot index.
- **Identity**: stable. A position is looked up from the id and is never used as identity (FR-038).

### WorldCompositionSettings (R22)

| Field | Meaning |
|---|---|
| `marginCells: int` | Width of the margin band in cells. Default reproduces F004's half-cell fit margin. |
| `depotRule: {portrait, landscape}` | Margin cell at the end of the row trunks. |
| `depositRule` | Margin cell, never inside a hit shape or a keep-out. |
| `propWeightsPerStage[3]: (kind, weight)[]` | Prop selection weights for each stage. |
| `propDensityPerStage[3]: float` | Probability that an eligible cell gets a prop. |
| `maxPropHeightArtPx = 24` | Height limit for props. |
| `hatchRule`, `edgeRulePerStage[3]` | Railing, then wall, then gantry. |

**Validation**: Edit Mode tests check that the values are in range and that the default lattice reproduces the F004 Extent and fit.

### FactoryWorldLayout (R04, R06, R07)

| Field or method | Meaning |
|---|---|
| `LayoutId: string` | Identity used by the camera model. |
| `LocationCount`, `Orientation` | — |
| `Bounds: Rect` | **Equals today's `SlotGridLayout.Extent`**: plates plus the badge band, in world units. The margin is not included. |
| `Margin` | Kept separate. The camera model adds it as it does today. |
| `AnchorOf(id)`, `FootprintOf(id)`, `HitShapeOf(id)`, `SortKeyOf(id)` | Per-location geometry. |
| `TryHit(world, out id)` | Closed-form hit test. |
| `DeckCells` | Rectangle-grid cells of 70×54 art px covering **all** locations plus the margin band. |
| `IsUnlocked(id, unlockedCount)` | Unlock state of a location. |
| `DepotCell`, `DepotAnchor`, `DepositCell` | Positions of the depot and the deposit. |
| `TrunkOf(row) → cells` | Static lane in the 19 px row gap. |
| `BranchOf(id) → segment` | From the plate's rear edge to the row trunk. |

**Validation:**

- Footprints never overlap, and hit shapes are disjoint.
- Every branch reaches its trunk, every trunk reaches the depot, and no lane crosses a footprint.
- `DepotCell` and `DepositCell` lie in the margin band and outside hit shapes and keep-outs.
- `Bounds` and `CameraScaleModel.LargestFittingScale` equal the F004 values for 12 locations in 3 and 4 columns on every screen class (parity).
- Test layouts (6, 20, L) pass all of the above.

### MachinePresentation (R08)

| Field | Meaning |
|---|---|
| `Location`, `MachineTypeIndex`, `Level` | Identity and level of the machine. |
| `VisualState` | From the existing `SlotGridModel.StateFor`. |
| `UpgradeAvailable` | `NextUpgrade.Available && Affordable`. |
| `ActivityPace: Slow\|Normal\|Fast` | From `CycleTicks`, capped per family by flash rate (R12). |
| `CompletionsDelta: int` | `CompletedCycles` minus the baseline. 0 when the baseline resets (restore, offline return, decrease). |
| `LimitingResourceId?` | Set when the blocker is MissingInput. |

### AttentionState (R09)

| Field | Meaning |
|---|---|
| `SharedCauses: (ResourceId, AffectedLocations[])[]` | Groups of 2 or more MissingInput machines. |
| `Shortage: (ResourceId, CoveragePpm, AffectedCount)?` | Over resources that block at least one MissingInput machine and have `HasDemand`. Ordered by coverage ascending, then affected count descending, then content order. |
| `StoreFull: bool` | At least one machine is StorageFull. |
| `SystemFlags: {Contracts, Research, Prestige, Overdrive}` | Existing conditions. Market has no flag. |
| `HubDot` | True if any `SystemFlags` entry is set. Production attention never drives the hub dot. |

### DevelopmentStageValue (R15)

| Field | Rule |
|---|---|
| `Stage` | `t ≤ 0 → Early`; otherwise `clamp(floor(3·u/t), 0, 2)`, giving Early, Partial or Established. |
| `ActivityDensity` | 3 bins. |
| `LevelDensity` | 3 bins. |

**Inputs**: unlocked and maximum location counts, active machines and machine levels. Prestige count is never an input.

### PendingMilestone (R14)

| Field | Meaning |
|---|---|
| `Kind: FactoryLevelUp\|PrestigeCompleted` | Milestone type. |
| `NewlyUnlocked[]` | Locations unlocked by the milestone. |
| `ArisenOffline` | When true, the milestone is listed in the summary only. |

### EffectInstance (R12)

| Field | Meaning |
|---|---|
| `Kind: CompletionPop\|Spark\|Steam\|BuildDust\|UpgradeGlint\|VeinChip` | Effect type. |
| `Location`, `StartMs`, `Frames`, `Fps` | Placement and timing. |

**Pool**: 8 renderers, made of 3 active plus 5 for draining tails.

**Timing**: coalescing timestamps are sized by `LocationCount`. The window is 250 ms.

**Never spawned** under reduced motion or for offline deltas.

### NavigationFrame (R11)

| Field | Meaning |
|---|---|
| `Kind: Dialog\|PinnedValue\|MachineCard\|BuildCard\|MachineDetails\|Hub\|SystemView\|Settings` | Frame type. |
| `Invoker` | Element that receives focus on return. |

**Rules:**

- MachineCard and BuildCard are mutually exclusive.
- Opening the Hub closes either of them and restores the camera.
- Back and Escape pop one frame per frame tick.

### DisplayPreferences v2 (R20)

**Format**: `{ "version": 2, "reducedMotion": "followSystem"|"on"|"off", "showZoomButtons": bool }`.

**Read order:**

1. If `display-v2.json` exists, read it with the v2 strict parser.
2. If that file is invalid, use the defaults and emit a diagnostic. Do not fall back to v1.
3. If `display-v2.json` is absent, read `display-v1.json` with the unchanged v1 parser and set `showZoomButtons = false`.
4. If neither file exists, use the defaults.

**Write:**

- The temporary file is `display-v2.json.tmp`, with atomic replace.
- `display-v1.json` is left in place.

### World Bible (R22)

The World Bible is a document at `docs/world/world-bible-v<major>.<minor>.md`, with semantic versioning.

**v0.1 contents:**

- site concept (a neutral starter industrial platform on a dark base);
- site name and tagline;
- mood words;
- naming and copy rules;
- the swappable-ground rule;
- notes on future deposits.

**Referenced by**: art briefs, mockups and `reviews/world-copy.md`.
