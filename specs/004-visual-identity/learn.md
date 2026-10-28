# What I Learned: Visual Identity - 2.5D Pixel-Art Restyle

**Feature**: Restyle the game as 2.5D pixel art through an agent-operated art pipeline and a URP 2D Renderer switch.
**Generated**: 2026-10-28 (batch 1 on 2026-10-27, batches 2 and 3 added)
**Scope**: MAQA batches 1 to 3 (tooling, baselines, camera scale model, panel polish, S1 spike assets, URP phase 0 switch and reviews)
**Implementation status**: 18/56 tasks completed (batch 1: T400-T407, T409; batch 2: T408, T410, T450-T452; batch 3: T411, T413, T415, T453; T412 B2 performance deferred by the owner, not done; GitHub Issues are the status authority)

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

### Long-run budgets
**What it is**: A full Play Mode run takes 25-30 min (`-TimeoutSeconds 3000`); C: ran out of space in the first Android build (`Library/Bee` about 4.8 GB per worktree). **Where we used it**: T411.

---

## Architecture Overview

Batch 1 adds only tooling around the game: `tools/art-pipeline` (uv project: pixel pass, checks, cues, palette, models), a headless Blender rig, ComfyUI workflows, PowerShell gates (`check-art-tooling`, `test-art-pipeline`, chained from `test-unity.ps1` on art-path changes), and a PlayMode `RenderParityTests` class with committed baselines. No runtime code changed in batch 1. Batch 3 adds the URP 17.6.0 2D Renderer assets and config tests (T411) and the S1 spike generator under `art-source/f004/spike/s1/` (T415). Batch 2 adds the pure `CameraScaleModel` (not yet wired), baseline helpers in `scripts/f004-b1/`, and small panel fixes (machine hint, contract copy and reason line, `StopPropagation`).

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
