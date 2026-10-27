# Plan Reviews — Feature 004 (2026-10-01)

Read-only reviews of `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md` and the diagrams, at most three roles concurrently, Medium tier. Every verdict was **PASS-WITH-FIXES**; no FAIL, no critical finding. The parent applied the dispositions below to the artifacts in the same commit as this record.

## Wave 1 and 2 verdicts

| Role | Verdict | High findings |
|---|---|---|
| Software Architect | PASS-WITH-FIXES | ARCH-1 (F003 components not yet implemented; #253 merged only task text), ARCH-2 (fallback model contradictions), ARCH-3 (camera rules incomplete) |
| Test/QA | PASS-WITH-FIXES | QA-1 (no Python test lane), QA-2 (capture and diff method), QA-3 (StandalonePlayer coverage) |
| Technical Artist | PASS-WITH-FIXES | TA-1 (overhang gap is 0.5 cell, not 1), TA-2 (atlas settings govern packed pixels; Android compression) |
| Performance Engineer | PASS-WITH-FIXES | PERF-1 (F003 evidence not reusable as B1), PERF-2 (ABA session with archived APK), PERF-3 (metric sources), PERF-5 (allocation probes) |
| Art Director | PASS-WITH-FIXES | AD-1 (spike matrix too large → staged), AD-2 (independent reviewer unnamed), AD-3 (palette semver), AD-7 (cue size at `n_min`), AD-11 (style drift across routes) |
| UI/UX (developer + independent reviewer lens) | PASS-WITH-FIXES | UX-1 (no host surface for the reduced-motion control), UX-2 (control semantics) |

## Dispositions

| Finding(s) | Disposition | Applied in |
|---|---|---|
| ARCH-1 | Accepted. OD-3 reworded; Feature 003 dependency table and gates added | plan.md (Owner decisions, Feature 003 dependencies); presentation contract |
| ARCH-2, TA-7, UX-8, QA-10 | Accepted. Primary frames plus retained fallback frames with own PPU/pivot/world size; fallback only if it validates; F003 failure matrix stays green; fallback never shipped, exempt from crispness | presentation contract; data-model; research R10 |
| ARCH-3, ARCH-4, UX-3, UX-4, UX-5, UX-11, QA-4, QA-6 | Accepted. Allowed set, `pxPerPoint` provider, odd-size alignment, runtime degrade, instant snaps, anchor hold, wheel and button steps, sheet pan-then-step and restore, level count and margin per class, real-HUD Play Mode variant and negative case | camera-scale contract; research R02, R03 |
| ARCH-5 | Accepted. F003 state names; slot conditions separate; no second precedence rule | presentation contract; data-model |
| ARCH-6, AD-8 | Accepted. Integer-millisecond presentation clock, paused on suspend; reduced-motion pure function; flash metric; frame 0 lights-on | research R05; presentation contract; art-pipeline checks |
| ARCH-7, ARCH-8 | Accepted. `IReducedMotionSignal` per platform; adapter only refreshes; store header and `Preferences/` ignored by checkpoint code | research R11 |
| ARCH-9, ARCH-11, QA-11 | Accepted. Global settings asset, renderer index, configuration test, no expectation changes in phase 0, test-name manifest, rollback dry-run | research R01; render-foundation contract |
| ARCH-10, QA-1, TA-13 | Accepted. `scripts/test-art-pipeline.ps1`, `maqa-config.yml` command, batch gate, pins | research R07; plan Test strategy and step 3 |
| QA-2 | Accepted. Deterministic RenderTexture capture, committed baselines, C# diff helper, emulator block check for fractional ratios | research R13 |
| QA-3, PERF-5 | Accepted. StandalonePlayer-tagged crispness, reduced motion, fallback; `GcAllocationCounter` probes and a 600-frame player run | plan Test strategy; presentation and camera contracts; research R12 |
| QA-5 | Accepted. Invariants survive re-baselining; assertion diff in QA report | research R15; camera-scale contract |
| QA-7, AD-1, AD-9, TA-3 | Accepted. Staged spike S1–S4, isolated spike import path, A/B contact sheets, owner pick or reject, bounded redo, no freeze without a complete record | research R16; plan step 5 |
| QA-8 | Accepted. In-game sheet captures, seeded shuffle, withheld key, recorded reviewer session; scripted timed trial | research R17 |
| QA-9, TA-5 | Accepted. Per-frame tolerance metric; Workbench or Cycles CPU preferred; golden reproduction test per route | research R09; art-pipeline contract |
| QA-12, TA-11 | Accepted. Darkest-light contrast; IoU on a 32×32 downsample | art-pipeline checks |
| TA-1, UX-6, UX-7 | Accepted. Overhang ≤ 0.5 cell (D = 24: canvas 48×60); footprint-anchored overlays; densest-overlay Play Mode frame | research R04; presentation contract; art-pipeline checks |
| TA-2, TA-12 | Accepted. Atlas overrides for Default, Standalone, Android; pages ≤ 1024²; Sprite Atlas v2 packing as a recorded settings decision | research R10; art-pipeline contract; plan budgets |
| TA-4 | Accepted. Route-specific reduction and fixed pass order; fixed canvas per animation | research R07; art-pipeline contract |
| TA-6, AD-3, AD-4 | Accepted. Palette groups with budgets, reserved cue colors, CVD-safe pairs, semver | art-pipeline contract; research R18 |
| TA-8, UX-9, UX-10 | Accepted. Integer-multiple UI icons by key, enumerated per surface; 200 % text layout tests; UI token contrast | research R10; plan Test strategy |
| TA-9 | Accepted. Manifest schema, hashes, import dependency, postprocessor version | research R10 |
| TA-10, PERF-8 | Accepted. `gpu` lease; headless Blender batches; preflight recorded with every measurement | research R12, R14; plan step 3 |
| PERF-1, PERF-2, PERF-3, PERF-4, PERF-6, PERF-7, PERF-9, PERF-10 | Accepted. B1 re-measured from an archived build; ABA session; metric source table; CPU budget as max(10 %, 2 points); batch count held; W004-V fixture with forced presentation states; miss options; budget freeze file | research R12; render-foundation contract; plan steps 1, 2, 10 |
| AD-2, AD-6, AD-12 | Accepted. Second Art Director session, UI/UX and Game Designer/PO as reviewers; provenance additions; supersession table | research R18; art-pipeline contract; data-model |
| AD-5, TA-13 | Accepted. Brief fields and inventory table (29 F003 rows + floor + UI icons) | art-pipeline contract; data-model |
| AD-7 | Accepted. Cue ≥ 12×12 art px, own integer factor, ≥ 24 pt at `n_min`; body variant carries the state class; label threshold 12 pt cap height | research R06 |
| AD-10 | Accepted. Baked lighting with emissive frames as default; 2D-light accents only as spike S4 option | research R16 |
| AD-11 | Accepted. Style-anchor sheet, shared ramp rules, cohesion review; generative route blocked until the LoRA approval | research R08 |
| UX-1 | Owner decided 2026-10-01: a gear icon in the HUD opens a new Settings sheet that hosts the control (OD-8) | spec FR-020a; plan OD-8, Components; research R11; presentation contract |
| UX-2 | Accepted. Radio group with live effective-state line; in-memory apply and "Not saved" note on write failure | research R11; presentation contract |

## Wave 3

| Role | Verdict | High findings |
|---|---|---|
| Game Designer/PO | PASS-WITH-FIXES | GD-3 (single-level zoom range on the worst phone: rule 7 outcome and owner visibility) |
| DevOps/Operations | PASS-WITH-FIXES | OPS-1 (B1 APK archive), OPS-2 (`gpu` lease task and label), OPS-3 (pipeline tests outside `maqa-config.yml`) |
| Security/Privacy | PASS-WITH-FIXES | SEC-1 (strict preferences reader limits) |

| Finding(s) | Disposition | Applied in |
|---|---|---|
| GD-1, GD-2, GD-8 | Accepted. Salience rule (problem vs benign states); variant (b) without badges on running machines; reduced motion affects only looping art; effective-state wording | research R06, R11 |
| GD-3, GD-5, GD-7 | Accepted. Single-level rule-7 outcome; zoom-level table in every spike and acceptance record; recommended option and default per spike stage; owner note in the plan | camera-scale contract; research R16; plan |
| GD-4 | Accepted as an owner note (instant snaps; optional later ease, off under reduced motion) | plan |
| GD-6 | Accepted. Balanced problem states and benign distractors in the timed trial; per-state breakdown | research R17 |
| GD-9 | Accepted. One player-facing term "storage full" (Feature 003 naming) | spec, research |
| OPS-1 | Accepted. Archive path outside worktrees, `reviews/baselines.md`, retention, rebuild fallback | research R12 |
| OPS-2, OPS-5 | Accepted. `gpu` resource, label, AGENTS.md section, acquisition rules; `BLENDER_EXE`; tooling absence blocks only production Issues | research R14 |
| OPS-3, OPS-4 | Accepted. Pipeline script chained from `test-unity.ps1` for art paths, named in the QA contract and the after-implement gate; absolute `uv`, proxy cleared, frozen offline run, "tooling unavailable" outcome | research R07 |
| OPS-6, OPS-10 | Accepted. Winning raws only, gitignored rejects, 25 MB/150 MB budgets, `.gitignore` entries | research R07; plan step 3 |
| OPS-7 | Accepted. Baseline PNG importer check and size record | research R13 |
| OPS-8, OPS-9 | Accepted. Android GLES3 build gate, clean reimport, lock file, stripped variants, noise check; packer-mode decision and revert assertion | research R01 |
| SEC-1, SEC-2, SEC-5 | Accepted. 4 KB cap, exact version, unknown-member rejection, token walk, native atomic replace, diagnostic once per session | data-model; presentation contract |
| SEC-3 | Accepted. Platform signal files, never-throw rule, `SetLastError`, `link.xml` | presentation contract |
| SEC-4, SEC-6 | Accepted. `models.json` with SHA-256 and verification; license change is a product decision | art-pipeline contract |

All nine plan reviews passed with fixes; every finding is accepted and applied. Remaining owner decisions are the staged spike outcomes and the LoRA download, both scheduled inside the work breakdown.

## Re-validation 2026-10-27

Trigger: the branch was 471 commits behind `main` (Feature 003 and Feature 005 closed, `main` dacf111). Merge `cc0aa79` had wiki conflicts only (F004 source renumbered S019 → S038). Parent drift inventory (Software Architect lens, Medium tier `sonnet`, read-only) found **material drift**; the parent applied it in `3409874`. Three read-only re-reviews (Medium tier `sonnet`, `max_parallel: 3`) reviewed `3409874`; all verdicts **PASS with fixes**, no blocker; every finding is applied in the following commit.

Owner decision 2026-10-27: the Feature 005 §5 UI follow-ups join F004 as User Story 8 (FR-029–FR-033, SC-010, tasks T450–T453).

Task IDs: T193–T242 collided with Feature 003 follow-up Issues (T193–T196, T243–T251, e.g. #307); the feature now uses T400–T455.

| ID | Lens | Finding | Disposition |
|---|---|---|---|
| D-1 | Drift | `FactoryCameraController` is not the sole camera writer; `RunCameraFraming.FitAll` writes `orthographicSize` | Contract owner text and QA grep guard; T427 makes `RunCameraFraming` geometry-only |
| D-2 | Drift | Zoom is continuous (exponential) with `RunZoomSettings` 16–64 pt/cell | Baseline paragraph in `contracts/camera-scale.md`; T427 replaces it |
| D-3 | Drift | Unobstructed rect and `pxPerPoint` live on `RunShellDocument`, not `FactoryHudDocument`; rect includes the F005 ore-vein overlay | Contract, research R02, T427 and table tests updated |
| D-4 | Drift | `ZoomReadabilityTests` was deleted in T119; invariants live in `RunHudLayoutTests` and `SlotGridTests` | Seven re-baseline cases named in the contract |
| D-5 | Drift | T110 catalog merged with a hard-coded F001 requirement table that pins sprite names | T428 per-key registry |
| D-6 | Drift | Machine state comes from `SlotGridModel.BadgeFor` badge keys | Contract and T429 updated |
| D-7 | Drift | No settings UI or preferences file; `ShellPanelIds` closed list; `ApplicationLifecycleAdapter` does not exist | Mount via `ShellPanelRegistry`; `PersistenceRoots`; refresh in `CoreGameFoundationBootstrap.NotifyFocusChanged` |
| A1 | Architect | WU28 misses `NavigationInputTests.cs`; removing the serialized `RunZoomSettings` field would orphan scene YAML | `RunZoomSettings` kept as reduced serialized type, no scene hand edit; navigation tests moved to T454 |
| A2 | Architect | "Sole writer" wording inaccurate | Reworded with grep guard |
| A3 | Architect | T427 too large | Split: T427 (controller, framing, wiring, re-baseline, crispness, table play test) and T454 (pinch end, zoom-button state, `NavigationInputTests`) |
| A4–A5 | Architect | Gesture end only for pinch; not-ready rect | Additive `NavigationIntentKind` member; no re-fit until the rect resolves |
| B1–B2 | Architect | F001 keys unused at runtime | Decision: not start-up requirements; `PresentationSpriteCatalogTests` added to WU29 |
| C1 | Architect | `BadgeFor` boundary OK | Existing signatures kept; `SlotGridTests` in WU30 if `SlotVisual` changes |
| D1–D2 | Architect | Preferences path safe; focus re-read must stay tiny | Path `<root>/IdleFactory/Preferences/`, cleanup test; lazy re-read |
| E1–E2, U1 | Architect, UI/UX | `HandleBack` closes only slot/tab sheets | New `HandleBack` branch and `ShowPanel` state specified; T433 write set extended |
| F1–F2 | Architect | R-23 data reachable through `session.Catalog` without Core | FR-029 wording; T450 writes the failing test first |
| G1–G2 | Architect | No `link.xml`; T119 wording | T432 creates `Assets/link.xml`; plan wording fixed |
| U2 | UI/UX | Gear fit in the top strip at 360×640 and 200 % text | Placement, focus order and rect rule in the contract |
| U3, F3, F4 | UI/UX, Test/QA | FR-030 focus fallback undefined; existing test asserts old target | Ordered fallback in FR-030; T450 re-baselines `UsingAFixButtonMovesFocusToTheActiveToggleInsteadOfLosingIt` and adds edge cases |
| U4, F4 | UI/UX, Test/QA | "0 s" threshold and copy not fixed | FR-031 pins "+$<amount> at run start" and the "formats as 0 s" rule; boundary tests 0.4/0.5/1.0 s |
| U5–U6 | UI/UX | Sheet states and accessibility unspecified | State table and accessibility bullet in the contract |
| U7 | UI/UX | Scenarios miss FR-031–FR-033 | US8 scenarios 5–7 |
| U8 | UI/UX | No independent review of the Settings sheet | New T455 |
| F1 | Test/QA | T451 named the wrong research test | Copy assert in `OreVeinPanelTests.cs` (Play Mode) |
| F5 | Test/QA | No reproducible CS0618 check | `git grep` gate plus Editor-log check in T452 |
| F6–F8 | Test/QA | Per-task commands, gates, icon assertion | Added to T450–T452 |
| F9 | Test/QA | Ledger lacks the new tasks | Ledger refresh after `$speckit-analyze` |

Play-Tester: omitted for every F004 batch, reason in `plan.md` › Review waves and role disposition.
