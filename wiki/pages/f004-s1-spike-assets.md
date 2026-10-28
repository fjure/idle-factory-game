---
title: F004 S1 spike assets and silhouette gate
type: reference
sources: [S042, S045, S046]
updated: 2026-10-28
---

# F004 S1 spike assets and silhouette gate

T415 (#527, PR #582) produced the style-spike S1 assets at art density D=24 on the Blender route, for both candidate projections, under `art-source/f004/spike/s1/`. The machine pair is Miner/Assembler, taken from the F003 provisional Art Bible IoU table (0.612). (S042)

## Result

| Projection | Silhouette IoU, full | At 32 px | Gate (at most 0.70) |
|---|---|---|---|
| Oblique 3/4 | 0.759 | 0.789 | FAILS |
| Isometric 2:1 | 0.651 | 0.672 | PASSES |

The gate favours isometric 2:1 for this pair. The projection decision itself belongs to the spike review and the owner, not to this measurement. QA re-rendered one asset byte-identically (R09). (S042)

## Generator learnings

- The rig world fill of 0.15 needed compensation in the generator (`LIT_GAIN` 2.0, `AMBIENT` 0.25). (S042)
- The overhang rule caps machine height at about 0.55 units. (S042)
- JSON written with CRLF on Windows broke the recorded `checkReportHash` after Git LF normalization; write with `newline="\n"`. (S042)

## S1 review outcome (T416)

T416 (#528, PR #590, merge `07dc3f4`) recorded the S1 review in `reviews/style-spike.md` (A = oblique 3/4, B = isometric 2:1). Palette lock, crispness (98 % of vertical edges on grid) and fit-all pass; the known phone-landscape `n_fit < n_min` case is left to T427. (S045, S046)

- Blind grayscale/CVD naming FAILS for both projections, tied, on state cues only: warning differs from paused by a few red pixels. S3 must prove cue icons and the naming gate is re-run on the S3 winner (SC-001). (S046)
- Recommendation and default: B. Second-session Art Director: AGREE WITH CONDITIONS (isometric lattice, re-run naming after S3, S2 legibility on phone portrait, half-pixel pivots leave about 2 % of vertical edges off-grid). (S046)
- Owner decision is pending (T417). The evidence has no physical device. (S045, S046)
- Spike import into Unity: [F004 isolated spike import](./f004-t414-spike-import.md).

Related: [art production pipeline](./art-production-pipeline.md), [visual identity and art direction](./visual-identity-art-direction.md), [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md).
