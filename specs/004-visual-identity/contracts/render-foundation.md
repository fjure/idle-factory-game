# Contract: Rendering Foundation (Phase 0)

**Scope**: spec FR-004–FR-006, US7, SC-008; research R01, R12, R13.

## Preconditions

1. Every Feature 003 Issue that writes `Assets/IdleFactory/Scenes/CoreGameFoundation.unity`, the camera, `ProjectSettings/GraphicsSettings.asset` or `ProjectSettings/QualitySettings.asset` is merged.
2. B1 exists for W003 on the current host and protocol (Feature 003 evidence or a fresh run).
3. Baseline screenshots exist at the resolutions in research R13.
4. Leases `unity,emulator` held by the phase-0 owner.

## Change set (one self-contained commit)

| Path | Change |
|---|---|
| `Packages/manifest.json`, `Packages/packages-lock.json` | Add `com.unity.render-pipelines.universal` `17.6.0` (built-in to 6000.6.0f1) |
| `Assets/IdleFactory/Rendering/IdleFactoryURP.asset`, `IdleFactory2DRenderer.asset` | New pipeline and 2D Renderer data: default sprite material Unlit, HDR off, post-processing off, no 2D lights |
| `ProjectSettings/GraphicsSettings.asset` | Default render pipeline = the new asset |
| `ProjectSettings/QualitySettings.asset` | Every quality level's render pipeline = the new asset |
| `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` | URP additional camera data on the factory camera (Unity tooling only; serialized diff limited to the camera) |
| `Assets/IdleFactory/Tests/EditMode/RenderPipelineConfigurationTests.cs` | Asserts the assignment in Graphics settings and every quality level, Gamma color space, Unlit default sprite material |

Color space stays Gamma. `ProjectSettings.asset` must not change except for re-serialization noise that `scripts/check-settings-noise.ps1` rejects anyway.

## Gates (all must pass; any failure blocks phase 0)

| Gate | Evidence |
|---|---|
| Edit Mode and Play Mode suites | `scripts/test-unity.ps1` full run: same pass set as before phase 0 |
| Windows test player | `scripts/test-unity.ps1 -StandaloneWindowsPlayer` |
| Screenshot parity | ≤ 0.1 % differing pixels, no channel difference > 2/255, per resolution (R13) |
| Android emulator journey | `scripts/android-emulator-journey.ps1` with the Feature 003 steps; GLES3 build path unchanged |
| Performance B2 | Same-session comparison with B1 within the provisional budgets (R12) |
| Settings noise | `scripts/check-settings-noise.ps1 -Commit <sha>` |

## Rollback

Revert the single phase-0 commit; no Core, content or save change is involved. A rollback after merge is a new revert commit through the normal review path.
