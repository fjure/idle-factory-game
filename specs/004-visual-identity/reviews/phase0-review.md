# Phase-0 Review Record: T411 Render Switch (T413)

Scope: T413 independent review of the phase-0 render switch, T411 (#523, PR #583, commits `a3ca464` and `1a5e533`, merge `b9221ca`, diff `5906797..1a5e533`).

Date: 2026-10-28. Covers FR-004, FR-005, SC-006, SC-008.

Reviewers: independent Software Architect, Test/QA and DevOps/Operations agents, none of them the T411 author or its MAQA QA. Each verdict was given separately. No Unity, build or emulator run was made for this review; the reviewers read the code at `1a5e533`, the worker's build and test artifacts and the MAQA QA evidence.

Overall: phase 0 is accepted with follow-ups. No blocker and no critical finding.

## 1. Verdict summary

| Lens | Verdict | Blockers | Majors |
|---|---|---|---|
| Software Architect | APPROVE-WITH-FOLLOW-UPS | 0 | 0 |
| Test/QA | APPROVE-WITH-FOLLOW-UPS | 0 | 1 (F1, record) |
| DevOps/Operations | APPROVE-WITH-FOLLOW-UPS | 0 | 1 (OPS-4, operational) |
| **Overall** | **Phase 0 accepted with follow-ups** | 0 | 2 |

## 2. Software Architect

Verdict: APPROVE-WITH-FOLLOW-UPS (no blocker, no major).

- A1 (note) Boundaries hold. No runtime `.cs` in the diff; only test-side code (`RenderPipelineConfigurationTests`, `RenderParityTests`, `RenderCaptureHarness`). `git grep UnityEngine.Rendering.Universal` in non-test `Assets/**/*.cs` is empty. The config test resolves URP types by string name (`RenderPipelineConfigurationTests.cs:27`), so `IdleFactory.EditModeTests.asmdef` gains no URP reference. Domain, Simulation and Persistence are untouched. Disposition: none.
- A2 (note) Asset ownership. The four new assets sit in `Assets/IdleFactory/Rendering/` with `.meta` files, as the contract requires. The 2D Renderer `m_DefaultMaterialType` is 1 (Unlit, R01). Disposition: none.
- A3 (note) Packages. The manifest adds only URP 17.6.0. The lock adds its transitives: render-pipelines.core, universal-config, shadergraph, graph-authoring, burst 2.0.0, collections 6.6.0, mono-cecil 1.11.6, searcher 4.9.5, profiling.core 1.0.3, test-framework.performance 6.6.0. R01 named only core, shadergraph and universal-config; the rest are justified transitives. Disposition: record the full list here; R01 and the wiki are updated at the next ingest.
- A4 (note) Rollback is atomic. The change set is limited to the manifest, the lock, `Rendering/`, GraphicsSettings, QualitySettings, ShaderGraphSettings and a camera-only scene diff (+45 lines). No Core, content or save change. Reverting the merge restores Built-in (worker dry-run passed); the new `ShaderGraphSettings.asset` is deleted cleanly by the revert. Disposition: none.
- A5 (minor) Path extensions acceptable. `ShaderGraphSettings.asset` is unavoidable tooling output (defaults). `RenderCaptureHarness.cs` changed only the comparator. Both were parent-approved and recorded on #523. Disposition: recorded as contract deviations in section 4.
- A6 (minor) The comparator is now R13-literal (`RenderCaptureHarness.cs`, `DifferingThreshold = 2`), so the 0.1 % budget can never decide: any counted pixel already breaks the 2/255 channel rule. This is a contract inconsistency in R13 and the render-foundation Gates row, not a defect, and it is covered by the owner decision of 2026-10-27. Two parity unit-test expectations changed against the contract line "no test expectation changes in phase 0", also covered by that decision. Disposition: recorded in section 4; one-line erratum in R13 and the Gates row that the budget is vestigial, or give it meaning later (for example a report-only count of delta > 0). No code change now.
- A7 (minor) The cold-run guard (`ShaderUtil.allowAsyncCompilation = false` in SetUp and TearDown) is test-local, restored afterwards, and does not change the runtime. It fixes a real first-frame flake. Disposition: T412 confirms that the 25 to 30 min Play Mode duration is not caused by synchronous compilation.
- A8 (minor) `UniversalRenderPipelineGlobalSettings.asset`: the legacy top-level stripping fields (about lines 20 to 31, value 0) differ from the authoritative managed-reference values (about lines 173 to 176, value 1). The legacy fields are ignored serialization remnants in 17.6. QA saw the asset stable over 5 runs; the worker's emptied `m_List` was not reproduced. Low but real operability risk (a noisy re-save on a version bump or session change). Disposition: T412 diffs the asset after its Android builds; a cheap Edit Mode guard (non-empty `m_List`, managed stripping values present) in a later F004 task; wiki pitfall note.
- A9 (note) Evolution. R03 uses a controller-owned integer scale model, not URP PixelPerfectCamera. The 2D Renderer and camera renderer index 0 stay in place; Unlit and Gamma keep the PPU change independent. `m_Strip2DUnusedVariants = 1` strips the 2D-light variants, so the optional S4 2D-light accents would need it set to 0 and the variant budget (490 vs 693) re-measured. Disposition: note for the S4 task and the wiki.

## 3. Test/QA

Verdict: APPROVE-WITH-FOLLOW-UPS (no blocker). No Unity, build or emulator run. Player 39/39 and the Android journey PASS were taken from the worker and MAQA QA evidence, not re-inspected.

Coverage checked (no finding):

- Contract coverage in `RenderPipelineConfigurationTests` (11 tests): pipeline in Graphics (:49-52); all 6 quality levels, a null level fails (:55-63); global settings exist and are registered by GUID in `GraphicsSettings.asset` (:66-76); Gamma (:79); exactly one renderer, the 2D Renderer, at default index 0 (:85-95); HDR, depth and opaque off, MSAA 1 (:98-105); default material Unlit (:108-116) plus a runtime SpriteRenderer shader probe (:119-132); camera `m_RendererIndex` 0 and camera post off (:135-152); no Light2D in the scene (:155-170).
- Not covered by the tests: the package version 17.6.0 pin, Renderer2D post-processing data, the "scene diff camera only" rule (review-only) and the rollback state (its own gate).
- Parity: 9 screen classes (640x480, 1280x720, 1920x1080, 360x640, 390x844, 640x360, 844x390, 768x1024, 1024x768) times 4 frames (w003s world and shell, w004v n_min and fit-all) = 36 comparisons. Everything named in R13 and the contract is present. The fractional-ratio and adb-screencap uniformity check belongs to the emulator journey.
- Manifest parity: `manifest-baseline.txt` and `manifest-urp.txt` differ only by the 11 new config tests (Edit 2363 to 2374; Play 2275, unchanged; all Passed). Caveat: the manifest cannot show the two changed assertions (F2).
- Negative paths: a revert to Built-in fails the default-pipeline, quality-level, Unlit probe, camera and global-settings tests. There is no mutation test; the rollback dry-run is its own gate.
- Comparator tests pin both limits (`AChannelDifferenceOfThreeFails` :119; 2/255 pass case :107).

Findings:

- F1 (major, record) The 0.1 % budget is unreachable under literal R13, and two budget tests are vacuous or mislabelled (`RenderParityTests.cs:128` and `:139`; `RenderCaptureHarness.cs:56`). A pixel counts only above delta 2, so `Differing > 0` implies `MaxChannelDelta > 2`, which already fails. `MoreThanOneTenthOfAPercentOfDifferingPixelsFails` (:128) now fails on max delta and no longer isolates the budget. `ExactlyOneTenthOfAPercentPasses` (:139) uses delta 1, so `Differing = 0` and it is vacuous. `Passes` is unchanged, and `MaxDifferingFraction` is dead. Missing: a test that a frame with every pixel shifted by 1/255 passes, which is the behaviour the owner decision introduced. Disposition: recorded here with the two assertion changes; a test-hardening follow-up renames them, adds the uniform 1/255-shift pass test, or drops the dead `MaxDifferingFraction`.
- F2 (minor) The contract line "no expectation changes" conflicts with the owner-approved edits at `RenderParityTests.cs:114` and `:132`. Disposition: deviation and owner decision recorded in section 4.
- F3 (minor) The cold-run guard has no direct assertion and rests on n = 1 for the original flake. One QA Play Mode run timed out at 900 s and a later full run passed 2275/2275. There is no warm-up or retry that detects a frame with skipped draws. Disposition: note; an optional warm-up frame in a later hardening task; T412 watches the cold-run time.
- F4 (minor) No pin for package 17.6.0 and no Renderer2D post-processing assertion. Disposition: later hardening task or note.
- F5 (note) The Light2D check is name-based and has no positive control (`:162`), so it would pass vacuously if the type were renamed. Disposition: later hardening task.
- F6 (note) Parity uses a harness-built RenderTexture capture. Real Game-view and device output is covered only by the emulator journey. Disposition: note.

## 4. DevOps/Operations

Verdict: APPROVE-WITH-FOLLOW-UPS (no blocker). Read of artifacts and docs only. Build evidence (`build-report.tsv`, `build.log` in `artifacts/f004/t411-urp-build` and `t411-urp-build-run1-nostrip`) is worker-produced.

- OPS-1 (minor) Build reproducibility is adequate, though not bit-for-bit proven. `build-report.tsv` names `T411-urp-pre-commit.apk`, built from the pre-commit tree with identical content. The shipped `T411-urp-a3ca464.apk` hash and size match per QA. Development IL2CPP, comparable to B1. `packages-lock.json` is committed (deterministic graph). Disposition: T412 states the commit and clean-Library state of its builds.
- OPS-2 (minor) Clean-reimport evidence is sufficient for the gate: the worker's full suite after deleting `Library/` gave an identical manifest, and QA saw the global settings stable over 5 runs. Disposition: T412 diffs `UniversalRenderPipelineGlobalSettings.asset` after its Android builds.
- OPS-3 (minor) Settings noise. `check-settings-noise.ps1` covers only the `ProjectSettings.asset` and `EditorSettings.asset` signatures; R01 already notes it cannot see QualitySettings or pipeline assets. No checker change now. Pitfall line: check the git diff of the URP global settings asset and `ShaderGraphSettings.asset` after builds. A signature is added only on recurrence (`ShaderGraphSettings.asset` is now legitimately tracked, so it gets no signature). Disposition: pitfall entry.
- OPS-4 (major, operational) The full Play Mode run took 25 to 30 min in QA, against the 900 s default (`scripts/test-unity.ps1:51`). An operator run without `-TimeoutSeconds 3000` times out and is misreported. Disposition: pitfall entry now; T412 records the Play Mode suite duration for B1 vs URP to separate host load from URP cost. No default change without a measured baseline, because the timeout is also a hang guard.
- OPS-5 (note) APK +10.5 MB levers, no verdict. Native code is the largest part: role 82.3 MB in 3 files in B1 vs 108.2 MB in 4 files in T411 (about +26 MB uncompressed, probably Burst or URP native). Runtime dependencies are burst and collections. Other levers: IL2CPP stripping level; URP SMAA `AreaTex.tga` (269 KB) and 3 compute shaders (67 KB). Compare only clean builds or `unzip -v` entry sums, because incremental builds carry about 14 MB of dead space. Disposition: T412, with burst-disable and strip-level experiments if the budget fails.
- OPS-6 (minor) Rollback for operators is thin. `render-foundation.md` says "Revert the single phase-0 commit", but the change is two commits merged as PR #583 (merge `b9221ca`). Disposition: section 5 of this record gives `git revert -m 1 b9221ca` on a branch through the normal review path; both commits are covered, and the comparator change reverts with them, restoring the strict rule under which the Built-in baselines pass.
- OPS-7 (minor) The rollback dry-run was asserted but not archived, and R01 also asks for the sprite packer mode (`EditorSettings m_SpritePackerMode`) revert assertion. Disposition: section 5 cites the dry-run and its result; T412 repeats it if in scope.
- OPS-8 (minor) C: ran out of space during the first Android build (`Library/Bee` about 4.8 GB per worktree). The failure was silent and expensive. Disposition: pitfall note (at least 20 GB free on C: before an Android build); optional free-space preflight later.

## 5. Recorded deviations and decisions

- Owner decision 2026-10-27 (Issue #523): the parity comparator follows R13 literally (`DifferingThreshold = 2`). Consequence: the 0.1 % budget never decides (A6, F1).
- Two parity unit-test assertion changes in `RenderParityTests.cs` (`:114`, `:132`: Differing 1 to 0; budget test delta raised). They go against the contract line "no test expectation changes in phase 0" (F2). Covered by the owner decision above; the contract line is not edited here.
- Parent-approved path extensions: `ShaderGraphSettings.asset` (unavoidable tooling output, defaults) and `RenderCaptureHarness.cs` (comparator only). Recorded on #523 (A5).
- Two-commit split: `a3ca464` is the render switch; `1a5e533` is the comparator and test follow-up. PR #583 merged them as `b9221ca`. Both are reverted together by reverting the merge (OPS-6).

## 6. Rollback for operators

Procedure: on a branch, `git revert -m 1 b9221ca`, then the normal PR and review path. Do not revert on `main` directly.

Dry-run evidence (from the T411 worker's report to the parent, 2026-10-28; not archived as a log):

- Scratch local branch `scratch/t411-rollback-dryrun`: `git revert` of `a3ca464` only (the switch commit; `1a5e533` did not yet exist). The project was reopened and compiled in batch mode. `RenderParityTests` 18/18 passed against the Built-in baselines, with 0 differing pixels on every frame checked. `Assets/IdleFactory/Rendering/` and `RenderPipelineConfigurationTests` were gone. The branch was deleted with `git update-ref -d` and never pushed.
- Not asserted in the dry-run: the sprite packer mode check (`EditorSettings m_SpritePackerMode`) named in R01, and a revert of the final merge (both commits).

## 7. Follow-ups

All items are open.

### T412 (performance and build evidence)

| ID | Finding source | Owner task | Action |
|---|---|---|---|
| OPS-1 | DevOps | T412 | State the commit and clean-Library state of every build. Open. |
| OPS-2, A8 | DevOps, Architect | T412 | Diff `UniversalRenderPipelineGlobalSettings.asset` after its Android builds. Open. |
| OPS-4 | DevOps | T412 | Record Play Mode suite duration for B1 vs URP and cold-run time (separate host load from URP cost). Open. |
| A7, F3 | Architect, QA | T412 | Confirm the 25 to 30 min duration is not caused by synchronous compilation; watch cold-run time. Open. |
| OPS-5 | DevOps | T412 | APK growth levers: native code, Burst, strip level, SMAA and compute shaders; experiments only if the budget fails. Open. |
| OPS-7, R01 | DevOps | T412, if in scope | Repeat the rollback dry-run, including the packer-mode assertion and a merge revert. Open. |

### Pitfall and wiki

| ID | Finding source | Owner task | Action |
|---|---|---|---|
| OPS-4 | DevOps | Pitfall entry | Full Play Mode run needs `-TimeoutSeconds 3000`. Open. |
| OPS-8 | DevOps | Pitfall entry | At least 20 GB free on C: before an Android build. Open. |
| OPS-3, A8 | DevOps, Architect | Pitfall entry | Diff the URP global settings asset and `ShaderGraphSettings.asset` after builds. Open. |
| A9 | Architect | S4 task and wiki | 2D lights need `m_Strip2DUnusedVariants` set to 0 and the variant budget (490 vs 693) re-measured. Open. |

### Later test-hardening task (not yet created; owner decides)

| ID | Finding source | Owner task | Action |
|---|---|---|---|
| F1 | QA | Test hardening | Dead 0.1 % budget: rename the two tests, add the uniform 1/255-shift pass test, or drop `MaxDifferingFraction`. Open. |
| A8 | Architect | Test hardening | Edit Mode guard on the global-settings `m_List` (non-empty, managed stripping values present). Open. |
| F4 | QA | Test hardening | Package pin for 17.6.0; Renderer2D post-processing assertion. Open. |
| F5 | QA | Test hardening | Light2D positive control. Open. |
| F3 | QA | Test hardening | Warm-up frame for the cold-run guard. Open. |

### Spec erratum

| ID | Finding source | Owner task | Action |
|---|---|---|---|
| A6 | Architect | Spec erratum | R13 and the render-foundation Gates row: the budget is vestigial under the literal rule. Open. |
