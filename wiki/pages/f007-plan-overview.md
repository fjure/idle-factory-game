---
title: Feature 007 plan overview (planned)
type: decision
sources: [S064]
updated: 2026-11-27
---

# Feature 007 plan overview (planned): stylized industrial visual reboot

F007 replaces the D24 pixel-art presentation with a richer stylized industrial 2.5D look. It changes no gameplay, economy, content or save state. Everything below is a plan, not a result; numbers marked hypothesis are unmeasured. (S064)

## Milestones

M1 audit (incl. a physical-phone baseline of the F006 look); M2 briefs, shared foundations, three full-parity prototypes and the comparison; M3 owner decision; M4 Art Bible v2, pipeline v2 freeze and budget set v2; M5 incremental production; M6 final acceptance. Tasks are generated for M1 to M3 only (T600-T699 reserved, T600-T641 proposed); M4 to M6 are generated after the M3 record, followed by analyze, ledger refresh and MAQA sync. Rejected: all tasks now with three conditional branches, and separate exploration and production features. (S064)

## Owner decisions (2026-11-27)

- OD7-1: F006 closes in parallel; AD-1, AD-4, AD-7 move to F007; PERF-9 is closed by the M1 phone baseline; C-68 stays with F006.
- OD7-2: all three directions at full prototype parity (no reduced-fidelity screening).
- OD7-3 / OD7-9: performance evidence on a physical Google Pixel 11 Pro (Android 17); no tablet exists, so the tablet class is the Windows player's tablet resolution.
- OD7-4: one shared dusk mood through World Bible v0.3 (approval before M2 scoring). OD7-5: families go live as they merge; a temporary style mix is accepted.
- OD7-6 / OD7-15 (hybrid): short upgrade effect plus 3 build-out stages that follow level (stages 0–2 at 10 and 25; the second threshold may move to 50) and drop back with prestige, plus a persistent facility finish tier from the persisted `PrestigeCount` (2-4 steps, survives resets). Neither adds a reward or save field.
- OD7-7: small integrated status indicator replaces free-standing badges. OD7-8: denser on-screen location geometry approved in principle; concrete geometry decided at M3.
- OD7-13 / OD7-14: owner plays each direction on the Pixel before M3; three go/adjust checkpoints in M2 (after briefs plus WB v0.3, after one machine per direction, at the C1 result); no elimination without the owner's word.
- Pending: OD7-10 WB v0.3 approval, OD7-11 rubric anchors, OD7-12 M3 direction and geometry. (S064)

## Seam and lane

A presentation-only **look profile** (`F006` default, `F007-A/B/C` prototype, later `F007`) selects catalog, indicator glyphs, environment, layout pitch and camera policy. The bootstrap owns one resolver; DevTools injects an `ILookProfileSource` at `BeforeSceneLoad`. Prototype assets sit behind a dev-only scene added only for `IDLEFACTORY_DEV` builds, checked through `BuildReport.packedAssets`. Rejected: branches per direction, scene copies. Layout pitch becomes instance data in world units and part of `LayoutComposition` identity. Lane is MAQA; phone runs hold `unity,emulator,gpu`, and every run passes `-Serial` when both devices are attached. (S064)

All ten plan-review lenses returned APPROVE WITH CONDITIONS with no blocker; the accepted findings are applied (see `reviews/plan-reviews.md`). (S064)

See [directions and measurement plan](./f007-directions-and-measurement-plan.md), [visual identity](./visual-identity-art-direction.md), [F006 plan overview](./f006-plan-overview.md), [URP foundation](./urp-rendering-foundation.md).

## Batch B0 status (2026-11-27)

Gates re-verified, T602 and T638 merged, T603 blocked, T601/T604 await the Pixel. See [batch B0 gates and tooling](./f007-batch-0-gates-and-tooling.md) and [baseline capture lessons](./f007-baseline-capture-lessons.md). (S064)
