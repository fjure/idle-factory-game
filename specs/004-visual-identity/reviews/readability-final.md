# Final readability record (Feature 004, SC-001 and SC-002, T445)

Issue #563, task T445 (parent-only gate). Stage A (evidence preparation) by a Test/QA session (Sonnet, Medium tier at high reasoning), 2026-11-05. The author did not produce the art, is not the Art Director, named no tile, opened no key, and gives no verdict. Results are appended by the parent after the independent naming and the owner's run. Method continues [`readability-spike.md`](readability-spike.md) (S3/S4) but on the shipped build; scope additions come from [`final-acceptance.md`](final-acceptance.md) section 2 (G1, G2, Art Director condition C2) and owner decision F1 (2026-11-05, portrait 720 x 1280 at 320 dpi is a DEV-2 class with n_min 2).

## Status

| Gate | Evidence | State |
|---|---|---|
| SC-001, blind naming in grayscale and deuteranopia, protanopia, tritanopia, zero confusions, incl. markers (DEV-3) | 6 sets x 4 filters x 22 tiles in [`readability-final/sc001/`](readability-final/sc001/README.md); transcripts and scoring in [`sc001/naming/`](readability-final/sc001/naming/score.json) | **PASS on the SC-001 fields (22/22, zero confusions, all 24 sheets); machine type reported separately (reviewer A swapped it in 3 sets), see Results** |
| SC-001 at 1280 x 720 n = 2 and portrait 720 x 1280 n = 2 (condition C2) | sets `phoneL-1280x720-n2`, `phoneP-720x1280-n2` | both named: gate fields 22/22 in all four filters, markers right; machine-type result per Results |
| SC-002, owner, 9 of 10 within 2.0 s with per-state breakdown (standard, running machines animate) | [`sc002/score-standard.json`](readability-final/sc002/score-standard.json), log [`sc002/logs/`](readability-final/sc002/logs/sc002-log-standard.json) | **NOT MET: 8 of 10 correct within 2.0 s. Accepted as not met by the owner (2026-11-05), recorded failure, no rerun** |
| SC-002 reduced-motion variant (G2) | `readability-final/sc002/reduced-motion/` | **not run (owner decision 2026-11-05)** |
| Owner visual confirmation of the sheets | T447 | pending |

SC-001 naming and the SC-002 standard trial are scored below (2026-11-05). Verdicts are data; the parent and the owner decide acceptance. The owner has already decided on the SC-002 result (see Results).

## Method

### Capture provenance

- Code: `origin/main` at `c027625` (worktree of Issue #563, branch `maqa/issue-563-t445-readability-final`). Unity 6000.6.0f1, batch mode, Play Mode, `-buildTarget win64` (Windows target; Android target not used, no emulator touched).
- Harness: a temporary Play Mode test (never committed, deleted after the run) following `RenderCaptureHarness`: the authored scene's sprite catalog (`RunSceneRun`, real F004 art, floor tile on), the production `SlotGridView` (forced `SlotVisual`s: condition, machine key, state badge, Boosted/Overdrive markers, selection frame, lock level), the production `FactoryCameraController` with the integer scale model (`Fit`, then `ZoomFromVisibleControl(-1)` to `n_min`), the authored `CoreGameFoundationPanelSettings` shell built by `ShellRig.CreateAuthored` with the device profile of each class (so `pixelsPerPoint` and the unobstructed HUD rectangle are the shipped ones), a W003-S session bound to the shell (real HUD, ore-vein overlay), a world `RenderTexture` of the exact device size (MSAA off, HDR off, point filter), 3 warm-up renders, and a floor camera bound to the capture camera. The Windows/Editor render path is not the Android player render path: no physical device and no emulator capture here.
- Measured scales (Play Mode log of the run), px per pt, `n_min` / fit-all:

| Class | px per pt | n_min | n_fit | Remark |
|---|---|---|---|---|
| phone portrait 1080 x 2400, 420 dpi | 2.631 | 3 | 4 | matches Art Bible 4.1 |
| phone portrait 720 x 1280, 320 dpi | 2.004 | 2 | 2 | Art Bible 4.1 row still says 3 / 3 (stale until patch v1.0.4, condition C1); owner decision F1 |
| phone landscape 1280 x 720, 320 dpi | 2.004 | 2 | 2 | DEV-2, DEV-4 |
| tablet landscape 2048 x 1536, 264 dpi | 1.867 | 2 | 4 | Art Bible 4.1 lists n_fit 5; the shell's unobstructed rectangle gives 4 on this run (a measured fact for the record, not judged here) |

- No class ran below its floor (`BelowFloor` false everywhere). Markers were drawn at `n_min` on every class, including 1280 x 720 and 720 x 1280 at n = 2.

### SC-001 sheets

Per set (class and `n`): 22 tiles, each one slot's 80 x 64 art px canvas cut around the slot pivot from an isolated capture (neighbours removed, floor kept) at device scale 1x. Tiles: miner and assembler x six states as plate-with-machine (Art Bible 8.5: no bare occupied plate), empty plate, locked plate L2 to L5 with the digit overlay, Boosted, Overdrive, both markers, selected machine, selected empty plate. Each set is rendered with `art_pipeline.checks` as grayscale and deuteranopia, protanopia, tritanopia; every filter sheet has its own seeded shuffle (seed + filter index), so the reviewer cannot carry numbers over. Vocabulary: [`vocabulary.md`](readability-final/sc001/vocabulary.md) (slot condition, machine type, state, marker, selection, certainty). Gate: zero confusions per set and filter (spec SC-001, DEV-3 blocking). Sets, seeds and tile sizes: [`readability-final/sc001/README.md`](readability-final/sc001/README.md). Keys: outside the repository until naming; released afterwards (`sc001/keys/`).

| Set | Class | n | Zoom | Tiles | Seed |
|---|---|---|---|---|---|
| `emuP-1080x2400-n3` | emulator portrait | 3 | n_min | 22 | 4450 |
| `emuP-1080x2400-n4` | emulator portrait | 4 | fit-all | 22 | 4460 |
| `phoneL-1280x720-n2` | phone landscape (C2) | 2 | n_min = fit-all | 22 | 4470 |
| `phoneP-720x1280-n2` | phone portrait (C2) | 2 | n_min = fit-all | 22 | 4480 |
| `tabL-2048x1536-n2` | tablet landscape | 2 | n_min | 22 | 4490 |
| `tabL-2048x1536-n4` | tablet landscape | 4 | fit-all | 22 | 4500 |

### SC-002 (owner timed trial, standard set only)

The owner ran `sc002/standard/trial.html` once (practice screen first, then 10 timed screens; log [`sc002/logs/sc002-log-standard.json`](readability-final/sc002/logs/sc002-log-standard.json), finished 2026-11-05T14:22:26Z). Scored with `final_readability.py score` against the sealed key (now released under [`sc002/keys/`](readability-final/sc002/keys/), together with the reduced-motion key, because both runs are over); full result in [`sc002/score-standard.json`](readability-final/sc002/score-standard.json).

**Result: 8 of 10 correct within 2.0 s (need 9): NOT MET.** Median 0.85 s over the correct trials, all correct trials under 1.5 s (slowest 1.414 s).

| Trial | Problem state | Machine | Key slot | Clicked slot | Seconds | Correct in time |
|---|---|---|---|---|---|---|
| 1 | missing input | assembler | 10 | **7** | 0.793 | **no** |
| 2 | waiting | assembler | 4 | **1** | 1.472 | **no** |
| 3 | waiting | smelter | 8 | 8 | 1.414 | yes |
| 4 | missing input | assembler | 5 | 5 | 0.800 | yes |
| 5 | storage full | smelter | 0 | 0 | 0.864 | yes |
| 6 | storage full | press | 5 | 5 | 0.725 | yes |
| 7 | waiting | smelter | 1 | 1 | 1.036 | yes |
| 8 | missing input | assembler | 1 | 1 | 0.781 | yes |
| 9 | missing input | smelter | 8 | 8 | 0.836 | yes |
| 10 | storage full | miner | 9 | 9 | 0.920 | yes |

| Problem state | Trials | Correct within 2.0 s | Median s |
|---|---|---|---|
| waiting | 3 | 2 | 1.414 |
| missing input | 4 | 3 | 0.796 |
| storage full | 3 | 3 | 0.864 |

The two misses (slot numbers are 0-based, row-major over the 3 x 4 portrait grid, as in the log):

- Trial 1: the problem was the missing-input assembler in slot 10. The owner clicked slot 7 (click at screen px 649, 1307), which holds an **inactive smelter**, one of the two benign distractors of that screen (slot 9 was paused). Time 0.79 s: a fast wrong pick between two cued machines, not a slow one.
- Trial 2: the problem was the waiting assembler in slot 4. The owner clicked slot 1 (click at 607, 994), a **running press** with no cue; the only distractor of that screen was slot 9 (paused). Time 1.47 s. Waiting is also the slowest state (median 1.414 s) and the only state with a correct trial near the limit (trial 3, 1.414 s).

Environment of the run (from the log): Chrome 156 on Windows, device pixel ratio 1, browser window 1864 x 893 CSS px (desktop-sized), the screen shown at 386 x 857 CSS px (phone-shaped image, scale 0.357 of the 1080 x 2400 capture) because the page fits the screenshot to the window height. The window was therefore not phone-sized as `OWNER-RUN.md` asked, only the screenshot was. One run, one person who knows the art, no repeat.

Owner decisions of 2026-11-05 (as given to the parent): (1) the SC-002 standard result of 8/10 is accepted as NOT met: a recorded failure, no rerun. (2) The reduced-motion variant was not run (owner decision). Consequence for the record: the reduced-motion measurement requested by review item G2 does not exist; running = no cue is proven for SC-001 naming (stage B above) but not timed without motion.


## Limits

- Windows Editor renders at the stated device pixel sizes and densities; no physical device, no emulator capture. The Android player may differ in GPU rounding, not in the integer scale model.
- Tiles are isolated slots (neighbours removed), the SC-002 screens are full 12-slot screens; the full-grid proof of FR-015 is not part of this record.
- The HUD in the SC-002 screens comes from a bound stress session and is not consistent with the forced machine states; it is never a cue for the answer.
- SC-002 is shown on a desktop browser scaled to a phone-shaped window; physical size and viewing distance differ from a phone. Nearest-machine hit testing is used instead of an exact hit shape.
- The owner knows the art; SC-002 is a recognisability check, not a naive-player study. Two independent reviewer sessions named the SC-001 sheets (Art Bible 9.5 requires one); they could not be given a glyph key, so machine-type names rest on a guess.
- The temporary capture test is not in the repository; the raw captures are not committed. Regeneration needs a new capture run with the same specification (`final_readability.py plan`).
