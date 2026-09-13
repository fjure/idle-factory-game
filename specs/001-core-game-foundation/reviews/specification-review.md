# SPEC-001: Specification review evidence

**Date**: 2026-09-13

**Artifact**: [Core Game Foundation](../spec.md)

**Authority**: [original draft](../../../Specify-Draft.md), [constitution v1.0.0](../../../.specify/memory/constitution.md), [AGENTS.md](../../../AGENTS.md), [project workflow](../../../docs/spec-kit-greenfield-workflow.md).

## Scope and decision

The specify phase translates the supplied draft into four independently testable stories. The original draft is retained unchanged. The owner explicitly selected completion-time atomic resource exchange, preservation of interrupted progress, and no production credit for stopped time. This decision is recorded in the specification rather than left for implementation.

All numeric recipe, storage, world and footprint examples are authorized temporary development fixtures. The clarification phase introduced one approved initial economy rule: offline production uses 80% efficiency and an eight-hour credited-duration cap. No broader final economy, logistics, rendering architecture or production asset catalogue has been introduced.

## Role review record

Three bounded read-only role agents reviewed the source material in parallel; further lenses were assigned sequentially to the same agents. The parent owns reconciliation and artifact edits. No implementation agent or MAQA worker was dispatched.

| Agent / lens | Evidence and disposition |
|---|---|
| discovery_product — Game Designer / Product Owner | Reviewed value, four-story scope, all nine source scenarios and exclusions. Final spec review returned PASS with no blocking/high findings before the agent's subsequent usage-limit error. |
| discovery_economy — Game Systems / Economy | Checked finite storage, resource conservation, pause/resume, shared definitions and non-canonical tuning. Final review returned PASS; D001 accounting agrees with the owner decision. |
| discovery_economy — Test/QA | Checked complete-footprint placement, independent instances, failure paths, mid-cycle save/load and event-equivalent time partitioning. Final review identified one LOW coverage improvement: show more than one cycle within one time advance. Parent added US4.5 and its named fixture variant. |
| discovery_ux — UI/UX | Completed discovery review of touch/pointer navigation, footprint preview, selection, inspection and recovery feedback. Parent matched these findings to FR-004/005/014/027 and US1–3. Final independent re-review ended at the agent usage limit and did not produce a verdict. |
| discovery_product — Software Architect | Separate discovery review supported explicit state ownership and view-independent simulation. Classes, assemblies, tick strategy, persistence encoding and speculative future infrastructure remain planning concerns. Parent checked final alignment; no independent final architecture-artifact PASS is claimed. |
| discovery_ux — Art Direction / Technical Art | Discovery review accepted explicitly temporary readable placeholders; final assets, animation, shaders and atlas work remain excluded. Placeholder production/integration ownership and a proportionate brief belong in planning before visual implementation. |
| discovery_ux — Security / Privacy | Separate persistence review required whole-save validation, supported-version checks and preservation of current state/prior successful save on failure. These are reflected in FR-023–027 and US3; no cloud/auth/encryption scope was added. |
| discovery_economy — Performance | Discovery review separated long-time correctness from performance evidence. Planning must choose a measured workload/device/budget; no invented FPS or factory-scale promise. |
| discovery_product — DevOps / Operations | Separate feasibility review identified test-harness, command and Unity worktree prerequisites below. They do not block specification, but remain implementation gates. |

The parent reviewed the final edited spec and the [quality checklist](../checklists/requirements.md), including the added multi-cycle and alternate-footprint scenarios. Independent discovery was completed for all applicable domains; the initial UI/UX re-review limitation is recorded below and was closed during clarification. Specification-quality PASS is not gameplay QA or feature-completion evidence.

Omitted production roles: Gameplay/Backend, Frontend/UI implementation, Asset Artist, 3D/Render Artist and Technical Art integration have no assigned implementation or asset-production work in this phase. Sound Designer and Composer are omitted because audio is excluded. Online Services is omitted because no remote service is introduced. Relevant implementation and independent verification roles must be assigned again in later phases.

## Workflow and checks

- Read `.specify/extensions.yml` before specifying. Evaluated and executed the enabled optional `before_specify` branch-convention validation. No `after_specify` hook is registered.
- Queried the initialized wiki before specification: no domain pages or registered sources were available to recover. No wiki-backed product claim was invented; verified planning decisions should be ingested after planning.
- Used the resolved core `.specify/templates/spec-template.md`, preserving required section order and replacing template content.
- Created `feat/001-core-game-foundation` with matching `specs/001-core-game-foundation` directory, according to the configured GitFlow naming rule and sequential numbering.
- Set the ignored local `.specify/feature.json` pointer. Core `check-prerequisites.ps1 -Json -PathsOnly` resolves the correct feature/spec paths. Its `BRANCH` field reports the logical feature identifier; the actual Git branch includes `feat/`.
- Parent checked original-draft coverage, consistent approved cycle behavior, unique requirement identifiers, completed mandatory sections, absence of unresolved clarification/template placeholders, local links and whitespace.
- No application tests were run because this phase changes specification documents only. No `plan.md`, `tasks.md`, code or GitHub task issues were generated.

## Handoff to planning

The project's `$speckit-learn-clarify` review is complete. Next run `$speckit-plan` and its independent role review wave. Do not bypass tasks, analysis, orchestration or implementation gates because this specification checklist passes.

Planning must resolve:

- The development device, screen sizes, touch/pointer validation setup, numerical precision policy and measured performance workload/budgets.
- Plain domain/state ownership, Unity adapters, persistence validation/write strategy, lifecycle checkpoint integration, and the acceptance/test matrix, including multi-cycle advances, capped offline recalculation and invalid-save preservation.
- Named placeholder UI/visual production and review owners, a proportionate development brief, and documented temporary interactions. No final art pipeline production is authorized by this spec.
- A bounded test-harness bootstrap followed by verified Edit Mode/Play Mode commands before gameplay work or MAQA dispatch. Unity Test Framework 1.8.0 is resolved in `Packages/packages-lock.json`, but project test assemblies/wrapper and real `maqa-config.yml` test commands are still missing. An empty successful test run is insufficient evidence.
- Verified test prerequisites on the worker base before dependent MAQA work: the installed coordinator starts new workers from the remote default branch. Avoid assigning a worker that needs the missing runner in order to construct that runner.
- Worktree location and Unity isolation: configured `worktree_base: ".."` lies outside this session's writable root. Choose a permitted location or obtain the necessary environment authorization when that phase runs. Match each Editor/MCP/test invocation to its intended project/worktree, and serialize shared scene, prefab, package and project-setting writes.

The Unity package/toolchain is present, but Unity MCP tools were not exposed in this agent runtime. Specification completion does not establish Editor connectivity or implementation readiness.

## Clarification-phase UI/UX follow-up (2026-09-13)

The independent `discovery_ux` reviewer completed CLARIFY-001 against the committed specification and returned specification-quality PASS, with no high-impact UX question candidates. This closes the missing final independent UI/UX verdict recorded above; that earlier record describes the original specify run.

Reviewed evidence: touch/pointer journeys (US1, FR-004, SC-005), placement/selection/inspection feedback (FR-005/014), coherent save boundaries and failure preservation (US3, FR-023–027), and explicitly temporary visuals (A-004, SC-008).

Planning must define pending save/load interaction, atomic lifecycle-checkpoint timing, selection reconciliation after world replacement, and the actual screen/input setup. A failed operation must cause no state mutation; the word "unchanged" does not by itself require globally pausing simulation while persistence runs. Inspector actions must never target an obsolete instance after a successful restore. These are contract and interaction-design obligations, not new gameplay scope.

No files were changed by the reviewer and no Unity tests or implementation QA were performed.

## Clarification decisions (2026-09-13)

Five questions were asked and answered during `$speckit-learn-clarify`:

1. Returning after suspension, screen lock or closure recalculates eligible production at a disadvantage instead of pausing without credit.
2. The initial configurable efficiency is 80%; eight credited hours represent 6.4 effective production hours.
3. At most eight offline hours are credited per return; additional elapsed time creates no backlog.
4. Local UTC device time is the source. Negative/invalid differences grant zero; positive differences are capped. No online time or additional manipulation detection is introduced.
5. Manual save and reported suspension/background/regular-shutdown transitions attempt a coherent lifecycle checkpoint. The last successful checkpoint is authoritative; periodic autosave is excluded.

These decisions expand the original draft from offline-readiness to one bounded offline-production slice. They are encoded in US3, the offline edge cases, FR-022/032–037, the Offline Interval entity, SC-009/010, A-005, Clarifications and Scope boundaries. The owner decisions supersede the original exclusion of real offline calculation for this feature.

## Clarification final review

The required read-only role wave reviewed the expanded and corrected specification:

| Lens | Verdict | Evidence |
|---|---|---|
| Game Designer / Product Owner | PASS | The owner decisions are explicit, testable and bounded; no material product decision remains unresolved. |
| Software Architect | PASS | Deterministic reuse, state ownership, coherent validation and exactly-once boundaries are specified without prescribing persistence mechanics. |
| Game Systems / Economy | PASS | `effective time = min(valid offline time, 8 hours) × 80%`; the maximum is 6.4 hours (23,040 seconds). Atomic cycle accounting and block behavior remain consistent. |
| Test / QA | PASS after correction | Review found that legacy restore scenarios could conflict with automatic offline progress. US3.1/2 now use zero measured offline duration, and SC-004 compares nonzero returns against the saved snapshot advanced by approved effective time. US3.7 provides unsaturated checks at one and 6.25 offline seconds. |
| UI/UX | PASS after correction | The same restore contradiction was independently found and rechecked after correction. Return feedback covers measured time, effective time, resources and limiting conditions. |
| Security / Privacy | PASS | Local UTC, invalid/backward handling, capped forward time, whole-checkpoint validation, preservation on failure and exactly-once intent match the approved bounded threat posture. |

No blocking or high specification finding remains. This is specification-quality evidence, not implementation QA.

Planning retains responsibility for lifecycle-event ordering and deduplication, coherent persistence of the reconciled state plus consumed interval, interruption/retry behavior, numerical precision, return-summary pending/error interaction, selection reconciliation, screen/input setup, measured performance workloads and the Unity test harness. External replacement of an old local save is not claimed to be prevented by the approved local-clock policy.
