# What I Learned: Visual Identity - 2.5D Pixel-Art Restyle

**Feature**: Restyle the game as 2.5D pixel art through an agent-operated art pipeline and a URP 2D Renderer switch.
**Generated**: 2026-11-05 (batch 1 on 2026-10-27, batches 2 to 12 added)
**Scope**: MAQA batches 1 to 12 (final verification in batch 12: Test/QA evidence, lens reviews, B3 performance, readability gates, owner acceptance and the F003 placeholder deletion; floor re-render, floor import with importer hardening, field floor layer, Settings sheet with polish and independent review, restyled UI icons with blind naming in batch 11; rectangular slot lattice, pinch zoom preview, corner-aware ore-vein cut and the F004 art import in batch 10; art-pipeline v1 checks, sprite catalog requirements, integer camera and sprite import rules in batch 9; Art Bible v1.0, its review, revision and sign-off in batch 8; tooling, baselines, camera scale model, panel polish, S1 spike assets, URP phase 0 switch and reviews, spike import, S1 review, display preferences, emulator scenario, S1 owner decision, B2 budgets, render-foundation hardening, layout polish, S2 spike, S2 evidence and D24 decision, N-5/N-6 phone-landscape polish, S3 cue and S4 lighting variants, in-game captures and timed-trial harness, owner trials, revised waiting badge)
**Implementation status**: 75/77 tasks ticked in `tasks.md` after the batch 12 mirror (batch 12: T442, T444, T445, T446, T447, T476 (new); batch 11: T470, T471, T472, T473, T433, T455, T441, T475 and T474 (closed as satisfied); T472-T475 are new; batch 10: T456, T454, T463, T440; T470 and T471 (new) open; batch 9: T462, T428, T427, T430; T463 (new) open; batch 8: T424, T425, T461 (new), T426; T462 (new) open; batch 1: T400-T407, T409; batch 2: T408, T410, T450-T452; batch 3: T411, T413, T415, T453; batch 4: T414, T416, T432, T443; batch 5: T412, T417, T418, T457, T458; batch 6: T419, T420, T459; batch 7: T421, T422, T460 (new), T423; GitHub Issues are the status authority)

---

## Key Decisions

### 1. Capture the old look before changing the renderer

**What we did**: T409 committed 36 Built-in screenshots (9 screen classes x 4 layers) and a pinned fixture (W004-V) before the URP switch, plus a comparator test.

**Why**: "Looks the same" is not testable afterwards. A baseline taken on the pre-switch commit turns the URP phase into a revertible diff with a measurable pass criterion, and three repeat captures with 0 differing pixels proved the capture itself is deterministic.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Eyeball the URP result | Not reproducible, misses 1-pixel shifts |
| Capture baselines after the switch | Bakes in any regression |

**When you'd choose differently**: If the capture is not deterministic (animation, time-dependent shaders), you need a tolerance model first. Note the comparator counts every channel delta above 0 as "differing", so T411 still has to decide whether 0.1 % strictness suits URP.

### 2. Make tool output byte-reproducible, then check it

**What we did**: The Blender rig renders on Cycles CPU with seed 0, and PNG metadata stamps are disabled; scripted routes are checked against goldens.

**Why**: QA failed #515 because Blender wrote the absolute path and date into PNG tEXt chunks. That broke reproducible hashes and leaked local paths into committed files. Reproducibility is the only way an independent reviewer can trust generated art.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Tolerance-only comparison (0.5 % of pixels) | Still needed for EEVEE and same-seed generation, but hides real drift where exact equality is possible |
| GPU render | Driver variance |

**When you'd choose differently**: For genuinely stochastic generators (ComfyUI) exact bytes are impossible; commit the raw output as an intermediate and make only the later passes exact.

### 3. Pin the facts a tool cannot detect

**What we did**: ComfyUI briefs always set `grid` and `expected_size`.

**Why**: `detect_grid` is unreliable on blended upscales, and non-integer upscales are refused only when `expected_size` is set. A guard that depends on an optional input is not a guard.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Trust auto-detection | Wrong on blended upscales |
| Always refuse non-integer upscales | Blocks legitimate spike experiments |

**When you'd choose differently**: With a reliable detector, drop the manual pins to reduce brief boilerplate.

### 4. Lease the GPU like the Unity slot

**What we did**: `scripts/resource-lease.ps1` gained a `gpu` resource.

**Why**: ComfyUI and Blender GPU work contend with each other and with Unity, as the Unity editor and emulator already did.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Convention only ("ask first") | Parallel sessions already corrupted evidence once |

**When you'd choose differently**: With a single agent per workstation, a lease is overhead.

### 5. Placeholders stay labelled as placeholders

**What we did**: Palette v0 (48 colours) and the 0.6 darkest-light factor are checked in as defaults, not Art Bible values.

**Why**: The tooling can be tested now, while the real values wait for the style spike.

**When you'd choose differently**: If a placeholder starts shipping in assets, freeze a versioned value instead.

### 6. Put the rounding rule where the real devices are (batch 2, T408)

**What we did**: `CameraScaleModel` is a pure class with 40 Edit Mode tests. Odd pixel extents get a half-*device*-pixel offset (`1/(2*n*D)`), which differs from the contract's "half an art pixel" wording. `n_min` still uses the exact ceiling; the owner decided (2026-10-28) that T427 adds a 1 % tolerance (`ceil(x - 0.01)`).

**Why**: A 1280x720 landscape screen at 320 dpi has 2.0046 px/pt, which pushes `n_min` to 3 while only `n = 2` fits at D = 24, so with the exact ceiling that device falls into the runtime degrade (`presentation.scale.belowFloor`). The owner accepted a tolerance on the readability floor over a permanent degrade on a common screen class.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Strict ceiling | Empty set of legal scales on a real device class |
| Lower the 24 pt floor | Changes the readability promise for every device |

**When you'd choose differently**: If the floor were a legal or accessibility requirement, you would shrink the art density (D) instead of bending the floor.

### 7. Measure the noise before the effect (batch 2, T410)

**What we did**: The Built-in B1 baseline records an ABA spread of 58.05 versus 48.18 FPS on the same build, so differences under about 5 FPS are noise. B1 must run at least 3 times in one cold-booted session.

**Why**: A before/after claim for the URP switch is only as good as the control's repeatability. Two draw counters read 0 in 6000.6, so SetPass per render stands in for them.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| One run per build | Cannot separate a 10 FPS change from host load |
| Editor stats | Include Editor rendering; wrong for a player claim |

**When you'd choose differently**: On a dedicated device with stable load, fewer repeats would do.

### 8. Fix the cause you can reproduce, keep the guard you cannot (batch 2, T450)

**What we did**: R-23 (wrong machine-panel hint for a multi-output machine) had no failing case against the shipped catalog, because each resource has one producer. The guards stay, and the multi-output case is tested by editing the content JSON.

**Why**: Deleting a defensive branch because today's data cannot reach it breaks the next content revision.

**When you'd choose differently**: If a guard is dead code with no plausible data path, remove it.

### 9. Measure the gate on both candidates, decide later (batch 3, T415)

**What we did**: The S1 spike assets (Miner/Assembler, D=24, Blender route) were produced for oblique 3/4 and isometric 2:1. The silhouette IoU gate (at most 0.70) fails for oblique 3/4 (0.759 full, 0.789 at 32 px) and passes for isometric 2:1 (0.651 / 0.672). QA re-rendered one asset byte-identically.

**Why**: The F003 provisional table (0.612) was the only prior number. Measuring both projections on the same pair turns the projection choice into evidence. The generator needed compensation for the rig's world fill (`LIT_GAIN` 2.0, `AMBIENT` 0.25), and the overhang rule caps machine height at about 0.55 units.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Pick the recommended projection (oblique) unmeasured | The gate then fails on the first real pair |
| Loosen the 0.70 gate | Weakens the readability promise |

**When you'd choose differently**: One pair is a small sample; widen to more pairs before freezing the Art Bible.

### 10. Switch the renderer behind a pixel-exact gate (batch 3, T411, T413)

**What we did**: URP 17.6.0 with a 2D Renderer, Unlit sprite material, Gamma kept, HDR/depth/opaque/MSAA/post off. Parity 18/18 (max delta 1/255), Edit 2374, Play 2275 (baseline plus 11 config tests), Windows player 39/39, Android journey passes (about 40 FPS, same as B1 on the loaded host). APK 47.89 MB (+10.5 MB), 490 stripped variants (2262 without URP stripping) from 95 shaders. Architect, Test/QA and DevOps each approved with follow-ups: dead 0.1 % budget, global-settings guard, package pin, Light2D positive control.

**Why**: The T409 baselines made "looks the same" a number. The comparator is R13-literal (owner decision 2026-10-27), so the budget is vestigial; that is honest, not hidden. A cold-run guard (`ShaderUtil.allowAsyncCompilation = false`) covers the one cold-Library frame that failed at 18.9 %.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Keep the stricter any-delta comparator | Contradicts R13 as written |
| Skip the independent review | Phase 0 touches settings that no test fully guards |

**When you'd choose differently**: If you use 2D lights (S4), set `m_Strip2DUnusedVariants` to 0 first, or stripping removes the variants.

### 11. Keep reviewer verdicts verbatim (batch 3, T413, T453)

**What we did**: The phase-0 review writer's first draft failed QA on fidelity: wrong section pointers, and dispositions added or softened. The final record copies each reviewer's verdict unchanged. The T453 UI/UX re-review accepted T450-T452 and logged N-1 (mid-word title wrap at 200 % on wide classes), N-2 (pause reason below the actions at 640x480), N-3 (accepted) and N-4 (URP GaussianDepthOfField "stripped" error, 22 times in the dev player log).

**Why**: A summary that improves a verdict stops being evidence.

**When you'd choose differently**: Summarize only in a separate section clearly marked as the writer's own.

---

### 12. A spike gets its own import rules and its own scene (batch 4, T414)

**What we did**: The S1 sprites were imported under `Assets/IdleFactory/Art/F004/Spike/D16|D24|D32`. An editor `AssetPostprocessor` (`SpikeTextureImportRules`) forces PPU = D, point filter, no mips and no compression per density folder, and a `SpikeScaleVariant` scene shows them at `n_min` and fit-all through `CameraScaleModel`.

**Why**: A pixel-scale spike answers "does this look right at integer scales", so the import settings must be fixed by rule, not by hand. A postprocessor plus probe tests make a wrong setting impossible to commit unnoticed, and an isolated scene keeps the spike away from shipping UI.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Hand-set importer options per sprite | Easy to drift, hard to review in meta files |
| Spike inside the real run scene | Mixes throwaway and shipping code |

**When you'd choose differently**: If the spike will be promoted, import with the production rules from the start. Note the cost: the spike asmdef compiles into player builds until the path is deleted or promoted, and pixels per point are only approximated (`Screen.dpi/160`).

### 13. A gate that does not discriminate is still a result (batch 4, T416)

**What we did**: The S1 review measured silhouette IoU (A oblique 3/4 fails at 0.759/0.789; B isometric 2:1 passes at 0.651/0.672), and a second-session Art Director ran the blind grayscale and color-vision naming. Naming failed for both, tied, because the S1 art has body variants but no cue icons, so warning differs from paused by a few red pixels. The recommendation is B, accepted by the Art Director with conditions (isometric lattice, re-run naming after S3, S2 legibility on phone portrait, whole-pixel pivots because about 2 % of vertical edges are off-grid). The owner decision was pending at that point; batch 5 records it (decision 16).

**Why**: Reporting "fails, tied, cause: cues only" moves the fix to the stage that owns it (S3 cue icons) instead of letting a projection choice appear to solve readability. The decision rests on the gate that did separate the candidates.

**When you'd choose differently**: If the naming gate could be tied by construction, add a stage-appropriate probe (cue icons in the spike) so the gate can discriminate.

### 14. Preferences are not saves (batch 4, T432)

**What we did**: `DisplayPreferencesStore` keeps the reduced-motion override in `IdleFactory/Preferences/display-v1.json`, separate from checkpoints and the session log. A strict token walk reads it (4 KB, version 1 only); a missing or invalid file means "follow system" and logs `preferences.display.invalid` once per session. The parent corrected the worker's first reading that a missing file is silent: the contract says it is reported.

**Why**: A cosmetic preference must never be able to corrupt or be pruned with player progress, and failure must degrade to the safe default. The OS signal is read through an interface with Android, Windows and default variants that never throw.

**When you'd choose differently**: If the value is queried every frame, cache the effective state; `Resolve(FollowSystem)` is a JNI round trip with allocations (FR-026, a note for T433).

### 15. Test the script without the device (batch 4, T443)

**What we did**: The emulator journey script gained `-Scenario F004`. It was validated by parse checks, rejection of unknown steps and scenarios, a lease conflict that stops before any `adb` call, and synthetic crisp and blurred PNGs for the crispness analyzer.

**Why**: The emulator is a leased, shared resource; proving the logic offline keeps device time for what only a device can show. The limits are recorded: not run on a device, thresholds may need tuning in T444, and the FPS step measures plain W003-S until a build applies the W004-V forced presentation.

### 16. Decide on the gate that separated the candidates (batch 5, T417, T418)

**What we did**: The owner chose B, isometric 2:1, because it was the only candidate passing the silhouette gate (IoU 0.651/0.672 against 0.70), and carried the Art Director's five conditions into tasks (T456 lattice, S3 cue icons, S2 legibility, pivots in T424). The S2 spike then ran B at two densities: D24 passes (0.651), D16 fails (0.704), while the palette budgets pass at both.

**Why**: A decision with no discriminating evidence is a taste vote; this one rests on a number plus explicit follow-up conditions, so the risks the numbers could not cover (naming, pivots, phone portrait) each got an owner task instead of a hope.

**When you'd choose differently**: If both candidates had passed, decide on cost and conditions, not on the gate. A failing D16 result is still evidence: report it and let the density review (T419) weigh it against file size and effort.

### 17. Measure on a quiet host, then freeze numbers as budgets (batch 5, T412)

**What we did**: B2 was built from an empty Library and measured in one cold-booted ABA session (B1, B2, B1, control, B2, B1) after the parent paused workers and moved all leases to the measuring Issue. The result (61.37 vs 61.18 FPS, APK +8.99 MB, 490 variants) became frozen thresholds for B3; budgets that cannot be measured yet (GC, texture memory, atlas) are frozen as rules and deferred.

**Why**: A bracketing baseline (B1 before and after) turns "no regression" into a claim with a known noise band (0.42 FPS here). Freezing the number prevents later arguing about what "no worse" meant.

**When you'd choose differently**: On a physical device the host-GPU advantage disappears and the margins may shrink; treat every emulator budget as exploratory until then (FR-027). The SetPass drop from about 8-14 to under 1 reflects the SRP Batcher, so compare it only against itself at B3.

### 18. Find the root cause of a console error before silencing it (batch 5, T457)

**What we did**: The 22 "stripped shader" errors in the Windows player came from `m_PostProcessData` being non-null on the 2D Renderer, so Renderer2D built post passes that loaded stripped shaders even with post-processing off. Setting it to null removed all 22 and kept 356 Android variants. The same task made every parity assertion able to fail, added three warm-up frames before capture, and guarded the global settings asset.

**Why**: Hiding the log line would have left a pipeline that tries to load shaders the build removed. Fixing the setting keeps the console gate honest.

**When you'd choose differently**: The settings guard catches an emptied list but not a partial trim or a dropped rid. If those matter, assert the full expected list or keep diffing the asset after each Unity run.

### 19. A layout fix keeps the stable-layout rule (batch 5, T458)

**What we did**: The pause reason moved into the status line at 640x480 so it is visible without scrolling, while blockers stay below the actions so buttons never shift (Feature 003 T249). At 200 % text the side-panel header wraps instead of breaking the sheet title mid-word. An independent reviewer accepted it and found two smaller phone-landscape cases (N-5, N-6).

**Why**: Moving a button to make room for text would trade one usability bug for another. Putting the new information in a reserved row fixes the complaint without breaking an earlier decision.

**When you'd choose differently**: If a reserved row cannot hold the longest message, allow wrapping or a tooltip; do not move the primary actions.


### 20. Choose on the measured gate, then turn every caveat into a task (batch 6, T419, T420)

**What we did**: The S2 evidence put D16 and D24 side by side (zoom and margin table per screen class, silhouette IoU 0.651 vs 0.704, crispness, icon-room arithmetic). A second-session Art Director found blind naming failing on both densities (warning vs paused, bare-plate slot conditions) and agreed with D24 under seven conditions. The owner chose D24 on 2026-10-29 and each condition was written into T421, T422, T424, T427 or T456.

**Why**: Naming failed equally at both densities, so it could not choose; the gate that separated them (IoU) did. Conditions left in a review are forgotten, conditions in task text are tested. Condition 1 ("T427 before the freeze") contradicted the task dependency (T427 needs T426) and was recorded as a note instead of silently ignored.

**When you'd choose differently**: If a later gate flips the order (for example icon room at 24 pt), reopen the density with a stated parameter change rather than quietly relaxing the 24 pt floor.

### 21. Scope layout fixes to a class, and keep what the class shares (batch 6, T459)

**What we did**: At phone landscape 640x360 and 150-200 % text, USS rules scoped to the compact-landscape class made Close icon-only (44 pt, tooltip "Close"), set the machine name to body size and tightened vertical spacing. The status row moved from y 248 to 189 (66 pt visible against 64 needed). The owner relaxed the original "reason fully readable" rule to "status row and reason begin in the first view, at least 2 lines visible"; the test encodes that rule and its date. The parent's full Play Mode run then caught a contract card shrinking 1 pt, because narrowing the horizontal inset from 8 to 6 px changed the text wrap; the fix kept 8 px.

**Why**: A change scoped to one class still shares layout with other panels. Vertical gains are safe; horizontal insets feed text wrapping everywhere. A strict assertion that cannot be met is a product decision, not something a worker should loosen alone.

**When you'd choose differently**: If the 2 pt margin proves fragile on devices, shorten the title (a wording change that needs an owner decision) rather than stretching the rules further. The tooltip is the project's accessible-name convention, but screen-reader exposure on Android is unverified.

### 22. A conditional QA pass needs a full-suite backstop (batch 6, #601)

**What we did**: QA subagents without Unity could only give conditional verdicts; the parent's full Play Mode suite was the real gate. Doc-only records (the AD review, the owner decision) committed onto an in-review branch got a new worker-done marker and a Low-tier delta QA; a QA FAIL on a missing number was fixed and re-checked before the merge.

**Why**: Targeted runs cover what the author thought of; the full suite covers what they did not.

**When you'd choose differently**: If the full suite becomes too slow to run per Issue, run every Play Mode file that opens the changed screen class at the changed text scale instead.

### 23. Shape-coded badges beat body treatment (batch 7, T421)

**What we did**: Two cue families were built on the D24 miner: (a) a warning-coloured body with shape-coded icons, (b) a uniform pixel badge per state, none on a running machine. Both were rendered at 17 and 23 art px with a fixed anchor (59, 33) and 51 sprites.

**Why**: Colour alone fails for colour-blind players and shifts with lighting; a shape is readable in greyscale. 23 px meets 24 pt at the minimum scale on all four screen classes, 17 px misses on the emulator (19.4) and tablet landscape (18.2). Measuring the 24 pt floor in arithmetic first removed 17 px before any player saw it.

**When you'd choose differently**: If a screen class needs a smaller badge, change the camera scale or the density, not the 24 pt floor.

### 24. Blind naming and the timed trial are complementary gates (batch 7, T422, T423, T460)

**What we did**: An independent session named sprites blind (9/9 in all sets), yet the owner's timed trial failed round 1 (a 6/10, b 8/10 within 2.0 s). The waiting badge was redrawn (T460), re-named blind 9/9 by a fresh session, and the owner scored 10/10 (median 1.077 s) in round 2.

**Why**: Naming asks whether a symbol can be told apart without a hint; the trial asks whether it is read in under two seconds. A symbol can pass the first and fail the second. A failed owner gate became a new small task (T460) while T423 stayed open and recorded both rounds, so no gate was loosened.

**When you'd choose differently**: For a quick sanity check, naming alone is cheap; use the trial before a freeze decision. Keep the trial blind: QA never opens `key.json`, and naming sessions see only sheet PNGs.

### 25. Measure IoU on bodies, not composites (batch 7, T421, T423)

**What we did**: The owner ruled that the silhouette IoU rule applies to bodies only. The worst body pair scored 0.651 at full size and 0.672 at 32 px; composites with the same badge on both machines scored 0.77 and 0.78.

**Why**: A badge is meant to change the silhouette. Measuring a composite would punish the cue for doing its job and hide whether the body shape itself stays distinct between machine types.

**When you'd choose differently**: Add a second, separate check if badges ever cover a body feature that distinguishes types.

### 26. Lighting B (flat body plus a 2D-light accent sprite) (batch 7, S4)

**What we did**: The owner chose lighting B over baked lighting A.

**Why**: The owner picked B on the light A/B sheets without a recorded reason beyond the comparison itself. B separates the light cue (an accent sprite or Light2D) from the body sprite, so lighting can change without re-rendering bodies; the cost is runtime 2D-light rendering, which T424 must measure against the B2 budgets.

**When you'd choose differently**: If Light2D cost misses the B2 budgets on the emulator, fall back to A. T424 carries that measurement, together with the badge colour semantics (cool waiting, warm problems, neutral paused or inactive; shape stays primary) and the 24 pt gaps at 460 dpi phones (23.95 pt) and 1280x720 at n = 2 (22.95 pt).

### 27. A review blocker before a freeze becomes a revision task (batch 8, T425, T461)

**What we did**: The second-session Art Director found that rim rule C5 contradicted sections 6.1 and 2 and that the spike Miner failed it. The owner picked option (b) (outline OR rim reaches 3:1, as a composite-mask share) and the parent created T461 to revise the Art Bible before T426 froze it.

**Why**: A freeze "with conditions" leaves a known contradiction in a document that later tasks (T434 to T438) treat as law. A small revision task keeps the signed version clean and gives the revision its own Issue, QA and commit (af0ee63).

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Sign off v1.0 with listed conditions | Later tasks would implement against a contradictory rule |
| Option (a): add a 1 px inner rim to every floor-facing face | Changes the art for a rule, not for readability, and amends two sections |

**When you'd choose differently**: When the finding is a cosmetic note, carry it as a named condition instead of spending a task.

### 28. State the measurement method with the number (batch 8, T461 QA)

**What we did**: Section 9.3 now says exactly how the bare-floor rim share is counted, including that off-canvas neighbours count (56 pairs at 62 % for `floor_0`, 58 pairs at 60 % for `floor_1`).

**Why**: QA could not reproduce the shares at first because its script dropped off-canvas neighbours. A figure without its neighbourhood and edge handling is not independently checkable.

**When you'd choose differently**: Never for a gate figure; for exploratory numbers a one-line note is enough.

### 29. Registry of requirements instead of pinned sprite names (batch 9, T428)

**What we did**: `PresentationSpriteCatalog` keeps a per-key requirement registry. F001 keys are not start-up requirements, F003 keys are validated when present, and F004 rows arrive through `Configure(entries, requirements)`. Fallback frames keep PPU 64 and stay static.

**Why**: Pinning sprite names would break every time art is renamed or regenerated; a requirement per key lets each feature declare what it needs while older art keeps working. Missing art logs `presentation.art.fallback` or `presentation.key.unresolved` as warnings, never a crash.

**When you'd choose differently**: For a shipped, frozen asset set a hard start-up failure is stricter and simpler. Note the open seam: the bootstrap scene path cannot apply F004 required rows yet (T429/T456).

### 30. One camera writer, integer levels, one tolerance (batch 9, T427)

**What we did**: `FactoryCameraController` is the only camera writer and works in integer scale levels. The owner's 1 % `n_min` tolerance applies to `MarkersVisibleAt` too (x0.99), and the pan remainder is kept per axis only below one device pixel.

**Why**: Two writers or fractional scales cause shimmer in pixel art. Applying the same tolerance everywhere avoids two rules that disagree about what is readable. Keeping a larger remainder at a limit would let the camera drift off the pixel grid (Architect A5).

**When you'd choose differently**: A smooth-zoom game would trade crispness for continuity. The first-run landscape HUD gap is pinned by a test and tracked as T463 rather than hidden.

### 31. Importer rejections are warnings, found by a later gate (batch 9, T430, T462)

**What we did**: The editor-only sprite importer checks the canvas from the PNG header first, and a rejected asset gets a `LogWarning` and Unity defaults. The runner treats any LogError as fatal, so expected rejections cannot be errors. The Sprite Atlas v2 mode is a recorded settings decision with a test guard; the art-pipeline v1 checks stay standalone and are called by later tasks.

**Why**: An importer that throws blocks the whole editor; a warning plus a downstream catalog or atlas check (T440) catches the same defect where it can name the family. The standalone checks keep the rules testable without Unity.

**When you'd choose differently**: In a CI-only import pass a hard error is better. Without a later gate, warnings alone would be silently ignored.

### 32. Accept the lattice the art can actually tile (batch 10, T456)

**What we did**: The isometric slot lattice is rectangular: 70 x 35 diamonds at a pitch of 70 x 54 art px. `SlotGridLayout` owns `PivotOf`, `HitTest` (a 90 x 45 diamond clipped to the cell), `Extent` and `RowOf`. The owner accepted it on 2026-11-04 and the Art Bible became v1.0.2 (floor period 70 x 54). The 20 icon pairs above 0.70 IoU were accepted as an in-context naming target for T441, not a gate (OD-17).

**Why**: One layout class that answers pivot, hit test and extent keeps views, camera fit and art on the same geometry. A hit area larger than the diamond, clipped to its cell, is easier to tap without overlapping a neighbour.

**When you'd choose differently**: With a true isometric offset grid the rows would interleave and `RowOf` would be more than integer division. Here the cost was a new floor period, which is why the 60 x 30 floor tile no longer fit (decision 34).

### 33. Preview a gesture, snap on its end, and always have a cancel (batch 10, T454, T463)

**What we did**: Pinch zoom previews continuously and snaps to an integer level on `ZoomGestureEnded` (`IsGesture` marks gesture intents); `EndPinch` and `CancelPinch` are separate paths. Zoom buttons are soft-disabled at the limits and show a 3 s "Maximum zoom" or "Minimum zoom" hint instead of a tooltip only. Separately, the ore-vein overlay is cut from the free rectangle corner-aware: an above and a beside candidate, the larger `LargestFittingScale` wins, a tie goes to the larger area.

**Why**: Integer scale is the crispness rule, but a finger needs continuous feedback, and a cancelled gesture (a lost touch) must restore the last level rather than snap somewhere new. Picking the cut by the resulting scale, not by area alone, is what raised landscape `n_fit` (1280x720 @ 320 from 1 to 2, 2400x1080 @ 420 from 2 to 3).

**When you'd choose differently**: A game with free zoom needs none of this. A tooltip for the disabled state is invisible on touch, which is why a visible hint replaced it.

### 34. Do not import an asset that misses its contract (batch 10, T440)

**What we did**: One `import-manifest.json` imported seven families (126 PNGs, hashes verified, byte-identical) into four Sprite Atlas v2 with a repeatable Editor tool; F003 placeholders stayed as retained fallback frames. The 60 x 30 floor tile was left out because the lattice period is 70 x 54, and became T470 (re-render) and T471 (import and importer hardening).

**Why**: Importing a tile that cannot tile would make the batch look complete while shipping a visible seam. Splitting it out keeps the evidence (C8 0 failures over 275,616 ring px, FR-010 worst glyph IoU 0.556) honest and gives the re-render its own `gpu` lease.

**When you'd choose differently**: For a throwaway prototype a stretched tile is fine; for a shipped asset set a known mismatch should become a task, not a workaround.

### 35. Compare like with like when measuring size (batch 10, T440)

**What we did**: Gradle in-place APK packaging left about 19 MB of dead gaps (a 66 MB file against about 46.4 MB of entries). Atlas memory was computed per page, not per sprite pixel. The owner then set the APK cap to 150 MB file size (2026-11-04, Art Bible v1.0.3).

**Why**: A file size taken after incremental builds measures the build history, not the content. Per-page atlas memory matches what the GPU actually allocates.

**When you'd choose differently**: A cap on download size (store-reported) differs from install size; state which one the number means.

### 36. Draw the field floor per tile in the view, not as one tiled sprite (batch 11, T472)

**What we did**: `SlotGridView` draws the field floor with pooled per-tile `SpriteRenderer`s: pivot on slot centres, sorting order -100, a catalog fallback, 0 GC when warm and a 2048-tile cap. About 100-250 tiles are visible at Android zooms and about 600 at n=1 on 1920x1080; the measured cost is deferred to T444/B3.

**Why**: The floor is an opaque 70 x 54 rectangle on the same lattice as the slots, so each tile already has a pivot from `SlotGridLayout`. Per-tile renderers reuse that geometry and keep pixel snapping per tile, while a tiled sprite would need its own period maths and could drift from the lattice. The pool and the cap bound the cost.

**When you'd choose differently**: If the tile count grows (a much larger field), a single mesh or a tilemap batches better; this is the first thing to revisit if B3 shows the renderer count hurts.

### 37. Separate the import from the sign-off (batch 11, T471)

**What we did**: The floor import (PPU 24, required `F004-FLOOR-TILE` row, atlas, C8 on the real floor) landed first. `reviewedBy` was written in a separate commit after an independent Art Director review, so only the data and doc delta needed a second QA. The same pattern, in the dict schema and with no revision bump, covered L2-L5 and the cash icon in T441. The importer was hardened at the same time: orphan PNGs, duplicates, SHA-256 verified after copy, a placement-mismatch diagnostic and the manifest written last.

**Why**: A reviewer must not approve their own work, and a worker that writes the sign-off field has approved its own work. Writing the manifest last means a failed import never leaves a manifest that claims success.

**When you'd choose differently**: For a throwaway asset the sign-off field can stay empty; for a shipped family it is part of the gate.

### 38. Score blind naming with someone who did not make the icons (batch 11, T441)

**What we did**: An independent namer named the restyled icons without seeing the keys; the parent scored the answers. The misses were useful: the 8 pt INFO pin hint reads as a missing icon, CASH was low or confused with PRESTIGE, the INDUSTRY and SETTINGS gears were confused, and the chevron-over-bar group was confusable. Tint contrast was at least 3.41.

**Why**: An icon its author can name proves nothing about a new player. Scoring by the parent keeps the namer from grading itself, and the OD-17 target (naming in context, not a gate) turns the findings into follow-up work.

**When you'd choose differently**: With a label next to every icon, naming matters less; here several icons stand alone on the HUD strip.

### 39. Keep the OS signal in the bootstrap and cache it (batch 11, T433, T475)

**What we did**: The Settings sheet (Follow/On/Off radio group, effective-state line, "Not saved" note) sits on `DisplayPreferencesStore`. The bootstrap owns the lazy OS reduced-motion signal and its cached value; the store raises `Changed` and the sheet unsubscribes. `IReducedMotionSignal.Available` is a default interface member, so existing implementers keep compiling. A failed file replace is retried once for 40 ms, and only for HResult 0x80070020 and 0x80070021 (sharing and lock violations); other errors show "Not saved" at once.

**Why**: Resolving the OS setting is a JNI round trip with allocations, so views must not poll it. A default interface member adds a capability without touching every implementer. Filtering the retry by HResult avoids hiding a real failure (a full disk) behind a wait.

**When you'd choose differently**: Without a lock-prone file system (Android app storage) no retry is needed; it exists for Windows editor and test runs.

### 40. Test the worst text scale, and put the note first (batch 11, T475, T455)

**What we did**: An independent UI/UX review on captures found that the Settings sheet clipped at 640x360 and 844x390 at 200 % text scale (F1), although the automated visibility test only asserted 100 %. The fix put the "Not saved" note first, hid the group label and the radio "(selected)" suffix on compact landscape, and right-aligned the gear on a wrapped strip with `justify-content: flex-end`. The first QA of the review itself failed because the matrix lacked keyboard and pointer rows and the reviewer identity. F4 (TalkBack sees one empty `SurfaceView`) was accepted as risk R-21.

**Why**: A layout test at the default scale only proves the default. Putting the status note first keeps the most important line visible when space runs out. Accepting F4 explicitly, with an ID, is better than leaving it unrecorded.

**When you'd choose differently**: If the sheet were a full screen the compact rules would not be needed; here it shares a small landscape viewport with the HUD.

### 41. Check the worker's claims, not only its code (batch 11, workflow)

**What we did**: Three worker reports overclaimed: T441 had not re-run the full Play Mode suite after its last fixes, the T455 record lacked matrix rows, and `reviewedBy` was a string instead of the dict schema. QA and the parent caught each. Two usage-limit stops were resolved by renewing leases and messaging the same worker. A stale pytest launcher (exit 3) was fixed with `uv sync --frozen --offline --reinstall-package pytest`. A warm-Library worktree was moved and reused across Unity Issues.

**Why**: A worker report is a claim; the evidence is the commit, the results XML and the schema. Resuming the same worker keeps its context, and a reused Library saves the import time.

**When you'd choose differently**: A trusted, fully automated check (schema validation in CI) can replace the manual look at `reviewedBy`; that is worth adding.

### 42. Fail closed instead of falling back (batch 12, T476)

**What we did**: After the owner chose to delete the Feature 003 placeholders (option D), the 78 sprites, their SVG sources and the legacy fallback rows were removed. A missing or invalid frame now makes the catalog invalid (FR-024) with one diagnostic per key and no substitute sprite. Tests use `RequireRegistryRows = true`, and the shipped scene asserts the flag.

**Why**: A silent placeholder hides a broken import in a shipped build; a start-up failure names the key. The Windows player suite stayed 62/62 on main after the deletion.

**When you'd choose differently**: A live service that must keep running with partial content might prefer a labelled fallback.

### 43. Restate a budget when the measure no longer fits (batch 12, T444)

**What we did**: Budget 8 (SetPass at most 0.86) held on every phone frame, but desktop and the 2048-tile cap needed 2 to 7 passes. The owner restated it in two tiers: a single pass up to about 173 renderers, above that `camera.Render` CPU time (0.5/1.0 ms and 1.5/3.0 ms mean/p95). The emulator rows stayed open as an accepted gap.

**Why**: A pass count is a proxy for cost; the CPU time is the cost. An honest "not demonstrated" plus a recorded gap beats a number from an invalid session.

**When you'd choose differently**: With a physical device, measure frame time directly and drop the proxy.

### 44. An invalid ABA stays invalid (batch 12, T444)

**What we did**: Four cold-booted emulator sessions were run. Three were disturbed by another game client; in the fourth, B1, B3 and empty-run controls all ran about 44 FPS and the 30 FPS control read 25 to 27. B3 was reported as indicative only (43.4 vs 44.6 FPS) and not as a pass.

**Why**: The control proves the session; if it does not reproduce, no before/after statement is valid.

**When you'd choose differently**: Only a quiet host and a healthy emulator change this.

### 45. Give blind namers a reference, and run trials at target size (batch 12, T445)

**What we did**: SC-001 gate fields were 22/22 on all 24 sheets, but reviewer A swapped the machine type names because the vocabulary had no reference picture; this was reported apart. The owner's SC-002 trial (8 of 10, not met, accepted, no rerun) ran in a 1864x893 desktop window, and the reduced-motion variant was not run.

**Why**: Blind naming tests whether shapes are distinct, not whether names are known. A timed trial at the wrong size measures a different task.

**When you'd choose differently**: If type names must be learnable without help, the swap is a real finding rather than a vocabulary gap.

### 46. Close verification with accepted gaps that have IDs (batch 12, T442, T446, T447)

**What we did**: Final QA, eight lens reviews and owner acceptance ended with no blocker. Remaining gaps were recorded, not hidden: no physical device (tap, pinch, pan, font scale 2.0, rotation), no real pinch injection, a journey step with a fixed dismissal coordinate (F4), missing smoke test class (F2), plus Art Bible v1.0.4 for the 720x1280 class.

**Why**: Acceptance with named gaps lets later work pick them up; a vague pass does not.

**When you'd choose differently**: A feature with a device lab should close the device gaps before acceptance.

### 47. Workflow lessons from batch 12 (batch 12, workflow)

**What we did**: QA caught worker overclaims (the journey summary read as PASS despite a failed step). Parent reports (not in the records): the T447 branch predated the T445 merge, and `SettingsSheetTests.TheLiveLineFollowsTheStateTable` failed once. Finished workers' `adb` servers, wait loops and `mcp-for-unity.exe` locked worktrees against `git worktree move`.

**Why**: Compare report wording with results, merge main before QA, rerun a single flaky failure once and record it, and list processes by worktree path before moving it.

**When you'd choose differently**: A CI that validates the result XML makes the first check automatic.

---

---

## Concepts to Know

### Golden files per route
**What it is**: A stored expected output compared on every run. **Where we used it**: `tools/art-pipeline` goldens, run by `scripts/test-art-pipeline.ps1`. **Why it matters**: Without it, a library bump silently changes every asset.

### Tri-state tool exit codes
**What it is**: 0 pass, 1 fail, 2 or 3 "tool unavailable". **Where we used it**: `check-art-tooling.ps1`, `test-art-pipeline.ps1`. **Why it matters**: A missing Blender must not look like a failing test, nor be silently skipped.

### Contract versus live version
**What it is**: The plan named ComfyUI v0.38.0; validation ran on v0.43.0. **Where we used it**: #516. **Why it matters**: Record what you actually tested.

### Measurement environment limits
**What it is**: `UnityStats` and `ProfilerRecorder` are wrong or zero in a batch-mode editor. **Where we used it**: T409 metrics deferred to T410. **Why it matters**: A wrong number is worse than a missing one.

### Comparator literalism
**What it is**: Owner decision 2026-10-27: the parity comparator follows R13 exactly, so only pixels with a delta above 2/255 count against the 0.1 % budget. **Where we used it**: T411 (#523). **Why it matters**: It resolves the strictness question that batch 1 left open; a test must measure the rule that was written, not a stricter one.

### Pure model first, adapter later
**What it is**: Camera scale math lives in a Unity-free class, tested at every screen class. **Where we used it**: `CameraScaleModel` before the T427 controller change. **Why it matters**: Rounding bugs show up in milliseconds in Edit Mode instead of on a device.

### Obsolete API hygiene
**What it is**: Unity 6000.6 marks `PreventDefault()` obsolete; `ResearchPanel` now calls `StopPropagation()` plus `focusController?.IgnoreEvent(evt)` (T452). **Why it matters**: Obsolete messages become removals; fix them while behavior is covered by tests.

### Global settings drift
**What it is**: URP auto-creates GlobalSettings; its legacy top-level stripping fields differ from the authoritative managed-reference ones, and Unity once emptied `m_List`. **Where we used it**: T411, T453 (re-serialized, reverted). **Why it matters**: Diff the asset after every Unity run; settings-noise checks do not cover it.

### Line endings in recorded hashes
**What it is**: JSON written with CRLF on Windows changed after Git LF normalization, breaking the recorded `checkReportHash`. **Where we used it**: T415. **Why it matters**: Write generated files with `newline="
"`.

### Evidence limits of a player
**What it is**: The Windows player has no text-scale input, so 200 % evidence comes only from the rig or a device; a dev player can be driven with user32 `mouse_event` plus `CopyFromScreen`. **Where we used it**: T453.

### Known first-run flake
**What it is**: `RenderParityTests` at desktop 640x480 failed once in the first full Play Mode run after a fresh `Library` import or a target switch, then passed on isolation and rerun. **Where we used it**: T414, T416 evidence runs. **Why it matters**: Rerun once before treating it as a regression, and record it; it is a test-hardening candidate.

### Long Unity runs and agents
**What it is**: QA subagents handed back before a 25-30 min Play Mode run finished, and stopping them did not kill Unity. **Why it matters**: Start long runs as the parent (or have QA return the PID and results path) and judge the evidence from the results XML.

### Long-run budgets
**What it is**: A full Play Mode run takes 25-30 min (`-TimeoutSeconds 3000`); C: ran out of space in the first Android build (`Library/Bee` about 4.8 GB per worktree). **Where we used it**: T411.

### Class-scoped layout exception
**What it is**: USS rules that apply only for one device class and text scale (`.run-text-scale--150` to `--200` with `.factory-hud--compact-landscape`). **Where we used it**: T459. **Why it matters**: Other classes stay unchanged, but a shared property such as the horizontal inset can still re-wrap their text.

### Soft-disabled control
**What it is**: A button that stays visible and focusable at its limit but ignores the action and explains why. **Where we used it**: T454 zoom buttons. **Why it matters**: Hiding it moves the other controls, and a tooltip does not exist on touch.

### Stable ordering for ties
**What it is**: A tie-break rule (larger area) when two candidates score equally. **Where we used it**: T463 ore-vein cut. **Why it matters**: Without it the layout flips between frames at the equality point.

---

## Architecture Overview

Batch 1 adds only tooling around the game: `tools/art-pipeline` (uv project: pixel pass, checks, cues, palette, models), a headless Blender rig, ComfyUI workflows, PowerShell gates (`check-art-tooling`, `test-art-pipeline`, chained from `test-unity.ps1` on art-path changes), and a PlayMode `RenderParityTests` class with committed baselines. No runtime code changed in batch 1. Batch 3 adds the URP 17.6.0 2D Renderer assets and config tests (T411) and the S1 spike generator under `art-source/f004/spike/s1/` (T415). Batch 4 adds the isolated spike import and scene (T414), the display-preferences store and reduced-motion signal (T432, not yet wired to presentation), an F004 emulator scenario (T443) and the S1 review record (T416). Batch 2 adds the pure `CameraScaleModel` (not yet wired), baseline helpers in `scripts/f004-b1/`, and small panel fixes (machine hint, contract copy and reason line, `StopPropagation`).

Batch 11 adds the floor family end to end (re-render, import, slots pipeline, per-tile floor renderers in `SlotGridView`), the Settings sheet and gear, and the restyled UI icons bound by key.

Batch 10 adds runtime geometry and the first real art: `SlotGridLayout` (rectangular lattice), gesture-aware zoom intents in the camera controller, a corner-aware unobstructed rectangle in `RunShellDocument`, and the F004 import (manifest, catalog rows with explicit canvas and placement, four Sprite Atlas v2, the `F004ArtIntegration.cs` tool). The floor family waits for T470 and T471.

```
brief -> route (Blender | ComfyUI | script) -> pixel pass -> checks -> goldens/gates
                                              pre-switch baselines (T409) -> URP parity (T411)
```

---

## Glossary

| Term | Meaning |
|------|---------|
| Pixel pass | Deterministic reduction to the art grid, palette and alpha rules |
| W004-V | Pinned fixture with forced machine states used for render baselines |
| `n_min` | Smallest allowed integer camera scale; stand-in is 24 px per cell until T427 |
| Lease | Atomic file marking exclusive use of a workstation resource |
| `CameraScaleModel` | Pure class computing the integer camera scale rules (T408); applied by T427 |
| SetPass | Render state changes per draw group; stand-in for the draw counters that read 0 in 6000.6 |
| `SpikeTextureImportRules` | Editor postprocessor that fixes PPU, filter, mips and compression for the spike density folders |
| `SlotGridLayout` | Pure rectangular-lattice geometry: pivot, hit test, extent, row (T456) |
| `LargestFittingScale` | Largest integer camera scale that fits a rectangle; picks the ore-vein cut (T463) |
| `import-manifest.json` | Single list of imported art files with hashes, read by the importer and atlas tool (T440) |
| `DisplayPreferencesStore` | Reads and writes `display-v1.json`, the reduced-motion override, outside the save |

### ABA bracketing
**What it is**: Measure the baseline (A), the change (B), then the baseline again, so drift over the session is visible. **Where we used it**: T412, with a 30 FPS control. **Why it matters**: A single before/after pair cannot tell a regression from host noise.

### Quiet-host protocol
**What it is**: Pausing other work, closing launchers, and moving every lease to one Issue before a timed run. **Where we used it**: T412. **Why it matters**: Host load changes FPS more than most code changes; the protocol makes the measurement valid, and a busy lease should make a worker stop early rather than measure anyway.

### Verbatim parallel reviews with a diff QA
**What it is**: Independent reviewers that return text run in parallel; the parent saves their reports verbatim plus a consolidated table, and a Low-tier QA diffs the record against the saved reports. **Where we used it**: T425, `reviews/art-bible-review.md`. **Why it matters**: The parent adds synthesis without changing any verdict, and the diff proves it. Also: check the density before stating a PPU in an assignment (D24 sprites use PPU = D = 24, not the 64 PPU F003 fallback).

### Delta QA after a merge from main
**What it is**: When main moves and an in-review PR conflicts, merge main into the branch and review only what changed. **Where we used it**: #591 landing under the open batch-5 PRs. **Why it matters**: QA verdicts are per commit; a new sha needs a new verdict before the merge gate.
