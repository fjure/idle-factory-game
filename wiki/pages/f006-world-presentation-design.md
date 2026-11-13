---
title: F006 world presentation design (planned)
type: component
sources: [S056, S057, S058, S059]
updated: 2026-11-13
---

# F006 world presentation design (partly implemented)

Design from `research.md` R04-R08, R11, R23 of Feature 006. Implemented in batch P1: the layout seam (R04, T512), the sorting layers (T513), `CompletedCycles` (R08, T514) and the `ShellNavigationStack`/`ShellSheetHost` extraction (R11, T511). Art families for deck, links, depot, vein and edge, and the shortage-ranking models (T532), landed in P2; runtime wiring of deck, links, depot and vein is still planned. See [batch P1](./f006-batch-p1-enabling-refactor.md) and [batch P2](./f006-batch-p2-art-and-presentation.md). (S056, S058, S059)

## Layout seam (R04, implemented in T512)

- `FactoryWorldLayout`: immutable class with `LocationCount`, `LayoutId`, `Bounds`, `AnchorOf`, `FootprintOf`, `HitShapeOf`, `SortKeyOf`, `TryHit(world, out id)`, `DeckCells`, depot, trunk, branch and deposit cells.
- **`Bounds` equals today's `Extent`**; the margin stays a separate value that the camera model keeps adding, so it is not counted twice. `ScaleLayout.Columns`/`Key` become `LayoutId` plus `Bounds`.
- `LatticeLayoutBuilder` reproduces the 3 and 4 column lattice. A separate **explicit-cells constructor** serves test layouts (6, 20, L shape).
- A parity test checks `LargestFittingScale` against current values for every screen class. `FactoryCameraController` stays the sole camera writer. `BuildingLocationId` equals the slot index; machine identity stays the instance id. (S056)

## Deck, sorting layers, props (R06)

- Rectangle-grid `Tilemap`, cell 70x54 art px; not isometric, because 17.5 px half-steps break whole-pixel placement. `DeckCells` cover all locations plus the margin, so the camera scale never jumps on unlock; only tile art changes (deck, hatch with padlock and digit, outline foundation, flat foundation).
- Sorting layers `WorldBase`, `WorldDeck`, `WorldLinks`, `Default`, `Overlay`, `Effects`, created in P1 (T513, implemented) **through Unity tooling** (TagManager, settings-noise exception recorded). A guard test is needed because `SortingLayer.NameToID` silently returns 0 for a missing layer.
- Props only in the margin ring and on empty or locked cells, at most 24 art px tall, deterministic cell-hash rule weighted per stage; they always draw under machines. One world atlas plus one FX atlas; the deck replaces the F004 floor pool. (S056)

## Depot, trunks, branches, vein (R07, R23)

- One static **trunk** per row in the 19 px row gap toward the depot; each occupied location has a **branch** to its trunk. Pips run only on branches while the machine is Running, so each pip belongs to one machine; no pip is a unit count and there are never machine-to-machine links. Reduced motion: no pips, static active tile.
- Depot: 4 fill states x 3 stage variants = 12 sprites.
- Ore-vein marker on `DepositCell` (margin cell from `WorldCompositionSettings`): 6-frame chip loop while manual mining runs, static highlight under reduced motion. Not a hit target; no pacing change. (S056)

## CompletedCycles (R08, implemented in T514) and shortage signal (R09, planned)

- The simulator is stateless across advances, so it emits a per-advance count in `CompletionPass`, **+1 per completed cycle** (never the batch factor), exposed only after a successful commit. `RunSession` owns a session-only `long` per `InstanceId` (pruned on removal, reset on replace, empty on construction/restore/prestige) and surfaces it as `MachineStateView.CompletedCycles` via a defaulted constructor parameter. **Not saved**; no economy code reads it. Offline completions are not deltas. Tests: faulted advance, multi-completion, no-observer-effect SHA, property test against trace, public-API fixture diff.
- "Short on <resource>" ranking: resources blocking at least one MissingInput machine, filtered to `HasDemand`, ordered by `SupplyCoveragePpm` ascending, then affected count descending, then content order. No MissingInput machine means no signal. The hub dot shows system attention only (Contracts, Research, Prestige, Overdrive). (S056)

## ShellTab shim migration (R11)

Extract `ShellNavigationStack` and `ShellSheetHost` from the 2652-line `RunShellDocument` as a pure move; keep `ShellTab` as an internal shim (209 references in 45 files) mapping `SelectTab` to hub plus system view; remove the tab bar from UXML in P3; re-baseline tests per file. Stack frames: Dialog, PinnedValue, Card/BuildCard/Details, Hub/SystemView/Settings. The machine card is docked (band or side column), never floating. (S056; T511 extracted the stack and host, S058) See [run shell and panels](./run-shell-and-panels.md), [pixel camera scale](./pixel-camera-scale.md), [plan overview](./f006-plan-overview.md).

## P0 constraints on the camera and card

OD-9b adopted the UI/UX conditions as binding: the camera centres the world in the area the chrome leaves free, never clips a machine column and keeps the selected machine 100 % visible; 640x360 at 200 % text needs a compact card or bottom sheet; the hub and the card are exclusive. Evidence: [direction decision](./f006-p0-direction-decision.md), [baseline](./f006-p0-baseline.md). (S057)
