# Final acceptance (T446, T447)

Issue #564 (T446) records the independent final reviews of Feature 004 Visual Identity. The reviewed commit is `1838f22` (origin/main, 2026-11-05). Eight read-only lens reviews were each run by a separate agent that implemented none of the work. T447 (owner visual acceptance) will append its own section later. Pending in parallel: T444 (performance B3) and T445 (SC-001/SC-002 readability gates).

## 1. Summary

| Lens | Verdict | Blocker | Major | Minor | Info | Conditions (count) |
|---|---|---|---|---|---|---|
| Game Designer / Product Owner | ACCEPT WITH CONDITIONS | 0 | 0 | 5 | 3 | 4 |
| Software Architect | ACCEPT WITH CONDITIONS | 0 | 0 | 3 | 3 | 2 |
| Test/QA | ACCEPT WITH CONDITIONS | 0 | 1 | 4 | 4 | 3 |
| UI/UX Reviewer | ACCEPT WITH CONDITIONS | 0 | 0 | 6 | 2 | 3 |
| Art Director | ACCEPT WITH CONDITIONS | 0 | 1 | 3 | 1 | 2 |
| Technical Art | ACCEPT WITH CONDITIONS | 0 | 0 | 3 | 4 | 3 |
| Security and Privacy | ACCEPT | 0 | 0 | 3 | 7 | 0 |
| DevOps and Operations | ACCEPT WITH CONDITIONS | 0 | 1 | 4 | 3 | 3 |

Counts are taken from each report's findings table. The Game Designer / Product Owner report's own count line reads minor 4 and info 4; its table lists G1-G5 as minor and G6-G8 as info, so the table counts are used.

## 2. Conditions before feature completion

1. **Game Designer / Product Owner 1** T445 records `reviews/readability-final.md`: SC-001 zero confusions in grayscale and 3 CVD filters on the shipped build, including n=2 sheets for 1280x720 AND 720x1280 (G1); SC-002 >= 9/10 within 2 s with motion, plus the reduced-motion variant (G2). Objectively checkable by file existence and the recorded counts.
2. **Game Designer / Product Owner 2** T447 `reviews/final-acceptance.md` records the owner's per-family acceptance and a human-finger pass covering pinch and edge taps (G3) and a rotation check for repeated Welcome-back (G4).
3. **Game Designer / Product Owner 3** T444 B3 meets the frozen budgets (SC-007/FR-025); this lens treats it as pending, not failed.
4. **Game Designer / Product Owner 4** T448 sets spec status and ticks checkboxes (G7).
5. **Software Architect 1** Docs: update contracts/render-foundation.md (two commits, PR #583/b9221ca, comparator note) and presentation-and-state-cues.md (enum and Sorting Groups now exist) in the batch close-out. Checkable: `grep -n "one self-contained commit" specs/004-visual-identity/contracts/render-foundation.md` returns nothing or carries an "as built" note.
6. **Software Architect 2** Rollback residual (A-2) is recorded as an accepted residual or closed by a dry-run in T448/T449. Checkable: a sentence in tasks.md/learn.md or review record naming `m_SpritePackerMode` and the final-merge revert as accepted/verified.
7. **Test/QA 1** QA-1: a committed list (e.g. `reviews/rebaseline-names.md`) names every removed Edit/Play test case and its replacement or reason, derived by `diff` of the two names files; checkable by counts 3 and 30.
8. **Test/QA 2** QA-6: T447 owner checklist includes a human two-finger pinch and slow-pan check on a device, or the owner records in closeout that FR-016 panning and FR-017 pinch stay unverified.
9. **Test/QA 3** T444 (SC-007/FR-025/FR-027) and T445 (SC-001/SC-002) complete with passing evidence before the feature is declared complete; both are pending, not failures.
10. **UI/UX Reviewer C1** Art Bible section 4.3 and DEV-4 text list the portrait 720x1280@320dpi phone with n_min 2 / 22.95 pt / 14.97 pt (check: grep docs/art/F004-art-bible-v1.md for "720x1280").
11. **UI/UX Reviewer C2** Closeout (T448/T449) records R-21 consequence U-1 (zoom hint and gear unannounced to TalkBack) and the unverified real-touch gaps (U-6).
12. **UI/UX Reviewer C3** T447 owner acceptance includes one real-finger pass on a phone: tap a slot at n_min, pinch zoom settle, and the Settings sheet at OS font scale 2.0.
13. **Art Director C1** Art Bible section 4.3, DEV-2, DEV-4, section 4.1 row 1, AR-2 and section 20 are updated (patch v1.0.4 citing the owner decision of 2026-11-05) in the batch close-out; checkable by `grep -n "34.4 pt" docs/art/F004-art-bible-v1.md` returning no portrait-320dpi "meets" row and `grep -n "1.0.4" docs/art/F004-art-bible-v1.md` returning the revision row.
14. **Art Director C2** T445 and T447 (pending) must confirm blind naming and visual acceptance at 1280x720 n = 2 and portrait 720x1280 n = 2 (DEV-3 marker rule), since the portrait phone is now a DEV-2 class.
15. **Technical Art C1** T444 (B3) must commit measured values for budget rows 6 (world-art texture memory <=8 MB), 7 (atlas pages <=1024x1024), 8 (SetPass held at B2 0.77-0.78 with floor and restyled art), 10 (clean-build APK <=150 MB) and 11 (shader variants <=490 or explained); each checkable in `performance-budgets.md` section 4.
16. **Technical Art C2** After the final Android/B3 build, `git diff` of `UniversalRenderPipelineGlobalSettings.asset` shows only the known one-line `rid` change (or is explained).
17. **Technical Art C3** B3 records the floor tile count per screen class (TA-2) so the 2048 cap and pool size are shown to be unreached.
18. **DevOps and Operations C1** T444 records an OPS-7 dry-run on a scratch branch that reverts the final URP merge with `-m 1`, compiles, runs RenderParityTests, and asserts `EditorSettings m_SpritePackerMode`. Evidence file: specs/004-visual-identity/reviews/performance-budgets.md section 3 updated, or reviews/performance-b3.md. Scratch branch not pushed (`git ls-remote` shows no such branch).
19. **DevOps and Operations C2** T444 records OPS-4 (two full Play Mode runs with compile time and duration) in reviews/performance-b3.md or performance-budgets.md section 3.
20. **DevOps and Operations C3** Before T448 closeout, the follow-ups F2, F3 and F4 are filed as Issues or listed in the closeout note, so they are not lost.

Security and Privacy has no conditions (verdict ACCEPT); its two optional checks remain in its report in section 4.

## 3. Owner decisions referenced

R-21 accepted (TalkBack sees only one SurfaceView). The 14 Feature-003 text-only buttons stay without icon. 2026-11-05: F1 — DEV-2/DEV-4 extend to the portrait 720x1280 phone at 320 dpi (n_min 2: 22.95 pt world cue, 14.97 pt digit); Art Bible §4.3 is corrected in the batch-12 close-out. F2–F4 from T442 are recorded as accepted gaps: no `RestyledSceneSmokePlayerTests` (covered in substance by the 62 tagged player cases), missing real-shell n assertions for several screen classes, and the fixed-coordinate Welcome-back dismissal in the `F004Crispness` step of `scripts/android-emulator-journey.ps1`; no new tasks.

## 4. Lens reviews

### Game Designer / Product Owner — verdict: ACCEPT WITH CONDITIONS
The restyle delivers the stated value with no gameplay, content or save change, and all automated gates are green. The P1 player-readability gates (SC-001 final, SC-002 final) and owner acceptance are still pending (T445, T447), so acceptance is conditional on them.

### Scope reviewed
- Commit: origin/main `1838f22` (worktree `.maqa-worktrees/issue-564-t446-final-reviews`). Read-only; no tools beyond git/grep.
- Read: `specs/004-visual-identity/spec.md` (stories, FR, SC, exclusions), `tasks.md` (T445-T449 text, open boxes), `reviews/final-test-evidence.md` (T442), `readability-spike.md`, `art-bible-review.md` (summary), `art-direction-review.md` (summary table), `polish-review.md`, `settings-review.md`, `ui-icon-integration.md`, `style-spike.md` (owner decisions), `performance-budgets.md` (header), `phase0-review.md` (verdicts), `wiki/INDEX.md`; `docs/agents/roles-and-reviews.md` (role section).
- Check: `git diff --stat cc0aa79 HEAD -- Assets/IdleFactory/Core Assets/IdleFactory/Content` prints nothing (cc0aa79 = F004 main-drift merge) and `git log cc0aa79..HEAD` on those paths is empty; no Assets file deleted since then. Persistence lives under Core.

### FR/SC coverage (product level)
| FR/SC | Status | Evidence |
|---|---|---|
| FR-001 gameplay, economy, content, save unchanged | met | Empty diff on `Core/` and `Content/` since `cc0aa79` (command above); Edit 2566 / Play 2572 green (`final-test-evidence.md` s1) |
| FR-002/003, SC-003 keys keep ids, all visuals restyled | met (QA read pending) | `F004AssetContractTests` +49, `UiIconIntegrationEditTests` all 50 UI keys; `art-direction-review.md` s1 (all families approved, with conditions) |
| FR-004-006, SC-008 phase 0 | met | `phase0-review.md:22,36` APPROVE-WITH-FOLLOW-UPS; `TheB1PreSwitchBaselinesAreUnchanged` in Play Mode |
| FR-007-009, SC-009 style decided on evidence before production | met | owner decisions `style-spike.md:187` (B, 2026-10-28), `:433` (D24, 2026-10-29); Art Bible sign-off in wiki `f004-art-bible-v1-review-and-signoff.md` |
| FR-010, SC-001 states readable w/o color or motion | partial: spike-level pass, final pending | 6 sets 9/9 zero confusions (`readability-spike.md:11,37`); badges 20/20, markers 8/8 (`art-direction-review.md` s1); shipped-build re-run is T445 |
| FR-011/012, SC-005 fit-all 12 slots, integer zoom | met on tested classes; partial for untested | n_fit >= n_min in `final-test-evidence.md` s7a/7b; real-shell gaps F3 (640x480 etc.); emulator crispness PASS n=4 (s8) |
| FR-013/014/015 silhouettes, contrast, tall art | met | `art-direction-review.md` (32/32 machines), `ui-icon-integration.md` contrast table |
| FR-016/017/018 crisp pixels, snap, 44-pt hit | partial | crispness 100 % on emulator (s8); real two-finger pinch and real-finger tap not verified (s8) |
| FR-019/020/020a, US4 animation, reduced motion, Settings | met | `ReducedMotionTests` in player 62/62; `settings-review.md:15` ACCEPT WITH FOLLOW-UPS (R-21 accepted) |
| FR-021-024, SC-004 provenance, checks, fallback | met (reviewedBy fill is a closeout item) | pipeline 220 PASS on exact commit (s2); `ArtFallbackTests` 5 in player |
| FR-025-027, SC-007 60 FPS, no allocations, budgets | pending (T444) | `PresentationAllocationTests` 8 in player; APK 46,389,823 B vs cap 47,345,615 B (0.96 MB headroom); B3 not run |
| FR-028 wiki decisions recorded | met | `wiki/INDEX.md` lines 81-99 (F004 pages); mandatory hooks ran per recent commits |
| FR-029-033, SC-010 F005 follow-ups | met | `polish-review.md` s2 all Accept |
| SC-002 owner timed trial | pending (T445) | spike round 2 passed 10/10 median 1.077 s with motion (`art-bible-review.md:162`); reduced-motion not yet measured |
| SC-006 F003/F005 suites unchanged | met, name-level diff review left to QA | s9; 16+13+1+3 renamed/removed re-baselined names |
| Exclusions (no new machines/content, no economy tuning, no sound, local tools) | respected | Empty Core/Content diff; wiki F004 pages list art tooling only |

### Findings
| ID | Sev | Finding | Evidence | Disposition |
|---|---|---|---|---|
| G1 | minor | T445's SC-001 sheet scope names only "1280x720 at n = 2". Owner decision F1 (2026-11-05) puts portrait 720x1280 @320 dpi at n_min 2 too (22.95 pt cue, 14.97 pt lock digit), a common phone class. Without sheets at that size, blind naming is unproven where cues are smallest. | `tasks.md:305`; `final-test-evidence.md` s7d F1 | fix now: add 720x1280 portrait n=2 to T445 sheets |
| G2 | minor | Running = "no cue" is only proven with motion on. Art Bible review R6 asked for a reduced-motion SC-002 variant; it is in T445 text but no result exists. Player consequence: with reduced motion a still running machine and a healthy idle one differ only by body variant. | `art-bible-review.md:22,162`; `tasks.md:305` | accept as pending; must be measured in T445 |
| G3 | minor | At n_min=2 on a 320 dpi phone the 90x45 hit diamond is 44.9 pt (inference: 45 art px x 2 / 2.0045), a 0.9 pt margin over the 44-pt rule; on the 420 dpi emulator neighbouring diamonds overlap at the 70 px pitch and the nearest centre wins (tap 115 px off-centre opened slot 6). Real-finger usability unverified. | `final-test-evidence.md` s8 carry-forward table | follow-up: include a human finger tap/pinch pass in T447 |
| G4 | minor | The emulator journey saw a second Welcome-back sheet after the rotation steps (attributed to the harness). If the offline summary re-appears on rotation, that is a player-visible annoyance; not shown to be a harness-only effect. Inference. | `final-test-evidence.md` s8 (run 3), F4 | follow-up: check by hand during T447 whether a rotation re-shows the sheet |
| G5 | minor | Player-visible icon readability remains weaker than the spec's "legible" promise (US5): 8-pt pin hint reads as a missing icon, CASH/PRESTIGE and INDUSTRY/SETTINGS confusion, 36/50 blind naming. Already tracked; consequence is that labels, not icons, carry meaning on those surfaces, which is acceptable because text stays beside them. | `ui-icon-integration.md` findings; `art-direction-review.md` s1; Issue #555 | accept (tracked) |
| G6 | info | US5 scenario 1 says "all icons use the new style"; 14 text-only F003 buttons stay without icon by owner decision, so no surface mixes styles. | `ui-icon-integration.md` "Not bound" list | accept |
| G7 | info | `spec.md` header still reads `Status: Draft`; spec lists SC-010 beyond SC-001-SC-009; `tasks.md` boxes (e.g. T433, T441, T442, T455, T470, T471) are unticked although merged. | `spec.md:7` area; `tasks.md:263-302` | fix at T448 (checkbox sync) and set spec status |
| G8 | info | F2: planned `RestyledSceneSmokePlayerTests` never written; journey is covered in substance by 62 tagged cases and the emulator journey. Owner recorded this. | `final-test-evidence.md` s4 | accept |

No blocker or major findings. Counts: blocker 0, major 0, minor 4 (G1-G4), info 4 (G5-G8; G5 tracked as #555).

### Conditions for acceptance
1. T445 records `reviews/readability-final.md`: SC-001 zero confusions in grayscale and 3 CVD filters on the shipped build, including n=2 sheets for 1280x720 AND 720x1280 (G1); SC-002 >= 9/10 within 2 s with motion, plus the reduced-motion variant (G2). Objectively checkable by file existence and the recorded counts.
2. T447 `reviews/final-acceptance.md` records the owner's per-family acceptance and a human-finger pass covering pinch and edge taps (G3) and a rotation check for repeated Welcome-back (G4).
3. T444 B3 meets the frozen budgets (SC-007/FR-025); this lens treats it as pending, not failed.
4. T448 sets spec status and ticks checkboxes (G7).

### Software Architect — verdict: ACCEPT WITH CONDITIONS
Boundaries, data ownership, render foundation and fallback design match the contracts; two documentation/evidence conditions remain (rollback sprite-packer assertion, contract text drift). Spot-check review, not an exhaustive code audit.

### Scope reviewed
- Commit 1838f22 (worktree issue-564-t446-final-reviews), read-only.
- specs/004-visual-identity/contracts/render-foundation.md, presentation-and-state-cues.md; reviews/phase0-review.md, reviews/performance-budgets.md (rollback sections).
- Assets/IdleFactory/Core and Unity asmdefs, Editor asmdef, Unity/Presentation (PresentationSpriteCatalog, StateCueSet, SlotGridView, PresentationAnimationClock), Unity/Persistence/DisplayPreferencesStore, Unity/Bootstrap reduced-motion signal files, Assets/link.xml, Rendering/ assets, ProjectSettings/GraphicsSettings.asset, ProjectSettings.asset.
- docs/agents/roles-and-reviews.md (Software Architect section). Not read in depth: Blender/ComfyUI pipeline code, tests, learn.md.

### FR/SC coverage (architecture lens)
| FR/SC | status | evidence |
|---|---|---|
| FR-004–FR-006 / SC-008 (URP switch, Gamma, rollback) | met (rollback sprite-packer item partial, see A-2) | ProjectSettings/GraphicsSettings.asset:49 (custom pipeline set); ProjectSettings/ProjectSettings.asset:49 `m_ActiveColorSpace: 0` (Gamma); Assets/IdleFactory/Rendering/ (URP, 2D renderer, global settings, volume profile); reviews/phase0-review.md:27,65-66,76 |
| FR-002/FR-003 (catalog, fallback frames) | met | PresentationSpriteCatalog.cs:34 `fallbackFrames`, :249-250 codes `presentation.art.fallback` / `presentation.key.unresolved`, :262-264 registry semantics |
| FR-010/FR-013 (single state cue, no second precedence rule) | met | SlotGridView.cs:155-185 `BadgeFor` delegates to `StateFor` (single precedence); StateCueSet.cs:9-12,74 maps `MachineVisualState` to rows keyed by BadgeFor keys |
| FR-019/FR-020/FR-026 (shared clock, reduced motion) | met (inference from structure, no allocation measurement redone) | Presentation/PresentationAnimationClock.cs (no `Update`/`LateUpdate` in any Presentation file, grep) |
| FR-024 (fallback stays interactive) | met by design | catalog fallback path above; covered by T442 tests per final-test-evidence (not re-run) |
| Display preferences store location/atomicity | met | Persistence/DisplayPreferencesStore.cs (`IDisplayPreferencesFileSystem` seam, `Preferences/display-v1.json` path, separate from Checkpoints/SessionLog files) |
| Signal isolation (R11) | met | Bootstrap/ReducedMotionSignal.{Android,Windows,Default}.cs, IReducedMotionSignal.cs; Assets/link.xml preserves only JNI types and `ReducedMotionSignal` with rationale |

### Findings
| ID | Severity | Finding | Evidence | Disposition |
|---|---|---|---|---|
| A-1 | info | Dependency direction is clean: `IdleFactory.Core` has `noEngineReferences: true` and no references; `IdleFactory.Unity` references only Core (+ InputSystem); Editor asmdef is editor-only, `autoReferenced: false`. No code references the URP assembly; only `SlotGridView` uses `UnityEngine.Rendering` (Sorting Group, core engine), so the render pipeline stays a project-settings concern and rollback needs no code change. | Core/IdleFactory.Core.asmdef; Unity/IdleFactory.Unity.asmdef; grep `UnityEngine.Rendering` in Assets/IdleFactory/**/*.cs | accept |
| A-2 | minor | Rollback evidence is incomplete versus R01: the dry-run was not repeated in the final evidence and the sprite-packer mode (`EditorSettings m_SpritePackerMode`) revert assertion plus a revert of the final merge are explicitly "still not asserted". The procedure (`git revert -m 1 b9221ca`) is documented and the revert is structurally limited to settings/Rendering/manifest, so the risk is documentation-level. | reviews/performance-budgets.md:95, :123; reviews/phase0-review.md:66,76 | follow-up: record as accepted residual in T448/T449 close-out (or one scratch-branch dry-run when the editor lease is free) |
| A-3 | minor | Contract text drift: render-foundation.md says "one self-contained commit" and "no test expectation changes in phase 0"; reality is two commits merged as PR #583 (merge b9221ca) with a comparator change, and the contract still names a single commit. presentation-and-state-cues.md still says `SlotGridView` "uses only `sortingOrder` values" and that T429 "may" introduce a `MachineVisualState` enum; the code now has the enum and Sorting Groups. Contracts are not the current truth for readers. | contracts/render-foundation.md (Change set, Rollback); reviews/phase0-review.md:65; SlotGridView.cs:163 `StateFor`, :5 `using UnityEngine.Rendering` | fix now (docs only) in batch close-out, alongside the Art Bible §4.3 correction already decided |
| A-4 | minor | Evolution cost: PresentationSpriteCatalog.cs (36.7K) and SlotGridView.cs (53.4K) bundle registry, validation, fallback, diagnostics and view construction. Fine for F004, but any next feature touching presentation keys pays a large-file review cost. Inference from file size and method density, not a defect. | Assets/IdleFactory/Unity/Presentation (file sizes) | follow-up (post-F004 refactor candidate, e.g. split catalog registry from catalog data) |
| A-5 | info | Data ownership holds at the surface checked: `PresentationSpriteEntry` serialized fields (frames, fallbackFrames, placement) are private with read-only list accessors (`FallbackFrames` returns `IReadOnlyList`); runtime writes in `SlotGridView` set only `SpriteRenderer.sprite`, never the catalog or entries. Preferences are written only through the store, never into the save roots. | PresentationSpriteCatalog.cs:31-35,100-103; SlotGridView.cs:692,1126-1149; DisplayPreferencesStore.cs | accept |
| A-6 | info | Known items with architectural flavor, no new action: #656 (camera clear color, one-frame LateUpdate lag, harness lacks FloorCamera) and #555 (`StartupStatusPanel.ApplyReducedMotion` with no production caller; reduced motion reaches the world via the shared clock, but that panel path is dead code until wired). | Issues #656, #555 | accept as recorded |

### Conditions for acceptance
1. Docs: update contracts/render-foundation.md (two commits, PR #583/b9221ca, comparator note) and presentation-and-state-cues.md (enum and Sorting Groups now exist) in the batch close-out. Checkable: `grep -n "one self-contained commit" specs/004-visual-identity/contracts/render-foundation.md` returns nothing or carries an "as built" note.
2. Rollback residual (A-2) is recorded as an accepted residual or closed by a dry-run in T448/T449. Checkable: a sentence in tasks.md/learn.md or review record naming `m_SpritePackerMode` and the final-merge revert as accepted/verified.

### Test/QA (risk-based matrix) — verdict: ACCEPT WITH CONDITIONS
Every FR has automated or recorded evidence on the exact green suite (Edit 2566 / Play 2572 / player 62 / pipeline 220), but SC-006 lacks a name-level re-baseline list, and several real-shell/real-pointer gaps remain (known F3, pinch) with no recorded owner decision on them.

### Scope reviewed
- Commit `1838f22` (worktree issue-564-t446-final-reviews). Tested code commit in T442: `701e8c8`; inference: 1838f22 only adds the T442 evidence merge (#666).
- `specs/004-visual-identity/`: spec.md (FR/SC), quickstart.md, reviews/final-test-evidence.md, final-test-names.txt (5141 lines, grep by class), tasks.md (open boxes), polish-review.md (grep).
- `docs/agents/roles-and-reviews.md` "Test and QA Engineer" (l.56-61). `Assets/IdleFactory/Tests/**` grep for Explicit/Ignore.

### FR/SC coverage (evidence = test class in final-test-names.txt unless a path is given)
| FR/SC | status | evidence |
|---|---|---|
| FR-001, FR-002, SC-006 | partial | full suites green; `RenderParityTests.TheB1PreSwitchBaselinesAreUnchanged` (names:3989). Name-level list of 30 removed Play / 3 Edit cases not produced (final-test-evidence.md §3, "QA's job") |
| FR-003, SC-003 | met | `F004AssetContractTests` (49 new), `PresentationSpriteCatalogTests`, `UiIconIntegrationTests` (48), floor key/import tests (`TheFloorKeyIsRegisteredInTheSceneCatalog...`) |
| FR-004–FR-006, SC-008 | met (earlier gates) | `RenderParityTests`, `RenderPipelineConfigurationTests`; B1/B2 in reviews/baselines.md; not re-run at final beyond suites |
| FR-007–FR-009, FR-021–FR-022, SC-009 | met (documents) | style-spike.md, art-bible-review.md; no code test (n.a. for tests) |
| FR-010, SC-001 | partial | `StateCueSetTests`, `StateCueOverlayTests` (48) prove structure; the "named by independent reviewer" part is T445 (pending, not a failure) |
| FR-011, SC-005 | partial | `CameraScaleModelTests` (37), `ScreenClassScalePlayTests` real shell for 8 devices; fixed-HUD model only for 640x360/360x640/390x844/844x390/768x1024/1024x768; none for 640x480 (F3) |
| FR-012 | met | re-baselined `SlotGridTests`/`NavigationInputTests`/`MachinePanelGuidanceTests`; list by name outstanding (see QA-1) |
| FR-013, FR-014, FR-023 | met | pipeline suite 220 (`tools/art-pipeline/tests/test_checks.py`, `checks.py` IoU/contrast), exact commit `report.json` pass |
| FR-015 | met | `TheFootprintHitAreaFollowsTheSlotRectNotTheArt`, `StateCueOverlayTests` |
| FR-016 | partial | `PixelCrispnessTests` (6, in player too) + emulator crispness PASS (n=4, 100 % uniform blocks, evidence §8). "No shimmer during slow panning" has no test or capture (inference from grep: no pan-shimmer case) |
| FR-017 | partial | model + wheel/pinch cases (`CameraScaleModelTests`, 14 Pinch / 9 Wheel names) and zoom-limit hint on device; real two-finger pinch NOT verified (evidence §8) |
| FR-018 | met / partial | `ASlotFootprintIsAtLeast44ByPointsAtEveryAllowedScale`; real-finger tap on 90x45 diamond not verified |
| FR-019, FR-020, FR-020a | met | `ReducedMotionTests` (4, player), `ReducedMotionBootstrapTests`, `DisplayPreferencesStoreTests` (72, corruption cases), `SettingsSheetTests` (17: gear 44 pt, Esc/back focus return) |
| FR-024 | met | `ArtFallbackTests` (5, in player) |
| FR-025, FR-027, SC-007 | pending | T444 (B3); APK size 46,389,823 B vs cap 47,345,615 B (evidence §2) |
| FR-026 | met | `PresentationAllocationTests` (8), 600-frame player run |
| FR-028 | met (document) | wiki pages; not test-covered |
| FR-029–FR-033, SC-010 | met (inference from names + polish-review.md) | `MachinePanelGuidanceTests` (90), `OfflineSummaryPanelTests`, `UiReviewFixesTests` offer ordering; I did not open each test body |
| SC-002 | pending | T445 |
| SC-004 | met | pipeline 220 + `F004AssetContractTests` |

Quickstart scenarios: S1 met (B1/B2 + suites); S2 met (documents); S3 met with F3 caveat; S4 partial (T445 pending; overhang case present); S5 met; S6 met (`F004AssetContractTests`, pipeline); S7 pending (T444).

### Findings
| ID | sev | finding | evidence | disposition |
|---|---|---|---|---|
| QA-1 | major | SC-006 says re-baselined zoom tests are "listed explicitly". T442 gives only class counts (Edit -3, Play -30 removed names), no name-by-name mapping of removed cases to replacements. | final-test-evidence.md §3 last bullet, §9 SC-006 | fix now: produce the removed-name list and map each to its re-baselined replacement (read-only diff of baselines-b1-test-names.txt vs final-test-names.txt) |
| QA-2 | minor | Known F3 stays a real gap: 640x480 has no n assertion at all; 5 more classes only via a fixed-HUD model; DEV-1 (460 dpi) is model-only. Planned class `RestyledSceneSmokePlayerTests` (F2) never existed; substance covered by 62 tagged cases and the emulator journey. Owner recorded F2-F4 as "record only". Consequence: SC-005 "every screen class" rests partly on a model. | final-test-evidence.md §4, §7a-c, §10 | accept (owner-recorded); state in closeout |
| QA-3 | minor | New consequence for #555 ("blind-naming capture test should be Explicit"): `BalanceTestSupport.cs:38` records that the runner counts a skipped test as a failure, so `[Explicit]` cannot keep a test out of the default run. The fix needs a category or runner change, not just an attribute. | Assets/IdleFactory/Tests/EditMode/Balance/BalanceTestSupport.cs:38 | follow-up on #555 |
| QA-4 | info | No `[Explicit]` tests exist. 11 `Assert.Ignore` sites are Editor-only composition-seam guards (FactoryRuntimeHostTests, RunBootstrapTests, RunDesktopLifecycleTests, RunLifecycleTests, RunPanelCompositionTests, StartupHudTests, TimeScaleSeamTests). Run reports skipped=0 in Editor, so they executed; in the Windows player those classes are not tagged. No ignored F004 test found. | grep in Assets/IdleFactory/Tests; evidence §1 | accept |
| QA-5 | minor | No placeholder-baseline test found: `TheB1PreSwitchBaselinesAreUnchanged` compares to real archived B1 data. But no automated visual regression for the restyled floor/art (RenderCaptureHarness has no FloorCamera, #656); art correctness rests on pipeline checks, device screenshots e01/e02 and owner acceptance T447. Layout-only tests (`RunHudLayoutTests` 60, `TextScaleLayoutTests` 26) prove geometry, not art. | #656; evidence §9 | accept; T447 is the gate |
| QA-6 | minor | FR-016 slow-pan shimmer and FR-017 real pinch (preview, snap, third-finger cancel) have no automated or device evidence. Pointer injection is impossible on the emulator and batch UI Toolkit drops device pointer events. | evidence §8 | accept only with an owner human check in T447; otherwise follow-up |
| QA-7 | info | tasks.md has unticked boxes for closed Issues (T433, T455, T441, T442, T470, T471; GitHub shows #546/#547/#555/#560/#650/#651 CLOSED). Derived data, T448 mirrors them. | tasks.md l.263,264,280,281,287,302; `gh issue list` | accept; verify in T448/T449 |
| QA-8 | info | `StartupStatusPanel.ApplyReducedMotion` is called only from its own file (line 352) and a test (UiIconIntegrationTests.cs:243); FR-020 behaviour for that panel is test-proven but unwired in production (known, #555). | grep | follow-up on #555 |
| QA-9 | info | APK journey: F004Crispness needed manual dismissal (F4); evidence is a hand rerun on the same build, acceptable but not reproducible by script. T442 environment note: removed-name review and shader-variant count (F5) not done. | evidence §8, §10 | accept |

### Conditions for acceptance
1. QA-1: a committed list (e.g. `reviews/rebaseline-names.md`) names every removed Edit/Play test case and its replacement or reason, derived by `diff` of the two names files; checkable by counts 3 and 30.
2. QA-6: T447 owner checklist includes a human two-finger pinch and slow-pan check on a device, or the owner records in closeout that FR-016 panning and FR-017 pinch stay unverified.
3. T444 (SC-007/FR-025/FR-027) and T445 (SC-001/SC-002) complete with passing evidence before the feature is declared complete; both are pending, not failures.

### UI/UX Reviewer — verdict: ACCEPT WITH CONDITIONS
Restyled shell is coherent and usable on all evidenced classes; no blocker. Conditions are record-keeping only (R-21 consequence, DEV-2/4 scope, open evidence gaps), because several lens items rest on rig/emulator evidence, not real-finger or screen-reader evidence.

#### Scope reviewed
Commit 1838f22 (worktree issue-564-t446-final-reviews). Read: spec.md FR/SC, reviews/settings-review.md, polish-review.md (T453, T458, T459), ui-icon-integration.md, final-test-evidence.md (sections 7, 8, 10), PNGs e01, e03, e08 under reviews/final-test-evidence/. Not opened individually: settings-review/*.png, polish-review/*.png, F004-B2 baselines (relied on the reviewers' text; inference where stated). No tests or devices run.

#### FR/SC coverage (UI/UX lens)
| FR/SC | Status | Evidence |
|---|---|---|
| FR-010 state shape cues / text in panel | met (visual); SC-001 gate pending T445 | final-test-evidence/e01 (red arrow cue per blocked machine, shape not colour-only), polish-review.md N-2 (status text) |
| FR-011 fit-all 12 slots, whole-number scale | met | final-test-evidence.md s7a (n_fit per class, asserted); e01, e02 |
| FR-017 zoom settles on levels, buttons step | partial | e03/e04 limit hints verified on device; real pinch NOT verified (final-test-evidence.md s8) |
| FR-018 44x44 targets, keyboard, safe area | partial | settings-review.md layout facts (gear/radios >= 44); real-finger tap on 90x45 hit diamond not verified (s8) |
| FR-019/FR-020 reduced motion + override | met in code/rig; gap on spinner | settings-review.md state table; ui-icon-integration.md (spinner stops); #555 notes no production caller for StartupStatusPanel.ApplyReducedMotion |
| FR-020a gear + Settings sheet | met with follow-ups (T475 #659 closed via #664) | settings-review.md F1-F6, owner decisions |
| FR-014 3:1 / 4.5:1 contrast | met | ui-icon-integration.md tint table (min 3.41) |
| FR-024 fallback keeps slot interactive | n.a. to this lens (QA) | ArtFallbackTests in manifest |
| FR-029..FR-033 / SC-010 | met | polish-review.md s2, T458/T459 re-reviews |
| Text scale 200 % | met on rig, device only via OS font 2.0 on 1080x2400 | polish-review.md s1 limits; settings-review.md device pass |
| SC-005 crisp, all 12 slots | met (emulator crispness PASS, n=4) | final-test-evidence.md s8 |
| 640x480 desktop class | partial | F3 (no n assertion); layout covered by rig captures (polish-review.md) |

#### Findings
| ID | Sev | Finding | Evidence | Disposition |
|---|---|---|---|---|
| U-1 | minor | Consequence of R-21: with TalkBack on, none of the new controls (gear, radios, zoom hint "Maximum zoom"/"Minimum zoom", cue states, text state labels) is exposed; the zoom-limit hint is visual-only (transient toast) so a screen-reader or low-vision player gets no limit feedback. Accepted limit, but F004 adds new non-announced feedback. | settings-review.md F4; final-test-evidence.md s8 TalkBack row | accept (R-21); record in closeout that zoom hint/ gear are unannounced |
| U-2 | minor | DEV-2/DEV-4 now cover portrait 720x1280@320dpi: cue icon 22.95 pt vs 24 pt intent, lock digit 14.97 pt; well under 44 pt chrome rule is not at issue (cues are non-interactive) but readability margin on the most common mid-range phone class is 4.4 % short. Owner decision F1 accepts; Art Bible 4.3 text must actually be corrected. | final-test-evidence.md s7d | accept; condition C1 |
| U-3 | minor | Information hierarchy on landscape 640x360-class with open sheet: sheet takes 50 % width and partly covers slot 4 column; at 1280x720 first-run capture the selected slot stays visible. Acceptable, but "Cost: $78" renders as a bordered chip that looks like a button next to the disabled "Build Assembler" (F003 legacy look). Inference from e08 only. | e08-1280x720-sheet-open.png | follow-up (cosmetic) |
| U-4 | minor | At max zoom the "+" button shows a dark/dimmed face while it holds the focus ring, and the hint toast is small (about 12 pt, bottom-left, near the Mine control). Disabled state is conveyed by dimming only (contrast low); the hint text carries the meaning. | e03-zoom-in-limit-hint.png | follow-up: confirm disabled "+" label still meets 3:1 (not in evidence) |
| U-5 | minor | "Overdrive" caption nearly touches the button border in portrait 1080x2400 ("Overdrive" at the edge) and at 1280x720; at 200 % text the header wraps (accepted T458). Risk of clipping with longer localized strings is not tested. | e01, e08 | accept; info |
| U-6 | minor | Open evidence gaps that bear on lens acceptance: no real-finger pinch/tap (F-section 8), no n assertion for 640x480/360x640/390x844/844x390/768x1024/1024x768 in the real shell (F3), journey script needs manual Welcome-back dismissal (F4: an automated run after an offline gap leaves the factory covered, a player-visible modal that must be dismissed; not a defect). | final-test-evidence.md s7, s8, s10 | accept per owner F2-F4; T447 owner visual acceptance should cover finger tap on a phone |
| U-7 | info | Known #555 items re-confirmed as player-visible: 8 pt pin hint reads as missing icon; INDUSTRY/SETTINGS and CASH/PRESTIGE confusable (CASH keeps label; SETTINGS gear in the HUD has no label, tooltip only, invisible on touch). The gear is distinguishable by position (last in strip). | ui-icon-integration.md | follow-up #555 |
| U-8 | info | Reduced-motion switch lives only behind the gear; the effective/"Not saved" line sits below the radios and can be below the fold on phones (F1) — T475 reportedly addressed; not re-verified here. | settings-review.md F1; #659 | verify in T447 on a phone |

No blocker or major findings. Consistency with F003/F005 shell: tab bar, HUD strip, sheet header/Close pattern, focus return preserved (polish-review.md FR-030, settings-review.md F10).

#### Conditions for acceptance
C1. Art Bible section 4.3 and DEV-4 text list the portrait 720x1280@320dpi phone with n_min 2 / 22.95 pt / 14.97 pt (check: grep docs/art/F004-art-bible-v1.md for "720x1280").
C2. Closeout (T448/T449) records R-21 consequence U-1 (zoom hint and gear unannounced to TalkBack) and the unverified real-touch gaps (U-6).
C3. T447 owner acceptance includes one real-finger pass on a phone: tap a slot at n_min, pinch zoom settle, and the Settings sheet at OS font scale 2.0.

### Art Director — verdict: ACCEPT WITH CONDITIONS
The shipped set conforms to F004-PAB v1.0.3 on every check I could run (127 PNGs on palette-v1, binary alpha, isometric lattice, crisp integer scale). The one open item is the stale Art Bible 4.3 text for F1 (condition C1).

#### Scope reviewed
- Commit `1838f22` (worktree issue-564-t446-final-reviews).
- Docs read: `docs/art/F004-art-bible-v1.md` (sections 0, 1, 4.1-4.3, 20, plus grep of cue/badge rules at 339-342, O-2 at 595), `docs/agents/roles-and-reviews.md` (Art Director section, lines 82-87 and the Art source asset matrix row), `specs/004-visual-identity/reviews/final-test-evidence.md` (7d, 8, 10), `art-direction-review.md` (F-09, OD-4).
- Images viewed: `reviews/final-test-evidence/e01` (portrait 1080x2400), `e02` (landscape 2400x1080), `e07` (1280x720 first run).
- Scripted check (inference-free, run by me, read-only): all 127 PNGs under `Assets/IdleFactory/Art/F004/` against `tools/art-pipeline/palette/palette-v1.json` (48 colours): 0 off-palette pixels, 0 semi-transparent pixels. Spot sizes: badge 80x64 canvas for machines, floor tile 70x54 (matches section 3.5 pitch), 88 manifest entries with per-frame sha256.
- Not done: the 8.5 MB of other PNGs under `art-source/` and the F004 asset-briefs row-by-row pass (reviewed in T439/`art-direction-review.md`, not re-litigated); no device viewing beyond the committed screenshots.

#### FR/SC coverage (Art Director lens)
| FR/SC | status | evidence |
|---|---|---|
| FR-003 / FR-009 (projection B, one grid, palette v1) | met | e01/e02: 2:1 isometric plates on a 70x54 lattice, flat-body + accent machines; palette/alpha scan above |
| FR-010 (state by shape first, colour last; running = no badge) | partial | e01/e02: running machines carry no badge; red "down-arrow" badges on 9 machines are distinct shape-first glyphs. Whether all badge shapes pass blind naming is T445 (pending) |
| FR-011 / OD-4 (integer scale only) | met | `final-test-evidence.md` s8: `F004Crispness` PASS, n = 4 both axes, edge concentration 1.000, 100 % uniform blocks |
| FR-021a / FR-022 (floor/grid readability) | met | e01/e02: floor tile visible but low-contrast against plates, no competing noise; e07: lock digits 2-5 and empty-slot plates legible |
| SC-003 (0 placeholders) | met (per QA evidence) | `final-test-evidence.md` s9; fallback frames remain `f003-*` named keys by design (cues/slots files `f003-*_00.png`) |
| SC-004 (asset checks, provenance) | met | pipeline 220 PASS (s9); manifest 88 entries with sha256; provenance in `art-source/f004/*/check-report.json` (#657 refresh pending) |
| SC-001 / SC-002 (readability) | n.a. pending | T445 owner session |
| Section 4.3 cue-size rule, DEV-1..4, AR-1/AR-2 | partial | see A-01 and conditions C1 |

#### Findings
| ID | Severity | Finding | Evidence | Disposition |
|---|---|---|---|---|
| A-01 | major (documentation) | Art Bible 4.3 states "320 dpi phones, portrait: n_min 3, 34.4 pt, meets". The shipped tolerance gives n_min 2 (22.95 pt), same 4.4 % shortfall as DEV-2, and the 4.1 row (n_min 3, n_fit 3, margin 18.2 %) is stale; the Play Mode log reports n_min 2 and n_fit 2 for portrait. | `docs/art/F004-art-bible-v1.md:209`, table 4.1 row 1; `final-test-evidence.md:116` | fix now (condition C1) |
| A-02 | minor | Portrait n_fit differs between the Bible table (3) and the real shell log (2): the 4.1 values for that row (n_fit, levels, margin) must be re-measured, not only n_min. AR-1 "fit-all only" still holds (n_min = n_fit = 2). | `final-test-evidence.md:116` vs Bible 4.1 row 1 (inference: n_fit 2 reading is from the log excerpt, I did not re-run) | fix with C1 |
| A-03 | minor | In e01/e02 the red badge sits at the plate's lower-right and visually overlaps the top corner of the plate in the next row (landscape right column and first row). Bible anchor (59,33) is as specified, but the overlap makes the badge read as belonging to the plate below. | e01 (badges at y 740/920/1100 over next-row plate tips), e02 (badge at 1366,210) | follow-up (info for #555 icon-confusion cluster); no change to frozen anchor |
| A-04 | minor | e07: first-run landscape at n = 2 shows empty plates with a small "x" glyph; the glyph is 11-ish art px and low-contrast, the lock plates are clearer than the empty plate. Consistent with DEV-3 (marker size) and with #555 8-pt hint. | e07 | accept; T445 blocking naming at 1280x720 n = 2 covers it |
| A-05 | info | F2-F4 and known items (#555, #656, #657, #650 rim share 0.9167 vs 0.90 threshold F-09) need no new Art Director action; #650 remains a thin margin, gate retained. | `art-direction-review.md` F-09 | accept |

#### Recommendation on the F1 wording change (recommendation only)
Lines to change in `docs/art/F004-art-bible-v1.md`:
1. Table 4.1 row 1 (portrait 720x1280, 320 dpi): `n_min` 3 becomes "3 (2 with the 1 % tolerance)", re-measure `n_fit`, "Levels", "Levels at or below fit-all" and "Margin at fit-all" from the T427/T442 Play Mode log; keep "1, fit-all only".
2. Line 209 (4.3 table): 320 dpi phones, portrait: `n_min` 2, **22.95 pt**, 1.05 pt (4.4 %) short, deviation **DEV-2**; the "meets" row survives only for classes at n_min 3 (the 420 dpi and tablet rows).
3. Line 214 (DEV-2 row): drop "if the T427 tolerance gives n = 2" (T442 confirmed it), add the portrait phone; text after table: "Both are recorded as accepted deviations" updated to cite the owner decision of 2026-11-05.
4. Line 218 (DEV-4): title "(lock-level digit, 320 dpi phones at n = 2: 1280x720 and portrait 720x1280)"; 14.97 pt applies to both; the "exists only if the tolerance gives n = 2" clause becomes "confirmed (T442)"; the sentence "without the tolerance n = 3 gives 22.46 pt" stays.
5. Line 339 (badge row) already says "except DEV-1 and DEV-2", no change; line 595 (O-2) and section 4.2 AR-1/AR-2 wording: add the portrait phone to AR-2 and mark T442 confirmation; add a section 20 row.
Version: a patch release `F004-PAB v1.0.4` (revision log row, header sentence), not v1.1 or a new Art Bible. Reason (inference from the log pattern 1.0.1-1.0.3): those patches recorded owner decisions with no palette, threshold or style change; this one does the same, and the "meets" line is a derivation error against the already-implemented tolerance (errata in substance, owner-accepted deviation size). The Art Bible rule itself (24 pt cue, 23 px set, 1 % tolerance) does not change, so no migration decision and no re-approval of existing assets is needed; the owner decision of 2026-11-05 must be cited in the revision row.

#### Conditions for acceptance
- C1: Art Bible section 4.3, DEV-2, DEV-4, section 4.1 row 1, AR-2 and section 20 are updated (patch v1.0.4 citing the owner decision of 2026-11-05) in the batch close-out; checkable by `grep -n "34.4 pt" docs/art/F004-art-bible-v1.md` returning no portrait-320dpi "meets" row and `grep -n "1.0.4" docs/art/F004-art-bible-v1.md` returning the revision row.
- C2: T445 and T447 (pending) must confirm blind naming and visual acceptance at 1280x720 n = 2 and portrait 720x1280 n = 2 (DEV-3 marker rule), since the portrait phone is now a DEV-2 class.

### Technical Art — verdict: ACCEPT WITH CONDITIONS
Import settings, atlases, provenance, floor pooling and sorting are sound on the committed state. The memory, atlas-page, batch, shader-variant and clean-APK budgets are not yet measured against the restyled art (deferred to T444/B3), so acceptance is conditional on that evidence.

#### Scope reviewed
Commit `1838f22` (worktree issue-564-t446-final-reviews, read-only). No test-results available; committed records only.
- `docs/art/F004-technical-art-integration.md` (sections 1-9), `specs/004-visual-identity/reviews/performance-budgets.md` (sections 4-6), `reviews/final-test-evidence.md` (T442), `docs/agents/roles-and-reviews.md` Technical Artist section.
- `Assets/IdleFactory/Art/F004/` (structure, 127 PNG `.meta`, 5 atlas `.meta`), `Atlases/machines.spriteatlasv2(.meta)`, `floor.spriteatlasv2`, `Assets/IdleFactory/Unity/Presentation/SlotGridView.cs` (floor pool, sorting), `ProjectSettings/TagManager.asset`, all 88 `art-source/f004/**/provenance.json`.

#### FR/SC coverage (Technical Art lens)
| FR/SC | status | evidence |
|---|---|---|
| FR-002 (pixel-crisp sprites, PPU 24, point filter) | met | all 127 PNG `.meta`: `spritePixelsToUnits: 24` x127, `filterMode: 0` (grep, no exception), `enableMipMap: 0` x127, `alphaIsTransparency: 1`, `spriteMode: 1`; atlas `filterMode: 0`, `generateMipMaps: 0`, `padding: 2`, `enableRotation: 0`, RGBA32 (`textureFormat: 4`), 1024 max (machines.spriteatlasv2.meta) |
| FR-003 / SC-003 (every F003 key restyled, importer reproducible) | met | integration doc s1, s2 (`F004ArtIntegration` batch command), s9.3 (hash-verified copy, atomic manifest, orphan/duplicate guards); T442 evidence row SC-003 |
| FR-022 / FR-023 (provenance, license, generator versions) | met | 88/88 `provenance.json` have `reviewedBy` filled (python check; the six empty ones listed in doc s4 are now filled), `license`, `route`, `tools`, `pixelPass.scriptSha256` |
| Floor tile (T471/T472): 70x54, pivot (35,27), pooled | met | floor PNG `.meta` pivot (0.5,0.5); `SlotGridView.cs:496,686-711` (pool, disabled not destroyed, `FloorMaxTiles=2048` cap at :358); `FloorOrder=-100` at :473, overlay layer "Overlay" in `TagManager.asset:44` |
| Atlas / memory (budgets 6, 7) | partial | 4 world atlases single page (inference from doc s1 page-based figure); measured B3 value not committed; `performance-budgets.md` rows 6, 7 "Deferred: T444" |
| Batch count (budget 8), shader variants (11), APK <=150 MB (10) with final art | partial | rows 8, 10, 11: B2 values are placeholder art; clean APK owed to T444 (doc s6 estimates ~46.37 MB, inference) |
| SC-009/perf budgets 4, 5 | partial | deferred to T444 (Performance lens) |

#### Findings
| ID | Sev | Finding | Evidence | Disposition |
|---|---|---|---|---|
| TA-1 | minor | Floor pool renderers all use the default sorting layer with order -100, no Sorting Group; correctness rests on slot Sorting Groups having order >=0 and sharing the layer. Sound today, but untested against a future layer change. | `SlotGridView.cs:473, 690` | accept (inference: covered by Play Mode SlotGridTests/floor tests that passed in T442) |
| TA-2 | minor | Floor sprite is in its own atlas (`floor.spriteatlasv2`), so floor tiles are one extra SetPass versus slots; worst case 2048 SpriteRenderers if camera zoom is absurd (silently no floor above cap). Typical tile count not recorded in the budget doc. | `floor.spriteatlasv2`, `SlotGridView.cs:358` | follow-up: record floor tile count and SetPass at B3 (T444) |
| TA-3 | minor | Floor tile has no `floor_2` pixels and its two detail clusters repeat visibly on a 70x54 grid (period = pitch); art pipeline accepted it. | doc s9.2, s9.4 | accept (owner/Art Director visual gate T447) |
| TA-4 | info | Budget table still has 6 rows "Deferred: B3"; the final texture memory (<=8 MB), atlas page size, SetPass, variant count 490 and clean APK size are therefore uncommitted for the shipped art. | `performance-budgets.md` rows 4-8, 10 | see condition C1 |
| TA-5 | info | URP global settings asset is re-serialized by every Android build (one-line `rid` removal); stripping state not emptied. Asset path is `Assets/IdleFactory/Rendering/UniversalRenderPipelineGlobalSettings.asset`. | doc s6, `performance-budgets.md` s5 | accept; keep diffing after builds (settings-noise hook) |
| TA-6 | info | Known: #657 (scriptHashes/check-report refresh on next Blender re-run), #650 (rim share 0.9167), #656 (camera clear colour, no FloorCamera in RenderCaptureHarness). No new consequence for import settings. | brief; doc s4 | follow-up as filed |
| TA-7 | info | Structure complete: 133 files non-meta under `Art/F004`, every file and folder has a `.meta`; one shipped `import-manifest.json` at the art root (T430 rule). | `find` on `Assets/IdleFactory/Art/F004` | accept |

#### Conditions for acceptance
- C1: T444 (B3) must commit measured values for budget rows 6 (world-art texture memory <=8 MB), 7 (atlas pages <=1024x1024), 8 (SetPass held at B2 0.77-0.78 with floor and restyled art), 10 (clean-build APK <=150 MB) and 11 (shader variants <=490 or explained); each checkable in `performance-budgets.md` section 4.
- C2: After the final Android/B3 build, `git diff` of `UniversalRenderPipelineGlobalSettings.asset` shows only the known one-line `rid` change (or is explained).
- C3: B3 records the floor tile count per screen class (TA-2) so the 2048 cap and pool size are shown to be unreached.

### Security and Privacy — verdict: ACCEPT (no blocker or major finding)

Reason: F004 adds no secrets, no absolute user paths, no runtime network calls and no new Android permission. The only persisted-data change is a separate, strictly parsed, presentation-only preferences file that fails safe to "follow system". Generative-asset licences are recorded and owner-approved; the residual risk is training-data provenance, accepted under FR-021.

#### Scope reviewed
- Commit: `1838f22` (origin/main). Review worktree `.maqa-worktrees/issue-564-t446-final-reviews`, read-only. Its HEAD was briefly detached by one of my commands and restored to `maqa/issue-564-t446-final-reviews`. Content unchanged, tree clean.
- Diff: `git diff ee92f1f..1838f22` with binaries and `.meta` excluded, written to scratchpad `t446/diff-u0.txt`. Grepped added lines for secrets, tokens, user names, absolute paths, network and exec patterns.
- Read in full: `Assets/IdleFactory/Unity/Persistence/DisplayPreferencesStore.cs`, `Assets/IdleFactory/Unity/Bootstrap/ReducedMotionSignal.Android.cs`, `Assets/link.xml`, `Packages/manifest.json` and lock diff, `ProjectSettings/*` diff, `tools/art-pipeline/models.json`, `tools/art-pipeline/pyproject.toml`, `art-source/f004/resources/reproducibility.json`, `art-source/f004/resources/comfy_gen.py` (header and model check), `art-source/f004/cues/f003-badge-running/provenance.json`, `specs/004-visual-identity/spec.md` FR-001, FR-020, FR-021.
- Git-grepped the final tree for user paths, host identifiers and LAN IPs, and binary `.blend` and PNG metadata for user paths.

#### FR/SC coverage (my lens)
| FR/SC | Status | Evidence |
|---|---|---|
| FR-001 save format unchanged | met (inference from diff) | `JsonRunCheckpointStore.cs` and Save V2/V3 code not in the F004 changed-file list; `git diff --name-only ... -- 'Assets/IdleFactory/Unity/Persistence/*'` lists only `DisplayPreferencesStore.cs` |
| FR-020 preference stored outside save | met | `DisplayPreferencesStore.cs:158-160` (`IdleFactory/Preferences/display-v1.json`, separate from checkpoints); `:200` path built from constants |
| FR-020 invalid or missing file falls back to follow-system, no dialog, rewritten on change | met | `DisplayPreferencesStore.cs:222-235` (`Reload`), `:54-70` (`Invalid()` returns `FollowSystem`, one `Debug.LogWarning` with fixed code), `:241-256` (`TrySave` rewrites) |
| FR-020 bounded, strict parse | met | `:167` 4096-byte cap, `:35-44` length check then bounded read, `:85-187` strict token walk (no escapes, no reflection, duplicate keys rejected) |
| FR-021 generative licences and provenance | met (owner-accepted risk) | `tools/art-pipeline/models.json` (Apache-2.0, `commercialUse: true`, SHA-256, `trainingDataNote`, owner approval text); 14 `route: comfy` provenance records |
| FR-021 secrets, paths in shipped metadata | met | Added-line grep: no keys or tokens; no `C:\Users\...` in final tree (`git grep` over `art-source tools Assets Packages ProjectSettings specs docs scripts`) |
| SC (no runtime network) | met | No `UnityWebRequest`, `HttpClient`, sockets or URLs in `Assets/` added lines; `comfy_gen.py` and `comfy_pass.py` talk only to `127.0.0.1:8188` (dev tooling) |

#### Findings
| ID | Sev | Finding | Evidence | Disposition |
|---|---|---|---|---|
| S-1 | minor | Forward compatibility: a future `"version": 2` preferences file reads as invalid, falls back to follow-system, and is overwritten on the next change. Data at risk is one presentation choice. | `DisplayPreferencesStore.cs:404-416` (`version` must be exactly `1`) | follow-up (bump-time migration note). Accept now. |
| S-2 | minor | Base-model source URLs track Comfy-Org `main` (unpinned revision). SHA-256 is checked before every run (`comfy_gen.py` model check; `provenance.py:116-119`), so drift causes a refusal, not silent use. Owner accepted the hash as the pin on 2026-11-03. | `tools/art-pipeline/models.json` (`z_image_turbo_bf16`, `qwen_3_4b`, `z_image_ae`: `version: unpinned`) | accept; follow-up to pin an HF commit if the owner wants one |
| S-3 | minor | Training-data provenance for Z-Image-Turbo and the `tarn59` pixel-art LoRA is undisclosed. Shipped sprites derived from them (14 `route: comfy` items and resources under `Assets/IdleFactory/Art/F004/`) carry this as a distribution risk. FR-021 explicitly allows it as a recorded accepted risk. Licence strings come from the registry and the HF card; I could not verify them online (inference). | `models.json` `trainingDataNote`; `art-source/f004/items/*/provenance.json`, `art-source/f004/resources/*/provenance.json` | accept per FR-021 policy. Owner to keep the acceptance on record. Not a legal review. |
| S-4 | info | Preferences file has no integrity check. Tampering only changes the reduced-motion choice. | `DisplayPreferencesStore.cs` | accept |
| S-5 | info | Missing file counts as invalid, so a first run logs `preferences.display.invalid` once. This is noise, not a leak: the code is fixed and carries no path or bytes (`:166`, `:72`). | `DisplayPreferencesStore.cs:54-70`, `:72` | accept |
| S-6 | info | Android probe reads `Settings.Global.animator_duration_scale` through JNI. Read-only, no PII, no new permission, exceptions return "not reduced". `link.xml` preserves only the JNI types. | `ReducedMotionSignal.Android.cs:14-31`; `Assets/link.xml` | accept |
| S-7 | info | Package delta: one explicit addition, `com.unity.render-pipelines.universal` 17.6.0 (Unity first-party). Transitive additions are Unity builtin or registry packages from `packages.unity.com`. No git, tarball or local sources. | `Packages/manifest.json`; `packages-lock.json` | accept |
| S-8 | info | Python tooling: Pillow 11.3.0 and pytest 8.4.2 pinned, hashes in `uv.lock`, run with `--frozen --offline`. Dev-only, not shipped. I could not check advisories offline. | `tools/art-pipeline/pyproject.toml`, `uv.lock` | follow-up: routine advisory check on the Pillow pin |
| S-9 | info | Pre-existing LAN address `192.168.2.172` appears in F001-era docs and specs (4 files). F004 did not add it. | `docs/integrations/unity-standalone-profiler-android.md:12`; `specs/001-core-game-foundation/learn.md:313` | not F004; optional scrub follow-up |
| S-10 | info | Owner dev-tooling leaks checked: no user name, host name or serial in F004 content. Lease host name is written only to the Git common dir (`scripts/resource-lease.ps1:201`), not tracked. Binaries and PNG metadata have no user paths. | `git grep` over the final tree | accept |

No issues are tied to known Issues #555, #656, #657 or #650. None affects my lens.

#### Conditions for acceptance
None required for this lens. Optional, checkable:
1. The owner keeps the S-3 acceptance on record (already in `models.json` approval text dated 2026-11-03). Check: `git show 1838f22:tools/art-pipeline/models.json | grep -c "accepted the SHA-256 as the pin"` returns 3 (the three base-model entries; verified).
2. Follow-ups S-1 and S-8 are logged on a Issue or the wiki before feature close. This is not a blocker.

#### Counts
blocker 0, major 0, minor 3 (S-1, S-2, S-3), info 7 (S-4 to S-10).

### DevOps and Operations — verdict: ACCEPT WITH CONDITIONS (no blocker; rollback proof and two measurements still open in T444)

Scope reviewed (read-only, commit 1838f22; worktree feat-004-visual-identity at fec3d0a, same object store):
- `git diff --stat ee92f1f..1838f22 -- scripts tools .githooks` (94 files, 9699 insertions).
- scripts/test-unity.ps1 (art chain, `-ArtPipelineOnly`), scripts/test-art-pipeline.ps1 (exit codes, report), scripts/resource-lease.ps1 (gpu resource), scripts/check-after-implement-hooks.ps1 (gate 6), scripts/check-settings-noise.ps1, .githooks/pre-commit, scripts/android-emulator-journey.ps1 (lines 1565-1580, 1880-1890), Assets/IdleFactory/Tests/EditMode/RenderPipelineConfigurationTests.cs (lines 28, 207, 245-310).
- specs/004-visual-identity/reviews/performance-budgets.md §1, §3, §5; reviews/phase0-review.md A4; research.md R01; tasks.md T411, T444, T457.
- docs/agents/workstation-pitfalls.md (lines 33-35, 46, 51, 60, 66).
- Host check: C: 31.3 GB free (Get-PSDrive), above the 20 GB pitfall threshold. Read-only; no Unity, build, adb or lease action.

#### FR/SC coverage (DevOps lens; FR text not re-read, mapping from tasks.md and reviews)

| FR/SC | status | evidence |
|---|---|---|
| FR-004, FR-005, SC-008 (URP switch, parity, build gates) | met (per T411 record); rollback proof partial | tasks.md:228; performance-budgets.md:16 (one rid dropped and restored by git checkout) |
| FR-025, SC-007 (performance budgets, B2 build) | partial | performance-budgets.md:93 (OPS-4 Play Mode duration not measured) |
| FR-026 (rollback) | partial | performance-budgets.md:95 (OPS-7 not repeated); phase0-review.md:A4 (atomic change set, worker dry-run passed) |
| FR-027 (physical-device gap) | met as recorded | performance-budgets.md:121 |
| SC-006 (guard against vacuous settings tests, N-4) | met in EditMode test | RenderPipelineConfigurationTests.cs:252-310 (m_SettingsList guard, positive/negative control) |

#### Lens criteria

1. Build reproducibility. Met. test-art-pipeline.ps1 pins `uv run --frozen --offline`, resolves uv by absolute path, clears proxy variables for its own process, and always writes report.json. Exit 0 pass, 1 test failure, 3 unavailable. test-unity.ps1 (lines ~465-478) throws on exit 1 and on exit 3 before Unity starts, so 3 is never read as a pass. Both paths are honest. Not run here (no Unity, no uv run).
2. Scripts and lease usage. Met. resource-lease.ps1 adds `gpu` to the known set and status output. Journey script takes -LeaseOwner. Hook gate 6 checks art-path commit ancestry against report.commit and status=pass with dirty=false. Note: test-results/ is gitignored (.gitignore:102), so the report is worktree-local and not reproducible from a fresh clone (see F3).
3. Emulator journey, Welcome-back. Weakness confirmed (record-only). The Welcome-back dismissal is a fixed tap at 0.85,0.738 with a 2 s sleep, at journey-script lines 1571-1572 and 1884-1885, and no check that the dialog is present before the tap or that it closed after. If the dialog is absent, the tap lands on game UI. T442 needed a manual dismissal (F4). Consistent with the owner's record-only decision.
4. Settings-noise and URP guard coverage. Partial. check-settings-noise.ps1 covers only ProjectSettings.asset (icons, graphics API, managed variant) and the EditorSettings enter-play-mode keys. It has no signature for UniversalRenderPipelineGlobalSettings, GraphicsSettings or QualitySettings, and no check for EditorSettings m_SpritePackerMode (R01 says so). The URP guard lives in the EditMode test (RenderPipelineConfigurationTests.cs), which covers the m_SettingsList and stripping keys and the 17.6.0 pin. Accepted by the owner's discovery note; residual gap is in F2.
5. Rollback path of the URP switch (R01). Partial. The procedure is documented (`git revert -m 1 b9221ca`, performance-budgets.md:95), and phase0-review A4 records a worker dry-run that restored Built-in. OPS-7 (scratch-branch dry-run with compile, RenderParityTests and the m_SpritePackerMode check) is not repeated and is scheduled in T444 (tasks.md:313).
6. Disk, Library cost and worktree hygiene. Met. pitfalls line 34 gives the Library/Bee size (about 4.8 GB), the 20 GB C: floor, and the T411 out-of-space failure. Lines 35, 51 and 66 cover the lingering player, emulator/adb from worktrees and the long-path removal. Host has 31.3 GB free now.

#### Findings

| ID | sev | finding | evidence | disposition |
|---|---|---|---|---|
| F1 | major | OPS-7 R01 rollback dry-run not repeated. The only proof of the revert of the final URP merge is a read-only ancestry check. The sprite packer mode check is not asserted. | performance-budgets.md:95; research.md R01 | fix now: run inside T444 (already scheduled, tasks.md:313) on a scratch branch, never pushed. Condition C1. |
| F2 | minor | Settings-noise hook has no URP/Graphics/Quality signatures and no m_SpritePackerMode check. Partly covered by the EditMode guard only. The B2 build dropped one m_SettingsList rid, and the file was restored by git checkout, not by a guard. | check-settings-noise.ps1 header and checks; performance-budgets.md:16; RenderPipelineConfigurationTests.cs:252-310 | follow-up: add signatures for UniversalRenderPipelineGlobalSettings.asset and EditorSettings m_SpritePackerMode in a later tooling Issue. Diff the asset after each Android build (already in performance-budgets.md:122). |
| F3 | minor | Hook gate 6 needs test-results/art-pipeline/report.json, which is gitignored and per worktree. A fresh clone or CI cannot satisfy the gate without rerunning the pipeline. This is consistent with the local-workstation scope. | .gitignore:102; check-after-implement-hooks.ps1 gate 6 | follow-up: note in workstation-pitfalls or the hook header. No code change needed for F4. |
| F4 | minor (record-only, F4 known) | Welcome-back dismissal is a fixed-fraction tap with no presence check. | android-emulator-journey.ps1:1571-1572, 1884-1885 | accept (owner decision 2026-11-05 F4). Follow-up on journey hardening. |
| F5 | minor | OPS-4: no measured Play Mode duration for B1 vs URP. The 25-30 min figure is unexplained. | performance-budgets.md:93 | fix now in T444: one `scripts/test-unity.ps1 -TimeoutSeconds 3000` run each on ac813fd and b9221ca with compile time logged, under unity,emulator,gpu. Condition C2. |
| F6 | info | test-unity.ps1 art chaining: fail-safe when origin/main is unknown; -IfChanged skip leaves report untouched; exit 3 throws. Correct. | test-art-pipeline.ps1 Get-ArtPathsChanged; test-unity.ps1 | accept. |
| F7 | info | Disk at review time 31.3 GB free on C:, above the 20 GB floor. | Get-PSDrive C | accept. Recheck before the T444 Android build. |
| F8 | info | Known non-blocking Issues in my lens: none new. #656 (camera clear colour) and #657 (checks report refresh) do not touch ops. | brief | none. |

Note: no new Issues were created and no GitHub state was changed.

#### Conditions for acceptance (objectively checkable)

- C1: T444 records an OPS-7 dry-run on a scratch branch that reverts the final URP merge with `-m 1`, compiles, runs RenderParityTests, and asserts `EditorSettings m_SpritePackerMode`. Evidence file: specs/004-visual-identity/reviews/performance-budgets.md section 3 updated, or reviews/performance-b3.md. Scratch branch not pushed (`git ls-remote` shows no such branch).
- C2: T444 records OPS-4 (two full Play Mode runs with compile time and duration) in reviews/performance-b3.md or performance-budgets.md section 3.
- C3: Before T448 closeout, the follow-ups F2, F3 and F4 are filed as Issues or listed in the closeout note, so they are not lost.

## 5. T447 owner visual acceptance

Prepared by the Technical Artist (evidence only, no verdict) for Issue #565. Covers SC-003 and SC-009. The owner decides; every "Owner decision" and "verified" cell below is intentionally empty. Sheets are composed from committed PNGs (Pillow, nearest-neighbour 4x; F003 at 128 px / 64 PPU, F004 at 24 PPU, so world size differs by design; the script is not committed). Total evidence under `final-acceptance/` is about 0.7 MB.

### 5.1 Before/after sheets per family

Left = Feature 003 placeholder (`Assets/IdleFactory/Art/Development/F003/Sprites/`), right = shipped F004 frame 00 (`Assets/IdleFactory/Art/F004/<family>/`), each at 1x and 4x. Art Director status is from `reviews/art-direction-review.md` section 1.

| Family | Before/after sheet | Art Director status | Open conditions (art-direction-review) | Owner decision |
|---|---|---|---|---|
| Machines (4) | [machines.png](final-acceptance/before-after/machines.png) | APPROVE WITH CONDITIONS | OD-1 ratify flat emission (Lighting B); F-09 thin Assembler rim margin (0.917 vs 0.90); F-10 Press smallest mass | |
| Slot plates (empty, occupied, locked L2-L5) and selection frame | [slot-plates-and-selection-frame.png](final-acceptance/before-after/slot-plates-and-selection-frame.png) | APPROVE WITH CONDITIONS (frame: APPROVE) | F-01 locked keyhole reads as "1"; F-02 locked plate not dark; L2-L5 pixel-identical | |
| Floor tile | [accents-labels-floor.png](final-acceptance/before-after/accents-labels-floor.png) (after only; F003 had no floor sprite, compare in the world pairs) | APPROVE WITH CONDITIONS | OD-5 no-outline exemption sentence; F-17 / T456 60 x 30 period; TA-3 repetition on the 70 x 54 lattice | |
| Cues: 5 badges (+ running = intentionally empty) | [cues-badges.png](final-acceptance/before-after/cues-badges.png) | APPROVE | none | |
| Markers (Boosted, Overdrive) | [markers.png](final-acceptance/before-after/markers.png) | APPROVE | DEV-3 naming at 1280x720 n = 2 and 720x1280 n = 2 (T445 + owner, see 5.2) | |
| Lock labels L2-L5, machine accents | [accents-labels-floor.png](final-acceptance/before-after/accents-labels-floor.png) (after only, new in F004) | Labels: APPROVE WITH CONDITIONS; accents: with machines | OD-9 label backdrop not in Art Bible; OD-1 for accents | |
| UI icons (49 F003 keys + new settings icon) | [ui-icons.png](final-acceptance/before-after/ui-icons.png) | APPROVE WITH CONDITIONS | blind naming 36/50 (72 %); three confusable clusters (arrow-over-bar, ring, abstract currency); causes sit in the brief | |
| Resources (Ore, Bar, Plate, Gear) | [resources.png](final-acceptance/before-after/resources.png) | APPROVE WITH CONDITIONS | ore small and speckled; gear drawn face-on | |
| Items (3 pictograms) and tier chips (3) | [items-and-tier-chips.png](final-acceptance/before-after/items-and-tier-chips.png) | Items: APPROVE WITH CONDITIONS; chips: APPROVE | drill head and overclock core weak pictograms | |

In-game pairs (before = F004-B1 pre-switch baselines in `Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B1/`; after = shipped-build captures):
- [world-landscape.jpg](final-acceptance/before-after/world-landscape.jpg): `w003s-1280x720-world` vs `e02-landscape-2400x1080-factory` (emulator).
- [world-portrait.jpg](final-acceptance/before-after/world-portrait.jpg): `w003s-390x844-world` vs `e01-portrait-1080x2400-factory` (emulator).
- [world-t445-practice.jpg](final-acceptance/before-after/world-t445-practice.jpg): `w003s-390x844-shell` vs the T445 SC-002 standard practice screen (read from `origin/maqa/issue-563-t445-readability-final`, unmerged at preparation time; only the composite is committed here).

Overall F004 art sign-off (all families), Owner decision: ______

### 5.2 Owner checklist for the T447 conditions

No physical device exists on this workstation (emulator only; pointer injection is impossible there, see final-test-evidence section 8). Items marked "phone" may have to be recorded as unverified; if so, record the reason and carry the item into the T448 closeout list.

| # | Source condition | Check | Needs | verified / not verified (reason) |
|---|---|---|---|---|
| 1 | UI/UX C3, G3 | Real-finger tap on a slot at n_min (90x45 hit diamond, 0.9 pt margin over 44 pt; edge taps near neighbours) | phone | |
| 2 | UI/UX C3, Test/QA QA-6 (FR-017) | Two-finger pinch zoom: preview, settle and snap, third-finger cancel | phone | |
| 3 | Test/QA QA-6 (FR-016) | Slow pan: no shimmer on the floor or machines | phone | |
| 4 | UI/UX C3, U-8 | Settings sheet at OS font scale 2.0: reduced-motion switch and the effective/"Not saved" line reachable, not lost below the fold | phone or emulator font scale | |
| 5 | GD/PO 2, G4 | Rotate the device after dismissing Welcome-back: the sheet must not reappear (harness showed a second sheet after rotation, unattributed) | phone or emulator | |
| 6 | Art Director C2, DEV-3 | Visual acceptance at 1280x720 with n = 2 (markers named at a glance) | editor Game view or screenshot | |
| 7 | Art Director C2, DEV-3 | Visual acceptance at portrait 720x1280 with n = 2 | editor Game view or screenshot | |
| 8 | TA-3 | Floor repetition (period equals the 70 x 54 pitch; two detail clusters repeat) acceptable at fit-all and n_min | any screen | |

### 5.3 Feature 003 placeholder decision (SC-003, SC-009)

SC-009 (style decision recorded before production assets): this prep did not re-verify the decision date; the owner confirms or points to the record: ______

Inventory (measured in this worktree at origin/main `163eb15`):

| What | Path | Count / size | Referenced by |
|---|---|---|---|
| F003 placeholder sprites (fallback frames) | `Assets/IdleFactory/Art/Development/F003/Sprites/` | 78 PNG + 78 `.meta`, about 61 KB of PNG (128 px; UI icons 48 px; 64 PPU). Machines 4, slots 7, badges 6, markers 2, resources 4, items 6, UI icons 49 | All 78 GUIDs are referenced by `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` (scene sprite catalog, retained as fallback frame). Tests: `F003AssetContractTests`, `F004AssetContractTests` (reads the 78 keys), `PresentationCatalogFallbackTests` |
| F003 SVG sources | `Assets/IdleFactory/Art/Development/F003/Source/` | 78 SVG (about 75 KB) + `PROVENANCE.md` + metas | `F003AssetContractTests` (SourceRoot); `PROVENANCE.md` says "temporary, replaced in Feature 004" |
| F001 development placeholders (older, outside SC-003) | `Assets/IdleFactory/Art/Development/F001/{Imported,Source}` | 21 PNG + 21 SVG | `F001AssetContractTests`, `PresentationSpriteCatalogTests` |
| Docs | `docs/art/F003-*.md`, `docs/art/F001-*.md` | 5 files | history only |

The shipped F004 files keep the F003 key names (`f003-*_00.png` under `Art/F004/`), so asset IDs stay stable.

Options:

| Option | What changes | Consequences |
|---|---|---|
| A. Keep as fallback (status quo) | nothing | Missing or invalid F004 art still degrades to a tappable placeholder with one `presentation.art.fallback` diagnostic (quickstart step 2, contract presentation-and-state-cues). SC-003 "0 placeholders in the shipped set" holds only if "shipped set" means the catalog primary frames; the placeholders still ship in the build as fallback frames (about 61 KB of textures) |
| B. Archive sources only | Move `Source/` (78 SVG + PROVENANCE.md) out of `Assets/` (for example to `art-source/f003-archive/`); keep `Sprites/` | Fallback behaviour unchanged. `F003AssetContractTests` SourceRoot checks must be repointed or retired. No runtime or build effect. Reversible |
| C. Archive sprites and sources | Move `Sprites/` as well and drop the legacy frame from the scene catalog (Unity tooling, one-time scene re-save) | Removes the fallback path: a missing F004 frame then fails start-up validation instead of showing a placeholder. `PresentationCatalogFallbackTests`, `F003AssetContractTests` and the F004 contract test reading the 78 F003 keys must be rewritten; legacy-shape rows in `PresentationSpriteCatalog` become dead code; quickstart step 2 changes |
| D. Remove outright | C plus delete from git | As C; history keeps them in git. Also removes the visual baseline reference these before/after sheets use |

Technical Artist note (not a verdict): the task keeps the fallback frames until the decision, so A or B needs no code change; C and D need a plan and a test rewrite and are follow-up work.

Owner decision (A / B / C / D, with date): ______
