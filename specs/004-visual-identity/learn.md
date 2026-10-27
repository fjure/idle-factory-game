# What I Learned: Visual Identity - 2.5D Pixel-Art Restyle

**Feature**: Restyle the game as 2.5D pixel art through an agent-operated art pipeline and a URP 2D Renderer switch.
**Generated**: 2026-10-27
**Scope**: MAQA batch 1 (tooling and baselines; no player-visible change yet)
**Implementation status**: 9/56 tasks completed (T400-T407, T409; GitHub Issues are the status authority)

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

---

## Architecture Overview

Batch 1 adds only tooling around the game: `tools/art-pipeline` (uv project: pixel pass, checks, cues, palette, models), a headless Blender rig, ComfyUI workflows, PowerShell gates (`check-art-tooling`, `test-art-pipeline`, chained from `test-unity.ps1` on art-path changes), and a PlayMode `RenderParityTests` class with committed baselines. No runtime code changed.

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
