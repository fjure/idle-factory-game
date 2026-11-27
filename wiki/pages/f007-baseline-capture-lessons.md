---
title: F007 baseline capture lessons (editor Play Mode)
type: howto
sources: [S064]
updated: 2026-11-27
---

# Deterministic baseline captures in editor Play Mode (T603 lessons)

T603 (#800, PR #834) is **BLOCKED**, not merged, after three unique QA failures on commits eee053f, a6eb469 and 9f62c16. The lessons are durable for any pixel-baseline capture. Related: [F007 batch B0](./f007-batch-0-gates-and-tooling.md), [pixel camera scale](./pixel-camera-scale.md). (S064)

- **HUD readouts are not frozen.** Store and cash come from the live session, which advances on wall-clock time before RuntimeHost is disabled. Fix: `CoreGameFoundationBootstrap.OverrideRunCompositionForTests` with a fixed UTC, 0 active seconds and an isolated temp root. (S064)
- **TextCore dynamic font atlas packing depends on glyph request order**, giving +-1 anti-aliasing jitter across sessions (one example: 7 px). Fix: pre-warm in a fixed order with `ClearFontAssetData` plus `TryAddCharacters` over a fixed character set. (S064)
- **World camera framing carries over between capture jobs.** A 640x360 cell captured first panned about 41 px. Reset or force framing per job, and prove determinism with a shuffled job order, not only repeated sessions in the same order. (S064)
- **A Windows player window cannot exceed the 1920x1080 host display.** Larger classes (tabL 2560x1600, emuP 1080x2400) use editor Play Mode with `PlayModeWindow.SetCustomRenderingResolution`; `buildKind` is `editor-playmode`. The owner has not confirmed this route. (S064)
- **Mutation-test the metric tests.** It found two gaps, IoU centering and outward rect rounding, since fixed. (S064)

Open owner decisions on #800: accept the editor-playmode route; unblock for one more fix round. (S064)
