# Style spike record (Feature 004)

Record per research R16 and the data model "Style-spike record". It is filled stage by stage. No Art Bible freeze and no production asset before a complete record exists (SC-009). This file currently holds § S1 (T416) and § S2 (T419); S3 and S4 are added by their own tasks.

## S1 Projection: 3/4 oblique vs isometric 2:1 (T416, #528, FR-007, FR-008, SC-009)

Evidence author: Test/QA session with UI/UX Reviewer lens (Sonnet), 2026-10-28, base `d045089`. The evidence author did not produce the art and gives **no Art Director verdict**; the independent second-session Art Director review is a separate step (empty subsection at the end).

### Candidates and the A/B mapping

| Label | Projection | Assets | Source |
|---|---|---|---|
| **A** | 3/4 oblique | 28 pixel assets (Miner, Assembler with states, 4 slot plates, ore, ingot), D = 24, canvas 80x64, footprint 48x24 px | `art-source/f004/spike/s1/pixel/oblique-3-4/` (T415, PR #582, Blender route) |
| **B** | Isometric 2:1 | the same 28 assets | `art-source/f004/spike/s1/pixel/isometric-2-1/` |

Both are imported through the isolated path `Assets/IdleFactory/Art/F004/Spike/D24/{oblique-3-4,isometric-2-1}/` (T414) and shown by the scene `Assets/IdleFactory/Art/F004/Spike/Scenes/SpikeScaleVariant.unity` (`SpikeScaleViewer`). Density D = 24 only. Palette v0 for both. Machine pair: Miner and Assembler (highest silhouette-confusion risk, R16).

The 12 slots of every screenshot, in reading order: Miner running, Assembler running, Miner warning, Assembler paused / Miner inactive, Assembler inactive, empty slot, selected slot / locked slot, occupied plate, ore, ingot. Running machines animate (6 frames, 8 fps); the screenshots are single frames.

### How the in-game evidence was made (read before interpreting)

- A temporary player (never committed, removed before the commit) booted straight into `SpikeScaleVariant`. The committed viewer and scene are unchanged.
- **Phone and tablet classes: Windows player, offscreen render** at the exact physical resolution into a point-filtered render texture, with the display density overridden by a command-line value (the viewer reads `Screen.dpi`, which on a desktop is the monitor's). Classes: phone landscape 1280x720 at 320 dpi (`phoneL`), phone portrait 720x1280 at 320 dpi (`phoneP`), tablet landscape 2048x1536 at 264 dpi (`tabL`), tablet portrait 1536x2048 at 264 dpi (`tabP`). These are the physical reference devices of `ScreenClassScaleTableTests`. They are **not** captured on physical devices; no physical device was used.
- **Emulator**: Android emulator `Medium_Phone_API_36.0` (1080x2400, 420 dpi), cold boot, `-gpu host`, GLES3-only IL2CPP ARM64 Development APK, screenshots by `adb screencap` (the on-screen label and buttons of the viewer are visible in them). Portrait `emuP` and landscape `emuL` (rotation applied while the app ran). Emulator-only evidence, not physical-device, performance or release evidence.
- **n_min and n_fit in the screenshots are approximations.** `SpikeScaleViewer` approximates pixels per point as `Screen.dpi / 160` (2.0 at 320 dpi, 1.65 at 264 dpi, 2.625 at 420 dpi) and fits against the **whole screen with no HUD**. The production rule uses `ShellPanelScale` (effective pixels per point) and the HUD-aware unobstructed rectangle. The authoritative per-class numbers are the zoom-level table below (from `ScreenClassScaleTableTests`). Where the two differ, the table governs, and the screenshots show the viewer's `n` (stated per row).
- The viewer lays the 12 slots on the existing rectangular Feature 003 lattice for both projections. Candidate B's diamond plates are therefore wider than their lattice cell and **overlap neighbouring plates** in the screenshots; a true isometric slot lattice (and its hit-testing and sorting consequences, R04) is not part of this spike and would be part of the decision if B is chosen.
- Crispness of the screenshots was measured (see gate 6).

### Contact sheets (1x)

Shuffled with seed 415; each sheet shows the 14 tiles at native size. The answer keys are `*_key.json` (`seed`, `order`); a reviewer who names the tiles must not be given them.

| Projection | Grayscale | Deuteranopia | Protanopia | Tritanopia | Key |
|---|---|---|---|---|---|
| A oblique | [grayscale](style-spike/s1/sheets/oblique-3-4_grayscale.png) | [deut](style-spike/s1/sheets/oblique-3-4_deuteranopia.png) | [prot](style-spike/s1/sheets/oblique-3-4_protanopia.png) | [trit](style-spike/s1/sheets/oblique-3-4_tritanopia.png) | [key](style-spike/s1/sheets/oblique-3-4_key.json) |
| B isometric | [grayscale](style-spike/s1/sheets/isometric-2-1_grayscale.png) | [deut](style-spike/s1/sheets/isometric-2-1_deuteranopia.png) | [prot](style-spike/s1/sheets/isometric-2-1_protanopia.png) | [trit](style-spike/s1/sheets/isometric-2-1_tritanopia.png) | [key](style-spike/s1/sheets/isometric-2-1_key.json) |

The same sheets, with the pixel files and provenance, are in `art-source/f004/spike/s1/sheets/`.

### On-device screenshots (A/B, n_min and fit-all)

Folder: `style-spike/s1/shots/`, file name `<class>-<A|B>-<nmin|fit>.png`. "n shown" is the integer scale the viewer used; "authoritative" is the table row below.

| Class | Screen (px) | Viewer pt scale | A n_min | A fit-all | B n_min | B fit-all | n shown (n_min / fit-all) |
|---|---|---|---|---|---|---|---|
| Phone landscape (Windows player) | 1280x720 | 2.0 | [A](style-spike/s1/shots/phoneL-A-nmin.png) | [A](style-spike/s1/shots/phoneL-A-fit.png) | [B](style-spike/s1/shots/phoneL-B-nmin.png) | [B](style-spike/s1/shots/phoneL-B-fit.png) | 2 / 3 |
| Phone portrait (Windows player) | 720x1280 | 2.0 | [A](style-spike/s1/shots/phoneP-A-nmin.png) | [A](style-spike/s1/shots/phoneP-A-fit.png) | [B](style-spike/s1/shots/phoneP-B-nmin.png) | [B](style-spike/s1/shots/phoneP-B-fit.png) | 2 / 3 |
| Tablet landscape (Windows player) | 2048x1536 | 1.65 | [A](style-spike/s1/shots/tabL-A-nmin.png) | [A](style-spike/s1/shots/tabL-A-fit.png) | [B](style-spike/s1/shots/tabL-B-nmin.png) | [B](style-spike/s1/shots/tabL-B-fit.png) | 2 / 6 |
| Tablet portrait (Windows player) | 1536x2048 | 1.65 | [A](style-spike/s1/shots/tabP-A-nmin.png) | [A](style-spike/s1/shots/tabP-A-fit.png) | [B](style-spike/s1/shots/tabP-B-nmin.png) | [B](style-spike/s1/shots/tabP-B-fit.png) | 2 / 6 |
| Emulator portrait | 1080x2400 | 2.625 | [A](style-spike/s1/shots/emuP-A-nmin.png) | [A](style-spike/s1/shots/emuP-A-fit.png) | [B](style-spike/s1/shots/emuP-B-nmin.png) | [B](style-spike/s1/shots/emuP-B-fit.png) | 3 / 5 |
| Emulator landscape | 2400x1080 | 2.625 | [A](style-spike/s1/shots/emuL-A-nmin.png) | [A](style-spike/s1/shots/emuL-A-fit.png) | [B](style-spike/s1/shots/emuL-B-nmin.png) | [B](style-spike/s1/shots/emuL-B-fit.png) | 3 / 5 |

Missing captures: none of the requested cells. Not captured: any physical phone or tablet (none available to this assignment), and the phone/tablet classes on the emulator (one AVD only).

### Zoom-level table per screen class (D = 24, `maxScale` 6, readable cell 24 pt)

Source: `ScreenClassScaleTableTests` "physical reference devices" table of the full Edit Mode run on this commit (effective pixels per point from `ShellPanelScale.Decide`; HUD-aware rectangle from the test's fixed model of the closed shell). `levels` = `maxScale - n_min + 1`; zoom-out steps below fit-all = `n_fit - n_min + 1` (1 means fit-all only: **no zoom range below fit-all, pinch and zoom-out do nothing**).

| Screen class (reference device) | Effective px/pt | n_min | n_fit | Allowed levels | Levels at or below fit-all | Margin at fit-all | Where there is no zoom range |
|---|---|---|---|---|---|---|---|
| Phone portrait 720x1280, 320 dpi | 2.004 | 3 | 3 | 4 (3..6) | 1 | 18.2 % | **Fit-all only** |
| Phone landscape 1280x720, 320 dpi | 2.004 | 3 | 2 | 4 | 0 | 40.0 % | **Violation** `n_fit < n_min` (known; owner decision 2026-10-28: `n_min` gets a 1 % tolerance, implemented by T427; with it `n_min` is 2 and fit-all only) |
| Phone portrait 1080x2400, 420 dpi (emulator) | 2.631 | 3 | 4 | 4 | 2 | 28.8 % | |
| Phone landscape 2400x1080, 420 dpi (emulator) | 2.631 | 3 | 4 | 4 | 2 | 22.7 % | |
| Phone portrait 1170x2532, 460 dpi | 2.881 | 3 | 5 | 4 | 3 | 17.7 % | |
| Phone landscape 2532x1170, 460 dpi | 2.881 | 3 | 4 | 4 | 2 | 28.5 % | |
| Tablet portrait 1536x2048, 264 dpi | 2.022 | 3 | 3 | 4 | 1 | 28.3 % | **Fit-all only** |
| Tablet landscape 2048x1536, 264 dpi | 1.867 | 2 | 5 | 5 | 4 | 10.9 % | |
| Desktop 1280x720 / 1920x1080 / 2560x1440 | 1 / 1.5 / 2 | 1 / 2 / 2 | 3 / 5 / 6 | 6 / 5 / 5 | 3 / 4 / 5 | 21.2 / 12.4 / 21.2 % | |

Consequences for the owner: with whole-number scales (OD-4) the 720x1280 phone and the portrait tablet have exactly one zoom-out level (fit-all); zoom changes there only go in (toward `maxScale`). The 1280x720 phone is the single violation of the rule and is carried by T427. The table does not depend on the projection candidate; it is the same for A and B at D = 24. The screenshots of the Windows phone and tablet classes use the viewer's approximated scale (column "n shown" above) and so show `n_min` 2 and fit-all 3 or 6 where the authoritative values are 3 and 2 to 3, or 3 and 3 for the portrait tablet; the emulator screenshots show 3 / 5 where the authoritative values are 3 / 4 (the viewer fits the whole screen, the table reserves the HUD).

### Gate results

| # | Gate (R16) | A oblique | B isometric | Evidence / owner |
|---|---|---|---|---|
| 1 | Silhouette IoU, Miner vs Assembler, at most 0.70 | **FAIL**: 0.759 full, 0.789 at 32 px | **PASS**: 0.651 full, 0.672 at 32 px | `art-source/f004/spike/s1/check-report.json` (`silhouette_iou[...]`, T415, QA re-render byte-identical for one asset). Measured, not a review. |
| 2 | Grayscale / CVD naming (SC-001 method, shuffled, key outside the reviewer) | FAIL for A and B, tied, not discriminating (state cues only, see second-session review) | FAIL for A and B, tied, not discriminating (state cues only, see second-session review) | Owner of the gate: independent agent reviewer session (not producer, brief author or spike Art Director), transcript recorded in `reviews/readability-*.md`. Sheets above; the evidence author has not named the tiles and has not read the keys for judging. |
| 3 | Cue legibility at `n_min` and fit-all (UI/UX lens) | see observations below; formal verdict pending | see observations below; formal verdict pending | S1 contains body variants only (lit, dim, warning body) and no cue icons; the icon rule (>= 12x12 art px, >= 24 pt at `n_min`, R06) is the S3 gate. Verdict by the UI/UX Reviewer session (not the evidence author). |
| 4 | Palette lock | PASS | PASS | `palette` check on all 28 assets of both candidates (no disallowed group, budgets kept), `palette_definition` 48 of 48 colors cap, no problems. Contrast against declared surfaces: **not run** (needs the Art Bible surfaces, T424). |
| 5 | Reproducibility | PASS (sampled) | PASS (sampled) | Models and renders regenerate from `build_models.py` / `pipeline_s1.py` (build 3.57 s, render 3.48 s total for both candidates); T415 QA re-rendered one asset byte-identically (R09). Golden hashes are owned by the art-pipeline tests, not re-run here. |
| 6 | Crisp rendering (uniform n x n blocks at rest) | PASS horizontally; vertical 98 % of edges on the grid | PASS horizontally; vertical 98 % of edges on the grid | Measured on all 24 screenshots: every horizontal run length is a multiple of the shown `n` (gcd = n, exact), and 97.8 to 98.0 % of vertical edges sit on the n grid (the rest are offset by a fraction of `n`, that is the half art pixel of some sprite pivots, not a scale error). Same figures for A and B; shimmer under pan is not measured here. |
| 7 | Fit-all rule (all 12 slots at fit-all, `n_fit >= n_min`) | PASS in all 24 screenshots (all 12 visible, integer scale, margin) | PASS in all 24 screenshots | Table above: `n_fit >= n_min` everywhere except the known 1280x720 phone landscape violation (T427, owner decision). HUD-aware proof is the Play Mode variant of T427, not this spike. |
| 8 | Overhang / pivot / canvas / outline / alpha / frames | PASS | PASS | `check-report.json`: 204 per-asset checks across 28 assets, 0 failures. |
| 9 | Effort per asset | not measured here | not measured here | T415 timings only (seconds per asset on the Blender route); human review effort is not in this record. |

**Observations from the screenshots (UI/UX lens, evidence author, not approval):**

- A, n_min: Miner and Assembler differ clearly in outline (shield versus gear) in every state; paused/inactive are dark desaturated bodies; the warning variant is the running body with a small red light instead of the yellow one (a few art pixels wide); that difference is easy to miss at n_min on the phone (a few device pixels, about 3 pt) and is the first thing S3 must carry with an icon. The selected slot (blue frame), the locked slot (hatched plate with a lock) and the empty slot (corner marks) read at n_min.
- B, n_min: machine bodies are smaller on the screen (the diamond plate carries the same 48x24 px footprint but the model sits low on it), the plates overlap on the rectangular lattice, and the warning light is as small as in A. Selected (diamond frame), locked, empty and occupied plates all read at `n_min`, with less margin between neighbours than in A.
- Fit-all: on the tablets (`n` = 6 shown; authoritative 5 landscape, 3 portrait) and the emulator both candidates are comfortable; no cue is clipped. On the 720x1280 phone at n 3 both candidates keep all 12 slots with margin.
- Ore and ingot are small in both candidates (roughly one third of a cell) but identifiable at n_min.
- B's plate overlap and the missing isometric lattice are a layout cost that the numeric IoU gate does not see.

### Recommended option and default

- **Agents' recommended option (evidence author's reading of the gates): B, isometric 2:1.** It is the only candidate that passes the measured silhouette gate (0.651 / 0.672 against 0.70), where A fails by 0.06 to 0.09, so A would need redrawn machines before it could pass. B's costs: an isometric slot lattice (overlap, hit-testing and sorting, R04) is needed, and its n_min machines are smaller. Gates 2 and 3 are pending and could change this; the recommendation is the agents' proposal to the owner, and the independent Art Director review below may amend it.
- **Default if no preference: B isometric 2:1** (the only candidate with no failed gate; gates 2 and 3 pending for both).
- Second-session Art Director: AGREE WITH CONDITIONS (see review)

### Second-session Art Director review

Independent second-session Art Director review with blind grayscale/CVD naming, on evidence commit 976f5e9. Read-only Sonnet session (Medium tier), not the producer, brief author or spike Art Director. Received 2026-10-28. The parent recorded it with the prose shortened; the naming table, scores, confusions, gate results, numbers, verdict and conditions are kept exactly as the reviewer gave them.

Review limits:
- Tiles were named from the grayscale and CVD PNGs before any key JSON was opened. `art-source/f004/spike/s1/**/*.json` was not opened.
- Viewed: 5 of 24 shots (phoneP-A-nmin, phoneP-B-nmin, tabL-B-fit, phoneL-B-fit, emuP-B-nmin), all 8 sheets, and this record.
- Not opened in full: research R03/R06/R18, the contract text beyond a grep, and the three wiki pages. The gate checks rest on this record, the shots and the sheets.
- Gates 4, 5 and 8 were accepted from the recorded measurements, not re-checked.

#### 1. Naming gate

The shuffled order is identical for A and B and for all four filters (seed 415, 14 tiles, 4 columns). The threshold is spec.md SC-001, which requires naming every machine state, marker and slot condition with "zero confusions" on shuffled sheets in grayscale and in all three CVD simulations (also spec.md:52). R16 sets no looser S1 threshold.

| Position | Named | Key | Result |
|---|---|---|---|
| r1c1 | assembler (gear), paused/warning unsure | assembler-paused | type right, state coin-flip |
| r1c2 | miner (wedge), state unsure | miner-warning | type right, state coin-flip |
| r1c3 | slot-selected | slot-selected | correct |
| r1c4 | ingot | ingot | correct |
| r2c1 | miner, run | miner-run | correct only in CVD (yellow dot); not separable in grayscale |
| r2c2 | slot-empty | slot-empty | correct |
| r2c3 | assembler inactive | assembler-inactive | correct |
| r2c4 | assembler run | assembler-run | correct |
| r3c1 | ore | ore | correct |
| r3c2 | slot-locked | slot-locked | correct |
| r3c3 | miner inactive | miner-inactive | correct |
| r3c4 | assembler, paused/warning unsure | assembler-warning | type right, state coin-flip |
| r4c1 | miner, state unsure | miner-paused | type right, state coin-flip |
| r4c2 | slot-occupied | slot-occupied | correct |

| Sheet | Identity (type, slot, resource) | Full naming incl. machine state, certain | Confusions |
|---|---|---|---|
| Grayscale, A and B | 14/14 | 9/14 | 3 miners (run/warning/paused) not separable; 2 assemblers (paused/warning) coin-flip |
| Deuteranopia, A and B | 14/14 | 10/14 | the yellow run dot identifies miner run; miner warning vs paused and assembler paused vs warning remain |
| Protanopia, A and B | 14/14 | 10/14 | same as deuteranopia |
| Tritanopia, A and B | 14/14 | 10/14 | same pattern (red light renders pink, cyan body) |

Confusions:
1. In grayscale, miner warning, paused and run (same body value).
2. Miner warning vs paused, in every filter.
3. Assembler paused vs warning, in every filter.

There were no confusions across kinds (miner vs assembler, slot vs machine, ore vs ingot) or between slot conditions.

**Result at zero confusions: A FAIL, B FAIL. They are tied, so the gate does not discriminate between the projections.** The cause is state cues only. S1 has body variants and no cue icons, and warning differs from paused by a few red art pixels. The fix belongs to S3, where cue icons of at least 12×12 art px are tested. The naming gate must be re-run on the S3 winner for SC-001.

#### 2. Gate review

1. Silhouette IoU (A 0.759 / 0.789 FAIL; B 0.651 / 0.672 PASS against 0.70): AGREE. A's miner and assembler both sit in a wide, flat slab. B's toothed gear and chamfered wedge differ clearly at 1×.
2. Naming: FAIL for both, tied, on state cues only (section 1).
3. Cue legibility: no UI/UX verdict given. The warning light is tiny in both, and miner warning vs paused cannot be told apart at 1× or at n_min (phoneP shots, n=2 shown). Run and inactive read well.
4. Palette lock (PASS, 48/48 cap): AGREE, accepted from the record. Contrast against surfaces is not run (T424), so this is not an S1 contrast pass.
5. Reproducibility (PASS, sampled on one asset): AGREE as "sampled". One asset does not prove all 28.
6. Crisp rendering: AGREE PASS with a caveat. 2 % of vertical edges sit off-grid because of half-pixel sprite pivots, equally in A and B. Fix pivots to whole art pixels in the Art Bible before the freeze, or they shimmer under pan (shimmer not measured).
7. Fit-all (PASS in all 24 shots): AGREE. Phone landscape 1280×720 has n_fit 2 < n_min 3 (T427, 1 % tolerance), so fit-all is the only level there and there is no zoom-out. This is a standing risk.
8. Overhang, pivot, canvas, outline, alpha and frames (204 checks, 0 failures): AGREE, accepted from the record.
9. Effort per asset: AGREE it is not measured. This is a record gap, not a pass.
- B's plates overlap on the rectangular lattice (tabL-B-fit, phoneL-B-fit): confirmed. This is a cost of the spike layout, not an art defect. It needs a real isometric slot lattice for spacing, hit-testing and sort order.

Art Director judgement at n_min and fit-all:
- Silhouette readability: B is better than A machine against machine.
- Perspective: B is consistent. A's slab reads as a flat box.
- Grid readability: A's plates sit naturally on the F003 lattice. B's diamonds overlap and need an isometric lattice.
- State readability: run and inactive are strong. Warning vs paused is weak in both. B's bodies are smaller at n_min.
- Ore and ingot are small but identifiable in both.

#### 3. Verdict on "B isometric 2:1, default if no preference B": AGREE WITH CONDITIONS

B is the only candidate that passes the silhouette gate, which R16 treats as the highest-risk readability item. A fails it by 0.06–0.09, and its lattice advantage is a layout cost, not a readability one. Conditions for T417:
1. B requires an isometric slot lattice before production: plate spacing, row sorting, footprint-only hit-testing per R04, and overhang within the 0.5-cell gap. Record this as an accepted cost owned by a named task.
2. Warning vs paused is unresolved. The S1 naming gate fails for both, so choosing B does not solve SC-001. S3 must prove cue icons (at least 12×12 art px and at least 24 pt at n_min) on B, and the full shuffled gate must be re-run.
3. S2 (D = 16 vs 24) must confirm that B still reads at the smallest scale on phone portrait. phoneP-B-nmin shows bodies about 70–90 device px wide at n=2.
4. The half-art-pixel pivots must be resolved in the Art Bible rules before the freeze.
5. B stays the default only if the owner has no preference for A and accepts the costs above. Choosing A would need redrawn machines to pass the 0.70 IoU gate, which is a new bounded candidate round per R16.

#### 4. Risks for S2 and production

- State cues fail at the floor. S2 at D = 16 would shrink the red cue pixels further. S3 icons are a hard dependency, so S2 and S3 sign-off needs a shuffled-gate re-run.
- B's isometric lattice is unproven. Overlap, hit-testing and sorting have not been tested.
- Phone landscape 1280×720 has n_fit < n_min (T427), so fit-all is its only level. Phone portrait and tablet portrait also have no zoom-out range.
- Half-pixel pivots leave about 2 % of vertical edges off-grid. Shimmer under pan is not measured.
- Evidence limits: the Windows shots use the viewer's approximated scale; the emulator is the only device path; contrast was not run; effort was not measured; reproducibility was sampled.

### Owner decision

Owner decision 2026-10-28 (T417, #529): **B, isometric 2:1**, the agents' recommended option and default.

Reason: B is the only candidate that passes the measured silhouette gate (IoU 0.651 / 0.672 against 0.70); A fails it and would need a new candidate round. The owner accepts the costs and the five conditions of the second-session Art Director review (§ 3 above). They are carried as follows:

1. Isometric slot lattice (plate spacing, row sorting, footprint-only hit testing per R04, overhang within the 0.5-cell gap): new task **T456**, after T426, T427 and T428.
2. Warning vs paused: S3 (T421/T422) proves cue icons (at least 12×12 art px, at least 24 pt at `n_min`) on B, and the full shuffled naming gate is re-run.
3. S2 (T418/T419) confirms that B still reads at the smallest scale on phone portrait.
4. The half-art-pixel pivots are resolved in the Art Bible rules (T424) before the freeze.
5. B is the owner's choice; no redo round for A.

T418 (S2) is ready.

## S2 Density: D16 vs D24 on isometric 2:1 (T419, #531, FR-007, FR-011, SC-005)

Evidence author: Test/QA session with UI/UX Reviewer lens (Sonnet), 2026-10-29, base `bf971fa` (T418, PR #599 merged). The evidence author did not produce the art and gives **no Art Director verdict**; the independent second-session Art Director review and the owner decision (T420) are separate steps (empty subsections at the end). Projection B (isometric 2:1) only, per the owner decision of 2026-10-28.

### Candidates

| Label | Density | Assets | Source |
|---|---|---|---|
| **D16** | 16 art px per cell, canvas 54x44 (slot/machine) and 22x16 (ore/ingot) | 24 imported sprites (14 assets, running machines 6 frames) | `art-source/f004/spike/s2/pixel/D16/`, imported at `Assets/IdleFactory/Art/F004/Spike/D16/isometric-2-1/` (the S1 model scaled by 16/24 and rendered again, T418) |
| **D24** | 24 art px per cell, canvas 80x64 and 32x24 | the same 24 sprites, frames pixel-identical to the S1 isometric set (`d24MatchesS1Isometric`) | `art-source/f004/spike/s2/pixel/D24/`, imported at `Assets/IdleFactory/Art/F004/Spike/D24/isometric-2-1-s2/` |

Palette v0 for both. The 12 slots of every screenshot are the S1 slots in the same reading order (Miner running, Assembler running, Miner warning, Assembler paused / Miner inactive, Assembler inactive, empty slot, selected slot / locked slot, occupied plate, ore, ingot; running machines are single frames of the animation).

### How the in-game evidence was made (read before interpreting)

- Same method as § S1, with a **temporary player that is never committed and was removed before this commit**: a scene with the S1 viewer logic (`CameraScaleModel`, `SlotGridLayout`, 12 slots on the rectangular Feature 003 lattice) reading the S2 sprites of both densities, with `artPixelsPerCell` 16 or 24, readable cell 24 pt, `maxScale` 6. The committed `SpikeScaleViewer` and scene were not changed (the committed viewer is hard-wired to the `s1-` sprite names and D24).
- **Windows player, offscreen render** (phone and tablet classes) at the exact physical resolution into a point-filtered render texture, display density overridden by a command-line value: `phoneL` 1280x720 at 320 dpi, `phoneP` 720x1280 at 320 dpi, `tabL` 2048x1536 at 264 dpi, `tabP` 1536x2048 at 264 dpi. Not captured on physical devices; none was used.
- **Android emulator `Medium_Phone_API_36.0`** (`emulator-5554`, 1080x2400, 420 dpi), cold boot with `-gpu host`, GLES3-only IL2CPP ARM64 Development APK (built from the temporary scene with the emulator-only GLES3 override, restored afterwards), screenshots by `adb screencap`. The on-screen label and the D16 / D24 / n_min / Fit-all buttons of the temporary viewer are visible and were driven with `adb shell input tap`. Portrait `emuP` and landscape `emuL` (`cmd window user-rotation lock`). Emulator-only evidence, not physical-device, performance or release evidence.
- **Approximation limits (as in S1).** The viewer approximates pixels per point as `Screen.dpi / 160` (2.0 at 320 dpi, 1.65 at 264 dpi, 2.625 at 420 dpi) and fits against the whole screen with no HUD. The production rule uses `ShellPanelScale` (effective px per pt, 2.004 for the 720x1280 and 1280x720 references) and the HUD-aware rectangle. The zoom-level table below (from `ScreenClassScaleTableTests`) governs; the screenshots show the viewer's `n`. Because 2.0 is 0.2 % below 2.004, the viewer's `n_min` on the 320 dpi phones is one lower than the authoritative one (D24: 2 instead of 3; D16: 3 instead of 4). To also show the authoritative `n_min`, the Windows classes were captured a second time with the density set to `px per pt x 160` (320.64 dpi phones, 323.52 dpi `tabP`, 298.72 dpi `tabL`); those files are named `...-nminAuth.png`. The `n_fit` of the shots is the whole-screen fit; the authoritative `n_fit` is in the table.
- Like S1, the viewer draws the diamond plates on the rectangular lattice (plate 48x24 art px at D16, 70x35 at D24, slot spacing 2.5 cells), so plates overlap neighbours: plate width / spacing is 1.20 at D16 and 1.17 at D24. The isometric lattice (T456) is not part of this spike.
- The measured plate, ore and ingot widths in the portrait shots equal art px x n exactly (e.g. D16 n=4: 192 / 48 / 56 px; D24 n=3: 210 / 51 / 57 px), so the shown `n` is the real scale.

### Contact sheets (1x)

Shuffled by T418 per density; each sheet shows the 14 tiles at native size. The answer keys are `*_key.json`; a reviewer who names the tiles must not be given them. The evidence author did not open the keys.

| Density | Grayscale | Deuteranopia | Protanopia | Tritanopia | Key |
|---|---|---|---|---|---|
| D16 | [grayscale](style-spike/s2/sheets/isometric-2-1-D16_grayscale.png) | [deut](style-spike/s2/sheets/isometric-2-1-D16_deuteranopia.png) | [prot](style-spike/s2/sheets/isometric-2-1-D16_protanopia.png) | [trit](style-spike/s2/sheets/isometric-2-1-D16_tritanopia.png) | [key](style-spike/s2/sheets/isometric-2-1-D16_key.json) |
| D24 | [grayscale](style-spike/s2/sheets/isometric-2-1-D24_grayscale.png) | [deut](style-spike/s2/sheets/isometric-2-1-D24_deuteranopia.png) | [prot](style-spike/s2/sheets/isometric-2-1-D24_protanopia.png) | [trit](style-spike/s2/sheets/isometric-2-1-D24_tritanopia.png) | [key](style-spike/s2/sheets/isometric-2-1-D24_key.json) |

The same sheets, with the pixel files and provenance, are in `art-source/f004/spike/s2/sheets/D16/` and `D24/`.

### On-device screenshots (D16 / D24, n_min and fit-all)

Folder `style-spike/s2/shots/`, file name `<class>-<D16|D24>-<nmin|fit>.png` (viewer scale) and `<class>-<D>-nminAuth.png` (Windows classes at the authoritative px per pt). "n shown" is the integer scale in the file.

| Class | Screen (px) | D16 n_min | D16 fit-all | D24 n_min | D24 fit-all | n shown D16 (n_min / fit) | n shown D24 (n_min / fit) | nminAuth (n) |
|---|---|---|---|---|---|---|---|---|
| Phone landscape (Windows) | 1280x720 | [a](style-spike/s2/shots/phoneL-D16-nmin.png) | [a](style-spike/s2/shots/phoneL-D16-fit.png) | [a](style-spike/s2/shots/phoneL-D24-nmin.png) | [a](style-spike/s2/shots/phoneL-D24-fit.png) | 3 / 5 | 2 / 3 | [D16](style-spike/s2/shots/phoneL-D16-nminAuth.png) 4, [D24](style-spike/s2/shots/phoneL-D24-nminAuth.png) 3 |
| Phone portrait (Windows) | 720x1280 | [a](style-spike/s2/shots/phoneP-D16-nmin.png) | [a](style-spike/s2/shots/phoneP-D16-fit.png) | [a](style-spike/s2/shots/phoneP-D24-nmin.png) | [a](style-spike/s2/shots/phoneP-D24-fit.png) | 3 / 5 | 2 / 3 | [D16](style-spike/s2/shots/phoneP-D16-nminAuth.png) 4, [D24](style-spike/s2/shots/phoneP-D24-nminAuth.png) 3 |
| Tablet landscape (Windows) | 2048x1536 | [a](style-spike/s2/shots/tabL-D16-nmin.png) | [a](style-spike/s2/shots/tabL-D16-fit.png) | [a](style-spike/s2/shots/tabL-D24-nmin.png) | [a](style-spike/s2/shots/tabL-D24-fit.png) | 3 / 6 | 2 / 6 | [D16](style-spike/s2/shots/tabL-D16-nminAuth.png) 3, [D24](style-spike/s2/shots/tabL-D24-nminAuth.png) 2 |
| Tablet portrait (Windows) | 1536x2048 | [a](style-spike/s2/shots/tabP-D16-nmin.png) | [a](style-spike/s2/shots/tabP-D16-fit.png) | [a](style-spike/s2/shots/tabP-D24-nmin.png) | [a](style-spike/s2/shots/tabP-D24-fit.png) | 3 / 6 | 2 / 6 | [D16](style-spike/s2/shots/tabP-D16-nminAuth.png) 4, [D24](style-spike/s2/shots/tabP-D24-nminAuth.png) 3 |
| Emulator portrait | 1080x2400 | [a](style-spike/s2/shots/emuP-D16-nmin.png) | [a](style-spike/s2/shots/emuP-D16-fit.png) | [a](style-spike/s2/shots/emuP-D24-nmin.png) | [a](style-spike/s2/shots/emuP-D24-fit.png) | 4 / 6 | 3 / 5 | not needed (ppp 2.625 vs 2.631 gives the same `n_min`) |
| Emulator landscape | 2400x1080 | [a](style-spike/s2/shots/emuL-D16-nmin.png) | [a](style-spike/s2/shots/emuL-D16-fit.png) | [a](style-spike/s2/shots/emuL-D24-nmin.png) | [a](style-spike/s2/shots/emuL-D24-fit.png) | 4 / 6 | 3 / 5 | same |

All 12 slots are visible in every shot (checked by counting the slot rows and clusters in every file, and by viewing the shots listed under the observations). 32 files in total. Not captured: any physical device; the phone and tablet classes on the emulator (one AVD).

### Zoom-level and margin table per screen class (D16 and D24 separately; readable cell 24 pt, `maxScale` 6)

Source: `ScreenClassScaleTableTests` "physical reference devices" table, run on this commit (class run: 7 of 7 passed): effective px per pt from `ShellPanelScale.Decide`, HUD-aware rectangle from the test's fixed model of the closed shell. `levels` = `maxScale - n_min + 1`; "at or below fit" = `n_fit - n_min + 1` (1 means fit-all only: pinch and zoom-out do nothing). The table does not depend on the projection.

| Screen class (reference device) | px/pt | D16 n_min | D16 n_fit | D16 levels | D16 at or below fit | D16 margin | D24 n_min | D24 n_fit | D24 levels | D24 at or below fit | D24 margin |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Phone portrait 720x1280, 320 dpi | 2.004 | 4 | 4 | 3 | **1 (fit-all only)** | 27.2 % | 3 | 3 | 4 | **1 (fit-all only)** | 18.2 % |
| Phone landscape 1280x720, 320 dpi | 2.004 | 4 | 4 | 3 | **1 (fit-all only)** | 19.9 % | 3 | 2 | 4 | **0: violation** | 40.0 % |
| Phone portrait 1080x2400, 420 dpi (emulator) | 2.631 | 4 | 6 | 3 | 3 | 28.8 % | 3 | 4 | 4 | 2 | 28.8 % |
| Phone landscape 2400x1080, 420 dpi (emulator) | 2.631 | 4 | 6 | 3 | 3 | 22.7 % | 3 | 4 | 4 | 2 | 22.7 % |
| Phone portrait 1170x2532, 460 dpi | 2.881 | 5 | 6 | 2 | 2 | 34.1 % | 3 | 5 | 4 | 3 | 17.7 % |
| Phone landscape 2532x1170, 460 dpi | 2.881 | 5 | 6 | 2 | 2 | 28.5 % | 3 | 4 | 4 | 2 | 28.5 % |
| Tablet portrait 1536x2048, 264 dpi | 2.022 | 4 | 5 | 3 | 2 | 20.3 % | 3 | 3 | 4 | **1 (fit-all only)** | 28.3 % |
| Tablet landscape 2048x1536, 264 dpi | 1.867 | 3 | 6 | 4 | 4 | 28.7 % | 2 | 5 | 5 | 4 | 10.9 % |
| Desktop 1280x720 / 1920x1080 / 2560x1440 | 1 / 1.5 / 2 | 2 / 3 / 3 | 5 / 6 / 6 | 5 / 4 / 4 | 4 / 4 / 4 | 12.4 / 30.0 / 47.5 % | 1 / 2 / 2 | 3 / 5 / 6 | 6 / 5 / 5 | 3 / 4 / 5 | 21.2 / 12.4 / 21.2 % |

Reading for the owner:

- **Single-level range on the worst phone (720x1280 and 1280x720, 320 dpi): fit-all only at D16 and at D24.** Zoom only goes in (toward `maxScale`), never out. D24 additionally has the portrait tablet at fit-all only; D16 gives it two levels at or below fit-all (4 and 5).
- **The 1280x720 phone: D24 is the known violation** (`n_fit` 2 < `n_min` 3; owner decision 2026-10-28: `n_min` gets a 1 % tolerance, implemented by T427; with it D24 `n_min` is 2 and fit-all only). **D16 has no violation there** (`n_min` = `n_fit` = 4) with a 19.9 % margin and larger on-screen art (cell 31.9 pt).
- D16 has fewer allowed levels on the 460 dpi phones (2 levels, 5 and 6, because `n_min` rises to 5); D24 keeps 4 levels with 3 or 2 at or below fit-all. On large screens (tablet landscape, desktop) D16 has as many levels below fit-all as D24 or more.
- The D = 32 rows and the panel-point screens are in the test output; D = 32 is out of S2 scope.

### Gate results (D16 vs D24)

| # | Gate (R16) | D16 | D24 | Evidence / owner |
|---|---|---|---|---|
| 1 | Silhouette IoU, Miner vs Assembler, at most 0.70 | **FAIL**: 0.704 full, 0.739 at 32 px (pair assembler-run / miner-run; 0.004 and 0.039 over the limit) | **PASS**: 0.651 full, 0.672 at 32 px (frames identical to the S1 isometric set) | `art-source/f004/spike/s2/check-report.json` (`silhouette_iou[D16]`, `[D24]`, T418). Measured, not a review. |
| 2 | Grayscale / CVD naming (SC-001 method) | pending (sheets above); the S1 result stands: state cues only, and S2 has no cue icons | pending, same | Independent agent reviewer session, transcript in `reviews/readability-*.md` (not this author). |
| 3 | Cue legibility at `n_min` and fit-all (UI/UX lens) | see "Cue legibility at `n_min`" below; formal verdict pending | same | S2 has body variants only and no cue icons; the icon rule (>= 12x12 art px, >= 24 pt at `n_min`) is the S3 gate (T421/T422). |
| 4 | Palette lock | PASS: 29 of 48 colors, group budgets kept | PASS: 30 of 48 colors, group budgets kept | `palette_group_budget[D16]` / `[D24]`, `palette_definition` 48 of 48 cap. Contrast against declared surfaces: **not run** (needs the Art Bible surfaces, T424). |
| 5 | Reproducibility | PASS (sampled): `s2-miner-run-00` byte-identical (sha256 `a74be031...`) | PASS (sampled): byte-identical (sha256 `5f101885...`) | `reproducibility[D16]` / `[D24]`; one asset per density, not all 24. Golden hashes belong to the art-pipeline tests, not re-run here. |
| 6 | Crisp rendering (uniform n x n blocks at rest) | PASS at n = 4, 5, 6; **not uniform at n = 3** | PASS horizontally at every n; vertical 97.5 % on the grid | Measured on all 32 screenshots (below). |
| 7 | Fit-all rule (all 12 slots, `n_fit >= n_min`) | PASS: all 12 visible in every shot; `n_fit >= n_min` on every row of the table | PASS in the shots; `n_fit < n_min` on the 1280x720 phone (known, T427) | Table above. The HUD-aware proof is the Play Mode variant of T427, not this spike. |
| 8 | Overhang / pivot / canvas / outline / alpha / frames | PASS | PASS | `check-report.json`: 204 checks across 28 assets (14 per density), 0 failures. The pivot check requires the footprint, not whole art pixels (see below). |
| 9 | Effort per asset | not measured here; T418 timings only (D16 models 16.1 s, D24 4.7 s, renders about 3 s each) | same | Human review effort is not in this record. |

**Gate 6 measurement.** Per screenshot (the viewer label and buttons excluded): every horizontal run of equal color between the first and last run of a row was tested for being a multiple of the shown `n` (and the gcd of all runs taken), and the share of vertical color transitions whose row index modulo `n` sits on the dominant residue was taken.

| Shots (n shown) | Runs that are multiples of n | gcd of runs | Vertical edges on the dominant grid |
|---|---|---|---|
| D24, 16 files (Windows n = 2, 3, 6; emulator n = 3, 5) | 100 % | n (exact) | 96.8 to 97.5 % |
| D16, 11 files (Windows n = 4, 5, 6; emulator n = 4, 6) | 100 % | n (exact) | 100 % |
| **D16, Windows, n = 3** (`phoneL`, `phoneP`, `tabL`, `tabP` `-D16-nmin`, and `tabL-D16-nminAuth`; 5 files) | **95.1 %** | **1** | 100 % |

The D16 defect at n = 3 comes from the half-art-pixel pivot in x (below): the plate and the machine of a slot sit 1 to 2 device px off each other's block grid, so runs of 2, 5, 10, 11 and 13 px appear at plate and body edges. The authoritative `n_min` of D16 is 3 on tablet landscape (affected), 4 on the 320 dpi phones, the portrait tablet and the emulator (even, clean) and 5 on the 460 dpi phones (odd, not measured). Shimmer under pan is not measured.

**Half-art-pixel pivots (T424 open).** From `candidates-D16.json` and `candidates-D24.json`: D16 slot and machine sprites have pivot x = 26.5 art px (canvas 54 wide, odd) and ore/ingot x = 10.5; y is whole (10.0 and 3.0). D24 has x whole (40.0, 16.0) and y whole (15.0) for slot and machine, but ore/ingot y = 4.5. Measured consequences: D24 has 2.5 % of vertical edges off the grid (1 to 2 px at n = 3, 3 px at tabL n = 6), from ore and ingot only; D16 has horizontal runs off the grid at odd n (above).

### Cue legibility at `n_min` (UI/UX lens observations, not approval)

Authoritative `n_min` per class, effective px per pt from the table. Art sizes are the opaque bounding boxes of the sprites, and the on-screen widths were verified on the portrait shots (plate, ore and ingot measured equal to art px x n). pt = device px / effective px per pt. Sizes are given as "D16 / D24" art px in the header.

| Class (px per pt) | Density, n_min | Cell | Miner body (27x19 / 38x27 art px) | Assembler body (34x20 / 50x30) | Plate (48 / 70 wide) | Ore (12x9 / 17x12) | Ingot (14x10 / 19x13) | Warning-vs-paused patch, miner (5x4 / 7x4 art px) | 12x12 icon | art px for 24 pt at n_min |
|---|---|---|---|---|---|---|---|---|---|---|
| 320 dpi phones (2.004) | D16, 4 | 64 px / 31.9 pt | 108 px / 53.9 pt | 136 / 67.9 | 192 / 95.8 | 48 / 24.0 | 56 / 27.9 | 20x16 px / 10.0x8.0 pt | **24.0 pt** (23.95) | 13 |
| same | D24, 3 | 72 px / 35.9 pt | 114 px / 56.9 pt | 150 / 74.9 | 210 / 104.8 | 51 / 25.4 | 57 / 28.4 | 21x12 px / 10.5x6.0 pt | 18.0 pt | 17 |
| Emulator 420 dpi (2.631) | D16, 4 | 64 px / 24.3 pt | 108 px / 41.0 pt | 136 / 51.7 | 192 / 73.0 | 48 / 18.2 | 56 / 21.3 | 20x16 px / 7.6x6.1 pt | 18.2 pt | 16 |
| same | D24, 3 | 72 px / 27.4 pt | 114 px / 43.3 pt | 150 / 57.0 | 210 / 79.8 | 51 / 19.4 | 57 / 21.7 | 21x12 px / 8.0x4.6 pt | 13.7 pt | 22 |
| Tablet portrait (2.022) | D16, 4 | 64 px / 31.7 pt | 108 / 53.4 | 136 / 67.3 | 192 / 95.0 | 48 / 23.7 | 56 / 27.7 | 20x16 px / 9.9x7.9 pt | 23.7 pt | 13 |
| same | D24, 3 | 72 px / 35.6 pt | 114 / 56.4 | 150 / 74.2 | 210 / 103.9 | 51 / 25.2 | 57 / 28.2 | 21x12 px / 10.4x5.9 pt | 17.8 pt | 17 |
| Tablet landscape (1.867) | D16, 3 | 48 px / 25.7 pt | 81 px / 43.4 pt | 102 / 54.6 | 144 / 77.1 | 36 / 19.3 | 42 / 22.5 | 15x12 px / 8.0x6.4 pt | 19.3 pt | 15 |
| same | D24, 2 | 48 px / 25.7 pt | 76 px / 40.7 pt | 100 / 53.6 | 140 / 75.0 | 34 / 18.2 | 38 / 20.4 | 14x8 px / 7.5x4.3 pt | 12.9 pt | 23 |

Observations. Viewed: phoneP D16 nmin and nminAuth, phoneP D24 nmin, phoneL D16 and D24 nminAuth, the emulator portrait probe (D24 n_min) and emuL D16 fit. The rest was assessed by measurement only.

- **Machine type, lit versus inactive, and the slot conditions read at `n_min` in both densities.** Miner (wedge with conveyor stripes) and Assembler (gear, teal body) differ clearly in outline; inactive bodies are dark and desaturated; selected (blue diamond frame), locked (hatched plate with a small lock block), empty (corner marks) and occupied plates are distinct. At the authoritative `n_min` on the 320 dpi phone all of this reads at both densities; at the viewer's n = 2 (D24) the details are small but the classes stay distinguishable.
- **Warning versus paused versus running is unresolved at both densities** (Art Director condition 2). The three states differ only in a few art pixels: warning vs paused differ in 13 px (miner, 5x4 bounding box) at D16 and 24 px (7x4) at D24, and in 36 px (8x5) and 76 px (10x8) for the assembler. At `n_min` that is a patch of about 10 x 6 to 8 pt on a 320 dpi phone and 8 x 5 to 6 pt on the emulator. The miner's red bead is visible next to the yellow bead of running, but warning and paused bodies are not separable by value. D16 has the smaller patch in art pixels and about the same in pt (its `n_min` is higher). No cue icon exists in S2.
- Ore and ingot are small and identifiable in both: 24.0 / 27.9 pt (D16) and 25.4 / 28.4 pt (D24) wide on the 320 dpi phones; 18 to 22 pt on the emulator and on tablet landscape for both densities.
- **D16 is a scaled S1 model** (QA warning from #530): thin features are lost, visible as a cruder plate bevel and corner marks, fewer and wider conveyor stripes, fewer gear teeth, a smaller lock block on the locked plate and a thinner selection frame with fewer rivets. The silhouettes stay readable. D24 keeps the S1 detail.
- Plates overlap neighbours in both densities (1.20 versus 1.17 of the slot spacing), as in S1.
- Fit-all: comfortable in both densities on every class; no cue clipped.

### Art Director condition 3: does B still read at the smallest scale on phone portrait?

Measured body widths at `n_min` on phone portrait (bodies are the machine sprites; widths = art px x n; plate, ore and ingot widths of the same shots measured equal):

| Capture | D16: n, miner / assembler width | D24: n, miner / assembler width |
|---|---|---|
| Windows phoneP, viewer scale (the S1 shot method) | n = 3: 81 / 102 px | n = 2: 76 / 100 px |
| Windows phoneP, authoritative `n_min` (`...-nminAuth`) | n = 4: 108 / 136 px (53.9 / 67.9 pt) | n = 3: 114 / 150 px (56.9 / 74.9 pt) |
| Emulator portrait (1080x2400) | n = 4: 108 / 136 px (41.0 / 51.7 pt) | n = 3: 114 / 150 px (43.3 / 57.0 pt) |

The Art Director's "bodies about 70 to 90 device px wide at n = 2" matches D24 at the viewer's n = 2 (miner 76 px, assembler 100 px); the authoritative `n_min` on that phone is 3, where the bodies are 114 and 150 px. **At the authoritative `n_min` the D16 bodies (108 and 136 px) are within 5 to 10 % of the D24 bodies (114 and 150 px), so size does not separate the densities; D16 reaches it with fewer art pixels (miner 27x19 instead of 38x27).** On this evidence B still reads at the smallest scale on phone portrait in both densities (type, lit, inactive, slot conditions). What does not read in either density is warning versus paused, which is a cue-icon question for S3. This is the agents' reading; the Art Director review judges the condition.

### Room for a 12x12 art px cue icon at >= 24 pt at `n_min` (Art Director condition 2, S3 gate)

Arithmetic: icon pt = 12 x n_min / (px per pt); the rule needs 24 pt. S2 has no cue icons; this only states which density leaves room.

- **D16** gives 24.0 pt on the 320 dpi phones (23.95, 0.2 % below 24) and 23.7 pt on the portrait tablet, but 19.3 pt on tablet landscape and 18.2 pt on the 420 dpi emulator phone. A 12 px icon is 44 % of the miner body width (27) and fits the body height (19).
- **D24** gives 18.0 pt on the 320 dpi phones, 17.8 pt on the portrait tablet, 13.7 pt on the emulator and 12.9 pt on tablet landscape. Reaching 24 pt needs 17 art px (45 % of the 38 px miner body) on the phones and 22 to 23 on the emulator and tablet landscape; a 12 px icon is 32 % of the body.
- Neither density meets 12x12 art px at >= 24 pt on every class. D16 comes closest (within 1.2 % on the 320 dpi phones and the portrait tablet; the largest need is 16 art px), because its `n_min` is higher relative to the art pixel; D24 needs up to 23 art px. S3 (T421/T422) decides the icon size and must state which parameter changes (icon art px, the 24 pt floor or the density).

### Evidence limits (carried)

- (a) **No automated test covers the S2 import folders.** `SpikeImportTests` (4 tests, 4 passed on this commit) compares the S1 D24 folders `D24/{oblique-3-4,isometric-2-1}` with the S1 sources and tests the path-to-density rule; `D16/isometric-2-1` and `D24/isometric-2-1-s2` have no sprite-level test. The measured plate, ore and ingot widths in the shots are the only on-device check that the importer applied PPU = D and point filtering. No test was added (out of scope).
- (b) D16 is a scaled S1 model (16/24); thin features are lost (observations above).
- (c) Half-art-pixel pivots remain open (T424); their effects are reported under gate 6.
- The Windows classes use the viewer's approximated scale; the `nminAuth` files use the authoritative px per pt for `n_min` but a whole-screen fit, not the HUD-aware fit. The emulator is the only device path (one AVD). Contrast, effort and shimmer under pan were not measured; reproducibility was sampled. No physical device was used.

### Recommended option and default

- **Agents' recommended option (evidence author's reading of the gates, not a decision): D24.** It is the only density that passes the measured silhouette gate (0.651 / 0.672 against 0.70) and keeps the S1 detail; D16 fails it by 0.004 (full) and 0.039 (32 px) on the pair assembler-run / miner-run and is the S1 model scaled down with thin features lost. D24's costs: the 1280x720 phone violation (T427 tolerance), less icon room (up to 23 art px needed for a 24 pt icon at `n_min`) and the portrait tablet at fit-all only. D16's advantages: no 1280x720 violation, more icon room per art pixel, fewer art pixels per asset (effort not measured). D16's costs: the crispness defect at odd n = 3 from the half-pixel x pivot (tablet landscape), fewer levels on the 460 dpi phones, and the failed IoU gate, which a redrawn D16 silhouette could still fix in a bounded candidate round.
- **Default if no preference: D24** (the only density with no failed measured gate; gates 2 and 3 pending for both). The recommendation is the agents' proposal; the independent Art Director review below may amend it, and the owner decides in T420.
- Second-session Art Director: AGREE WITH CONDITIONS (see review)

### Second-session Art Director review

Independent second-session Art Director review with blind grayscale/CVD naming, on evidence commit `e2a7492`. Read-only Sonnet session (Medium tier), not the producer (T418), brief author or evidence author (T419). Received 2026-10-29. The parent recorded it with the prose shortened; the naming table, scores, confusions, gate results, numbers, verdict and conditions are kept as the reviewer gave them.

Review limits:
- All 14 tiles were named from the sheets before any `*_key.json` was opened; `art-source/f004/spike/s2/**/*.json` was not opened.
- Sheets viewed: D16 grayscale, D16 tritanopia, D24 grayscale, D24 deuteranopia. The D16 deuteranopia/protanopia and D24 protanopia/tritanopia sheets were not opened; the CVD scoring rests on the two CVD sheets viewed.
- Shots viewed: `phoneP-D16-nminAuth` and `phoneP-D24-nminAuth`. Gates 1, 4, 5, 8, 9 and the zoom table are accepted from the record. Spec, research and wiki items were read only through the quotes in this file. The icon arithmetic was recomputed.

#### 1. Blind naming

Both densities share the same shuffled order (seed 418, 4 columns, 14 tiles); the reviewer's names are identical for D16 and D24 and for every filter viewed.

| Position | Named | Key | Result |
|---|---|---|---|
| r1c1 | slot-empty (plain bevelled plate) | slot-occupied | WRONG slot condition |
| r1c2 | miner, run (bright dot) | miner-run | correct; in grayscale not separable from r1c4/r4c2, the run dot is clear only in colour/CVD |
| r1c3 | ingot | ingot | correct |
| r1c4 | miner, warning/paused unsure | miner-warning | type right, state coin-flip |
| r2c1 | slot-occupied (dark striped plate, cube) | slot-locked | WRONG slot condition |
| r2c2 | assembler inactive | assembler-inactive | correct |
| r2c3 | ore | ore | correct |
| r2c4 | assembler, warning/paused unsure | assembler-warning | type right, state coin-flip |
| r3c1 | slot-locked (plate with X/corner marks) | slot-empty | WRONG slot condition |
| r3c2 | assembler run (bright centre) | assembler-run | correct |
| r3c3 | miner inactive (dark wedge) | miner-inactive | correct |
| r3c4 | assembler, warning/paused unsure | assembler-paused | type right, state coin-flip |
| r4c1 | slot-selected (frame) | slot-selected | correct |
| r4c2 | miner, warning/paused unsure | miner-paused | type right, state coin-flip |

| Sheet | Identity (kind) | Full naming incl. state and slot condition, certain | Confusions |
|---|---|---|---|
| D16 and D24 grayscale | 14/14 | 6/14 (miner-run not separable by value) | 3 slot conditions; 3 miner states (run/warning/paused); 2 assembler states (warning/paused) |
| D16 tritanopia and D24 deuteranopia | 14/14 | 7/14 (the yellow run dot works) | 3 slot conditions; miner warning vs paused; assembler warning vs paused |

Confusions: (1) slot empty, occupied and locked, a new failure versus S1 on the same D24 art: a bare plate tile has no cue for "occupied", so the reviewer treats it as a sheet-design and convention issue rather than an art defect, but SC-001 still requires zero confusions; (2) and (3) warning vs paused for miner and assembler, as in S1; (4) D16 is worse on slot-locked (the lock block shrinks to a small grey blob in grayscale; D24 shows a clear white block). No confusions across kinds (miner vs assembler, slot vs machine, ore vs ingot) in either density.

**SC-001 (zero confusions): D16 FAIL, D24 FAIL, tied as in S1.** Gate 2 "pending" becomes "fail on both, no discrimination between densities".

#### 2. Gates (D16 / D24)

1. Silhouette IoU: AGREE. D16 FAIL 0.704 full and 0.739 at 32 px (the 32 px figure is the meaningful miss, 0.039 over); D24 PASS 0.651 / 0.672.
2. Naming: FAIL on both (above).
3. Cue legibility: no verdict. Run and inactive read; warning vs paused does not, at either density. Cue patches are 5x4 / 7x4 art px (miner) and 8x5 / 10x8 (assembler).
4. Palette lock: AGREE as recorded (29 / 30 of 48); contrast still not run (T424).
5. Reproducibility: AGREE as "sampled".
6. Crisp rendering: AGREE. D16 not uniform at n = 3 on Windows (95.1 % of runs, gcd 1) from the half-art-pixel x pivot; D24 vertical edges 96.8 to 97.5 % on grid from ore/ingot pivot y = 4.5. Both are a T424 fix.
7. Fit-all: AGREE. Standing risk: on the 720x1280 and 1280x720 320 dpi phones both densities are fit-all only (no zoom-out).
8. Overhang, pivot, canvas, outline, alpha, frames: AGREE as recorded.
9. Effort: not measured; a record gap, not a pass.

#### 3. Answers

**Condition 3 (B at the smallest scale on phone portrait): YES at both densities**, checked on `phoneP-D16-nminAuth` (n = 4) and `phoneP-D24-nminAuth` (n = 3). Miner vs assembler, lit vs inactive, selected frame, locked plate (D24 clearly, D16 weaker), ore and ingot read. Bodies are 108/136 px (D16) vs 114/150 px (D24); the S1 "70 to 90 px" was the viewer's n = 2, not the authoritative `n_min`. Not readable at either density: warning vs paused and the bare-plate slot conditions. D16's detail loss is visible even at n = 4 (cruder bevel, smaller lock, fewer gear teeth).

**Condition 2 (room for cue icons, >= 12x12 art px and >= 24 pt at `n_min`)**: icon pt = 12 x `n_min` / (px per pt).

| Class | D16: `n_min`, 12 px icon pt | D24: `n_min`, 12 px icon pt |
|---|---|---|
| 320 dpi phones, 2.004 | 4, 23.95 | 3, 17.96 |
| Portrait tablet, 2.022 | 4, 23.7 | 3, 17.8 |
| Emulator 420 dpi, 2.631 | 4, 18.2 | 3, 13.7 |
| Tablet landscape, 1.867 | 3, 19.3 | 2, 12.9 |

Neither density meets 12 art px at 24 pt on every class. D16 needs 12 to 13 art px on the 320 dpi phones and the portrait tablet and up to 16 elsewhere; D24 needs 17 on the phones and 22 to 23 on the emulator and tablet landscape. Proportionally there is no density advantage: a 24 pt icon takes about 45 % of the miner body width at either density (12/27 at D16, 17/38 at D24), about 59 to 60 % for the emulator and tablet-landscape needs. D24's 17x17 gives about twice the pixel area of D16's 12x12 (289 vs 144 px) for shape-coded warning vs paused glyphs. The binding constraint is the on-screen floor, so the icon must be an overlay badge offset from the body; canvas headroom must be checked (D24 canvas 80x64, body 38x27, plate 70x35; D16 canvas 54x44, body 27x19).

#### 4. Verdict on "D24, default if no preference D24": AGREE WITH CONDITIONS

D24 is the only density with no failed measured gate and keeps the S1 detail; D16 fails the IoU gate at 32 px by 0.039 and is a scaled S1 model with detail lost. Both fail naming for the same cue reasons, so the choice rests on IoU, detail and on-screen size. D24's accepted costs: the 1280x720 phone `n_fit` 2 < `n_min` 3 (T427 tolerance), the portrait tablet at fit-all only, and a larger icon need on the emulator and tablet landscape. D16's one advantage (no 1280x720 violation) is outweighed. Conditions:

1. **T427 before the D24 freeze**: the 1 % `n_min` tolerance and its HUD-aware Play Mode proof pass; record fit-all-only on both 320 dpi phone orientations and on the portrait tablet as a standing accepted risk with no zoom-out.
2. **S3 cue icons (T421/T422) on D24**: test at 17x17 art px or larger (>= 24 pt at `n_min` on 320 dpi phones; 22 to 23 art px on the emulator and tablet landscape), or state which parameter changes (icon px, floor or density). Overlay badge offset from the body, canvas headroom verified, silhouette IoU kept at or below 0.70. Warning vs paused must differ by shape in grayscale, not only by colour.
3. **Re-run the full shuffled naming gate on the S3 winner** at D24, grayscale and all three CVD filters, zero confusions. The sheet must show slot conditions as plate-with-machine plus the empty/occupied/locked conventions (so "occupied" is not a bare plate), or the convention is stated in the Art Bible before the gate.
4. **T424 pivots and freeze**: whole art pixels for all pivots (D24 ore/ingot y = 4.5); re-run the crisp check expecting 100 % vertical edges on grid at every `n`; measure shimmer under pan; run the contrast check against the surfaces; freeze the Art Bible at D24 with these rules.
5. **T456 isometric slot lattice at D24**: plate 70 px wide at 2.5-cell (60 px) spacing overlaps (1.17); resolve overlap, row sorting and hit-testing with the lattice.
6. **Evidence debt**: sprite-level import tests for `D24/isometric-2-1-s2` (D16 only if reopened), effort per asset, physical-device captures (emulator only so far).
7. **No redo round for D16** unless the owner chooses D16; then a redrawn silhouette passing 0.70 at full size and at 32 px, and a naming-gate re-run.

### Owner decision (T420)

Owner decision 2026-10-29 (T420, #532): **D24**, the agents' recommended option and default, on projection B (isometric 2:1).

Reason: D24 is the only density with no failed measured gate (silhouette IoU 0.651 / 0.672 against 0.70) and keeps the S1 detail; D16 fails the gate (0.704 / 0.739) and is the S1 model scaled down. Both densities fail the blind naming gate for the same cue reasons, so naming does not decide between them. The owner accepts D24's costs (1280x720 phone `n_fit` 2 < `n_min` 3 until the T427 tolerance, fit-all only on the 320 dpi phones and the portrait tablet, a larger icon need on the emulator and tablet landscape) and the seven conditions of the second-session Art Director review (§ 4 above). They are carried as follows:

1. `n_min` tolerance and HUD-aware proof at D24: **T427** (acceptance at D = 24); the fit-all-only classes are recorded as a standing accepted risk in the Art Bible (**T424**). T427 depends on the Art Bible sign-off (T426), so the proof cannot precede the freeze; T426 signs off with T427's proof still open; T442 (after T427) is the gate that confirms it.
2. S3 cue icons on D24 at 17x17 art px or larger (>= 24 pt at `n_min` on the 320 dpi phones; 22 to 23 art px on the emulator and tablet landscape) as an overlay badge offset from the body, shape-coded warning vs paused in grayscale, canvas headroom checked, silhouette IoU at most 0.70: **T421**; T422 states which parameter changes where a class still misses 24 pt.
3. Full shuffled naming gate re-run on the S3 winner at D24 (grayscale and three CVD filters, zero confusions) with slot conditions shown as plate-with-machine plus the empty/occupied/locked conventions: **T422** (convention stated in the Art Bible by **T424**).
4. Whole-art-pixel pivots (D24 ore/ingot y = 4.5), crisp check at 100 % vertical edges on grid at every `n`, shimmer under pan, contrast against the surfaces, Art Bible frozen at D24: **T424** (contrast and crispness reviewed by **T425**).
5. Isometric slot lattice at D24 (70 px plate at 60 px spacing overlaps): **T456**.
6. Evidence debt: sprite-level import tests for `D24/isometric-2-1-s2` and the S3 import folder, effort per asset: **T422**; the physical-device gap stays stated in **T444**.
7. D24 is the owner's choice; no redo round for D16.

T421 (S3/S4) is ready.

## S3/S4 Cue variant and lighting (T422, #534, FR-010, SC-001, SC-002)

Evidence author: Test/QA session with UI/UX Reviewer lens (Sonnet), 2026-10-29, base `726c360` (T421, PR #606 merged). The evidence author did not produce the art (T421 did) and gives **no Art Director verdict** and **no recommendation of a winner**; the blind naming session, the Art Director review and the owner decisions S3 and S4 (T423) are separate steps. Measured facts only. The readability record is [`readability-spike.md`](readability-spike.md).

### Candidates

| Label | Content | Source |
|---|---|---|
| **S3 variant (a)** | body treatment (hazard stripes) plus a shape-coded exception icon above the machine; icon art 17 px or 23 px | `art-source/f004/spike/s3-s4/pixel/s3a-*`, imported at `Assets/IdleFactory/Art/F004/Spike/D24/isometric-2-1-s3/` |
| **S3 variant (b)** | uniform rounded-square pixel badge with a glyph above the machine, body unchanged; 17 px or 23 px | `s3b-*` in the same folders |
| **S4 lighting A** | S2 baked-light body (`s3-body-run`) | slot 1 of every capture |
| **S4 lighting B** | flat body plus a separate accent sprite (`s3-lightB-comp-run`) | slot 11 of every capture |

Cued states (miner only in S3): waiting, missing input, storage full, paused, inactive; running is uncued. The assembler, plates, ore and ingot are the S2 assets.

### How the in-game evidence was made (read before interpreting)

- Same method as § S2 with a **temporary viewer that was never committed and was deleted before this commit** (a runtime component, a temporary editor script and a temporary scene, all under `Assets/TempT422/`): `CameraScaleModel`, `SlotGridLayout`, the 12 slots on the rectangular Feature 003 lattice, `artPixelsPerCell` 24, readable cell 24 pt, `maxScale` 6, point filter. The 12 slots in reading order: miner running (light A), waiting, missing input, storage full, paused, inactive (all `s3<v>-comp-<state>-<size>`), plate empty, plate locked, assembler running on a plate (S2), bare occupied plate, miner running with light B, ore. Slots are drawn in index order (row-major), so overlaps of neighbouring plates are deterministic (the S2 viewer left the order to Unity).
- **Windows classes: offscreen render in the batch editor, not a standalone player.** A temporary editor script rendered the camera into a point-filtered `RenderTexture` at the exact physical resolution (cleared before each capture) with the density set to px per pt x 160 (320.64 dpi phones, 323.52 dpi portrait tablet, 298.72 dpi landscape tablet). The shown scale of `...-nmin` is therefore the **authoritative `n_min`** of the S2 zoom table; `...-fit` is the viewer's whole-screen fit. This differs from § S2 (a Windows player); pixel layout does not depend on the host, and the composition is checked pixel-identical (below).
- **Emulator**: `Medium_Phone_API_36.0` (`emulator-5554`, 1080x2400, 420 dpi), GLES3-only IL2CPP ARM64 Development APK built from a temporary scene (temporary application id `com.idlefactory.t422temp`; the emulator-only GLES3 override and the application id were restored by the build script, `ProjectSettings` noise was restored with `git checkout`, and the build target was switched back to StandaloneWindows64), screenshots by `adb exec-out screencap`. The viewer's label and Next button are visible (the button was driven by a long press with `adb shell input swipe`; a short `input tap` is not seen by IMGUI); in landscape the label overlaps one plate. Portrait `emuP` and landscape `emuL` (`settings put system user_rotation`). Emulator-only evidence.
- **Physical devices: none available and none used.** The gap stays open for T444.
- **Approximations as in S2.** The viewer fits against the whole screen with no HUD; on the emulator it uses `Screen.dpi / 160` = 2.625 (production 2.631, same `n_min`) and reports `n_fit` 5 where the HUD-aware table says 4. The authoritative zoom table is the § S2 table (`ScreenClassScaleTableTests`); it does not depend on the cue art and is not repeated.
- **Composition fidelity.** `tools/art-pipeline/tests/test_timed_trial.py` rebuilds three Windows captures (`phoneP-a23-nmin`, `tabL-b17-fit`, `tabP-a17-nmin`) from the committed sprites and the slot centres in `tools/art-pipeline/trials/trial_layouts.json` and requires **pixel identity** with the committed screenshots. The ore (half-art-pixel pivot y = 4.5, the T424 finding) is excluded: at odd `n` Unity places it on a fractional position and rounds differently from the harness.

### On-device screenshots (variant and size, n_min and fit-all)

Folder `style-spike/s3/shots/`, name `<class>-<variant><size>-<nmin|fit>.png`; 48 files (6 classes x 4 variant/size x 2 modes). "n shown" is the integer scale in the file (n_min / fit).

| Class | Screen (px) | n_min shots (a23, a17, b23, b17) | fit-all shots (a23, a17, b23, b17) | n shown (n_min / fit) |
|---|---|---|---|---|
| Phone landscape (Windows) | 1280x720 | [a23](style-spike/s3/shots/phoneL-a23-nmin.png), [a17](style-spike/s3/shots/phoneL-a17-nmin.png), [b23](style-spike/s3/shots/phoneL-b23-nmin.png), [b17](style-spike/s3/shots/phoneL-b17-nmin.png) | [a23](style-spike/s3/shots/phoneL-a23-fit.png), [a17](style-spike/s3/shots/phoneL-a17-fit.png), [b23](style-spike/s3/shots/phoneL-b23-fit.png), [b17](style-spike/s3/shots/phoneL-b17-fit.png) | 3 / 3 |
| Phone portrait (Windows) | 720x1280 | [a23](style-spike/s3/shots/phoneP-a23-nmin.png), [a17](style-spike/s3/shots/phoneP-a17-nmin.png), [b23](style-spike/s3/shots/phoneP-b23-nmin.png), [b17](style-spike/s3/shots/phoneP-b17-nmin.png) | [a23](style-spike/s3/shots/phoneP-a23-fit.png), [a17](style-spike/s3/shots/phoneP-a17-fit.png), [b23](style-spike/s3/shots/phoneP-b23-fit.png), [b17](style-spike/s3/shots/phoneP-b17-fit.png) | 3 / 3 |
| Tablet landscape (Windows) | 2048x1536 | [a23](style-spike/s3/shots/tabL-a23-nmin.png), [a17](style-spike/s3/shots/tabL-a17-nmin.png), [b23](style-spike/s3/shots/tabL-b23-nmin.png), [b17](style-spike/s3/shots/tabL-b17-nmin.png) | [a23](style-spike/s3/shots/tabL-a23-fit.png), [a17](style-spike/s3/shots/tabL-a17-fit.png), [b23](style-spike/s3/shots/tabL-b23-fit.png), [b17](style-spike/s3/shots/tabL-b17-fit.png) | 2 / 6 |
| Tablet portrait (Windows) | 1536x2048 | [a23](style-spike/s3/shots/tabP-a23-nmin.png), [a17](style-spike/s3/shots/tabP-a17-nmin.png), [b23](style-spike/s3/shots/tabP-b23-nmin.png), [b17](style-spike/s3/shots/tabP-b17-nmin.png) | [a23](style-spike/s3/shots/tabP-a23-fit.png), [a17](style-spike/s3/shots/tabP-a17-fit.png), [b23](style-spike/s3/shots/tabP-b23-fit.png), [b17](style-spike/s3/shots/tabP-b17-fit.png) | 3 / 6 |
| Emulator portrait | 1080x2400 | [a23](style-spike/s3/shots/emuP-a23-nmin.png), [a17](style-spike/s3/shots/emuP-a17-nmin.png), [b23](style-spike/s3/shots/emuP-b23-nmin.png), [b17](style-spike/s3/shots/emuP-b17-nmin.png) | [a23](style-spike/s3/shots/emuP-a23-fit.png), [a17](style-spike/s3/shots/emuP-a17-fit.png), [b23](style-spike/s3/shots/emuP-b23-fit.png), [b17](style-spike/s3/shots/emuP-b17-fit.png) | 3 / 5 |
| Emulator landscape | 2400x1080 | [a23](style-spike/s3/shots/emuL-a23-nmin.png), [a17](style-spike/s3/shots/emuL-a17-nmin.png), [b23](style-spike/s3/shots/emuL-b23-nmin.png), [b17](style-spike/s3/shots/emuL-b17-nmin.png) | [a23](style-spike/s3/shots/emuL-a23-fit.png), [a17](style-spike/s3/shots/emuL-a17-fit.png), [b23](style-spike/s3/shots/emuL-b23-fit.png), [b17](style-spike/s3/shots/emuL-b17-fit.png) | 3 / 5 |

Windows `n_min` is authoritative; the whole-screen fit gives n = 3 on both 320 dpi phones (their HUD-aware `n_fit` is lower on landscape, the known T427 case, see § S2). All 12 slots are visible in every shot (checked on the lattice of each class). Not captured: any physical device; the phone and tablet classes on the emulator.

### Cue size in points per class (the "which parameter changes" statement)

pt = art px x n / effective px per pt (`ShellPanelScale`); `n_min` and `n_fit` from the § S2 D24 table. Icon art px are the nominal sizes of the two sprite sets (17 and 23); the paused and inactive icon layers measure exactly 17x17 and 23x23 opaque px in both variants (variant (a) waiting, missing input and storage full layers also carry the body treatment and are wider).

| Class (px per pt) | n_min | 17 px at n_min | 23 px at n_min | art px for 24 pt at n_min (exact) | 17 px at fit (n_fit) | 23 px at fit (n_fit) |
|---|---|---|---|---|---|---|
| 320 dpi phone, portrait and landscape (2.004) | 3 | **25.4** | 34.4 | 17 (16.03) | 25.4 (3) | 34.4 (3) |
| Portrait tablet 264 dpi (2.022) | 3 | **25.2** | 34.1 | 17 (16.18) | 25.2 (3) | 34.1 (3) |
| Emulator 420 dpi (2.631) | 3 | 19.4 **(misses)** | **26.2** | 22 (21.05) | 25.8 (4) | 35.0 (4) |
| Tablet landscape 264 dpi (1.867) | 2 | 18.2 **(misses)** | **24.6** | 23 (22.40) | 45.5 (5) | 61.6 (5) |
| 460 dpi phone (2.881), not captured, § S2 table | 3 | 17.7 (misses) | **23.95 (misses by 0.05 pt)** | 24 (23.05) | 29.5 (5) | 39.9 (5) |
| 320 dpi phone landscape if the T427 1 % tolerance gives n = 2 (2.004) | 2 | 17.0 (misses) | 22.95 (misses) | 25 (24.05) | 17.0 (2) | 23.0 (2) |

**Which parameter changes (measured, for the owner).** At 17 px the emulator class (19.4 pt) and tablet landscape (18.2 pt) miss 24 pt at `n_min`; for those classes the changed parameter is the **icon art size: 23 px** (26.2 pt on the emulator, 24.6 pt on tablet landscape; 24.6 pt is the minimum over the four captured classes). One 23 px set meets 24 pt on every captured class, so no per-class set is needed to reach 24 pt; the 17 px set meets it only on the 320 dpi phones and the portrait tablet. The scale (`n_min`), px per pt and the badge anchor do not change. Two classes of the § S2 table are **not** met by 23 px and are reported, not decided: the 460 dpi phones (23.95 pt, 0.2 % short; 24 px gives 25.0 pt) and the 1280x720 phone if T427's tolerance lets `n` drop to 2 (22.95 pt; 25 px needed).

### Silhouette IoU (QA warning W2 from T421, stated neutrally)

The condition "silhouette IoU at most 0.70" holds for the **bodies alone**: 0.651 full and 0.672 at 32 px (miner against assembler, the S2 D24 result, T421 `check-report.json`). With the cue layers composited on the miner, the **both-cued composites reach 0.77 and 0.78** (T421 QA measurement), above 0.70. Whether the 0.70 rule applies to bodies only or to composites is the owner's reading of the Art Director condition; this record states both numbers only.

### Contact sheets from the in-game captures (SC-001; naming pending)

Tiles are crops of isolated in-game captures (same camera and integer scale as the full-lattice capture, one slot per render so neighbouring plates do not bleed, background (40, 40, 48)), nine per sheet in a seeded shuffle (3 x 3, row-major), native device pixels: an occupied slot shows a plate with the machine (miner running, the five cued states of the miner, assembler running) plus the empty and locked plates as in § S2. Grayscale and the three CVD simulations per set. Keys (`*_key.json`) are written by the script and were **not opened by the evidence author**; a reviewer who names the tiles must not be given them.

| Set | Variant, size | Capture (class, n) | Tiles | Grayscale | Deuteranopia | Protanopia | Tritanopia | Key |
|---|---|---|---|---|---|---|---|---|
| `a23` | a, 23 px | phoneP, n = 3 | 9 | [grayscale](style-spike/s3/sheets/a23_grayscale.png) | [deuteranopia](style-spike/s3/sheets/a23_deuteranopia.png) | [protanopia](style-spike/s3/sheets/a23_protanopia.png) | [tritanopia](style-spike/s3/sheets/a23_tritanopia.png) | [key](style-spike/s3/sheets/a23_key.json) |
| `b23` | b, 23 px | phoneP, n = 3 | 9 | [grayscale](style-spike/s3/sheets/b23_grayscale.png) | [deuteranopia](style-spike/s3/sheets/b23_deuteranopia.png) | [protanopia](style-spike/s3/sheets/b23_protanopia.png) | [tritanopia](style-spike/s3/sheets/b23_tritanopia.png) | [key](style-spike/s3/sheets/b23_key.json) |
| `a17` | a, 17 px | phoneP, n = 3 | 9 | [grayscale](style-spike/s3/sheets/a17_grayscale.png) | [deuteranopia](style-spike/s3/sheets/a17_deuteranopia.png) | [protanopia](style-spike/s3/sheets/a17_protanopia.png) | [tritanopia](style-spike/s3/sheets/a17_tritanopia.png) | [key](style-spike/s3/sheets/a17_key.json) |
| `b17` | b, 17 px | phoneP, n = 3 | 9 | [grayscale](style-spike/s3/sheets/b17_grayscale.png) | [deuteranopia](style-spike/s3/sheets/b17_deuteranopia.png) | [protanopia](style-spike/s3/sheets/b17_protanopia.png) | [tritanopia](style-spike/s3/sheets/b17_tritanopia.png) | [key](style-spike/s3/sheets/b17_key.json) |
| `a23-tabLmin` | a, 23 px (optional) | tabL, n = 2 | 9 | [grayscale](style-spike/s3/sheets/a23-tabLmin_grayscale.png) | [deuteranopia](style-spike/s3/sheets/a23-tabLmin_deuteranopia.png) | [protanopia](style-spike/s3/sheets/a23-tabLmin_protanopia.png) | [tritanopia](style-spike/s3/sheets/a23-tabLmin_tritanopia.png) | [key](style-spike/s3/sheets/a23-tabLmin_key.json) |
| `b23-tabLmin` | b, 23 px (optional) | tabL, n = 2 | 9 | [grayscale](style-spike/s3/sheets/b23-tabLmin_grayscale.png) | [deuteranopia](style-spike/s3/sheets/b23-tabLmin_deuteranopia.png) | [protanopia](style-spike/s3/sheets/b23-tabLmin_protanopia.png) | [tritanopia](style-spike/s3/sheets/b23-tabLmin_tritanopia.png) | [key](style-spike/s3/sheets/b23-tabLmin_key.json) |
| `a23-tabLfit` | a, 23 px, fit-all (optional) | tabL, n = 6 | 9 | [grayscale](style-spike/s3/sheets/a23-tabLfit_grayscale.png) | [deuteranopia](style-spike/s3/sheets/a23-tabLfit_deuteranopia.png) | [protanopia](style-spike/s3/sheets/a23-tabLfit_protanopia.png) | [tritanopia](style-spike/s3/sheets/a23-tabLfit_tritanopia.png) | [key](style-spike/s3/sheets/a23-tabLfit_key.json) |
| `b23-tabLfit` | b, 23 px, fit-all (optional) | tabL, n = 6 | 9 | [grayscale](style-spike/s3/sheets/b23-tabLfit_grayscale.png) | [deuteranopia](style-spike/s3/sheets/b23-tabLfit_deuteranopia.png) | [protanopia](style-spike/s3/sheets/b23-tabLfit_protanopia.png) | [tritanopia](style-spike/s3/sheets/b23-tabLfit_tritanopia.png) | [key](style-spike/s3/sheets/b23-tabLfit_key.json) |

Required sets: `a23`, `b23`, `a17`, `b17` (320 dpi phone portrait at the authoritative `n_min` = 3, the Art Director's reference class). The four `tabL` sets are optional extras for the smallest scale (n = 2) and fit-all (n = 6). Sheets are reproducible: `tools/art-pipeline/trials/make_capture_sheets.py` (seed 4220 plus the set's index in sorted order).

#### Blind naming (independent session)

Independent session: Sonnet, read-only, saw only the 24 sheet PNGs of the six sets `a23`, `a17`, `b23`, `b17`, `a23-tabLmin`, `b23-tabLmin` (4 filters each), never the keys. The `a23-tabLfit` and `b23-tabLfit` sets were not given to it. Abbreviations in the reviewer's report: SF storage full, MI missing input, W waiting, P paused, IN inactive, R miner running, ASM assembler running (the only assembler tile in every set, **named "running" by assumption**), EP empty plate, LP locked plate. Tile positions are row r, column c of the 3 x 3 sheet (row-major). The reviewer named each tile in each of the four filters separately and gave the **same name in all four filters** for every tile, so each row below holds for grayscale, deuteranopia, protanopia and tritanopia. "Unsure" answers count as named-as-given.

##### Naming tables (reviewer's answers) and scoring against the keys

**Set a23 (variant a, 23 px, phone n = 3)**

| Tile | Named (all four filters) | Certainty | Cues used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | storage-full | unsure | hatched body + corner badge of horizontal bars (list) | storage-full | yes |
| r1c2 | missing-input | unsure | hatched body + warning triangle | missing-input | yes |
| r1c3 | plate-locked | certain | dark striped plate with small box/lock glyph | plate-locked | yes |
| r2c1 | plate-empty | certain | flat plate with cross/bracket mark | plate-empty | yes |
| r2c2 | assembler-running | certain | gear body, glowing core | assembler-running | yes |
| r2c3 | waiting | unsure | hatched body + hourglass in round badge | waiting | yes |
| r3c1 | running | certain | plain miner, bright front spot, no badge | running | yes |
| r3c2 | paused | certain | two tall vertical bars above plain miner | paused | yes |
| r3c3 | inactive | certain | dark dimmed body + ring/minus badge | inactive | yes |

Hard to separate (reviewer): r1c1, r1c2 and r2c3 share the hatched body and differ only in the small badge glyph; grayscale hardest; the yellow and red hatch cannot be told apart by colour in any filter; the glyph-to-state mapping (list = storage full, triangle = missing input, hourglass = waiting) is a semantic guess. Unsure tiles: 3 of 9.

**Set a17 (variant a, 17 px, phone n = 3)**

| Tile | Named (all four filters) | Certainty | Cues used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | plate-empty | certain | (none given) | plate-empty | yes |
| r1c2 | assembler-running | certain | (none given) | assembler-running | yes |
| r1c3 | plate-locked | certain | (none given) | plate-locked | yes |
| r2c1 | storage-full | unsure | hatched + small list badge | storage-full | yes |
| r2c2 | paused | certain | tall pause bars | paused | yes |
| r2c3 | running | certain | (none given) | running | yes |
| r3c1 | inactive | certain | dark body + ring badge | inactive | yes |
| r3c2 | missing-input | unsure | hatched + triangle | missing-input | yes |
| r3c3 | waiting | unsure | hatched + round badge (reads as bowtie/X, not a clear hourglass) | waiting | yes |

Hard to separate (reviewer): r2c1, r3c2 and r3c3 share the hatched body and the smaller badges are less legible; r2c1 and r3c3 are the closest pair in grayscale; running versus paused differs subtly in the body in grayscale, only the pause bars separate them. Unsure tiles: 3 of 9.

**Set b23 (variant b, 23 px, phone n = 3)**

| Tile | Named (all four filters) | Certainty | Cues used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | running | certain | no badge | running | yes |
| r1c2 | inactive | certain | grey ring/minus badge | inactive | yes |
| r1c3 | plate-empty | certain | (none given) | plate-empty | yes |
| r2c1 | assembler-running | certain | (none given) | assembler-running | yes |
| r2c2 | waiting | certain | hourglass badge | waiting | yes |
| r2c3 | missing-input | certain | down-arrow-into-tray badge | missing-input | yes |
| r3c1 | storage-full | certain | list badge | storage-full | yes |
| r3c2 | plate-locked | certain | (none given) | plate-locked | yes |
| r3c3 | paused | certain | two vertical bars badge | paused | yes |

Hard to separate (reviewer): none. Mild: r3c1 versus r3c3 differ in badge fill but the glyphs are clear; inactive relies on a small grey badge that is easy to miss but visible. Unsure tiles: 0 of 9.

**Set b17 (variant b, 17 px, phone n = 3)**

| Tile | Named (all four filters) | Certainty | Cues used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | missing-input | certain | down-arrow badge | missing-input | yes |
| r1c2 | running | certain | (none given) | running | yes |
| r1c3 | assembler-running | certain | (none given) | assembler-running | yes |
| r2c1 | inactive | certain | grey ring (smaller, dimmer) | inactive | yes |
| r2c2 | paused | certain | (none given) | paused | yes |
| r2c3 | waiting | certain | hourglass badge | waiting | yes |
| r3c1 | storage-full | certain | list badge | storage-full | yes |
| r3c2 | plate-locked | certain | (none given) | plate-locked | yes |
| r3c3 | plate-empty | certain | (none given) | plate-empty | yes |

Hard to separate (reviewer): missing-input arrow versus storage-full list slightly closer at 17 px in grayscale, still distinguishable; inactive has low contrast and could be mistaken for no badge. Unsure tiles: 0 of 9.

**Set a23-tabLmin (variant a, 23 px, tablet landscape n = 2)**

| Tile | Named (all four filters) | Certainty | Cues used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | assembler-running | certain | (none given) | assembler-running | yes |
| r1c2 | missing-input | unsure | hatched + triangle | missing-input | yes |
| r1c3 | storage-full | unsure | hatched + list | storage-full | yes |
| r2c1 | plate-empty | certain | (none given) | plate-empty | yes |
| r2c2 | plate-locked | certain | (none given) | plate-locked | yes |
| r2c3 | paused | certain | (none given) | paused | yes |
| r3c1 | inactive | certain | (none given) | inactive | yes |
| r3c2 | waiting | unsure | hatched + hourglass | waiting | yes |
| r3c3 | running | certain | (none given) | running | yes |

Hard to separate (reviewer): r1c2, r1c3 and r3c2 differ only by glyph; the triangle is very dim in protanopia and deuteranopia, weakening missing input there; the hatch is lost in grayscale at this size. Unsure tiles: 3 of 9.

**Set b23-tabLmin (variant b, 23 px, tablet landscape n = 2)**

| Tile | Named (all four filters) | Certainty | Cues used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | storage-full | certain | (none given) | storage-full | yes |
| r1c2 | missing-input | certain | (none given) | missing-input | yes |
| r1c3 | paused | certain | (none given) | paused | yes |
| r2c1 | inactive | certain | grey ring badge | inactive | yes |
| r2c2 | assembler-running | certain | (none given) | assembler-running | yes |
| r2c3 | waiting | certain | (none given) | waiting | yes |
| r3c1 | running | certain | (none given) | running | yes |
| r3c2 | plate-empty | certain | (none given) | plate-empty | yes |
| r3c3 | plate-locked | certain | (none given) | plate-locked | yes |

Hard to separate (reviewer): none; the inactive ring badge is the least conspicuous. Unsure tiles: 0 of 9.

**Reviewer's overall note (reviewer's judgement only, shortened):** variant b is easier to read than a (distinct glyph set, no state depends on body hatching); in a, storage full, missing input and waiting share one hatched body and only a small glyph separates them. a is weakest in grayscale and at 17 px (the hourglass degenerates into an X or bowtie); deuteranopia and protanopia are nearly identical here. 23 px reads clearly better than 17 px for both variants, and variant a suffers more. At tablet landscape n = 2 with 23 px, b stays fully readable while a keeps the storage full / missing input / waiting glyph ambiguity. Running, paused and inactive are easy in both; empty, locked and assembler are easy in all sets.

##### Score against the keys (keys opened after the naming was recorded)

Correct tiles of 9, per set and filter. Because the reviewer's names were identical across filters, the score is the same in each filter. Confusions are listed as key versus named.

| Set | Grayscale | Deuteranopia | Protanopia | Tritanopia | Confusions (key vs named) | Unsure tiles (named as given) | SC-001 zero-confusion gate (all four filters) |
|---|---|---|---|---|---|---|---|
| `a23` | 9/9 | 9/9 | 9/9 | 9/9 | none | 3 | PASS (0 confusions) |
| `a17` | 9/9 | 9/9 | 9/9 | 9/9 | none | 3 | PASS (0 confusions) |
| `b23` | 9/9 | 9/9 | 9/9 | 9/9 | none | 0 | PASS (0 confusions) |
| `b17` | 9/9 | 9/9 | 9/9 | 9/9 | none | 0 | PASS (0 confusions) |
| `a23-tabLmin` | 9/9 | 9/9 | 9/9 | 9/9 | none | 3 | PASS (0 confusions) |
| `b23-tabLmin` | 9/9 | 9/9 | 9/9 | 9/9 | none | 0 | PASS (0 confusions) |

Facts for the owner (no recommendation): every named tile of every given set matches its key, in all four filters, so the zero-confusion rule of the gate is met by all six named sets, including the 17 px sets and the tablet-landscape n = 2 sets. In variant a the three tiles that the reviewer marked unsure (storage full, missing input, waiting; 3 of 9 in each of `a23`, `a17` and `a23-tabLmin`) were named correctly, but the reviewer reports that only the small glyph separates them and that the glyph-to-state mapping was a semantic guess, i.e. the correct answers rest on that guess; variant b had no unsure tile in any set. The assembler was named "running" by assumption (it is the only assembler tile and the only assembler state in the sets, so the name is correct by construction and says nothing about assembler states). One reviewer session; SC-001 also needs the owner's visual confirmation, and the sets `a23-tabLfit` and `b23-tabLfit` (fit-all, n = 6) were not named.

### SC-002 timed-trial harness (for the owner's T423 run)

`tools/art-pipeline/trials/timed_trial.py` builds, per variant, 10 seeded 12-slot screenshots (default: emulator portrait 1080x2400 at the authoritative `n_min` = 3, 23 px icons) with exactly one problem machine each (waiting, missing input, storage full, balanced 4/3/3 in a seeded order, tested for every seed), one or two benign distractors (paused or inactive), otherwise running miners and assemblers and empty and locked plates, plus an answer key and a clickable timing page. Generated sets: `style-spike/s3/trial/a` (seed 4220) and `trial/b` (seed 4221, so the owner cannot reuse positions). The keys sit next to the pages: **do not open `key.json` before the run**. Run (T423): open `trial/<a|b>/trial.html` in a browser (no network; make the window phone-shaped, the image scales to the window height so it is smaller than on a phone), click the stuck machine, save the log at the end, then `uv run --frozen --offline --project tools/art-pipeline python tools/art-pipeline/trials/timed_trial.py score --dir <set> --log <log.json>` (pass: at least 9 of 10 correct within 2.0 s). A typed alternative is `run-cli`; other class, size or seed: `generate --class phoneP --size 17 --seed N --out ...`. The owner has not run it yet.

### Effort per asset (Art Director condition 6)

| Asset family | Assets | Generation time (measured) | Agent effort | Source |
|---|---|---|---|---|
| S2 D24 models and renders | 14 assets (24 sprites, 6-frame running machines) | models 4.65 s, renders 2.92 s | not measured | `s2/check-report.json` `timings` |
| S2 D16 models and renders (comparison) | 14 assets (24 sprites) | models 16.12 s, renders 3.12 s | not measured | same |
| S3/S4 body layers | 4 | 0.04 s | 2.0 min (estimate) | `s3-s4/effort.json` |
| S3/S4 plates | 3 | 0.0 s | 0.5 min (estimate) | same |
| S3/S4 cue layers (a and b, five states, two sizes) | 20 | 0.01 s | 12.0 min (estimate, 0.6 min each) | same |
| S3/S4 composites | 21 | 0.0 s | 3.0 min (estimate, 0.143 min each) | same |
| S4 lighting B (body, accent, composite) | 3 | 5.14 s (the Blender body render) | 8.0 min (estimate, 2.67 min each) | same |
| S3/S4 total | 51 | pipeline wall time 19.47 s | 25.5 min (estimate) | same |

Generation seconds are measured by the pipelines (no S1 timing is recorded in the repository). **Agent minutes are the T421 author's estimates, not measurements**; human review time and the owner's time are not included anywhere. The cost of this evidence step (captures, sheets, harness) is not in the table.

### Sprite-level import tests (Art Director condition 6)

`SpikeImportTests` (Edit Mode; `scripts/test-unity.ps1 -Suite EditMode -TestFile Assets/IdleFactory/Art/F004/Spike/Tests/EditMode/SpikeImportTests.cs`): the new test `ImportedS2D24SpritesHaveSpikeSettingsManifestPivotAndSizeAndMatchTheirSources` covers all 24 sprites of `D24/isometric-2-1-s2/` against `candidates-D24.json` (importer settings, custom pivot per manifest entry, sprite rect equal to the manifest canvas, 24 pixels per unit, byte identity with `art-source/f004/spike/s2/pixel/D24/` and the manifest sha256). The existing S3 test already covers sprite level for all 51 sprites (settings, custom pivot (0.5, 0.234375), 80x64 rect, byte identity) and was left unchanged. Targeted run: 6 of 6 passed.

### Limits

Emulator and Windows offscreen only; no physical device; the Windows classes were rendered in the batch editor, not a standalone player; the viewer has no HUD, so fit-all and the 1280x720 phone follow the § S2 table; the full lattice overlaps neighbouring plates (T456); the owner knows the art (SC-002 is a recognizability check); the blind naming was one reviewer session on six of the eight sets and the timed trial is not yet run; agent minutes are estimates.

### Owner timed trial, round 1, and partial decisions S3/S4 (T423, #535)

Timed trial by the owner on 2026-10-29 with the T422 harness (`tools/art-pipeline/trials/timed_trial.py`), sets `s3/trial/a` (seed 4220) and `s3/trial/b` (seed 4221), emulator portrait at `n_min` 3 with 23 px icons, browser window phone-shaped. Logs scored by the parent with `timed_trial.py score`; SC-002 passes at 9 of 10 correct within 2.0 s.

| Variant | Correct | Correct within 2.0 s | Median | Misses and slow trials | SC-002 |
|---|---|---|---|---|---|
| a | 8/10 | 6/10 | 1.107 s | wrong slot on two `waiting` trials (8, 10); slow `storage-full` 3.95 s (trial 1) and 3.85 s (trial 9) | FAIL |
| b | 10/10 | 8/10 | 0.865 s | slow first trial `storage-full` 2.56 s; slow `waiting` 3.34 s (trial 3) | FAIL |

Per state, b: missing-input 4/4 within time, storage-full 2/3, waiting 2/3. In both variants `waiting` is the weakest state (a: 1 of 3 within time; b: the slowest trial).

Owner decisions (2026-10-29, chosen in the session):

- **S3 = variant b** (uniform pixel badges per state on an unchanged body, none on running), **icon size 23 art px** for every class. The 24 pt gaps that remain (460 dpi phones 23.95 pt; 1280x720 phone at n = 2 under the T427 tolerance, 22.95 pt) are carried to T424 and T427.
- **Silhouette IoU at most 0.70 applies to machine bodies**; the shared overlay badge is excluded (bodies 0.651 / 0.672; badge-on-both composites 0.77 / 0.78 are informational).
- **S4 = lighting B**: flat body plus 2D-light accents (Light2D or additive accent sprite). The Art Bible (T424) defines the light model; render cost is checked against the frozen B2 budgets.
- **SC-002 not met yet**: the `waiting` glyph of variant b is revised in a small round (**T460**), followed by a fresh trial set for b and a repeat of the owner's trial. T423 closes after that trial passes.

### Revised `waiting` badge of variant b: blind naming of `b23r2` (T460, #608)

T460 redrew only the 23 px `waiting` badge of variant b (bold hourglass with a solid upper sand mass, sand pile and stream; fill cue_info, the only cool-filled b badge; frame, size and anchor unchanged). New SC-001 set `s3/sheets/b23r2_*` (composed from the sprites at n = 3, seed 4602) and trial set `s3/trial/b-r2` (seed 4602, unscored practice screen first).

Independent blind naming session (Sonnet, read-only, not involved in T421, T422 or the round-1 naming), 2026-10-29. It received only the four `b23r2` PNG copies in a scratch folder, without keys or repository access. Names were identical in all four filters; the parent scored them against `b23r2_key.json`.

| Tile | Named (all four filters) | Certainty | Cue used (reviewer) | Key | Match |
|---|---|---|---|---|---|
| r1c1 | miner – storage full | unsure on state | stacked-bars badge | storage-full | yes |
| r1c2 | empty plate | certain | plain plate, faint corner marks | plate-empty | yes |
| r1c3 | miner – waiting | certain | hourglass badge | waiting | yes |
| r2c1 | miner – missing input | unsure on state | down-arrow-into-tray badge | missing-input | yes |
| r2c2 | miner – inactive | fairly certain | grey circle-minus badge | inactive | yes |
| r2c3 | locked plate | certain | dark striped plate with lock-like object | plate-locked | yes |
| r3c1 | miner – paused | certain | white pause-bars badge | paused | yes |
| r3c2 | miner – running | fairly certain | no badge, by elimination | miner-running | yes |
| r3c3 | assembler – running | fairly certain | gear shape, no badge, by elimination | assembler-running | yes |

Score: 9/9 in grayscale, deuteranopia, protanopia and tritanopia; zero confusions; SC-001 gate PASS for `b23r2`. Hard pairs named by the reviewer: storage full vs missing input (glyph meaning, similar mid-tone fills; yellow and olive close under deutan/protan), inactive vs paused (both neutral badges, separated by glyph and brightness), running read only by the absence of a badge. The reviewer's judgement: waiting, paused and locked are clearly distinct; the filters made little difference because the glyphs carry the state.

### Owner timed trial, round 2, and final decisions S3/S4 (T423, #535)

Repeat trial by the owner on 2026-10-29 on `s3/trial/b-r2` (seed 4602; variant b, 23 px, revised `waiting` badge from T460; emulator portrait at `n_min` 3; one unscored practice screen first), browser window phone-shaped, scored by the parent with `timed_trial.py score`. Owner logs of both rounds: `style-spike/s3/trial/owner-logs/` (`round1-a.json`, `round1-b.json`, `round2-b-r2.json`).

| Set | Correct | Correct within 2.0 s | Median | Per state within 2.0 s | SC-002 |
|---|---|---|---|---|---|
| b-r2 | 10/10 | 10/10 | 1.077 s | missing-input 4/4 (slowest 1.80 s), storage-full 3/3, waiting 3/3 (1.06–1.08 s) | PASS |

**Final owner decisions (2026-10-29):**

- **S3 = cue variant b** with uniform pixel badges per state at **23 art px** on every class, overlay anchor bottom-centre (59, 33), no badge on running machines; the `waiting` badge is the T460 revision (bold hourglass, cue_info fill, the only cool-filled badge). The 17 px set is not used.
- **Silhouette IoU at most 0.70 on machine bodies only**; badges are overlays and excluded.
- **S4 = lighting B**, flat body plus 2D-light accents; the Art Bible (T424) defines the light model and checks its render cost against the frozen B2 budgets.
- SC-001: the S3 winner is named with zero confusions in grayscale and all three CVD filters (`b23`, `b17`, `b23-tabLmin` in § S3/S4; `b23r2` with the revised badge above). SC-002: PASS in round 2.

Carried into T424 (Art Bible v1.0): the colour semantics of the badge fills (cool = waiting, warm = problem states, neutral = paused/inactive) with shape as the primary carrier; the remaining 24 pt gaps at 23 px (460 dpi phones 23.95 pt; the 1280x720 phone at n = 2 under the T427 tolerance, 22.95 pt); running is read by the absence of a badge. The style-spike record S1–S4 is complete (SC-009).
