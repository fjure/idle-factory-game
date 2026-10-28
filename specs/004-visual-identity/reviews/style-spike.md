# Style spike record (Feature 004)

Record per research R16 and the data model "Style-spike record". It is filled stage by stage. No Art Bible freeze and no production asset before a complete record exists (SC-009). This file currently holds § S1 (T416); S2 to S4 are added by their own tasks.

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

Owner decision: pending (T417)
