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
| UX-1 | **Owner decision required** (host surface of the reduced-motion control) | plan Pending owner decisions |
| UX-2 | Accepted. Radio group with live effective-state line; in-memory apply and "Not saved" note on write failure | research R11; presentation contract |

## Wave 3

Game Designer/PO (acceptance recheck), DevOps/Operations (phase 0, tooling, lease change) and Security/Privacy (local preferences file) review after these fixes; their results are appended below.
