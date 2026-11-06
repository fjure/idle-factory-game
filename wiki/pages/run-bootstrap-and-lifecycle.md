---
title: Run bootstrap and lifecycle
type: component
sources: [S018, S019]
updated: 2026-11-06
---

# Run bootstrap and lifecycle

T110 switches `CoreGameFoundationBootstrap` from the Feature 001 session to a Save V2 `RunSession`; T119 removes the Feature 001 composition entirely (see [Feature 001 retirement](./feature-001-retirement.md)). It composes [run content dispatch](./run-content-dispatch.md), [run persistence](./run-persistence.md) and [run cold start and V1 import](./run-cold-start-and-v1-import.md). (S018)

## Start-up order

1. Non-content reference validation, then HUD readiness and layout.
2. v2 content load through `GameplayContentDispatcher`, with a resolver backed by the sprite catalog, so an unknown key fails as `presentation.key.unresolved`.
3. The legacy catalog only when a V1 import is needed: `LazyLegacyCheckpointSource` reads it only if a `v1/` directory exists.
4. The Save V2 store, the cold-start source selection (V2, V1 import, fresh) and finally `RunSession`. (S018, S019)

Every failure goes to `DisableWithError`/`DisableWithCode` with a coded diagnostic before any checkpoint I/O. The start-up status panel shows the code. Diagnostics carry the exception type name only, never `exception.Message` or paths. Content failures create no store and leave files byte-identical. (S018, S019)

The scene holds two content references: `gameplayContent` (the Iron v2 sheet) and `legacyImportContent` (the V1 catalog, only for import). (S019)

## Runtime host and lifecycle

- `FactoryRuntimeHost` advances only while a machine is running or a timer is pending (effects, Overdrive, contract deadline); idle intervals are dropped. Development builds log one static line `ResumeAndReconcile ms=<n>` that T123 parses (PE-2). (S018, S019)
- `RunLifecycleAdapter` replaces the retired Feature 001 lifecycle adapter and owns the return classification types since T119: suspension writes a checkpoint, a return reconciles exactly once, and an Android rotation without departure creates no offline summary (see [Android runtime rotation](./android-runtime-rotation.md)). (S018, S019)
- Since T250 the suspension and return rules above apply to mobile only; on desktop, focus loss is not a departure (see [desktop focus is not a departure](./desktop-focus-lifecycle.md)). (S018, S019)
- Transient UI actions register through `RegisterTransientAction`; a rotation cancels them without spending, placing or prestiging and shows "Cancelled by rotation" (UX-9). (S018, S019)
- Since T119 the bootstrap never composes `JsonCheckpointStore`, and `RunAuthorityTests` guards that it has exactly one gameplay content input (GD-1). (S018, S019)

## Shell composition (T192)

`ComposeRunShell` builds the shell and slot grid into locals and publishes them only when `Configure` succeeds. On failure it destroys the component and keeps the reason in `runShellError` for the start-up report, so a later retry composes again instead of skipping a half-ready shell. `RunShellDocument.Build` removes a partial UXML clone, and `Dispose` cancels a pending confirmation once through `OnCancel(Cancelled)` (see [run shell notices and gate fixes](./run-shell-notices-and-gate-fixes.md), F-7). (S019)

## Panel composition (T194)

`CoreGameFoundationBootstrap.ComposeRunPanels` runs right after the shell is published and builds every panel with the shell's icon resolver. `TryComposePanel` registers only a fully built panel; a failure records `panel.<id>.failed` with the exception type only, and the ID keeps the shell placeholder. Handles are disposed by `RecomposeRunPanels` and in `OnDestroy`. The startup surface mounts on the document root before the shell exists, and the offline summary mounts under the shell below the dialog layer only while it shows something. The scene holds 18 serialized panel UXML/USS fields, set through a Unity batch script. The shell keeps owning the Overdrive control, so `OverdriveControl` is not mounted. (S019) The panels themselves are listed in [run panels](./run-panels.md).

The shell built on this bootstrap is described in [run shell and panels](./run-shell-and-panels.md).
