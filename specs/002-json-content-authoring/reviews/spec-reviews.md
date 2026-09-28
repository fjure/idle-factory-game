# Specification Reviews — JSON Content Authoring Migration

**Date:** 2026-09-28 · **Artifact:** `specs/002-json-content-authoring/spec.md` (draft before Clarify) · **Mode:** independent, read-only, run in parallel (max_parallel 3)

| Lens | Tier | Verdict | Findings |
|---|---|---|---|
| Game Designer/Product Owner + Game Systems/Economy | Medium (sonnet) | PASS-WITH-FINDINGS | P2: no requirement anchors the T049 multi-role review. P3: nothing owns SC-008 measurement. P3: retirement has no human approval step (Constitution X). P3: content limits deferred to the plan. Every T042–T050 obligation maps to an FR/SC; D001 values and offline revision 1 match exactly. |
| Software Architect + Security/Privacy | Medium (sonnet) | PASS-WITH-FINDINGS | P1: the 128-unit ID limit is a Save V1 parser guard, not a domain invariant. P2: `StrictJsonLexicalValidator` is private to `SaveV1Mapper` (plan must decide: extract or duplicate). P2: the owner of the presentation mapping after retirement is unnamed (plan decision). P3: the domain rejects numerator ≥ denominator. FR-011 values verified against the assets. Bootstrap no-partial-publication is feasible through the existing `DisableWithError` path. |
| Test/QA | Low (haiku) | PASS-WITH-FINDINGS | SC-005 needs a defined manual walkthrough. FR-013, SC-002 and SC-004 need player-build evidence; Android is emulator-only. FR-002 needs objective completeness checks. SC-008 is environment-dependent. |

## Disposition (parent)

Applied to the spec:

- **FR-004**: the ID length limit is now described as a new content-validator guard.
- **Edge Cases**: the offline-policy rule now reads numerator ≥ denominator.
- **FR-002**: adds two automated documentation completeness checks.
- **FR-013**: sets the evidence level for each platform.
- **FR-021**: new; requires the independent review verdicts before final acceptance.
- **SC-004**: requires proof in a Windows player.
- **SC-005**: becomes a recorded walkthrough.

Routed to Clarify as human decisions: the approval gate before SO retirement, whether SC-008 is binding, and the behavior for an unmatched presentation key.

Routed to the plan: extracting or duplicating the strict JSON lexical validator, the host assembly and namespace, the owner of the presentation mapping, content limit values, how packaged content is accessed on each platform, and the SC-008 measurement method.

Omitted lenses: UI/UX (no player-visible change beyond the existing error state), Art Director and Technical Art (no asset changes at the spec stage; they are re-evaluated if the plan moves the presentation mapping), Performance (only SC-008, handled in the plan), DevOps (no build or runtime service change), Online Services (no online boundary).
