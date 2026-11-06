---
title: F004 batch 12 final verification
type: decision
sources: [S055]
updated: 2026-11-05
---

# F004 batch 12 final verification

Batch 12 closed Feature 004 verification: T442 final Test/QA, T446 eight lens reviews, T444 B3 performance, T445 readability gates, T447 owner acceptance, T476 deletion of the F003 placeholders. Records: `reviews/final-test-evidence.md`, `final-test-rebaseline.md`, `final-acceptance.md`, `performance-b3.md`, `readability-final.md`. (S055)

## Test evidence (T442, commit 701e8c8)
Edit Mode 2566, Play Mode 2572, Windows player 62 (tagged `StandalonePlayer`), art pipeline 220, all passed. After T476 the player suite passed 62/62 on main 40d73b1 (parent report). The name diff against B1 removes 3 Edit and 30 Play cases, all mapped to renamed or re-baselined replacements (SC-006). A real two-finger pinch cannot be injected on the emulator (`sendevent` denied); it stays unverified. The scripted `F004Crispness` journey step failed once because a fixed-coordinate dismissal missed the Welcome-back sheet; it passed only after a manual dismissal, so it is not a clean journey PASS (accepted gap F4). `RestyledSceneSmokePlayerTests` never existed; the 62 tagged cases cover it in substance (F2). (S055)

## Lens reviews (T446)
Seven of eight lenses returned ACCEPT WITH CONDITIONS, Security ACCEPT; no blocker. Owner decision F1: the portrait 720x1280 phone at 320 dpi is a DEV-2 class with `n_min` 2; Art Bible v1.0.4 corrected sections 4.1/4.3, DEV-2, DEV-4. (S055; see [pixel camera scale](./pixel-camera-scale.md))

## Performance B3 (T444)
Met: world-art texture 3.05 MB (cap 8), atlas pages within 1024, APK 46.4 MB, shader variants 356 (cap 490), advance and presentation paths 0 GC. Budget 8 (SetPass) was restated by the owner in two tiers: a single pass up to about 173 renderers, above that `camera.Render` CPU time (tier 1 and 2). Whole-host-frame GC stays PR-2 (six samples per 300 frames), accepted. Emulator rows 1, 2, 3, 9 are an accepted gap: B3 43.4 FPS against B1 44.6 under the same throttle. No physical-device data (FR-027). OPS-4 Play Mode 2248/2248 vs 2275/2275; OPS-7 revert dry run clean. (S055; see [emulator profiling](./android-emulator-profiling.md))

## Readability (T445)
SC-001: gate fields 22/22, zero confusions on all 24 sheets (6 sets x 4 filters), including 1280x720 and 720x1280 at n=2. Machine type was named with swapped labels by reviewer A in three sets because the vocabulary gave no reference picture; reported separately. SC-002 standard: owner 8 of 10 within 2 s, not met, accepted without rerun; the reduced-motion variant was not run. The owner trial ran in a 1864x893 desktop-sized browser window, not phone-sized. (S055)

## Owner acceptance (T447) and placeholders (T476)
Owner accepted all families. Real-finger checks (tap at `n_min`, pinch, slow pan, font scale 2.0, rotation after Welcome-back) are not verified: no physical device. Option D chosen: the F003 placeholder sprites and sources are deleted (T476, #672). FR-024 now fails closed: a missing or invalid frame makes the catalog invalid, no fallback sprite is drawn, and the shipped scene sets `RequireRegistryRows`. See [presentation boundary](./unity-presentation-boundary.md), [F003 pipeline](./f003-art-pipeline.md). (S055)

Related: [batch 12 pitfalls](./f004-batch-12-workflow-pitfalls.md), [batch 11](./f004-batch-11-floor-settings-icons.md), [visual identity](./visual-identity-art-direction.md).
