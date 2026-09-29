# F003 Provisional Art Bible v0.1

**Version**: `F003-PAB v0.1` (provisional, development-only)

**Status**: Pre-production, authored by the Art Director author session (T090, Issue #172). It is not approved until the independent review of T091 (Issue #173) records PASS and freezes this version. Until then no asset production (T108) and no Technical Art integration (T111) may start.

**Scope**: Feature 003 (`specs/003-first-factory-prestige`, FR-034, research R15): the slot grid, the four machines, the Iron resources, the three Item tiers, machine and slot state cues, and the UI icons and UI tokens. It is a provisional Art Bible. It does not claim final art direction, and it is replaced by a new approved version if the owner decides on a final style (a style change needs a new version and a migration decision for existing assets).

**Companion brief**: [`F003-asset-briefs-v0.1.md`](./F003-asset-briefs-v0.1.md) lists all 78 asset rows in the F001 inventory format. This document holds the rules; the brief holds the rows.

**Inherited conventions**: [`F001-development-placeholders-v0.1.md`](./F001-development-placeholders-v0.1.md) and [`F001-technical-art-integration.md`](./F001-technical-art-integration.md) (top-down orthogonal grid, 64 px per cell, 64 PPU, method `T1`, provenance `P1`, point-filtered lossless import).

**Unity target**: `6000.6.0f1`; orthographic `SpriteRenderer` world and a UI Toolkit overlay. Screen classes: `360x640`, `390x844`, `768x1024` with their rotations, and desktop `1280x720` and `1920x1080` (`contracts/ui-surfaces.md`).

## 1. Principles

1. The factory must be readable at a glance with 12 slots in view. State is read from silhouette and symbol first and from color last (FR-033: no state by color alone).
2. All art is flat, geometric and repository-authored. No gradient, texture, lighting, perspective or lore is introduced.
3. Every number in this document is a rule that T109, T111, T112 and T129 can measure.

## 2. View and pixel system

- **View**: top-down orthographic, no perspective distortion, no rotation of machines (a machine never rotates because there are no ports).
- **Cell**: 1 cell = 64x64 source pixels = 1 world unit at 64 PPU (F001).
- **Slot**: 1 slot = 2x2 cells = 128x128 source pixels = 2 world units. Slot pitch is 160 px (128 px slot plus a 32 px gap). A 12-slot factory is a 4-column by 3-row grid (extent 608x448 px) or a 3-column by 4-row grid (extent 448x608 px). T112 chooses the orientation by screen aspect. The four starting slots are the first four in reading order, and slots 5 to 12 unlock at factory levels 2 to 5 in pairs (5-6 at level 2, 7-8 at 3, 9-10 at 4, 11-12 at 5).
- **Slot anatomy** (slot-local pixels, origin bottom-left, y up): header band y 96..128 (32 px) holds up to one badge and two markers as `[marker 28][badge 72][marker 28]`; the machine art region is x 8..120, y 4..92 (SVG coordinates, y down: y 36..124).
- **Anchors and pivots**: slot bases, machine bodies and the selection frame are 128x128 with a bottom-left custom pivot at (0,0) on the slot anchor. Badges, markers, resource icons, Item icons and UI icons use a center pivot (0.5,0.5). Badge and marker centers are at slot-local (64,112), (14,114) and (114,114).
- **Sorting order** (default sorting layer, explicit `sortingOrder`): slot base 10, machine 100, selection frame 150, badges and markers 300. UI Toolkit icons follow UI document order.
- **Source format**: SVG 1.1, explicit pixel `viewBox`, transparent background, flat fills, integer coordinates on a 2 px grid, no embedded raster, linked file, external font, filter, mask, animation or script. All labels are vector paths.
- **Import format**: lossless RGBA PNG at the exact brief size, point filtering, no atlas unless W003 shows a need.

## 3. Palette

All hex values are fixed for v0.1. Contrast ratios below are WCAG 2.x relative-luminance ratios, computed for this document with Python (`(L1 + 0.05) / (L2 + 0.05)`), and must be reproduced by T109 and T111.

### 3.1 Palette roles

| Role | Hex | Use |
|---|---|---|
| Backing (world and screen background) | `#111827` | camera background, screen background |
| Surface | `#1F2937` | panels, sheets, occupied and locked slot plates |
| Surface raised | `#374151` | secondary buttons, cards on a surface |
| Ink | `#0B1020` | outlines, badge and marker glyphs and labels, text on bright fills |
| Light | `#E5E7EB` | primary text, locked-slot digit |
| Light strong | `#F8FAFC` | outer focus ring |
| Text secondary | `#CBD5E1` | secondary text |
| Muted | `#94A3B8` | muted text, disabled text, empty-slot marks, padlock |
| Grid / border | `#64748B` | occupied-slot outline, disabled border |
| Accent / selection | `#38BDF8` | selection frame, primary button, focus accent |
| Positive | `#4ADE80` | RUN badge, positive text |
| Warning | `#FBBF24` | IN! badge, warning text |
| Danger | `#F87171` | error text, destructive confirm |

### 3.2 State, marker and item colors

| Element | Fill | Ink on fill | Ink / fill ratio | Fill vs `#111827` |
|---|---|---|---:|---:|
| Badge RUN | `#4ADE80` | `#0B1020` | 10.86 | 10.18 |
| Badge WAIT | `#A5B4FC` | `#0B1020` | 9.50 | 8.90 |
| Badge IN! | `#FBBF24` | `#0B1020` | 11.34 | 10.63 |
| Badge FULL | `#F0ABFC` | `#0B1020` | 10.76 | 10.08 |
| Badge PAUS | `#94A3B8` | `#0B1020` | 7.38 | 6.92 |
| Badge OFF | `#CBD5E1` | `#0B1020` | 12.75 | 11.95 |
| Marker boosted | `#2DD4BF` | `#0B1020` | 10.17 | 9.53 |
| Marker Overdrive | `#FB923C` | `#0B1020` | 8.37 | 7.84 |
| Selection frame | `#38BDF8` | n/a | n/a | 8.28 (6.85 vs `#1F2937`) |
| Tier I frame (bronze) | `#D08A4E` | `#0B1020` | 6.70 | 6.27 |
| Tier II frame (silver) | `#CBD5E1` | `#0B1020` | 12.75 | 11.95 |
| Tier III frame (gold) | `#FACC15` | `#0B1020` | 12.36 | 11.58 |

Every badge, marker and tier-chip label passes the 4.5:1 rule with a minimum of 6.70 (rule in section 8.3). Every fill also has at least 3:1 against the backing.

### 3.3 Machine, resource and slot colors

| Element | Fill | vs `#111827` | vs ink `#0B1020` |
|---|---|---:|---:|
| Machine Miner | `#CA8A04` | 6.04 | 6.44 |
| Machine Smelter | `#DC2626` | 3.67 | 3.92 |
| Machine Press | `#60A5FA` | 6.98 | 7.45 |
| Machine Assembler | `#C084FC` | 6.71 | 7.17 |
| Resource Iron Ore | `#B45309` | 3.53 | 3.77 |
| Resource Iron Bar | `#94A3B8` | 6.92 | 7.38 |
| Resource Iron Plate | `#E2E8F0` | 14.39 | 15.36 |
| Resource Gear | `#FACC15` | 11.58 | 12.36 |

| Slot element | Colors | Contrast |
|---|---|---|
| Occupied plate | fill `#1F2937`, 4 px outline `#64748B` | outline 3.73 vs `#111827` |
| Empty slot marks | brackets and plus `#94A3B8` on the backing | 6.92 |
| Locked plate | fill `#1F2937`; padlock `#94A3B8` | 5.72 (padlock on plate) |
| Locked digit `L2` to `L5` | `#E5E7EB` on `#1F2937` | 11.86 |
| Locked hatch | 2 px lines `#374151` on `#1F2937` | decorative only, carries no meaning, exempt from the ratio |

Machine fills are non-text graphics: they need at least 3:1 against the backing, and the lowest is 3.67 (Smelter). No text is set on a machine body.

Color is supplemental everywhere. Section 8.4 shows that color alone cannot separate the state badges.

## 4. Shape language and machine silhouettes

- Build every sprite from rectangles, straight lines, circles, triangles and 45-degree strokes. Outside corners are square. Interior geometry is inset by at least 4 px from the outline.
- Outline: 4 px ink `#0B1020` on machines and slot plates, 2 px on badges, markers, resources and tier chips. No stroke thinner than 2 px and no gap narrower than 2 px at source scale.
- Machines fill their region with one flat fill from section 3.3 plus at most three interior ink primitives (windows, slots, dots).

Machine silhouettes (SVG coordinates on the 128x128 canvas, y down, inside the art region x 8..120, y 36..124):

| Machine | Geometry (union of primitives) | Mask area (px, % of 16,384) |
|---|---|---|
| Miner | rect (24,36)-(104,76); triangle (24,76), (104,76), (64,124) | 5,120 (31.2 %) |
| Smelter | rect (16,68)-(112,124); chimney rect (72,36)-(104,68) | 6,400 (39.1 %) |
| Press | bar rect (16,36)-(112,60); stem rect (48,60)-(80,92); bar rect (16,92)-(112,124) | 6,400 (39.1 %) |
| Assembler | circle center (64,80) radius 36; teeth rects (56,36)-(72,52), (56,108)-(72,124), (8,72)-(28,88), (100,72)-(120,88) | 4,964 (30.3 %) |

**Uniqueness rule**: the intersection-over-union (IoU) of any two machine masks must be at most 0.70, measured on the 128x128 mask and on a 32x32 downsample (a 4x4 block counts as filled at 8 or more of 16 pixels; this equals a slot at 16 pixels per cell). Computed for these geometries, full resolution / 32x32:

| Pair | IoU 128 | IoU 32 |
|---|---:|---:|
| Miner / Smelter | 0.452 | 0.452 |
| Miner / Press | 0.491 | 0.488 |
| Miner / Assembler | 0.611 | 0.612 |
| Smelter / Press | 0.575 | 0.575 |
| Smelter / Assembler | 0.501 | 0.505 |
| Press / Assembler | 0.382 | 0.380 |

The maximum is 0.612 (Miner / Assembler). Each silhouette also carries a distinct dominant feature (downward point, chimney, H-beam, circle with teeth) that survives the header band overlap of section 6.3.

## 5. Slot family

| Slot condition | Silhouette | Shape / text cue | Color (supplemental) |
|---|---|---|---|
| Empty (buildable) | four open corner brackets (24 px legs, 4 px) and a centered 32 px plus sign; no fill | plus sign | `#94A3B8` on backing |
| Locked | filled plate, 45-degree hatch at 16 px spacing, centered padlock (40x32 body, 24 px shackle) | padlock plus the text `L2`, `L3`, `L4` or `L5` (the factory level that unlocks it; digit cap height 24 px) | plate `#1F2937`, padlock `#94A3B8`, digit `#E5E7EB` |
| Occupied | filled plate with a 4 px outline, one machine body on it | machine silhouette (section 4) | plate `#1F2937`, outline `#64748B` |
| Selected (any slot) | double outline: 4 px, 4 px gap, 4 px, drawn around the slot edge | double outline | `#38BDF8` |

Empty, locked and occupied are distinguishable in grayscale by silhouette alone: open brackets and plus, a hatched plate with a padlock, or a plate with a machine.

## 6. State, marker and cap rules (ART-2)

### 6.1 Caps

- At most **one state badge** and **two markers** per slot. A locked or empty slot carries neither.
- The selection frame is a slot-condition cue and does not count as a badge or a marker. There is no other overlay on a slot.
- At most 6 layers per slot: base, machine, selection frame, one badge, two markers. A 12-slot factory draws at most 12 + 12 + 1 + 12 + 24 = 61 slot sprites.
- A third marker kind, or a second badge, needs a new Art Bible version.

### 6.2 State badge precedence

The world shows one badge per occupied slot. The machine panel lists every blocker (contract FR-004 and UX-10). The badge is the highest-ranked row whose condition holds, taken from the machine state read model (`active` flag, running/paused/waiting, blocker kind):

| Rank | Condition | Badge | Glyph (unique silhouette) | Text cue | Fill |
|---:|---|---|---|---|---|
| 1 | blocker `Inactive` (active flag off) | `F003-BADGE-INACTIVE` | circle with a 45-degree slash | `OFF` | `#CBD5E1` |
| 2 | blocker `MissingInput` | `F003-BADGE-MISSING-INPUT` | outlined down triangle | `IN!` | `#FBBF24` |
| 3 | blocker `StorageFull` | `F003-BADGE-STORAGE-FULL` | box with filled top bar and fill line | `FULL` | `#F0ABFC` |
| 4 | waiting (cycle progress equals the cycle duration, completion count 0) with no blocker kind | `F003-BADGE-WAITING` | hourglass (two triangles apex to apex) | `WAIT` | `#A5B4FC` |
| 5 | paused with no blocker kind | `F003-BADGE-PAUSED` | two vertical bars | `PAUS` | `#94A3B8` |
| 6 | running | `F003-BADGE-RUNNING` | right-pointing triangle | `RUN` | `#4ADE80` |

The six glyphs differ in silhouette (circle, triangle-down outline, box, hourglass, bars, triangle-right), and each has a distinct text label of at most four letters. See Art Director decision AD-3 for the mapping of `paused` and `waiting`.

### 6.3 Markers

| Marker | Condition | Glyph | Position | Fill |
|---|---|---|---|---|
| `F003-MARKER-BOOSTED` | at least one active Item effect targets this machine | double up-chevron | left of the badge, center (14,114) | `#2DD4BF` |
| `F003-MARKER-OVERDRIVE` | factory Overdrive is active (shown on every occupied slot) | lightning bolt | right of the badge, center (114,114) | `#FB923C` |

Both fit at once, so two markers are the cap. Research and Industry effects are permanent and never markers. Markers have no text; their cue is the glyph, and the machine panel names the effect in text.

### 6.4 Slot condition cues (unique silhouette plus text or shape cue)

| Condition | Unique silhouette | Text or shape cue |
|---|---|---|
| Empty | open brackets | plus sign |
| Locked | hatched plate | padlock and `L2` to `L5` |
| Occupied | machine on a plate | machine silhouette; state by badge |
| Selected | double outline | double outline |
| Each of six badge states | section 6.2 glyphs | labels `OFF`, `IN!`, `FULL`, `WAIT`, `PAUS`, `RUN` |
| Boosted, Overdrive | chevrons, bolt | glyph |

### 6.5 Badge placement on the machine

The header band is reserved (section 2) so the badge never hides a machine silhouette feature at 64 pixels per cell. At the minimum projected badge size the header band can cover up to 4 projected pixels of the top of the machine art (at 16 pixels per cell), and section 4 already measures uniqueness on the full silhouette; the badge covers at most 35 % of the projected slot area at every supported zoom (section 8.2).

## 7. Resource, Item-tier and UI-icon families

| Family | Size and pivot | Rules |
|---|---|---|
| Resources (4: Ore, Bar, Plate, Gear) | 48x48, center | Full color, 2 px ink outline, one primitive family each: Ore two triangles, Bar trapezoid ingot, Plate square with four rivets, Gear circle with eight teeth. They are icons for stock lists and contract rows and imply no port or belt |
| Items (3 icons) | 64x64, center | Full color glyph inside a baked tier frame: Tier I circle frame with 1 pip and `I`, Tier II square frame with 2 pips and `II`, Tier III triangle frame with 3 pips and `III` |
| Tier chips (3) | 48x24, center | Small tier tag for list headers: frame shape, pip count and numeral repeat the item cue |
| UI icons (49) | 48x48, center; displayed 24x24 pt | One-color white glyph (`#FFFFFF`), tinted by tokens; 4 px minimum stroke, at most 6 primitives per icon; meaning by shape only |

Tier cue is triple redundant (frame shape, pip count, numeral), so tiers do not depend on bronze, silver and gold color.

The enumerated IDs of all resource, Item, tier chip and UI icon rows are in the brief (rows 20 to 78 of the inventory). Every content `presentationKey` and every UI icon has its own row (ART-3).

## 8. Measurable readability rules (ART-1)

Pixels per cell (ppc) means projected screen points per 64 source pixels (1 cell). A slot at N ppc is 2N points wide.

### 8.1 Zoom steps

| Step | ppc | Slot size | Must be readable at this step |
|---:|---:|---:|---|
| Z16 (camera minimum) | 16 | 32 pt | Empty, locked and occupied slots by silhouette; each of the four machine silhouettes; the selection frame; every badge glyph at a minimum projected size of 27x12 pt. Markers, labels and locked digits may be suppressed |
| Z24 (12-slot floor) | 24 | 48 pt | Everything of Z16, plus both markers (10.5 pt) and the locked digit (cap height 9 pt) |
| Z32 | 32 | 64 pt | Everything of Z24, plus badge labels recognizable by length and shape (cap height 6 pt) at a projected badge size of at least 36x16 pt |
| Z64 | 64 | 128 pt | All labels plainly legible (cap height 12 pt); no outline merges with fills |

### 8.2 Minimum projected sizes and the 12-slot rule

- Badge: natural size 72x32 px; the presentation scales it up so its projected size is at least 27x12 pt, and never below natural size at 64 ppc. It covers at most 35 % of the projected slot area at every ppc from 16 to 64 (computed: 27x12 = 324 of 1,024 pt2 at 16 ppc = 31.6 %; 27x12 of 2,304 at 24 ppc = 14.1 %; 72x32 of 16,384 at 64 ppc = 14.1 %).
- Markers: natural 28x28 px, at least 10x10 pt projected, hidden below 24 ppc.
- **Readable-at-12-slots rule**: with all 12 slots in view and no sheet open, the fit-all zoom must be at least 24 ppc (the Z24 column) on every supported screen class, including rotations. Computation, using the world area left after the top strip (48 pt) and, on phones, the tab bar (56 pt), or, on tablet and desktop, a side panel (planning value 360 pt wide; T112 owns the real width and must re-check), extent with a half-cell margin (3x4 layout 8 x 10.5 cells; 4x3 layout 10.5 x 8 cells), ppc = min(width / cells wide, height / cells tall):

| Class (pt) | World area (pt) | Layout | Fit-all ppc |
|---|---|---|---:|
| 360x640 | 360x536 | 3 x 4 | 45.0 |
| 390x844 | 390x740 | 3 x 4 | 48.8 |
| 640x360 (rotated) | 640x256 | 4 x 3 | 32.0 |
| 844x390 (rotated) | 844x286 | 4 x 3 | 35.8 |
| 768x1024 | 408x976 | 3 x 4 | 51.0 |
| 1024x768 (rotated) | 664x720 | 4 x 3 | 63.2 |
| 1280x720 | 920x672 | 4 x 3 | 84.0 |
| 1920x1080 | 1560x1032 | 4 x 3 | 129.0 |

  The worst case is 32.0 ppc (phone landscape). The camera must not fit-all below 24 ppc, and T112 verifies this in `SlotGridTests` and `ZoomReadabilityTests`.
- With a bottom sheet open (at most 60 % of the safe-area height), the selected slot (at least 48 pt at 24 ppc) stays in view above it: the visible world band on a 360x640 phone is 640 - 48 - 384 = 208 pt.

### 8.3 Contrast rules

- Text, glyphs and labels drawn as part of a badge, marker, tier chip or slot label: at least 4.5:1 against their own fill. Minimum computed: 6.70.
- Non-text graphics (machine fills, resource fills, outlines, frames, icon tints against their surface): at least 3:1 against the surface they sit on. Minimum computed among art: 3.53 (Iron Ore on the backing).
- Decorative marks (locked hatch) must not carry meaning.
- T109 and T111 recompute all ratios with the WCAG formula and compare with sections 3 and 9.

### 8.4 Grayscale and color-vision-deficiency checks

- **Grayscale** (Rec. 709 relative luminance) of the six badge fills: RUN 0.553, WAIT 0.477, IN! 0.579, FULL 0.547, PAUS 0.360, OFF 0.657. RUN and FULL differ by 0.006, so grayscale cannot separate them and the glyph plus label must.
- **Color-vision deficiency** (Machado, severity 1.0, linear-RGB matrices for protanopia, deuteranopia and tritanopia), smallest pairwise CIELAB distance between the six badge fills: normal vision 19.2 (PAUS / OFF), protanopia 2.1 (WAIT / FULL), deuteranopia 12.3 (WAIT / FULL), tritanopia 12.2 (WAIT / PAUS). At 2.1, WAIT and FULL are indistinguishable by color under protanopia, and the rule that follows applies.
- **Pass rule** (T109 and T111): render the sheet of 6 badges, 2 markers, 4 slot bases (empty, `L2`, occupied, selected) and 4 machines at Z24 and Z32 in (a) grayscale, (b) protanopia, (c) deuteranopia, (d) tritanopia. Two reviewers name every tile from a shuffled sheet with zero confusions. Any confusion blocks integration even when colors differ. These are desktop screening checks, not clinical validation, and T129 repeats them on the integrated scene.

### 8.5 Numeric noise limits

| Limit | Value |
|---|---|
| Outline width | machine and slot plate 4 px; badge, marker, resource, tier chip 2 px |
| Minimum stroke / gap | 2 px |
| Interior primitives per machine (beyond the silhouette) | at most 3 |
| Primitives per resource icon | at most 4; per UI icon at most 6 |
| Fill colors per sprite | at most 4, plus ink |
| Hatch (locked plate) | 45 degrees, 16 px spacing, 2 px line, one layer |
| Layers per slot | at most 6 (section 6.1) |
| Slot sprites per 12-slot factory | at most 61 |
| Label cap height | badge and tier-chip labels 12 px; slot digit 24 px |
| Text labels in art | at most 4 letters (`L2` to `L5` at most 2 characters) |
| Palette | only the hex values of section 3 and section 9 |

## 9. UI tokens (ART-4)

These tokens govern UI Toolkit styling for T112 (shell) and T113 to T118 (panels). UI Toolkit USS variables use the same names. Dark theme only; there is no light theme in v0.1.

### 9.1 Palette roles and contrast

| Token | Hex | Use | Ratio |
|---|---|---|---|
| `--bg` | `#111827` | screen background | text-primary 14.33 |
| `--surface` | `#1F2937` | panels, sheets, cards | text-primary 11.86, text-secondary 9.89, muted 5.72 |
| `--surface-raised` | `#374151` | secondary buttons | text-primary 8.33, accent 4.81 |
| `--text-primary` | `#E5E7EB` | body and values | 14.33 on bg, 11.86 on surface |
| `--text-strong` | `#F8FAFC` | outer focus ring | 16.96 on bg, 14.03 on surface |
| `--text-secondary` | `#CBD5E1` | secondary text | 9.89 on surface, 11.95 on bg |
| `--text-muted` | `#94A3B8` | captions, disabled text | 5.72 on surface, 6.92 on bg; **not** on surface-raised (4.02) |
| `--accent` | `#38BDF8` | selection, primary button fill, focus accent | 8.28 on bg, 6.85 on surface |
| `--on-accent` | `#0B1020` | label on accent fill | 8.84 |
| `--positive` | `#4ADE80` | positive text and RUN | 8.42 on surface; ink on it 10.86 |
| `--warning` | `#FBBF24` | warning text | 8.79 on surface; ink on it 11.34 |
| `--danger` | `#F87171` | error text, destructive confirm fill | 5.31 on surface; ink on it 6.84; **not** on surface-raised (3.73) |
| `--border` | `#64748B` | card and disabled borders | 3.08 on surface, 3.73 on bg |

Text and glyph pairs must be at least 4.5:1 (3:1 for text of 18 pt or larger, or 14 pt bold). Borders and icon tints against their surface must be at least 3:1. `--positive`, `--warning` and `--danger` are used only together with an icon and text, never alone.

### 9.2 Component tokens

| Token | Value |
|---|---|
| Primary button | fill `--accent`, label `--on-accent` (8.84), icon plus text, at least 44x44 pt |
| Secondary button | fill `--surface-raised`, label `--text-primary` (8.33), 1.5 pt border `#94A3B8` (4.02 on the fill, 6.92 on `--bg`), at least 44x44 pt |
| Destructive confirm | fill `--danger`, label `--on-accent` (6.84), icon `F003-UI-ICON-WARNING` plus text |
| Focus ring | 2 pt inner ring `--on-accent` (`#0B1020`) plus 2 pt outer ring `--text-strong` (`#F8FAFC`), drawn outside the control edge with 0 pt offset. Ratios: outer vs surface 14.03, outer vs bg 16.96, inner vs accent fill 8.84, inner vs outer 18.10. Always visible on keyboard focus, never removed, and the ring is visible on the accent fill because the inner ring separates it |
| Selected card or row | 2 pt `--accent` border plus a check icon (not color alone) |

### 9.3 Type

| Level | Minimum size | Use |
|---|---|---|
| Body | 14 pt | labels, panel text, blocker hints |
| Caption / secondary | 12 pt | units, detail lines, tab labels, badge legend |
| HUD value | 16 pt | Cash, RP, store usage numbers |

Sizes are panel points and scale with the player's text scale up to 200 % without clipping (sheets scroll). No text below 12 pt anywhere, and no text is drawn inside art except the badge and slot labels of section 8.5. UI text uses the project default UI Toolkit font; no external font is introduced.

### 9.4 Disabled and rejected look

- Disabled control: fill `--surface`, label and icon `--text-muted` (5.72, stricter than the WCAG exemption), border 2 pt dashed `--border` (3.08), and the reason text visible in `--text-secondary` next to the control. A disabled control stays focusable so its reason can be read, and activation is rejected with the reason.
- Locked item or type: `F003-UI-ICON-LOCK` beside the requirement text.
- Rejection: `F003-UI-ICON-WARNING` plus the reason-code copy in `--danger` on `--surface`, beside the control that was used.

### 9.5 Icons in UI

UI icons are tinted white glyphs. Enabled tint `--text-primary`, disabled `--text-muted`, selected or accent `--accent`. Icons with a tab label show the label at 12 pt or larger; all six tabs fit at 44 pt or more (360 pt / 6 = 60 pt per tab). Touch target and density rules stay in `contracts/ui-surfaces.md` (Global).

## 10. Tooling and provenance (ART-6)

- **Method `T1`**: repository-authored SVG 1.1, rasterized with **Inkscape CLI `1.4.2`** (`inkscape <source> --export-type=png --export-filename=<import> --export-width=<w> --export-height=<h> --export-background-opacity=0`). The Asset Artist and Technical Artist record the actual build and platform (the F001 record used Inkscape 1.4.2 (f4327f4, 2025-05-13), Windows 11 x64).
- **No silent substitution**: a different tool or version, and any generator (ComfyUI, Blender, image models, hosted services, stock or third-party art, external icons or fonts, raster inputs), needs a revision of the briefs and an explicit independent Art Director approval before production. This rule is repeated in the brief and checked by T109 and T129.
- **Provenance `P1`**: repository-authored primitives with no external asset source. Any language-model or tool assistance is disclosed truthfully in `Assets/IdleFactory/Art/Development/F003/Source/PROVENANCE.md` (T108: method `T1`, tool version, brief ID, Art Bible version, transformation steps). Ownership is the repository owner (`fjure`); no public license grant is claimed because the repository has no root license file.
- No ComfyUI or Blender is assumed for Feature 003.

## 11. Art Director decisions

Decisions that the sources left open and that this version fixes. Each is open to challenge at T091.

| ID | Decision | Rationale |
|---|---|---|
| AD-1 | A slot is 2x2 cells (128x128 px), keeping the F001 64 px per cell convention. The task's "logical footprint 1 slot" is therefore 1 slot = 2x2 cells | A single 64 px cell gives 32 px at 16 ppc, too small for badge and silhouette; 2x2 gives the readable-at-12-slots result of section 8.2 |
| AD-2 | Every slot reserves a 32 px header band for one badge and two markers, and machine art lives below it | Guarantees the badge never hides the machine silhouette at Z64 and fixes the cap layout |
| AD-3 | `paused` means paused with no blocker kind (for example, the initial state before the first run-predicate evaluation) and `waiting` means cycle progress equal to the duration with no blocker kind. A blocker of kind inactive, missing input or storage full always selects its own badge first (rank 1 to 3) | The read model (`CopyMachineStates`) has the flags running/paused/waiting plus a blocker kind; the badge shows the most specific cause. The Game Designer / Product Owner check of T091 should confirm this mapping against `contracts/ui-surfaces.md` |
| AD-4 | F003 does not reuse Feature 001 asset IDs. The F001 "inactive" pause-bars glyph becomes the paused badge, and inactive uses a circle with a slash | Two states need unique glyphs, and F001 had a different status set (normal, selected, placement valid and invalid, producing, missing input, full output, inactive) |
| AD-5 | Resource keys `F003-RES-IRON-ORE`, `-IRON-BAR`, `-IRON-PLATE` and `F003-RES-GEAR`, and Item keys `F003-ITEM-FLUX-CATALYST` (Tier II) and `F003-ITEM-OVERCLOCK-CORE` (Tier III), are proposed names | The contract names only `F003-RES-IRON-ORE`, `F003-MACHINE-MINER` and `F003-ITEM-DIAMOND-DRILL-HEAD`; T097 must use exactly the 11 keys of the brief, or the brief is revised first |
| AD-6 | UI icons are monochrome white glyphs tinted by tokens | Removes state-color variants and lets the disabled look be a token |
| AD-7 | Tier is redundantly coded by frame shape, pip count and numeral, plus a bronze, silver or gold color | Satisfies non-color cues |
| AD-8 | Research, Industry and contract surfaces select icons by effect kind, contract resource and control state, because the content contract gives no `presentationKey` to them | Reported, not overridden: the contract is not changed |
| AD-9 | Badge and slot labels are vector paths of at most four letters (`PAUS` for paused) | No external font and a legible fit in a 72x32 badge |
| AD-10 | Fit-all camera floor is 24 ppc, and the 4x3 or 3x4 orientation is chosen by T112 | Computed in section 8.2 with a worst case of 32.0 ppc |

## 12. Traceability to the plan review findings

| Finding | Where addressed |
|---|---|
| ART-1 (measurable readability: pixels per cell per zoom step, 4.5:1 contrast, grayscale and CVD checks, numeric noise limits) | sections 3, 8.1 to 8.5 |
| ART-2 (precedence, cap of one badge and two markers, unique silhouette plus text or shape cue per state, locked slots) | sections 5, 6.1 to 6.4 |
| ART-3 (icon IDs for every `presentationKey` and UI icon) | section 7 and the brief (78 rows, 11 `presentationKey` rows marked `*`) |
| ART-4 (UI tokens: palette roles, focus ring, minimum text size, disabled look, contrast) | section 9 |
| ART-5 (T129 reviewer independence) | section 13 |
| ART-6 (Inkscape version and no silent substitution) | section 10 and the brief's production contract |
| FR-034 / R15 (provisional Art Bible, top-down orthographic, 64 px per cell, silhouettes, families, noise limits, readable at 12 slots) | sections 2, 4 to 8 and the brief |
| Visual states of `contracts/ui-surfaces.md` | brief cross-check table |

## 13. Review and freeze

1. **T091 independent review**: a different Art Director session approves or rejects this version and the briefs, records the frozen version and checked rows in `specs/003-first-factory-prestige/reviews/art-direction-review.md`, and includes the Game Designer / Product Owner check of AD-3.
2. **T108 to T111**: source production, source review and Technical Art integration follow only after PASS. A style change needs a new Art Bible version and a migration decision.
3. **T129**: the integration reviewers are sessions different from the T090 and T111 authors (ART-5).
