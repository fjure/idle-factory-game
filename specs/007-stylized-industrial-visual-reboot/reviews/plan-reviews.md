# F007 plan reviews (2026-11-27)

The reviews ran on commit `4238827e`. Every lens was read-only and ran in a separate session. None of the reviewers wrote any of the reviewed artifacts. The parent recorded the findings and dispositions below. The reviewers' full reports were returned in-session and are summarized here per finding.

## Verdicts

| Lens | Tier and model | Verdict | Blocker | High/Major | Medium | Low/Minor/Info |
|---|---|---|---|---|---|---|
| Software Architect | High (opus) | APPROVE WITH CONDITIONS | 0 | 7 | 0 | 6 |
| Art Director | Medium (sonnet) | APPROVE WITH CONDITIONS | 0 | 3 | 5 | 2 |
| Technical Artist | Medium (sonnet, shared session with the Art Director, separate section) | APPROVE WITH CONDITIONS | 0 | 5 | 4 | 1 |
| Performance Engineer | Medium (sonnet) | APPROVE WITH CONDITIONS | 0 | 2 | 6 | 2 |
| UI/UX Reviewer | Medium (sonnet) | APPROVE WITH CONDITIONS | 0 | 2 | 5 | 1 |
| World Designer | Medium (sonnet) | APPROVE WITH CONDITIONS | 0 | 0 | 4 | 4 |
| Game Designer / Product Owner | Medium (sonnet) | APPROVE WITH CONDITIONS | 0 | 3 | 0 | 4 |
| Game Systems / Economy (light) | Medium (sonnet, shared session with the PO, separate section) | APPROVE WITH CONDITIONS | 0 | 0 | 0 | 3 |
| Test/QA | Medium (sonnet) | APPROVE WITH CONDITIONS | 0 | 5 | 6 | 2 |
| DevOps | Low (haiku) | APPROVE WITH CONDITIONS | 0 | 0 | 3 | 5 |

No lens rejects the plan.

Two reviewers reported gaps in their own coverage:

- The UI/UX reviewer did not open the PNG captures.
- The Performance reviewer did not open two of its wiki pages.

Neither gap changes a disposition.

## Dispositions

Status values:

- **A** = accepted and applied in this revision;
- **O** = needs an owner decision (section 3);
- **M2** = carried as a written M2 brief or acceptance note.

| ID | Summary | Disposition | Applied in |
|---|---|---|---|
| ARCH-1 | Asset isolation has no mechanism: DevTools is code-only, so prototype assets need a holder | A: dev-only scene with a DevTools holder, added only for `IDLEFACTORY_DEV` builds; BuildReport check with a positive control | R07, look-profile rule 4, T609, T615 |
| ARCH-2 | Renderer switch undesigned: the shipped URP asset has one 2D renderer and a test enforces it | A: route (a), a dev-only second URP asset set through `QualitySettings.renderPipeline`; the release variant count is asserted unchanged | R06, look-profile rule 9, T625 |
| ARCH-3 | Sorting-layer model and opaque meshes in the Universal Renderer do not mix | A: the C1 spike decides depth-placed versus transparent-queue meshes, with order tests as exit criteria | R06, T625 |
| ARCH-4 | Pitch is compile-time `const` and static | A: T610 converts it to instance data in world units, part of `LayoutComposition` identity | R09, data-model, T610 |
| ARCH-5 | The static catalog registry does not fit per-profile keys | A: per-profile requirement tables; the registry stays the F006 table | R07, look-profile rule 3, T609 |
| ARCH-6 | Static global composition and init order (DevTools `AfterSceneLoad` runs after bootstrap `Awake`) | A: the bootstrap owns one resolver; DevTools injects `ILookProfileSource` at `BeforeSceneLoad` | R07, look-profile, T609 |
| ARCH-7 | The parity test bypasses tap→id and frame-time paths | A: scripted taps through `WorldTapResolver`, a fixed time source, per-tick hashes, a negative control, and a no-sim-write guard | R19, look-profile tests |
| ARCH-8 | "1 cell = 1 world unit" is wrong | A: corrected (1 world unit = 24 F006 art px; the cell is the 70×54 lattice) | data-model |
| ARCH-9 | Location count is the constant 12, not content | A: restated as fact | R09, look-profile rule 6 |
| ARCH-10 | The hit-size formula mixes the const density | A: profile-aware, single writer kept | R15, T610 |
| ARCH-11 | Invalid persisted dev selection | A: logged fallback to the shipped default; release ignores the key | look-profile rule 5 |
| ARCH-12 | Cleanup covers only assets | A: full cleanup list | R07 |
| ARCH-13 | C3 "no real lights" overstated | A: reworded | R06 |
| AD-1 | C is under-defined and weaker than B by construction | A: C brief with a ramp/toon shader, contact-shadow blob, capped lamp lights, and a rule to separate model flaws from rendering flaws | R06 |
| AD-2 | The mood is prose only | A: measurable mood anchors in WB v0.3 | R21, FR-007a |
| AD-3 | A carries L2 cost; light ordering unspecified | A: A brief states the L2 light cap and ordering | R04 |
| AD-4 | Rubric anchor 1 = "F006-like" is circular | A: anchors on reference criteria per dimension; F006 is scored, not used as an anchor | SC-002, R18 |
| AD-5 | Rubric bar, non-visual dimensions, thin sample | A: 8 visual dimensions; performance and before/after handled separately; disagreement rule; calibration | SC-002, R18 |
| AD-6 | Blinding impossible | A: stated; matched framing and shuffled order | R18 |
| AD-7 | Indicator keep-out against taller bodies; relative size only | A: absolute pt floor; keep-out per look; dense-cluster test | FR-027b, R10 |
| AD-8 | Upgrade marker may collide at the denser pitch | A: side placement allowed; captured in the proposed geometry | R10 |
| AD-9 | Standard view transform clips emissive highlights | A: keep Standard; emissive via the mask, no bloom; one-asset AgX test recorded | R05 |
| AD-10 | Originality guard is a checklist only | A: prompt lint, prompt and seed log, no reference screenshots as img2img or ControlNet input | R14 |
| TA-1 | ASTC bytes per px wrong by 2× | A: corrected (6×6 = 0.444 B/px, 8×8 = 0.25 B/px) | R05, R13 |
| TA-2 | Canvas inconsistent with 128 px/cell; layers missing | A: canvas 427×341; memory re-derived with shadow, emissive and mips | R05 |
| TA-3 | Emulator ASTC support and the hang hypothesis are untested | A: the phone is authoritative; record ASTC support; sweep bytes against texels | R13 |
| TA-4 | Per-pass trim misaligns the passes | A: union-bbox trim and alignment check | R14 |
| TA-5 | Shadow and emissive blend, sorting and format unbudgeted | A: R8/A8 shadow, blend materials, sorting layers, SetPass estimate | R14, R17 |
| TA-6 | Reproducibility pins missing | A: CPU, seed, denoiser off, versions; SSIM set from a two-render test | R14 |
| TA-7 | Bilinear, mips and padding bleed; tight meshes | M2: brief and import rules | R14 |
| TA-8 | `mesh-export` has no checks; sorting for meshes | A: checks listed | art-pipeline-v2 |
| TA-9 | A page plan ignores padding; n_min text | A: 9 frames per page; text fixed | R04 |
| TA-10 | `Art/F006/` trigger | A (see OPS-3) | T602 |
| PERF-1 | GPU time and overdraw missing | A | R17 |
| PERF-2 | ASTC only meaningful on the phone; 3 cold loads, report the max | A | R13 |
| PERF-3 | Memory understated | A (with TA-1, TA-2) | R05 |
| PERF-4 | SetPass is an Editor reading | A: on-device pass counts | R17 |
| PERF-5 | Budget set v2 contents | A: list fixed for the M4 task | R17 |
| PERF-6 | C1 spike gate too loose | A: absolute budgets, URP asset settings listed | R06 |
| PERF-7 | Device state protocol | A | R16 |
| PERF-8 | W007-D undefined | A: defined | R17 |
| PERF-9 | Dev-build overhead | A: label every number; release builds for SC-011 | R16 |
| PERF-10 | Steady state excludes the profile swap | A | look-profile rule 10 |
| UX-1 | Indicator at 60 % area falls below the 24 pt glyph floor | A: the glyph keeps the 24 pt floor at n_min on every class | FR-027b, R10 |
| UX-2 | Lights-off is tonal; reduced motion removes motion | A: the glyph is the normative carrier; SC-008 adds static and reduced-motion naming | FR-027b, SC-008 |
| UX-3 | Integrated glyph has no contrast field | A: plated or outlined glyph with a contrast floor | FR-027b, R10 |
| UX-4 | Overhang tap ambiguity | A: OverhangTapTests assertions (front-most visible machine wins; selection frame visible; pt separation) | R19, look-profile rule 7 |
| UX-5 | 640×360 missing | A: reduced check at M2, binding class at M6 | FR-011a |
| UX-6 | SC-006 satisfiable by fill | A: paired with SC-004 and a salience rule; per class and per state | SC-006 |
| UX-7 | M2 UX check per direction | A: select front, back and edge machine; card; record pass or fail | R18, T634 |
| UX-8 | Name the F006 suites that must pass under every profile | A | R19 |
| WD-1 | WB v0.3 scope incomplete | A | R21 |
| WD-2 | A-6 wording implies a hidden source | A: "utility runs serving the site" | spec A-6 |
| WD-3 | Background implies a wider world | A: background rules | R15, R21 |
| WD-4 | Miner+Smelter pairing implies a chain | A: grouping never by production chain | R09, FR-024 |
| WD-5 | v0.3 is non-additive | A: MINOR, with a migration table and a §7 clarification | R21 |
| WD-6 | Forbidden-term list | A | R21 |
| WD-7 | MINOR/MAJOR left open | A: MINOR recommended, decided in the version | R21 |
| WD-8 | Stage legibility in the dusk look | A | R21 |
| PO-1 | No owner hands-on play on the Pixel | **O-1** | — |
| PO-2 | No owner checkpoints inside M2 | **O-2** | — |
| PO-3 | The SC-008/009 baseline depends on unwritten F006 T546 | A: T546 is a precondition of T630; fallback task T641 reproduces the F006 baseline if T546 is not on `main` by then | R18, T630, T641 |
| PO-4 | Rubric dimensions | A (with AD-5) | SC-002 |
| PO-5 | SC-004 area definition | A: body area excluding shadow and overhang reported separately | SC-004 |
| PO-6 | SC-006 fill-only | A (with UX-6) | SC-006 |
| PO-7 | No revise loop | A: T640 conditional revise task | plan |
| GS-1 | Thresholds 10/25/50 from the existing speed milestones | **O-3** | — |
| GS-2 | Stage behaviour after prestige | **O-3** | — |
| GS-3 | Stage fixture must be reachable | A | R11 |
| QA-1, QA-2 | Parity test specification; mid-run switch; save-byte purity | A | R19 |
| QA-3 | Release isolation via BuildReport; extend `check-release-build.ps1`; Edit Mode reference guard | A | R07, T615 |
| QA-4 | DevTools tests skipped by the standard runs | A: defined runner path and a non-zero count gate; profile-agnostic cases in the normal suites | R19, T609 |
| QA-5 | Re-baselining without tooling | A: `reviews/rebaseline-map.md` and a test-id comparison script (T638) | R19 |
| QA-6 | Missing edge-case tests | A: listed | R19 |
| QA-7 | SC-010 method | A: effect-spawn counter and frozen-clock pixel compare | R18 |
| QA-8 | Metric scripts need known-answer tests | A | R18, T603 |
| QA-9 | Capture seeds, sidecar schema, validator | A | R08, evidence contract |
| QA-10 | SC-012 fixtures and suites named | A | R19 |
| QA-11 | Per-profile allocation wiring; page cap stays strict | A | R19 |
| QA-12 | 44 pt and non-overlap tests with a negative case | A | R19 |
| OPS-1 | `emulator` lease already covers the phone | A: docs only | T602 |
| OPS-2 | `-Serial` required when both devices are attached | A | R16, T602 |
| OPS-3 | Art-path triggers miss `Art/F006/` (live gap) | A | T602 |
| OPS-4 | Release builds exclude DevTools | Confirmed, no action | — |
| OPS-5 | Settings-noise check does not cover the renderer settings | A: manual diff review of GraphicsSettings and QualitySettings in the commit | art-pipeline-v2, T625 |
| OPS-6 | Phone performance needs the `gpu` lease | A: `unity,emulator,gpu` | R16 |
| OPS-7 | Resource labels and the Windows player batch gate | A | plan lane |
| OPS-8 | adb from the main checkout | A | T602 |

## Owner decisions requested

- **O-1 (PO-1)**: Should the owner play each prototype direction hands-on on the Pixel before the M3 decision, using a profiler-free dev APK with the profile switch?
- **O-2 (PO-2)**: Should M2 have owner checkpoints? These are go/adjust checkpoints, not eliminations, which keeps OD7-2. Proposed checkpoints:
  1. after the three briefs and WB v0.3;
  2. after one machine per direction in-engine;
  3. at the C1 spike result: continue, adjust, or stop C.
- **O-3 (GS-1, GS-2)**: The build-out stages would use the existing speed milestones at levels 10, 25 and 50 (`iron-factory-content.json`, `milestones`). The stages are a pure function of the current level, so they reset with prestige. Is that acceptable, given that the stage changes then coincide with the existing 2×/2×/3× speed milestones and read as a milestone cue?
