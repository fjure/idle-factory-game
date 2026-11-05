# Final readability record (Feature 004, SC-001 and SC-002, T445)

Issue #563, task T445 (parent-only gate). Stage A (evidence preparation) by a Test/QA session (Sonnet, Medium tier at high reasoning), 2026-11-05. The author did not produce the art, is not the Art Director, named no tile, opened no key, and gives no verdict. Results are appended by the parent after the independent naming and the owner's run. Method continues [`readability-spike.md`](readability-spike.md) (S3/S4) but on the shipped build; scope additions come from [`final-acceptance.md`](final-acceptance.md) section 2 (G1, G2, Art Director condition C2) and owner decision F1 (2026-11-05, portrait 720 x 1280 at 320 dpi is a DEV-2 class with n_min 2).

## Status

| Gate | Evidence | State |
|---|---|---|
| SC-001, blind naming in grayscale and deuteranopia, protanopia, tritanopia, zero confusions, incl. markers (DEV-3) | 6 sets x 4 filters x 22 tiles in [`readability-final/sc001/`](readability-final/sc001/README.md) | **sheets ready; independent naming pending** |
| SC-001 at 1280 x 720 n = 2 and portrait 720 x 1280 n = 2 (condition C2) | sets `phoneL-1280x720-n2`, `phoneP-720x1280-n2` | sheets ready; naming pending |
| SC-002, owner, 9 of 10 within 2.0 s with per-state breakdown (standard, running machines animate) | [`readability-final/sc002/standard/`](readability-final/sc002/OWNER-RUN.md) | **owner run pending** |
| SC-002 reduced-motion variant (G2) | `readability-final/sc002/reduced-motion/` | owner run pending |
| Owner visual confirmation of the sheets | T447 | pending |

No result below this line exists yet. Naming transcripts, confusion lists, the owner's logs and counts are appended in the "Results" section by the parent.

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

Per set (class and `n`): 22 tiles, each one slot's 80 x 64 art px canvas cut around the slot pivot from an isolated capture (neighbours removed, floor kept) at device scale 1x. Tiles: miner and assembler x six states as plate-with-machine (Art Bible 8.5: no bare occupied plate), empty plate, locked plate L2 to L5 with the digit overlay, Boosted, Overdrive, both markers, selected machine, selected empty plate. Each set is rendered with `art_pipeline.checks` as grayscale and deuteranopia, protanopia, tritanopia; every filter sheet has its own seeded shuffle (seed + filter index), so the reviewer cannot carry numbers over. Vocabulary: [`vocabulary.md`](readability-final/sc001/vocabulary.md) (slot condition, machine type, state, marker, selection, certainty). Gate: zero confusions per set and filter (spec SC-001, DEV-3 blocking). Sets, seeds and tile sizes: [`readability-final/sc001/README.md`](readability-final/sc001/README.md). Keys: outside the repository, held by the parent, never opened by the author.

| Set | Class | n | Zoom | Tiles | Seed |
|---|---|---|---|---|---|
| `emuP-1080x2400-n3` | emulator portrait | 3 | n_min | 22 | 4450 |
| `emuP-1080x2400-n4` | emulator portrait | 4 | fit-all | 22 | 4460 |
| `phoneL-1280x720-n2` | phone landscape (C2) | 2 | n_min = fit-all | 22 | 4470 |
| `phoneP-720x1280-n2` | phone portrait (C2) | 2 | n_min = fit-all | 22 | 4480 |
| `tabL-2048x1536-n2` | tablet landscape | 2 | n_min | 22 | 4490 |
| `tabL-2048x1536-n4` | tablet landscape | 4 | fit-all | 22 | 4500 |

### SC-002 trials

Ten seeded 12-slot screens per set from in-game captures (not composed from loose sprites as in the spike): emulator portrait 1080 x 2400 at `n_min` = 3, world with floor plus the real shell (top strip, tab bar, zoom controls, from a bound W003-S session; the HUD text does not describe the forced machines). One problem machine per screen (waiting, missing input, storage full, balanced 4 / 3 / 3 in a seeded order), one or two benign distractors (paused or inactive), the rest running machines (80 %), empty (10 %) and locked (10 %) plates. Machine types vary over miner, smelter, press, assembler (the spike used miner and assembler only). One unscored practice screen per set.

| Set | Motion | Seed | Frames |
|---|---|---|---|
| `standard` | running machines animate (machine body and lamp, 6 frames at 8 fps, as the shared presentation clock drives them); every non-running state is frame 0 and still, so the stuck machine is the still one | 4461 | animated PNG per screen, 125 ms per frame |
| `reduced-motion` | reduced motion on: `AdvanceFrames(0, frozen: true)`, every machine frame 0, nothing moves | 4462 | static PNG per screen |

What differs in reduced motion: only the running machines' body frames (the animation stops and the lamp stays on frame 0); the state cues are identical in both sets, because badges are static by design. The two sets use different seeds and layouts so the owner cannot recall positions. The page ([`sc002/OWNER-RUN.md`](readability-final/sc002/OWNER-RUN.md)) logs, per trial, the nearest slot, the click position in screen pixels, the time from first painted frame to click, and window and display-scale facts. Scorer and report: `tools/art-pipeline/trials/final_readability.py score` (9 of 10 correct within 2.0 s, per-state breakdown, median). Keys: outside the repository, never opened by the author.

### Tooling

`tools/art-pipeline/trials/final_readability.py` (plan, sc001, sc002, score) and `tools/art-pipeline/tests/test_final_readability.py` (determinism, balance 4 / 3 / 3, one problem per screen, shuffle per filter, keys never in the output folders, motion versus reduced motion, scoring). The older harness `timed_trial.py` (F003 rectangular lattice, composed sprites) is not used for the final trial.

## Results

*Not run yet.* Appended by the parent: SC-001 naming per set and filter (counts, unsure tiles, hard pairs, confusions), SC-002 per-set count, per-state breakdown and median time for both variants, then the gate decision and the owner's confirmation (T447).

## Limits

- Windows Editor renders at the stated device pixel sizes and densities; no physical device, no emulator capture. The Android player may differ in GPU rounding, not in the integer scale model.
- Tiles are isolated slots (neighbours removed), the SC-002 screens are full 12-slot screens; the full-grid proof of FR-015 is not part of this record.
- The HUD in the SC-002 screens comes from a bound stress session and is not consistent with the forced machine states; it is never a cue for the answer.
- SC-002 is shown on a desktop browser scaled to a phone-shaped window; physical size and viewing distance differ from a phone. Nearest-machine hit testing is used instead of an exact hit shape.
- The owner knows the art; SC-002 is a recognisability check, not a naive-player study. Only one independent reviewer session is the recorded SC-001 method (Art Bible 9.5).
- The temporary capture test is not in the repository; the raw captures are not committed. Regeneration needs a new capture run with the same specification (`final_readability.py plan`).
