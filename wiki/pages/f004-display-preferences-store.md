---
title: F004 display preferences store (T432)
type: component
sources: [S045, S047]
updated: 2026-10-29
---

# F004 display preferences store (T432)

T432 (#545, PR #589) adds `DisplayPreferencesStore`, the reduced-motion override kept out of the save. It owns `<root>/IdleFactory/Preferences/display-v1.json` with `reducedMotion` in {followSystem, on, off}. (S045)

- Strict token-walk reader, 4 KB cap, version 1 only; atomic write leaves the prior file unchanged on failure. (S045)
- Missing or invalid file falls back to `followSystem` and logs `preferences.display.invalid` once per session. A missing file is not silent: the parent corrected the worker's first reading against contract line 30. (S045)
- `IReducedMotionSignal` has Android (JNI `animator_duration_scale`), Windows (`SPI_GETCLIENTAREAANIMATION`) and Default variants; they never throw and log error codes only. JNI types are preserved in `Assets/link.xml`. (S045)
- `CoreGameFoundationBootstrap.NotifyFocusChanged` re-reads the file only when focus returns. (S045)
- Security/Architect review: APPROVE. Notes for T433: own the signal and the effective-state accessor in the bootstrap and cache the effective value, because `Resolve(FollowSystem)` is a JNI round trip with allocations (FR-026). (S045)
- Not yet exercised on a device (JNI path, link.xml). (S045)

## Flake note (batch 5)

`SaveReplacesAnExistingFileAndRewritesAnInvalidOne` failed once with a transient `IOException` around `File.Replace` and passed 3/3 reruns; a hint was posted on #546. Rerun before suspecting the store. See [F004 batch 5 workflow pitfalls](./f004-batch-5-workflow-pitfalls.md). (S047)

Related: [visual identity and art direction](./visual-identity-art-direction.md), [desktop focus lifecycle](./desktop-focus-lifecycle.md).
