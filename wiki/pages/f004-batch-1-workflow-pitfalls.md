---
title: F004 batch 1 workflow pitfalls
type: howto
sources: [S040]
updated: 2026-10-27
---

# F004 batch 1 workflow pitfalls

- Usage limits interrupted three workers mid-task. Resuming the same agent worked after renewing its lease and checking its worktree. (S040)
- `powershell -ExecutionPolicy Bypass` is refused by the auto-mode classifier for subagents; use `powershell -NoProfile -File <script>`. (S040)
- A QA FAIL on #515 came from Blender embedding absolute paths and dates in PNG tEXt chunks; see [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md). (S040)
- Tool versions drift from the plan (ComfyUI v0.43.0 live versus v0.38.0 in the contract); record the live version in the evidence instead of assuming the contract. (S040)

Related: [F005 batch D workflow pitfalls](./f005-batch-d-workflow-pitfalls.md).
