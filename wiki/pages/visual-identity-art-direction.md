---
title: Visual identity and art direction
type: decision
sources: [S038, S040, S042, S043, S044, S045, S046, S047, S049, S050, S051, S053, S054, S055, S056, S057]
updated: 2026-11-11
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

MAQA batch 1 closed: T400-T407 tooling and T409 baselines. See [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md) (pitfalls are linked there). Palette v0 was a placeholder until the style spike; palette v1.0.0 was frozen with the Art Bible v1.0 and supersedes it for production art. (S040, S038)

## Batch 3 status (2026-10-28)

Merged: T415 (S1 spike assets), T411 (URP phase 0), T413 (phase-0 review), T453 (US8 polish review). The S1 measurement favours isometric 2:1 over oblique 3/4 on the silhouette gate; the projection decision is still open. See [F004 S1 spike assets](./f004-s1-spike-assets.md), [F004 URP phase 0 results](./f004-urp-phase0-results.md) and [F004 US8 polish review](./f004-us8-polish-review.md). T412 (B2) is deferred. (S042, S043, S044)

## Batch 4 status (2026-10-28)

Merged: T443 (F004 emulator scenario), T414 (spike import), T432 (display preferences), T416 (S1 review). S1 recommends B, isometric 2:1, with Art Director conditions; the owner decision is pending (T417). See [F004 S1 spike assets](./f004-s1-spike-assets.md), [F004 isolated spike import](./f004-t414-spike-import.md), [F004 display preferences store](./f004-display-preferences-store.md), [F004 emulator scenario](./f004-emulator-scenario.md) and [batch 4 pitfalls](./f004-batch-4-workflow-pitfalls.md). (S045, S046)

## Batch 5 status (2026-10-29)

Merged: T417 (S1 owner decision: B, isometric 2:1, with five Art Director conditions), T412 (B2 budgets), T457, T458, T418 (S2 spike: D24 IoU 0.651 passes, D16 0.704 fails; palette budgets pass). See [F004 S1 decision and S2 spike](./f004-s2-spike-and-s1-decision.md), [F004 B2 frozen budgets](./f004-b2-frozen-budgets.md) and [batch 5 pitfalls](./f004-batch-5-workflow-pitfalls.md). (S046, S047)

## Batch 6 status (2026-10-29)

Merged: T419 (S2 evidence and second AD review), T420 (owner decision S2 = D24 with seven AD conditions, mapped to T421, T422, T424, T427, T456), T459 (N-5/N-6 polish). See [F004 S1 decision and S2 spike](./f004-s2-spike-and-s1-decision.md), [F004 US8 polish re-review](./f004-us8-polish-review.md) and [batch 6 pitfalls](./f004-batch-6-workflow-pitfalls.md). (S046, S049)

## Batch 7 status (2026-10-29)

Merged: T421 (S3 cue and S4 lighting variants), T422 (in-game captures, SC-002 harness), T460 (revised waiting badge), T423 (owner trials; SC-002 passed in round 2). Final: S3 = variant b badges at 23 px, S4 = lighting B. See [F004 S3 cue and S4 lighting decision](./f004-s3-s4-cue-and-lighting-decision.md) and [batch 7 pitfalls](./f004-batch-7-workflow-pitfalls.md). (S050)

## Batch 8 status (2026-10-30)

Merged: T424 (Art Bible v1.0 and briefs, D24, PPU = D = 24), T425 (three independent reviews), T461 (revision R1 to R15), T426 (owner sign-off of v1.0 at af0ee63). Next ready: T427, T428, T430, T462. See [Art Bible review and sign-off](./f004-art-bible-v1-review-and-signoff.md) and [batch 8 pitfalls](./f004-batch-8-workflow-pitfalls.md). (S051)

## Batch 10 status (2026-11-04)

Merged: T456 (rectangular 70 x 54 lattice, Art Bible v1.0.2), T454 (pinch zoom), T463 (ore-vein cut), T440 (art import). Art Bible v1.0.3 records the 150 MB APK cap. Next: T470 floor re-render, T471 floor import. See [batch 10 components](./f004-batch-10-lattice-zoom-ore-vein-art-import.md) and [batch 10 pitfalls](./f004-batch-10-workflow-pitfalls.md). (S053)

Related: [Unity presentation boundary](./unity-presentation-boundary.md), [core architecture](./core-architecture.md), [pixel camera scale](./pixel-camera-scale.md).

## Status after batch 11 (2026-11-05)

Merged: T470, T471, T473 (floor), T472 (field floor in `SlotGridView`), T433, T475, T455 (Settings sheet, polish, review), T441 (restyled UI icons, blind naming); T474 closed as satisfied by T438 and T441. Blind naming flagged INFO, CASH, INDUSTRY/SETTINGS and the chevron group as confusable. See [batch 11 components](./f004-batch-11-floor-settings-icons.md) and [batch 11 pitfalls](./f004-batch-11-workflow-pitfalls.md). (S054)

## Status after batch 12 (2026-11-05)

Final verification merged (T442, T444, T445, T446, T447, T476). Owner accepted all art families; SC-002 standard trial 8 of 10 is an accepted failure, reduced motion not run, emulator performance rows and real-finger checks are accepted gaps (no physical device). Art Bible is v1.0.4. See [batch 12 verification](./f004-batch-12-final-verification.md) and [pitfalls](./f004-batch-12-workflow-pitfalls.md). (S055)

Planned (F006): Art Bible v1.1 is additive and gated by owner decision OD-10, after World Bible v0.1. See [F006 plan overview](./f006-plan-overview.md). (S056)

## F006 direction (2026-11-11)

F006 chose an open platform deck with a railing edge (direction A) on top of this identity; see [direction decision](./f006-p0-direction-decision.md). (S057)
