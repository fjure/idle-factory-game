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

## T528 (P2 strand) review carries

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-26 | T528 Architect F1; owner decision 2026-11-18 | `AttentionMarkerView` was never instantiated in production since T532, so world attention markers never showed and the tap priority is inactive. | T557 (#767) |
| C-27 | T528 Architect 6 | B-2 ("first reframe before the entry layout", T535 Architect F6): the original F6 text is not in the repo, the wiki or Issue #707. Probable reading: the first camera fit must wait for the entry/hub layout, as the card already does through `TryGetUnobstructedScreenRect`. | T542 (confirm with the owner if the reading is wrong) |
| C-28 | T528 Architect 7 | B-5 confirmed by code: a text-scale change with Details open runs the `ApplyRunViewport` Fit branch without the `cardBandPending` guard. Named acceptance check: portrait 200 %, Details open, change text scale, card edge not overdrawn. | T542 |
| C-29 | T528 Architect F2, F3 | `MachinePresentationModel.CauseOf` classifies from aggregate rates (confirm `ProductionPerMinuteMicro` is effective, not potential; test a blocked maker). `MachineCard.CauseDetail` English strings and tooltip-only detail. | T547 |
| C-30 | T528 Architect N4, N5 | `WorldTapResolver` takes the concrete `AttentionMarkerView` (an `IMarkerHits` interface would keep it pure); `CoreGameFoundationBootstrap` keeps growing (1,773 lines). | T542 or a refactor task |
| C-31 | T528 Art Director 1, 4 | Look items for the batch-C gate: an Overdrive machine's pip is mostly under the marker; the pip's lower half crosses the machine body at the rear edge (mitigation: start at rear edge + 5); the depot slab is clipped 5 px by the last-column plate; the left-depot junction is mirrored 1 px off (no shipped layout). | T543-C gate |
| C-32 | T528 Art Director 3 | The pip loop is not in `check_flash` composites; optional per-location pip phase. | T522 tooling or T542 |
| C-33 | T528 Art Director 1, 5 | SetPass may rise by one (Overlay: pips from the FX atlas between cue draws); census and budget 8 re-measure; Art Bible 21.12 awaits owner confirmation. | T531; owner |
| C-34 | T528 Art Director 2 | A visually stronger depot full cue (art change). | Later art task |

## T557 (attention marker wiring)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T557 | #767 | #768 | `1e2d474` | PASS: Edit Mode 3043/3043, Play Mode 3240/3240, settings noise OK | Architect APPROVE_WITH_NOTES; UI/UX with Art Director ACCEPT_WITH_NOTES |

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-35 | UI/UX F1 | The scene test proves keep-out for the first marker only: not the shifted second shortage marker, the store-full marker, other layouts, the card band, or the backdrop tone (Art Bible C1/C8). Optional Edit Mode sweep over all shipped layouts. | T543-C gate; T542 |
| C-36 | UI/UX F2 | The store-full marker draws over the depot and may hide the FULL copper lip. | T543-C gate; owner if an offset (contract change) is needed |
| C-37 | UI/UX F3 | The marker icon is scaled 22/23 (non-integer) on pixel art. Drawing at native size changes `MarkerSizePixels` in the contract. | T543-C gate; owner |
| C-38 | UI/UX F4 | World markers have no screen-reader name or focus; the strip indicator is the accessible route. Record as a deliberate decision. | T547, T542 |
| C-39 | UI/UX F5 | After a layout change a marker can be drawn and hit at its old position for up to 0.25 s until the feed refreshes. Fix: invalidate the presenter on `LayoutChanged`, or skip rendering when the model's layout differs. | T542 |
| C-40 | QA, Architect | The touch-circle negative check is weak (skips plates that hit the icon); no Edit Mode bind-twice test. | T542 |

## T539 (build card)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T539 | #711 | #769 | `760c1d1` | PASS: Edit Mode 3043/3043, Play Mode 3320/3320, settings noise OK | Architect APPROVE_WITH_NOTES; UI/UX ACCEPT_WITH_NOTES |

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-41 | Architect 1 | `BuildCard.Refresh` rebuilds `RunSnapshots.Factory` on every quantity change, so it can allocate every frame while the card is open (SC-008). Cache the snapshot or refresh only when affordability or Cash display changes; measure. | T547 perf; T542 |
| C-42 | Architect 2 | `previewSource` returns `SlotGridView` as an interface; a destroyed view is not `null` through the interface. Add a Unity-object null or alive guard in `Reapply`/`ClearBuildPreview`. | T540 |
| C-43 | Architect 3 | `RunShellDocument.RebindCard` always activates the machine card panel; pick the panel from `buildCardOpen` (unreachable today). | T540 |
| C-44 | UI/UX 1 | Available and selected locations differ only in alpha; occupied and locked locations get no explicit mark from the card (FR-032 "mark"). | T543-C gate; T542 |
| C-45 | UI/UX 2 | "location N" has no number drawn in the world. Copy change or optional number badge. | T542 |
| C-46 | UI/UX 3 | At 200 % in portrait and 640x360 the type list may show 0–1 rows; no test for visible rows or scroll-to-selected. | T543-C gate; T542 |
| C-47 | UI/UX 4, 5 | The shortfall text appears up to three times. `BuildDust` is silently empty when the optional FX row is missing, with no static fallback outside reduced motion. | T542; T543-C gate |
| C-48 | UI/UX 6–8, QA | Record Cancel-left order; no test of the not-available state for the build card; warning/danger text contrast on the card not asserted; T555 A3 focus guard and A6 hold reset have no direct assertions. | T547; T543-C gate (contrast) |

## T540 (development stages and milestones)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T540 | #712 | #770 | `6a5f525` | PASS: Edit Mode 3119/3119, Play Mode 3351/3351, settings noise OK (first round 641c5a0 also PASS) | Architect APPROVE_WITH_NOTES; UI/UX + Art Director ACCEPT_WITH_NOTES (re-check 6a5f525: overlay acceptable as transient); Product Owner + Game Systems ACCEPT_WITH_NOTES |

Owner decision 2026-11-18: the stage is offset by the level-1 location count (research R15, data-model, tasks, world-presentation amended), so a new Iron run starts Early.

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-49 | UI/UX re-check, QA | Banner overlay vs OD-9b C1: at 200 % on 640x360, 844x390 and 360x640 the banner (up to ~29 % of the height) may cover a top-row selected machine or build ghost for 2.5 s; no test checks the selected machine rect. Mandatory device check; fallback fix = primary line only on compact height or large text (B-04 in accessible text and summary). Whether C6 (≤ 20 % chrome) covers transients is an owner reading. | T543-C gate (mandatory); T542 fix if covered; owner (C6 reading) |
| C-50 | UI/UX U3 | The banner has no live-region announce; TalkBack may never read it. | T543-C gate; T547 |
| C-51 | UI/UX U5, Architect 4 | A second milestone within 2.5 s replaces the first text; `ClearFeedback` is not called when reduced motion is switched on mid-reveal; check the animation clock across background and return. | T546; T542 |
| C-52 | Architect 2 | Banner pops before an open confirm dialog on Back (probably unreachable). Document or reorder. | T542 |
| C-53 | Architect 5, 6 | Bootstrap is 1862+ lines; extract a WorldFeedbackCoordinator. `DeckPlanner.Build` legacy undressed path is a footgun: make dressing mandatory in production callers or assert in the view. | T547; T542 |
| C-54 | UI/UX re-check | `PlaceMilestoneBanner` allocates a new array every tick while the banner shows. Hoist it. | T542 |
| C-55 | QA | No end-to-end Play Mode test of a real prestige banner after the re-baseline change, nor of a real offline return through `OfflineSummaryPanel`; the test-rebaseline DeckRenderTests row sits below the table (cosmetic). | T542; T543-C gate (offline return) |
| C-56 | PO, UI/UX, Art | B-01s "Level {n}. 1 new location opens." is provisional; World Designer to confirm. Stage-3 density on a full factory is 5 props (C-13), and the shuffled triptych trial must use the new Early start. Art Bible 21.12 still a draft (owner). | T547; T546; owner |

## T543 batch-C gate (reports `playtest-agent-f006-C.md`, `uiux-f006-C.md`, build 15deea2)

Owner decision 2026-11-18: fix before batch D through T558 (U-C1/C-F5 depot framing, C-F2 rotation re-fit, C-F1/U-C4 compact banner) and T559 (U-B13 periodic autosave). Refuted by UI/UX: C-F3 as a marker cap defect (per-machine pins are T528 slot badges; the attention marker cap holds) and C-F4 (the badge is not a tap target; the marker tap selects).

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-57 | UI/UX U-C2 (C-F3) | The store-full attention marker reuses the machine badge sprite and is much smaller than the per-machine badges; the shortage marker shows a generic arrow instead of the resource icon. | T542; owner |
| C-58 | UI/UX U-C3 (C-F4) | The large slot badge looks tappable but is not; nothing shows which pin is the tappable marker. | T542 |
| C-59 | UI/UX U-C5, U-C11, Play-Tester C-F8 | 640x360 at 200 %: build card Cancel/Confirm break mid-word, 3-line title, about 1.3 type rows, Confirm collides with the dev-build label; "Overdrive Ready" clipped at 360x640/200 %; "Research" breaks mid-word at 844x390. | T547; T542 |
| C-60 | UI/UX U-C6, U-C7, U-C8; Play-Tester C-F7, C-F10, C-F11 | Mine button overlaps lattice slots while a card is open; stale idle hint under the tablet build card; after Expand factory the first Back left Market open; a 9 s gap shows a full Welcome back modal; the world recentres about 120 px on build-card hand-over. | T547; T542 |
| C-61 | UI/UX U-C12, Play-Tester C-F6 | Hard-edged deck rectangle with about 13 % black above it. | Art Director; owner |
| C-62 | Both reports, not covered | TalkBack and accessible names (Android tree is one SurfaceView), Windows player, prestige banner, offline milestone line, build dust and hand-over on device, second shortage marker (rank 0 possibly off screen right), 150 % text. Build note: delete `Library/Bee/Android` before a gate APK build (stale Gradle intermediate gave a hanging 64 MB APK). | T542; final T543 gate |

## T558 (batch-C gate fixes)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T558 | #771 | #773 | `0cdc8a7` | PASS: Edit Mode 3127/3127, Play Mode 3439/3439, settings noise OK (first round 1758b0b also PASS) | Architect APPROVE_WITH_NOTES (fix-now C-F2 round trip done); UI/UX + Art Director ACCEPT_WITH_NOTES |

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-63 | UI/UX + AD F3, F4, F5 | Machine hit area at n = 3 (cell about 27 pt) against 48 dp; banner at exactly 20.0 % on 640 x 360 / 200 %; whether the hidden second banner line reaches the screen reader. Device checks. | final T543 gate; T542 |
| C-64 | Architect 2, QA 3, 4 | `FitBounds`/`ViewBounds` computed twice per frame (cache); `SlotGridTests` fit expectation mirrors the production tier formula; no test asserts B-04 in the offline summary; the lazily created `RunViewportDriver` would be stale if the camera controller were replaced. | T542; T547 |

## T559 (periodic autosave)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T559 | #772 | #774 | `7ee8d74` | PASS: Edit Mode 3161/3161, Play Mode 3445/3445, targeted persistence rerun green, settings noise OK (first round 4f5b5ef also PASS) | Software Architect (High tier, concurrency) APPROVE_WITH_NOTES (fix-now F1 done); Security/Privacy ACCEPT_WITH_NOTES |

As built: `AutosavePolicy` (30 s interval, 2 s spacing after a structural change, full-interval backoff after a failure) in the Application layer; `RunPersistence.AutosaveWhenDue` writes only while Running with no pending prestige; the main thread captures and serializes (about 0.12 ms on the dev host) and a pool thread publishes through the optional `IRunCheckpointBackgroundPromoter` under the store's operation gate (about 30 ms of file IO); one write in flight, every other persistence access joins it first; same Save V3 format and generation protocol. A kill without a pause callback restores the last periodic checkpoint and credits the absence once.

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-65 | Architect F2, QA | No IL2CPP/Android coverage of the pool thread: add a `[Category("StandalonePlayer")]` two-write case and confirm a periodic checkpoint in the F006 emulator journey (force-stop after more than 30 s). Main-thread cost measured on the Windows host only. | T542; T544 |
| C-66 | Architect F6, Security | Write rate during build bursts (up to about 30 writes per minute, five flushed files each) and flash wear (about 120 writes per hour while the run changes); measure frame time and IO on the emulator. The `mainMedian < 8 ms` assertion is wall-clock based. | T544 |
| C-67 | Architect F7, Security N3/N4 | The interval is a code default (`AutosavePolicy.DefaultIntervalMs`), not a run setting as the task text says; PO to confirm. The operation gate is held for about 30 ms of IO, so a command in that window waits. Diagnostics listeners can now run on a pool thread. Wiki `run-persistence.md` and `desktop-focus-lifecycle.md` ("no periodic autosave") are stale: update at the batch ingest. | owner (interval); wiki ingest; T547 |

## Batch 6 (P4): T541, T547, T531 and owner decisions 2026-11-19

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T541 | #713 | #778 | `b7282a2` | PASS: no-emulator validation of all five scenarios, negative cases, settings noise OK | DevOps lens in QA |
| T547 | #718 | #777 | `8289733` (two fix rounds on `2139397`) | PASS on re-QA 2: independent coverage scan, citations, v0.1 untouched | World Designer record (verdict CONFLICTS, 4 open: CF-2..CF-5, applied by T560) |
| T531 | #704 | #779 | `40ff48d` | PASS: Edit Mode 3163/3163, Play Mode 3450/3450, Windows player 82/82, settings noise OK | Performance ACCEPT_WITH_NOTES |

Owner decisions 2026-11-19 (through the coordinating session):
- T560 (#776) added: pre-T542 polish takes the code carries named for T542/T547; T542 only verifies. The T542 user32 real-input checklist on the Windows player is recorded as not covered.
- CF-1: World Bible `F006-WB v0.2` (MINOR): "Store" is the capacity term in player text, "Depot" the world structure; no string renamed.
- T560 behaviours accepted: Mine hidden while a phone card is open; one Back clears the banner and the sheet or card under it; milestone banners queue; no welcome-back modal under 60 s; dim tint marks occupied and locked locations while the build card is open (no sprite-mark art task).
- Budget 8 restated on the player method (F006 delta at most +12 passes over the F004-only 34; `camera.Render()` CPU tier 1 binding): `specs/004-visual-identity/reviews/performance-b3.md` section 12.
- Art Bible 21.12 confirmed with a worst-case ceiling of 110 renderers (v1.1.2).

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-68 | T531 Performance review 3 | Card-closed steady state allocates about 21 per frame, unattributed (about 100x the PR-2 floor). Deep profile. | Owner decision: separate Issue after the P4 gates |
| C-69 | T531 Performance review 4, 5, 7 | Report text: budget 4 was read on W003, not on W006-V; budget 9 is outside R16's list; probe with/without labels swapped (absolute values used); C-33 closed as within noise; optional C-20 shared factory snapshot and atlas-sorted sprite order. | T544 (final `performance-b4.md`) |
| C-70 | T541 QA | `F006Autosave` restore check can pass against an unchanged periodic file (require a newer generation or check the screenshot); every tap fraction is provisional; landscape zoom and card-column fractions unconfirmed; `-ZoomPrefTaps` from the Settings screenshot. | T542 |
| C-71 | T547 QA W-1..W-3 | Name FactoryCameraController, GridWorldMapper, StateCueSet and AttentionMarkerView in class D; the coverage extraction script is not committed; L-rows are term-swept only. | World Designer re-read after T560 |
| C-72 | T560 worker | `ProductionPerMinuteMicro` is the potential rate of active makers, not the effective rate: a starved smelter reads "Makers cannot keep up" (C-29). Semantics unchanged, pinned by a test. | Record |

## T560 (pre-T542 polish)

| Task | Issue | PR | Commit | QA | Reviews |
|---|---|---|---|---|---|
| T560 | #776 | #780 | `38ff7cc` (fix round on `137da8e`) | PASS: Edit Mode 3167/3167, Play Mode 3483/3483, Windows player 82/82, settings noise OK (first round 137da8e also passed: PM 3471) | Software Architect APPROVE_WITH_NOTES; UI/UX with Art Director ACCEPT_WITH_NOTES; World Designer re-read CONFORMS (Part 3) |

Done: CF-2..CF-5, B-4 (visible second cause line), B-6, C-28 (guard now covers the Details sheet), C-39 (LayoutId compare, 0.3 s safety net), C-41 for BuildCard and MachineCard (600-frame zero-allocation tests), C-54, C-64, C-51 (queue), C-52 (documented), C-44 (dim tint), C-47, C-57 store-full icon, B-1 audit (27 sites, all reachable), C-4, C-22, C-7, B-3, B-15, B-16, C-35, C-40, C-48, C-55 (offline return), doc fix. Owner-accepted behaviours are listed in the batch-6 section above.

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| C-73 | T560 partial | C-59: "Overdrive Ready" clip at 360x640/200 % and "Research" break at 844x390; C-60: ~120 px recentre on build-card hand-over; C-57: shortage marker generic arrow (no code path found). Device checks D1-D9 in the T560 UI/UX review. | T542 (device sweep); final T543 |
| C-74 | T560 not done | C-65 StandalonePlayer two-write autosave test (no player-compatible RunPersistence rig); C-53 `DeckPlanner.Build` undressed-path assert (first Refresh is legitimately undressed; needs bootstrap ordering); C-55 real prestige banner end-to-end test; B-13 duplicate PanelScaffold sheet (per-card `styleSheets.Add`) and B-14 shim (production callers remain). | Later task (refactor/test debt); C-65 also T544 device evidence |
| C-75 | UI/UX + Art Director N1, N3, N4 | Store-full marker draws the 24 px storage icon at 22 px (0.917x, non-integer; shimmer risk): author a 22/23 px marker sprite (art task). Dim tint 0.5 makes occupied plates almost `floor_1`; consider 0.6 after the device look. "it." widow in the side-panel hint and the four F004-B2 baselines. | Final T543 (D4, D5); later art task |
| C-76 | Architect 1d, 2b, 2c, 3c, 5c | Stale ShellNavigationStack docs; MachineCard cause and rates refresh only on state, shortfall or completion change (rate-only change lags); the card's CompletionsDelta written in a rebuild frame is lost (no production reader); no bootstrap-level cold-start test beyond T560's C-60 case; confirm `familyFpsCaps` 8 equals the old class default. | T542 notes; later |
