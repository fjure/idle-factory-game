---
title: F004 Art Bible v1.0 review and sign-off
type: decision
sources: [S051, S046]
updated: 2026-10-30
---

# F004 Art Bible v1.0 review and sign-off

Batch 8 wrote, reviewed, revised and signed off the Art Bible `F004-PAB v1.0` (`docs/art/F004-art-bible-v1.md`) and the briefs `F004-AB v1.0` (`docs/art/F004-asset-briefs-v1.md`). (S051)

## Content (T424, #536, PR #613)

- Density D24: PPU = D = 24 (D24 sprites), cue variant b at 23 px, lighting B realised as L1 accent sprites with 0 Light2D, with an L2 Light2D envelope reserved. (S051, S046)
- Contrast run: 39 of 53 pairs pass, and the 14 fails are explained in the document. (S051)

## Review (T425, #537, PR #614)

Three independent read-only reviews (second-session Art Director, UI/UX, Game Designer/Product Owner) all returned APPROVE WITH CONDITIONS and were recorded verbatim in `reviews/art-bible-review.md`, with consolidated conditions R1 to R15. One blocker: R1, the rim rule C5 contradicted sections 6.1 and 2, and the spike Miner failed it. (S051)

## Owner decisions (2026-10-29)

- **R1:** option (b): a graphic passes if its ink outline OR its rim reaches 3:1 against every backdrop tone, measured as a composite-mask share (threshold 90 %). (S051)
- **R4:** lock-level digit at scale 3; the slot panel names the level. (S051)
- **R7:** markers stay 11x11 as a named deviation; marker and CVD naming is blocking in T439 and T445. (S051)
- **R8:** the running badge is an `intentionallyEmpty` manifest entry without a PNG. (S051)
- New tasks T461 (#615, Art Bible revision before the freeze) and T462 (#616, art-pipeline v1 checks and `palette-v1.json`, after T426 and before T434 to T438). The `tasks.md` drift was fixed; the ledger has 63 units. (S051)

## Revision (T461, #615, PR #617, af0ee63)

Covers R1 to R5, R7, R8, R11, R12, R15: footprint box 68x30 and plate diamond 70x35, deviations DEV-3 and DEV-4, ComfyUI pin v0.43.0 in contract, plan and research. QA round 1 could not reproduce the bare-floor rim shares because its script dropped off-canvas neighbours; section 9.3 now states the method exactly and reproduces 56 pairs at 62 % (`floor_0`) and 58 pairs at 60 % (`floor_1`). (S051)

## Sign-off (T426, #538, PR #618, 2026-10-30)

The owner approved v1.0 at the reviewed content of commit af0ee63, including DEV-1 to DEV-4 and accepted risks AR-1 and AR-2. The spike Miner still fails the 90 % rim rule over bare floor; that is a T434 requirement, not a reopened decision. The spike import path is deleted by T440 after the production import. Ready next: T427, T428, T430, T462. (S051)

Related: [visual identity](./visual-identity-art-direction.md), [S3/S4 decision](./f004-s3-s4-cue-and-lighting-decision.md), [batch 8 pitfalls](./f004-batch-8-workflow-pitfalls.md).
