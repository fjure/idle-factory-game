# B1 pre-switch baselines: manifest (T409)

Issue #521, base commit `ee92f1f` (Feature 003 and 005 merged, Built-in pipeline, Gamma, Unity 6000.6.0f1). Evidence for FR-004, FR-006, FR-025; research R12, R13. The archived APK, B1 FPS and the player-side metrics belong to T410 (`reviews/baselines.md`).

## 1. Test manifest (before any T409 file)

Full `scripts/test-unity.ps1` on the clean base: **Edit Mode 2323 passed, Play Mode 2230 passed, 0 skipped, 0 failed**. The sorted `fullname | result` list of every test case is in [baselines-b1-test-names.txt](./baselines-b1-test-names.txt) (two sections). Per-section SHA-256 of the list lines (without the section header): Edit Mode `0ecef7c6f31cf1bcaf71b32ef74bb4c9802a2caeb9e6cb09127fc28727c08d79`, Play Mode `036d6130ce711bc4def4107ec153d07b4644f49bdf96ff70e8fb1a9e6dfa946f`.

Regenerate from a run's result XML (PowerShell, ordinal sort): for each `//test-case` emit `fullname + ' | ' + result`, sort with `[Array]::Sort($lines, [StringComparer]::Ordinal)`.

T409 itself adds one Play Mode class, `RenderParityTests`, with 18 tests (9 screen-class parity tests, 6 comparator tests, 1 PNG round trip, W004-V pin, importer check). **After T409 the expected Play Mode count is 2230 + 18 = 2248; Edit Mode stays 2323.** T411 must show the same names and pass set as this manifest plus those 18 tests, and no test expectation changed. The importer check is a Play Mode test because the ledger restricts T409's paths to `Tests/PlayMode/...`; it behaves like the Edit Mode check R13 describes (reads `TextureImporter` only).

## 2. Screenshot baselines

Method (R13): `RenderCaptureHarness` (`Fixtures/W004V/RenderCaptureHarness.cs`) builds the W003-S checkpoint (`06-w003-s-stress`, sha256 `0467d293...ab73`) through the production `RunSession`, `RunShellPresenter`, `SlotGridView`, `RunCameraFraming` and `ShellRig` (shell at 1 point = 1 pixel), with the scene's own sprite catalog and camera look. No host `Update` runs and no clock advances, so the frame is static. The world camera renders into an exact-size ARGB32 RenderTexture (MSAA off, HDR off, opaque clear); the shell panel renders into its own RenderTexture (clearColor on, opaque grey). Both are read back with `ReadPixels` and stored as PNG layers.

Per screen class, 4 PNGs under `Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B1/`:

| File stem | Content |
|---|---|
| `w003s-<WxH>-world` | W003-S world at fit-all |
| `w003s-<WxH>-shell` | W003-S run shell (HUD, tab bar, zoom column) |
| `w004v-fitall-<WxH>-world` | W004-V forced states, fit-all |
| `w004v-nmin-<WxH>-world` | W004-V forced states, `n_min` |

Screen classes (9): 640x480, 360x640, 640x360, 390x844, 844x390, 768x1024, 1024x768, 1280x720, 1920x1080. 36 PNGs, **951,192 bytes in total** (largest 62,874 B: `w003s-1920x1080-shell.png`). SHA-256 per file: `sha256sum *.png` in that folder (lines of the form `<hash>  <name>`, names sorted); SHA-256 of that whole listing: `81201a30cdfb404c9fb03425aa2cc3646485fa9ee68488838730675d9a67f65a`.

Importer (every PNG, enforced by `EveryBaselineExistsAndKeepsAReadableUncompressedImporterOutsideEveryAtlas`): Texture Type Default (not a Sprite), Read/Write on, Compression None, no mipmaps, NPOT scale None, max size 4096, Point filter, referenced by no `SpriteAtlas`.

Tolerance (R13, `RenderParity.Compare`): a pixel differs when any RGBA channel differs at all; pass requires at most 0.1 % differing pixels and no channel delta above 2/255. Result of the capture against the committed baselines, on this host, three consecutive runs: **0 differing pixels, max channel delta 0/255 in all 36 images** (the capture is deterministic).

`n_min` note: the integer scale model of Feature 004 does not exist yet, so the baseline preset `n_min` is the Feature 003 readability floor (24 pixels per cell, `RunZoomSettings.fitAllFloorPixelsPerCell`) centred on the grid; the preset name is kept so the fixture needs no rename later. Fit-all pixels per cell: 1920x1080 and 1280x720 64.00 (clamped), 1024x768 58.67, 640x480 46.75, 768x1024 45.00, 390x844 42.75, 360x640 39.00, 844x390 32.88, 640x360 29.75.

Re-baselining (only on the pre-switch commit, with a recorded reason): run `RenderParityTests.cs` with `IDLEFACTORY_RENDER_BASELINE_WRITE=1`; the importer settings above are not rewritten by that run, so keep the committed `.meta` files.

## 3. W004-V fixture

`Fixtures/W004V/W004VFixture.cs`. Forced presentation states over the W003-S checkpoint, no Core change: slot `i` shows state `i mod 6` (running, waiting, missing input, storage full, paused, inactive), boosted marker on even slots, Overdrive marker on every third slot, selection frame on slot 6 (badge + both markers + selection, the densest overlay). Camera presets `n_min` and fit-all. **Fixture hash (SHA-256 of `W004VFixture.Canonical()`): `1920dfafb75e69131fe43bb38f66c088762b195c46aa7916c568747c7ecca6fc`**, pinned by `TheW004VFixtureIsPinned` and logged as `RENDER-FIXTURE` by every parity run. Running-machine animation does not exist in Feature 003 art (static sprites), so the forced picture is the visual worst case for this baseline; the B3 run adds animation.

## 4. Metrics

| Metric | B1 value | Source |
|---|---|---|
| Sprites drawn on W004-V (all 12 slots) | 47 SpriteRenderers (12 plates, 12 machines, 12 badges, 6 boosted, 4 Overdrive, 1 selection frame) | `RENDER-FRAME` log, `SlotGridView.ActiveRendererCount` |
| World-art texture memory | 913,912 B (0.87 MiB), 14 distinct textures, no atlas | `RENDER-METRIC` log, `Profiler.GetRuntimeMemorySizeLong` over the drawn sprites' textures; **Editor runtime size, not a player or build-report figure** |
| Batches, SetPass, draw calls | not collected here | `UnityStats` also counts the Editor's own rendering (values varied with the screen size, 250 to 275 SetPass for a 47-sprite frame) and the `ProfilerRecorder` counters read 0 in batch mode, so any number would be wrong. Collected in T410 from the Windows player / Frame Debugger on W004-V |
| Shader variants | not collected here | Needs a build report: collected in T410 |
| APK size | not collected here | Needs the Android build: collected in T410 |
| Texture memory (player Profiler, build-report atlas sum) | not collected here | Collected in T410 |

T410 (#522) collected the rows marked "collected in T410" above (APK size and archive, shader variants, texture sizes, SetPass per render, B1 emulator FPS): see [baselines.md](./baselines.md).

## 5. B2 post-switch regression baselines (T429, owner decision 2026-11-03)

B1 above stays the pre-switch before-evidence (T444, SC-001) and is never re-baselined; its `sha256sum *.png` listing hash `81201a30cdfb404c9fb03425aa2cc3646485fa9ee68488838730675d9a67f65a` is pinned by `TheB1PreSwitchBaselinesAreUnchanged`.

Reason: T429 changed what the slot grid draws (running machines draw no badge, badges and markers moved to the D = 24 anchors), so the 9 screen-class parity cases can no longer match the Built-in frames. The owner declared them the completed phase-0 gate and moved their comparison to `Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B2/` (same 4 stems per screen class, 36 PNGs), written with `IDLEFACTORY_RENDER_BASELINE_WRITE=1` from the T429 state. `RenderBaselines.Directory` is B2, `RenderBaselines.B1Directory` is B1. Importer rules are the same as B1 and are enforced for both folders. File listing hash (lines `<hash>  <name>` with two spaces as `sha256sum --text *.png` prints them, names sorted, C locale, same format as B1): `c0cc118d24ded99d3a732813dda69617a2a1942db5b9c4a5e0b418cad538cdf0`.
