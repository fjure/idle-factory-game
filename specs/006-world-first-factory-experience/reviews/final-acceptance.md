# Final acceptance (Feature 006, T548, T549)

Issue #719 (T548) records the independent final reviews of Feature 006 World-First Factory Experience. The reviewed commit is `945b1557` (origin/main, 2026-11-26, after T544 #716 and the final T543 #715 merged). Ten read-only lens reviews were run by nine separate agents that implemented none of the work. Art Director and Technical Art share one agent as compatible lenses with separate sections. The parent assembled the sections verbatim. T549 (owner visual acceptance) appends its own section later. The T546 readability record (`readability-f006.md`) and the owner's timed trials run in parallel and are pending at the time of review.

## 1. Summary

| Lens | Verdict | Blocker | Major | Minor | Info | Conditions |
|---|---|---|---|---|---|---|
| Game Designer / Product Owner | ACCEPT WITH CONDITIONS | 0 | 0 | 7 | 6 | 5 |
| Software Architect | ACCEPT WITH CONDITIONS | 0 | 0 | 5 | 3 | 2 |
| Test/QA | ACCEPT WITH CONDITIONS | 0 | 2 | 7 | 3 | 4 |
| UI/UX Reviewer | ACCEPT WITH CONDITIONS | 0 | 0 | 4 | 4 | 3 |
| Art Director | ACCEPT WITH CONDITIONS | 0 | 1 | 3 | 4 | 4 |
| Technical Art | ACCEPT WITH CONDITIONS | 0 | 0 | 3 | 4 | 3 |
| World Designer | ACCEPT WITH CONDITIONS | 0 | 0 | 1 | 5 | 3 |
| Performance | ACCEPT WITH CONDITIONS | 0 | 2 | 5 | 3 | 4 |
| Security and Privacy | ACCEPT WITH CONDITIONS | 0 | 0 | 3 | 4 | 2 |
| Game Systems / Economy (Low tier) | ACCEPT WITH CONDITIONS | 0 | 0 | 3 | 4 | 3 |

No lens rejects F006, and no lens reports a blocker. The five Majors are PERF-1 and TESTQA-3 (SC-008 steady allocations), PERF-9 (no physical-device data), TESTQA-4 (SC-005 pending T546) and AD-1 (upgrade marker art). Section 3 records the owner's disposition for PERF-1, TESTQA-3, PERF-9 and AD-1. TESTQA-4 has no disposition: it closes when the T546 record exists (section 2, row 1).

UIUX-F3: the owner asked T548 to reproduce the T543 UI/UX F-3 report ("first world tap ignored after a cold start"). The UI/UX lens did not reproduce it: 0 of 12 taps were ignored, and 14 of 14 including two trial attempts selected. Under the owner's rule it is not filed.

Exception to read-only: for that reproduction only, the parent authorised the UI/UX lens to build one profiler-free development APK of `945b1557` and drive the AVD `IdleFactory_Test_API_36` via adb. The parent acquired the `unity,emulator` leases for owner `issue-719` before the spawn and released them after the lens returned. The lens changed no tracked file, and its worktree was left clean.

## 2. Conditions before feature completion

| # | Condition | Source | Owner of the step |
|---|---|---|---|
| 1 | `reviews/readability-f006.md` written with the SC-004, SC-005 (blind, P3, hub re-test, timed) and SC-006 results, and the owner's timed trials scored | TESTQA-4, GDPO, UI/UX C1, WD C1 | T546 (parent and owner) |
| 2 | T549 owner visual acceptance per surface and screen class, including the looks each lens lists and the GD/PO play-through (first upgrade, hand-delivered contract, stage change, real offline return) | GDPO C1, GDPO C4, UI/UX C2, TESTQA C4 | T549 (owner) |
| 3 | Owner dispositions recorded for SC-008, the missed guidance thresholds and AD-1 | PERF C1, TESTQA C2, GDPO C3, AD | done 2026-11-26, section 3 |
| 4 | Open carries kept in `reviews/p3b-followups.md` on main: C-77 to C-101, the T544 QA text findings, ARCH, UIUX, SECURITY, GS, TA and AD items marked "later" | ARCH C2, UI/UX C3, SECURITY C1, GDPO-12 | parent (coordination branch to main) |
| 5 | Final suite counts and the test manifest note (EM 3187, PM 3526, player 85; `final-test-names.txt` is the T542 manifest) | TESTQA C3 | parent, T550 |
| 6 | Post-P4 Issues to be created (none exist yet): C-68 deep profile plus a physical-device frame/battery/thermal measurement; a world-only outlined upgrade marker on the markers atlas (AD-1, TA-1); the deck edge/void art task (AD-4); the store-full marker scale and cap note (AD-7, TA-3, C-75) | PERF, AD, TA | parent after the gates |
| 7 | `check-settings-noise.ps1 -Commit` clean and the art-pipeline run at exit 0 on the final commit | SECURITY C2, TA C3 | parent, T550 |
| 8 | AD conditions 1 to 3: an Art Bible entry for the reuse of `F003-UI-ICON-UPGRADE` as a world marker, and a dated Art Director proof of the shipped marker over the T562 tiles. Both go with the post-P4 marker art task in row 6, per the owner's AD-1 disposition. Also a correction of the "dark outline" statement in `uiux-f006-final.md` section 5, carried to `p3b-followups.md` as a records fix. | AD C1 to C3 | parent (records), post-P4 art task |

## 3. Owner decisions referenced

| Date | Decision |
|---|---|
| 2026-11-25 | Upgrade marker hidden on boosted machines accepted (ART-2, C-98). |
| 2026-11-26 | Budget 8 uses the same-state F006 delta reading (+8.5 to +11.4, PASS). The fixed-34 reading (+12.5 / +14.7 at 12 markers) is recorded only. |
| 2026-11-26 | **SC-008 accepted as a deviation for F006**: `RunShellPresenter.Refresh` and the defined paths are asserted at zero allocations; the whole real-scene frame still allocates about 15 to 21 per steady frame (C-68, unattributed); emulator FPS budgets 1 to 3 are missed by every build on this host (environmental per the T544 ABA); no physical-device, battery or thermal data exists. A C-68 deep-profile Issue and a physical-device measurement follow after the gates. |
| 2026-11-26 | **AD-1 accepted for F006, art task after P4**: the reused `F003-UI-ICON-UPGRADE` glyph stays. It has no `ink_0` outline and no Art Bible entry. Its contrast would be 1.55 to 2.8:1 against plate_2, steel_2/3 and copper_2, but per AD-1 it is unproven whether those tones occur under the marker in the overlay band. A world-only outlined marker on the markers atlas follows as one post-P4 art and atlas task, which also removes the extra SetPass cost (TA-1). Parent evidence for the decision, not reported by a lens: in the parent-run T546 SC-005 P3 blind naming, the upgrade-marker tile was named correctly in all four filters for emuP, phoneP, phoneL and tabL. The two P3 misses were hub-dot tiles. The T546 record is still pending. |
| 2026-11-26 | **Guidance thresholds accepted as measured** (spec clarification: SC-001, SC-002, SC-003, SC-004, SC-006 and SC-010 thresholds are guidance only). Recorded misses from `baseline.md` T542: SC-003 upgrade 3 steps vs 2 and stopped-machine category 4 steps vs 0; SC-010 persistent labels 10 vs 6; unobscured share 44 to 51 % on phone portrait vs 50 % and 66 to 67 % on landscape vs 70 %; phone chrome at 200 % text 34 to 56 % vs 20 %. T549 judges them visually. |
| 2026-11-26 | Store-full markers per machine, uncapped, accepted for F006 (cap note after P4 with C-75); deck hard edge and black void become an art task after P4; the 44 px settle on card close is accepted as a Low note; the Auto-sell discovery hint, the offline summary copy and an Overdrive confirm go to the polish list after F006; the T543 F-3 first-tap report is filed only if reproduced (not reproduced, see section 1). |
| 2026-11-25 | C-68 deep profile becomes a separate Issue after the P4 gates. |

## 4. Lens reviews

### Game Designer / Product Owner — verdict: ACCEPT WITH CONDITIONS

Reviewed read-only at origin/main `945b15578d2a03fd2bb49de7ca40a962b1e2d633` (HEAD confirmed). I implemented none of this work. Product-level review only: I did not re-run suites, drive the emulator, or open trial keys/images. T546 `readability-f006.md` is pending (not yet written) and the owner timed trials are pending; both are stated as pending, not assumed.

#### Scope reviewed
- `spec.md` (US1–US8, FR-001–FR-047, SC-001–SC-013), `tasks.md` (T542–T549 text and checkbox state), `reviews/baseline.md` (T502 and T542 before/after), `reviews/final-test-evidence.md` (sections 1, 5, 9, 12, 13, T562 captures), `reviews/performance-b4.md` (B4b table), `reviews/playtest-agent-f006-final.md` (whole), `reviews/uiux-f006-final.md` (verdict, carry table, findings, C-98), `reviews/world-bible-conformance.md` (overall verdict, Part 3 line), `reviews/world-copy.md`, `reviews/art-direction-f006.md` (verdict matrix), `reviews/p3b-followups.md` (whole, ends at C-76), the parent's owner-decision list in the T548 brief.
- Not read by design: other lenses' files, `t562-keys/`, `t546/`.

#### FR/SC coverage (Game Designer / PO lens)
| FR/SC | Evidence | Status |
|---|---|---|
| FR-001, FR-003 (no economy/save/persisted-presentation change) | Play-Tester Channel A: strategy baseline diff has no economy value change; final-test-evidence section 4: no SaveV1/V2/V3 or Balance fixture changed, balance trace unchanged | Met |
| FR-002, FR-025, FR-029 (cues reflect authoritative state, completion signal) | Play-Tester sections 2.2, 2.3; UI/UX F-6; no contradicting finding | Met (device-observed on 1080x2400 only) |
| FR-004–FR-006 (F004 rules, classes, allocations) | performance-b4 rows 5–11 PASS; rows 1–3 emulator FPS not met (environmental, owner decision) | Met with a recorded gap (GDPO-9) |
| FR-007/SC-001 factory dominance (guidance) | baseline.md T542: world >= 60 % on 6 of 10 classes at 100 %; phone classes at 200 % chrome 34–56 % vs 20 % guidance | Partly met (guidance), GDPO-2 |
| FR-008, FR-008a, FR-010, FR-010a (persistent layer, Mine, vein) | UI/UX and Play-Tester captures (Mine hold, strip chip, Office, Overdrive); B-8/B-10 behaviours owner-accepted | Met |
| FR-009/SC-010 less text (guidance) | baseline.md: labels 13 to 10 (77 %) against 6 (50 %) on every class | Not met (guidance), GDPO-3 |
| FR-011–FR-015 (continuous facility, depot, no inter-machine links, decoration) | world-bible-conformance CONFORMS (Part 3), art-direction matrix; F-2 hard-edged deck | Met; first-look edge deferred (GDPO-8) |
| FR-016–FR-019 (card, camera, details, removal) | Play-Tester 2.3, 2.5 (D7, 0 px); F-4 44 px settle accepted; SC-002 in baseline | Met |
| FR-020–FR-024 (hub, Back, sheets, shared system) | Play-Tester 2.4, 2.6 (every row within 2 taps); UI/UX C-86 PASS on every sheet, portrait and landscape; real Back bug C-85 fixed | Met |
| FR-026, FR-028 (shared cause, top shortage) | resource-icon marker and tap (Play-Tester 2.3, UI/UX C-88) | Met |
| FR-027 (upgrade available) | T563: lifecycle 2.2, per-class C-98 UI/UX section 5; hidden on boosted machines accepted | Met with accepted residual (GDPO-10) |
| FR-030, FR-031 (numbers on demand; shape-only distinguishability) | SC-005 blind naming 22/22 and P3 110/112 (parent; one miss re-tested 16/16) | Met; T546 record pending |
| FR-032–FR-034 (build flow) | Play-Tester 2.1, 2.5: build in 2 taps, preview, shortfall; UI/UX C-86 build card at 640x360/200 % and tablet PASS | Met |
| FR-035 (milestones) | Play-Tester 2.6 stage-change banner within 0.7 s, gone by +6 s; F-3 covers top-row markers at 100 % | Met (Low residual on compact classes) |
| FR-036/SC-006 (progress visible) | parent ordering 10/10 Windows set and 10/10 emuP set; Play-Tester k01 Partial stage seen; owner meaningfulness confirmation pending | Met blind; owner confirm pending |
| FR-037–FR-039/SC-011 (not bound to 12) | final-test-evidence section 8, UI/UX n=2, n=3, 12-machine grids | Met |
| FR-040–FR-041, FR-047/SC-012a, SC-013 | direction decision recorded earlier; WB v0.1 frozen, v0.2 (CF-1); conformance CONFORMS after T560 | Met |
| FR-042 (before/after sheets, owner verdict per surface), SC-012b | T549 not started; device capture folder holds start/card sets only; sheets for details, building, hub, system view, offline summary, reduced motion, triptych not yet assembled | Pending (owner) |
| FR-043/SC-004/SC-005 timed | parent-run blind parts done; owner timed trials SC-004 (Windows + emuP) and SC-005 timed (standard + reduced-motion) pending; `readability-f006.md` not written | Pending |
| FR-044 (asset pipeline) | art-direction-f006 verdicts; art-pipeline PASS in final-test-evidence row 4 | Met |
| FR-045 (Play-Tester + UI/UX per batch, owner reviews) | `playtest-agent-f006-{A,B,C,final}.md`, `uiux-f006-{A,B,C,final}.md`; final report proposes go with notes | Met; owner has the reports |
| FR-046 (baselines measured first) | `baseline.md` c568d24 probes, T542 re-run | Met |
| SC-002 machine stays visible (guidance) | machine 100 % visible on every class; unobscured: portrait phones 44–51 % (50 % met only at 360x640/200 %), landscape phones 66–67 % at 100 % (70 %) | Partly met (guidance), GDPO-2 |
| SC-003 fewer steps (guidance) | upgrade 3 (guidance 2); one stopped machine 1; every-stopped-machine category 4 counted steps (guidance 0); reach 2, return 1 per the counted evidence string | Partly met (guidance), GDPO-1 |
| SC-007 no functional regression | EM 3187, PM 3526, player 85, art-pipeline green; fixtures/trace unchanged | Met |
| SC-008 performance | allocations 0, memory 3.54 MB, SetPass restated, shader variants 356, APK 44.6 MiB PASS; emulator FPS ~45 on any build (owner: environmental) | Met on R16 list; device gap open (GDPO-9) |
| SC-009 accessible on every class | final-test-evidence section 8 and UI/UX; TalkBack not coverable (R-7); keyboard focus not device-verified (Play Mode tests) | Met in tests; device gaps recorded |

#### Findings
| ID | Severity | Evidence | Statement |
|---|---|---|---|
| GDPO-1 | Minor | baseline.md T542 SC-003 table | SC-003 guidance is missed on two journeys: upgrade and see the factory again stays at 3 (guidance 2), and "learn the category of every stopped machine" is 4 counted steps (0-tap guidance; the journey counts card reads, while the badges answer it in 0 taps and SC-004 is the real test). Guidance, not a gate, but no owner disposition is recorded. |
| GDPO-2 | Minor | baseline.md SC-001/SC-002 | World share and unobscured share miss guidance on phones: portrait 44–51 % unobscured vs 50 %, landscape 66–67 % at 100 % vs 70 %, phone chrome at 200 % text 34–56 % vs 20 %. T537 B-8 (landscape phones hide both columns with a card open) is documented as built. Needs the owner's reading in T549, not a fix. |
| GDPO-3 | Minor | baseline.md SC-010 | Persistent labels 13 to 10 on every class against a target of 6 (guidance 50 %). The intent (less text) is delivered in part (six tab labels gone, no information lost). No owner disposition for the miss is recorded yet. |
| GDPO-4 | Minor | Play-Tester 2.1, F-7; owner list | World-first path: the first upgrade at about 2–3 minutes needs Auto-sell or a manual Sell, found by a long Market scroll; Mine fills the store but gives no Cash by itself. Owner deferred the Auto-sell discovery hint to later polish. Residual product risk: a new player can stall before the first upgrade. Accepted deferral; confirm with a first-time play in T549. |
| GDPO-5 | Minor (evidence gap) | Play-Tester 2.6 | The first contract was never hand-delivered on the final build: a fresh run cannot reach it in a session, and in the W005-L set Accept was tested but delivery came from offline auto-delivery. After Accept the sheet scrolls so the active contract is half out of view with focus on a disabled button (F-5, Low). This is the first system-driven goal and the only part of the world-first path no agent played by hand. |
| GDPO-6 | Minor (evidence gap) | Play-Tester 2.6, section 4 | Offline return was verified only for a synthetic 8 h credit (full "Welcome back" text of about 25 lines, F-8) and a real 20 s gap (no modal, accepted). No real 60 s to 8 h return was played. Owner deferred the summary copy to polish. |
| GDPO-7 | Info | tasks.md T546 and T549; brief | Pending, not defects: owner timed SC-004 (Windows + emuP), SC-005 timed (standard + reduced-motion), `readability-f006.md`, the owner SC-006 meaningfulness confirm, T549 before/after sheets and per-surface owner verdicts (FR-042, SC-012). The device capture folder holds start and card sheets per class; sheets for details, building, hub, system view, offline summary, reduced motion and the triptych still have to be assembled for T549. |
| GDPO-8 | Minor | Play-Tester F-2, spec FR-011 | The deck ends in a hard-edged rectangle with a black void above and below at fit-all in every stage, so the first screen a player sees carries it. Owner decision: art task after P4; conformance unaffected. Recorded as the most visible polish gap for the owner's veto at T549, not re-filed. |
| GDPO-9 | Info | performance-b4 rows 1–3 | Emulator FPS budgets 1–3 are not met by any build on this host (about 45 fps, B4a build included); owner decision: environmental. There is no physical-device frame-rate evidence in F006, so the world-first feel at 60 fps is unproven on a real phone. Windows frame p95 and 0 steady-state allocations are in budget. C-68 (about 21 unattributed allocations per frame, card closed) goes to a separate Issue after the gates. |
| GDPO-10 | Info | Play-Tester 2.2, UI/UX F-4 | The upgrade marker is hidden on boosted machines (owner-accepted, C-98) and the Overdrive hub row activates in one tap with no confirm (owner: polish after F006). Together, an accidental Overdrive makes all upgrade markers vanish. Accepted risk, noted. |
| GDPO-11 | Info | Play-Tester F-1, F-3, F-4; owner list | Owner-accepted and not re-filed: store-full markers per machine uncapped (cap note after P4, C-75), banner covering marker tops at 100 % text on 1080x2400, 44 px settle on card close. |
| GDPO-12 | Info | `reviews/p3b-followups.md` (ends at C-76); `final-test-evidence.md` lines 211–281; `tasks.md` T542–T544 | At the reviewed commit the carry ledger stops at C-76; carries C-77 to C-98 live only in `final-test-evidence.md` and `uiux-f006-final.md`, and C-99 to C-101 are recorded on the coordination branch (121740df), not on main. T542–T544 checkboxes are still open on main (derived; the parent mirrors them). The T549 record must cite the complete carry set. |
| GDPO-13 | Info | uiux-f006-final F-2, F-3, F-1 | Minor residuals: tablet hint widow "it.", unconfirmed first-tap-ignored 2 of 6 about 20 s after cold start (not reproduced), dev-build label over Confirm at 640x360/200 % (dev builds only). None changes the product verdict. |

Product reasoning: the shipped F006 delivers the promise of US1–US7. The factory dominates on the classes that matter most, machines are inspected and acted on in place with a one-tap cause, every system is within 2 taps and one Back, building takes 2 taps, the upgrade, attention and shortage markers behave per the Play-Tester's device checks, stages and milestones are legible, and world copy conforms to the frozen World Bible. Functional and economy gates are clean and no economy value changed. What is not delivered is mostly guidance-level (GDPO-1 to 3) and owner-accepted polish (GDPO-8, 10, 11). What remains is owner work, not engineering (GDPO-7, T549).

#### Conditions for acceptance
1. Owner records T549 acceptance per surface and screen class with before/after sheets for no panel, card, details, building, hub, system view, offline summary, reduced motion, 200 % text and the stage triptych (FR-042, SC-012). Check: `final-acceptance.md` holds an owner verdict row for each.
2. Owner timed trials are completed and scored: SC-004 on the Windows and emuP sets (9 of 10 within 3.0 s and 5.0 s) and SC-005 timed (10/10 within 2.0 s, median no worse than 1.077 s), standard and reduced-motion, with results in `readability-f006.md`. Check: the file exists with the four sets scored; a miss returns to the owner as a decision, not a waiver.
3. The T549 record states the guidance misses as owner-accepted or deferred, with the figures: SC-003 (upgrade 3 vs 2; 4 steps vs 0), SC-010 (10 vs 6 labels), SC-001/SC-002 phone chrome and unobscured shares, the A-9 limitation, and the physical-device FPS gap. Check: each figure appears with a disposition.
4. The owner plays a first-time path on a real phone or the emulator that includes the first upgrade, a hand-delivered first contract, a stage change and a real 60 s to 8 h offline return, since no agent covered these (GDPO-4, 5, 6). Check: a short owner note per step in the T549 record.
5. The T549 record cites the complete carry set (C-77 to C-101 and C-68) and T542–T544 are ticked through the parent mirror. Check: carries listed with owner and target; checkboxes match Issues.

#### Counts
Blocker 0 / Major 0 / Minor 7 (GDPO-1, 2, 3, 4, 5, 6, 8) / Info 6 (GDPO-7, 9, 10, 11, 12, 13) / Conditions 5.

### Software Architect — verdict: ACCEPT WITH CONDITIONS

#### Scope reviewed

Reviewed commit `945b15578d2a03fd2bb49de7ca40a962b1e2d633` (HEAD of the read-only worktree confirmed). Read-only; no suites re-run.

- Contracts: `contracts/presentation-model.md` (section 2 `UpgradeAvailable` rule), `data-model.md` (DisplayPreferences v2, R20), `plan.md` (persistence and layering rows).
- Code: `Unity/Presentation/CameraScaleModel.cs` (`OpenSlot`, `KeepCurrentViewLegal`, `OverrunWorld`, `Constrain`, `PanSlotIntoBand`), `Unity/UI/RunShellDocument.cs` (`EscapePressedThisFrame`, `Update`), `Unity/UI/RunShellPresenter.cs` (`RenderFactory`, `AffordableUpgradeMask`, cash-driven invalidation), `Unity/Presentation/World/SlotVisualModel.cs`, `MachinePresentationModel.cs`, `WorldEffectsFeed.cs`, `SlotRenderers.cs`, `Unity/Persistence/DisplayPreferencesStore.cs`, asmdefs (`IdleFactory.Core`, `IdleFactory.Unity`).
- Tests: `PlayMode/F006/HubContractsBackSceneTests.cs`, `PlayMode/F006/UpgradeMarkerSceneTests.cs`, `EditMode/F006/UpgradeMarkerTests.cs`.
- Carries C-87, C-95, C-96 read from `p3b-followups.md` on the coordination branch `feat-006-maqa-batch5` (the reviewed commit's copy of the file stops at C-76). Commit `55a8e48f` (T561 fix round) for the C-87 history.
- Not reviewed (other lenses or pending): UI/UX, performance, readability record T546.

#### FR/SC coverage (Architect lens)

| FR/SC | Evidence | Status |
|---|---|---|
| Boundaries: domain and simulation in `IdleFactory.Core` | Core asmdef has `noEngineReferences: true` and no references; no `using UnityEngine` anywhere under `Assets/IdleFactory/Core`. | PASS |
| Presentation depends inward only | `IdleFactory.Unity` references `IdleFactory.Core` and `Unity.InputSystem` only; no reverse reference. | PASS |
| MonoBehaviours delegate decisions | `RunShellDocument.Update` only detects Escape and calls `HandleBack`; marker rule, camera band logic and affordability live in plain classes (`SlotGridModel`, `CameraScaleModel`, `MachinePresentationModel`). | PASS |
| FR-027 upgrade marker; contract `UpgradeAvailable = Available && Affordable` | Contract rule at `MachinePresentationModel.cs:324`; the shipped draw path is `SlotVisual.UpgradeMarker` -> `SlotRenderers.cs:160`. Duplicate rule, parity-tested (ARCH-4). | PASS with notes |
| DisplayPreferences v2 shape and read order (data-model R20) | `DisplayPreferencesStore.ReadFiles`: v2 present -> strict parse, invalid -> defaults, never falls back to v1; v2 absent -> v1 parser with `ShowZoomButtons=false`; neither -> defaults without diagnostic. Write: `display-v2.json.tmp` plus atomic replace with bounded retry, v1 left in place, 4096-byte read cap. Matches the contract. | PASS |
| Mutable state ownership | Camera sheet state (`sheetPriorScale`, `sheetRelaxWorld`, `sheetTouchedByPlayer`) is owned by `CameraScaleModel` and reset in `CloseSheet`; the presenter owns `affordableUpgradeMask` and `snapshot`; the feed owns its own model, pool and `factory` snapshot. No shared mutable aliasing found. | PASS |
| Back handling (R11, one back stack) | `EscapePressedThisFrame` scans all Keyboard devices without allocation; `HandleBack` is idempotent per frame. | PASS with notes (ARCH-2, ARCH-3) |
| `ui-surfaces-f006.md` versus code | Sampled the hub, back and sheet rules only (surface geometry belongs to the UI/UX lens). No divergence found in the sampled paths. | PASS (sampled) |

#### Findings

| ID | Severity | Evidence | Statement |
|---|---|---|---|
| ARCH-1 (C-87a) | Minor | `CameraScaleModel.cs:575-590, 663-683, 391-412`; tests added in `55a8e48f` | The hand-over fix `KeepCurrentViewLegal` grows the sheet relaxation by `OverrunWorld`, and `OverrunWorld` skips an axis whose limits are degenerate (`lowX > highX`, `lowY > highY`). `Constrain` instead pins a degenerate axis to the fit-extent centre (`ClampAxis`). So if the band grows at the hand-over and an axis turns degenerate, the view stays off-centre, no relaxation is added, and the next `Pan` or layout change snaps that axis to centre: a jump that can move the slot out of the band. This is the untested case in C-87. The new tests assert slot-in-band and `Pan(Vector2.zero) == false` only for the measured 1080x2400 non-degenerate case. Narrow (needs a short content axis plus a band change); no player-visible occurrence reported on the device runs. **Not a blocker; later polish.** Fix: treat a degenerate axis as "overrun = distance to the fit centre" in `OverrunWorld`, or re-run `Constrain` for that axis at the hand-over, plus one EditMode test with a degenerate axis. |
| ARCH-2 (C-87b) | Info | `RunShellDocument.cs:3194-3205` | The multi-keyboard assumption (any Keyboard device's Escape is Android Back) is documented only in the XML comment of `EscapePressedThisFrame` and in the C-85 test comment. It is sound for a touch-first Android build and a Windows dev build; a second physical keyboard's Escape on desktop also acts as Back, which is the desired result. **Not a blocker.** Optionally mirror the one-line assumption in `ui-surfaces-f006.md` under Back. |
| ARCH-3 (C-87c, d) | Minor | `HubContractsBackSceneTests.cs:68-101` | `BackOnAKeyboardThatIsNotTheCurrentOneStillClosesTheContractsSheet` is probably not discriminating. `Press(other.escapeKey)` queues a state event on `other`; in the Input System a device that receives non-noise state becomes `Keyboard.current`, so the old `Keyboard.current` polling would see the press too, and the test cannot fail against the pre-fix code (reasoned from the Input System behaviour, not executed). The first test calls `HandleBack()` directly and does not exercise polling. The evidence for the fix is the emulator measurement in `final-test-evidence.md` section 12, not this test. The optional zero-allocation test for `EscapePressedThisFrame` does not exist. **Not a blocker for F006; later polish.** Make the test keep `current` current (queue a state event on `current` in the same update after pressing on `other`), or call `EscapePressedThisFrame` directly with a two-keyboard setup, and add a `GC.GetAllocatedBytesForCurrentThread` assertion. |
| ARCH-4 (C-96) | Minor | `SlotVisualModel.cs:156-160`, `MachinePresentationModel.cs:324`, `UpgradeMarkerTests.cs:62-80` | The affordability rule exists twice. `SlotGridModel.Describe` uses `Available && RejectionReason != ValueLimit && Affordable`; `MachinePresentationModel.Refresh` uses `Available && Affordable` (no ValueLimit exclusion). The parity test varies blocker, phase and active only, always with available and affordable true and no rejection reason, so the two rules differ on the `ValueLimit` branch and nothing pins that. Impact today: none visible. The production marker draws from `SlotVisual.UpgradeMarker` only; `MachinePresentation.UpgradeAvailable` and `UpgradeMarkerVisible` have no production consumer (only the model and tests). `WorldEffectsFeed`'s `UpgradeAvailable` staleness (the feed rebuilds its snapshot on machine identity, location count or structure revision, never on cash) is therefore latent; it becomes a defect only if a world-layer consumer is added without a cash trigger. **Not a blocker; later polish.** Fix: one shared helper for the rule called by both, and either drop `UpgradeAvailable` from the world model or document it as snapshot-time. |
| ARCH-5 (C-95a) | Minor | `UpgradeMarkerSceneTests.cs:177-215` | The test name `TheShippedSceneShowsTheMarkerWhenTheCashPaysForTheUpgradeAndHidesItWhenTheCashIsSpent` overclaims: the body asserts the appear direction only (cash added, marker on) and ends without spending cash. The disappear direction is covered only through the model (`AnUnaffordableOrMissingUpgradeNeverShowsTheMarker`) and by the mask comparison being a symmetric `!=`. Low risk, but the name is a false evidence claim. Cheap fix; condition C1. |
| ARCH-6 (C-95b) | Minor | `RunShellPresenter.cs:203-206, 418-433`; `SlotVisualModel.cs:165` | `UpgradeCost` is set for every machine with an upgrade, regardless of the marker rule, so `AffordableUpgradeMask` also counts machines whose marker a problem badge hides. Cash crossing such a cost flips the mask and sets `factoryDirty`, rebuilding the factory snapshot and grid with no visible change. Cost: one extra rebuild per crossing, bounded by 12 locations and by cash crossings (not per frame); no correctness problem, and it errs on the safe side (never misses a visible change). **Not a blocker; later polish** (include a location in the mask only when its marker can show, keeping the Boosted case that the draw path handles at `SlotRenderers.cs:160`). Residual risk is performance only; the performance lens owns whether it shows in B4b. |
| ARCH-7 | Info | `DisplayPreferencesStore.cs:1-6` | `Persistence` uses `IdleFactory.Unity.Bootstrap` (`IReducedMotionSignal`) and `UnityEngine`: the persistence namespace depends on the bootstrap namespace inside one assembly. Not an assembly-rule violation, but the signal interface would sit better in a shared namespace. No action for F006. |
| ARCH-8 | Info | `IdleFactory.Unity.asmdef`; repository layout | The responsibility split is realised by two assemblies (`Core` engine-free, `Unity` with Presentation, UI, Persistence and Bootstrap namespaces) rather than five folders; the engine-free boundary the constitution cares about holds. The separation inside `IdleFactory.Unity` is by namespace only and not compiler-enforced. Existing structure, not an F006 regression. |

#### Conditions for acceptance

- C1 (cheap, objectively checkable): `TheShippedSceneShowsTheMarkerWhenTheCashPaysForTheUpgradeAndHidesItWhenTheCashIsSpent` (`UpgradeMarkerSceneTests.cs:178`) either asserts the hide direction in the shipped scene, or is renamed to the appear-only claim it proves, or the final record states that the hide direction is covered by the model tests only. May land in the T549 polish round.
- C2 (record keeping): `p3b-followups.md` keeps C-87, C-95 and C-96 open as later polish after F006 (ARCH-1, ARCH-3, ARCH-4, ARCH-6), and notes the C-87 test-discrimination gap so no later record cites the non-current-keyboard test as proof of the polling fix.

Carry answers (blocker or later polish):

- C-87: later polish, none a blocker. Degenerate axis at the hand-over: real but narrow latent jump (ARCH-1). Multi-keyboard assumption: documented in code, acceptable (ARCH-2). `HubContractsBackSceneTests` does not discriminate the old polling and the optional zero-alloc test is absent (ARCH-3); the fix itself was verified on the emulator.
- C-95: later polish. The missing disappear assertion is an overclaiming test name (condition C1); the mask over-counting is a bounded extra rebuild with no visible effect (ARCH-5, ARCH-6).
- C-96: later polish. Duplicate rule with a parity test that misses the `ValueLimit` branch; the snapshot-stale `WorldEffectsFeed` value has no production consumer, so no player-visible defect (ARCH-4).

#### Counts

Blocker 0, Major 0, Minor 5, Info 3, Conditions 2.

### Test/QA (risk-based matrix) — verdict: ACCEPT WITH CONDITIONS

Reviewer: independent Test/QA, Medium tier (Claude sonnet), read-only, no suite re-runs. HEAD of the review worktree confirmed `945b15578d2a03fd2bb49de7ca40a962b1e2d633`. I implemented none of this work.

#### Scope reviewed

- `spec.md` (FR-001 to FR-047, SC-001 to SC-013), `tasks.md` requirement coverage table and T542 to T549 text.
- `reviews/final-test-evidence.md` (sections 1 to 13 and the T562 block), `reviews/final-test-names.txt` (6,652 lines: Edit Mode 3,167 and Play Mode 3,483 names, every line `Passed`), `reviews/performance-b4.md` (B4b), `reviews/baseline.md` (T542 section), `reviews/p3b-followups.md` (C-27 to C-76), `reviews/playtest-agent-f006-final.md`, `reviews/uiux-f006-final.md`, `reviews/world-bible-conformance.md` (verdict lines).
- Code at the reviewed commit, targeted reads only: `Assets/IdleFactory/Tests/PlayMode/F006/W006VRealScenePlayerTests.cs`, `W006VWorldPlayerTests.cs`, `HubContractsBackSceneTests.cs`, `RunShellPresenterAllocationTests.cs`, `AutosavePoolThreadPlayerTests.cs`, `Assets/IdleFactory/Tests/EditMode/Run/AutosaveWriteCostTests.cs`.
- Git ancestry: T562 (`31a18f66`) and T544 tests (`19901958`) are in the T544 QA commit `1e72e26b`; `git diff 1e72e26b HEAD -- Assets Packages ProjectSettings scripts` is empty, so the T544 QA result (EM 3187, PM 3526, player 85) covers the reviewed content (T543 was records-only).
- Not read, by brief: other lenses' files, `t562-keys/`, `t546/`. `reviews/readability-f006.md` (T546) does not exist yet: pending, not blocking this review.

**Completion gate (AGENTS.md) check.** Edit Mode, Play Mode (not 0 tests), tagged Windows player and art pipeline all passed on this content (T542 3167/3483/82 and art pipeline PASS; T561 3175/3516/82; T563 3187/3522/83; T544 3187/3526/85 on the final test code; T562 art pipeline 378 passed). Console: no `error CS`, `LogScan` PASS in every F006 journey. Settings noise checked per commit. QA PASS on committed results is recorded per Issue in `p3b-followups.md`. Open gate items: T546 readability record, owner timed trials, T549 owner acceptance, and the `after_implement` hooks (parent-owned, not part of this review). `tasks.md` still shows T542, T543, T544 unticked; the checkboxes are derived by `scripts/maqa-sync-task-checkboxes.ps1` before the hooks, so this is expected (Info).

#### FR/SC coverage (Test/QA lens)

Test classes are named as in `reviews/final-test-names.txt` (EM = Edit Mode, PM = Play Mode, PP = tagged Windows player). "Journey" = `scripts/android-emulator-journey.ps1` run recorded in `final-test-evidence.md`. T561 to T563 and T544 added tests that the manifest does not list (see TESTQA-1).

| FR/SC | Evidence | Status |
|---|---|---|
| FR-001, SC-007 (no economy, content, save change) | EM `BalanceTraceTests`, `BalanceTraceFullTests`, `SaveV2/V3 *` fixtures, `PublicApiSnapshotTests`, `DeviceSetShippedCatalogTests`; `git diff --stat c568d24 HEAD` over Fixtures/Balance empty (evidence 4); Play-Tester channel A diff shows no economy value changed | Covered |
| FR-002, FR-029, FR-030 (state honesty, completion signal, no numbers in world) | EM `CompletedCyclesLifecycleTests`, `CompletedCyclesNoObserverEffectTests`, `CompletedCyclesPropertyTests`, `MachinePresentationModelTests`, `EffectPoolTests`; PM `StateCueOverlayTests`, `AttentionDisplayTests` | Covered |
| FR-003 (presentation state not persisted; prefs in local store) | EM `DisplayPreferencesV2Tests`, `DisplayPreferencesStoreTests`, byte-identical SaveV3 fixtures; no test directly asserting that selection, camera or card state is absent from the save | Indirect (TESTQA-9) |
| FR-004, FR-044 (Art Bible, pipeline) | EM `F006AssetContractTests`, `F004AssetContractTests`, `AtlasPageBudgetTests`, `PresentationSpriteCatalogTests`; `scripts/test-art-pipeline.ps1` PASS (378); art-direction and world-bible records | Covered |
| FR-005, SC-009 (classes, 100 to 200 % text, 44 pt, reduced motion, keyboard) | PM `RunTextScaleTests` (150), `RunHudLayoutTests` (238), `TextScaleLayoutTests`, `PanelTitleWrapTests`, `KeyboardTraversalTests`, `RunKeyboardFocusTests`, `PanelScaffoldTests`, `RunDeviceProfileTests`, `ReducedMotionTests`; PP `ReducedMotion`, `ReducedMotionWorld`; device 48 dp case `OnTheDeviceEveryCardControlIsAtLeastFortyEightDpInDevicePixels` (T561) | Covered in Play Mode. Device accessibility tree, TalkBack, keyboard on device not reachable (TESTQA-6) |
| FR-006, SC-008 (allocation, budgets) | EM `AtlasPageBudgetTests`; PM `PresentationAllocationTests`, `RunShellPresenterAllocationTests`; PP `F006Allocation` (7), `W006VWorldPlayerTests`, `W006VRealScenePlayerTests`; `performance-b4.md` | Partial: asserted for defined paths and `Refresh`; whole real-scene frame about 20 allocations per steady frame (C-68, open); SetPass, frame p95, texture MB are logged, not asserted (TESTQA-2, TESTQA-3) |
| FR-007 to FR-010, SC-001, SC-010 | PM `PersistentLayerTests` (148), `RunHudLayoutTests`, `BaselineMatrixTests` (10, probe), `OverdriveControlTests`, `OreVeinPanelTests`; `baseline.md` T542 tables | Measured; guidance not met on phones (world 54 to 72 %, chrome above 20 % at 200 %, labels 13 to 10 against target 6). Guidance only per spec clarification; recorded for T549 |
| FR-008a (zoom buttons off on touch) | EM `DisplayPreferencesV2Tests` (`showZoomButtons`); PM `SettingsSheetTests`; journey Zoom step is INFO only (preference below the fold, `-ZoomPrefTaps` not supplied) | Covered in tests; not on device |
| FR-010a (ore-vein marker) | PM `OreVeinPanelTests` (75), `OreVeinRealPointerHoldTests`, `DepositViewTests`; PP `W006VWorldPlayerTests` (vein loop); `performance-b4.md` real-scene vein | Covered |
| FR-011 to FR-015 | EM `DeckLayoutTests`, `DeckWiringTests`, `DeckCompositeProofTests`, `DepotLinkModelTests`, `SortingLayerGuardTests`; PM `DeckRenderTests`, `DepotLinkViewTests`, `StageDressingViewTests`, `BuildCardWorldTests`; `t527-composite-proofs.md`; PP `ReducedMotionWorld` | Covered; marker keep-out on device not run (TESTQA-7) |
| FR-016 to FR-019 | PM `MachineCardTests` (109), `MachineDetailsParityTests` (incl. `RemoveIsNeverOnTheCardAndNeedsDetailsAndTheConfirmation`), `CardCameraTests` (48), `MachinePanelTests`, `BuildPricePanelTests`; EM `CameraScaleModelTests` (37, incl. T561 band-grow cases); PM T561 hand-over cases | Covered. T561 recentre fix proven by temporary revert (cases fail on old code) |
| FR-020 to FR-022, FR-024 | PM `HubPanelTests` (78), `BackOrderTests`, `HubContractsBackSceneTests`, `NavigationInputTests`, `SurfaceConsistencyTests`; EM `ShellNavigationStackTests`; journey `HubContracts` (real Back state check, T561) | Covered; device Back on every sheet PASS (UI/UX final C-86) |
| FR-023 | PM `SurfaceConsistencyTests` (87), `PanelScaffoldTests`, `OfflineSummaryPanelTests`, `StartupStatusPanelTests` | Covered |
| FR-025, FR-026, FR-028 | EM `AttentionModelTests` (42), `MissingInputCauseTests`, `StateCueSetTests`; PM `AttentionDisplayTests`, `AttentionMarkerSceneTests`, `StateCueOverlayTests`; device marker tap at 0 and 40 px (T561, UI/UX final C-88) | Covered |
| FR-027 (upgrade marker) | T563 tests (3 new EM groups, 2 real-scene PM cases; not in the manifest), `W006VWorldPlayerTests` worst-case census; device screenshot `t563/device/marker-affordable-and-problem.png`; T562 per-class tile `cueShown` | Covered; owner-accepted limits C-98 and hidden-on-boosted |
| FR-031 | EM `StateCueSetTests`, `StageTriptychDeltaTests` (grayscale at n-min); T545/T562 blind sheets (grayscale and three CVD) scored by the parent: SC-005 blind 22/22 per set, P3 tiles 110/112 (one miss re-tested 16/16) | Covered in the pipeline; final readability record pending (TESTQA-4) |
| FR-032, FR-033 | PM `BuildCardTests` (86), `BuildSheetPanelTests`, `BuildPricePanelTests`; journey `F006Build` PASS on the `empty-location` set (T561); EM `BuildPricingTests` | Covered; insufficient-Cash build card and desktop build card not captured on device (UI/UX final) |
| FR-034 | PM `BuildCardTests` (dust, static highlight), `ReducedMotionTests`, `EffectPoolTests` (EM) | Covered in tests; dust not seen in 1 s and 3 s device captures (T542) |
| FR-035, FR-036, SC-006 | EM `MilestoneWatcherTests` (19), `DevelopmentStageTests` (21), `StageTriptychDeltaTests` (36); PM `MilestoneBannerTests` (21), `StageWiringSceneTests`, `StageDressingViewTests`; Play-Tester final banner and Partial stage on device; SC-006 ordering 10/10 Windows and 10/10 emuP (parent) | Covered except a real prestige banner end to end and compact-class banner check (TESTQA-7); owner confirmation pending |
| FR-037, FR-038, SC-011 | EM `WorldLayoutValidationTests` (44, layouts "six", "twenty", "l-shape"), `NoFixedSlotCountGuardTests`, `WorldLayoutParityTests`, `SlotGridLatticeTests`, `LatticeGeometryExportTests`; PM `LayoutIndependenceTests` (20), `SlotGridTests`, `ShellGeometryProbeTests` | Covered (two counts and one non-rectangular layout). FR-038 has no test named for identity-vs-position; follows from id-keyed hit tests |
| FR-039 | spec future table, wiki page (documentation) | Documentation only, as intended |
| FR-040, FR-041, SC-012 (first bullet) | T502, T505 to T509 records (`direction.md`, `a03-anchor.md`) | Recorded |
| FR-042, SC-012 (second bullet) | T549 owner acceptance | Pending (by design, follows T548) |
| FR-043, SC-004, SC-005 | Parent blind parts done (see FR-031 and SC-006 rows); SC-004 and SC-005 owner timed trials (Windows and emuP, timed standard and reduced motion) and `readability-f006.md` | **Pending** (TESTQA-4) |
| FR-045 | `playtest-agent-f006-A/B/C/final`, `uiux-f006-A/A-rerun/B/C/final` | Covered |
| FR-046 | `baseline.md` (c568d24) and T542 re-run | Covered |
| FR-047, SC-013 | `world-bible-conformance.md` (CONFORMS after T560, v0.2), `world-copy.md`; EM `EveryShippedKeyHasText` | Covered (other lens judges conformance) |
| SC-002 | `baseline.md` T542: machine 100 % visible on every class; unobscured share 44 to 51 % on portrait phones, 66 to 68 % at 100 % on landscape phones | Measured; guidance partly missed, recorded |
| SC-003 | `baseline.md` T542: upgrade 3 (guidance 2), 0-tap problem category journey counts 4 card reads, system reach and return as guided (hub adds one step) | Measured; guidance partly missed, recorded |
| SC-007 (journeys) | Journeys Default, F003, F004, F005, F006; `test-rebaseline.md` | Partial (TESTQA-5) |

#### Findings

| ID | Severity | Evidence | Statement |
|---|---|---|---|
| TESTQA-1 | Minor | `reviews/final-test-names.txt` (written once, commit `76b7ee16`); `final-test-evidence.md` sections 12, 13 | The committed test manifest is the T542 one (Edit Mode 3,167, Play Mode 3,483). Final content is Edit Mode 3,187, Play Mode 3,526, player 85. Tests added later (for example `HubContractsBackSceneTests`, `RunShellPresenterAllocationTests`, `AutosavePoolThreadPlayerTests`, the `EmptyLocation_*` set cases, the T563 marker cases) are not in the manifest, so the FR-to-class mapping cannot be checked against a final per-test list. No hash or list exists for the final content. |
| TESTQA-2 | Minor | `W006VRealScenePlayerTests.cs:30` ("a miss never fails the suite"), `:110-113`, `:641-725`; `W006VWorldPlayerTests.cs:14` | The SC-008 budget numbers (SetPass delta, `camera.Render()` CPU, frame p95, texture MiB, census against 95 and 110, whole-frame allocations) are only logged as `B4B-*` lines and transcribed into `performance-b4.md`. The suites assert robust facts (census at most 173, slot renderers at most 61, marker not raising the worst case, zero allocations on `Refresh` and the synthetic 600 frames) and `AtlasPageBudgetTests` (EM) guards the 512 page size. Budgets 4, 6, 8 have no failing assertion, so a later regression passes the suite. The acceptance evidence is the prose record, which is sound for a one-time gate but not a regression guard. |
| TESTQA-3 | Major | `performance-b4.md` B4b.1 row 5 and B4b.3 (about 20 allocations per steady frame, real scene, 60 FPS); C-68 | SC-008 (binding) first bullet requires zero managed allocations per frame in steady state on the reference workload. Every defined path and `RunShellPresenter.Refresh` reads 0 and is asserted, but the whole host frame in the real packaged scene still allocates about 20 per steady frame, unattributed. Owner decision defers the deep profile (C-68) to a separate Issue after the gates. Until T549 records the owner's acceptance of this deviation against the criterion text, SC-008 is not literally met. The emulator FPS budgets 1 to 3 are not met by any build on this host (environmental per the ABA, owner decision, not R16 gates). |
| TESTQA-4 | Major | `final-test-evidence.md` T562 section ("Not run here: any trial"); no `reviews/readability-f006.md` | SC-005 (binding) and FR-043 are not yet closed: the owner timed trials (SC-004 Windows and emuP, SC-005 timed standard and reduced-motion) and the T546 readability record are pending. Parent-run blind parts are done (SC-005 blind 22/22 in four filters for four sets; P3 tiles 110/112 with the one miss re-tested 16/16; SC-006 10/10 twice). Reduced-motion cue readability depends on the pending timed run. The `cueShown` oracle rule (C-92) means every P3 tile is scored. |
| TESTQA-5 | Minor | `final-test-evidence.md` rows 11 to 13 and 12.1 rows 4 to 7, 13 rows 4 and 5; `performance-b4.md` B4b.4 | SC-007 asks that existing journeys pass. Every F003, F004 and F005 run since T542 reports FAIL on `ProducingFps` (33 to 58 fps against 60; the final quiet-host reading is 44 to 47 with controls and an ABA against the B4a-era build equal), so no scripted run passes as written; all other steps pass. Default, F003 and F005 were last run on code before the T561 camera, Back-polling and journey-script fix round and before the T563 presenter change; F004 and F006 were re-run on later code (T561 final, T563). The Play-Tester final session played the merged main by hand and showed no regression, which covers part of this. |
| TESTQA-6 | Minor | `final-test-evidence.md` rows 15, 16 (16 resolved by T544), section 7 D8 and marker 11; `uiux-f006-final.md` F-5, Not covered | On-device accessibility is unverified: the Unity player exposes no accessible nodes through adb, TalkBack and keyboard order were not run on any device, the Windows real-input checklist is NOT COVERED (owner decision 2026-11-19, no host input injection), and world markers have no screen-reader name (C-38 recorded). FR-005, FR-008a and FR-010 (accessible non-gesture path) rest on Play Mode focus and traversal tests only. Residual risk, no defect found. |
| TESTQA-7 | Minor | `final-test-evidence.md` section 11 verdict tables (NOT RUN items), `p3b-followups.md` C-49, C-55, C-74; `uiux-f006-final.md` section 7 and 8; `playtest-agent-f006-final.md` F-3 | Device or end-to-end evidence is missing for: the mandatory compact-class check of the milestone banner against a top-row machine (C-49; the banner at 100 % on 1080x2400 covers marker tops, F-3); marker keep-out, backdrop and draw-order checklist items 3 to 6; reduced motion on device; a real prestige banner end to end (C-74); the Established stage transition and the 12-location keep-out at 640x360. Model and Play Mode tests cover banner queueing, offline flagging and prestige as a run change, but FR-035's prestige acknowledgement has no end-to-end case. |
| TESTQA-8 | Minor | `HubContractsBackSceneTests.cs` and `final-test-evidence.md` 12.2 C-85 row; `AutosaveWriteCostTests.cs` (`mainMedian < 8 ms`); `AttentionMarkerSceneTests` (tap at 0 and 6 px) | Three tests carry a known weakness: (a) the second `HubContractsBackSceneTests` case (Escape on a non-first Keyboard) also passes on the old `Keyboard.current` polling, so it pins behaviour but cannot catch the defect it was written for; the fix rests on device evidence (3 of 3 first-Back launches). (b) `AutosaveWriteCostTests` asserts a wall-clock median and is host-timing sensitive by its own comment: a flake source, and a pass proves little about a slow host. (c) the marker scene test projects 0 and 6 px at editor scale (1 px per point); the 40 px device reach is hand-tested with adb (UI/UX final C-88) with no committed test. |
| TESTQA-9 | Minor | `final-test-names.txt` (no persistence-negative test by name); FR-003 | No test is named as asserting that selection, camera, card and effect state never enter the save. FR-003 is evidenced indirectly (Save V3 fixtures re-save byte-identically, preferences use the existing store, no save-format change in the diff). A one-line negative assertion over a mapped checkpoint would pin it. |
| TESTQA-10 | Info | `final-test-evidence.md` rows 5 and 12.2 (journey calibration) | Device fractions for landscape, zoom and hub (portrait only) stay provisional (C-82, C-88); the UI/UX final measured hub entry and Contracts row in landscape. No gate depends on them. |
| TESTQA-11 | Info | `tasks.md` lines 801, 811, 817 | T542, T543, T544 checkboxes are unticked while their Issues are merged. Derived data; the parent mirrors them before the hooks. |
| TESTQA-12 | Info | `final-test-evidence.md` section 12.2 | Good practice noted: the T561 camera fix was proven by a temporary revert of the production file (device-scale Play Mode case and three Edit Mode cases fail on the old code, restored byte-identical). The T542 misreadings (marker icon, marker tap) were corrected with assertions on the sprite identity and the projected tap. |

#### Conditions for acceptance

1. **C1 (TESTQA-4).** Before F006 closes, `reviews/readability-f006.md` exists and records the SC-004 and SC-005 owner timed trial results (Windows and emuP; timed standard and reduced-motion) against 9 of 10 within 3.0 s and 5.0 s, 10/10 within 2.0 s with median not above 1.077 s, and the blind-naming results; any miss is recorded with its disposition. T549 does not record SC-005 or FR-043 as met without it.
2. **C2 (TESTQA-3).** `final-acceptance.md` (T549) records the owner's explicit disposition of the whole-frame steady allocation (about 20 per frame, C-68) against the literal SC-008 wording, and of the unmet emulator FPS budgets, with the C-68 deep-profile Issue number. It may not state "zero allocations in steady state" without the qualifier "on the defined presentation paths".
3. **C3 (TESTQA-1).** The final evidence carries the final counts (Edit Mode 3,187, Play Mode 3,526, player 85) and either regenerates `final-test-names.txt` for the final content or states in `final-test-evidence.md` that the manifest is the T542 state and names the classes added since.
4. **C4 (TESTQA-5, TESTQA-7).** T549 records, as a stated limitation, that no scripted journey passes `ProducingFps` on this host, that Default, F003 and F005 were last run before the T561 and T563 changes, and that the compact-class banner check (C-49), reduced motion on device, prestige banner end to end and on-device accessibility remain not covered, each with its owner disposition or carry.

#### Counts

Blocker 0, Major 2 (TESTQA-3, TESTQA-4), Minor 7 (TESTQA-1, 2, 5, 6, 7, 8, 9), Info 3 (TESTQA-10, 11, 12), Conditions 4.

### UI/UX Reviewer — verdict: ACCEPT WITH CONDITIONS

#### Scope reviewed

Reviewed commit `945b1557` (worktree HEAD confirmed, no tracked edits). Read: `contracts/ui-surfaces-f006.md` (all sections and the as-built notes), `reviews/uiux-f006-final.md` (T543, 8 findings, carry table, C-98 per-class table, D4 CVD check) and spot-checked captures (`c04-shortage-marker-tap`, `h-640x360-fs200-build`), `reviews/final-test-evidence.md` (sections 7 to 13, SC-009 and accessibility notes), SC-009 and SC-012 text in `spec.md`. I wrote none of the UI and did not write T543. Batch A to C reports were used through the T543 carry table, not re-read in full. One own device run for UIUX-F3 (below). Not re-run: full suites, Windows sessions, TalkBack. Pending in parallel: T546 readability record, owner timed trials (SC-004, SC-005 timed), T549.

#### FR/SC coverage (UI/UX lens)

| FR/SC | Evidence | Status |
|---|---|---|
| FR-005 supported classes | T543 captures: phone portrait 100/200 %, landscape, 720x1280, 640x360 at 200 %, tablet; Windows only through T562 `tabL` | Covered, except 844x390 and 150 % text on device and a Windows build card (not captured) |
| FR-007/FR-008/SC-001 persistent layer | `PersistentLayerTests` plus `a02`, `c04`, `e03`, `j-tablet-*` | PASS (guidance) |
| FR-016 to FR-019 machine card, SC-002 | `b02`, `c01`, `c04`; Upgrade ring `c01` | PASS on device; keyboard order only by Play Mode tests |
| FR-020 to FR-023 hub and sheets | `l01`, `m-*` (8 sheets), `e04`, `e05` | PASS |
| FR-005/FR-021/FR-024 Back stack | C-86 on every sheet and card, portrait and landscape | PASS |
| FR-032/FR-033 build card | `i-p200-build`, `h-640x360-fs200-build`, `j-tablet-build`, `g-small720-build`; my run showed the portrait card (selected line, rows, Cancel/Confirm side by side) | PASS. "Need $X more" state not captured on device (Play Mode only) |
| FR-027 markers and attention (C-88, C-98) | taps PASS at 40 px, reach about 15 to 23 pt; per-class table T543 section 5 | PASS with residual risk UIUX-3 |
| SC-009 targets 44/48 pt | Play Mode assertions; device measurement impossible (no accessibility nodes), screenshots read about 45 to 47 dp (+-2) on the 100 % portrait class | PASS by test, not device-measured (UIUX-4) |
| SC-009 200 % text, no clipping | 640x360 at 200 % build card whole words; Welcome back scrolls with Continue visible | PASS (dev-build label overlap UIUX-1) |
| SC-009 reduced motion | Play Mode `ReducedMotion*` player tests; no device run in T543 or T548 | PASS by test; not seen on device |
| SC-009 keyboard | `KeyboardTraversalTests`, `NavigationInput`; adb key events show no focus change (T543 F-5) | PASS by test only |
| SC-005 CVD/grayscale (UI/UX part) | D4 plate tint luminance about 2:1, survives grayscale and deuteranopia; cue tiles 110/112 with the one miss re-tested 16/16 | PASS; T546 record pending |
| Empty/error states | Items empty, Market "Nothing to sell", store-full cause lines, load-error dialog with three actions, Welcome back | PASS |
| SC-012 owner acceptance | T549 | Pending |

#### Findings

| ID | Severity | Evidence | Statement |
|---|---|---|---|
| UIUX-F3 | Info (not reproduced; closes T543 F-3) | Own run below | First world tap about 20 s after a cold start: 12 of 12 attempts selected (build card opened), 0 ignored. Treat T543 F-3 as not confirmed; do not file. Method and limits below |
| UIUX-1 | Minor (dev only) | `h-640x360-fs200-build` (re-viewed) | "Development Build" label overlaps the lower edge of Confirm at 640x360/200 %. Absent in release. Third type row is cut by the card (scrolls, scrollbar visible). Record only |
| UIUX-2 | Minor | T543 F-2, `j-tablet-N` | Tablet side-panel hint ends with a one-word last line and stays on screen in every closed state. Copy polish after P4 |
| UIUX-3 | Minor, residual risk | T543 section 5 | Upgrade marker size gate sits at 23.76 pt per cell; small phone 720x1280 measures about 24 pt (+0.3), phones 24 to 28 pt. A smaller class or coarser density would silently drop the marker. Owner accepted hidden-on-boosted (C-98); this is a different exposure. T549: look at the smallest phone class |
| UIUX-4 | Minor | `final-test-evidence.md` D8, section 8 | 48 pt target size is asserted in Play Mode and not verifiable on a device; screenshot measurement gave 45 to 47 dp on one class. Treat the test as the evidence; owner touch test on a real phone in T549 is the only real check |
| UIUX-5 | Info | portrait captures `c04`, `s0` | The deck's hard edge and the large black void above the factory (owner decided: art task after P4) is visible on every portrait capture. T549 should judge it on the phone portrait class first; it is the largest visible "unfinished" impression |
| UIUX-6 | Info | T543 F-4 | Overdrive row activates on one tap, no confirm (owner: polish list after F006). Combined with an accidental tap it also removes the upgrade markers (accepted) |
| UIUX-7 | Info | T543 section 8 | Not device-covered: Windows build card, "Need $X more" state, reduced motion, banner at compact classes, 844x390, 150 % text, zoom and Back-state thresholds, TalkBack, keyboard focus order. All covered by Play Mode tests only |

##### UIUX-F3 reproduction detail

Build: profiler-free development APK of `945b1557` (Unity 6000.6.0f1 batch, `BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, 44.6 MB, Library/Bee/Android absent first). AVD `IdleFactory_Test_API_36`, cold boot (`-gpu host -no-snapshot-load -no-snapshot-save`), 1080x2400 at 420 dpi, 100 % text. Fresh install, no fixture: the world has empty locations, a tap on the first empty location (294,932 px) opens the build card.

Each attempt: `am force-stop`, 4 s idle, `logcat -c`, launch with `monkey` (process cold start), wait 20 s, screencap, `adb shell input tap 294 932`, wait 2.5 s, screencap. Selection decided by pixel difference of the bottom card band (1750 to 2300 px) between the two captures: 53.2 mean grey difference in every attempt (build card appeared); I viewed one post-capture to confirm ("Selected: Miner at location 1", Cancel/Confirm). Tap offset after launch 20.6 to 21.0 s. Input logcat lines per attempt 29 (33 in attempt 7), process alive each time. Two earlier trial attempts (same method) also selected, so 14 of 14 in all.

| Series | Attempts | Tap about 20 s after launch | Selected | Ignored |
|---|---|---|---|---|
| a1 to a12 | 12 | yes | 12 | 0 |

Result: **not reproduced** (0 of 12; T543 saw 2 of 6). Limits: the emulator was cold-booted once, not before every attempt (each attempt is an app cold start on a warm emulator); T543's cases were the L2 checkpoint at 200 % text with a build slot; I used the fresh-install world at 100 %. A residual first-tap effect on a loaded or just-booted device is not excluded. Cleanup: app uninstalled, APK and capture folder deleted, `.utmp` (gradle intermediates) and ProjectSettings/URP noise removed, no `Assets/UI Toolkit/` existed, `Library/Bee/Android` removed, emulator and adb server killed, worktree `git status` clean. Leases not released.

#### Conditions for acceptance

1. T546 readability record `reviews/readability-f006.md` is written and shows SC-005 (final, four sets) with no regression; the owner timed SC-004 and SC-005 results are recorded (pending, binding SC-005).
2. T549 owner visual acceptance records an accept verdict per surface and class in the before/after sheets (SC-012), with explicit looks at: phone portrait empty deck edge and void (UIUX-5), the small phone marker size (UIUX-3), the 640x360 at 200 % build card (UIUX-1), tablet hint wording (UIUX-2), the closed-state framing, the hub and eight sheets, the empty and error dialogs, and a touch test of target sizes on a real phone (UIUX-4).
3. UIUX-1 to UIUX-7 are carried into `p3b-followups.md` as open polish or record items with the owner decisions named; no Blocker or Major exists.

#### Counts

Blocker 0 / Major 0 / Minor 4 (UIUX-1 to UIUX-4) / Info 4 (UIUX-F3, UIUX-5 to UIUX-7) / Conditions 3

### Art Director — verdict: ACCEPT WITH CONDITIONS

Independent read-only review at `945b15578d2a03fd2bb49de7ca40a962b1e2d633` (confirmed HEAD of the review worktree). I produced none of this work. Medium tier.

#### Scope reviewed

- Rules: `docs/art/F004-art-bible-v1.md` (`F004-PAB v1.1.2`: sections 3.2, 5, 8.6, 9.4 C1/C8, 21.1 to 21.12, revision log), `reviews/art-direction-f006.md` (T525 AD conditions 1 to 9, findings E1/U1/D1 to F1), `reviews/t527-composite-proofs.md` plus the 4x3 stage-2 composite `reviews/t527-proofs/4x3-stage2.png` (viewed), `reviews/p3b-followups.md` (C-75), `reviews/final-test-evidence.md` (section 13 and T562), `reviews/uiux-f006-final.md`, `reviews/playtest-agent-f006-final.md` (F-2), `reviews/performance-b4.md` B4b.2.
- Assets: `Assets/IdleFactory/Art/F006/` (84 PNGs, 49 manifest entries, atlases), `Assets/IdleFactory/Art/F004/ui-icons/f003-ui-icon-upgrade_00.png` and the F004 markers (pixel-read: colours and sizes), `f006-ui-office_00.png`, `art-source/f006/**/provenance.json` (56 files), `tools/art-pipeline/palette/palette-v1.json`.
- Code: `SlotRenderers.cs`, `StateCueSet.cs`, `UpgradeMarkerTests.cs` (what is and is not asserted about the marker).
- Limits: no new captures were taken (no emulator, no Unity). Shimmer and all composed-frame judgments rest on the recorded captures and proofs, plus the one composite I viewed. The T546 readability record and the owner timed trials are pending and not judged here. Owner decisions listed in the brief are not re-filed.

#### FR/SC coverage (Art Director lens)

| FR/SC | Evidence | Status |
|---|---|---|
| FR-044 (world art conforms to the Art Bible, palette-exact, allowance rows) | per-family `check-report.json` all passed; `t527-composite-proofs.md` sections 2 to 4; T525 conditions 1, 4, 5, 6, 9 MET | PASS |
| FR-047 (provenance, license, approved brief per asset) | 56 `provenance.json` cover all 49 manifest assets; license, rig and script hashes, brief version, route recorded | PASS (AD-2, AD-5, AD-6) |
| SC-013 (art family reviews) | `art-direction-f006.md` plus composite proofs closing T525 conditions 2, 3, 7, 8 | PASS with carries (AD-3, AD-4) |
| FR-027 (upgrade marker, lower priority than problems) | `SlotRenderers.cs:160`, `UpgradeMarkerTests`, `final-test-evidence.md` section 13, `uiux-f006-final.md` section 5 | CONDITION (AD-1) |
| Stage triptych (stage identity) | `t527-proofs/*-stage{1,2,3}.png`; SC-006 ordering 10/10 on both sets (parent) | PASS; E1 closed (AD-3) |
| Plates, foundations, deck, depot, edge V, links, props | T527 proofs: zero wall or gantry pixels in any C8 ring or keep-out, ring pixels in set R; the stage-2 composite I viewed reads cleanly (plates, lanes, K4 badges, front V) | PASS |
| Cue stack, K4 tails, SC-005 cue legibility (art lens) | `t556-cue-contrast.md`; parent blind naming 22/22 and P3 110/112 (the miss re-tested 16/16) | PASS (T546 record pending) |
| Reduced motion and flash (condition 8) | FX flash matrix at source worst 2/s | PASS at source; sum on composited frames not re-measured here |

#### Findings

| ID | Severity | Evidence | Statement |
|---|---|---|---|
| AD-1 | Major | `Assets/IdleFactory/Art/F004/ui-icons/f003-ui-icon-upgrade_00.png` (24x24, one colour `ui_text` #f2f5fa, 96 opaque px, no `ink_0` outline); Art Bible 5 (UI glyphs allowance = `ui` only), 8.6 and 3.2 (markers: 11x11 box, `cvd_safe` + `ink_0` outline), line ~583 (a new marker = minor release), 21.4 (no row), revision log (no T563 entry); `SlotRenderers.cs:66,160` | The T563 upgrade marker draws a UI glyph in the world overlay: a different family from every other marker, about 16x15 opaque px against the 11x11 box, no ink outline, and no Art Bible amendment, AD pixel review or C8/C1 ring proof. `uiux-f006-final.md` section 5 says "white with a dark outline", but the sprite has no dark pixel. Contrast of `ui_text` with no outline against allowed backdrop tones: floor_0 12.3, floor_1 10.1, dim_0 11.6 (good), but plate_1 4.1, plate_2 2.6, steel_2 2.8, copper_2 2.6, steel_3 1.55 (below 3:1). Whether those tones occur under it in the overlay band is unproven. It never covers the badge or marker box 2 (asserted) and is never red. Captures and the device screenshot show it legible on the captured scenes; the gap is documentation and proof, not an observed defect. |
| AD-2 | Minor | `art-source/f006/**/provenance.json` `artBibleVersion` | Records carry `F004-PAB v1.1.0`; the current version is v1.1.2. The v1.1.1 and v1.1.2 patches change no asset, palette or style rule (revision log), so no migration is needed. Note it at closeout so it is not read as drift. |
| AD-3 | Info | `t527-composite-proofs.md` section 2, Art Bible 21.10 item 2 | E1 is closed on the plate-plus-machine reading (0.58 to 0.59, under 0.75). On the plate-only reading the wall is 1.5 to 1.6 times larger; the AD fallback (top band to `steel_1`) is unexecuted. In the composite I viewed, the wall is the largest bright mass but sits clear below the lattice and does not compete with the machines. It stays provisional until the SC-006 record (parent ran 10/10 on both sets; T546 records it). Accept. |
| AD-4 | Minor | `playtest-agent-f006-final.md` F-2, `uiux-f006-C.md` U-C12; `4x3-stage2.png` | The deck is an axis-aligned rectangle with a hard edge and a dark void around it at fit-all in every stage. Owner decision taken: art task after P4. Residual risk: the sense of place is carried by the front V and the depot, not the deck perimeter, and the stage-2 wall reads as a separate object below the lattice. Not a defect for F006 acceptance. |
| AD-5 | Info | provenance files | License text is a repository-ownership statement ("no public license grant"); no third-party material and no AI-generated pixels for these families. Route is headless Blender batch (the MCP bridge was unreachable at render time; recorded per asset, consistent with the hybrid-route decision). Concept `direction-a` and `direction-b` provenance exist. |
| AD-6 | Info | `f006-ui-office_00.png` | The Office glyph meets the P2-F22 owner decision: 24x24, single `ui_text`, 204 px, importer point filtered with no mips. T554 approval is recorded in `test-rebaseline.md`; I found no separate written AD pixel review in `reviews/`. |
| AD-7 | Minor | p3b-followups C-75; `uiux-f006-final.md` D5 | The store-full marker draws the 24 px storage icon at 22 px (0.917x, non-integer). Shimmer on pan or zoom was "not testable with adb". Owner decision: art task after P4. Residual: the crispness metric excludes the marker band (C-79), so shimmer stays unmeasured. |
| AD-8 | Info | Art Bible 21.4 row for `f006-fx-completion-highlight` | The 2,880 padded figure (F1 of T525) is now in the table; F1 resolved. |

#### Conditions for acceptance

1. Record the T563 upgrade marker in the Art Bible (patch or errata in 21.x plus the revision log) as the named reuse of `F003-UI-ICON-UPGRADE` in the world overlay: size, `ui_text` colour, absent outline, placement (26, row 21), the boosted-machine rule (ART-2). Checkable: grep of `docs/art/F004-art-bible-v1.md` for `F003-UI-ICON-UPGRADE` finds the entry.
2. Close the AD-1 proof: a dated AD note (in `reviews/` or the Art Bible) reading the shipped marker over the T562 upgrade tiles of the four classes, stating the backdrop tones actually under it and the contrast, or the C8 ring check extended to the marker. If any backdrop is below 3:1, the fix (an outlined variant) becomes an art task after P4, not an F006 blocker.
3. Correct the "dark outline" statement in `uiux-f006-final.md` section 5 (single-colour glyph).
4. Carry AD-4 and AD-7 as dated art-task Issues after P4, as the owner decided; note AD-2 in the closeout.

#### Counts

Blocker 0 / Major 1 / Minor 3 / Info 4 / Conditions 4

### Technical Art — verdict: ACCEPT WITH CONDITIONS

Independent read-only review at `945b15578d2a03fd2bb49de7ca40a962b1e2d633` (confirmed HEAD of the review worktree). I produced none of this work. Medium tier. No suites were re-run; the latest recorded runs (art pipeline exit 0 with 378 passed, 28 of them F006 trials; EM 3187, PM 3526, player 85) are used as evidence.

#### Scope reviewed

- Importer settings: all 210 `*.png.meta` under `Assets/IdleFactory/Art/F006` and `F004` (counted with grep), plus the upgrade icon and Office glyph metas in full.
- Atlases: `F006/Atlases/world.spriteatlasv2(.meta)`, `fx.spriteatlasv2(.meta)`, the four F004 atlases (`cues`, `machines`, `markers`, `slots`).
- Pipeline and provenance: `tools/art-pipeline/`, `art-source/f006/` (manifests, check reports, 56 `provenance.json`), `Assets/IdleFactory/Art/F006/import-manifest.json` (49 entries, sha256 per frame).
- Records: `reviews/performance-b4.md` B4b.1 and B4b.2, `reviews/final-test-evidence.md` (art-pipeline rows, section 13), `reviews/t527-composite-proofs.md`, Art Bible 21.4, 21.9 (T552 page cap), 21.12.
- Code: `SlotRenderers.cs`, `StateCueSet.cs`, `UpgradeMarkerTests.cs`.
- Not done: no Unity inspection, profile or build.

#### FR/SC coverage (Technical Art lens)

| FR/SC | Evidence | Status |
|---|---|---|
| FR-006 / SC-008 (budgets, R16) | `performance-b4.md` B4b.1: texture 3.54 MB (budget 8 MB), pages 512x512 RGBA32, shader variants 356 (cap 490), APK 44.6 MiB, SetPass delta +8.5 to +11.4 over the same-state F004-only value | PASS on the restated reading; owner reading open for the marker worst case (TA-1) |
| FR-044 / FR-047 (import conformity, provenance) | 210 PNG metas uniform: point filter, PPU 24, no mips, not readable, alpha-is-transparency, Full Rect; 56 provenance files cover all 49 manifest assets with hashes | PASS |
| FR-027 (upgrade marker, performance part) | B4b.2: census unchanged (99/101, equals boosted + Overdrive), `Refresh` 0 allocations, +4.4/+4.7 SetPass at 12 markers | PASS with TA-1 |
| Art pipeline tests | `scripts/test-art-pipeline.ps1` exit 0, `ART_PIPELINE_PASS`, 378 passed (`final-test-evidence.md`) | PASS |
| Sprite and pixel-perfect rules | metas as above; marker drawn at scale 1; deck cell 70x54 pinned to whole art pixels in `DeckLayoutTests`; only exception is the 22 px store-full marker (TA-3, owner-accepted) | PASS with TA-3 |
| Atlas rules (512 page, 1 MiB, F006 world and FX) | `world.spriteatlasv2.meta`: padding 2, no rotation, no tight packing, 512 override on Default/Standalone/Android, RGBA32; `fx.spriteatlasv2`: one folder packable, same settings | PASS |

#### Findings

| ID | Severity | Evidence | Statement |
|---|---|---|---|
| TA-1 | Minor | `Assets/IdleFactory/Art/F004/ui-icons/f003-ui-icon-upgrade_00.png.meta` (no atlas; `markers.spriteatlasv2` holds only boosted and overdrive); `performance-b4.md` B4b.2 | The upgrade icon is an unatlased 24x24 texture drawn up to 12 times on the Overlay layer. Measured: +4.4 (landscape) and +4.7 (portrait) SetPass at 12 markers; its own delta 5.4 to 5.8 at 12, 0.5 to 1.9 in natural runs. Against the fixed 34 the worst case is +12.5 and +14.7 (0.5 and 2.7 over +12); on the restated same-state reading it passes (+11.4 and +9.7). Texture memory is fine (3.39 MiB with markers forced). The B4b option (put it on the markers atlas, about -4 to -5 passes at 12) is low complexity and needs an Art/TA decision. Not a blocker. |
| TA-2 | Minor | same files | Moving the icon onto an atlas changes the shared F004 UI-icon import path (the UI Toolkit icons and the world catalog use the same folder by key). It needs a world-only copy or registry row plus a re-measure, so it is a post-F006 task. Recommend bundling it with C-75 (22/23 px marker sprite) so one marker-atlas pass covers upgrade, store-full and the 22 px marker. |
| TA-3 | Minor | p3b-followups C-75 | The store-full marker is drawn 0.917x (24 to 22 px), a non-integer scale against the world integer-scale rule (Art Bible 21.9 and 4). Owner-accepted for F006 and tracked as an art task. Shimmer is unmeasured because the crispness band excludes the marker area (C-79). |
| TA-4 | Info | `F006/**/*.png.meta` platform blocks | Per-sprite importers list `textureCompression: 1` for Standalone, Android, WebGL and iOS but `overridden: 0`, so the default platform applies; atlas pages override explicitly to RGBA32. Only unatlased textures (upgrade icon, Office glyph, UI icons) use importer defaults. Consistent with the T552 page rule and the measured 3.01 to 3.39 MiB runtime pages. |
| TA-5 | Info | `performance-b4.md` rows 6, 7, 10, 11 | Budgets pass with margin: 3.54 MB of 8, largest page 512x512, APK 46.78 MB (+11,484 B on B4a), 356 of 490 shader variants (unchanged from B4a and B3). Renderer census: real scene 88 to 91 (96 with tails), synthetic worst case 99/101 under the confirmed 110 ceiling (21.12); the signed 95 is exceeded by +4/+6 in the synthetic worst case only. |
| TA-6 | Info | `art-source/f006/**/provenance.json` | Records carry rig-v2 sha256, render-script sha256 and the headless-route note, and a second headless render was byte-identical per the records. `artBibleVersion` is v1.1.0 (current v1.1.2; patches change no asset rule). |
| TA-7 | Info | `performance-b4.md` B4b.4 | Emulator FPS budgets 1 to 3 are unmet by any build (owner: environmental; physical-device gap open). World-art CPU cost is small (`camera.Render()` 0.16 to 0.22 ms mean). Not re-filed. |

#### Conditions for acceptance

1. Record the owner reading of budget 8 for the marker worst case (+12.5 and +14.7 against the fixed 34), per B4b.1 row 8 and B4b.2. Checkable: a dated owner decision line in `performance-b4.md` or `final-acceptance.md`.
2. Add a dated Issue (or extend the C-75 art task) "world-only copy of the upgrade, store-full and 22 px markers on the markers atlas; re-measure SetPass" so TA-1 and TA-3 have an owner and a measurement. Checkable: Issue number cited in `final-acceptance.md`.
3. Keep `scripts/test-art-pipeline.ps1` at exit 0 on the final commit of the feature; the T548 commit is records-only, so the 378-pass run stands. Checkable: `final-test-evidence/art-pipeline.log`.

#### Counts

Blocker 0 / Major 0 / Minor 3 / Info 4 / Conditions 3

### World Designer — verdict: ACCEPT WITH CONDITIONS

#### Scope reviewed
- Read-only worktree HEAD confirmed `945b15578d2a03fd2bb49de7ca40a962b1e2d633`.
- `reviews/world-copy.md` (sections 1-7, N/L/class D rows, coverage proof), `reviews/world-bible-conformance.md` (Parts 1-3), `docs/world/world-bible-v0.1.md` and `v0.2.md` (presence), `tasks.md` T547/T560 lines.
- Shipped strings at the reviewed commit by search and targeted read: `UI/FactoryRun.uxml`, `UI/RunRejectionCopy.cs`, `UI/Panels/ExpansionPanel.cs`, `UI/Panels/OfflineSummaryPanel.cs`, `UI/Panels/PrioritiesPanel.cs`, `UI/Panels/HubPanel.cs`, `UI/RunShellDocument.cs`, `UI/RunShellDocument.Hub.cs`, `UI/UnlockCopy.cs`, `UI/MachineCard.cs`, `UI/MilestoneCopy.cs`, `Presentation/World/SlotVisualModel.cs`. Diff of `Assets/IdleFactory/Unity/UI` and `Presentation` since T560 commit `137da8e` (8 files) for newly added player-visible literals.
- Term sweeps over UI and Presentation (.cs, .uxml): slot, warehouse, epic/legendary/mega/ultimate/tycoon/empire, bay, "Systems", exclamation marks, Depot/trunk/vein/region in visible text.
- Not read: the capture PNGs (visual world reading is the Art Director and UI/UX lens; I rely on Part 2 for the visual rows). Pending in parallel: T546 readability record.

#### FR/SC coverage (World Designer lens)
| FR/SC | Evidence | Status |
|---|---|---|
| FR-047 (world bible conformance, vocabulary) | All T547 Part 2 changes verified present at HEAD (table below); sweeps clean | PASS |
| SC-013 (no unowned world-facing string) | world-copy.md section 7 coverage proof (888 literal lines, 0 unaccounted); no new player literals since `137da8e` (diff check) | PASS (see WD-2 on record staleness) |
| FR-019 to FR-021 (hub label "Office", never "Systems") | `HubPanel.cs:30` HubLabel "Office"; CF-4 strings at `RunShellDocument.Hub.cs:81`, `HubPanel.cs:428,637` | PASS |
| Stage progression framing (R14, R15, B-01 to B-04) | `MilestoneCopy.cs:37-41` text identical to world-copy rows; stage names G-01 to G-03 not shown (by design) | PASS |
| Offline summary wording (R14) | `OfflineSummaryPanel.cs:570` "Location n ..."; milestones reuse `MilestoneCopy.Full`; N-42 rows | PASS; copy polish deferred by owner (see WD-4) |
| World tone consistency across surfaces | Sweeps: no hype, no exclamation in player text, no Systems label, no warehouse/region/lore names | PASS (see WD-3) |

T547 Part 2 string changes, verified at HEAD:
| Change | Shipped text | Result |
|---|---|---|
| CF-2 slot to location (16 sites) | `FactoryRun.uxml:46` "tap an empty location"; `RunRejectionCopy.cs:17,18`; `ExpansionPanel.cs:109,127,189` ("Locations", "Locations now and next", " locations."); `OfflineSummaryPanel.cs:570`; `PrioritiesPanel.cs:172`; `RunShellDocument.cs:1305`; no quoted player string with "slot" remains (remaining hits are identifiers, sprite keys, GameObject names) | Applied |
| CF-3 Factory Sale | `HubPanel.cs:39`, `RunShellDocument.cs:38` | Applied |
| CF-4 no generic "system" | `RunShellDocument.Hub.cs:81` "needs attention"/"need attention"; `HubPanel.cs:428` "Nothing is available in the Office right now."; `:637` "That could not be opened" | Applied |
| CF-5 | `HubPanel.cs:610` "Overdrive is locked."; `UnlockCopy.cs:111` "Locked." | Applied |
| B-4 | `MachineCard.cs:91,92` "Store full, so makers cannot deliver", "Makers cannot keep up"; detail line wired (`:487-499`) | Applied |
| B-6 | `MachineCard.cs:78` "Close machine card"; `RunShellDocument.cs:40` "Close panel" | Applied |
| C-56 B-01s | `MilestoneCopy.cs:38` "Level {n}. 1 new location opens." | Applied |
| CF-1 / v0.2 | `docs/world/world-bible-v0.2.md` present; "Store" capacity term used consistently | Conforms |

#### Findings
| ID | Severity | Evidence | Statement |
|---|---|---|---|
| WD-1 | Info | `Assets/IdleFactory/Unity/UI`, `Presentation` at HEAD vs Part 3 | The shipped strings match the T547 Part 2 specification and the Part 3 re-read exactly. No player-visible literal has been added since `137da8e` (the 8 changed files add only a sprite key and identifiers). |
| WD-2 | Minor | `reviews/world-copy.md` rows N-06, N-07, N-20, N-23, N-29, N-35, N-40, N-41, N-42h, L-08, L-11 and the section 7 intro | The ownership record still labels these rows "CHANGE" and says they "stay open until T560 applies them"; section 3 still lists B-01s as "draft". The code is correct (WD-1) and Part 3 of the conformance record closes them. The inventory is stale and a reader of world-copy.md alone would see 11 open conflicts. Documentation only. |
| WD-3 | Info | `ExpansionPanel` ("Storage", "Upgrade storage"), `MachinePanelPresenter` ("Storage is full"), `SlotVisualModel` state word "storage full" vs "Store full" elsewhere | Two capacity words ("Store", "Storage") coexist. Permitted and recorded by v0.2 section 8 (owner decision CF-1), so not a defect; residual one-word-per-thing risk for a later polish pass. |
| WD-4 | Info | `OfflineSummaryPanel.cs:357-579` | Offline summary is plain, past tense and hype-free, but long and not world-flavoured. Owner already deferred offline summary copy to later polish; not a conformance issue. |
| WD-5 | Info | `HubPanel.cs:420,428`; `UI/MilestoneCopy.cs` | World naming surfaces as designed: "Office" (hub), "Factory Sale", "Deck extended.", "N new locations open." Region/location naming is minimal (single `starter-platform`, no region names shown), so no region-naming risk exists in F006. Stage names Open deck/Walled yard/Gantry line are never shown to the player; stage progression is framed only by "Deck extended." plus the deck art. That is conformant (G rows say "only if UI/UX decides") but means the three stage names have no player-visible use. |
| WD-6 | Info | Part 2 "Not verifiable here" | Device-only checks stay open and are not passed by this lens: vein and depot framing on device per screen class, TalkBack reading of the banner, hub accessible name and attention strip, two-line cause label fit at 200 % per class (UI/UX). |

#### Conditions for acceptance
1. T546 readability record `reviews/readability-f006.md` exists (pending in parallel) and reports no world-text truncation on the checked classes at 200 % for the Part 2 strings ("Locations now and next", the B-4 two-line cause label, "Office, N need attention" is accessible-only).
2. Optional, no gate: `reviews/world-copy.md` rows listed in WD-2 are re-marked CONFORMS (applied by T560) at the final-acceptance record so that SC-013 inventory and the code agree. The reviewer does not hold this as a blocker.
3. Device-only items in WD-6 remain carried to the owner gate (T549) and are not claimed as passed.

#### Counts
Blocker 0 / Major 0 / Minor 1 / Info 5 / Conditions 3 (1 gating pending T546, 1 optional, 1 carry)

### Performance — verdict: ACCEPT WITH CONDITIONS

#### Scope reviewed
Reviewed commit `945b1557` (HEAD confirmed). Read: `reviews/performance-b4.md` (B4b and the B4a history), the raw evidence under `reviews/performance-b4/b4b/` (`windows-player-lines.txt` B4B-REFRESH-STILL and W003-HOSTFRAME lines, `device-probe-lines.txt` B4BPROBE burst lines, `fps-captures.tsv`, `f006-autosave-journey.md`), `specs/004-visual-identity/reviews/performance-budgets.md` §4 rows 8 and 9, `research.md` R16, `spec.md` FR-006 and SC-008, `p3b-followups.md` (C-66, C-68, C-75), `RunShellPresenterAllocationTests.cs`, and the T544 QA comment on Issue #716. No measurements were taken. All numbers below are exploratory evidence from one workstation (FR-027).

#### FR/SC coverage (Performance lens)
| FR/SC | Evidence | Status |
|---|---|---|
| FR-006 (no steady-state allocation in recurring presentation updates) | `RunShellPresenterAllocationTests` (PM), `B4B-REFRESH-STILL` 600 calls = 0 allocs in four states (player), W006-V synthetic 600 frames = 0; card-open cost of B4a (~400/frame) fixed by T560 | Met for the defined presentation paths and `Refresh`. Whole real-scene frame still about 15 to 21 allocs per steady frame (C-68), unattributed |
| SC-008 allocations ("zero ... on the reference workload") | B4b.3 | Partly met: zero on the defined paths; not zero for the whole real-scene host frame (about 100x the PR-2 floor). SC-008 wording and the "PASS for defined paths" reading differ; owner-visible (PERF-1) |
| SC-008 texture memory (#6), atlas page (#7), shader variants (#11), APK (#10) | build report 3.54 MB of 8; 512x512 max; 356 of 490; 44.6 MiB of 150 | Met, well founded (build report plus runtime) |
| SC-008 Windows frame p95 (#4) | W003 S 0.016 / B 0.040 / R 0.021 ms; W006-V world 0.022 ms; real-scene `Refresh` p95 0.20 to 0.37 ms including rebuild frames | Met on the budget's own workload (W003-B 0.040 at the range's upper edge); real-scene figure above 0.02 to 0.04 but under the 1 ms ceiling; disclosed |
| SC-008 SetPass (#8, restated) | 42.1 / 44.0 all-state vs F004-only 33.6 / 34.2; delta +8.5 to +11.4; marker worst case delta +11.4 / +9.7 | Met on the restated same-state reading (owner decision). Residual: fixed-34 reading +12.5 / +14.7 |
| SC-008 emulator FPS (#1 to #3) | 44.6 to 47.9 FPS, p95 33 to 44 ms, 0 to 7 intervals >= 50 ms; ABA 44.8 / 44.6 / 43.8 | Not met by any build on this host; environmental per ABA (owner decision); not on R16's list |
| Budget 9 (guest CPU vs paired B1) | not measured | Gap (outside R16's list) |
| R16 labels and method | F004 method, W006-V fixture, per-atlas SetPass reported | Met |

#### Findings
| ID | Severity | Evidence | Statement |
|---|---|---|---|
| PERF-1 | Major | performance-b4.md B4b.1 row 5; spec.md SC-008 first bullet; C-68 | SC-008 says zero allocations per frame in steady state on the reference workload. The real packaged scene measures about 20 per steady frame at 60 FPS (about 2.5 uncapped) card closed, 17 to 21 card open, 15.5 build card. The record states PASS only "for the defined paths and for `Refresh`" and keeps the whole frame open, which is honest, but SC-008 as written is not literally met. The owner deferred the deep profile to a separate Issue after the gates. Acceptance needs this recorded as an explicit owner-accepted deviation (or SC-008 reworded), not left as an implied PASS. |
| PERF-2 | Minor | B4b.4 rows 1 to 3 and ABA | The environmental claim for budgets 1 to 3 is well founded: A1 44.8, B 44.6, A2 43.8 with controls 44.4 to 47.9 on the same emulator and session, host confirmed quiet, device main thread about 20 ms in both builds. It rests on one ABA in one session of one emulator; the cause of the drop from B4a's 61.4 is not isolated (host GPU stayed in P8 at about 880 MHz, observed only). It supports "F006 P3/P4 code costs nothing measurable here", not "60 FPS holds on a device". Budgets 1 to 3 are not R16 gates, so this does not block. |
| PERF-3 | Minor | budget 9 row; B4b.5 | Budget 9 is unmeasured; no B1 build was run. It is outside R16's list, so the scope-gap reading is defensible, but it is a frozen F004 budget (met +3.2 % at B3) with no F006 value; keep it listed as an open gap. |
| PERF-4 | Minor | B4b.1 row 8, B4b.2 | The owner's budget 8 reading (delta over F004-only in the same state, max +12) holds on the numbers: +8.5 to +11.4, marker worst case +11.4 / +9.7. Headroom is thin (0.6 under +12 in landscape). Against the fixed 34 the marker worst case is +12.5 / +14.7. The record's own option (put the 24x24 upgrade icon on the markers atlas, about -4 to -5 passes at 12 markers, low complexity) was not taken; recommend a polish Issue. `camera.Render()` CPU 0.16 to 0.24 ms is far inside the tier-1 bound, so CPU is not the risk. Not a defect. |
| PERF-5 | Info | B4b.3; `RunShellPresenterAllocationTests.cs:96-161`; windows-player-lines.txt `B4B-REFRESH-STILL` | The new hot path (per-frame affordability mask in `RunShellPresenter.Refresh`) is well covered: 600 calls = 0 allocations with the mask loop doing work (upgradeMarkersOn 4 to 6 in the player; the PM test asserts at least one affordable upgrade), mean 0.0008 to 0.0011 ms per call, and a separate test asserts moving cash without crossing a cost does not rebuild. The 7 to 9 % of non-structural frames that allocate 50 to 90 are frames where a displayed value changed, by design. |
| PERF-6 | Info | B4b.2 | Marker census: worst case 99 / 101, equal to the boosted + Overdrive case (marker yields to Item boost, ART-2), asserted in `W006VWorldPlayerTests` and `W006VRealScenePlayerTests`. Below the confirmed ceiling 110 (-11 / -9), above the signed 95 by +4 / +6 (as in B4a), far below tier 173. Store-full markers per machine uncapped is an owner-accepted risk (cap note with C-75); the census does not include them, so the worst-case figure holds only while that holds. |
| PERF-7 | Minor | T544 QA text findings; `device-probe-lines.txt:110-111`; `RunShellPresenterAllocationTests.cs:144,160-161` | The carried text findings are accurate and non-blocking. (a) C-66 ran the idle window first, then the burst: a fixed order, so warm-up or thermal drift is confounded with the burst effect (49.9 vs 49.4 FPS, p95 both 33.3 ms); the write cost is read from `/proc/self/io` (about 19 KB per write, 0.56 MB per minute), which is order-independent. (b) The cash-moves case asserts no rebuild and unchanged markers, but its zero-allocation result is logged, not asserted. (c) Budget 5 max 162 to 215 allocations and the 19 to 29 % rebuild share are structural-event figures, not steady state. Correct or note in the final record; no re-measurement needed. |
| PERF-8 | Minor | B4b.5 C-66, `AutosaveWriteCostTests` | C-66 flash wear is emulator storage only, with no physical-flash figure. `mainMedian < 8 ms` is wall-clock based and host-timing sensitive (the test says so); it passed in the final run but is a latent flake. |
| PERF-9 | Major | B4b header, B4b.4, B4b.7 | Physical-device, battery and thermal gap: no value exists for any F006 budget on real hardware, and the only frame data (emulator) misses budgets 1 to 3 by about 14 FPS. FR-027 labels everything exploratory so this is not a defect, but "SC-008 met" cannot be claimed beyond "within budget on one Windows workstation plus an emulator". Must stay an explicit open gap in the final acceptance and in T549. |
| PERF-10 | Info | B4b.3 | C-68 (about 20 per steady frame, a function of time not content) is unattributed after two rounds. The rate is low (about 1.2 KB per frame) and the emulator probe shows 0 to 1 intervals >= 50 ms, so visible GC pauses are unlikely, but this is unproven on a low-end device. A separate Issue after the gates is a reasonable deferral. |

#### Conditions for acceptance
1. The final acceptance records PERF-1 as an owner-accepted deviation of SC-008's whole-frame allocation reading (about 20 per steady frame, C-68 deferred to a separate Issue), or SC-008 is reworded; checkable by an entry in `final-acceptance.md`.
2. The final acceptance keeps three gaps listed as open, not as passes: physical-device/battery/thermal (PERF-9), budgets 1 to 3 environmental-not-met (PERF-2), budget 9 unmeasured (PERF-3).
3. The C-66 and budget-5 text corrections (PERF-7) are present in `p3b-followups.md` as open polish items; checkable by grep.
4. The upgrade-icon-on-markers-atlas option (PERF-4) and the C-75 store-full cap note are on the post-F006 polish list.

#### Counts
Blocker 0 / Major 2 (PERF-1, PERF-9) / Minor 5 (PERF-2, PERF-3, PERF-4, PERF-7, PERF-8) / Info 3 (PERF-5, PERF-6, PERF-10) / Conditions 4

### Security and Privacy — verdict: ACCEPT WITH CONDITIONS

#### Scope reviewed
Reviewed commit `945b15578d2a03fd2bb49de7ca40a962b1e2d633` (confirmed HEAD of the read-only worktree). Read: `Assets/IdleFactory/Unity/Persistence/DisplayPreferencesStore.cs` (v2 schema, v1 migration, strict parser, atomic write with bounded retry), `JsonRunCheckpointStore.cs` and `SaveV2CheckpointCodec` (generation protocol, codes without file text), `Core/Run/Application/AutosavePolicy.cs` and `RunPersistence.Autosave.cs` (cadence, single in-flight background write, join before store access, failure handling), `CheckpointDiagnostics.cs`, `PersistenceRoots.cs`, `Assets/Plugins/Android/AndroidManifest.xml` plus `IdleFactoryBackupRules.androidlib` (both rule files), `ProjectSettings.asset` (`ForceInternetPermission: 0`, no custom keystore), `Assets/IdleFactory/Editor/F006EmulatorCaptureBuild.cs` and its asmdef, `Tests/PlayMode/F006/F006TrialCaptureAndroidLauncher.cs`, `scripts/check-release-build.ps1` (backup/manifest checks), `.gitignore`, and `reviews/performance-b4.md` (C-65, C-66: T544 two-write player test `AutosavePoolThreadPlayerTests`, device `-AutosaveCheck` force-stop restore of generation 13). Searches: network APIs, device identifiers, secrets and key files in tracked files, scripting-define use. Not re-run: suites; not opened: `t562-keys/`, `t546/`.

#### FR/SC coverage (Security lens)
| FR/SC | Evidence | Status |
|---|---|---|
| FR-003 / SC-008 (autosave cadence 30 s / 2 s, force-stop survives) | `AutosavePolicy`, `RunPersistence.Autosave.cs`; performance-b4.md C-65 (player test, device force-stop restored generation 13), C-66 (about 19 KB per write, about 2.3 MB/h periodic) | Pass (physical-flash wear not measured, Info) |
| Preferences v2 schema, v1 migration, corrupt/missing, atomic write (R20, T537/T538) | `DisplayPreferencesStore` (strict walk, 4 KB cap, no v1 rescue of an invalid v2, temp file in same dir, `File.Replace`/`Move`, retries only on transient HResults, no throw, fixed diagnostic code only); `DisplayPreferencesStoreTests`, `DisplayPreferencesV2Tests` exist | Pass |
| Checkpoint store integrity (R08) | `JsonRunCheckpointStore` over `CheckpointGenerationFiles`; rejections are static codes, never file text; background write only touches prepared bytes under the store gate; `finally` always completes the job | Pass |
| FR-031 / D-M backup and export | Manifest `allowBackup=true` with both rule files excluding `SessionLog`/`Exports` in `file` and `external` domains, `IdleFactory/` and `IdleFactory-Dev/` variants; no FileProvider; release script enforces it | Pass |
| No network, no PII | No `UnityWebRequest`/`HttpClient`/`System.Net`/`OpenURL`/analytics/device id in non-test code; `ForceInternetPermission: 0`; manifest has no `uses-permission` | Pass |
| Logs | `CheckpointDiagnostics` carries operation, stage, exception type, member:line only; preferences log only `preferences.display.invalid` | Pass |
| Test/capture code isolation | Capture define applied per build via `extraScriptingDefines`, never written to ProjectSettings; launcher is `#if IDLEFACTORY_T562_CAPTURE && UNITY_ANDROID && !UNITY_EDITOR` and lives in the test assembly (`UNITY_INCLUDE_TESTS` constraint); `F006EmulatorCaptureBuild` sits in the Editor-only asmdef | Pass with SEC-1, SEC-2 |
| Secrets in repo | `.gitignore` covers `.env*`, `*.keystore`, `*.key`; `test-results/` ignored; no tracked keystore/pem/jks/credential files; no keys or capture answers (`t562-*`) tracked; `androidUseCustomKeystore: 0` | Pass |

#### Findings
| ID | Severity | Evidence | Statement |
|---|---|---|---|
| SECURITY-1 | Minor | `Assets/IdleFactory/Editor/F006EmulatorCaptureBuild.cs` (C-101) | The class is not `#if`-gated. It cannot ship: the `IdleFactory.Editor` asmdef is `includePlatforms: ["Editor"]`, it has no menu entry, and it needs `-executeMethod`. Residual risk is limited to a developer running it outside the leases. Accepted as documented; optional hardening is an `#if` or a one-line guard that refuses without a lease/env marker. |
| SECURITY-2 | Minor | `F006EmulatorCaptureBuild.BuildApk` finally block | It restores graphics APIs and the use-default flag but not the scripting backend (IL2CPP) or `targetArchitectures` (ARM64) that it sets, and a crash before the `finally` could leave `T562CaptureLauncher.unity` in `Assets/`. This is ProjectSettings noise risk, not a leak; the settings-noise hook and QA run catch it. |
| SECURITY-3 | Minor | `scripts/check-release-build.ps1` | The release gate checks backup rules and manifest but has no assertion that a shipped APK contains neither the T562 launcher nor test assemblies. The structural protections above hold today; a regression guard would make it objective. |
| SECURITY-4 | Info | `F006EmulatorCaptureBuild` extra defines | The capture build is a development build with only `IDLEFACTORY_T562_CAPTURE`, not `IDLEFACTORY_DEV`, so it uses the normal persistence root. It is emulator-only; an `IdleFactory-Dev` root would isolate it from a real save on a shared device. |
| SECURITY-5 | Info | `DisplayPreferencesStore` | A downgrade to an older build reads v1 and loses `showZoomButtons` (accepted, R20). v1 is never rewritten or deleted, so no data loss. The write path does not fsync the directory entry after rename, an accepted general limitation. |
| SECURITY-6 | Info | `AndroidManifest.xml`, backup rules | Checkpoints and preferences are backup-eligible by design (only the log and exports are excluded). No sensitive PII is stored; the save is player progress only. Backup of a Dev-root save is also allowed; irrelevant for release. |
| SECURITY-7 | Info | performance-b4.md C-66 | Autosave write volume is about 2.3 MB/h periodic and about 34 MB/h in a burst of one structural change every 2.2 s; the 2 s spacing bounds the storm. Physical flash wear not measured (already an open device gap). |

#### Conditions for acceptance
1. Record SECURITY-1 to SECURITY-3 as accepted carries (or fix before release): either gate `F006EmulatorCaptureBuild` or add a release-script assertion that the release APK/IL2CPP output holds no `F006TrialCaptureAndroidLauncher` and no test assemblies. Checkable by reading `check-release-build.ps1` or the carry entry.
2. `scripts/check-settings-noise.ps1 -Commit <sha>` must stay clean on the final commit (it guards SECURITY-2).

#### Counts
Blocker 0 / Major 0 / Minor 3 / Info 4 / Conditions 2

### Game Systems / Economy — verdict: ACCEPT WITH CONDITIONS

#### Scope reviewed

- Reviewed commit `945b1557` (read-only, via `git show` and `git diff c568d24 945b1557`; `c568d24` is the F006 baseline measured in `reviews/baseline.md`). No build, test or Unity run was made.
- Economy definition and data files, all unchanged over `c568d24..945b1557`: `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `Content/Development/development-catalog.json`, `Content/Schemas/gameplay-content-v3.schema.json`, `Core/Domain/Definitions.cs`, `Core/Domain/DefinitionCatalog.cs`, `Core/Run/Content/*` (RunCatalog, IndustryUpgradeDef, PrestigeDef), `Core/Run/Application/BuildPricing.cs`, `OfflineReturn.cs`, `OfflineReconciler.cs`, `RunOfflineSummary.cs`, `PrestigeThreshold.cs`, `PrestigeTransition.cs`, `Core/Run/Domain/ContractRewards.cs`, `SalePricing.cs`, `Core/Run/Simulation/OfflineSegmenter.cs`, `Core/Application/OfflineReconciler.cs`, `specs/005-economy-contracts/**`, `Assets/IdleFactory/Tests/EditMode/Balance/**` (0 changed files in each set).
- Changed Core/Unity files that touch economy-adjacent logic: `Core/Run/Application/AutosavePolicy.cs` (new, T559 commit `4f5b5efa`), `RunPersistence.Autosave.cs`, `RunSnapshots.Items.cs` (ResearchCost, CanPurchaseResearch, OverdriveStageAt), `RunSnapshots.Factory.cs`, `Unity/Presentation/World/MachinePresentationModel.cs`, `Unity/Presentation/World/SlotVisualModel.cs`, `Unity/UI/Panels/OfflineSummaryPanel.cs`, `Core/Run/Application/RunSession.cs` and `RunSimulator.cs` (T514 `059845bb`).
- Evidence read: `specs/006-world-first-factory-experience/tasks.md` (T559 block, lines 785-786), `reviews/playtest-agent-f006-final.md` (sections 1, 2.2, 2.6, 4 and the counts), `reviews/baseline.md` (header), `reviews/final-test-evidence.md` (not re-run).
- Not read: the scratch diff `test-results/t543pt/baseline.diff` named in the Channel A section. It is untracked and not in the repository, so the Channel A claim is checked through the committed record instead (see item 1).

#### FR/SC coverage (Game Systems lens)

| FR/SC | Evidence | Status |
|---|---|---|
| FR-027 (upgrade available = exists and affordable) | `MachinePresentationModel.cs:98` and `:324`; `SlotVisualModel.cs:158-160`; domain `RunSnapshots.Factory.cs:426` (`affordable = costOk && state.Cash >= cost`); `UpgradeMarkerTests.cs:63` | PASS (item 3) |
| Economy numbers unchanged outside spec (Channel A, gate C) | committed `specs/005-economy-contracts/reviews/strategy-baseline-r3.md` unchanged; Content, Balance tests and economy definitions unchanged over `c568d24..945b1557` | PASS (items 1, 2) |
| Offline summary values from the domain | `RunOfflineSummary` fields (`CashBefore/After`, `SalesIncome`, `Completions`, `EfficiencyNumerator/Denominator`) feed `OfflineSummaryPanel.cs`; one derived percent is computed in presentation | PASS with finding GS-1 (item 4) |
| Autosave cadence (T559, tasks.md 785-786) | `AutosavePolicy.cs:43`, `:46` (30 s, 2 s); offline accounting unchanged per commit message | PASS as implemented; traceability finding GS-3, behaviour finding GS-2 |

#### Findings

| ID | Severity | Path:line or evidence | Statement |
|---|---|---|---|
| GS-1 | Minor | `Assets/IdleFactory/Unity/UI/Panels/OfflineSummaryPanel.cs:420` | The efficiency percent is computed in presentation: `EfficiencyNumerator * 100 / EfficiencyDenominator`. The domain exposes only the numerator and denominator (`RunOfflineSummary.cs:234-237`). The value is 80 % under the only supported policy (4/5, enforced by `DefinitionCatalog.cs:10-11` and `:135-136`), so the displayed number is correct today. It is still a number derived in presentation; if the policy ever changes, the rule lives in the wrong layer. Move the percent to a domain property or keep it as a recorded accepted Minor. |
| GS-2 | Minor | `Core/Run/Application/AutosavePolicy.cs:43, 46`; `RunPersistence.Autosave.cs:137`; `tasks.md:786` | The T559 periodic checkpoint (30 s, plus 2 s spacing for structural changes) changes what a force-stop costs the player. Offline accounting credits from the last checkpoint, so the credited window after a force-stop shrinks from up to the last departure to up to 30 s. The formula is unchanged (commit `4f5b5efa` says so), and the owner decided this on 2026-11-18. The balance runner (Channel A) does not model autosave, and the Play-Tester covered only a 20 s real-time return (`playtest-agent-f006-final.md` section 2.6). The behaviour change is therefore unmeasured. |
| GS-3 | Minor | `specs/006-world-first-factory-experience/tasks.md:785-786`; `spec.md` (no "autosave" text) | The periodic autosave requirement exists only in the tasks.md T559 block. spec.md has no autosave requirement. T559 cites `FR-003`, but spec.md FR-003 says presentation-only state must not be persisted, which is a different requirement. FR-045 is the UI/UX review rule. The requirement needs a spec.md entry or a corrected citation. |
| GS-4 | Info | `AutosavePolicy.cs:41-43`; `tasks.md:786` | tasks.md T559 says the interval is "in the run settings, default 30 s". The code keeps the default in the class (the comment names it the source of truth), and the run content files are unchanged. This is a deliberate deviation from the task text, not a value change. |
| GS-5 | Info | `Assets/IdleFactory/Tests/EditMode/F006/UpgradeMarkerTests.cs:63-81` | The slot-to-presentation parity test covers only `UpgradeAffordable = true` across 4 blockers x 3 phases x active. Unaffordable cases are tested on each model separately (`UpgradeMarkerTests.cs:83`, `MachinePresentationModelTests.cs:135`). The test sets the affordability flags directly; the `cash >= cost` derivation is not part of the parity check (it is tested in the domain layer, which I did not re-read). |
| GS-6 | Info | `RunSnapshots.Items.cs:430` (ResearchCost), `:439` (CanPurchaseResearch) | The refactor changes behaviour only for malformed content: a level outside the cost table now yields 0 and "not purchasable" instead of an index exception. Valid content is unchanged (same values, same ordering). |
| GS-7 | Info | `Core/Run/Application/RunSession.cs`, `RunSimulator.cs` (T514 `059845bb`) | Additive only (`CompletedCycles` read value and tests). No economy number changed. Removed lines in the whole Core/Unity diff contain no economy constant (scan of `-` lines). |

#### Item results (lens checklist)

1. **Channel A economy diff (PASS).** The committed economy record `specs/005-economy-contracts/reviews/strategy-baseline-r3.md` is byte-identical between `c568d24` and `945b1557`. No file under `specs/005-economy-contracts/`, `Content/`, `Core/Run/Content/`, the economy domain files or `Tests/EditMode/Balance/` changed in that range. The range includes the gate-C commit, so the claim "no economy change vs gate C" holds for the committed record. The playtest's "differs only in build commit, wall times and projection lines" statement refers to a scratch diff I could not read; I did not verify it.
2. **Production, upgrade, reward and offline numbers (PASS with GS-2).** No economy data or pricing file changed in the range. The only new numeric defaults are the autosave constants (GS-2, spec-decided per tasks.md T559). The research and Overdrive changes preserve values (GS-6).
3. **Upgrade-marker affordability parity (PASS).** Domain: `affordable = costOk && cash >= cost` (`RunSnapshots.Factory.cs:426`), the same `TryUpgradeCost` rule used by `RunCommands.Factory.cs:215` for the actual purchase. Presentation: presentation model `Available && Affordable` (`MachinePresentationModel.cs:324`); slot model `hasUpgrade && Affordable`, with `hasUpgrade` also excluding `ValueLimit`, which is redundant because `affordable` implies `costOk` (`SlotVisualModel.cs:158-160`). Both marker rules add `!IsProblemState`, matching FR-027 ("unobtrusive, lower priority than problem states"). The parity test exists (`UpgradeMarkerTests.cs:63`, GS-5 for its limit).
4. **Offline-summary values from the domain (PASS with GS-1).** Cash before, after and delta, sales income, completions, stock deltas, credited and effective ticks, and the efficiency ratio all come from `RunOfflineSummary`. Presentation does unit conversion from ticks to ms (`OfflineSummaryPanel.cs:409-411`, display only), the sign of the cash delta, the 1-second display cut-off (`:413-416`, T451 display rule), and the percent (GS-1). The idle classification in `IsIdle` (`:395-398`) reads domain fields and only chooses the wording.

#### Conditions for acceptance

1. GS-3 closed: spec.md gains an FR for periodic autosave (with its owner decision date), or tasks.md T559 cites the correct FR. Checkable by `grep -i autosave specs/006-world-first-factory-experience/spec.md` returning a requirement line, or by the corrected citation in tasks.md.
2. GS-2 recorded: final-acceptance.md names the autosave change as an accepted behaviour change (owner decision 2026-11-18), noting that offline credit after a force-stop is bounded by the 30 s checkpoint, and that no balance-runner or real-time measurement covers it.
3. GS-1 disposed: either the percent moves to the domain, or the finding is recorded as an accepted Minor.

#### Counts

- Blocker 0
- Major 0
- Minor 3 (GS-1, GS-2, GS-3)
- Info 4 (GS-4, GS-5, GS-6, GS-7)
- Conditions 3

#### Residual risk (owner decisions noted, not re-filed)

- Store-full and marker-cap items (playtest F-1) are presentation rules, not economy, and are outside this lens.
- The Channel A scratch diff (`test-results/t543pt/baseline.diff`) was not readable from the repository; the verdict on item 1 rests on the committed record.

## 5. T549 owner visual acceptance

**Date**: 2026-11-26. **Commit judged**: main `ac49f6c8` (final F006 main, PR #791). **Evidence**: `visual-acceptance-f006.md` and the sheets in `visual-acceptance-f006/sheets/` (merged into the coordination branch at `c3e0e41c`). A capture agent (Test/QA, Asset Artist lens, sonnet) produced the evidence and gave no verdict. The verdicts below are the owner's, given in the parent session.

| Method | Coverage |
|---|---|
| Rig | The T502 `BaselineRig`. 10 classes x 100/200 % x 7 states. The before and after pairs are like for like. |
| Windows player, real scene | The stage triptych and the P3 stills for 8 classes. |
| Real APK on AVD `IdleFactory_Test_API_36` | Portrait and landscape x 100/200 % x 7 states. Also the stage triptychs, reduced motion, the look points and the play-through stills. |

### 5.1 Verdicts per surface and screen class

| Surface | Phone classes (emuP, emuL, 360x640, 640x360, 390x844, 844x390) | Tablet (768x1024, 1024x768) | Desktop (1280x720, 1920x1080) |
|---|---|---|---|
| No panel | Accepted (100 % and 200 %) | Accepted | Accepted |
| Card | Accepted | Accepted | Accepted |
| Details | Accepted | Accepted | Accepted |
| Building (build card + ghost) | Accepted | Accepted | Accepted |
| Hub (Office) | Accepted | Accepted | Accepted |
| System view (Contracts) | Accepted | Accepted | Accepted |
| Offline summary | Accepted | Accepted | Accepted |
| 200 % text | Accepted | Accepted | Accepted |
| Stage triptych (FR-036, no before) | Accepted (device portrait and landscape; Windows 360x640, 640x360, 390x844, 844x390) | Accepted (Windows) | Accepted (Windows) |
| Reduced motion | Accepted. Measured on the device, portrait 100 %, no panel: 3,150 and 4,410 changed pixels with motion; 0 and 0 with reduced motion On. | Not captured separately. The same setting applies. | Not captured separately. The same setting applies. |

The owner accepts the play-through stills as evidence of the GD/PO moments, with the known limits:
- **Upgrade**: a level-25 smelter on the W003-S set. It is not the first upgrade of a fresh run.
- **Hand-delivered contract**: "Delivered 167" on the W005-L checkpoint.
- **Real offline returns**: cold starts of pushed checkpoints on the device.

### 5.2 Look points (section 2 rows 2 and 8, C-108)

| Look point | Evidence | Disposition |
|---|---|---|
| Smallest phone (360x640, 640x360): the upgrade marker at about 24 pt | `lookpoints-smallest-phone.png`, `p3-stills-phone-*` | Accepted |
| Deck edge and void | Triptychs and device sheets | Accepted for F006. The art task follows after P4 (owner decision 2026-11-26; post-gate Issue). |
| "Development Build" label over Confirm (640x360, 200 %) and over Overdrive (360x640) | `lookpoints-smallest-phone.png` | Accepted. Development builds only. |
| Tablet hint wrap | `lookpoints-tablet-hint.png` | Accepted |
| 48-pt touch-target overlay crop | Not captured. Play Mode tests remain the evidence; the device reads 45 to 47 dp (C-108). | Gap accepted by the owner |
| Live stage change | Not captured: no fixture crosses a threshold. The stages are shown by the triptychs. | Gap accepted by the owner |

### 5.3 Guidance measurements versus the baseline (owner: acceptable for F006)

Re-measured on this run by the T502 probe. The values equal the T542 section of `baseline.md`; the full tables are in `visual-acceptance-f006.md`. Each row is owner-accepted as measured, under the spec clarification that these thresholds are guidance only and the owner decision of 2026-11-26.

| Guidance | Baseline to after | Status |
|---|---|---|
| SC-001 world share >= 60 % | Met at 1920x1080, 390x844, 844x390, 360x640 and emulator portrait/landscape at 100 %. Not met at 640x360, tablet and 1280x720. | Accepted as measured |
| SC-001 chrome at 100 % <= 15 % | Met at tablet, desktop and emulator portrait. Not met on the other phone classes. | Accepted as measured |
| SC-001 chrome at 200 % <= 20 % | Not met on any phone class: after 21.8 to 55.8 %, baseline 27.4 to 62.0 %. Tablet and desktop meet it. | Accepted as measured |
| SC-002 unobscured share with the card open | Rig probe (docked card), after vs baseline: phone portrait 0.55 to 0.82 (baseline 0.45 to 0.53), phone landscape 0.75 to 1.03 (baseline 0.53 to 0.71). Every phone class meets 50 % portrait and 70 % landscape on this probe. `baseline.md` T542 records 44 to 51 % (portrait) and 66 to 67 % (landscape) by its own method. | Accepted as measured |
| SC-003 upgrade | 3 steps vs 2 | Accepted |
| SC-003 stopped-machine category | 4 steps vs 0 | Accepted |
| SC-003 other journeys | One stopped machine: 1. System views: reach 2, return 1 (3 counted). Build: 3. | Met |
| SC-010 persistent labels | 13 to 10 vs 6, on every class and scale | Accepted |

The emulator FPS budgets 1 to 3 and the missing physical-device data (PERF-9) stay open under the SC-008 deviation in section 3. They go to the C-68 and device-measurement Issue.

TESTQA C4: no scripted emulator journey reaches the `ProducingFps` threshold on this host. The Default, F003 and F005 journey runs predate T561 and T563. The journey frame data is therefore environmental and stale for those scenarios, and it is not F006 evidence; owner-accepted under the same deviation.

### 5.4 A-9 limitation

A-9 (p3a-followups): the Android development APK was 82 MB with 46 MB of dead zip entries, and `adb install` failed on the near-full emulator.

The T549 build of `ac49f6c8` (`BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`) measures 46,723,142 B, which is under the F004 APK budget of 47.3 MB. It installed on the AVD without error. The capture APK (with test assemblies) measures 48,636,807 B.

The owner accepts A-9 as resolved for F006. Remaining limitation: there is no release-build size measurement in this record (see C-109 for the release-build checks).

### 5.5 Verdict

**T549 PASS (owner, 2026-11-26)**: visual acceptance is given for every surface on every captured screen class. Conditions 2 and 8 of section 2 are satisfied as far as T549 is concerned. The art and record parts of row 8 go with the post-gate Issues.

Separate result in the same session: `scripts/test-unity.ps1 -StandaloneWindowsPlayer` on `ac49f6c8` passed 85/85.
