---
title: F006 batch 6 part B and closeout workflow pitfalls
type: howto
sources: [S063]
updated: 2026-11-26
---

# F006 batch 6 part B and closeout pitfalls

- **Sonnet QA agents return early on long Unity runs.** They report before the suite ends. Have the agent hold a foreground wait loop until the test summary line appears, and check the log for it. (S063)
- **`scripts/maqa-merge.ps1` leaves the Issue open and `maqa:in-review` after merging.** The parent still runs the coordinator's `merged #<n>` step and closes the Issue; the closeout sync then mirrors the checkboxes. (S063)
- **The "Development Build" watermark overlaps controls on small phones in development APKs** (Confirm at 640x360 at 200 %, Overdrive at 360x640). It is a development-only artifact and the owner accepted it; do not file it as a product defect or score a tile against it (C-91, C-108). (S063)
- **The capture rig has no deck art.** The T502 `BaselineRig` draws no deck, stage or world dressing, so before and after rig sheets compare like with like; the real scene comes from the Windows player or the device. The rig test file is not committed; its source is kept as a `.txt` under `tools/art-pipeline/trials/f006/`. (S063)
- **A tester can misread a state as a defect.** C-77 and C-78 (marker shows an arrow, marker tap selects nothing) were the Missing-input badge versus the attention marker. Check a device finding against the build before assigning a fix. (S063)
- **Keep answer keys outside the repository until scoring is finished**, then release them with the records. (S063)
- **Capture APKs with test assemblies are not shipping builds.** State that in the record. `BuildApk` does not restore the scripting backend, and the capture builder is not `#if`-gated (C-101, C-109). (S063)

Related: [batch 6 pitfalls](./f006-batch-6-workflow-pitfalls.md), [final gates](./f006-final-gates-and-acceptance.md), [workstation pitfalls](../../docs/agents/workstation-pitfalls.md).
