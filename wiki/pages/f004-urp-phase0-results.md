---
title: F004 URP phase 0 results and review
type: decision
sources: [S042, S043, S047, S048]
updated: 2026-10-29
---

# F004 URP phase 0 results and review

T411 (#523, PR #583) switched the project to URP 17.6.0 with a 2D Renderer: Unlit default sprite material, Gamma kept, HDR, depth, opaque texture, MSAA and post-processing off. T413 (#525, PR #584) is the independent review. Plan and gates are in [URP rendering foundation](./urp-rendering-foundation.md). (S042)

## Evidence

- Full suite: Edit Mode 2374, Play Mode 2275; the manifest is the B1 baseline plus 11 config tests. (S042)
- Screenshot parity 18/18, maximum channel delta 1/255. The comparator is R13-literal (owner decision 2026-10-27), which leaves the 0.1 % budget vestigial. (S042)
- Windows player 39/39; Android journey passes. Producing FPS was about 40, the same as B1 on the loaded host. (S042)
- APK 47.89 MB, +10.5 MB against B1; T412 rules on the size budget. (S042)
- Stripped variants: 490 with URP stripping on, 2262 without, from 95 shaders (B1: 693 from 52). (S042)
- A cold-run guard sets `ShaderUtil.allowAsyncCompilation = false`; the first cold-Library parity frame once failed at 18.9 %, probably from async shader compilation. (S042)
- B2 performance (T412, #524) is deferred by the owner to a quiet host and is not done. (S042)

## Review (T413)

Architect, Test/QA and DevOps each gave APPROVE-WITH-FOLLOW-UPS (S043). Open follow-ups: the vestigial budget, a global-settings guard, a package pin and a Light2D positive control. S4 2D lights need `m_Strip2DUnusedVariants` set to 0. (S043)

## URP settings pitfalls

- URP auto-creates GlobalSettings, DefaultVolumeProfile and ShaderGraphSettings. (S042)
- In the global settings asset the legacy top-level stripping fields differ from the authoritative managed-reference ones. (S042)
- Unity once emptied `m_List` in that asset, and the T453 run re-serialized it (reverted). Diff the asset after every Unity run. (S042)

## Batch 5 follow-ups (2026-10-29)

- **B2 done.** T412 measured B2 on a quiet host and froze budgets: [F004 B2 frozen budgets](./f004-b2-frozen-budgets.md). The earlier "deferred" statements above are superseded. (S047, S048)
- **T457 (#593, PR #597).** Vacuous 0.1 % budget tests relabelled or replaced so every parity assertion can fail (R13 literal, `DifferingThreshold = 2`); a guard for the global settings asset (`m_SettingsList.m_List`, `m_RuntimeSettings.m_List`, authoritative stripping values, with synthetic negative controls); URP 17.6.0 pin and a Renderer2D post-processing assertion; a Light2D positive control; three warm-up frames before parity capture (cold run on a fresh Library 20/20, 0 differing pixels in 18/18). (S047)
- **N-4 root cause.** `m_PostProcessData` non-null on `IdleFactory2DRenderer.asset` made Renderer2D build post passes that load stripped shaders (the 22 GaussianDepthOfField errors); set to null. (S047)
- **Guard limit.** The new guard catches an emptied list, not a partial trim or a dropped rid; keep diffing the asset after every Unity run. (S047, S048)

Related: [F004 batch 3 workflow pitfalls](./f004-batch-3-workflow-pitfalls.md), [F004 B1 Built-in baseline](./f004-b1-builtin-baseline.md).
