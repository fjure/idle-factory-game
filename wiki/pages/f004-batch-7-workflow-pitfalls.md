---
title: F004 batch 7 workflow pitfalls
type: howto
sources: [S050, S049]
updated: 2026-10-29
---

# F004 batch 7 workflow pitfalls

Batch 7 merged #533 (T421), #534 (T422), #608 (T460) and #535 (T423). Play-Tester omitted (no production, economy or core-loop change). Windows player suite 39/39. (S050)

- **Blind naming isolation.** Copy only the sheet PNGs to a scratch folder and forbid repository access, so the naming session cannot open keys. The parent scores against the key. (S050)
- **Sealed trial keys.** QA must not open an owner trial's `key.json`, so the owner stays blind; key consistency is checked through pytest only. (S050)
- **A failed owner gate becomes a small task.** SC-002 failing in round 1 produced T460 with its own Issue via maqa-sync. T423 stayed open and recorded the rounds. Its record travels on its own `maqa/issue-<n>` branch, PR and Low-tier delta QA (as T420 did) so `maqa-merge` can gate it. (S050)
- **Practice screen first.** Round 1's slowest b trial was the first (2.56 s); round 2 added an unscored practice screen. (S050)
- **Warm Unity worktree reuse.** `git worktree move` carried the same worktree across 531, 533, 534 and 608, avoiding full imports ([batch 6](./f004-batch-6-workflow-pitfalls.md)). (S049, S050)
- **Rate-limit resume.** A subagent killed by the usage limit finished after SendMessage. (S050)
- **Stale pytest launcher.** In a moved worktree the venv `pytest.exe` fails with "Failed to canonicalize script path". Fix: `uv sync --frozen --offline --reinstall-package pytest --project tools/art-pipeline`. (S050)
- **Determinism checks exclude timing.** Re-rendering writes measured fields (`generationSeconds`, `pipelineWallSeconds`); compare without them. (S050)

Related: [S3/S4 decision](./f004-s3-s4-cue-and-lighting-decision.md), [visual identity](./visual-identity-art-direction.md).
