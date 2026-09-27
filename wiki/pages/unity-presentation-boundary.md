---
title: Unity presentation boundary
type: decision
sources: [S001]
updated: 2026-09-27
---

# Unity presentation boundary

Feature 001 uses an orthographic SpriteRenderer world with one UI Toolkit overlay and Input System 1.20.0. Raw touch and mouse input becomes semantic application commands; views render immutable snapshots and cannot mutate grid, buffers or production state directly. Bootstrap compilation against Unity 6000.6 superseded the earlier 1.17.0 pin. (S001)

Tap/click selects, a world drag pans, pinch or wheel zooms, and visible zoom buttons provide parity. Build mode creates a snapped preview, while visible Confirm and Cancel controls commit or exit. UI-owned pointers remain UI-owned. A second world-owned touch cancels a pending one-finger action and starts a consumed pinch, so its release cannot select or place. (S001)

Placement and production states combine color with outline, symbols or text. The inspector exposes progress, inputs, output capacity and every blocker. The offline panel separates measured, credited and effective duration from actual deltas and limiting conditions, and it never displays an uncommitted reward as granted. Every development UI control supports visible focus and keyboard activation on desktop. (S001)

Presentation-only camera, selection, preview, pointer and panel state is never saved. After a successful world replacement, pending interaction is cancelled and selection is resolved by persistent ID or cleared. (S001)

Placeholder production begins from an Art-Director-approved versioned inventory. Each row names the asset ID/type, logical grid footprint or `N/A`, ports `none/N/A`, power state `N/A`, visual states, pixel target, format, pivot/cell mapping, source/import path and provenance. `F001-Development-Placeholders v0.1` governs these development graphics while the Art Bible is explicitly `N/A`; the assets support behavior validation and are not final art direction. (S001)

Every world replacement must be followed by a view refresh. Before T061, the bootstrap configured `FactoryView` while the session still held the empty pre-reconciliation state, then refreshed only when no checkpoint existed. A cold start that restored a checkpoint therefore showed an empty grid until the first tap. The bootstrap now refreshes once after startup reconciliation whenever the session is `Running`. Simulation advances also change what views show. Before T062, only the inspector polled the session revision, so world badges and the Browse summary went stale until the next selection. Now `FactoryView.SyncPresentedStates` runs in `LateUpdate`, after the runtime host advanced the frame. It returns at once while the revision is unchanged. Otherwise it copies each machine's `MachinePresentationState` into a reused buffer, swaps only the badges whose state changed and re-raises `SelectionChanged` only for a changed selected machine. The state holds the active flag, recipe presence and blocker counts, but not progress, so it changes only on start, block, unblock or on/off. Core fills it allocation-free through `FactorySession.CopyMachinePresentationStates`, and at W001 scale an unchanged tick allocates nothing. Structural changes keep their explicit `Refresh` owners. (S001)

Unity 6000.6 UI Toolkit layout traps and HUD-aware camera framing are recorded in [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md). (S001)

The state authority behind these views is described in [core architecture](./core-architecture.md). (S001)
