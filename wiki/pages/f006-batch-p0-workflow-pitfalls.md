---
title: F006 batch P0 workflow pitfalls
type: howto
sources: [S057]
updated: 2026-11-11
---

# F006 batch P0 workflow pitfalls

- **Gate cleanup on the merge exit code.** Issue #680 (PR #723) conflicted with #722 in `tools/art-pipeline/art_pipeline/__main__.py`. `maqa-merge.ps1` refused, but a chained cleanup still closed the Issue unmerged and deleted the branch, which closed the PR. Recovery: push the commit back, reopen the Issue and PR, delete the false merged marker, merge origin/main, re-run QA. Run the merge alone, check its exit code and `git merge-base --is-ancestor`, then clean up. Parallel Issues that edit the same file conflict after the first merge, so dry-run the second. (S057; recorded in docs/agents/workstation-pitfalls.md, commit 7846950)
- **Scope may widen on a rejected deliverable.** The first mockups failed the parent's review, and the parent widened #682 to additive compositor roles instead of opening a new Issue. QA then still caught the occlusion bug (see [tooling](./f006-p0-art-tooling.md)). (S057)
- **The emulator /data partition fills up.** Reinstall the app fresh or clear old APKs before a device capture (baseline T502). (S057)
- **Unity runs re-serialize the iOS icon noise in ProjectSettings every time.** Restore it with `git checkout` and run the settings-noise check. (S057)
- **Removing a Unity worktree needs the `\\?\` delete**, and Android builds leave `.utmp/` in the worktree root. Check for it first; see the [F004 batch 12 pitfalls](./f004-batch-12-workflow-pitfalls.md). (S057)
- **Clear the panel before UI screenshots.** Baseline PNGs use a cleared PanelSettings; otherwise earlier frames show through. (S057)
