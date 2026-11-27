# Component Diagram: Visual Direction Reboot (Feature 007)

**Feature**: Replace the D24 pixel-art presentation with a stylized industrial look, chosen by evidence (three prototypes, M1 to M6 gates). No gameplay, economy, content or save change (FR-001).
**Generated**: 2026-11-27
**Scope**: Full feature, M1-M3 tasks (M4-M6 are regenerated after the owner decision)

---

## Overview

Four focused diagrams at component level: the runtime presentation seam, the `render-hd` art pipeline, the milestone gates, and the build-out stage by finish tier model. For layers and dependency direction see `software-architecture.md`. There is no `system-design.md`: F007 adds no infrastructure.

## 1. Runtime presentation seam

```mermaid
flowchart LR
    RS[RunSession snapshot] --> PM[Presentation models<br/>machine, attention, stage, depot]
    PM --> WV[World views<br/>machine view v2, indicator view,<br/>environment, depot]
    BOOT[Bootstrap] --> RES[LookProfileResolver]
    DT[DevTools<br/>IDLEFACTORY_DEV only] -. ILookProfileSource<br/>at BeforeSceneLoad .-> RES
    RES --> WV
    RES --> CAM[Camera<br/>single writer]
    RES --> FX[EffectPool]
    LP[(LookProfile assets<br/>+ requirement tables)] --> RES
    LPS[(LayoutPitchSettings<br/>world units)] --> FWL[FactoryWorldLayout<br/>+ LatticeLayoutBuilder]
    FWL --> WV
    HOLD[Dev-only holder scene] -. references prototype profiles .-> DT
    URP2[Dev-only second URP asset<br/>Universal Renderer, C1] -. set before first frame .-> DT
```

The tap path (`WorldTapResolver`, on location hit shapes) feeds input into the existing application operations; it is unchanged and not drawn.

## 2. render-hd pipeline flow

```mermaid
flowchart LR
    RIG[rig-v3.blend<br/>from rig-v2, same camera] --> BR[Headless Blender render<br/>render.py + param file]
    BR --> PASS[Passes: beauty, shadow,<br/>emissiveMask, optional normal]
    PASS --> POST[Post: union-bbox trim,<br/>one pivot, frame strip]
    POST --> CHK{Checks<br/>flash, IoU, anchors, contrast,<br/>memory, key colour, repro, prompt lint}
    CHK -- fail --> BLOCK[Manifest blocked]
    CHK -- pass --> MAN[Manifest row + provenance.json]
    MAN --> IMP[F007SpriteImportPostprocessor<br/>PPU, filtering, mips, ASTC/BC7]
    IMP --> ATL[Sprite Atlas v2 per family<br/>page rule 1 MiB until R13]
    CMF[ComfyUI: concepts and<br/>tileable textures only] -. inputs, never shipped .-> RIG
```

## 3. Milestone gates

```mermaid
flowchart LR
    M1[M1 Audit<br/>incl. Pixel baseline] --> M2[M2 Prototypes A, B, C<br/>+ comparison]
    M2 --> M3{M3 Owner decision}
    M3 --> M4[M4 Art Bible v2,<br/>pipeline v2, budget v2]
    M3 -. revise, T640 .-> M2
    M4 --> M5[M5 Families go live<br/>incrementally]
    M5 --> M6[M6 Final acceptance]
    C1[Checkpoint 1: briefs + WB v0.3] --> C2[Checkpoint 2: one machine per direction]
    C2 --> C3[Checkpoint 3: C1 spike result]
    C3 --> HP[Hands-on Pixel play, O-1]
    HP --> M3
```

Checkpoints 1 to 3 sit inside M2 (O-2); there is no elimination of a direction without the owner's explicit word.

## 4. Build-out stage x finish tier

```mermaid
flowchart LR
    LV[Machine level] --> ST[stage = MachineBuildOutStages<br/>thresholds 10/25, opt. 50<br/>drops back with prestige]
    PC[Persisted PrestigeCount] --> TI[tier = FacilityFinishTiers<br/>survives resets, never written]
    ST --> ART[Machine art variant<br/>stage x tier, orthogonal]
    TI --> ART
```

## Component Breakdown

### LookProfileResolver (bootstrap-owned)
**Role**: Single owner of the active profile; hands art, pitch and camera policy to views, camera and effect pool.
**Why this exists as a separate component**: One seam instead of per-direction branches or scene copies. DevTools only supplies an `ILookProfileSource`, so release code has no prototype references.
**Key interactions**:
- <- DevTools source at `BeforeSceneLoad`
- -> world views, camera, `EffectPool`. An unknown id falls back to the shipped default with a log entry.

### Per-profile requirement tables
**Role**: Required keys, sizes and pivots per profile; the existing static registry is the F006 table.
**Why separate**: Fail-closed validation at start-up, no silent cross-profile fallback.

### LayoutPitchSettings
**Role**: Lattice pitch in world units (F006: 70x54 art px at D24 = 2.917 x 2.25), part of `LayoutComposition` equality and `IdentitySuffix`.
**Why separate**: On-screen density can change without changing location identity (12 content-owned locations).

### Machine view v2 and indicator view
**Role**: Body, shadow, emissive mask and overlay parts per `MachineArtEntry`; the indicator sits at `indicatorAnchor`. C uses a mesh view with the same API.

### Dev-only holder scene and second URP asset
**Role**: Isolate prototype assets and the Universal Renderer (C1). Added only for `IDLEFACTORY_DEV` builds; the shipped URP asset keeps one 2D renderer.

### Pipeline (`render-hd`, `mesh-export`, checks)
**Role**: Reproducible offline art production with blocking checks and provenance. Interactive Blender MCP is allowed; shipped pixels come only from the headless batch.

## Design Reasoning

### Why this structure?
The data flow is unchanged from F006: snapshot, presentation models, world views, with no path back to simulation. The profile selects only appearance, pitch and camera policy, which makes the FR-001 parity test meaningful. The resolver is bootstrap-owned (ARCH-6) so DevTools cannot become a runtime dependency of release code.

### Alternatives considered

| Structure | Why it wasn't chosen |
|---|---|
| Per-direction branches or scene copies | Multiplies the parity surface; violates simplicity |
| DevTools owns the resolver, or `Resources/` paths | Leaks prototype assets into release (ARCH-1, ARCH-6) |
| C2 RenderTexture or C3 fake-lit in the 2D Renderer | Fill-rate trade-off, or a dishonest hybrid test (research R06) |

### When you'd restructure
If M3 picks a `Continuous` camera policy or promotes the Universal Renderer, the single-URP-asset isolation and the parity baselines must be re-done at M4.
