---
title: F004 S1 decision and S2 spike
type: decision
sources: [S046, S047, S049, S050, S051]
updated: 2026-10-30
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

## S2 evidence and owner decision (T419 #531 PR #602; T420 #532 PR #603)

**D24 on isometric 2:1**, owner decision 2026-10-29. (S046, S049)

- Zoom/margin table per density: on the 320 dpi phones both densities are fit-all only (no zoom-out); D24 also leaves the portrait tablet fit-all only and violates `n_fit >= n_min` on the 1280x720 phone (owner-approved 1 % `n_min` tolerance, T427). D16 has no violation there. (S046)
- D16 is not crisp at n = 3 on Windows (95.1 % of runs are multiples of n, gcd 1) from the half-art-pixel x pivot; D24 has 96.8-97.5 % of vertical edges on grid (ore/ingot pivot y = 4.5). Both are T424 fixes. (S046)
- Icon room: a 12 art px icon is 24.0 pt at D16 on 320 dpi phones but 18.2 pt on the emulator; D24 needs 17 art px (phones) or 22-23 (emulator, tablet landscape) for 24 pt. (S046)
- AD condition 3 answered: B reads at `n_min` on phone portrait at both densities (type, lit/inactive, selection, locked plate, ore, ingot). (S046)
- Second-session blind naming FAILS on both densities: warning vs paused (as in S1) and the bare-plate slot conditions empty/occupied/locked (new vs S1). Verdict: AGREE WITH CONDITIONS for D24. (S046)

Seven AD conditions and where they landed: (1) T427 tolerance plus HUD-aware Play Mode proof, fit-all-only recorded as accepted risk; "before the freeze" cannot be literal because T427 depends on T426; (2) cue icons at 17x17 art px or larger (superseded: S3 measured 17 px short of 24 pt on the emulator and tablet landscape, so the final size is 23 px; see [S3/S4 decision](./f004-s3-s4-cue-and-lighting-decision.md)) (S050), overlay badge, shape-coded warning vs paused, IoU at most 0.70 (T421); (3) full naming gate re-run on the S3 winner with plate-with-machine sheets and zero confusions (T422); (4) whole-pixel pivots, 100 % crisp, shimmer, contrast, then freeze (T424); (5) isometric slot lattice (T456); (6) evidence debt: sprite-level import tests, effort per asset, physical-device captures (T422); (7) no D16 redo round. Task texts T421, T422, T424, T427, T456 were extended. (S046, S049)

Workflow lessons: [F004 batch 6 workflow pitfalls](./f004-batch-6-workflow-pitfalls.md).

## Batch 8 status (2026-10-30)

Merged: T424, T425, T461, T426. The Art Bible v1.0 was signed off; see [Art Bible review and sign-off](./f004-art-bible-v1-review-and-signoff.md). (S051)

Related: [F004 S1 spike assets](./f004-s1-spike-assets.md), [F004 isolated spike import](./f004-t414-spike-import.md), [visual identity and art direction](./visual-identity-art-direction.md).
