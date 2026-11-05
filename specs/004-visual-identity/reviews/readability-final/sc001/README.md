# SC-001 sheets of the shipped build (T445, stage A)

Blind-naming material for the independent reviewer (spec SC-001, Art Direction condition C2, owner decision F1 of 2026-11-05). Nothing here is a verdict; the evidence author did not name any tile and did not open the answer keys.

## What a sheet is

- Source: temporary Play Mode captures of the shipped code at commit `c027625` (origin/main): the production `SlotGridView` with the authored scene sprite catalog, the production `FactoryCameraController` integer scale model, the authored `CoreGameFoundationPanelSettings` shell (real HUD rectangle) and the floor on. Presentation states are forced; Core state and saves are untouched.
- Tile = one slot's 80 x 64 art px canvas (pivot 40, 15 from the bottom) cropped around the slot pivot at the device scale `n`, one slot per render (other slots removed so neighbours do not bleed in). Tiles are used at 1x.
- 22 tiles per set: miner and assembler in the six states (running, waiting, missing input, storage full, paused, inactive) as plate-with-machine; the empty plate; the locked plate with the lock-level digit 2, 3, 4, 5; Boosted marker, Overdrive marker, both markers; a selected machine and a selected empty plate.
- Each set is rendered as grayscale (Rec. 709 luma) and deuteranopia, protanopia, tritanopia (Machado, severity 1.0) with `art_pipeline.checks`. Tiles are numbered row-major, 5 per row. Every filter sheet has its own seeded shuffle.
- Keys (tile id, condition, machine, state, markers, selection per number and filter) were kept outside the repository until the naming was done; they are now in `keys/`, with the namers' transcripts and the scoring in `naming/`.

## Sets

| Set (folder) | Screen class | Zoom | n | px per pt | Tile px | Seed |
|---|---|---|---|---|---|---|
| `emuP-1080x2400-n3` | phone portrait 1080 x 2400, 420 dpi (emulator class) | n_min | 3 | 2.631 | 240 x 192 | 4450 |
| `emuP-1080x2400-n4` | same | fit-all | 4 | 2.631 | 320 x 256 | 4460 |
| `phoneL-1280x720-n2` | phone landscape 1280 x 720, 320 dpi (DEV-2, DEV-4) | n_min and fit-all (both 2) | 2 | 2.004 | 160 x 128 | 4470 |
| `phoneP-720x1280-n2` | phone portrait 720 x 1280, 320 dpi (DEV-2 class since F1) | n_min and fit-all (both 2) | 2 | 2.004 | 160 x 128 | 4480 |
| `tabL-2048x1536-n2` | tablet landscape 2048 x 1536, 264 dpi | n_min | 2 | 1.867 | 160 x 128 | 4490 |
| `tabL-2048x1536-n4` | same | fit-all | 4 | 1.867 | 320 x 256 | 4500 |

Seed of a filter sheet = set seed + filter index (grayscale 0, deuteranopia 1, protanopia 2, tritanopia 3); order = `random.Random(seed).shuffle(sorted tile ids)`. The two sets that satisfy the Art Direction condition C2 (blind naming at 1280 x 720 and 720 x 1280, n = 2) are `phoneL-1280x720-n2` and `phoneP-720x1280-n2`.

## How to name

See [`vocabulary.md`](vocabulary.md). Pass rule (spec SC-001): zero confusions on every sheet in grayscale and in all three colour-vision filters, including the markers (DEV-3, blocking). The owner confirms the sheets visually in T447.

## Regenerate

`tools/art-pipeline/trials/final_readability.py sc001 --raw <captures> --out specs/004-visual-identity/reviews/readability-final/sc001 --keys <folder outside the repository>`; tested by `tools/art-pipeline/tests/test_final_readability.py`. The raw captures and the temporary capture test are not committed.
