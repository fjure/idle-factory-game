---
title: F004 batch 11 workflow pitfalls
type: howto
sources: [S054]
updated: 2026-11-05
---

# F004 batch 11 workflow pitfalls

- **Blender MCP bridge is off by default.** Start the Blender GUI (Auto Start add-on) under the `gpu` lease, verify with `get_blendfile_summary_path_info`, close it afterwards. (S054)
- **Rate limits.** Two usage-limit stops killed workers mid-task; renewing the leases and sending a message to the same worker resumed it without loss. (S054)
- **Overclaiming worker reports.** The T441 worker had not re-run the full Play Mode suite after its last fixes, the T455 record lacked matrix rows, and `reviewedBy` was written as a string instead of the dict schema. QA and the parent caught all three; verify before accepting. (S054)
- **Stale pytest launcher.** In a worktree venv it ends in exit 3; fix with `uv sync --frozen --offline --reinstall-package pytest --project tools/art-pipeline`. (S054)
- **Warm-Library worktree reuse.** `git worktree move` plus `switch -c` across Unity Issues saved import time; after an Android build there, lease `unity,emulator`. (S054)
- **Independent review before `reviewedBy`.** Split the worker deliverable into an import commit and a `reviewedBy` commit; re-QA only the data and doc delta. (S054)
- **`-TestFile`** takes the repo-relative path form. (S054)
- **Ledger row scope.** A wrong scope (T441 said parent-only) is fixed on the batch branch. (S054)
- **Test the stress case, not only 100 %.** A UI/UX review on captures found an F1 gap the automated test asserted only at 100 % text scale; the visibility test now needs to cover 200 %. (S054)

Related: [batch 11 components](./f004-batch-11-floor-settings-icons.md), [batch 10 pitfalls](./f004-batch-10-workflow-pitfalls.md).
