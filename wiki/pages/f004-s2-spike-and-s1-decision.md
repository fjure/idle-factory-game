---
title: F004 S1 decision and S2 spike
type: decision
sources: [S046, S047]
updated: 2026-10-29
---

# F004 S1 decision and S2 spike

## S1 owner decision (T417, #529, PR #595, 2026-10-28)

Projection **B, isometric 2:1**, the recommended default. Reason: it is the only candidate passing the silhouette gate (IoU 0.651 and 0.672 against the 0.70 limit); oblique 3/4 fails and would need a new candidate round. The five Art Director conditions carry as: an isometric slot lattice (new task T456); cue icons of at least 12x12 art px and at least 24 pt at `n_min` proven in S3, then the naming gate re-run; S2 must confirm legibility on phone portrait; half-art-pixel pivots resolved in the Art Bible rules (T424) before the freeze; no redo round for A. New tasks: T456 (lattice, #592), T457 (test hardening, #593), T458 (layout polish, #594). (S046, S047)

## S2 spike (T418, #530, PR #599)

The S1 set rebuilt on isometric 2:1 at D=16 and D=24 (S1 rig unchanged; D16 by scaling the S1 model by 16/24) under `art-source/f004/spike/s2/`. (S047)

| Density | Silhouette IoU, full | At 32 px | Gate (at most 0.70) |
|---|---|---|---|
| D24 | 0.651 | 0.672 | PASS (frames pixel-identical to S1) |
| D16 | 0.704 | 0.739 | FAIL |

Palette v0 group budgets pass at both densities (29 and 30 of 48 colors). Re-render is byte-identical. The D24 import lives in a separate folder (`D24/isometric-2-1-s2/`) so the exact S1 import test stays valid; importer settings were set through Unity with custom pivots from the manifest; the half-art-pixel pivot stays open. The D16 versus D24 verdict is for T419 and the owner. (S047)

Related: [F004 S1 spike assets](./f004-s1-spike-assets.md), [F004 isolated spike import](./f004-t414-spike-import.md), [visual identity and art direction](./visual-identity-art-direction.md).
