---
title: F006 batch P1 enabling refactor
type: component
sources: [S056, S058]
updated: 2026-11-12
---

# F006 batch P1: the enabling refactor (T510-T518)

Batch P1 landed the code seams that the world presentation needs, without changing player-visible behaviour. Edit Mode tests went from 2569 to 2683, Play Mode from 2596 to 2616. (S058)

## What merged

| Task | Issue / PR | Result |
|---|---|---|
| T516 | #690 / #728 | Art Bible v1.1.0 draft: world-layer budget, allowance and sprite tables, OD-9b conditions encoded |
| T517 | #691 / #729 | A-03 anchor measurement module `tools/art-pipeline/art_pipeline/f006/a03_anchor.py`, candidates K0-K5, report `reviews/a03-anchor.md` |
| T510 | #684 / #730 | Pure move of `SlotGridView` to `Presentation/World/`; `SlotVisualModel`, `SlotLattice`, `FloorRange`, `RunZoomSettings` and `SlotRenderers` extracted into their own files (only `SlotRenderers` changed visibility, private nested to internal) |
| T511 | #685 / #731 | `ShellNavigationStack` and `ShellSheetHost` extracted from `RunShellDocument`; `HandleBack` delegates |
| T513 | #687 / #732 | Sorting layers `WorldBase`, `WorldDeck`, `WorldLinks`, `Default`, `Overlay`, `Effects` created through Unity tooling; `SortingLayerGuardTests` |
| T514 | #688 / #733 | `CompletedCycles` (R08, OD-11): per-advance count in `RunSimulator`, session-owned `long` total, `MachineStateView.CompletedCycles`; no observer effect |
| T512 | #686 / #734 | Layout seam: `FactoryWorldLayout`, `LatticeLayoutBuilder`, `BuildingLocationId`, `WorldCompositionSettings` plus asset; camera migrated to `LayoutId` and `Bounds`; test layouts 6, 20 and L |
| T515 | #689 / #735 | Play Mode `RunShellTestNavigation` seam; R19 re-baseline list |

Commits: 524f5a9, b2cd84b, d314b23, 5502682, 3336321, 059845b, ccd9864, dff6be6. (S058) See [world presentation design](./f006-world-presentation-design.md) for the design these implement.

## Parent decisions (T518, e6c2d2a)

The owner signed off Art Bible v1.1.0 as drafted (OD-10), chose A-03 anchor K4, (59,33) with a 4 px pointer tail (OD-12), and decided to recolour the trunk under the badge band (strict C8 ring rule). See Art Bible section 21.8. (S058)

## Carried follow-ups

`reviews/p1-followups.md` assigns each to a later task. P1-F1: wire `WorldCompositionSettings` at runtime, with a composition hash in `LayoutId` (T527). P1-F2: the depot and the vein sit outside the pan clamp, so a P3 camera `ViewBounds` is needed (T534). The `data-model.md` corrections landed in 4315ecd. (S058)

## Pitfalls that will recur

- **Packed arrays are not hand edits.** Unity serializes enum or int arrays in ScriptableObject YAML as packed hex with an `i` suffix (`edgeRulePerStage: 000000000100000002000000i`, like `m_StackTraceTypes` in ProjectSettings). A reviewer once mistook it for hand-edited YAML. (S058)
- **JSON-authority exemption.** Presentation-only tuning ScriptableObjects are exempted from `JsonContentAuthorityTests` through the one-entry `PresentationTuningAssets` list. (S058)
- **No TagManager signature in the noise check.** `check-settings-noise.ps1` does not detect TagManager changes, so a deliberate sorting-layer edit passes clean; the exception is recorded in `reviews/test-rebaseline.md`. (S058)
- **Known flake.** `SessionLogAllocationTests.FlushThroughTheRealFileLayerStaysInTheAllocationAndMillisecondBudget` failed once at 64.9 ms against 25 ms, then passed; it is a host-timing flake. (S058)
- **Rate limit.** A usage limit killed the T510 worker mid-run; renew the lease, then resume the same worker with SendMessage, and it reuses its results. (S058)
- **gh flake.** One transient GitHub GraphQL error dropped both a label edit and a comment in a chained command. Re-verify the marker comment after such an error. (S058)

Workstation tips are in [P1 workflow pitfalls](./f006-batch-p1-workflow-pitfalls.md).
