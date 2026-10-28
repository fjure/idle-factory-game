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
| 2 | Grayscale / CVD naming (SC-001 method, shuffled, key outside the reviewer) | pending | pending | Owner of the gate: independent agent reviewer session (not producer, brief author or spike Art Director), transcript recorded in `reviews/readability-*.md`. Sheets above; the evidence author has not named the tiles and has not read the keys for judging. |
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

### Second-session Art Director review

<!-- The parent fills this subsection verbatim from the independent second Art Director session (fresh context). Provenance: session identity, date. Leave empty until then. -->

### Owner decision

Owner decision: pending (T417)
