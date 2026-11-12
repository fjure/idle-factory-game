# F006 batch P1 follow-ups (parent record)

**Batch**: P1, the enabling refactor (T510–T518), 2026-11-11 and 2026-11-12. **Recorded by**: the parent orchestrator from the independent reviews of the merged Issues. None of these blocked a merge. Each one names the later task that carries it. The assignment for that task cites this file.

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| P1-F1 | T512 (#686) architect review (e), F2 | Wire `Assets/IdleFactory/Art/F006/WorldCompositionSettings.asset` at runtime. Load it, validate it and feed it to `LatticeLayoutBuilder` instead of the built-in `Shipped` defaults. In the same change, `LayoutId` gains a composition hash or version, and the `Shipped` cache becomes settings-aware. Once the shell asks the same builder, remove the `RunShellDocument` orientation fallback to the F004 lattice. | T527, the first task that reads the stage weights from the settings |
| P1-F2 | T512 architect review (c); OD-9b UI/UX C1 | The depot and the vein sit one lattice cell outside `Bounds`, beyond the 0.5-unit fit and pan margin, so today's clamp hides them. The camera model gets a second extent, such as `ScaleLayout.ViewBounds` defaulting to `Bounds`, so pan and fit include the depot and the vein when they fit. Record vein and depot visibility per screen class; an out-of-view class is recorded, not hidden. | T534 (card docking and pan-away) with the C1 camera rule |
| P1-F3 | T512 architect F1; World Designer C-2 | The `KeepOutOf` doc in `FactoryWorldLayout.cs` says no lane may cover a keep-out. By geometry, a location's badge and marker boxes lie over its own trunk and branch, and the lanes draw below the overlay layer. Choose one: fix the doc to say so, or add a lane-vs-keep-out check. Also check cue-over-lane contrast in the link art review. | T526 (link import) and T525 (link-family review) |
| P1-F4 | T512 World Designer C-3; architect (d) | `Validate()` does not catch the deposit sharing the depot's collector column, for example deposit Right while depot Right. Add the check. Until then, keep the deposit on Left or Bottom while the depot is on Right. | T527 |
| P1-F5 | T512 World Designer C-6 | The stage-2 Wall default in `edgeRulePerStage` stays provisional until the C8 ring proof (OD-9b, Art Bible §21.8). | T521 or T525 (edge family) |
| P1-F6 | T512 architect F3 | `marginCells` is in world units while `deckBandCells` is in lattice cells. Consider renaming it to `marginWorldUnits`. `data-model.md` now documents the unit. | T527, together with P1-F1 |
| P1-F7 | T512 architect F4, QA observation 2 | `NoFixedSlotCountGuardTests` skips any line that mentions `ShippedLocationCount`, and it does not scan `RunShellDocument`, `CoreGameFoundationBootstrap` or `RunShellPresenter`. Narrow the bypass to the builder file and widen the scanned paths. | Optional, any later P3 Gameplay task |
| P1-F8 | T514 (#688) architect F1 | `RunSimulator.TryGetCompletedCount` could be internal. It stays public for symmetry with `TryGetLastCompletion`. | No action |
| P1-F9 | T516 (#690) and T517 (#691) QA | Align research R12 (lamp tones) with Art Bible v1.1.0. The `steel_1` trunk lane is recoloured where it passes the badge band (OD-10 decision, Art Bible §21.8). | T520 or T522 (LINK art) |
| P1-F10 | T517 | The K4 pointer tail is a mock. The production tail needs a brief and an Art Director pixel review through the cue-sprite pipeline (OD-12). | The P2 art briefs: the parent assigns it with T519–T524 |

**Scope note**: T512 also changed `SlotGridView.cs` (`Columns`/`SetColumns` → `Layout`/`SetLayout`), which was outside its assigned edit list. The architect review accepted this as necessary and minimal, and it is recorded on Issue #686.

## Batch-end gates (2026-11-12)

| Gate | Result |
|---|---|
| All batch Issues merged and closed | #690, #691, #684, #685, #687, #688, #686 and #689 merged through `scripts/maqa-merge.ps1` after independent QA PASS (PRs #728–#735). |
| Owner decisions | T518 recorded: OD-10 (Art Bible v1.1.0 signed off), OD-12 (A-03 anchor K4) and the trunk recolour. |
| Unity suites on the last Issue commit | Edit Mode 2683/2683 and Play Mode 2616/2616 (T515 QA, `dff6be6`). |
| Tagged Windows test-player suite | `scripts/test-unity.ps1 -StandaloneWindowsPlayer` on main `0f70490`, with leases `unity,emulator`: PlayerPlayMode 62/62 passed, exit 0. This is the first batch with runtime changes. |
| after_implement hooks | Wiki ingest (S056 re-dated, S058 added), learn review, and wiki lint all ran. `check-after-implement-hooks.ps1` gives PASS 7/7, after a fresh `test-art-pipeline.ps1` run (276 passed) for T517's art-path change. |
| Stale-worktree report | No P1 worktree is left. The 7 older entries (1 REMOVABLE, `docs/675-wiki-maintenance`; 6 REVIEW from F004 or other sessions) are unchanged and untouched. |
| Play-Tester | **Not triggered; reason recorded.** P1 changes no production, contract, upgrade, unlock, reward, offline-progress or prestige rule, and no player-visible flow. T510, T511 and T512 are behaviour-preserving refactors with identical expected values. T513 only adds unused sorting layers. T514 adds a read value with no observer effect: the golden trace and the save bytes are byte-identical. T516, T517 and T518 are documents and measurement tooling. The Play-Tester runs on the first batch that changes the player-visible loop (P2 art integration or P3). |
| UI/UX independent acceptance | Not required for this batch, because nothing changed visibly for the player. The T511 shell extraction was checked for identical focus and back behaviour by the QA with the UI/UX lens. |
