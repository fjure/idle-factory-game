# Badge pointer tail (K4): asset brief

**Brief version**: `F006-CUE-TAIL v0.1` (T524, Issue #697). Status: production candidate for the Art Director pixel review in T525. The producer does not approve its own art; `reviewedBy` in every `provenance.json` is `pending`.
**Authority**: owner decision OD-12 (Art Bible `F004-PAB v1.1.0`, section 21.8 item 4); measurement `specs/006-world-first-factory-experience/reviews/a03-anchor.md` (T517, candidate K4); follow-up P1-F10 (`reviews/p1-followups.md`); OD-9b UI/UX C2, C4, C5 and Art Director conditions 1 and 2 (`reviews/direction.md`). Covers FR-014, FR-025; research R17.

## Asset

| Field | Value |
|---|---|
| Asset ids | `f003-badge-waiting`, `f003-badge-missing-input`, `f003-badge-storage-full`, `f003-badge-paused`, `f003-badge-inactive` (the five F004 state badges; running stays intentionally empty, R8) |
| Type | state cue badge (F004 cue sprite b23) with a pointer tail; overlay layer, scripted route (research R07) |
| Perspective | Flat 2D overlay, drawn as the F004 badges: no perspective, 2:1 world lattice around it. The tail points straight down in screen space at the badge's own machine plate; it carries no isometric skew |
| Canvas, pivot | 80 x 64 art px, pivot (40, 15) from the bottom-left (unchanged; the layer is placed at the footprint pivot, scale 1) |
| Box (unchanged) | 23 x 23 art px at canvas x 48..70, rows 10..32; bottom-centre anchor (59, 33). Never rescaled (Art Bible 4.3), so 23 px legibility at `n_min` is unchanged (DEV-2 unchanged) |
| Tail | 4 art px below the box, rows 7, 5, 3 and 1 px wide, centred on canvas column 59 (rows 33..36). Opaque bounding box 48, 10, 71, 37 = 23 x 27 px |
| Tail colours | `ink_0` on the two edges of each row and the tip; the badge's own reserved cue fill inside. The `ink_0` bottom row of the box turns to the fill under the 7 px tail (7 px changed) so the tail joins the body. Palette ids only (`f004-palette` 1.0.0), no new colour |
| States | One per badge sprite; fill by state: waiting `cue_info`, missing-input `cue_alert`, storage-full `cue_warn`, paused `cue_neutral`, inactive `cue_dim` (Art Bible 8.1, 8.2). The tail is identical in shape for all five |
| Frames, format | 1 frame, PNG RGBA, alpha 0/255, 24 PPU, nearest-neighbour, no compression artefacts; path `art-source/f006/cue/<assetId>/<assetId>_00.png` |
| Rules kept | Art Bible v1.1.0: 3.2 cue geometry table (anchor unchanged), 8.4 (badge box), 21.8 item 1 (C8 ring: no `steel_*` in any ring, enforced by `check_overlay_backdrop` on the declared tones and the plate reference), item 4 (this tail). Outline `ink_0`, palette gate, reserved colours exactly `{ink_0, fill}` |

## Measured in T517 for this geometry (worst slot of the densest 12-slot frame)

Row band overflow 0, lattice extent overflow 0, foreign plate px 0, foreign hit shape px 0, own marker px 0, own branch px 0, own frame hidden 0, attribution ratio 1.14 (a directional cue, not proximity), gap to the own plate 2 px (tail tip), backdrop tone changes per halo px 0.129. Legibility: 23 px at `n_min`, 26.23 pt at 1080 x 2400 and 2400 x 1080, 34.12 pt on the tablet, 23.00 / 22.95 pt at 640 x 360 @2 and 720 x 1280 / 1280 x 720 (DEV-2).

## Provenance

Scripted, repository-authored, no generative model, LoRA or training data. Generator: `tools/art-pipeline/art_pipeline/f006/cue_tail.py` (`SCRIPT_VERSION t524-1`) on top of the F004 production (`cues.py`, `cues_production.py`, script version `t437-1`). Regenerate from `tools/art-pipeline`:

`uv run python -m art_pipeline cue-tail --out ../../art-source/f006/cue`

Hashes of the sources, the pixel pass steps, the check report hash and the output hashes are in each `provenance.json`. The output is byte-identical on a rerun (`tests/test_cue_tail.py`). The production tail is pixel-identical to the T517 K4 mock for the two badges the mock used (`test_production_tail_equals_the_t517_mock`).

## Checks run

The F004 badge gates (`cues_production.check_sprite`: palette, reserved colours, canvas, pivot, alpha, outline, cue icon size, glyph size, contrast, overlay band, backdrop and outline contrast on every declared tone and the plate reference, accent, reproduction) plus `tail_geometry` (exact row widths and colours, only the 7 joint pixels of the box changed, bounding box 23 x 27). Report: `check-report.json`. Review sheets (seeded shuffle, grayscale and three CVD modes): `sheets/`.

## For the Art Director (T525)

Pixel review of the five `_00.png` files at n = 1 and n = 8 on the plate and floor tones: tail legibility and join with the box, the `ink_0` joint row, the tip pixel, colour-blind sheets. Open item for the parent: the imported sprites in `Assets/IdleFactory/Art/F004/cues/` are not replaced by this task; the swap is an integration step after the review passes (Unity tooling, regenerate import manifest).
