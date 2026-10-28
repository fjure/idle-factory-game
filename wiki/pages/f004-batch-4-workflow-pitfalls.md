---
title: F004 batch 4 workflow pitfalls
type: howto
sources: [S045]
updated: 2026-10-28
---

# F004 batch 4 workflow pitfalls

- **Known first-run flake.** `RenderParityTests` desktop 640x480 (w003s world, about 18.9 % of pixels differing) failed once in the first full Play Mode run after a fresh `Library` import or an Android-to-Windows target switch (T414 and T416 workers). It passed in isolation and on every rerun. Rerun Play Mode once; it is a candidate for the open test-hardening decision. See [F004 URP phase 0 results](./f004-urp-phase0-results.md). (S045)
- **Untracked `Assets/UI Toolkit/`.** Unity runs in fresh worktrees regenerate `Assets/UI Toolkit/` (and its `.meta`). It blocks `git worktree remove` without `--force` and must never be committed. (S045)
- **Long Unity runs and QA agents.** QA subagents waiting on a 25-30 min full run were repeatedly forced to hand back before Play Mode finished, and stopping the agent did not kill its Unity child. The parent finished the check by waiting on the Unity PID and reading the results XML. Start such runs as the parent, or tell QA to return early with the run's PID and results path, and keep the QA verdict on the evidence. (S045)
- **Review text fidelity.** Second-session review text is recorded from a file the parent writes, and QA diffed it byte-for-byte (rule from [batch 3](./f004-batch-3-workflow-pitfalls.md)). (S045)
- **Deferred B2.** T412 (B2 performance, #524) was deferred again by the owner to 2026-10-29 (quiet host); #524 carries the `resource:gpu` label. (S045)

Related: [F004 emulator scenario](./f004-emulator-scenario.md), [F004 batch 3 workflow pitfalls](./f004-batch-3-workflow-pitfalls.md).
