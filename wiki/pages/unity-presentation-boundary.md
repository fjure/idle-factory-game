---
title: Unity presentation boundary
type: decision
sources: [S001]
updated: 2026-09-14
---

# Unity presentation boundary

Feature 001 uses an orthographic SpriteRenderer world with one UI Toolkit overlay and Input System 1.17.0. Raw touch and mouse input becomes semantic application commands; views render immutable snapshots and cannot mutate grid, buffers or production state directly. (S001)

Tap/click selects, a world drag pans, pinch or wheel zooms, and visible zoom buttons provide parity. Build mode creates a snapped preview, while visible Confirm and Cancel controls commit or exit. UI-owned pointers remain UI-owned. A second world-owned touch cancels a pending one-finger action and starts a consumed pinch, so its release cannot select or place. (S001)

Placement and production states combine color with outline, symbols or text. The inspector exposes progress, inputs, output capacity and every blocker. The offline panel separates measured, credited and effective duration from actual deltas and limiting conditions, and it never displays an uncommitted reward as granted. Every development UI control supports visible focus and keyboard activation on desktop. (S001)

Presentation-only camera, selection, preview, pointer and panel state is never saved. After a successful world replacement, pending interaction is cancelled and selection is resolved by persistent ID or cleared. (S001)

Placeholder production begins from an Art-Director-approved versioned inventory. Each row names the asset ID/type, logical grid footprint or `N/A`, ports `none/N/A`, power state `N/A`, visual states, pixel target, format, pivot/cell mapping, source/import path and provenance. `F001-Development-Placeholders v0.1` governs these development graphics while the Art Bible is explicitly `N/A`; the assets support behavior validation and are not final art direction. (S001)

The state authority behind these views is described in [core architecture](./core-architecture.md). (S001)
