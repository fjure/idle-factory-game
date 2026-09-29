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
