# F003 Source SVG provenance (T108, Issue #190)

Development placeholders under `Assets/IdleFactory/Art/Development/F003/Source/`. They are temporary and are replaced in Feature 004; asset IDs and slugs stay stable.

## Common record (applies to every row below)

- **Brief**: `F003-AB v0.1` (`docs/art/F003-asset-briefs-v0.1.md`). **Art Bible**: `F003-PAB v0.1` (`docs/art/F003-provisional-art-bible-v0.1.md`).
- **Method `T1`**: repository-authored SVG 1.1 source (explicit pixel `viewBox`, transparent background, flat fills and strokes, no embedded raster, linked file, external font, filter, mask, animation or script; labels are vector paths). Rasterization to PNG with Inkscape CLI `1.4.2` is out of scope for T108 and belongs to T111 (Technical Art), which records its own build and platform.
- **Provenance `P1`**: repository-authored primitives with no external asset source. Ownership: repository owner (`fjure`); no public license grant is claimed because the repository has no root license file.
- **Authoring and AI-assistance disclosure**: every file was authored by a Claude Code session (model Claude Sonnet 5.5, role Asset Artist, MAQA worker for Issue #190, 2026-10-06) under repository-owner direction. The language-model assistance was limited to textual authoring of primitive geometry (SVG 1.1 coordinates); a deterministic Python script in the session scratch directory emitted the files from a table of primitives. No image-generation model, ComfyUI, Blender, hosted generator, stock or third-party art, external icon set, external font or raster input was used or incorporated.
- **Tool and version**: plain-text SVG 1.1 authoring (Python 3.13.6 generator, not committed). Inkscape `1.4.2 (f4327f4, 2025-05-13)` on Windows 11 x64 was used only to render non-committed preview PNGs in the session scratch directory to check legibility; no preview is part of the repository.
- **Transformation steps (all files)**: (1) take the row's stable ID, source pixels and cue from the brief; (2) write primitive geometry with exact palette hexes from Art Bible sections 3 and 9; (3) build labels from stroke paths (block glyphs, cap height 12 px, slot digit 24 px); (4) confirm XML well-formedness, `viewBox` equal to the brief's source pixels, and absence of forbidden elements; (5) add the `.meta` file with the F001 SVG importer defaults and a fresh GUID (Technical Art T111 owns the real import settings).
- **Review status**: candidate until the independent Art Director source review T109 passes.

## Records

| # | Stable asset ID | Source file | Size | Method | Prov. | Art Bible / brief | AI assistance |
|---:|---|---|---|---|---|---|---|
| 1 | `F003-SLOT-EMPTY` | `f003-slot-empty-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 2 | `F003-SLOT-LOCKED-L2` | `f003-slot-locked-l2-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 3 | `F003-SLOT-LOCKED-L3` | `f003-slot-locked-l3-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 4 | `F003-SLOT-LOCKED-L4` | `f003-slot-locked-l4-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 5 | `F003-SLOT-LOCKED-L5` | `f003-slot-locked-l5-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 6 | `F003-SLOT-OCCUPIED-BASE` | `f003-slot-occupied-base-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 7 | `F003-SLOT-SELECTED-FRAME` | `f003-slot-selected-frame-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 8 | `F003-MACHINE-MINER` | `f003-machine-miner-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 9 | `F003-MACHINE-SMELTER` | `f003-machine-smelter-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 10 | `F003-MACHINE-PRESS` | `f003-machine-press-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 11 | `F003-MACHINE-ASSEMBLER` | `f003-machine-assembler-v01.svg` | 128x128 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 12 | `F003-BADGE-RUNNING` | `f003-badge-running-v01.svg` | 72x32 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 13 | `F003-BADGE-WAITING` | `f003-badge-waiting-v01.svg` | 72x32 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 14 | `F003-BADGE-MISSING-INPUT` | `f003-badge-missing-input-v01.svg` | 72x32 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 15 | `F003-BADGE-STORAGE-FULL` | `f003-badge-storage-full-v01.svg` | 72x32 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 16 | `F003-BADGE-PAUSED` | `f003-badge-paused-v01.svg` | 72x32 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 17 | `F003-BADGE-INACTIVE` | `f003-badge-inactive-v01.svg` | 72x32 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 18 | `F003-MARKER-BOOSTED` | `f003-marker-boosted-v01.svg` | 28x28 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 19 | `F003-MARKER-OVERDRIVE` | `f003-marker-overdrive-v01.svg` | 28x28 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 20 | `F003-RES-IRON-ORE` | `f003-res-iron-ore-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 21 | `F003-RES-IRON-BAR` | `f003-res-iron-bar-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 22 | `F003-RES-IRON-PLATE` | `f003-res-iron-plate-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 23 | `F003-RES-GEAR` | `f003-res-gear-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 24 | `F003-ITEM-DIAMOND-DRILL-HEAD` | `f003-item-diamond-drill-head-v01.svg` | 64x64 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): pip redrawn as one 8x8 px square (x2..10, y53..61), condition C6 (T109 reject) |
| 25 | `F003-ITEM-FLUX-CATALYST` | `f003-item-flux-catalyst-v01.svg` | 64x64 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): pips redrawn as 8x8 px squares with a 4 px gap (x2..10, x14..22), condition C6 (T109 reject) |
| 26 | `F003-ITEM-OVERCLOCK-CORE` | `f003-item-overclock-core-v01.svg` | 64x64 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): pips redrawn as 8x8 px squares with 4 px gaps (x2..10, 14..22, 26..34), condition C6 (T109 reject) |
| 27 | `F003-ITEM-TIER-1` | `f003-item-tier-1-v01.svg` | 48x24 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 28 | `F003-ITEM-TIER-2` | `f003-item-tier-2-v01.svg` | 48x24 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 29 | `F003-ITEM-TIER-3` | `f003-item-tier-3-v01.svg` | 48x24 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 30 | `F003-UI-ICON-CASH` | `f003-ui-icon-cash-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 31 | `F003-UI-ICON-RP` | `f003-ui-icon-rp-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 32 | `F003-UI-ICON-IP` | `f003-ui-icon-ip-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 33 | `F003-UI-ICON-FV` | `f003-ui-icon-fv-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 34 | `F003-UI-ICON-STORAGE` | `f003-ui-icon-storage-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 35 | `F003-UI-ICON-EXPAND-FACTORY` | `f003-ui-icon-expand-factory-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 36 | `F003-UI-ICON-TAB-FACTORY` | `f003-ui-icon-tab-factory-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 37 | `F003-UI-ICON-TAB-MARKET` | `f003-ui-icon-tab-market-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 38 | `F003-UI-ICON-TAB-CONTRACTS` | `f003-ui-icon-tab-contracts-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 39 | `F003-UI-ICON-TAB-ITEMS` | `f003-ui-icon-tab-items-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 40 | `F003-UI-ICON-TAB-RESEARCH` | `f003-ui-icon-tab-research-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 41 | `F003-UI-ICON-TAB-PRESTIGE` | `f003-ui-icon-tab-prestige-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 42 | `F003-UI-ICON-INDUSTRY` | `f003-ui-icon-industry-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 43 | `F003-UI-ICON-BUILD` | `f003-ui-icon-build-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 44 | `F003-UI-ICON-UPGRADE` | `f003-ui-icon-upgrade-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 45 | `F003-UI-ICON-REMOVE` | `f003-ui-icon-remove-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 46 | `F003-UI-ICON-TOGGLE-ON` | `f003-ui-icon-toggle-on-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 47 | `F003-UI-ICON-TOGGLE-OFF` | `f003-ui-icon-toggle-off-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 48 | `F003-UI-ICON-AUTOSELL` | `f003-ui-icon-autosell-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): arrowhead start moved to M28 10 so it overlaps the arc end (gap 1.18 px to 0 px, merged), T109 reject |
| 49 | `F003-UI-ICON-PRIORITY-UP` | `f003-ui-icon-priority-up-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 50 | `F003-UI-ICON-PRIORITY-DOWN` | `f003-ui-icon-priority-down-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 51 | `F003-UI-ICON-DRAG-HANDLE` | `f003-ui-icon-drag-handle-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 52 | `F003-UI-ICON-ACCEPT` | `f003-ui-icon-accept-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 53 | `F003-UI-ICON-DELIVER` | `f003-ui-icon-deliver-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 54 | `F003-UI-ICON-ABANDON` | `f003-ui-icon-abandon-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 55 | `F003-UI-ICON-ACTIVATE` | `f003-ui-icon-activate-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 56 | `F003-UI-ICON-CONFIRM` | `f003-ui-icon-confirm-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 57 | `F003-UI-ICON-CANCEL` | `f003-ui-icon-cancel-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 58 | `F003-UI-ICON-RETRY` | `f003-ui-icon-retry-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): arrowhead start moved to M28 10 so it overlaps the arc end (gap 1.18 px to 0 px, merged), T109 reject |
| 59 | `F003-UI-ICON-CLOSE` | `f003-ui-icon-close-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 60 | `F003-UI-ICON-INFO` | `f003-ui-icon-info-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 61 | `F003-UI-ICON-WARNING` | `f003-ui-icon-warning-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 62 | `F003-UI-ICON-LOCK` | `f003-ui-icon-lock-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 63 | `F003-UI-ICON-TIMER` | `f003-ui-icon-timer-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 64 | `F003-UI-ICON-OFFLINE` | `f003-ui-icon-offline-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 65 | `F003-UI-ICON-IMPORT` | `f003-ui-icon-import-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 66 | `F003-UI-ICON-SPINNER` | `f003-ui-icon-spinner-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 67 | `F003-UI-ICON-OVERDRIVE-READY` | `f003-ui-icon-overdrive-ready-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 68 | `F003-UI-ICON-OVERDRIVE-ACTIVE` | `f003-ui-icon-overdrive-active-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 69 | `F003-UI-ICON-OVERDRIVE-COOLDOWN` | `f003-ui-icon-overdrive-cooldown-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): bolt lowered to M20 8L8 23H17L14 36L27 19H19Z (apex inside canvas) and clock circle moved to 35,35 r7; bolt-to-clock gap 5.10 px, T109 reject |
| 70 | `F003-UI-ICON-OVERDRIVE-LOCKED` | `f003-ui-icon-overdrive-locked-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 71 | `F003-UI-ICON-RESEARCH-NODE-LOCKED` | `f003-ui-icon-research-node-locked-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 72 | `F003-UI-ICON-RESEARCH-NODE-AVAILABLE` | `f003-ui-icon-research-node-available-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 73 | `F003-UI-ICON-RESEARCH-NODE-OWNED` | `f003-ui-icon-research-node-owned-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry; T142 rework (Issue #268, Claude Code Sonnet 5.5, Inkscape 1.4.2 mask check at 8x): check moved to M14 20L21 27L32 15 and tick to M20 35H28; gaps to ring 2.49 px and to check 3.25 px (were 0.88 and 2.25), T109 reject |
| 74 | `F003-UI-ICON-RESEARCH-NODE-MAXED` | `f003-ui-icon-research-node-maxed-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 75 | `F003-UI-ICON-EFFECT-SPEED` | `f003-ui-icon-effect-speed-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 76 | `F003-UI-ICON-EFFECT-OUTPUT` | `f003-ui-icon-effect-output-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 77 | `F003-UI-ICON-EFFECT-SALE-PRICE` | `f003-ui-icon-effect-sale-price-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
| 78 | `F003-UI-ICON-EFFECT-UPGRADE-COST` | `f003-ui-icon-effect-upgrade-cost-v01.svg` | 48x48 | `T1` | `P1` | `F003-PAB v0.1` / `F003-AB v0.1` | Claude Code (Sonnet 5.5) textual authoring of primitive geometry |
