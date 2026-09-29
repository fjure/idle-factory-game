# Feature 003 plan review wave (T088)

**Date:** 2026-09-29
**Reviewed artifacts:** `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `tasks.md` at commit `a03cb26` on `feat/003-first-factory-prestige`.
**Mode:** read-only, `max_parallel: 3`. Compatible lenses shared one review where AGENTS.md allows it (Gameplay Developer + Performance, Security/Privacy + DevOps, UI/UX Reviewer + Art Director). Each lens reviewed independently of the plan author; no reviewer edited artifacts.

**Omitted roles:** Online Services (no remote boundary), Sound Designer and Composer (no audio or music in scope), 3D/Render Artist (no 3D-to-2D pipeline). Game Designer/Product Owner and Software Architect verdicts on the implementation follow in T132.

## Summary

| Lens | Verdict | Critical | High | Medium | Low |
|---|---|---|---|---|---|
| Test/QA | Approve with findings | 0 | 2 | 6 | 3 |
| Gameplay Developer | Approve with findings | 0 | 1 | 3 | 2 |
| Performance Engineer | Approve with findings | 0 | 2 | 3 | 2 |
| Security/Privacy | Approve with findings | 0 | 1 | 2 | 2 |
| DevOps/Operations | Approve with findings | 0 | 1 | 2 | 2 |
| Game Systems/Economy | Approve with findings | 1 | 2 | 4 | 2 |
| UI/UX Reviewer | Approve with findings | 0 | 2 | 5 | 3 |
| Art Director | Approve with findings | 0 | 1 | 3 | 2 |

No lens rejected the plan. Every finding is dispositioned into an artifact change (see "Disposition" at the end); product questions went to the owner.

## Owner decisions from this wave (2026-09-29)

| ID | Finding | Decision |
|---|---|---|
| OD-6 | ECO-1: the 0.1 s minimum effective cycle caps a 2 s recipe at 20× speed, so later multipliers add nothing and $10M at ~120 min is unreachable. | **Speed beyond the event bound becomes batch size.** A cycle is never shorter than the content-defined minimum (0.1 s); the part of the combined speed multiplier above that bound multiplies the inputs and outputs per cycle instead. Effective rates follow the full multiplier, every bonus keeps working, and the event-count and offline performance bounds hold. Visible effect: goods arrive in larger packets at the bound. |
| OD-7 | OPS-1: after a V2 import, progress that an older Feature 002 build later writes to `v1/` is ignored by Feature 003. | **Document the loss in both directions** (development saves only; no public release). Release note and T131 checklist; no detection code. |
| OD-8 | UX-7: Research presentation was "graph or list". | **Graph.** It needs a specified keyboard traversal order, pan/zoom or scroll at phone width, 44 pt nodes and a layout test at every screen class. |
| OD-9 | Follow-up to OD-6 while applying dispositions: a max-level batch (~24k units) can exceed the 16k top storage. | **Partial batches.** A due cycle completes the largest whole number of recipe units that inputs and free capacity allow (at least one, otherwise it waits); the remainder is dropped. Atomic and deterministic; also applies in the OD-2 retry pass. |

## Findings

### Test/QA

Verdict: APPROVE WITH FINDINGS. Every FR/SC has a named suite and the Edit/Play/StandalonePlayer/emulator split is coherent (verified: `test-unity.ps1` parameters, `GcAllocationCounter`, asmdefs, six V1 fixtures, emulator script without `-Scenario`). The gaps are missing tests or oracles, not design defects.

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| QA-1 | HIGH | save contract V1 import; T106 | Failure codes `import.v1.invalid`, `import.v1.clock`, `import.arithmetic`, `import.persistence` have no test or fixture. | T106: one test per import failure code (corrupt V1 fixture, clock-missing case, overflow through the port fake); V1 bytes unchanged, nothing published. |
| QA-2 | HIGH | T121; plan test strategy | T121 imports a V1 fixture in the Windows player, but the fixtures live in an Editor-only test location that no player build contains. | Name the packaging (Play Mode TextAsset copy) with a hash-equality test against the Edit Mode fixture. |
| QA-3 | MEDIUM | plan event order; T098 | Equivalence compares the implementation with itself; no seeds/counts; no multi-producer capacity contention; no same-instant expiry-and-completion case. | T098: tick-by-tick reference oracle for small scenarios, fixed seeds with failing-seed report, per-step conservation invariant, multi-producer contention and same-instant expiry cases. |
| QA-4 | MEDIUM | T102, T105; run-application `ConfirmPrestige` | Missing negatives: double confirm while pending, autosave/suspend/offline return during a pending transition, restart with `previousRunId == runId` through the real store, second prestige not treated as duplicate. | Add to `PrestigeTransitionTests`; the contract states whether `SaveNow` is blocked or queued while pending. |
| QA-5 | MEDIUM | save contract load rules 2–3; T104 | No tests for duration-retune rescale, past-due effects at load, or save/load continuation equivalence. | T104: three tests with a mid-run fixture (active effect, mid-cycle machine). |
| QA-6 | MEDIUM | T103 | Missing: boundaries collapsing to the same effective instant, `r == d` and `r == d+cap`, offline contention vs online. | T103 cases. |
| QA-7 | MEDIUM | plan Performance; T098, T122, T124 | No single shared W003 workload definition. | One `W003Workload` fixture builder reused by T098/T122/T124; workload hash in `w003-measurements.md`. |
| QA-8 | MEDIUM | OD-3; T106, T110, T118 | How "V1 no longer offered after Start new run" is remembered is unspecified and untested. | The fresh V2 checkpoint suppresses V1 selection; `RunBootstrapTests` restart case. |
| QA-9 | LOW | save contract fixtures; T106 | Fixture naming differs between contract and task; the machine count of `06-w001-scale` is unverified. | Align names; T106 asserts the count of `06` first. |
| QA-10 | LOW | T119, T120 | Retiring Feature 001 tests has no before/after inventory. | T119 records retained/deleted test classes with reasons; T120 compares counts, zero unexplained skips. |
| QA-11 | LOW | quickstart §5; T103 | Rotation-without-departure is mapped to the wrong suite. | Point to `RunLifecycleTests` and `OfflineSummaryPanelTests`. |

### Gameplay Developer

Verdict: APPROVE WITH FINDINGS. The planned names and namespaces fit the existing four-assembly layout; Core stays engine-free.

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| GD-1 | HIGH | T093; `JsonCheckpointStore`, `CheckpointStoreTests` | T093 makes `JsonCheckpointStore` read-only and also requires the existing suites to pass unchanged, but those suites call `Promote`/`RecoverPrevious`. Both cannot hold. | `JsonCheckpointStore` keeps its V1 write API as a thin delegate over `CheckpointGenerationFiles` for legacy tests; only the new `SaveV1CheckpointSource` is read-only and wired by bootstrap. T119 `RunAuthorityTests` proves bootstrap never references `JsonCheckpointStore`. |
| GD-2 | MEDIUM | data-model; T099, T103; `Core/Application/Contracts.cs` | New `OfflineReturnSummary`/`FactorySnapshot` collide with legacy types (CS0104 in files importing both until T119); the legacy `CommandOutcome` lacks overflow/limit reasons. | Distinct names `RunOfflineSummary`, `RunFactorySnapshot`; T099 defines `RunCommandResult` with its own reason codes and leaves the legacy enum unchanged. |
| GD-3 | MEDIUM | R01/R05; T098 vs T103 | Effects/deadlines are stored in UTC ms but offline advance runs on the effective timeline; T098 does not define how boundaries reach the simulator, and T103 depends on that API. | T098 fixes `RunSimulator.Advance` to accept caller-supplied sim-time boundary events ordered by `(kind, id)`; a test shows online and `OfflineSegmenter` use the same entry point. |
| GD-4 | MEDIUM | T096, T099, T104 | Oversized for single MAQA issues. | Split T096 (reader+validator / builder+dispatcher+authoring check), T099 (session+timeline+revisions+hot reads / factory commands+snapshots+rates), T104 (DTOs+mapper / store+lifecycle). |
| GD-5 | LOW | gameplay-content-v2 example; R04 | The example `maxCombinedSpeedPpm` (1000×) violates its own `speed.bound.too-fast` rule for a 2 s recipe; milestone ×60 combined could hit the guard. | Fix the example; T097 confirms the content table stays under the cap. |
| GD-6 | LOW | T095/T098 | Iteration order of machine/effect/priority collections is not pinned. | T095: arrays/lists with fixed-order iteration, no hash-order dependence. |

### Performance Engineer

Verdict: APPROVE WITH FINDINGS. The 12-machine linear scan is sound; the risk is the worst-case offline budget and measurement gaps.

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| PE-1 | HIGH | plan Performance; R04; T122 | The 8 h offline workload is not tied to an event count; W003 at 50% store may block early and hide the ~2.8M-completion worst case. | Add a stress variant (auto-sell on all resources, recipes at cap); record completions, timestamps and ns/event; name which variant carries the 500 ms / 2 s budgets. |
| PE-2 | HIGH | plan metric 3; T122, T124 | The emulator 8 h return budget (≤ 2 s, median of 3) has no measuring task. | Add a device-side timing step around `ResumeAndReconcile` to T123/T124. |
| PE-3 | MEDIUM | plan Baseline; T122 | Feature 001 baselines measure the closed-form model and are not comparable. | Declare no comparable baseline; the first W003 run is baseline r1; W001 stays a legacy regression reference. |
| PE-4 | MEDIUM | T098, T103 | `GcAllocationCounter` counts alloc samples, not bytes; Edit Mode timings are editor/Mono only; player allocations unchecked. | Budget "0 GC.Alloc samples per warmed Advance"; add a Play Mode/player allocation check on the host frame path; label Edit Mode timings informational. |
| PE-5 | MEDIUM | plan Workload; T099; T122 | Frame metric covers only `Advance`, not read-model copies and HUD refresh. | T122 times the full host frame; assert near-zero `StructureRevision` rebuilds in steady state. |
| PE-6 | LOW | T123 | How the W003 V2 state reaches the device (and avoids an offline credit) is unspecified. | T123 pushes a committed W003 Save V2 fixture with departure ≈ push time and verifies its hash; README row in T104. |
| PE-7 | LOW | R04; T098 | Pass count per timestamp (OD-2 retry, ≤ N+1) is unpinned. | Test-only counter; assert ≤ N+1 in the equivalence suite and feed PE-1 records. |

### Security/Privacy

Verdict: APPROVE WITH FINDINGS

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| SEC-1 | HIGH | research R10; T106; save contract | Cold-start source selection does not separate "V2 absent" from "V2 present but corrupt/unknown schema"; a corrupt `v2/` would fall through to V1 import, replacing prestige/IP progress and re-crediting the V1 offline interval. | Import only when `v2/` has no checkpoint files; present-but-invalid V2 takes the Feature 001 recovery path, never import. Test: corrupt V2 + valid V1. |
| SEC-2 | MEDIUM | save contract load rules; data-model | No cross-field temporal/consistency bounds: far-future effect/deadline/overdrive instants, start after departure, `runNumber` vs `prestigeCount`, `previousRunId` on run 1. | T104 mapper invariants plus one corrupt fixture each. |
| SEC-3 | MEDIUM | save contract envelope | Duplicate-member rejection and integer-only parsing are stated, but the parser is not; Newtonsoft defaults to last-wins. | T104 names the parser and rejects duplicates at token level; tests for duplicate key, `1.0`, `1e3`, BOM, >1 MiB, depth 17, int64 > 10^18. |
| SEC-4 | LOW | R08 storage layout | Manifest hashes detect corruption/torn writes, not authenticity (accepted local risk, R17). | Record in the T131 threat register. |
| SEC-5 | LOW | R17 diagnostics | No task asserts that V2/import failure messages carry only static codes, not file-derived text. | T104/T106 assertion. |

### DevOps/Operations

Verdict: APPROVE WITH FINDINGS

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| OPS-1 | HIGH | R17; T131 | Progress made in `v1/` by a Feature 002 build after a V2 import is silently ignored by a later Feature 003 build (V2 active means never re-import). Only the 003→002 direction is documented. | OWNER decision (see below). |
| OPS-2 | MEDIUM | save contract; T106 | Restart after an interrupted import promotion: reuse of the pending candidate vs recomputation with a fresh return instant is unspecified. | T106 specifies it; the test asserts it. |
| OPS-3 | MEDIUM | T093 | The generation-protocol extraction lacks a byte-level characterization of the V1 read path. | T093 golden test: identical `Selection` and hash over the six V1 fixtures. |
| OPS-4 | LOW | T123 | The emulator script acquires its lease before any step; the lease-conflict exercise must use a dummy owner without `adb`. The `-Scenario` default must keep existing behaviour. | T123 acceptance. |
| OPS-5 | LOW | T104 | The V2 fixture byte-compare must fail, not skip, when bytes drift without the regenerate flag. | T104 acceptance. |

### Game Systems/Economy

Verdict: APPROVE WITH FINDINGS. ECO-1 blocks the content-sheet task T097, not the plan structure.

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| ECO-1 | CRITICAL | R04 vs gameplay-content-v2 `speed.bound.too-fast`; FR-002; R03 | The 0.1 s minimum cycle at the global speed cap limits a 2 s recipe to 20× speed, reached near level 22. Higher levels, the ×3/×5 milestones, Research, Industry, Items and Overdrive then add nothing, and income tops out near $190–760/s, so $10M at ~120 min (≈ $1.4k/s average) is unreachable. The shape example (1000×) fails its own rule. | OWNER decision on how rate beyond the event bound is expressed (see decisions). Add a validator rule that no level/milestone below `maxLevel` is dead, and record the theoretical max income. |
| ECO-2 | HIGH | R13; T107, T125; SC-003/004 | No numeric pass/fail bands for the 10–15 min Gear, ~120 min eligibility and 70–80 min second run; the reference policy has no action latency or idle rules; sensitivity cases have no criterion. | `balance-policy.md` fixes bands (e.g. first Gear 600–900 s, eligibility 6,600–7,800 s, second run 4,200–4,800 s), sensitivity bounds, policy latency/idle windows and a no-perfect-timing rule; the golden CSV proves determinism, not plausibility. |
| ECO-3 | HIGH | T107; R11, R13 | The trace stops after run 2; compounding Industry upgrades could collapse run 3+. | Add a run 3–5 projection (spend all IP); assert monotone non-increasing run times above a floor (e.g. ≥ 25% of run 1); cap Industry per-level effects and `maxLevel`. |
| ECO-4 | MEDIUM | R05; R13 | Offline progress (8 h at 80%) is not in the sensitivity set and could skip much of the 120 min target. | Sensitivity variants: 8 h absence at minute 30/60 and repeated returns; if material, a new offline policy revision is an OWNER decision. |
| ECO-5 | MEDIUM | gameplay-content-v2 curves/contracts; FR-015 | Contract reward-to-market ratio grows unbounded with re-acceptance scaling; RP can be farmed. | Validation/trace invariant `1 ≤ reward / marketValue ≤ R_max` (content) for every reachable completion index; per-run or per-index caps for Cash/RP. |
| ECO-6 | MEDIUM | R12; effects table | Factory-target and type-target Items of the same kind do not conflict and stack multiplicatively; max combined multiplier and Item accrual across runs are unspecified. | Content record lists the maximum combined multiplier per kind (≤ cap, no dead source); bound Item accrual per run and state whether the persistent stack is capped. |
| ECO-7 | MEDIUM | R11; data-model | `removalRefundPpm` allows 1,000,000, which would break the strict Factory Value loss on build/remove; a Research sale-price bonus raises stock value in FV (bounded, ~$0.4M). | Validator range 0–999,999 and a test that build/remove strictly lowers FV (also under cost discounts). The sale-price effect on FV follows FR-020 ("at market prices") and is recorded as bounded. |
| ECO-8 | LOW | R06; curves | Per-step rounding can stall a floor curve (base 1 × 1.15 stays 1); sale vs FV rounding differ slightly. | Validator: curves strictly increase unless declared flat; document and test per-sale rounding. |
| ECO-9 | LOW | R13; T125 | Telemetry/safe-adjustment template lacks a bounded step. | T125 records per value: hypothesis, signal, step ≤ ±20% per revision; a step moving eligibility outside the ECO-2 band needs owner approval. |

Already sound: integer ppm composition and atomic overflow rejection (R03, R06); FV with machines and full invested cost leaving FV on removal; the pinned IP table; `industry.first-affordable.missing`; the trace runs real commands.

### UI/UX Reviewer

Verdict: APPROVE WITH FINDINGS. State coverage is good; the OD-3 import actions and OD-4 abandon are present; Remove and Prestige confirmations show refund/FV and cancel on rotation.

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| UX-1 | HIGH | ui-surfaces Global; T112 | Phone-portrait density is undefined (six tabs, sheets, 12 slots at 360×640; 360 pt tall when rotated). | Contract density rule: sheet max height, selected slot kept in view, HUD top-strip contents, Overdrive placement per screen class; tested in `RunHudLayoutTests`. |
| UX-2 | HIGH | ui-surfaces Global; T112–T118 | Focus management under-specified (modal trap, initial focus, focus return, Escape/Back, tab-bar arrows, focus after rotation cancel). | Contract rules; `RunKeyboardFocusTests` covers every confirmation (Remove, Prestige, Start new run, Item activation, Abandon). |
| UX-3 | MEDIUM | Evidence; T112 vs T113–T118 | Worst-case layout per panel is untested at rotated classes. | Each panel task adds a max-content layout test at all classes. |
| UX-4 | MEDIUM | T112 → T113–T118; T128 | The only UI/UX review is after all panels; a shell flaw would invalidate six parallel panels. | Early independent UI/UX review gate after T112, before T113–T118. |
| UX-5 | MEDIUM | ui-surfaces Contracts; T115 | Abandon (OD-4) is missing from the contract; confirmation and rotation behaviour undefined. | Add Abandon with a confirmation; include in confirmation and rotation tests. |
| UX-6 | MEDIUM | T118; Startup row | Rotation cancelling "Start new run" is not in T118. | T118 acceptance and test: rotation cancels it, V1 stays offered. |
| UX-7 | MEDIUM | ui-surfaces Research | "Graph or list" undecided. | OWNER → OD-8 (graph). |
| UX-8 | LOW | Global Numbers | Long-press for exact values is undiscoverable and may collide with pan/drag. | Tap-to-expand/detail line for touch; long-press threshold and conflict rules. |
| UX-9 | LOW | Global Rotation | Silent cancel on rotation gives no feedback. | One-line text notice "Cancelled by rotation"; in the rotation test. |
| UX-10 | LOW | Inventory; T113 | Machine blocker copy does not name auto-sell as the starvation cause. | Blocker copy names auto-sell; `MachinePanelTests`. |

### Art Director

Verdict: APPROVE WITH FINDINGS. Brief structure matches the Feature 001 inventory format; gate order T090 → T091 → T108 → T109 → T111 → T112, T129 last, lets nothing skip Art Direction before Technical Art.

| ID | Severity | Artifact§ | Finding | Disposition |
|---|---|---|---|---|
| ART-1 | HIGH | T090 vs F001 brief | Measurable readability rules are only summarised (zoom px-per-cell targets, 4.5:1 badge contrast, grayscale/CVD checks, numeric noise limits). | Art Bible v0.1 carries them; T109/T111 verify. |
| ART-2 | MEDIUM | T090; ui-surfaces slot grid | No state/marker precedence or per-slot marker cap; locked-slot cue unstated. | Precedence table, cap (one state badge + at most two markers), unique silhouette plus text/shape cue per state. |
| ART-3 | MEDIUM | R15; T090, T108 | UI icon families are not enumerated. | Enumerate icon IDs in the briefs; every `presentationKey` and UI icon has a row. |
| ART-4 | MEDIUM | R15 | No UI theme tokens (palette roles, focus ring, min text size, disabled look, contrast). | Short UI tokens section in the Art Bible; reference for T112–T118. |
| ART-5 | LOW | T129 | Reviewer independence from T090/T111 authors not stated. | T129 reviewers are different sessions from T090 and T111. |
| ART-6 | LOW | T111; R15 | Tool version and no-silent-substitution rule only by reference. | Repeat in the Art Bible. |

## Disposition

All findings are applied to the planning artifacts in the commit that follows this record (see the "Plan review wave" note in `tasks.md`). Findings marked OWNER were decided as OD-6 to OD-9 above. ECO-4 (offline sensitivity) is measured by the balance trace; a change to offline policy revision 1 would be a new owner decision.
