---
title: F006 batch 5 workflow pitfalls
type: howto
sources: [S061]
updated: 2026-11-19
---

# F006 batch 5 workflow pitfalls

- **An unwired SerializeField made the T539 build card inert in the shipped scene.** The worker had been told not to touch scenes, so the tree and styles references stayed null and unit tests passed. Every worker must wire the scene through Unity tooling (an `F006SceneWiring` step) and add a real-scene test. (S061)
- **`AttentionMarkerView` was never instantiated from T532 to T557.** Markers never showed and T528's tap priority was dead; the Architect found it only by reading the bootstrap. A task that adds a view needs a test that finds it in the booted scene. (S061)
- **Bumping `Contracts.Version` per produced unit drove per-frame rebuilds (T529).** Check any revision-keyed rebuild against the busiest producer; the fix rebuilds at once only on identity, layout, offline and replace, otherwise at most once per second. (S061)
- **`Library/Bee` carries absolute paths.** After a `Library/` moved between worktrees, stale paths broke the build; delete the Bee DAG files (the T559 worker did). (S061)
- **A gate APK built with stale Gradle intermediates hangs** (a 64 MB APK). Delete `Library/Bee/Android` before building a gate APK. (S061)
- **A banner placed in the unobstructed rect reframes the camera** and covered the top machine row; draw transient overlays absolutely. (S061)
- **Review findings can be refuted on device.** C-F3 and C-F4 were Play-Tester findings that UI/UX showed to be wrong (slot badges versus markers). Re-read a report against the code before acting. (S061)
- **Harness "Irreversible Local Destruction" warnings** came from run-noise cleanup of `Assets/UI Toolkit`. Workers list what they deleted. (S061)
- **Gate agents use no host input.** The dedicated AVD, `MSYS_NO_PATHCONV=1` and the other emulator rules are in [workstation pitfalls](../../docs/agents/workstation-pitfalls.md) and [emulator validation](./android-emulator-validation.md). (S061)

Related: [batch 4 pitfalls](./f006-batch-p3a-workflow-pitfalls.md), [gates](./f006-batch-5-gate-and-owner-decisions.md).
