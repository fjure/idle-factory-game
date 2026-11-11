# Contract: World Presentation (F006)

**Revision**: 2 (plan reviews, 2026-11-11).
**Scope**: world layout and composition; deck and environment; depot, links and deposit; development growth; art families and budgets; direction and acceptance evidence; World Bible conformance.
**Requirements**: FR-004, FR-010a, FR-011 to FR-015, FR-037 to FR-044, FR-046, FR-047.
**Research**: R02 to R07, R15, R17, R18, R22, R23.

## 1. Deck and environment (FR-011, FR-012, FR-014, FR-015)

**Grid.** Rectangle `Grid` with cells of 70×54 art px (70/24 by 54/24 world units). Cell origins sit on whole art pixels. The grid is not an isometric Tilemap.

**Sorting layers** (created through Unity tooling in P1):

`WorldBase` < `WorldDeck` < `WorldLinks` < `Default` (foundations and machines) < `Overlay` (cues, markers, selection, attention) < `Effects`

A guard test fails when any of these layers is missing.

**Cells.** The deck covers all locations plus the margin band. The art differs by location state:

| Location state | Art |
|---|---|
| Locked | Hatch, keeping the §8.5 padlock and level digit |
| Empty and unlocked | Outline foundation |
| Occupied | Flat solid foundation with the same 70×35 diamond. No plinth wall. |

The camera scale never changes on unlock.

**Props.**
- Allowed only in the margin ring and on empty or locked cells.
- At most 24 art px tall.
- Placed by a deterministic cell hash with weights per stage from `WorldCompositionSettings`.
- Never inside a hit shape, a badge or marker keep-out, or a lane.
- Always drawn under machines.

**Lamps.** Static. They use steel or teal tones, never `lit_*`, and never flicker.

**Reduced motion.** No decorative pixel moves. Machines hold frame 0.

**Tests:**
- `DeckLayoutTests` (Edit Mode): cells, hatch and padlock, the prop mask with its measured free area, and the seam test for the float cell size.
- `SortingLayerGuardTests`.
- `DeckRenderTests` (Play Mode): sorting, no prop above the overlay, and material parity with the slot sprites.
- `ReducedMotionWorldTests` (Play Mode): zero moving decorative pixels over 120 frames.
- A Tilemap module check in the player and in Android IL2CPP.

## 2. Depot and links (FR-013)

**Trunks.** One static trunk per row runs in the row gap toward the depot side.

**Branches.** Each occupied location has one branch, from the plate's rear edge to its trunk. Empty and locked locations have none.

**Depot.** Sits at `DepotCell` in the margin band. It has 4 fill states (index `min(3, floor(4·fill/capacity))`, plus the existing full state) times 3 stage variants.

**Pips.**
- Shown **only on branches**, while the machine is Running.
- Travel time per lane cell by `ActivityPace`: 800, 600 or 400 ms.
- Under reduced motion, no pips are drawn; the branch shows a static active tile instead.

**Prohibitions.**
- No lane between machines.
- No pip on a trunk.
- A pip never represents a unit count.

**Tests:**
- `DepotLinkModelTests` (Edit Mode): geometry on the 3×4, 4×3, 6, 20 and L layouts; fill index; branch presence per condition; pace mapping.
- `DepotLinkViewTests` (Play Mode): a pip stops within one frame of a block.

## 2a. Ore vein (FR-010a)

**Placement.** `DepositCell` is chosen by `WorldCompositionSettings`. It is a margin cell outside hit shapes and keep-outs.

**Activity.**
- While manual mining is in progress, a 6-frame chip loop plays on the shared clock.
- Under reduced motion, a static highlight replaces the loop.

**Interaction.** The vein is not a hit target in F006.

**Economy.** Yield, rate and pacing are unchanged. Game Systems confirms this.

**Tests:**
- `DepositViewTests` (Play Mode): placement validity on every test layout, activity follows the mining state, and a tap on the vein selects nothing.

## 3. Development growth (FR-036)

Deck-lit area, edges, prop weights and the depot variant change per `DevelopmentStageValue`. All of them reset with prestige.

**Minimum delta.** Between adjacent stages the deck-lit area share differs by at least 25 percentage points, and the grayscale difference is visible at n_min.

**Tests:**
- `DevelopmentStageTests`: boundaries at 4 and 8 of 12; `t ≤ 0` gives Early; no prestige input.
- `StageTriptychDeltaTests`.

## 4. Layout independence (FR-037, FR-038, SC-011)

Camera scale, framing, pan limits, hit-testing, card and build-card docking, deck, links, deposit and cues all consume `FactoryWorldLayout`. `ScaleLayout` keys on `LayoutId`.

**Tests:**
- `WorldLayoutParityTests`: exact F004 geometry and `LargestFittingScale` parity per screen class.
- `WorldLayoutValidationTests`.
- `LayoutIndependenceTests` (Play Mode): 6, 20 and L layouts through the real view, camera and selection.
- `NoFixedSlotCountGuardTests`: a `git grep` guard on new code. It explicitly exempts the listed F004 12-slot tests.

## 5. Art families and budgets (FR-004, FR-044)

**Versioning.** Art Bible `F004-PAB v1.1.0`, a minor release.

**Families.** F006-GROUND, FOUNDATION, DEPOT (12), LINK, PROP, EDGE, DEPOSIT, FX, and UI (only if UI Toolkit cannot express it).

**Amendment list.** Sections §2.2, §5, §6.4, §6.5, §8.5, §9.4 and §12 (see R17).

**Palette.** No new palette id. `lit_*` is not used for links, pips, sparks or lamps.

**Atlases.** One world atlas holds the static families. One FX atlas holds the effects. Each atlas has at most one 1024² page.

**World-layer budget** (numeric, recorded in Art Bible §2.2):

| Item | Limit |
|---|---|
| Tilemap renderers | ≤ 3 (base, deck, links) |
| Visible tiles at fit-all | ≤ (columns + 2·margin) × (rows + 2·margin) × 2 |
| World sprite renderers | ≤ 1 per branch pip + 8 FX + props ≤ 0.5 per eligible cell + 1 depot + 1 deposit |
| SetPass | +1 for the world atlas, net of the removed floor atlas; +1 for FX while active |
| Texture | Taken from the sprite-by-sprite table with padding and page rounding |

**A-03 anchor.** The OD-12 owner decision chooses among measured candidates.

**Process.** Each family follows these steps:
1. A brief that cites World Bible v0.1 and the Art Bible version.
2. Its route: Blender rig-v2 for ground, hatch, edge, prop, depot and deposit; scripted generation for lanes, pips and FX.
3. A deterministic pixel pass.
4. `scripts/test-art-pipeline.ps1`, which must exit 0.
5. A provenance record.
6. Art Director review in a fresh session.
7. World Designer conformance review.
8. Technical Art import.

**Tests:**
- `F006AssetContractTests`: import settings, PPU 24, point filtering, atlas membership, catalog keys, sprite counts.
- The art-pipeline checks.

## 6. Direction and acceptance evidence (FR-040 to FR-043, FR-046)

| Artifact | Content | Gate |
|---|---|---|
| `reviews/baseline/` + `baseline.md` | Screenshot matrix and probe JSON (R02) | Before P1 |
| `reviews/direction.md` + `direction/` | At least 2 directions × (portrait, landscape, 640×360 at 200 %, tablet); interaction states; stage triptych; animation strips; reduced-motion still; comparison and recommendation; OD-9 | Before P2/P3 production |
| `reviews/readability-f006.md` | SC-005 (incl. §9.4) and SC-004 with seeds and oracles | After P2; final after P3 |
| `reviews/final-acceptance.md` | Before/after sheets, guidance measurements against the baseline, owner verdicts per surface, the A-9 limitation | Completion |

## 7. World Bible conformance (FR-047, SC-013)

**`docs/world/world-bible-v0.1.md`.** Approved by the owner with OD-9 and frozen before the direction briefs.

**`reviews/world-copy.md`.** Lists every world-facing string with its owner, the World Bible version it follows, and a length check at 200 % text. The strings covered are:
- the hub label;
- the milestone banners;
- the stage names;
- the site name;
- the depot and vein names, if shown.

**`reviews/world-bible-conformance.md`.** The World Designer checklist, run after P2 and at P4. It must report zero conflicts and no unowned string.
