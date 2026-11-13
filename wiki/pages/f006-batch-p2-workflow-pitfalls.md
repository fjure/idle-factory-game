---
title: F006 batch P2 workflow pitfalls
type: howto
sources: [S059]
updated: 2026-11-13
---

# F006 batch P2 workflow pitfalls

- **File.Replace fails about 0.6 % of the time on this host.** Replacing onto an existing file raised `IOException` 0x80070497 (ERROR_UNABLE_TO_REMOVE_REPLACED). The T538 store now retries transient IO and access failures at 15, 30 and 60 ms, and that retry blocks the main thread. (S059)
- **Test around host failures.** Inject scripted failures before the real call and assert outcomes, not attempt counts, on the real file system; test doubles must absorb genuine host failures so exact counts stay deterministic. This was the cause of both T538 QA fails. (S059)
- **Blender MCP bridge was unreachable in every render task.** All P2 art ran headless, and each provenance record says so (P2-F8). (S059)
- **Stop workers that wait on background work after reporting** before QA starts. (S059)
- **A usage limit interrupted a worker mid-task.** Resume the same worker with SendMessage after renewing the lease. (S059)
- **The owner's Unity editor was open on the main checkout** while worktree CLI runs continued; check the editor and leases first. (S059)
- **`git branch -d` fails from the main checkout when its HEAD is stale.** Use a guarded `update-ref` after an ancestor check. (S059)
- **`Library/` was handed across 5 sequential Unity Issues**, as described in [P1 pitfalls](./f006-batch-p1-workflow-pitfalls.md). (S059)

Related: [P2 art and presentation](./f006-batch-p2-art-and-presentation.md).
