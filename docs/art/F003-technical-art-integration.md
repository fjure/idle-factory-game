# F003 Technical Art Integration

**Status**: Development-only placeholder integration for MAQA Issue #193 / WU32 / T111 (Feature 003, FR-034, SC-008 memory cost)

**Governing documents**: Art Bible `F003-PAB v0.1`, asset briefs `F003-AB v0.1` (frozen by T091), source approval in `specs/003-first-factory-prestige/reviews/art-direction-review.md` (sections "Source assets" and "Source assets — delta review": all 78 sources approved, 65 APPROVED and 13 APPROVED WITH NOTE).

These sprites are replaceable development placeholders. Feature 004 replaces them in a presentation-only swap; keys, slugs and GUIDs stay stable, and no gameplay or UI code holds a path, size, PPU or pivot (owner note 2026-10-01 on T110/T111): everything resolves by key through `PresentationSpriteCatalog`.

## Authority and tool evidence

- Rasterizer: `C:\Program Files\Inkscape\bin\inkscape.com`, `Inkscape 1.4.2 (f4327f4, 2025-05-13)`, Windows 11 `10.0.26200`, x64 (the tool and version fixed by the brief, ART-6; no substitute tool).
- Importer and test runner: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe` (Unity `6000.6.0f1`) in batch mode, with `scripts/test-unity.ps1`. No Unity MCP tool was used (the session MCP is bound to the main checkout).
- Importer method: a temporary Unity editor script (not committed) called `TextureImporter` and `TextureImporterSettings` for each PNG with the settings below and `SaveAndReimport()`. Two mechanical corrections were then applied to the generated `.meta` text: `spriteGenerateFallbackPhysicsShape: 0` (Unity 6000.6 writes `1` for a new sprite; F001 uses `0`) and `textureCompression: 0` on the non-overridden Standalone/Android/WebGL/iOS schema entries (same F001 convention: uncompressed everywhere, no platform override). The committed `.meta` files are therefore the whole importer record; no script is needed to reproduce them because the importer reads them as written (the contract test checks every setting).
- Catalog method: the 78 `F003-*` entries were appended to `spriteCatalog.entries` of the `CoreGameFoundationBootstrap` component in `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` as 78 `key` plus `sprite {fileID: 21300000, guid: <meta guid>}` pairs in YAML text (the same shape as the F001 entries). The text edit was chosen over an editor scene re-save to keep the serialized diff limited to the task (a re-save is free to rewrite unrelated serialized fields); the diff is exactly 156 added lines and no other scene line changes. `F003AssetContractTests` opens the scene in the editor and validates every entry, which proves Unity reads the text edit.
- Source files (`Source/*.svg`, `*.svg.meta`, `PROVENANCE.md`) are unchanged.

## Reproducible rasterization

Each approved SVG is transformed once to a PNG at the brief's exact pixel size (no crop, padding, rescale or atlas). From the repository root in PowerShell, with the manifest of the table below (`<slug>`, `<w>`, `<h>`):

```text
"C:\Program Files\Inkscape\bin\inkscape.com" Assets/IdleFactory/Art/Development/F003/Source/<slug>.svg --export-type=png --export-filename=Assets/IdleFactory/Art/Development/F003/Sprites/<slug>.png --export-width=<w> --export-height=<h> --export-background-opacity=0
```

A second run into a scratch directory matched all 78 files: ordered decoded RGBA pixel buffers are identical, with a combined SHA-256 of `da2d14318732820a60202136ad162bee5db76ba0de77ee1223552495259f82d4` (the encoded PNG bytes are not compared). Every PNG is RGBA at its exact brief dimensions with visible pixels.

## Imported contract

All 78 are single sprites, 64 PPU, full-rectangle mesh, point filter, clamp wrap, no mipmaps, not readable, alpha-as-transparency, uncompressed (`textureCompression: 0` on the default and every schema entry); Android and iPhone carry no override. Slot bases and machine bodies pivot at bottom-left `(0,0)` (the slot anchor); badges, markers, resources, Item icons, tier chips and UI icons pivot at the center. The sprite name equals the slug (`f003-<id>-v01`), which equals the lowercase stable ID plus `-v01`. No atlas: W003 has not shown a need, the whole set is 1.27 MiB, and Feature 004 changes density anyway.

The catalog validates world size (pixels divided by pixels per unit; one cell is one unit, one slot is two) and normalized pivot, so the same keys accept 32 PPU art in Feature 004 without code changes. The T110 interim placeholder path (the static list of 11 F003 keys and its generated, never-destroyed textures) is removed; `presentation.key.unresolved` still fires for any key without a registered entry.

## Footprint, ports and memory cost (SC-008)

Ports and logistics are `N/A` for every row. A slot is 2x2 cells (128x128 px = 2x2 world units at 64 PPU); a machine body is the same single slot and sits on the `F003-SLOT-OCCUPIED-BASE` plate; badges, markers and icons are not grid occupants. No sprite claims a port, belt, cable, power socket or logistics meaning (checked at source review). Decoded RGBA bytes are `width x height x 4`; the asset table lists them per asset.

| Family | Files | Decoded RGBA bytes |
|---|---:|---:|
| Slot bases and selection frame (128x128) | 7 | 458,752 |
| Machine bodies (128x128) | 4 | 262,144 |
| State badges (72x32) | 6 | 55,296 |
| Markers (28x28) | 2 | 6,272 |
| Resource icons (48x48) | 4 | 36,864 |
| Item icons (64x64) | 3 | 49,152 |
| Tier chips (48x24) | 3 | 13,824 |
| UI icons (48x48) | 49 | 451,584 |
| **Total** | **78** | **1,333,888 (1.27 MiB)** |

This is the exact decoded footprint of an uncompressed, mip-less RGBA32 set (the runtime texture cost is this figure plus object overhead), not a profiler measurement; `F003AssetContractTests` asserts `1333888`. F001 was 581,632 bytes for 21 sprites. The world view of a producing 12-slot factory draws from the 7 slot-family and 4 machine sprites (720,896 bytes) plus 6 badges and 2 markers (61,568 bytes); the rest is UI. The W003 measurement (T123) stays the authority for SC-008 on the device; this record supplies the asset input only.

## Asset table (footprint, pivot, GUID)

| Asset ID | Dimensions | Pivot | Decoded RGBA bytes | Referenced by | GUID |
|---|---:|---|---:|---|---|
| `F003-SLOT-EMPTY` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `e3f481a7ae2919c46a02fc8a101408e4` |
| `F003-SLOT-LOCKED-L2` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `d580649f91c54c3478b40f713c724594` |
| `F003-SLOT-LOCKED-L3` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `5c30b91a044dc3a44b41d576dbbfa3d0` |
| `F003-SLOT-LOCKED-L4` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `876d2a3b9d35b3f4fb5fce908e8671c0` |
| `F003-SLOT-LOCKED-L5` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `1efe10d14dd5403448398df541772f03` |
| `F003-SLOT-OCCUPIED-BASE` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `7480c55b6ad36ec4bbb51d3d9192e37e` |
| `F003-SLOT-SELECTED-FRAME` | `128x128` | bottom-left (0,0) | 65,536 | presentation code | `c70b0d40d25f7504aa2eeab1e023715c` |
| `F003-MACHINE-MINER` | `128x128` | bottom-left (0,0) | 65,536 | content key | `c37f5195458571d4bba76e457ee3701e` |
| `F003-MACHINE-SMELTER` | `128x128` | bottom-left (0,0) | 65,536 | content key | `1d6ba2d44896d7348bd3e33ff2172a48` |
| `F003-MACHINE-PRESS` | `128x128` | bottom-left (0,0) | 65,536 | content key | `d885140dcec5c734e85e7a6cfb27874c` |
| `F003-MACHINE-ASSEMBLER` | `128x128` | bottom-left (0,0) | 65,536 | content key | `112a4b5d8e7df49448e91cf56c1c2d7b` |
| `F003-BADGE-RUNNING` | `72x32` | center | 9,216 | presentation code | `8492d951f8f2a1d4ba9d6e86d4e0622b` |
| `F003-BADGE-WAITING` | `72x32` | center | 9,216 | presentation code | `05ff56a7e03a3fe4b991f9b0bf0db906` |
| `F003-BADGE-MISSING-INPUT` | `72x32` | center | 9,216 | presentation code | `19fb1d237cc51b64f972597d79b9c113` |
| `F003-BADGE-STORAGE-FULL` | `72x32` | center | 9,216 | presentation code | `0f06c10636e492c4ebcfd752b6f1961a` |
| `F003-BADGE-PAUSED` | `72x32` | center | 9,216 | presentation code | `a8997723d3bf3bc48a9f93f39f682c2e` |
| `F003-BADGE-INACTIVE` | `72x32` | center | 9,216 | presentation code | `74eb10ee9291bdc4985f9bb110fbbd68` |
| `F003-MARKER-BOOSTED` | `28x28` | center | 3,136 | presentation code | `60fc30c412faffa4b9163866d862e719` |
| `F003-MARKER-OVERDRIVE` | `28x28` | center | 3,136 | presentation code | `98779fd7d95663241a210bb77431808e` |
| `F003-RES-IRON-ORE` | `48x48` | center | 9,216 | content key | `a1d6b190df99da84aa069f8bea145833` |
| `F003-RES-IRON-BAR` | `48x48` | center | 9,216 | content key | `df3d4aaa0cfdc8d408dd482c38f99303` |
| `F003-RES-IRON-PLATE` | `48x48` | center | 9,216 | content key | `be3946c0cb5c42f4c8362f64e56a57a1` |
| `F003-RES-GEAR` | `48x48` | center | 9,216 | content key | `8b098426130b7574b878dd69e68d8cc6` |
| `F003-ITEM-DIAMOND-DRILL-HEAD` | `64x64` | center | 16,384 | content key | `c28044dd6a118974ca38067062c1375a` |
| `F003-ITEM-FLUX-CATALYST` | `64x64` | center | 16,384 | content key | `154a206f6c869f34b9ab9786c31afab8` |
| `F003-ITEM-OVERCLOCK-CORE` | `64x64` | center | 16,384 | content key | `73ea50d43a0680c4f8d5b1684d3a4aa5` |
| `F003-ITEM-TIER-1` | `48x24` | center | 4,608 | presentation code | `05cd1ded1eb6fe0408281f2a6e6840fa` |
| `F003-ITEM-TIER-2` | `48x24` | center | 4,608 | presentation code | `3d55058c3a341cf4b9670bc0624a41f7` |
| `F003-ITEM-TIER-3` | `48x24` | center | 4,608 | presentation code | `a6090d975d4b5854b9215d71946ce63f` |
| `F003-UI-ICON-CASH` | `48x48` | center | 9,216 | presentation code | `d8292eea862ccbd4aba4a2a2415f855e` |
| `F003-UI-ICON-RP` | `48x48` | center | 9,216 | presentation code | `a9ad79f020843b44c86017f905838cc8` |
| `F003-UI-ICON-IP` | `48x48` | center | 9,216 | presentation code | `d418119f1c95d2f4ca9d069796a98313` |
| `F003-UI-ICON-FV` | `48x48` | center | 9,216 | presentation code | `114c2460c4527a649b17ba8b5a4ae0c9` |
| `F003-UI-ICON-STORAGE` | `48x48` | center | 9,216 | presentation code | `7ece7d9a8e8c4c140b4404286397abf0` |
| `F003-UI-ICON-EXPAND-FACTORY` | `48x48` | center | 9,216 | presentation code | `f0e5e31db7358d84fb8378e5348d2794` |
| `F003-UI-ICON-TAB-FACTORY` | `48x48` | center | 9,216 | presentation code | `beaf6678b7774364ea3e93b22ae4f4fe` |
| `F003-UI-ICON-TAB-MARKET` | `48x48` | center | 9,216 | presentation code | `06011999b4e8c35499d51049f4b97d90` |
| `F003-UI-ICON-TAB-CONTRACTS` | `48x48` | center | 9,216 | presentation code | `f1c0b9793e02aed4e8e4dbd3122e7f5e` |
| `F003-UI-ICON-TAB-ITEMS` | `48x48` | center | 9,216 | presentation code | `71d3c09e1ea9cd241805193eed514364` |
| `F003-UI-ICON-TAB-RESEARCH` | `48x48` | center | 9,216 | presentation code | `1176a3fa93438bf48a2c72f639427d45` |
| `F003-UI-ICON-TAB-PRESTIGE` | `48x48` | center | 9,216 | presentation code | `7a785b8df50c39a408b1aaea3da429c8` |
| `F003-UI-ICON-INDUSTRY` | `48x48` | center | 9,216 | presentation code | `e6bb8942c7c3aed4289ccace9cee7d2e` |
| `F003-UI-ICON-BUILD` | `48x48` | center | 9,216 | presentation code | `1c4c1c626d6fa4e41b98f530bf492871` |
| `F003-UI-ICON-UPGRADE` | `48x48` | center | 9,216 | presentation code | `e9468e76c50259d4e8351a74a0fc37a4` |
| `F003-UI-ICON-REMOVE` | `48x48` | center | 9,216 | presentation code | `352e81ff884b2724cb0688327cccd23d` |
| `F003-UI-ICON-TOGGLE-ON` | `48x48` | center | 9,216 | presentation code | `f14d35befd276e245b5d313d5d0253bf` |
| `F003-UI-ICON-TOGGLE-OFF` | `48x48` | center | 9,216 | presentation code | `4bafd00a48e52be40902496f83327634` |
| `F003-UI-ICON-AUTOSELL` | `48x48` | center | 9,216 | presentation code | `0193906c95f474547a80eefc0506b0e1` |
| `F003-UI-ICON-PRIORITY-UP` | `48x48` | center | 9,216 | presentation code | `50c33d55143e2344f984e397cf301959` |
| `F003-UI-ICON-PRIORITY-DOWN` | `48x48` | center | 9,216 | presentation code | `0c1af301521e588409b780aa70070f4e` |
| `F003-UI-ICON-DRAG-HANDLE` | `48x48` | center | 9,216 | presentation code | `4d678b376232a8449baae68c2def0f6e` |
| `F003-UI-ICON-ACCEPT` | `48x48` | center | 9,216 | presentation code | `71b480597d633344f91e75ffd5083ef3` |
| `F003-UI-ICON-DELIVER` | `48x48` | center | 9,216 | presentation code | `bed9de821b46e1d458af2e736c879c98` |
| `F003-UI-ICON-ABANDON` | `48x48` | center | 9,216 | presentation code | `30f993b1841f85f42a10b4f2acdd021f` |
| `F003-UI-ICON-ACTIVATE` | `48x48` | center | 9,216 | presentation code | `7d31df42d9571574f8a06575ee9d331b` |
| `F003-UI-ICON-CONFIRM` | `48x48` | center | 9,216 | presentation code | `87c8320748c603f4f91ed70720c23308` |
| `F003-UI-ICON-CANCEL` | `48x48` | center | 9,216 | presentation code | `ab76c86b0a624434fa032745025ac942` |
| `F003-UI-ICON-RETRY` | `48x48` | center | 9,216 | presentation code | `c550084d71594534e92c8e1bd12ed804` |
| `F003-UI-ICON-CLOSE` | `48x48` | center | 9,216 | presentation code | `03d6859a7f997954486438ff6d8dd434` |
| `F003-UI-ICON-INFO` | `48x48` | center | 9,216 | presentation code | `4a7f853b2d1be794281660c34ef24394` |
| `F003-UI-ICON-WARNING` | `48x48` | center | 9,216 | presentation code | `ff2846cec41d8c74d8378096ce38d706` |
| `F003-UI-ICON-LOCK` | `48x48` | center | 9,216 | presentation code | `12bb05f092d49634ebe6a0f25b30c462` |
| `F003-UI-ICON-TIMER` | `48x48` | center | 9,216 | presentation code | `35535aa4a84379447b387b919d52becb` |
| `F003-UI-ICON-OFFLINE` | `48x48` | center | 9,216 | presentation code | `d26e69a09324e2b47a4c403ddf879fa7` |
| `F003-UI-ICON-IMPORT` | `48x48` | center | 9,216 | presentation code | `ffb6434eb24d0d44fb6115bb5dd74f21` |
| `F003-UI-ICON-SPINNER` | `48x48` | center | 9,216 | presentation code | `7475aef282bc05e4d80fe6a5c7f4a619` |
| `F003-UI-ICON-OVERDRIVE-READY` | `48x48` | center | 9,216 | presentation code | `7c2f4b8fb114b9243884bc3541294ae9` |
| `F003-UI-ICON-OVERDRIVE-ACTIVE` | `48x48` | center | 9,216 | presentation code | `7dacf8fcabe2cdd4f93eae6d32c57935` |
| `F003-UI-ICON-OVERDRIVE-COOLDOWN` | `48x48` | center | 9,216 | presentation code | `202d635679f1faf4f9adf7a36f0081e6` |
| `F003-UI-ICON-OVERDRIVE-LOCKED` | `48x48` | center | 9,216 | presentation code | `004a0999fa198d0408408859acf487e2` |
| `F003-UI-ICON-RESEARCH-NODE-LOCKED` | `48x48` | center | 9,216 | presentation code | `cb48227ed7211064c900ed0e1c88301c` |
| `F003-UI-ICON-RESEARCH-NODE-AVAILABLE` | `48x48` | center | 9,216 | presentation code | `fd9f280f315879f419d0e4f847dc511b` |
| `F003-UI-ICON-RESEARCH-NODE-OWNED` | `48x48` | center | 9,216 | presentation code | `fac68b4312b15674ca8f63f8ba55918e` |
| `F003-UI-ICON-RESEARCH-NODE-MAXED` | `48x48` | center | 9,216 | presentation code | `8867c3b73408c4e4c8f2f55ee94cd1a3` |
| `F003-UI-ICON-EFFECT-SPEED` | `48x48` | center | 9,216 | presentation code | `f65771483af23504bacbd632ec19779c` |
| `F003-UI-ICON-EFFECT-OUTPUT` | `48x48` | center | 9,216 | presentation code | `a935842684807ae488efbf262374cab6` |
| `F003-UI-ICON-EFFECT-SALE-PRICE` | `48x48` | center | 9,216 | presentation code | `f00d73e15fbc5e047ba015f42bbcbd38` |
| `F003-UI-ICON-EFFECT-UPGRADE-COST` | `48x48` | center | 9,216 | presentation code | `f17b44ba03e71d14a9e5f536fb32c61b` |

## Presentation key map

The 11 keys of `Assets/IdleFactory/Content/Iron/iron-factory-content.json` (4 machines, 4 resources, 3 Items, exactly the brief's `*` rows) resolve one-to-one to imported sprites through the scene catalog. The other 67 IDs are referenced by presentation code only and are registered under their stable ID as well, so no UI code needs a path. The Item names Flux Catalyst (Tier II) and Overclock Core (Tier III) are kept by the owner decision of 2026-10-06 (C3 settled); their GUIDs are created in this task and no key was renamed.

| Presentation key | Imported path |
|---|---|
| `F003-MACHINE-MINER` | `Assets/IdleFactory/Art/Development/F003/Sprites/f003-machine-miner-v01.png` |
| `F003-MACHINE-SMELTER` | `.../f003-machine-smelter-v01.png` |
| `F003-MACHINE-PRESS` | `.../f003-machine-press-v01.png` |
| `F003-MACHINE-ASSEMBLER` | `.../f003-machine-assembler-v01.png` |
| `F003-RES-IRON-ORE` | `.../f003-res-iron-ore-v01.png` |
| `F003-RES-IRON-BAR` | `.../f003-res-iron-bar-v01.png` |
| `F003-RES-IRON-PLATE` | `.../f003-res-iron-plate-v01.png` |
| `F003-RES-GEAR` | `.../f003-res-gear-v01.png` |
| `F003-ITEM-DIAMOND-DRILL-HEAD` | `.../f003-item-diamond-drill-head-v01.png` |
| `F003-ITEM-FLUX-CATALYST` | `.../f003-item-flux-catalyst-v01.png` |
| `F003-ITEM-OVERCLOCK-CORE` | `.../f003-item-overclock-core-v01.png` |

## Naming

Source `Source/<slug>.svg` and import `Sprites/<slug>.png` share the base name. The slug is the lowercase stable ID plus `-v01` and matches the slug column of the brief inventory. `F003AssetContractTests` asserts that the `Sprites` folder holds exactly the 78 PNGs and their 78 `.meta` files, that each ID and slug is a brief row, and that each PNG has its approved SVG.

## Readability per state against ART-1

Measured on the rasterized PNGs (point filtering, 64 PPU, the form the scene uses), as N2 of the source review required. Antialiasing creates blended edge colors, so palette membership is judged by the dominant fills and by the share of pixels that match a palette hex exactly (lowest 41 percent, the thin white UI glyphs).

| Check | Result |
|---|---|
| Canvas and size | all 78 at the brief's exact pixels; slot bases, machines, badges and markers are opaque to the canvas edge as designed |
| Badge glyph and label contrast, fill against ink `#0B1020` (8.3, at least 4.5:1) | RUN `#4ADE80` 10.86; WAIT `#A5B4FC` 9.50; IN! `#FBBF24` 11.34; FULL `#F0ABFC` 10.76; PAUS `#94A3B8` 7.38; OFF `#CBD5E1` 12.75; boosted marker `#2DD4BF` 10.17; Overdrive marker `#FB923C` 8.37 |
| Grayscale (Rec. 709) of the six badge fills (8.4) | RUN 0.553, WAIT 0.477, IN! 0.579, FULL 0.547, PAUS 0.360, OFF 0.657, identical to the Bible; RUN and FULL differ by 0.006, so the glyph and label separate them, as the Bible states |
| CIELAB minimum pairwise distance of the six badge fills | normal 19.2 (PAUS/OFF); protanopia 2.1 (WAIT/FULL); deuteranopia 12.3 (WAIT/FULL); tritanopia 12.2 (WAIT/PAUS), identical to the Bible; protanopia WAIT/FULL relies on the glyph (hourglass against box) and the label |
| Badge glyph separation at the minimum projected size | after nearest-neighbor downscaling the dark ink mask of every badge pair differs in at least 6.2 percent of its pixels at 27x12 and 6.8 percent at 36x16 (closest pairs FULL/OFF and FULL/PAUS), so the six glyphs stay distinct by shape |
| Slot states | empty (open brackets plus sign, no fill), locked L2 to L5 (hatch plate, padlock, text), occupied (plate with 4 px outline) and selected (double outline) differ in silhouette and fill coverage without color; the locked hatch carries no meaning |
| Machine states | state is never baked into a body; it is composed from one badge and up to two markers in the brief's layer order; the four silhouettes were checked pairwise at source review (IoU limit of Art Bible section 4) |
| Bolt icons with clipped tips (N2) | the geometry of `OVERDRIVE-READY` and `-LOCKED` reaches y = 0 (the miter of the top vertex leaves the 48x48 viewBox, so row 0 of the PNG is opaque at the tip), about 1 px or under 0.5 pt at 24 pt; `-COOLDOWN` is inside the canvas (alpha starts at row 1 and is faint there); `-ACTIVE` and the Overdrive marker are inside. This matches the approved-with-note verdicts; the sources stay unchanged |

Not done here: the two-reviewer shuffled-sheet grayscale and CVD pass of Art Bible 8.4 on the integrated scene, including the Z24 and Z32 renders, stays with T129. This record supplies machine measurements, not a reviewer.

## Conditions carried into integration

- **D2**: tier chips (`F003-ITEM-TIER-1` to `-3`) and Item icons (`F003-ITEM-*`) are never placed on the accent fill or on a light surface. Their baked frame colors (`#D08A4E`, `#CBD5E1`, `#FACC15`) and the numeral `#E5E7EB` assume the dark `--surface` or `--bg` token. T115 (Items panel) and T129 verify this in the scene.
- **Iron Ore contrast note (N1)**: `F003-RES-IRON-ORE` (`#B45309`) is 3.53:1 on the dark backing but 2.92:1 on `--surface`. Presentation code places it on `--bg` or the recorded backing, or the owner accepts 2.92:1 for a non-text graphic with its label beside it. T115 to T118 and T129 own that check.
- **C6 tier text**: Items lists also show the tier as UI text (for example "Tier II"), so a 32 pt icon is never the only tier cue (T115).
- **Second-reviewer pass**: Art Bible 8.4 with two reviewers and the shuffled sheet is deferred to T129 (ART-5: reviewers differ from the T090 and T111 authors).
- UI icons are monochrome white `#FFFFFF` and are tinted by tokens; they are never shown on the accent fill without a token contrast check (T112 and the panels).

## Prefab and scene validity

No prefab, material, animation, atlas, sorting-layer or `ProjectSettings` change belongs to T111, and the repository contains no prefab. The only consumer is the scene's `CoreGameFoundationBootstrap.spriteCatalog`, which now holds the 21 F001 entries followed by the 78 F003 entries (99 unique keys; the catalog rejects duplicates). The brief's recommended sorting orders (slot base 10, machine 100, selection frame 150, badge and markers 300) are a T112 renderer decision. Scene validity is proven by `F003AssetContractTests` (it opens `CoreGameFoundation.unity`, validates the catalog and compares each registered sprite's asset path with the imported file) and by the Play Mode suite that boots the scene.

## Validation

- Focused `F003AssetContractTests.cs`: PASS, 5/5, Unity Edit Mode (`scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/F003AssetContractTests.cs`).
- Full configured suite (`scripts/test-unity.ps1`) on 2026-10-06: Edit Mode 1572/1572 and Play Mode 176/176, zero skipped; the Play Mode count is non-zero.
- `PresentationSpriteCatalogTests` was adjusted for the removed interim path: an unregistered `F003-*` key and an unknown key are both unresolved.
- `scripts/check-settings-noise.ps1 -Commit <sha>`: see the Issue handoff; `ProjectSettings` changes produced by the Unity runs were reverted before the commit.
- Unity console: no relevant errors; the batch import logged only the benign licensing-token message.

## Provenance and limitations

Each PNG is solely the deterministic raster transform of its matching approved SVG (`Source/PROVENANCE.md`: method `T1`, provenance `P1`, repository-authored primitives, owner `fjure`, no public license grant claimed, no external, stock, generated-image or third-party source). The transformation steps are the Inkscape command above and the importer settings above; nothing else touched pixels. These are development placeholders: final screen and device readability, the grid seams of a composed factory, the second-reviewer 8.4 pass and the W003 device cost remain with T112, T123 and T129. Feature 004 replaces the art as a presentation-only swap through the same keys.
