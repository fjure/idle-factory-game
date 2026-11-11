---
title: F006 P0 mockup tooling
type: component
sources: [S057]
updated: 2026-11-11
---

# F006 P0 mockup tooling (T505, T506, T507)

- **Lattice export (T505, #723, merge a0ac4d3)**: Edit Mode test `LatticeGeometryExportTests` writes today's slot-grid geometry per screen class to `tools/art-pipeline/f006/lattice-geometry.json`, so mockups use the real layout instead of a redrawn one. (S057)
- **`mockup` compositor** in `tools/art-pipeline` reads that JSON, derives deck, trunk, branch, depot and deposit cells, and composites sprites at exact device pixels with the UI drawn from a point layout. (S057)
- **`art_pipeline concept-f006` (T506, #722)** scripts the concept sprites (no ComfyUI final art). Briefs live in `art-source/f006/briefs/direction-<a|b>.md`. (S057)
- **Sheets (T507, #724)**: `reviews/direction/` holds both directions and hub variants per class with a manifest. The parent rejected the first sheets: the directions were indistinguishable, the card sat away from its machine, text was clipped and the triptych showed no stages. The fix was additive compositor roles: edge, props, selection frame, card placement and text wrap. (S057)
- **Framing must model UI occlusion.** QA loop 1 failed on tablet because the framing check ignored the side panel docked over the world. Visibility is now measured against the docked UI, and the tablet depot is panned into view. (S057)
- **An empty `decorViolations` is not a C8 ring check.** The Art Director's condition 2 requires a separate ring proof before any wall art is accepted. (S057)

Related: [art production pipeline](./art-production-pipeline.md), [direction decision](./f006-p0-direction-decision.md), [visual identity](./visual-identity-art-direction.md).
