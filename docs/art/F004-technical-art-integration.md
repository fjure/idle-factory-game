# F004 technical art integration (T440, Issue #554)

Technical Artist evidence for importing the approved Feature 004 art into Unity. Covers FR-002, FR-003, FR-022, FR-023 and SC-003. Base: origin/main `18d080f` (Art Bible `F004-PAB v1.0.2` section 3.5 lattice 70 x 54). Unity 6000.6.0f1, URP 2D, 64 PPU for Feature 003 placeholders, PPU = D = 24 for every F004 sprite.

## 1. Result

| Family | Source (`art-source/f004/<family>/`) | Imported to (`Assets/IdleFactory/Art/F004/<family>/`) | PNGs | Catalog keys | Atlas |
|---|---|---|---|---|---|
| machines | `manifest-candidates.json` | bodies and accents, 6-frame loops at 8 fps | 48 | 4 `F003-MACHINE-*`, 4 `F004-ACCENT-*` | `Atlases/machines.spriteatlasv2` |
| slots | `manifest.json`, `manifest-t437.json` | 6 plates, selection frame, 4 lock labels | 11 | 7 `F003-SLOT-*`, 4 `F004-LABEL-LOCK-L2..L5` | `Atlases/slots.spriteatlasv2` |
| cues | `manifest.json` | 5 badges; the running badge is `intentionallyEmpty` and has no PNG | 5 | 5 `F003-BADGE-*` (+ `F003-BADGE-RUNNING`, see 3.3) | `Atlases/cues.spriteatlasv2` |
| markers | `manifest.json` | boosted, Overdrive | 2 | 2 `F003-MARKER-*` | `Atlases/markers.spriteatlasv2` |
| items | `manifest.json`, `manifest-t464.json` | 3 item icons (32 x 32), 3 tier chips (32 x 16) | 6 | 3 `F003-ITEM-*`, 3 `F003-ITEM-TIER-*` | none (UI only) |
| resources | `manifest.json` | 4 resources (32 x 24) | 4 | 4 `F003-RES-*` | none (UI lists only, OD-12) |
| ui-icons | `manifest.json` | 50 glyphs (24 x 24) | 50 | 49 `F003-UI-ICON-*`, `F004-UI-ICON-SETTINGS` | none (UI Toolkit) |
| floor | `manifest.json` | **not imported** (section 7, open item 1) | 0 | none | none |

126 PNGs (41,318 bytes) and 86 catalog keys (77 F003 keys with F004 primary frames, 9 additive `F004-` keys). The 78th F003 key, `F003-BADGE-RUNNING`, is the `intentionallyEmpty` entry. Decoded RGBA32 size of the whole set: about 1.52 MB (machines 0.98 MB, slots 0.23 MB, cues 0.10 MB, markers 0.04 MB, icons 0.12 MB, items and resources 0.03 MB), far under the 8 MB world-art budget (budget 6; the atlases pack the four world families, pages at most 1024 x 1024).

## 2. Import path

`Assets/IdleFactory/Editor/Art/F004ArtIntegration.cs` is the reproducible, committed import tool (justified outside the assignment paths: the task requires "import through the manifest ... via Unity tooling", and a tool keeps the step repeatable and testable). Batch form:

```
Unity.exe -batchmode -quit -projectPath . -buildTarget win64 -executeMethod IdleFactory.Editor.Art.F004ArtIntegration.RunFromCommandLine -f004Families machines,slots,cues,markers,items,resources,ui-icons
```

Steps (`-f004Steps import,atlas,catalog`, default all):

1. **import**: merges the per-family source manifests into the one shipped `Assets/IdleFactory/Art/F004/import-manifest.json` (the T430 rule: at most one manifest, at the art root). Every source PNG hash is checked against its source manifest before the copy; the PNGs are copied byte for byte to `Art/F004/<family>/<name>.png`; the manifest is written first, so `F004SpriteImportPostprocessor` finds it. The shipped entries carry the import fields (`assetId`, `ppu`, `fps`, `canvas`, `footprintPx`, `pivot`, `frames`) plus `key` and `family`. A key comes from the manifest, or by rule: `f004-machine-<m>-run` is `F003-MACHINE-<M>`, `f004-accent-<m>-run` is `F004-ACCENT-<M>`, otherwise the upper-cased asset id (the plates). The importer applies PPU = D = 24, the manifest pivot (integral art pixels, rule P1), point filter, no mipmaps, clamp, uncompressed RGBA32, FullRect mesh. No rejection or skip warning appears in the import log.
2. **atlas**: one Sprite Atlas v2 per world family (machines, slots, cues, markers) through `F004SpriteAtlasSettings.CreateFamilyAtlas`: RGBA32, point, padding 2, no rotation, pages at most 1024, explicit Default, Standalone and Android overrides. Items, resources and icons are UI-only and are not atlased.
3. **catalog**: registers the primary frames in the scene sprite catalog through `SerializedObject` (the scene is never hand-edited; Unity re-saved it, which also dropped the stale `FactoryCameraController` limits block, a one-time serialization migration that `RunZoomSettings` already announced). An existing F003 key keeps its placeholder as `fallbackFrames`; machine keys get `framesPerSecond` 8; the nine `F004-` keys are appended; the catalog flag `requireRegistryRows` is set.

The spike import path `Assets/IdleFactory/Art/F004/Spike/` (D16, D24, D32 sprites, `SpikeScaleVariant` scene, the runtime and editor assemblies `IdleFactory.Art.F004.Spike*`, `SpikeImportTests`) is deleted (owner decision T426, 2026-10-30). `art-source/f004/spike/` and the review evidence stay. `F004ImportRules.IsGovernedPath` keeps its Spike exclusion (harmless; its test still passes).

## 3. Catalog contract (SC-003)

### 3.1 Requirement rows

`PresentationSpriteCatalog` rows now state two shapes. The primary shape is the F004 art; the legacy shape is the F003 placeholder (64 PPU), which stays valid as a retained fallback frame, in the development catalog and in the unit-test catalogs. A frame set matches one shape as a whole, so a wrong size or pivot still fails the row, and the error text still says "wrong world size" or "wrong pivot".

| Keys | Primary shape (world units at 24 PPU) | Frames | Legacy shape |
|---|---|---|---|
| `F003-SLOT-*` (7), `F003-MACHINE-*` (4), `F003-BADGE-*` (6), `F003-MARKER-*` (2), `F004-ACCENT-*` (4), `F004-LABEL-LOCK-L2..L5` (4) | 80 x 64 art px canvas layer = 3.3333 x 2.6667, pivot (0.5, 0.234375) | plates and labels 1, others 1..16 | the old F003 rows (2 x 2 with a bottom-left pivot; badge 72/64 x 0.5; marker 28/64) |
| `F003-RES-*` | 32 x 24 = 1.3333 x 1.0, pivot (0.5, 5/24) | 1 | 0.75 x 0.75 centre |
| `F003-ITEM-*` | 32 x 32 = 1.3333 square, centre | 1 | 1 x 1 |
| `F003-ITEM-TIER-*` | 32 x 16 = 1.3333 x 0.6667, centre | 1 | 0.75 x 0.375 |
| `F003-UI-ICON-*`, `F004-UI-ICON-SETTINGS` | 24 x 24 = 1 x 1, centre | 1 (spinner 1..16) | 0.75 x 0.75 (no legacy for the settings icon) |

### 3.2 Carry-forwards from the T429 reviews

- **Optional rows to required.** `F004-ACCENT-*` and `F004-LABEL-LOCK-L2..L5` are now `required = true`. A required registry row only fails a catalog that sets `RequireRegistryRows`; the shipped scene sets it (missing key: `presentation.key.unresolved`, fail closed), while development and unit-test catalogs without the accent and label art stay legal. Without the flag, every unit and Play Mode catalog builder would have to register F004 art.
- **Explicit canvas-vs-tight placement.** `IsCanvasLayer(sprite)` was inferred from the sprite size. The catalog now resolves it per key: `PresentationSpriteEntry.Placement` (`Inferred`, `Canvas`, `Tight`) can be set explicitly; for `Inferred` the catalog takes the shape of the requirement row that the frames matched (primary canvas row: `Canvas`; legacy shape: `Tight`); a retained fallback entry is always `Tight`; only a key without a row still falls back to the size inference. `SlotGridView` uses `entry.IsCanvasLayer(frame)` (a small edit beyond the comment-only allowance; the diff is the per-renderer `*Canvas` flags and `PlaceLayer`, `BadgePosition`, `PlaceMarker` taking that flag; behaviour is unchanged for every existing catalog).
- **F003 plate, machine, badge and marker rows to the F004 canvas size**: section 3.1.
- **Resource sprites and the per-machine SortingGroup row orders.** No runtime view draws a resource sprite today (resources appear only in UI lists, OD-12; `git grep F003-RES` finds the catalog rows only). The rule for the first consumer is recorded here: a resource sprite drawn in the world must sort inside the Sorting Group of the machine it belongs to, or on its own Sorting Group whose `sortingOrder` is that slot's row (`SlotGridLayout.RowOf`, back row 0), so that it sorts against the machine rows exactly as plate, machine and accent do (research R04).

### 3.3 Every F003 key resolves to F004 primary frames

`F004AssetContractTests.EveryF003KeyResolvesToF004PrimaryFramesAndTheRunningBadgeThroughIntentionallyEmpty` reads the 78 F003 keys from the F003 sprite folder and asserts, against the scene catalog: a manifest entry; registered frames equal to the manifest frames by asset path and count; the manifest frame rate; exactly one retained F003 fallback at 64 PPU; no key running on its fallback (`Diagnostics` and `FallbackKeys` empty). `F003-BADGE-RUNNING` is the one exception by owner decision R8: its manifest entry is `intentionallyEmpty`, the state-cue row draws nothing for it, and its catalog entry keeps the F003 placeholder (`Tight`) so the content and F003 contract tests still resolve the key.

`F003AssetContractTests.EveryBriefRowIsRegisteredInTheSceneCatalogWithTheImportedSprite` is a recorded re-baseline: it used to assert that frame 0 of each key is the F003 sprite; it now asserts that the F003 sprite is the key's retained fallback frame (or the only frame of the running badge). Each of the other tests in that class is unchanged.

## 4. Review state and provenance (`reviewedBy`)

Source: `specs/004-visual-identity/reviews/art-direction-review.md` section 3 (T439, review base `9a70483`). `reviewedBy` is filled in 82 of 88 `provenance.json` files with role "Art Director (second session) + UI/UX Reviewer + Game Designer/PO", session "T439, Issue #553, branch maqa/issue-553-t439-art-direction-review", date 2026-11-03, the row verdict (`APPROVE` or `APPROVE WITH CONDITIONS`), the reviewed revision and commit, and the row text. Nothing was invented. The six left empty:

| Asset | Why |
|---|---|
| `f003-slot-locked-l2` .. `l5` (4) | Pixels changed after the T439 review: T468 (#641, merge `e9733df`) removed the keyhole that read as the digit 1 (finding F-01). The provenance revision stays 2; the re-review of the new pixels is open. The import ships the new pixels. |
| `f003-ui-icon-cash` | Pixels changed after the review: T469 (#642, `248b906`) redrew the coin so it does not read as a cross (finding F-06 / OD-14). The provenance revision still says 1 while the output hash is the new one. Re-review is open. |
| `f004-floor-tile` | Not imported (section 7). Review condition F-17 (T456 confirms the 60 x 30 period) now fails. |

The T464 tier chips (`a8bf3f8`), T437 slot frame and labels (`78524d8`) and T436 items (`f849b87`) predate the review base and keep their reviewed state; the empty and occupied plates and the floor only had their provenance refreshed by T468 (pixels identical, `git diff 9a70483 HEAD` shows no PNG change). Two pipeline tests pinned `reviewedBy == {}` (`test_cues_production.py`, `test_ui_icons.py`); they now normalise the reviewer record before comparing the committed files with a fresh generator run (the generator still writes it empty), so a regeneration followed by this step is the repeatable procedure. Outside the assignment paths; justified in the PR.

## 5. Evidence: C8 and FR-010 on the shipped art

**C8 (Art Bible 9.4).** Method: the imported PNGs (not the art-source files) are composed at art-pixel resolution on the T456 lattice (70 x 54 pitch, 3 columns and 4 columns): 12 occupied slots, a plate under each machine, machine frame k (0..5) of the four bodies in turn, the accent over the running slots (slots 0 and 6), a badge on the other ten (five badges in turn), both markers on every slot, one selection frame moved over all 12 slots. For each badge, marker and frame the pipeline check `check_overlay_backdrop` (`tools/art-pipeline/art_pipeline/checks_v1.py`) takes the 1 px ring of backdrop pixels outside the overlay mask, excludes ring pixels covered by another overlay, and requires 100 % in `A_ov` (`A_fr` for the frame). Result: **0 failing overlays in 2 lattices x 6 frames x 12 selections = 144 compositions; 275,616 ring pixels sampled**. Caveat: no floor art is imported (section 7), so bare backdrop counts as `floor_0` and `floor_1` as the rule prescribes; the floor tile's `floor_2` highlights are outside `A_ov`, so C8 must be re-run on the floor art when it exists (finding F-17 and the T456 Assembler rim re-measure belong to the same step).

**FR-010 badge shape distinctness (colour-agnostic).** `F004AssetContractTests.TheFiveBadgeGlyphsAreShapeDistinctWithoutColour` reads the five shipped badge PNGs, keeps the `ink_0` pixels that have four opaque neighbours (the glyph without the outline ring, independent of the fill colour), and asserts pairwise IoU at most 0.70, the pipeline's `IOU_MAX`. The worst pair measures 0.556 (the whole-chip silhouettes are identical by design, so the glyph is what separates the states; the shape cue per state stays independent of colour).

## 6. APK delta checkpoint (after the first imported family, machines)

Command (profiler-free, Development, IL2CPP, ARM64, GLES3; `scripts/` has no wrapper): `Unity.exe -batchmode -quit -projectPath . -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect -buildOutput <apk> -logFile <log>` in the worktree; C: had 51 GB free (limit 20 GB). Budget: `reviews/performance-budgets.md` section 4, row 10: APK growth at most +10 MB against the paired B1 (37,345,615 B), that is at most 47,345,615 B; B2 was 46,330,863 B.

| Build | APK file | Sum of compressed entries (`zipfile`) | Entries | Note |
|---|---|---|---|---|
| Pre-import baseline (`18d080f` + nothing) | 46,279,003 B | 46,199,747 B | 426 | 892,056,846 B build output |
| After the machines family (48 PNGs, atlas, catalog) | 66,081,859 B | 46,268,742 B | 428 | 914,007,716 B build output |
| **Delta** | file +19,802,856 B (not meaningful) | **+68,995 B (+0.15 %)** | +2 | |

The file size jump is not content: the second Gradle package step updated the existing APK in place and left 19.3 MB of unreferenced gaps inside the zip (gaps of 14.6 MB before `libmain.so` and 4.7 MB before the Newtonsoft resources; the entry sums differ by 69 KB). The entry sums are the cross-check that the budget table already uses. Entry changes: two new asset splits `sharedassets0.assets.split3/4` (+266,735 B compressed: the machines atlas and sprites) while `split1/2` shrink by 206,621 B as content moves, `libil2cpp.so` +7 KB. The baseline stays 1.07 MB under the 47.35 MB cap, and the family adds 0.07 MB, so there is **no readability cut and no owner decision**. 

**Final build after all seven families, the catalog and the spike deletion** (same command, `final-all-families.apk`, 914,778,079 B build output): sum of compressed entries 46,288,148 B in 429 entries, **+88,401 B (+0.19 %) against the baseline**, uncompressed entries +3.24 MB (the four atlas pages and sprites; `sharedassets0.assets.split3/4/5` +309,784 B compressed, `split1/2` -211,359 B as content moves, `libil2cpp.so` -6 KB after the spike assemblies left). The file is again 66.07 MB because of the same in-place packaging gaps; adding the entry delta to the clean baseline file gives an estimated clean size of about 46.37 MB, which is **0.98 MB under the 47,345,615 B cap** (row 10). The shader-variant stripping was not touched: after each build `UniversalRenderPipelineGlobalSettings.asset` showed the same one-line `rid` removal that the baseline build produced (the list is not emptied) and was restored with `git checkout`; `ProjectSettings/ProjectSettings.asset` showed the known iOS-icon noise and was restored; the editor was switched back to the Windows standalone target (`-buildTarget win64`) after the builds. The three build logs are `test-results/android/t440/` (ignored by git).

## 7. Open items and risks

1. **Floor tile not imported.** `art-source/f004/floor/manifest.json` is a 60 x 30 candidate (pivot (30, 15)); T456 replaced the floor period with the lattice pitch 70 x 54 (Art Bible 3.5, "T435 authors the floor art for that period"; a 2:1 diamond floor of 70 x 35 has an odd height). A 60 x 30 tile cannot tile a 70 x 54 period (no common phase; 70 is not a multiple of 60 or 30). Per the assignment the other families were imported and the art was not regenerated. Needed: a producer re-render of the floor at the 70 x 54 period (T435 / T466 owner), then import (`-f004Families` gains `floor`), then C8 and the Assembler rim re-measure on the shared floor phase. Until then the world has no F004 floor and the existing background shows.
2. **Locked plates and cash icon** are imported without a `reviewedBy` (section 4).
3. **Findings that name T440 and are not done here** because they edit the Art Bible, the briefs or MCP docs, which are outside this assignment's paths and need owner sign-off: F-02/OD-6 (locked plate wording), F-07/OD-2 (rig-v1 mentions to rig-v2 in the Art Bible, briefs and `docs/integrations/mcp-readiness.md`), F-08 (production anchor sheet and the OD-12 resource wording in section 13), F-13/OD-9/OD-11 (label backdrop `dim_1`, items rim tones), F-14 (golden-hash test for the Blender-route plates and floor). F-16: the machine `outline_contrast` check ignores the tones `dim_1`, `floor_2`, `steel_2` and `ui_line` under a body outline (the C5 set is {plate_1, plate_2, ink_0, floor_0, floor_1}); the aggregation here relies on the family reports as committed.
4. **Baselines.** Real art changes world renders. Re-written screenshot or render baselines are listed in the PR description and in section 8.
5. **T456 lattice review.** `GridWorldMapper.cs` needs no lattice change: it maps the F001 grid cells (`CellOrigin`, `CellCenter`, `WorldToGrid`) and holds no slot geometry; the lattice (pitch, pivots, hit test, extent) lives only in `SlotGridLayout`. The `SlotGridLayout.HitTest` comment is corrected: the tap shape is a diamond (`nx + ny <= 1`) in the cell, so the four cell corners belong to no slot (the old text said a tap above a cell belongs to the slot behind).
6. **uv environment.** `tools/art-pipeline/.venv` in this worktree held stale `pytest.exe` launchers ("Failed to canonicalize script path", `test-art-pipeline.ps1` exit 3); `uv sync --frozen --offline --reinstall-package pytest --project tools/art-pipeline` repaired it (ignored directory, nothing committed). `python -m pytest` always worked.

## 8. Validation

| Command | Result |
|---|---|
| `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/F004AssetContractTests.cs` | 35 of 35 after the first import (the file now holds 39 tests: 7 new T440 import and catalog tests and 4 catalog-contract tests) |
| `scripts/test-unity.ps1 -Suite EditMode -TimeoutSeconds 3000` | **2548 passed, 0 failed, 0 skipped** (an earlier run found one failure, `PresentationSpriteCatalogTests.EveryFrameOfAnAnimatedEntryIsValidated`: a pivot message where the old row said "world size"; fixed in the validator, no test changed) |
| `scripts/test-unity.ps1 -Suite PlayMode -TimeoutSeconds 3000` | first run 2457 of 2467 passed (10 failures: 9 `RenderParityTests` world frames and `SlotGridTests.TheSceneComposesTheShellAndDrawsTwelveSlotsFromTheRunReadModel`); after the recorded re-baselines below: **2467 passed, 0 failed, 0 skipped** |
| `scripts/test-art-pipeline.ps1` (also chained by `scripts/test-unity.ps1`) | `ART_PIPELINE_PASS`, 201 passed |
| `scripts/check-settings-noise.ps1 -Commit <sha>` | see the PR |

Recorded re-baselines (each is the expected consequence of registering real art; no test was weakened):

- `RenderParityTests` (9 screen classes): the screenshot baselines of the world frames under `Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B2/` were rewritten through the project tooling (`IDLEFACTORY_RENDER_BASELINE_WRITE=1`, one run of `RenderParityTests.cs`). Families rewritten: `w003s-<class>-world` (9), `w004v-nmin-<class>-world` (9), `w004v-fitall-<class>-world` (9) = 27 PNGs. The 27 shell frames are byte-identical and were not touched. The comparison run passes with the new baselines.
- `SlotGridTests.TheSceneComposesTheShellAndDrawsTwelveSlotsFromTheRunReadModel`: a fresh factory now draws 12 plates plus one lock label per locked slot (the `F004-LABEL-LOCK-*` art is registered and required), and a running Miner draws its accent in place of a badge. Old counts: 12 and `12 + 1 + badgeDrawn`.
- `F003AssetContractTests.EveryBriefRowIsRegisteredInTheSceneCatalogWithTheImportedSprite`: section 3.3.
- Pipeline tests `test_cues_production.py` and `test_ui_icons.py`: section 4.
