# F006 P3 batch B follow-ups (parent record)

**Batch**: P3 batch B remainder (T536, T537) after T535 and T538, plus the T543 batch-B gate.
**Recorded by**: the parent orchestrator, 2026-11-17, from the independent QA reports and the Software Architect, UI/UX, Product Owner and Art Director reviews of T536 (#708, PR #758) and T537 (#710, PR #759).

## Merged

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T536 | #708 | #758 | `d726309` | PASS: Edit Mode 2874/2874, Play Mode 3061/3061 | Architect APPROVE_WITH_NOTES; UI/UX and PO ACCEPT_WITH_NOTES; Art Director ACCEPT |
| T537 | #710 | #759 | `7042a7c` (fix round on `5fea213`) | PASS: Edit Mode 2877/2877, Play Mode 3149/3149 | Architect APPROVE_WITH_NOTES; UI/UX, PO and Art Director ACCEPT_WITH_NOTES |

T536 merged without a fix round. Its two review majors were not regressions and sat in files T537 rewrote, so they went into T537 as carried items 1 and 2, and both were closed there. The T537 fix round restored the OD-9b C1 assertion: the whole factory is in view at fit-all on every class. The first commit had relaxed that assertion for 640×360 at 200 %, and a parent cannot waive a binding owner condition. On compact landscape the zoom column now folds into the right action column, so the world share at 640×360 and 200 % rose from 27.7 % to 36.0 % (chrome 62.0 % to 55.8 %).

## Carried items

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| B-1 | T536 UI/UX F5, T537 item 5 | Audit the ~26 `SetSoftDisabled` call sites: every hosted disabled control needs a reason a touch user can reach (P2-F19). | T542 |
| B-2 | T535 Architect F6, T537 item 6 | First reframe before the entry layout. | T528 |
| B-3 | T534 Architect F8, T537 item 6 | Presenter-level integration tests. | T542 |
| B-4 | A-5 (copy), T537 item 7 | The card cause line is category-level ("Short on Iron Ore") and hides auto-sell. T528 supplies the specific cause in the model; the copy follows. | T528, then T547 |
| B-5 | A-6, T537 item 8 | Camera shift after a text-scale change with Details open; at portrait 200 %, overlays draw over the card edge. Needs a named acceptance check. | T543-B gate watch, then T528 or T542 |
| B-6 | A-12, T537 item 11 | No C2 leader from the card to its machine on landscape and tablet. The Close icon has no accessible name beyond its tooltip. | Later task (C2); T547 (name) |
| B-7 | T535 UI/UX U-2, U-4 to U-7; T534 UX F3 | Not checked in T537. | T542 or T547 |
| B-8 | T537 UI/UX 1, QA 4 | Phone landscape hides both persistent columns while a sheet or the machine card is open, so Cash, Store, attention, Office and Overdrive are not visible then. This is documented in contracts §1 "As built". | T543-B gate watch; owner |
| B-9 | T537 UI/UX 2, baseline.md | SC-010: persistent labels 13 to 10 (77 %, guidance 50 %). Phone-landscape chrome stays high (640×360 at 200 %: 55.8 %). | T543-B gate; owner |
| B-10 | T537 UI/UX 4 | Portrait: the ore-vein Mine overlay sits directly above Overdrive, a mis-tap risk. | T543-B gate watch |
| B-11 | T537 PO 8 | "Overdrive is not unlocked yet" and "Locked: not unlocked yet." give no cause. Name the condition, or show "Locked" alone. | T547 |
| B-12 | T536 UI/UX 2 | In-flow notices shift the body when a notice arrives or is dismissed. | T547 playthrough |
| B-13 | T536 Architect F5, T537 Architect 8 | USS cascade: `PanelScaffold.uss` is imported twice, and later FactoryRun rules can undercut the 48 pt floor. `ScaffoldScope` re-queries buttons on every geometry change. | T542 (performance and cleanup) |
| B-14 | T537 Architect 3 | `ShellTab`, `SelectTab` and `OpenTabAtSection` stay public as a shim, and the six-tab `TabNames` remain. Retire them when no caller is left. | T542 |
| B-15 | T537 QA (fix round) | `ScreenClassScalePlayTests.The1280x720PhoneAt320Dpi` no longer proves that the 1 % n_min tolerance is load-bearing (the replaced line duplicates line 195). The landscape zoom-hint assertion uses a loose `.Or`. | T542 |
| B-16 | T537 Architect 4 | Layout suites run with the zoom preference on, the worst case. The default touch player is covered only by the new landscape fit test. | T542 |
| B-17 | Office glyph | `f006-ui-office` is still not imported. The hub entry uses the contracts glyph behind `HubCards.EntryIconKey`. | T527 (with A-13) |

The open items from `p2-followups.md` and `p3a-followups.md` (A-9, A-10, A-11 remainder, A-13) remain carried as recorded there.

## T527 (P2 strand)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T527 | #700 | #762 | `d63f18a` | PASS: Edit Mode 2933/2933, Play Mode 3197/3197, art pipeline 348, settings noise OK | Architect APPROVE_WITH_NOTES; Art Director with Technical Artist, Performance, and World Designer with Product Owner ACCEPT_WITH_NOTES |

T527 merged without a fix round. No review finding was a blocker or major. The Art Director amended the worker's EDGE wording; the parent recorded it as Art Bible 21.10. The `test-rebaseline.md` pixel counts were corrected (155 and 241 per frame).

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-1 | Architect 1 | `DeckPlan.BuildProps` takes a modulus by `span + 1`; a prop wider or taller than a cell gives a modulus of 0. Unreachable with shipped props. Guard and test. | T529 |
| C-2 | Architect 7 | `FactoryDeckView.OnDestroy` calls `Destroy` on HideAndDontSave tiles; use `DestroyImmediate` in Edit Mode. | T529 |
| C-3 | Architect 5, Performance P2 | No warm-state 0-allocation test for the deck view after the floor allocation test was removed. | T529 (`F006AllocationTests`) |
| C-4 | Architect 6 | `F006SceneWiring.WriteCompositionSettings` overwrites prop weights and densities on every run, which would clobber later World Designer tuning. Write only when empty or default. | T542 |
| C-5 | Architect 2 | The layout composition is static global state (`LatticeLayoutBuilder.activeSettings`); consumers must get the layout only through `Shipped(orientation)`, and the plan should name the single-bootstrap assumption. | T534 |
| C-6 | Architect 3 | `PresentationSpriteCatalog.PlacedF006Keys` must be declared before `Registry` (static initializer order). Add a guard test or compute it in a method. | T531 |
| C-7 | Architect 4, Art Bible 21.9 item 7 | A full F004 re-import reverts the K4 tails; tests catch it. Make `F004ArtIntegration` run the swap or fail with a pointer. | T547 |
| C-8 | Performance P1, Art Bible 21.10 item 3 | Edge sprite renderers (7 to 11) are outside the 31-sprite world budget; about 103 renderers at 12 locations against the derived 95 (tier 173). | T531 census; T528 decides on the third Tilemap |
| C-9 | Performance list | T531 measurement list: W006-V workload, budgets 1 to 11, renderer census per layout, SetPass with card closed and 3 FX, `reviews/performance-b4.md`. | T531 |
| C-10 | Art Director 3 | The cue luminance ratios are not in the `cue_over_lane_contrast` gate, because adding them requires regenerating `art-source/f006/link` records. Needs an art-source task before T542, or an accepted risk recorded in T547. | Owner decision (new art task or T547) |
| C-11 | Art Director 3 | Runtime check that a `copper_2` pip with its `ink_0` ring passes the ring rule under a K4 tail. | T528 |
| C-12 | Art Director 2, 6; World Designer W1, W2 | Look items for the trials: gantry 9 px under the depot, floating stage-3 props, the dark base near the background tone (the V seems to float), the front-only wall reads as a fence. | T546 trials; owner final visual gate |
| C-13 | World Designer W3 | Props sit only on empty or locked cells, so a fully built factory loses its stage-3 density. Check in the stage trial. | T540 |
| C-14 | World Designer W4, Art Director 1 | The depot is outside the fit-all view at 640x360 and 200 % (recorded, reachable by panning). Confirm it stays discoverable. | T534, T542 |
| C-15 | QA minor 1 | `F004AssetContractTests` floor tone check moved from `Is.EqualTo` to `Is.SubsetOf`; still excludes `floor_2`. | Accepted |
| C-16 | Worker | SC-006 shuffled-order trial not run. A preliminary independent trial on the six `t527-proofs` captures is possible; the binding trial is T546 with the owner. | T530/T546 |

The World Designer confirmed the prop weights and densities (0.20, 0.30, 0.40).

## T556 (owner decision on C-10)

The Art Director accepted the outline-carried copper pairs with an area limit (no recolour); the parent recorded it as Art Bible 21.11.

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-17 | T556 Art Director F1 | The area gate is a per-badge average, not a per-badge maximum. Add a maximum when lane art changes. | Later art task |
| C-18 | T556 Art Director F2 | Thin margins (outline over `copper_1` 3.47:1, `cue_alert` over `dim_0` 3.52:1); CVD-simulated luminance is not computed in the cue gate. | Later art task |
| C-19 | T556 Art Director F3 | The gate note still points to the T522 report; reword at the next pipeline regeneration (never hand-edit the report). | Later art task |

## T529 (P2 strand)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T529 | #703 | #765 | `5d64819` (two fix rounds on `87f4ac0`) | PASS: Edit Mode 2971/2971, Play Mode 3202/3202, settings noise OK | Architect APPROVE_WITH_NOTES; Performance ACCEPT_WITH_NOTES (re-checked twice) |

Fix round 1 replaced a throttled full `RunSnapshots.Factory` rebuild (up to 10 per second on a running factory) with an allocation-free per-frame completion path. Re-QA then found that an active production contract bumps `Contracts.Version`, and with it `StructureRevision`, on every produced unit, so fix round 2 rebuilds immediately only on machine identity, layout, offline return and session replace, and at most once per second otherwise. Both cases now have 600-frame zero-allocation Play Mode tests.

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-20 | Performance re-check | Measure structural rebuilds per 600 frames and allocations per rebuild (about 174) on W006-V; label them structural-event allocations, not steady state. Consider one shared factory snapshot for `RunShellPresenter` and the feed. | T531 |
| C-21 | Architect, QA | The world model's VisualState and QuantityRevision are as old as the last rebuild (up to 1 s). The feed uses only deltas and ActivityFps; a later reader must not rely on them. | T531, T534 |
| C-22 | Architect | `familyFpsCaps` is not serialized in `WorldCompositionSettings.asset` (class default Assembler 8 fps applies; a test pins it). Serialize at the next Unity re-save. | T534 or T547 |
| C-23 | Architect | P2-F14 decision: the per-family fps caps live in the presentation tuning asset `WorldCompositionSettings`, not gameplay content data. | Recorded |
| C-24 | Worker | FX rows (pop, spark, steam, glint, dust) stay optional in the catalog; Steam and BuildDust are not spawned yet; pop placement at `PlateCentreOf` is unverified on device. | T528 (FX), T543-C gate |
| C-25 | Worker (P2-F15 N3) | Document in research R09 that a locked input gives no shortage. | Parent at batch end |
