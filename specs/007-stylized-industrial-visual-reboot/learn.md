# What I Learned: Stylized Industrial 2.5D Visual Reboot (F007)

**Feature**: Replace the pixel-art presentation with a stylized industrial 2.5D look; no gameplay, economy, content or save changes
**Generated**: 2026-11-27 (batch B0)
**Scope**: Batch B0 (T600, T602, T638; T603 blocked; T601 and T604 deferred until the physical Pixel is connected)
**Implementation status**: 3/45 tasks completed (T600, T602, T638 merged; T603 blocked, not merged)

---

## Key Decisions

### 1. Binding ledger over task prose when ordering disagrees

**What we did**: The T600 gate re-check treated the orchestration ledger as authoritative where task text and ledger conflict (for example T604 waits for T603, although the task text lists only T601 and T602).

**Why**: Task prose is edited by hand and drifts; the ledger is validated mechanically (45/45 rows, prerequisites only earlier units). Safer to serialise too much than to run a dependent capture early.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Follow the task text | Carries stale `[P]` hints and would start T604 before its baseline exists |
| Fix all text drift before B0 | The 3 MEDIUM and 4 LOW findings are non-blocking; they fold into the T637 regeneration |

**When you'd choose differently**: When the ledger itself is stale (not refreshed after a task change); then refresh it first.

---

### 2. Exact-id rebaseline map for missing tests

**What we did**: `tools/test-ids/compare_test_ids.py` compares test ids (3758 scanned) and accepts a missing id only through an exact-id row marked `moved` or `removed` in `reviews/rebaseline-map.md`. Class-level `.*` rows never satisfy it.

**Why**: A wildcard row can silently excuse deleted tests, which defeats the purpose of a test-preservation gate before a visual rewrite.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Class-level wildcard rows | Hide individual deletions |
| Rely on pass counts | A renamed or removed test with another added keeps the count equal |

**When you'd choose differently**: With generated or parameterised tests, where exact ids cannot be listed; the parser does not evaluate `#if` or expand TestCaseSource, so such ids need a separate check.

---

### 3. Lease coverage for new hardware and new art paths

**What we did**: The physical Pixel 11 Pro is covered by the `emulator` lease (with `-Serial` when two devices are attached); art-path triggers now include `Art/F006/` and `Art/F007/`.

**Why**: Parallel sessions corrupt device evidence, and a path trigger that misses a folder lets commits skip the art-pipeline gate (a real gap: commit 137da8e4).

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Separate `phone` lease | Same contention risk, more bookkeeping; adb is one server |

**When you'd choose differently**: If a second device is permanently attached and runs independently, give it its own resource name.

---

## Concepts to Know

### Determinism of pixel baselines

**What it is**: A baseline capture is only useful if the same input gives the same pixels. Wall-clock state, glyph packing order and carried-over camera state each break that silently.

**Where we used it**: T603 (#800) failed QA three times (eee053f, a6eb469, 9f62c16) on exactly these: live-session HUD values, TextCore dynamic atlas packing (+-1 AA jitter, 7 px seen), and 640x360 framing panning about 41 px after another cell.

**Why it matters**: Fixes are: `OverrideRunCompositionForTests` (fixed UTC, 0 active seconds, temp root), a fixed-order font pre-warm (`ClearFontAssetData` plus `TryAddCharacters`), and per-job camera reset. Prove determinism with a shuffled job order, not repeated same-order sessions.

### Mutation-testing the metric tests

**What it is**: Deliberately breaking the code under test to see whether tests notice.

**Where we used it**: T603 metric tests; it exposed untested IoU centering and outward rect rounding, now covered.

**Why it matters**: Image metrics look right on easy inputs and fail at rounding edges.

### Host display limits for player captures

**What it is**: A Windows player window cannot exceed the 1920x1080 display, so tabL 2560x1600 and emuP 1080x2400 need editor Play Mode with `PlayModeWindow.SetCustomRenderingResolution` (`buildKind` `editor-playmode`).

**Where we used it**: T603 capture route; not yet confirmed by the owner.

**Why it matters**: Evidence from a different build kind may not be comparable with device captures.

---

## Architecture Overview

B0 adds tooling around the visual work, not product code: gates record (`reviews/gates.md`), test-id comparison (`tools/test-ids/`, optional `-CheckTestIds` in `scripts/test-unity.ps1`), lease and trigger rules, and a capture harness (T603, unmerged). Look-profile and art work come in later batches.

## Glossary

| Term | Meaning |
|------|---------|
| Rebaseline map | `reviews/rebaseline-map.md`: candidates that excuse a missing test id |
| buildKind | Capture metadata naming the route, here `editor-playmode` |
| OD7-n | F007 owner decision numbers in plan.md |
