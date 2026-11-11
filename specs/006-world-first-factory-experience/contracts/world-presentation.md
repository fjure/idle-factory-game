# Contract: World Presentation (F006)

**Scope**: The world layout, the deck and environment, the depot and its links, the development growth, the art families, and the direction and acceptance evidence.
**Requirements**: FR-004, FR-011 to FR-015, FR-037 to FR-044, FR-046.
**Research**: R02 to R07, R15, R17, R18.

## 1. Deck and environment (FR-011, FR-012, FR-014, FR-015)

**Layering.** Sorting layers, from back to front:

`WorldBase` < `WorldDeck` < `WorldLinks` < the existing slot layers (foundation, machine) < `Overlay` (cues, markers, selection, attention markers) < `Effects`.

Decoration (props, lamps and edges) sits on `WorldDeck` or `WorldLinks`, never above `Overlay`.

**Location conditions.** A locked location shows a hatch tile. An empty unlocked location shows the deck with an outline foundation. An occupied location shows a solid foundation under the machine. Each condition differs in shape: hatch pattern, outline, or solid plinth.

**Prop placement.** Props are placed by a deterministic cell-hash rule from the layout. A prop never falls inside a badge or marker keep-out box (Art Bible §3.4), on a link lane, or inside a hit shape.

**Reduced motion.** With reduced motion on, decorative motion stops: lamp flicker, pips and effects. Machines show frame 0.

**Tests**:
- `DeckLayoutTests` (Edit Mode): cells, hatch versus unlocked, prop keep-out.
- `DeckRenderTests` (Play Mode): sorting order, no prop over an overlay.
- `ReducedMotionWorldTests` (Play Mode): zero moving decorative pixels across 120 frames.

## 2. Depot and links (FR-013)

**Depot.** The depot sits at `DepotAnchor` and has 4 fill states. The state index is `min(3, floor(4 × fill / capacity))`, and "full" applies when the store is full under the existing rules.

**Links.**
- Every **occupied** location shows a link along `LinkPathOf`. An empty or locked location shows none.
- A link shows activity with a pip while its machine phase is Running.
- The pip's travel time per lane cell comes from the `ActivityPace` bucket: 800, 600 or 400 ms.
- With reduced motion on, an active link shows the static active-variant tile.

**Constraints.**
- There are never links between machines.
- A pip never represents unit counts.

**Tests**:
- `DepotLinkModelTests` (Edit Mode): fill index, link presence per condition, pace mapping.
- `DepotLinkViewTests` (Play Mode): the pip stops within one frame of a block.

## 3. Development growth (FR-036)

The deck extent equals the cells of the unlocked locations plus the margin. Edge structures, prop density, lamp count and the depot variant are selected per `DevelopmentStageValue` (data-model).

Prestige resets these with the run.

**Tests**:
- `DevelopmentStageTests` (Edit Mode): boundaries at 1/3 and 2/3, clamps, no prestige input.
- Stage captures for SC-006.

## 4. Layout independence (FR-037, FR-038, SC-011)

The camera fit-all, the pan limits, hit-testing, the card focus, the deck, the links and the cues all consume `FactoryWorldLayout`.

**Prohibition.** No new code may reference a fixed count of 12 or a fixed column count. A `git grep` guard test checks this, analogous to the F004 single-camera-writer guard.

**Tests**:
- `WorldLayoutParityTests` (Edit Mode): exact parity with the F004 lattice.
- `WorldLayoutValidationTests` (Edit Mode): every validation rule in the data-model.
- `LayoutIndependenceTests` (Play Mode): the 6-location, 20-location and L-shaped test layouts through the real view, camera and selection.
- `NoFixedSlotCountGuardTests`.

## 5. Art families (FR-004, FR-044)

This section requires Art Bible `F004-PAB v1.1.0` (minor) with these families: F006-GROUND, F006-FOUNDATION, F006-DEPOT, F006-LINK, F006-PROP, F006-EDGE, F006-FX, and F006-UI if needed.

**Palette.** No new palette id. Family allowances go in §5. A world-layer budget is added to §2.2.

**A-03 fix.** The badge anchor moves within the plate's own row band.

**Process.** Every family has a brief, a route, the deterministic pixel pass, passing checks (`scripts/test-art-pipeline.ps1`), provenance, an Art Director review in a fresh session, and a Technical Art import.

**Atlas budgets.** Pages are at most 1024². Texture growth is at most 1.5 MB in total.

**Tests**:
- `F006AssetContractTests` (Edit Mode): import settings, PPU 24, point filtering, atlas membership, catalog keys.
- The art-pipeline checks.

## 6. Direction and acceptance evidence (FR-040 to FR-043, FR-046)

| Artifact | Content | Gate |
|---|---|---|
| `reviews/baseline.md` | Current-build screenshot matrix and the geometry, interaction and label probes (R02) | Before P1 |
| `reviews/direction.md` + `reviews/direction/*.png` | At least 2 directions × 3 screen classes, interaction states, the stage triptych, a 200 % variant, a comparison and a recommendation; the owner decision (OD-9) | Before P2 and P3 production |
| `reviews/readability-f006.md` | SC-005 blind naming and the timed trial on the new backdrop; the SC-004 trial | After P2; final after P3 |
| `reviews/final-acceptance.md` | Before/after sheets per class and state; guidance measurements against the baseline; an owner verdict per surface; the known limitation A-9 | Feature completion |
