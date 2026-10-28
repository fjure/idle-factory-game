---
title: F004 S1 spike assets and silhouette gate
type: reference
sources: [S042]
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

Related: [art production pipeline](./art-production-pipeline.md), [visual identity and art direction](./visual-identity-art-direction.md), [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md).
