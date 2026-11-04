---
title: F004 batch 10 lattice, zoom, ore-vein cut and art import
type: component
sources: [S053]
updated: 2026-11-04
---

# F004 batch 10 lattice, zoom, ore-vein cut and art import

Batch 10 merged #592 (T456, PR #646), #540 (T454, PR #647), #623 (T463, PR #648) and #554 (T440, PR #649, merge `38068d3`). Close-out: Windows player suite 57/57 on `38068d3`, art pipeline 201 passed. (S053)

## T456 rectangular slot lattice

The isometric lattice is rectangular: 70 x 35 diamonds at pitch 70 x 54 art px. `SlotGridLayout` (in `SlotGridView.cs`) provides `PivotOf`, `HitTest` (a 90 x 45 diamond clipped to the cell), `Extent` and `RowOf`. `StateCueSet` and `CameraScaleModel` changed with it, and the B2 baselines were re-recorded. Owner decision 2026-11-04: the rectangular lattice is accepted and the Art Bible became v1.0.2 (section 3.5, floor period 70 x 54). OD-17 accepted: the 20 icon pairs above 0.70 IoU are an in-context naming target for T441, not a gate. (S053)

## T454 pinch zoom preview

Pinch previews continuously and snaps on gesture end, with a cancel path: `NavigationIntentKind.ZoomGestureEnded` and `IsGesture`, and `FactoryCameraController.EndPinch` / `CancelPinch`. Zoom buttons are soft-disabled at the limits and show a 3 s hint, "Maximum zoom" or "Minimum zoom". This answers the open owner decision from batch 9 (the reason was tooltip-only). (S053)

## T463 corner-aware ore-vein cut

`RunShellDocument.TryGetUnobstructedScreenRect` cuts the ore-vein overlay corner-aware: it builds an above and a beside candidate, the one with the larger `CameraScaleModel.LargestFittingScale` wins, ties go to the larger area. Landscape `n_fit` changed 1280x720 @ 320 from 1 to 2 and 2400x1080 @ 420 from 2 to 3. This closes the first-run landscape HUD gap pinned in batch 9. See [pixel camera scale](./pixel-camera-scale.md). (S053)

## T440 art import

One `import-manifest.json` imports seven families, 126 PNGs, hashes verified and byte-identical. The catalog has 86 keys; F003 placeholders stay as retained fallback frames. Four Sprite Atlas v2 (RGBA32, point, 1024 max) are built by the repeatable Editor tool `F004ArtIntegration.cs`. Catalog rows are dual-shape with explicit canvas and tight placement, and the shipped scene now sets `RequireRegistryRows`. The Spike path was deleted. Evidence: C8 had 0 failures over 275,616 ring px; the FR-010 worst glyph IoU is 0.556. Detail: `docs/art/F004-technical-art-integration.md`. (S053)

The floor tile was not imported: the 60 x 30 candidate does not fit the 70 x 54 period. Follow-ups: T470 #650 (re-render, `gpu` lease) and T471 #651 (import and importer hardening, `unity` lease). (S053)

## APK cap

Owner decision 2026-11-04: the APK cap is 150 MB file size, replacing "+10 MB versus B1". Art Bible v1.0.3 and `performance-budgets.md` row 10 record it. (S053)

Related: [art production pipeline](./art-production-pipeline.md), [batch 9 components](./f004-batch-9-pipeline-camera-catalog-import.md), [batch 10 pitfalls](./f004-batch-10-workflow-pitfalls.md), [run shell and panels](./run-shell-and-panels.md).
