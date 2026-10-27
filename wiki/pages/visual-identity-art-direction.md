---
title: Visual identity and art direction
type: decision
sources: [S038, S040]
updated: 2026-10-27
---

# Visual identity and art direction

Feature 004 replaces the Feature 003 placeholder art with a 2.5D pixel-art/cartoon look and changes no gameplay, content or save state. The provisional Art Bible `F003-PAB v0.1` was a development placeholder and is superseded section by section by an independently approved Art Bible v1.x. (S038)

## Owner decisions (2026-10-01)

- Style: 2.5D, pixel-art/cartoon-like; recommended projection 3/4 top-down, isometric as a spike candidate; machines do not rotate. (S038)
- The rendering foundation moves to URP 2D as phase 0 of the feature; see [URP rendering foundation](./urp-rendering-foundation.md). (S038)
- Only whole-number pixel scales, including fit-all; unused space is margin; see [pixel camera scale](./pixel-camera-scale.md). (S038)
- Generated art may ship; the tool is chosen per asset family by agents because the owner does not produce art; see [art production pipeline](./art-production-pipeline.md). (S038)
- Reduced motion follows the OS and can be overridden in the game; the override lives in a separate local display-preferences file, never in the save. (S038)
- An independent agent names the grayscale and color-vision sheets; the owner runs a timed at-a-glance trial and accepts results. (S038)

## State cues

The owner questioned permanent per-machine badges, and discovery found 4-letter labels unreadable at the smallest pixel scale. The style spike compares (a) the machine body carrying the state (animated when running; dimmed when paused or off; warning lights plus a per-state icon for missing input, output full and waiting; no cue when running) against (b) redesigned pixel badges. Every state keeps a distinct shape, because color alone fails: WAIT and FULL are indistinguishable by color under protanopia. Cues and markers draw on an overlay layer above all machine art; tall machines never capture taps outside their footprint. (S038)

## Style spike and Art Bible

A spike in the running game (phone, tablet, emulator) decides projection, art density, cue variant and lighting before the Art Bible freeze and before any production asset. Producers never review their own output. A projection, density or palette change is a new major Art Bible version with a migration decision. (S038)

## Re-validation and scope update (2026-10-27)

- Plan re-validated against `main` after Features 003 and 005; re-reviews passed with fixes. (S038)
- Catalog: the merged T110 `PresentationSpriteCatalog` validates a hard-coded table of 21 F001 keys that pins sprite names. F004 replaces it with a per-key requirement registry. Decision: F001 keys are no longer start-up requirements, because no runtime view uses them. (S038)
- State cues map the badge keys that `SlotGridModel.BadgeFor` resolves (running, waiting, missing-input, storage-full, paused, inactive). No second precedence rule is allowed. (S038)
- Owner decision: the Feature 005 UI follow-ups (R-20 focus target, R-23 hint by resource, money and "0 s" copy, offer reason line, re-sort focus test, obsolete `PreventDefault`) became User Story 8. They are presentation-only. (S038)
- Play-Tester is omitted for every F004 batch with a recorded reason: no batch changes production, economy or core-loop rules. (S038)
- Task IDs: MAQA maps Issues by the task ID in the title across features. F004's T193–T242 collided with Feature 003 follow-up Issues that never appeared in any `tasks.md`, so F004 moved to T400–T455. Check the Issue titles (`gh issue list --label maqa --state all`), not only `tasks.md`, before reserving an ID range. (S038)

## Batch 1 status (2026-10-27)

MAQA batch 1 closed: T400-T407 tooling and T409 baselines. See [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md) (pitfalls are linked there). Palette v0 stays a placeholder until the style spike. (S040)

Related: [Unity presentation boundary](./unity-presentation-boundary.md), [core architecture](./core-architecture.md), [pixel camera scale](./pixel-camera-scale.md).
