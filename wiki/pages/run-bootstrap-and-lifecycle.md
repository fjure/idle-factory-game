---
title: Run bootstrap and lifecycle
type: component
sources: [S018, S019]
updated: 2026-10-07
---

# Run bootstrap and lifecycle

T110 switches `CoreGameFoundationBootstrap` from the Feature 001 session to a Save V2 `RunSession`. It composes [run content dispatch](./run-content-dispatch.md), [run persistence](./run-persistence.md) and [run cold start and V1 import](./run-cold-start-and-v1-import.md). (S018)

## Start-up order

1. Non-content reference validation, then HUD readiness and layout.
2. v2 content load through `GameplayContentDispatcher`, with a resolver backed by the sprite catalog, so an unknown key fails as `presentation.key.unresolved`.
3. The legacy catalog only when a V1 import is needed: `LazyLegacyCheckpointSource` reads it only if a `v1/` directory exists.
4. The Save V2 store, the cold-start source selection (V2, V1 import, fresh) and finally `RunSession`. (S018, S019)

Every failure goes to `DisableWithError`/`DisableWithCode` with a coded diagnostic before any checkpoint I/O. Build and Confirm stay disabled. Diagnostics carry the exception type name only, never `exception.Message` or paths. Content failures create no store and leave files byte-identical. (S018, S019)

The scene holds two content references: `gameplayContent` (the Iron v2 sheet) and `legacyImportContent` (the V1 catalog, only for import). (S019)

## Runtime host and lifecycle

- `FactoryRuntimeHost` advances only while a machine is running or a timer is pending (effects, Overdrive, contract deadline); idle intervals are dropped. Development builds log one static line `ResumeAndReconcile ms=<n>` that T123 parses (PE-2). (S018, S019)
- `RunLifecycleAdapter` is the Save V2 counterpart of the Feature 001 lifecycle adapter: suspension writes a checkpoint, a return reconciles exactly once, and an Android rotation without departure creates no offline summary (see [Android runtime rotation](./android-runtime-rotation.md)). (S018, S019)
- Transient UI actions register through `RegisterTransientAction`; a rotation cancels them without spending, placing or prestiging and shows "Cancelled by rotation" (UX-9). (S018, S019)
- An Editor-only Feature 001 bridge keeps the legacy scene suites green until T119 removes it; the player path is v2-only. (S019)

The shell built on this bootstrap is described in [run shell and panels](./run-shell-and-panels.md).
