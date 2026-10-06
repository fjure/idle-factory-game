# Art Direction Review (Feature 003)

## Art Bible v0.1 approval

**Task**: T091, Issue #173. Independent Art Director review with the Game Designer / Product Owner lens. Covers FR-034 and research R15.

**Reviewer**: MAQA worker session `issue:173:branch:maqa/issue-173-art-bible-approval`, a session separate from the T090 author (Issue #172). This session made no edit to the reviewed artifacts and produced no assets.

### Verdict

**APPROVED WITH CONDITIONS.** Art Bible `F003-PAB v0.1` and brief `F003-AB v0.1` are frozen at the commits below. No asset production (T108) and no Technical Art integration (T111) starts on any other version. Condition C1 to C6 (section "Conditions") are carried by named later tasks; none of them blocks T108. One item (C3, Tier II/III Item names) is an owner confirmation request that must be settled or explicitly accepted before T111 creates GUIDs.

### Frozen version and reviewed commits

| Artifact | Frozen identifier | Reviewed commit (`git log -1 --format=%H -- <file>`) |
|---|---|---|
| `docs/art/F003-provisional-art-bible-v0.1.md` | `F003-PAB v0.1` | `c7e34a9c23dab5ff3298eee6b545821de9650752` |
| `docs/art/F003-asset-briefs-v0.1.md` | `F003-AB v0.1` | `c7e34a9c23dab5ff3298eee6b545821de9650752` |

Review baseline: `origin/main` `7855fae`. Any content change to either file after these commits needs a new review record (a new Art Bible version if a rule, hex value, size, ID or row changes). The "Status" lines inside both files still say "not approved until T091"; this record supersedes them, and updating those two status lines is a separate housekeeping edit that the parent or the T090 author may make without changing content (this review was forbidden to edit `docs/art/**`).

### Checked brief rows

All 78 rows were checked against the Art Bible: IDs `F003-SLOT-*` (7), `F003-MACHINE-*` (4), `F003-BADGE-*` (6), `F003-MARKER-*` (2), `F003-RES-*` (4), `F003-ITEM-*` (6: 3 Item icons and 3 tier chips) and `F003-UI-ICON-*` (49), inventory rows 1 to 78 in order with no gaps. Machine, badge, marker, resource, Item and tier-chip rows (rows 8 to 29) were read one by one against sections 3 to 7. UI icon rows 30 to 78 were checked for uniqueness of ID, slug, size, pivot, tool, provenance and for coverage of every surface in `contracts/ui-surfaces.md` (cross-check table); their glyph descriptions are text, and T109 compares each drawn glyph with its row.

### Independent recomputation (spot checks; the Test/QA pass had covered the full set)

Reproduced in this session with Python: (1) row count 78, 78 unique IDs, sequential numbering, exactly 11 rows marked `*`; (2) WCAG ratios Overdrive marker on ink 8.37, padlock on plate 5.72, Smelter fill vs backing 3.67, primary text on backing 14.33 (all as documented); (3) machine mask areas 5,120 / 6,400 / 6,400 / 4,964 px and all six IoU pairs at full resolution and 32x32 (0.452, 0.491, 0.611, 0.575, 0.501, 0.382 and 0.452, 0.488, 0.612, 0.575, 0.505, 0.380), maximum 0.612 against the 0.70 limit; (4) fit-all ppc for 360x640 (45.0) and rotated 640x360 (32.0), the worst case, above the 24 ppc floor.

### Style and readability judgment (Art Director lens)

1. **Coherence with F001**: inherited unchanged (top-down orthographic, 64 px per cell, 64 PPU, `T1` SVG rasterized by Inkscape 1.4.2, `P1` provenance, point-filtered lossless import). The F003 style is a strict flat geometric extension (square outer corners, 4 px outlines on machines and plates, 2 px on small sprites, one flat fill plus at most three interior primitives). No gradient, texture, lighting or lore is introduced. Approved.
2. **Silhouette distinctness at the smallest zoom**: the four machines share no dominant feature (downward point, chimney, H-beam, toothed circle), the IoU maximum is 0.612 both at 128x128 and on the 32x32 (Z16) downsample. The header band (32 px) is disjoint from the art region, so a badge never removes a dominant feature at Z16 beyond the 4 projected pixel overlap that section 6.5 already accounts for. Approved.
3. **Slot family**: empty (open brackets and plus), locked (hatched plate, padlock and `L2` to `L5`), occupied (plate with a machine) and selected (double outline) differ by shape in grayscale. Approved.
4. **Noise limits for 12 slots on a phone**: at most 61 slot sprites, one badge and two markers per slot, markers hidden below 24 ppc, worst-case fit-all 32 ppc (phone landscape) leaves both markers and the locked digit on screen. Realistic. Approved.
5. **Tier-chip pip legibility**: the frame shape (circle, square, triangle), the pip count and the numeral are triple redundant, so a tier does not depend on pip size alone. The Art Bible states no pixel size for a pip. Accepted with condition C6.
6. **State readability (ART-2)**: each of the six badges has a unique glyph silhouette and a distinct text label of at most four letters; markers are glyph-only with the effect named in the machine panel. The precedence table is ordered by blocker kind and is deterministic (rank 1 to 6, first match wins), so a state combination cannot select two badges. The grayscale and CVD analysis honestly states that RUN/FULL cannot be separated by grayscale and WAIT/FULL by color under protanopia (2.1), and makes glyph plus label mandatory, with a zero-confusion pass rule for T109 and T129. This is the correct response, not a defect. Approved.
7. **UI tokens (ART-4)**: palette roles, focus ring (2 pt ink plus 2 pt outer light, always visible), 12 pt minimum text, disabled look with visible reason, and token contrast are stated with ratios, and each disallowed pair (muted and danger on surface-raised) is called out. Approved.

### Game Designer / Product Owner check: gameplay meaning per visual state

Authority: `contracts/ui-surfaces.md` (Slot grid, Overdrive control, Research, Items, Contracts, Startup rows), `contracts/run-application.md` (machine state read model: slot, type, level, active flag, running/paused/waiting, blocker kind, batch factor, progress ppm; `OverdriveStatus`), `data-model.md` (waiting = `k' = 0`, progress equals the cycle duration), `spec.md` FR-008, FR-019.

| Visual state or element | Meaning carried | Verdict |
|---|---|---|
| Slot empty (brackets, plus) | Buildable fixed slot; fresh run shows 4 with the "Build a Miner" hint (`F003-SLOT-EMPTY`, `F003-UI-ICON-BUILD`) | Correct |
| Slot locked `L2` to `L5` | The factory level that unlocks it, with a shape cue (padlock). Level 2 to 5 unlock slots 5-6, 7-8, 9-10, 11-12, matching FR-008 (six/eight/ten/twelve) and the plan's 4 to 12 slots | Correct |
| Slot occupied (plate plus machine) | One machine per slot; duplicate types allowed; slot position has no economic effect (nothing in the art implies position value) | Correct |
| Slot selected (double outline) | Selection for the build sheet or machine panel; not a badge or a marker | Correct |
| Badge OFF (`Inactive`) | Active flag off: the player has turned the machine off. Rank 1 so the deliberate player action is never hidden by a derived blocker | Correct |
| Badge IN! (`MissingInput`) | Missing input; the panel adds the corrective hint and, if auto-sell causes it, the toggle (UX-10). The badge does not claim the cause | Correct |
| Badge FULL (`StorageFull`) | Store cannot fit even one recipe unit (run-application: `StorageFull` applies only when not even one fits); partial batches do not show FULL | Correct |
| Badge WAIT | Progress equals the cycle duration, completed count 0 (waiting). See open point (a) | Correct, see (a) |
| Badge PAUS | Paused with no blocker kind. See (a) | Correct, see (a) |
| Badge RUN | Running | Correct |
| Marker boosted | At least one active Item effect targets this machine; Research and Industry are permanent and never markers (matches FR-018/FR-029: temporary effects only) | Correct |
| Marker Overdrive | Factory Overdrive active (FR-019 "both states are visible"; the HUD control shows the remaining time). See (b) | Correct, see (b) |
| Overdrive control icons ready / active / cooldown / locked | The four `OverdriveStatus` states plus the content unlock; remaining time is UI text, so the icon never carries a number | Correct |
| Research node icons locked / available / owned / maxed | Missing prerequisite, available, purchased and upgradable, maximum level; covers the "missing prerequisite or RP, with the reason" rows | Correct |
| Item icons and chips, Tier I, II, III | Tier by frame shape, pips and numeral; Item-effect targets and countdown are UI text | Correct, see (c) and C6 |
| Contract kinds (Plate, Gear, timed Bar) | Resource icon plus `F003-UI-ICON-TIMER`, `-ACCEPT`, `-DELIVER`, `-ABANDON`; Abandon absent for a timed contract is a control state, not art | Correct |
| Disabled and rejected controls | Token look plus `F003-UI-ICON-LOCK` and `-WARNING`, with the reason visible (Global "Rejections", "Affordability") | Correct |
| Startup, import, save-in-progress, offline summary, empty states | `IMPORT`, `SPINNER`, `RETRY`, `WARNING`, `OFFLINE`; empty states are text only | Correct |

No badge or marker implies a logistics port, belt, power state or balance promise (brief section "Production contract" and "Prohibited scope"). Approved for player-visible meaning.

### Decisions on the open points raised by QA

| Point | Decision | Reason | Blocks T108? |
|---|---|---|---|
| (a) AD-3: WAIT and PAUS reachable only with an empty blocker kind | **Accept with condition C1** | The data model defines waiting as `k' = 0` at the completion instant, and the run predicate pauses on inactive, missing input or full store. A machine is therefore almost always waiting or paused with a blocker kind, and the more specific IN!, FULL or OFF badge correctly wins. WAIT and PAUS remain as defensive states (first evaluation, an ordering gap). The mapping does not mislead the player, the panel lists every blocker, and there are exactly six badges with rank order fixed. Producing the two art rows costs little | No |
| (b) Overdrive marker on every occupied slot, including OFF slots | **Accept with condition C2** | Overdrive is factory-wide (FR-019), so the marker says "factory Overdrive is active", not "this machine is faster". On an OFF slot the OFF badge already says the machine is not producing. A per-slot suppression rule can be changed in presentation without changing any asset or ID | No |
| (c) 8 new presentation keys and the invented Item names "Flux Catalyst" and "Overclock Core" | **Accept the keys and the pattern; owner confirmation requested for the two names (C3)** | The contract names only `F003-RES-IRON-ORE`, `F003-MACHINE-MINER`, `F003-ITEM-DIAMOND-DRILL-HEAD`. The resource keys for Bar, Plate and Gear and the four machine keys follow the contract's pattern and are needed (FR-029, spec resources/machines). The Tier II and III Item names and their effects belong to product and economy content (T097, ECO/GD), not to art; they are provisional Art Director proposals and must not be treated as decided or as a balance promise. Assets are not balance carriers | No. Must be settled, or explicitly accepted as final for v0.1, before T111 creates GUIDs and `.meta` files. A name change before T111 costs a brief revision and renaming of two row slugs |
| (d) No `presentationKey` for research, Industry, contract entries | **Accept with condition C4** | The content contract fixes `presentationKey` on resources, machines and Items only; ART-3 asks for an ID for every `presentationKey` and every UI icon, which the brief provides (11 keys plus 67 other rows). Choosing icons by effect kind, contract resource and control state is consistent with the contract and keeps content data free of art keys | No |
| (e) Layout planning values (360 pt side panel, 48 and 56 pt bars) | **Accept with condition C5** | They are labelled planning values, used only to prove the 24 ppc floor is met with margin (worst case 32.0 ppc on phone landscape, 8 ppc above the floor; tablet portrait 51.0 uses the 360 pt panel). Real dimensions belong to T112 (UX-1, UX-3) and are re-checked there | No |

### Conditions

| ID | Condition | Owning task | Blocks T108 |
|---|---|---|---|
| C1 | The slot presentation must map the run-application machine state (`active`, running/paused/waiting, blocker kind) to exactly one badge by the section 6.2 order, with a test that enumerates every read-model combination and asserts a single badge, and that documents which combinations are unreachable. If WAIT or PAUS turns out to be unreachable in the real read model, the assets stay (defensive) and the finding is recorded; no third badge and no second badge per slot without a new Art Bible version | T112 (slot grid), verified again by T129 | No |
| C2 | The Overdrive marker keeps the meaning "factory Overdrive is active": the machine panel and any legend say so, the marker's accessible name is the factory-level state, and T112 may suppress it on an Inactive (OFF) slot without a version change. The boosted marker follows the same rule for an Item effect on an inactive machine | T112, T113 (machine panel) | No |
| C3 | **Owner confirmation requested**: the names "Flux Catalyst" (Tier II) and "Overclock Core" (Tier III), and their effects, are provisional Art Director proposals. The owner confirms them, or supplies other names, before T111 creates GUIDs. T097 must write exactly the 11 keys of the brief. Any rename revises the brief (`F003-AB v0.2`) and the affected row slugs first, and a renamed key must reach content and the catalog together | Owner, T097 (content), T111 | No (blocks T111) |
| C4 | Presentation code that selects icons for research, Industry, contracts, effects and control states uses explicit mapping tables. A test asserts that every mapped icon ID resolves to a catalog row of the brief, and that every `presentationKey` of content version 2 resolves (the start-up check `presentation.key.unresolved`) | T097 (key list test), T111 (catalog), T115 to T118 (panels) | No |
| C5 | T112 owns the real top strip, tab bar and side-panel dimensions, re-runs the section 8.2 fit-all table with them, and keeps every supported screen class, including rotations, at or above 24 ppc with the selected slot in view above a 60 % sheet. If a real dimension drops a class below 24 ppc, the fix is a layout change or a new Art Bible version, never a silent zoom floor change | T112 (`SlotGridTests`, `ZoomReadabilityTests`) | No |
| C6 | Tier-chip and Item-icon legibility: pips have a diameter of at least 8 px and a gap of at least 4 px, the numeral cap height is at least 12 px, all still meet the 2 px minimum stroke and gap. T109 measures them; Items lists also show the tier in UI text (for example "Tier II") so a 32 pt icon is never the only tier cue. T109 and T129 run the section 8.4 grayscale and CVD pass on the tier chips as well as the badges | T108 (production), T109 (source review), T115 (Items panel) | No |

### Gate statement

- T108 (Asset Artist source production) may start on the frozen `F003-PAB v0.1` and `F003-AB v0.1`, writing only under `Assets/IdleFactory/Art/Development/F003/Source/` with `PROVENANCE.md`, method `T1`, no generator, no substitute tool, no unlisted asset.
- T111 (Technical Art) may start after T109 passes and after C3 is settled.
- T129 reviewers must be sessions different from the T090 and T111 authors (ART-5).
- Constitution and AGENTS.md rules checked: role independence, provisional development-only content, provenance and no-silent-substitution (ART-6), no Unity or emulator use in this review.

### Not verified in this review

- No asset exists yet, so no rendered contrast, silhouette, grayscale or CVD result was inspected; those are T109 and T129. This review verified rules, IDs, arithmetic and meaning.
- The colour-vision and grayscale figures in Art Bible section 8.4 were read but not recomputed.

## Source assets

**Task**: T109, Issue #191. Independent Art Director review of the 78 SVG sources produced by T108 (Issue #190, merged at `ea022a8`, reviewed here at `origin/main` `b3ffa2e`) against `F003-PAB v0.1` and `F003-AB v0.1`. Covers FR-034 and ART-1 (measurable readability), ART-2 (caps, precedence, cues), ART-4 (tokens, where an asset consumes them) and ART-6 (tool, provenance, no substitution).

**Reviewer**: MAQA worker session `issue:191:branch:maqa/issue-191-art-source-review` (Art Director lens, Sonnet-class model). It did not author T090, T108 or any source, and it changed no asset, brief or Art Bible. The "Art Bible v0.1 approval" section above is unchanged. These are development placeholders that Feature 004 replaces (owner note on Issue #190); they are judged against the Bible and the briefs exactly, with no demand for polish beyond them.

### Result

| Verdict | Count | Assets |
|---|---:|---|
| APPROVED | 59 | all rows not listed below |
| APPROVED WITH NOTE | 12 | `F003-MARKER-BOOSTED`, `F003-RES-IRON-ORE`, `F003-RES-GEAR`, `F003-ITEM-TIER-1`, `-TIER-2`, `-TIER-3`, `F003-UI-ICON-EFFECT-OUTPUT`, `-EFFECT-UPGRADE-COST`, `-INDUSTRY`, `-WARNING`, `-OVERDRIVE-READY`, `-OVERDRIVE-LOCKED` |
| REJECTED (return to T108) | 7 | `F003-ITEM-DIAMOND-DRILL-HEAD`, `F003-ITEM-FLUX-CATALYST`, `F003-ITEM-OVERCLOCK-CORE` (condition C6, pips), `F003-UI-ICON-AUTOSELL`, `F003-UI-ICON-RETRY`, `F003-UI-ICON-RESEARCH-NODE-OWNED`, `F003-UI-ICON-OVERDRIVE-COOLDOWN` (minimum gap 2 px) |

Every reject is a measured violation of a numeric Art Bible or approval-condition rule with a small, precise fix. No style change is requested or allowed: each rejected asset keeps its ID, slug, size, cue and palette.

### Method and tools

- **Rasterization**: Inkscape `1.4.2 (f4327f4, 2025-05-13)` on Windows 11 x64 (the version fixed by ART-6), command as in the brief, run on all 78 sources at 1x with transparent background; the 71 sprites other than slot bases were also rendered at 4x, and 11 gap-critical icons at 8x. All renders stayed in the session scratch directory; nothing was added to the repository.
- **Analysis**: Python 3.13.6 with Pillow 12.3.0 (no numpy): SVG parsing (forbidden elements, `viewBox`, palette hexes, stroke widths, coordinate parity and fractions), alpha-mask measurements, WCAG 2.x relative-luminance ratios, Rec. 709 grayscale, Machado (2009, severity 1.0) protan/deutan/tritan matrices in linear RGB, CIE76 distance, 128x128 and 32x32 silhouette IoU, connected-component gap measurement.
- **Visual pass**: contact sheets of the 16 tiles (6 badges, 2 markers, 4 slot bases, 4 machines, with the selected frame on an occupied plate) at Z16, Z24 and Z32, shuffled, in normal, grayscale, protan, deutan and tritan simulation, plus 4x sheets of every family (resources, items, 49 icons) compared with the brief cue text. A numeric shape-distance proxy was also computed but is not used for any verdict.
- **Measurement tolerance**: sub-pixel gaps come from raster measurement and carry an uncertainty of about 0.25 px at 4x and 0.15 px at 8x. A gap under 1.5 px is treated as a violation, a gap from 1.5 px to 2 px as within tolerance and recorded as a note.

### Source-level checks (all 78)

| Check | Result |
|---|---|
| Inventory | 78 sources, 78 `.meta`, 78 provenance rows; IDs, slugs and sizes equal the brief (QA of T108 confirmed; re-verified by the sizes check) |
| SVG rules | all `version="1.1"`, `viewBox` equal to the width and height of the brief, no `image`, `use`, `filter`, `mask`, `script`, animation, `text`, `style`, gradient, pattern or `clipPath`; labels are vector paths |
| Palette | every hex is in Art Bible sections 3 and 9, plus `#FFFFFF` for icons (0 off-palette values) |
| Strokes | machines and slot plates 4 px; badges, markers, resources, chips and item frames 2 px; icons 4 px (8 px hammer head); none thinner than 2 px |
| Provenance (ART-6) | method `T1`, provenance `P1`, Inkscape 1.4.2 disclosed, language-model assistance disclosed, no generator, stock art, raster, font or external icon used; matches the brief |
| Ports and power | no source implies a port, belt, cable, socket or power state |
| Crispness | the axis-aligned sprites (slots, press, smelter, badges, markers, plate, tier-2 chip) render with 0 partially transparent pixels, so every outer edge lies on a pixel boundary; machine bounding boxes are x24..104, 16..112, 16..112, 8..120 and y36..124, all inside the art region and clear of the header band |

### ART-1 measurements recomputed

**Contrast (WCAG)**: all 11 badge, marker and tier-chip ink-on-fill ratios and fill-versus-backing ratios of Art Bible 3.2 reproduce to two decimals (minimum 6.70, Tier I). The machine and resource ratios of 3.3 reproduce (Smelter 3.67, Ore 3.53 lowest), as do the slot values (outline 3.73, padlock 5.72, digit 11.86, brackets 6.92) and the selection frame (8.28 and 6.85). New figures that the Bible did not state: on the occupied plate `#1F2937` the machines are Miner 5.00, Smelter 3.04, Press 5.77, Assembler 5.56; Iron Ore on `--surface` is 2.92 (the only non-text graphic below 3:1, recorded as a note on that asset); the tier numeral `#E5E7EB` is 14.33 on `--bg`, 11.86 on `--surface`, 8.33 on `--surface-raised` and 1.73 on the accent fill.

**Grayscale and CVD (Art Bible 8.4)**: badge fill luminance (RUN 0.553, WAIT 0.477, IN! 0.579, FULL 0.547, PAUS 0.360, OFF 0.657) and the smallest pairwise CIE76 distance between the six badge fills (19.2 normal PAUS/OFF, 2.1 protan WAIT/FULL, 12.3 deutan WAIT/FULL, 12.2 tritan WAIT/PAUS) reproduce exactly. WAIT and FULL are indistinguishable by color under protanopia, so the glyph plus label carry them, as the Bible requires.

**Silhouette uniqueness (Art Bible 4)**, measured on the rendered masks (alpha at least 128):

| Pair | IoU 128x128 | IoU 32x32 (4x4 block filled at 8 of 16) | Bible |
|---|---:|---:|---|
| Miner / Smelter | 0.451 | 0.452 | 0.452 |
| Miner / Press | 0.492 | 0.488 | 0.491 / 0.488 |
| Miner / Assembler | 0.610 | 0.612 | 0.611 / 0.612 |
| Smelter / Press | 0.575 | 0.575 | 0.575 |
| Smelter / Assembler | 0.502 | 0.505 | 0.501 / 0.505 |
| Press / Assembler | 0.382 | 0.380 | 0.382 / 0.380 |

Maximum 0.612, limit 0.70. Mask areas are 5,114, 6,400, 6,400 and 4,966 px against 5,120, 6,400, 6,400 and 4,964 in the Bible (within 0.1 %).

**Pixels per cell per zoom step** (projected points, source px times ppc / 64): Z16 slot 32 pt, badge at its minimum 27x12 pt (scale 0.375, 31.6 % of the slot, under the 35 % cap); Z24 slot 48 pt, badge 27x12 pt (14.1 %), markers 10.5 pt, locked digit cap 9.0 pt (24 px x 0.375); Z32 slot 64 pt, badge 36x16 pt (14.1 %), badge label cap 6.0 pt; Z64 slot 128 pt, natural sizes. The fit-all table recomputes with a worst case of 32.0 ppc (phone landscape 640x256 world area), above the 24 ppc floor.

**Grayscale and CVD shuffled-sheet pass (Art Bible 8.4)**: the 16-tile sheet was rendered at Z16, Z24 and Z32 in grayscale, protanopia, deuteranopia and tritanopia and shuffled. This reviewer inspected the shuffled sheets at Z16 (grayscale), Z24 (grayscale and protanopia) and Z32 (tritanopia) and named every tile with zero confusions, including RUN versus FULL (play triangle versus box glyph, labels `RUN` and `FULL`) and WAIT versus FULL under protanopia (hourglass versus box). The remaining zoom and simulation combinations were rendered but not inspected tile by tile; they use the same shapes and differ only in color. The Bible asks for two reviewers; only one independent reviewer was available to this session, so the second-reviewer repetition is carried to T129 on the integrated scene (see the gate statement). No confusion was found, so nothing is blocked on this check.

**Noise limits (Art Bible 8.5)**: interior ink primitives per machine 2, 2, 3, 1 (limit 3); fill colors per sprite at most 4 plus ink on every sprite when counted as `fill` values (the three item icons also carry an `#E5E7EB` numeral stroke); hatch 45 degrees, 16 px pitch, 2 px, one path; no more than 3 families per resource and no more than 4 SVG elements per UI icon; label cap height 12 px on badges and chips, 24 px on the slot digit; labels at most 4 letters. Ink insets inside badges are 4 to 5 px sideways and 8 px vertically (rule 4 px).

### Decisions on the T108 QA carry-forward and the open rule readings

| ID | Decision |
|---|---|
| D1 | **C6 pips (tier chips)**: C6 says pips have a diameter of at least 8 px and a gap of at least 4 px, which the approval wrote for the 64 px Item icon shown at 32 pt (4 pt and 2 pt on screen). The 48x24 tier chip is shown 1:1 (48x24 pt) and cannot hold 8 px pips next to a frame and a 12 px numeral. This review reads C6 by displayed size: pips at least 4 pt, gaps at least 2 pt, numeral cap at least 12 px. The chips measure 4 pt pips and 3 to 4 pt gaps and pass; the Item icons measure 3 pt pips and 1 pt gaps and fail (rejected). This is a recorded interpretation of the approval condition, not a version change; the owner may overrule it. |
| D2 | **Tier numeral `#E5E7EB` on a transparent background**: accepted. The art has no fill behind the numeral, and the only legal surfaces are the dark-theme tokens (Art Bible 9: no light theme), where the numeral is 8.33 to 14.33:1. Condition for T111 and T115: tier chips and Item icons never sit on the accent fill (1.73:1) or on a light surface; the Items list also shows the tier as UI text (C6). |
| D3 | **Fractional gear-tooth and odd-integer coordinates**: odd integers appear because strokes are centered on the path (a 2 px stroke at 1 or a 4 px stroke at 2 puts the outer edge on 0 and the inner edge on an even pixel). All axis-aligned art renders with 0 partially transparent pixels and machine bounding boxes land on even coordinates, so the intent of the 2 px grid (crisp edges) holds. The only fractional coordinates are the 64 one-decimal numbers of the four 45-degree teeth in `F003-RES-GEAR`; a 45-degree square cannot lie on an integer grid. Accepted: the grid rule applies to axis-aligned geometry, 45-degree elements are exempt. |
| D4 | **Tier chip content**: content is left-aligned and spans 30, 35 and 40 px of the 48 px canvas. The canvas size is fixed by the brief and the pivot is the canvas center, so the empty right part is by design; consumers lay the chip out as a 48x24 box. Accepted. |
| D5 | **Effect-output icon**: two 16x8 slabs instead of two squares are accepted (note on the asset). Two squares would not fit under the arrow within 4 px strokes and 2 px gaps at this size, and the arrow-over-stack composition is unique in the set. |
| D6 | **Primitive counting**: the Bible counts "primitives" per sprite (resource at most 4, icon at most 6) while its own rows require a plate with four rivets and a gear with eight teeth, which exceed the limit if every sub-shape counts. This review counts drawn families (an SVG element, with repeated instances of one shape in one path such as rivets, teeth or dots counting once, and an ink underlay counting as the outline). Every resource is at most 3 families and every icon at most 4 elements. Recorded so a stricter count is never applied silently. |
| D7 | **Minimum gap**: the 2 px minimum gap (Art Bible 4 and 8.5) is applied to separate parts of one sprite, measured at 4x and 8x with the tolerance stated in the method. Gaps under 1.5 px are rejected, 1.5 to 2 px are noted. |

### Per-asset verdicts

Row numbers are those of the brief inventory. "Backing" is `#111827`, "surface" `#1F2937`, "plate" the occupied slot plate `#1F2937`, "raised" `#374151`. Icon white ratios are the white glyph against the token surfaces; the tint tokens themselves were checked in the Art Bible review.

| # | Asset ID | Verdict | Measured evidence and, for rejects, the required change |
|---:|---|---|---|
| 1 | `F003-SLOT-EMPTY` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; 4 corner brackets, legs 24 px, 4 px stroke; plus sign 32 px (x48..80), 4 px; `#94A3B8` on backing 6.92:1; no fill; distinct from every other slot in grayscale |
| 2 | `F003-SLOT-LOCKED-L2` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; plate `#1F2937`; 45-degree hatch 16 px pitch, 2 px, one path, `#374151` (decorative, cleared behind the padlock); padlock body 40x32, shackle 24 px wide, `#94A3B8` on plate 5.72:1; digit `L2` vector, cap height 24 px (9.0 pt at Z24), `#E5E7EB` on plate 11.86:1 |
| 3 | `F003-SLOT-LOCKED-L3` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; plate `#1F2937`; 45-degree hatch 16 px pitch, 2 px, one path, `#374151` (decorative, cleared behind the padlock); padlock body 40x32, shackle 24 px wide, `#94A3B8` on plate 5.72:1; digit `L3` vector, cap height 24 px (9.0 pt at Z24), `#E5E7EB` on plate 11.86:1 |
| 4 | `F003-SLOT-LOCKED-L4` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; plate `#1F2937`; 45-degree hatch 16 px pitch, 2 px, one path, `#374151` (decorative, cleared behind the padlock); padlock body 40x32, shackle 24 px wide, `#94A3B8` on plate 5.72:1; digit `L4` vector, cap height 24 px (9.0 pt at Z24), `#E5E7EB` on plate 11.86:1 |
| 5 | `F003-SLOT-LOCKED-L5` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; plate `#1F2937`; 45-degree hatch 16 px pitch, 2 px, one path, `#374151` (decorative, cleared behind the padlock); padlock body 40x32, shackle 24 px wide, `#94A3B8` on plate 5.72:1; digit `L5` vector, cap height 24 px (9.0 pt at Z24), `#E5E7EB` on plate 11.86:1 |
| 6 | `F003-SLOT-OCCUPIED-BASE` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; plate `#1F2937`, 4 px outline `#64748B` (3.73:1 vs backing, 3.08:1 vs surface, both at least 3:1); the plate is the silhouette cue vs the open empty slot |
| 7 | `F003-SLOT-SELECTED-FRAME` | **APPROVED** | 128x128 viewBox, crisp (0 partially transparent pixels), palette only; outer ring 0..4, gap 4..8, inner ring 8..12 (4/4/4 as briefed); `#38BDF8` 8.28:1 vs backing, 6.85:1 vs plate; no fill; not a badge or marker |
| 8 | `F003-MACHINE-MINER` | **APPROVED** | bbox x24..104, y36..124 (inside the art region, clear of the header band); mask area 5,114 px (31.2 %); fill `#CA8A04` 6.04:1 vs backing, 5.00:1 vs plate; 4 px outline; interior ink primitives 2 (hopper slot, drill point), all at least 8 px from the outline; largest IoU 0.612 (vs Assembler) |
| 9 | `F003-MACHINE-SMELTER` | **APPROVED** | bbox x16..112, y36..124; mask area 6,400 px (39.1 %); fill `#DC2626` 3.67:1 vs backing, 3.04:1 vs the occupied plate it sits on (0.04 above the 3:1 rule, accepted); 4 px outline; interior ink primitives 2; chimney feature survives; orthogonal edges crisp |
| 10 | `F003-MACHINE-PRESS` | **APPROVED** | bbox x16..112, y36..124; mask area 6,400 px (39.1 %); fill `#60A5FA` 6.98:1 vs backing, 5.77:1 vs plate; 4 px outline; interior ink primitives 3 (the limit), bar ink inset 4 px from the outline (the minimum); orthogonal edges crisp |
| 11 | `F003-MACHINE-ASSEMBLER` | **APPROVED** | bbox x8..120, y36..124; mask area 4,966 px (30.3 %); fill `#C084FC` 6.71:1 vs backing, 5.56:1 vs plate; ink underlay gives the 4 px outline (circle r36 plus four teeth); interior ink primitives 1 (hub hole); largest IoU 0.612 (vs Miner) |
| 12 | `F003-BADGE-RUNNING` | **APPROVED** | 72x32, 2 px ink outline, label cap height 12 px as vector path, no live text; fill `#4ADE80`, ink `#0B1020` 10.86:1; right-pointing triangle plus `RUN`; ink inset 4 px L/R, 8 px T/B |
| 13 | `F003-BADGE-WAITING` | **APPROVED** | 72x32, 2 px ink outline, label cap height 12 px as vector path, no live text; fill `#A5B4FC`, ink 9.50:1; hourglass (two triangles apex to apex) plus `WAIT`; ink inset 5/4 px L/R |
| 14 | `F003-BADGE-MISSING-INPUT` | **APPROVED** | 72x32, 2 px ink outline, label cap height 12 px as vector path, no live text; fill `#FBBF24`, ink 11.34:1; outlined down triangle plus `IN!`; ink inset 4/4 px L/R |
| 15 | `F003-BADGE-STORAGE-FULL` | **APPROVED** | 72x32, 2 px ink outline, label cap height 12 px as vector path, no live text; fill `#F0ABFC`, ink 10.76:1; box with filled top bar and fill line plus `FULL`; ink inset 4/5 px L/R |
| 16 | `F003-BADGE-PAUSED` | **APPROVED** | 72x32, 2 px ink outline, label cap height 12 px as vector path, no live text; fill `#94A3B8`, ink 7.38:1; two vertical bars plus `PAUS`; ink inset 5/4 px L/R |
| 17 | `F003-BADGE-INACTIVE` | **APPROVED** | 72x32, 2 px ink outline, label cap height 12 px as vector path, no live text; fill `#CBD5E1`, ink 12.75:1; circle with a 45-degree slash plus `OFF`; ink inset 4/5 px L/R |
| 18 | `F003-MARKER-BOOSTED` | **APPROVED WITH NOTE** | 28x28, 2 px outline, fill `#2DD4BF`, ink 10.17:1; double up-chevron, 4 px stroke; readable at 10 px (Z24) in grayscale and CVD. Note: the chevron apex miter reaches 2 px from the outline inner edge (the nominal path vertex is 4 px inset); the only inset shortfall found; 2 px meets the minimum gap, no change required |
| 19 | `F003-MARKER-OVERDRIVE` | **APPROVED** | 28x28, 2 px outline, fill `#FB923C`, ink 8.37:1; lightning bolt polygon; ink inset 7/7 px; readable at 10 px |
| 20 | `F003-RES-IRON-ORE` | **APPROVED WITH NOTE** | 48x48, 2 px ink outline, 2 triangles, fill `#B45309` 3.53:1 vs backing as in the Art Bible. Note: against `--surface` `#1F2937` it is 2.92:1 (below the 3:1 rule for non-text graphics); the frozen palette fixes the hex, so this is not a source defect. T129 and T115 to T118 must place the Ore icon on `--bg` or accept the shortfall explicitly; the ink outline and the two-triangle silhouette still separate it |
| 21 | `F003-RES-IRON-BAR` | **APPROVED** | 48x48, 2 px outline, trapezoid ingot plus highlight bar, fills `#94A3B8` and `#E2E8F0` (2 fill colors); 6.92:1 vs backing, 5.72:1 vs surface; content sits 3 px below the canvas center (bbox y17..37), trivial |
| 22 | `F003-RES-IRON-PLATE` | **APPROVED** | 48x48, 2 px outline, square plate with four 4x4 rivets, fill `#E2E8F0` 14.39:1; crisp (0 partial pixels) |
| 23 | `F003-RES-GEAR` | **APPROVED WITH NOTE** | 48x48, 2 px ink underlay outline, circle with eight square teeth and a centre hole, fill `#FACC15` 11.58:1; 3 visible primitives (body, teeth, hole). Note: the four 45-degree teeth carry one-decimal coordinates (64 fractional numbers); orthogonal teeth and all outlines are integer. A 45-degree square cannot lie on an integer grid, so the rule is read as applying to axis-aligned geometry (decision D3) |
| 24 | `F003-ITEM-DIAMOND-DRILL-HEAD` | **REJECTED** | 64x64; circle frame bronze `#D08A4E`, drill-bit triangle over a diamond, 1 pip plus `I`. Rejected: tier pips are 6x6 px (one pip), below the C6 minimum of 8 px diameter and 4 px gap (at the 0.5 display scale of a 64 px icon shown at 32 pt that is 3 pt, where C6 corresponds to 4 pt and 2 pt). The numeral cap height (12 px), frame shape, glyph and colors pass. Required change: redraw the pips as 8x8 px squares with 4 px gaps (a row of three is 32 px wide and fits x2..34 beside the numeral at x50..62), keep everything else unchanged |
| 25 | `F003-ITEM-FLUX-CATALYST` | **REJECTED** | 64x64; square frame `#CBD5E1`, flask (circle base, neck), 2 pips plus `II`. Rejected: tier pips are 6x6 px with a 2 px gap, below the C6 minimum of 8 px diameter and 4 px gap (at the 0.5 display scale of a 64 px icon shown at 32 pt that is 3 pt, where C6 corresponds to 4 pt and 2 pt). The numeral cap height (12 px), frame shape, glyph and colors pass. Required change: redraw the pips as 8x8 px squares with 4 px gaps (a row of three is 32 px wide and fits x2..34 beside the numeral at x50..62), keep everything else unchanged |
| 26 | `F003-ITEM-OVERCLOCK-CORE` | **REJECTED** | 64x64; triangle frame `#FACC15`, square core with four 45-degree spokes, 3 pips plus `III`. Rejected: tier pips are 6x6 px with 2 px gaps, below the C6 minimum of 8 px diameter and 4 px gap (at the 0.5 display scale of a 64 px icon shown at 32 pt that is 3 pt, where C6 corresponds to 4 pt and 2 pt). The numeral cap height (12 px), frame shape, glyph and colors pass. Required change: redraw the pips as 8x8 px squares with 4 px gaps (a row of three is 32 px wide and fits x2..34 beside the numeral at x50..62), keep everything else unchanged |
| 27 | `F003-ITEM-TIER-1` | **APPROVED WITH NOTE** | 48x24; 2 px outline; ink pips 4x4 px (chip is shown 1:1, so 4 pt); numeral cap height 12 px, `#E5E7EB` on transparent (decision D2); content 30 px wide of 48, left-aligned (decision D4); circle frame `#D08A4E`, ink on fill 6.70:1. Pips accepted under the display-scaled reading of C6 (decision D1) |
| 28 | `F003-ITEM-TIER-2` | **APPROVED WITH NOTE** | 48x24; 2 px outline; ink pips 4x4 px (chip is shown 1:1, so 4 pt); numeral cap height 12 px, `#E5E7EB` on transparent (decision D2); content 35 px wide of 48, left-aligned (decision D4); square frame `#CBD5E1`, ink on fill 12.75:1; pip gap 4 px. Pips accepted under decision D1 |
| 29 | `F003-ITEM-TIER-3` | **APPROVED WITH NOTE** | 48x24; 2 px outline; ink pips 4x4 px (chip is shown 1:1, so 4 pt); numeral cap height 12 px, `#E5E7EB` on transparent (decision D2); content 40 px wide of 48, left-aligned (decision D4); triangle frame `#FACC15`, ink on fill 12.36:1; pip gaps 4 px horizontal, 3 px vertical. Pips accepted under decision D1 |
| 30 | `F003-UI-ICON-CASH` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (coin: circle, vertical bar, cross bar); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 31 | `F003-UI-ICON-RP` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (flask outline with a rising dot); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 32 | `F003-UI-ICON-IP` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (sawtooth roof over four short lines); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 33 | `F003-UI-ICON-FV` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (three ascending bars); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 34 | `F003-UI-ICON-STORAGE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (open crate: rectangle with a top opening line); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 35 | `F003-UI-ICON-EXPAND-FACTORY` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (square with four outward corner arrows); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 36 | `F003-UI-ICON-TAB-FACTORY` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (block plus chimney); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 37 | `F003-UI-ICON-TAB-MARKET` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (coin over an up-right arrow); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 38 | `F003-UI-ICON-TAB-CONTRACTS` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (document with fold and check); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 39 | `F003-UI-ICON-TAB-ITEMS` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (cube with three face lines); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 40 | `F003-UI-ICON-TAB-RESEARCH` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (node graph: three circles joined by lines); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 41 | `F003-UI-ICON-TAB-PRESTIGE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (up arrow through a ring); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 42 | `F003-UI-ICON-INDUSTRY` | **APPROVED WITH NOTE** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (ring with a plus and four ticks); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px. Note: reads as a crosshair rather than a gear at small size because the teeth are 6 px ticks; the brief cue (gear with a plus) is met nominally; no change required for a placeholder |
| 43 | `F003-UI-ICON-BUILD` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (45-degree hammer T); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 44 | `F003-UI-ICON-UPGRADE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (up chevron over a bar); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 45 | `F003-UI-ICON-REMOVE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (bin with a lid line); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 46 | `F003-UI-ICON-TOGGLE-ON` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (filled pill, knob right); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 47 | `F003-UI-ICON-TOGGLE-OFF` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (outlined pill, knob left); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 48 | `F003-UI-ICON-AUTOSELL` | **REJECTED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (coin with a circular arrow). Rejected: the arrowhead (`M29 14L36 11L32 4`) is detached from the arc end (29,9) by 1.19 px, below the 2 px minimum gap (Art Bible 4 and 8.5). Required change: overlap the arrowhead with the arc end so they merge, or leave a gap of at least 2 px |
| 49 | `F003-UI-ICON-PRIORITY-UP` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (up triangle over a bar); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 50 | `F003-UI-ICON-PRIORITY-DOWN` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (down triangle over a bar); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 51 | `F003-UI-ICON-DRAG-HANDLE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (six dots in two columns); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 52 | `F003-UI-ICON-ACCEPT` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (arrow entering a document frame); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 53 | `F003-UI-ICON-DELIVER` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (crate with a right arrow); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 54 | `F003-UI-ICON-ABANDON` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (document with a diagonal strike); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 55 | `F003-UI-ICON-ACTIVATE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (play triangle in a ring); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 56 | `F003-UI-ICON-CONFIRM` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (check mark); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 57 | `F003-UI-ICON-CANCEL` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (X mark); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 58 | `F003-UI-ICON-RETRY` | **REJECTED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (circular arrow, head at the top right). Rejected: the arrowhead (`M29 14L36 11L32 4`) is detached from the arc end (29,9) by 1.19 px, below the 2 px minimum gap (Art Bible 4 and 8.5). Required change: overlap the arrowhead with the arc end so they merge, or leave a gap of at least 2 px |
| 59 | `F003-UI-ICON-CLOSE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (chevron down over a bar); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 60 | `F003-UI-ICON-INFO` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (circle with dot and bar); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 61 | `F003-UI-ICON-WARNING` | **APPROVED WITH NOTE** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (triangle with exclamation bar and dot); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts 1.85 px. Note: the 1.85 px gap (exclamation bar corner to the inner triangle edge, 1.85 px analytically) is 0.15 px below the limit, within tolerance; accepted |
| 62 | `F003-UI-ICON-LOCK` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (padlock body plus shackle); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 63 | `F003-UI-ICON-TIMER` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (clock: circle with two hands); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 64 | `F003-UI-ICON-OFFLINE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (crescent moon beside a small square); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 65 | `F003-UI-ICON-IMPORT` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (arrow entering a box); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 66 | `F003-UI-ICON-SPINNER` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (three-quarter ring); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 67 | `F003-UI-ICON-OVERDRIVE-READY` | **APPROVED WITH NOTE** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (bolt outline); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px. Note: the bolt top miter tip (apex at y4, about 32 degrees) extends above the 48 px canvas and is cut flat at row 0 (about 3 px of tip); the shape still reads as a bolt; no change required |
| 68 | `F003-UI-ICON-OVERDRIVE-ACTIVE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (filled bolt in a ring); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 69 | `F003-UI-ICON-OVERDRIVE-COOLDOWN` | **REJECTED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (bolt outline with a clock corner). Rejected: the bolt and the clock ring come within 0.80 px of each other (8x render); the bolt apex is also cut by the top canvas edge (see Overdrive-ready), below the 2 px minimum gap (Art Bible 4 and 8.5). Required change: shrink or move the clock corner (circle at 34,34, r8) so the gap to the bolt is at least 2 px, and lower the bolt apex so its miter tip stays inside the canvas |
| 70 | `F003-UI-ICON-OVERDRIVE-LOCKED` | **APPROVED WITH NOTE** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (bolt outline with a padlock corner); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px. Note: the bolt top miter tip (apex at y4, about 32 degrees) extends above the 48 px canvas and is cut flat at row 0 (about 3 px of tip); the shape still reads as a bolt; no change required |
| 71 | `F003-UI-ICON-RESEARCH-NODE-LOCKED` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (node circle with a padlock); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 72 | `F003-UI-ICON-RESEARCH-NODE-AVAILABLE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (node circle with a plus); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 73 | `F003-UI-ICON-RESEARCH-NODE-OWNED` | **REJECTED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (node circle with a check and a level tick). Rejected: the level tick (path `M18 36H30`) ends 1.01 px from the node ring (r18 ring, inner edge r16), below the 2 px minimum gap (Art Bible 4 and 8.5). Required change: shorten or raise the tick (for example `M20 34H28`) so every tick corner is at least 2 px from the ring inner edge |
| 74 | `F003-UI-ICON-RESEARCH-NODE-MAXED` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (double ring with a check); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts 2.00 px |
| 75 | `F003-UI-ICON-EFFECT-SPEED` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (double right chevron); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 76 | `F003-UI-ICON-EFFECT-OUTPUT` | **APPROVED WITH NOTE** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (up arrow over two stacked blocks); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts 2.12 px. Note (carry-forward): the stack is two 16x8 slabs (y26..34 and y36..44) instead of two squares; the cue (arrow above a stacked pair) is intact and unique in the set (decision D5) |
| 77 | `F003-UI-ICON-EFFECT-SALE-PRICE` | **APPROVED** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (price tag); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts at least 2.5 px |
| 78 | `F003-UI-ICON-EFFECT-UPGRADE-COST` | **APPROVED WITH NOTE** | 48x48, white `#FFFFFF` only, 4 px strokes (8 px hammer head), at most 4 SVG elements; cue matches brief (down arrow beside a coin); white on bg 17.74:1, surface 14.68:1, raised 10.31:1; smallest gap between separate parts 1.51 px. Note: the 1.51 px gap (coin bar corner to coin ring) is within the raster tolerance of about 0.25 px of the 2 px limit; accepted, a 6 px bar would give 2 px |

### Notes that carry to later tasks

- **N1 (T115 to T118, T129)**: place `F003-RES-IRON-ORE` on `--bg`, or record that 2.92:1 on `--surface` is accepted; check tier chips and Item icons never sit on the accent fill.
- **N2 (T111)**: re-run the contrast, grayscale and CVD checks on the rasterized PNGs (point filtering, 64 PPU) of the approved assets; the three bolt icons with clipped tips (`OVERDRIVE-READY`, `-LOCKED`, and `-COOLDOWN` after rework) should be compared with the source after rasterization.
- **N3 (T129)**: repeat the Art Bible 8.4 shuffled-sheet pass with two reviewers on the integrated scene, as the Bible requires; this review supplies one reviewer.
- **C3 is still open**: the Tier II and III Item names remain owner confirmations that block GUID creation for the three Item icons, independent of the C6 reject.

### Not verified in this review

- No Unity import, PNG output, sprite catalog or scene exists yet; everything was judged on the SVG sources and on Inkscape renders held in scratch. Import settings, pivots and sorting are T111.
- The tint tokens against the icon surfaces were not re-measured (they were reproduced in the approval review); only the white glyph against the three surface tokens was.
- Gap measurements are raster-based (tolerance in the method); the three borderline notes (`EFFECT-UPGRADE-COST`, `WARNING`, `RESEARCH-NODE-MAXED` at 2.00) may be re-measured analytically by T111 if it matters.
- Meaning of each icon was judged by shape against the brief cue text, not with players; labels, state precedence and the AD-3 mapping are unchanged from the approval.

### Gate statement (T109)

- **Integrable by T111 now (71 assets)**: all 59 APPROVED and all 12 APPROVED WITH NOTE sources, that is every source except the seven below: the 7 slot bases, 4 machines, 6 badges, 2 markers, the 4 resources, the 3 tier chips and 45 of the 49 UI icons. T111 rasterizes them with the exact brief command and Inkscape 1.4.2, applies notes N1 and N2, and records its own evidence. The rows marked with a note stay approved; the notes are conditions on use and on rasterization, not blockers.
- **Not integrable (return to T108, 7 assets)**: `F003-ITEM-DIAMOND-DRILL-HEAD`, `F003-ITEM-FLUX-CATALYST`, `F003-ITEM-OVERCLOCK-CORE` (pips 8x8 px with 4 px gaps), `F003-UI-ICON-AUTOSELL`, `F003-UI-ICON-RETRY` (arrowhead gap), `F003-UI-ICON-RESEARCH-NODE-OWNED` (tick gap), `F003-UI-ICON-OVERDRIVE-COOLDOWN` (clock gap and apex). T111 must not import them in their current form. Because the three Item icons carry three of the 11 `presentationKey` values, the full key set cannot resolve in the catalog until they are re-approved; T111 may integrate the other 8 keys, and the Item keys wait for the rework and for C3.
- **Rework and re-review**: T108 changes only the geometry named in each row (same ID, slug, size, palette, cue; no restyle). A delta review by an independent Art Director re-measures only those seven assets with the same method; it needs no new Art Bible version.
- **Open to the owner**: decision D1 (display-scaled reading of C6) and decision D6 (primitive counting) are interpretations of frozen text and are recorded here so they can be overruled; neither changes a hex, size or ID.
- No Unity or emulator was used and no lease was taken; the review touched only this file.

## Source assets — delta review

**Task**: T143, Issue #269. Independent Art Director delta review of the seven sources that T142 (Issue #268, merged in PR #270, `e0a5de6`) reworked after the T109 rejection. Reviewed at `origin/main` `6ce964c`; before-state `580cf41`. Covers FR-034, ART-1 and ART-2.

**Reviewer**: MAQA worker session `issue:269:branch:maqa/issue-269-art-delta-review` (Art Director lens, Sonnet-class model). It did not author T090, T108, T109 or T142, changed no asset, brief or Art Bible, and reproduced every number below itself. It did not take numbers from the T142 worker or the QA comment on Issue #268. Owner decisions of 2026-10-06 apply: D1 accepted (tier chips stay approved and are out of scope), C3 settled (Item names Flux Catalyst and Overclock Core kept).

### Result

| Verdict | Count | Assets |
|---|---:|---|
| APPROVED | 6 | `F003-ITEM-DIAMOND-DRILL-HEAD`, `F003-ITEM-FLUX-CATALYST`, `F003-ITEM-OVERCLOCK-CORE`, `F003-UI-ICON-AUTOSELL`, `F003-UI-ICON-RETRY`, `F003-UI-ICON-RESEARCH-NODE-OWNED` |
| APPROVED WITH NOTE | 1 | `F003-UI-ICON-OVERDRIVE-COOLDOWN` |
| REJECTED | 0 | none |

### Method

- Inkscape `1.4.2` rasterized each of the 7 before and after SVGs at 8x (`-w 512` for 64 px items, `-w 384` for 48 px icons, transparent background). Python 3.13 with Pillow 12.3.0, no numpy.
- Gap method (same as T109: alpha mask at least 128, separate parts, edge to edge): every drawable element (`path`, `rect`, `circle`, `polygon`) was rendered alone with the others removed. The gap between two parts is the minimum distance between their mask boundary pixels, minus one sub-pixel, divided by 8. Gap 0.00 means the parts touch or overlap and merge into one shape, which is allowed (T109 required an overlap or at least 2 px). Tolerance about 0.15 px at 8x, as in T109.
- Union bounding box of all parts against the canvas for clipping. Palette hex set, `width`, `height`, `viewBox`, forbidden elements and element count compared with the before state. The `git diff` of the sources was read line by line: only the listed geometry lines and the PROVENANCE rows changed, with no `metadata`, `desc`, ID or palette change.
- Readability: before and after renders on the dark surface at 8x and at the display size (items 32 pt, icons 24 pt, at 1x, then enlarged nearest-neighbour), in normal, grayscale (Rec. 709), protanopia and deuteranopia (Machado 2009 severity 1.0). The UI icons are white only, so grayscale and CVD change nothing for them.

### Measured evidence

**Item icons (C6, pips)** (pips are the only changed geometry; the T109 requirement was 8x8 px pips with 4 px gaps):

| Asset | Pips after | Pip-to-pip gap (measured) | Pip to numeral | Union bbox in 64x64 | Before |
|---|---|---:|---:|---|---|
| `F003-ITEM-DIAMOND-DRILL-HEAD` | 1 x (8x8 at x2, y53) | n/a (one pip) | 50.0 px to the numeral stroke | 2.0,1.0 to 62.0,62.0 | pip 6x6 at x4 |
| `F003-ITEM-FLUX-CATALYST` | 2 x 8x8 at x2 and x14 | 4.00 | 33.0 | 2.0,2.0 to 62.0,62.0 | 6x6, gap 2 |
| `F003-ITEM-OVERCLOCK-CORE` | 3 x 8x8 at x2, x14, x26 | 4.00 and 4.00 | 16.0 from the last pip to the first numeral stroke | 2.0,1.25 to 62.0,62.0 | 6x6, gap 2 |

On screen at 32 pt the pips are 4 pt and the gaps 2 pt, which meets the C6 reading recorded in D1. Frame, glyph, numeral, colors and sizes are unchanged from the before state; pips keep their frame color. The 3 px spacing between numeral strokes is unchanged and is not a pip gap. Internal gaps of overlapped glyph layers (for example the flask neck to the plate, 0.88 px, dark ink on a dark plate) are unchanged from the T109-approved art and are layer overlays, not separate parts.

**UI icons (minimum gap 2 px or overlap, canvas, cue)**:

| Asset | Pair | Before (px) | After (px) | Union bbox after (48x48) |
|---|---|---:|---:|---|
| `F003-UI-ICON-AUTOSELL` | arrowhead to arc end | 1.05 | 0.00 (overlap, merged) | 6.38,3.0 to 42.38,42.25 |
| `F003-UI-ICON-AUTOSELL` | coin disc to arrowhead | 3.03 | 5.53 | |
| `F003-UI-ICON-RETRY` | arrowhead to arc end | 1.05 | 0.00 (overlap, merged) | 6.38,3.0 to 42.38,42.25 |
| `F003-UI-ICON-RESEARCH-NODE-OWNED` | tick to ring | 0.76 | 2.37 | 4.0,4.0 to 44.0,44.0 |
| `F003-UI-ICON-RESEARCH-NODE-OWNED` | check to ring, check to tick | 2.20, 2.12 | 3.65, 3.12 | |
| `F003-UI-ICON-OVERDRIVE-COOLDOWN` | bolt to clock ring | 2.30 | 4.98 | 3.88,1.62 to 44.0,44.0 |
| `F003-UI-ICON-OVERDRIVE-COOLDOWN` | bolt to clock hand | 7.78 | 9.43 | |
| `F003-UI-ICON-OVERDRIVE-COOLDOWN` | clock hand to ring | 0.60 (sliver) | 0.00 (hand meets ring) | |

Before the rework the cooldown bolt apex reached y = 0.00 (clipped by the canvas); after, the topmost mask row is y = 1.62 and the miter tip is inside. All seven sources stay inside their canvas with at least 1.0 px margin. The tick-to-ring gap of the original measures 0.76 px here against 1.01 px in T109; both are below 2 px and the difference is the pixel-mask bias described below.

**Unchanged contract, all seven**: same ID in `metadata`, same slug, same `width`, `height` and `viewBox` (64x64 items, 48x48 icons), same palette hexes (items `#0B1020`, `#1F2937`, frame color, glyph colors, `#E5E7EB`; icons `#FFFFFF` only), no forbidden element, UI icons at most 3 drawn parts (limit 4), 4 px strokes, same cue text in `desc`. No restyle: only the lines listed in the `git diff` changed, plus PROVENANCE rows.

### Gap-method resolution (cooldown 0.80 px against 2.43 px)

The T142 worker's 2.43 px is the correct figure for the geometric bolt-to-clock-ring gap of the original. Reproduced two ways: (a) analytically, the distance from the ring center (34,34) to the bolt edge line from (13,38) to (28,18) is 14.4 px; minus the 2 px half stroke and the ring outer radius 10 px gives 2.4 px; (b) by 8x alpha-mask measurement of the isolated bolt and ring masks: 2.30 px (the pixel method reads about 0.1 px low, the same bias as the arrowhead pair: T109 had 1.19 px, this review 1.05 px, analytic 1.19). The T109 figure of 0.80 px for this pair does not reproduce by either method and is withdrawn as a measurement of bolt to ring. The measurable T109 defects of that icon were real: the bolt apex was clipped by the canvas (y = 0.00), and the clock hand sat 0.60 px from the ring. T142 removed both, so the rework is valid, but the reason was the clipped apex and the sliver, not a sub-1 px bolt gap. This correction does not change any verdict.

### Per-asset verdicts

| Asset | Verdict | Evidence and judgment |
|---|---|---|
| `F003-ITEM-DIAMOND-DRILL-HEAD` | **APPROVED** | One 8x8 px pip, 2 px from the canvas edge, inside canvas. The T109 reject reason (6x6 pip, below the C6 8 px minimum) is fixed; at 32 pt the pip is 4 pt. Circle frame, drill-bit over diamond and `I` unchanged and readable in normal, grayscale, protan and deutan |
| `F003-ITEM-FLUX-CATALYST` | **APPROVED** | Two 8x8 pips with a measured 4.00 px gap (2 pt on screen). Square frame, flask and `II` unchanged; the lilac base shifts hue in grayscale and CVD but the square frame and the pip count carry the tier |
| `F003-ITEM-OVERCLOCK-CORE` | **APPROVED** | Three 8x8 pips with 4.00 px gaps, row spans x2..34, 16 px clear of the numeral. Triangle frame, square core with spokes and `III` unchanged; the frame shape distinguishes the three tiers without color |
| `F003-UI-ICON-AUTOSELL` | **APPROVED** | Arrowhead now overlaps the arc end (gap 0.00, merged), no sliver. The merged head reads as an arrowhead at 8x and at 24 pt, better than the detached head. The coin disc stays 5.53 px clear of the head. A very small notch remains at the join of arc and head; it is under 1 px at 24 pt and does not change the cue |
| `F003-UI-ICON-RETRY` | **APPROVED** | Same arrow rework as Auto-sell; arrowhead merged with the arc, circular-arrow cue with the head at the top right intact, inside canvas (top margin 3.0 px) |
| `F003-UI-ICON-RESEARCH-NODE-OWNED` | **APPROVED** | Tick shortened to `M20 35H28`, 2.37 px from the ring (was 0.76 px); check shortened so it stays 3.65 px from the ring and 3.12 px from the tick. Ring, check and level tick still read; cue unchanged |
| `F003-UI-ICON-OVERDRIVE-COOLDOWN` | **APPROVED WITH NOTE** | Bolt to clock 4.98 px (was 2.30), apex inside the canvas with a 1.62 px top margin, bolt outline plus clock corner cue preserved and readable at 24 pt. Note: the clock ring is now r7 with a 4 px stroke, so its inner space is 10 px (about 5 px on screen at 24 pt), the hand meets the ring at 12 o'clock and ends 2.0 px from the ring on the right; the clock reads as a ring with a small hand stub rather than a full clock face. The bolt outline and the clock corner still distinguish it from `F003-UI-ICON-OVERDRIVE-READY` and `-LOCKED`. Not blocking; Feature 004 may enlarge the clock if the UI places it small |

### Checks not covered or limited

- The second-reviewer repetition of the Art Bible 8.4 shuffled grayscale and CVD pass stays carried to T129 on the integrated scene (see the T109 gate statement); this delta used one reviewer and only the seven sources.
- Rasters were judged on a dark surface approximating the `--surface` token, not in the Unity scene; the tier numeral `#E5E7EB` contrast is unchanged (D2).
- The other 71 sources were not re-reviewed; their T109 verdicts stand. The "Art Bible v0.1 approval" section is unchanged.

### Gate statement (T143, T111)

All seven reworked sources are APPROVED or APPROVED WITH NOTE, and none of the T109 reject reasons remains. All 78 sources are now approved and integrable: 65 APPROVED and 13 APPROVED WITH NOTE (the 12 from T109 plus `F003-UI-ICON-OVERDRIVE-COOLDOWN`). This clears the T143 precondition of T111 and lifts the T109 hold on the three Item `presentationKey` values, so the full key set can resolve in the catalog (C3 is settled by the owner). T111 still depends on T110 and carries these conditions: tier chips and Item icons never sit on the accent fill or a light surface (D2), the Iron Ore contrast note, and the second-reviewer 8.4 repetition at T129. Source files stay unchanged during T111.
