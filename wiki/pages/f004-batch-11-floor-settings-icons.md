---
title: F004 batch 11 floor, Settings sheet and restyled icons
type: component
sources: [S054]
updated: 2026-11-05
---

# F004 batch 11 floor, Settings sheet and restyled icons

Batch 11 merged #650 (T470, PR #653), #546 (T433, PR #654), #651 (T471, PR #655), #657 (T473, PR #660), #547 (T455, PR #661), #656 (T472, PR #662), #555 (T441, PR #663) and #659 (T475, PR #664, merge `701e8c8`). #658 (T474) was closed as satisfied by T438 and T441 (owner decision 2026-11-05). (S054)

## Floor family (T470, T471, T473, T472)

- **T470.** The floor tile was re-rendered at 70 x 54 (opaque rectangle, pivot 35,27) with rig-v2. Geometry was checked through the Blender MCP, then one headless final render was made; a rerun was byte-identical. F-14 is a golden-hash test (plates plus floor). The Assembler rim share is 0.9167 against a 0.90 floor, a thin margin. (S054)
- **T471.** Import at PPU 24, `F004-FLOOR-TILE` as a required registry row, a floor atlas, and C8 run on the real floor. Importer hardening: orphan PNGs, duplicates, SHA-256 after copy, a placement-mismatch diagnostic, `GuardSceneSwitch`, manifest written last. The atlas memory figure was corrected to page-based, about 3.19 MB (the editor RGBA32 value is an upper bound). `reviewedBy` was filled only after an independent Art Director review, in a separate commit. (S054)
- **T473.** The slots pipeline and `cross_family` moved to the 70 x 54 lattice; the slots driver no longer writes floor outputs and the floor monkeypatch is gone. Committed slots check-report details and `scriptHashes` refresh on the next Blender rerun. (S054)
- **T472.** The field floor is drawn inside `SlotGridView`: pooled per-tile `SpriteRenderer`s, pivot on slot centres, sorting order -100, catalog fallback, 0 GC when warm, a 2048-tile cap. About 100-250 tiles on Android zooms, about 600 at n=1 1920x1080; the cost is measured at T444/B3 (items on #562). (S054)

## Settings sheet (T433, T475, T455)

- **T433.** Settings sheet and HUD gear, `ShellPanelIds.Settings`, a `HandleBack` branch and focus return. A radio group Follow/On/Off, an effective-state line, and a "Not saved" note when the write fails. Reduced motion freezes loops only. The bootstrap owns the lazy OS signal and its cached value (a JNI round trip, see [display preferences store](./f004-display-preferences-store.md)); the store raises `Changed`; a bounded 40 ms replace retry. Strip glyphs shrank 24 to 20 pt and the Store glyph is hidden on compact portrait. (S054)
- **T455.** Independent UI/UX review with a screenshot matrix, a state table and a device pass on the emulator. First QA FAIL: the matrix lacked keyboard and pointer rows and the reviewer identity. F4 (TalkBack sees one empty `SurfaceView`) was accepted as R-21. (S054)
- **T475 polish.** `Changed` unsubscribe; `IReducedMotionSignal.Available` as a default interface member; the retry fires only for HResult 0x80070020/21; copy "animation plays"; "Not saved" note first; on compact landscape at 150-200 % the group label and the radio "(selected)" suffix are hidden; one title; the gear is right-aligned on a wrapped strip (`justify-content: flex-end`). The first pass failed F1 at 640x360 and 844x390 at 200 %. (S054)

## T441 restyled UI icons

Restyled icons are bound by key on all F003 surfaces; the HUD gear is `F004-UI-ICON-SETTINGS`; the spinner stops under reduced motion; tint contrast is at least 3.41; 14 text-only button keys were accepted as unused. Blind naming (an independent namer, scoring by the parent) found: the 8 pt INFO pin hint reads as a missing icon, CASH reads low or confused with PRESTIGE, the INDUSTRY and SETTINGS gears are confused, and the chevron-over-bar group is confusable. `reviewedBy` for L2-L5 and the cash icon uses the dict schema with no revision bump, matching T439. (S054)

Related: [batch 11 pitfalls](./f004-batch-11-workflow-pitfalls.md), [batch 10 components](./f004-batch-10-lattice-zoom-ore-vein-art-import.md), [art production pipeline](./art-production-pipeline.md), [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md), [Unity presentation boundary](./unity-presentation-boundary.md).
