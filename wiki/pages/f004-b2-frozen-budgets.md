---
title: F004 B2 measurement and frozen budgets
type: reference
sources: [S047, S048]
updated: 2026-10-29
---

# F004 B2 measurement and frozen budgets

T412 (#524, PR #596) measured B2 (the URP phase-0 build at `b9221ca`) in one cold-booted ABA session (B1a, B2a, B1b, 30 FPS control, B2b, B1c) on a quiet host and froze the budgets that B3 (T444) must meet. All numbers are emulator or build-report evidence on one workstation (exploratory); there is no physical-device evidence (FR-027). (S048)

## B2 results against B1

- FPS on W003-S producing: B2 61.45 and 61.29 (mean 61.37), B1 mean 61.18; B1 spread 0.42 FPS; control 30.3 FPS valid. p95 present interval 17-18 ms; 0 intervals at or above 50 ms. (S048)
- Guest CPU +1.1 points (+3.2 %); PSS about +5 % (+25.9 MB), no budget. (S048)
- APK 46,330,863 B, +8.99 MB against B1 (budget +10 MB); 95 % of the growth is IL2CPP code and metadata from the URP and Core packages, not textures. Levers if needed: managed stripping level, unused URP features, Burst. (S048)
- Stripped variants 490 (B1: 693 from 52 shaders; URP: 95 shaders). (S048)
- SetPass per world-camera render at B2 (owner-requested addendum, 2026-10-29): 0.77-0.81 on W003-S and W004-V, against 7.86-13.96 at B1. The SRP Batcher makes the raw comparison misleading; batches and draw calls read 0 in this Editor backend, as at B1. (S048)

## Frozen budgets

Met at B2: average FPS at least 60, p95 bucket at most 19 ms, 0 intervals at or above 50 ms, CPU regression at most max(10 %, 2 points), APK growth at most +10 MB, variants not above 490 without explanation. Deferred to B3 (T444): Windows frame ms p95, 0 steady-state GC allocations per frame (known non-zero baseline: `RunShellPresenter.Refresh`, PR-2), world-art texture at most 8 MB, atlas pages at most 1024 x 1024. Batch count: B3 SetPass may not exceed the B2 value (0.78 on W004-V at 640x480) by more than 10 %, at most 0.86. (S048)

## Method learnings

- Build B2 from an empty `Library/` for a like-for-like comparison with B1; the build took 4 min 30 s. (S048)
- Unity dropped a `m_SettingsList` rid (`RenderingDebuggerRuntimeResources`) from the URP global settings asset during the builds; variant counts were unaffected. Restore with `git checkout`. (S048)
- Still open: Play Mode suite duration comparison (OPS-4), rollback dry-run repeat (OPS-7). (S048)

Workflow: [F004 batch 5 workflow pitfalls](./f004-batch-5-workflow-pitfalls.md). Context: [F004 B1 Built-in baseline](./f004-b1-builtin-baseline.md), [mobile performance targets](./mobile-performance-targets.md), [F004 URP phase 0 results](./f004-urp-phase0-results.md).
