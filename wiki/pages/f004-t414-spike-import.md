---
title: F004 isolated spike import (T414)
type: component
sources: [S045]
updated: 2026-10-28
---

# F004 isolated spike import (T414)

T414 (#526, PR #588, merge `53855e8`) imports the S1 sprites into Unity in isolation under `Assets/IdleFactory/Art/F004/Spike/D16`, `D24` and `D32`. D24 holds 48 sprites (24 per projection, byte-identical to the T415 output); D16 and D32 are empty. See [F004 S1 spike assets](./f004-s1-spike-assets.md). (S045)

- `SpikeTextureImportRules` (editor `AssetPostprocessor`) forces PPU = D, point filter, no mips and no compression per density folder; Edit Mode probe tests prove it. (S045)
- `SpikeScaleVariant` scene with `SpikeScaleViewer` shows sprites at `n_min` and fit-all via `CameraScaleModel` (see [pixel camera scale](./pixel-camera-scale.md)); pixels per point are approximated from `Screen.dpi/160`. The scene is not in the build list. (S045)
- Pitfall: the runtime spike asmdef compiles into player builds until the spike path is deleted or promoted. (S045)

Related: [F004 batch 4 workflow pitfalls](./f004-batch-4-workflow-pitfalls.md).
