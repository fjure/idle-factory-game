---
title: F004 S3 cue and S4 lighting decision
type: decision
sources: [S050, S046]
updated: 2026-10-29
---

# F004 S3 cue and S4 lighting decision

Batch 7 closed the style-spike record S1 to S4 on the D24 isometric 2:1 miner. (S050, S046)

## Variants (T421, #533, PR #606)

- **S3 cue:** (a) warning-coloured body plus shape-coded icons; (b) uniform pixel badges per state, none on a running machine. Badges at 17 and 23 art px, anchored at (59, 33).
- **S4 lighting:** A baked, B flat body plus a 2D-light accent sprite.
- 51 sprites under `Assets/IdleFactory/Art/F004/Spike/D24/isometric-2-1-s3/`.
- Worst body IoU 0.651 at full size and 0.672 at 32 px (gate at most 0.70); composites with the same badge on both machines 0.77 and 0.78.
- 24 pt rule: 23 px passes at n_min on all four screen classes; 17 px misses on the emulator (19.4 pt) and tablet landscape (18.2 pt).

## Evidence (T422, #534, PR #607)

48 in-game captures (Windows offscreen at authoritative px/pt, emulator GLES3 APK), SC-001 sheets, the SC-002 harness `tools/art-pipeline/trials/timed_trial.py` and an S2 sprite-level import test. Independent blind naming scored 9/9 in all 6 sets and 4 filters (variant a: 3 unsure tiles per set). (S050)

## Owner trial (T423, #535)

- **Round 1:** a 6/10, b 8/10 within 2.0 s, so SC-002 failed. Owner chose b at 23 px, the IoU rule on bodies only, and lighting B.
- **T460 (#608, PR #610):** the waiting badge was redrawn (bold hourglass, `cue_info` fill, the only cool badge). Set b23r2 named 9/9 blind by a fresh session.
- **Round 2 (PR #611):** trial b-r2 scored 10/10 within 2.0 s, median 1.077 s. SC-002 passes.

**Final: S3 = b at 23 px, S4 = B.**

## Why

Blind naming tests whether a symbol is distinguishable without hints; the timed trial tests speed under a deadline. Both gates are needed, and only the timed one caught the weak waiting badge. IoU is measured on bodies because a badge changes the composite silhouette by design.

## Carried to T424

- Badge colour semantics: cool for waiting, warm for problems, neutral for paused or inactive; shape stays primary.
- Remaining 24 pt gaps: 460 dpi phones at 23.95 pt, a 1280x720 phone at n = 2 at 22.95 pt.
- Light2D render cost against the B2 budgets ([F004 B2 frozen budgets](./f004-b2-frozen-budgets.md)).

Related: [F004 S1 decision and S2 spike](./f004-s2-spike-and-s1-decision.md), [visual identity](./visual-identity-art-direction.md), [batch 7 pitfalls](./f004-batch-7-workflow-pitfalls.md).
