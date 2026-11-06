---
title: F004 batch 6 workflow pitfalls
type: howto
sources: [S049, S038, S044]
updated: 2026-11-06
---

# F004 batch 6 workflow pitfalls

Batch 6 merged #531 (T419), #532 (T420) and #601 (T459). (S049)

- **QA without Unity gives only a conditional PASS.** On #601 the parent's full Play Mode suite caught what no targeted run covered: `ContractsAutoDeliverPanelTests.ABonusWindowCountsDownAndEndsWithoutAJumpInTheLayout(phone-landscape 640x360)` failed at 200 % (offer card 476 to 475 pt). The compact-landscape content padding had cut the horizontal inset from 8 to 6 px, which widened the card and changed the text wrap. Fix `2ebfe10` restored 8 px and kept only the vertical gain. Rule: a layout change scoped to one class keeps horizontal insets, or every Play Mode file that opens that class at that text scale runs; vertical-only changes are safer. See [F004 US8 polish re-review](./f004-us8-polish-review.md). (S049, S044)
- **Reuse a warm Unity Library.** The parent moved the detached issue-528 worktree (`git worktree move`) to the new Issue path and created the branch there, which saved a full import. (S049)
- **Blocked worker writes without Unity.** A worker whose lease is held by another Issue writes the fail-first tests and the implementation without Unity, pushes, and returns blocked; the parent resumes the same worker once it holds the lease (continues the [batch 5](./f004-batch-5-workflow-pitfalls.md) rule). (S049)
- **An unmeetable strict layout assertion is an owner decision.** The worker reported the measured numbers and stopped; the owner relaxed the criterion (2026-10-29); the test encodes the relaxed rule and names the decision date in its failure message. (S049)
- **Doc-only records need delta QA.** An AD review or owner decision committed by the parent onto an in-review branch gets a new worker-done marker and a Low-tier delta QA for the new sha before `maqa-merge`. A QA FAIL on a decision record (a missing number) was fixed and re-QA'd. (S049)
- **Task text can contradict dependencies.** Art Director condition 1 ("T427 before the freeze") cannot be literal because T427 depends on T426; recorded as a note on T420. (S049)

Related: [F004 S1 decision and S2 spike](./f004-s2-spike-and-s1-decision.md), [run panel conventions](./run-panel-conventions.md).
