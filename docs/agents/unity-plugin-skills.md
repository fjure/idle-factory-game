# Unity Plugin Skills (Claude Code only)

This file is part of the agent working agreement in [`AGENTS.md`](../../AGENTS.md). Read it before a Claude Code agent uses any `unity:*` skill, and when naming applicable skills in an assignment.

The Unity plugin for Claude Code adds `unity:*` skills. They are optional domain knowledge for Claude Code agents, not workflow steps or gates. Codex is the default integration and does not have them, so no assignment may depend on them, and a worker without them must be able to deliver the same result. They never override this document, the constitution, the approved Art Bible, or the active plan.

| Skill | Role | Use |
|---|---|---|
| `unity:ui-uitk` (routed through `unity:ui`) | UI/UX Developer, UI/UX Reviewer | UXML, USS, runtime binding, PanelSettings. All player-facing UI is UI Toolkit. |
| `unity:sprite-editor` | Technical Artist | Pivots, borders, and slicing through editor scripts instead of hand-edited importer metadata |
| `unity:manage-sprite-atlas` | Technical Artist | Only after an approved workload or plan decision shows a need for an atlas; the Art Bible defaults to no atlas |
| `unity:2d-pixel-perfect` | Technical Artist, Performance Engineer | Diagnosing shimmer or blur with the 64 PPU, point-filtered sprites. A Pixel Perfect Camera or render setup change requires a plan decision; after the URP switch, use the URP variant of the camera. |
| `unity:migrate-birp-to-urp` | Software Architect (plan review), Technical Artist | F004 only: planning, executing, and checking the move from the Built-in Render Pipeline to URP, including materials, shaders, and the choice of renderer |
| `unity:urp-postprocessing` | Technical Artist, Art Director (review), Performance Engineer | F004 and later: Volume overrides only as the approved Art Bible and the mobile render budget allow |
| `unity:validate-urp-render-graph-renderer-feature` | Technical Artist, Performance Engineer | F004 and later: checking a custom Renderer Feature against the render graph, only when the plan approves a custom feature |
| `unity:shader-graph-create-custom-node` | Technical Artist | F004 and later: a custom Shader Graph node, only when an approved brief requires a custom shader |
| `unity:optimize-audio`, `unity:audio-setup-mixers` | Sound Designer, Technical Artist | Audio import settings and mixer routing once the audio role is active |
| `unity-mcp-skill` | Any role assigned Unity MCP | Tool patterns for the focused Unity MCP calls this document already prefers |

Guardrails:

- `unity:unity-cli` may be used for read-only inspection, such as editor logs or an installed-editor listing. Edit Mode, Play Mode, and Windows-player tests still run through `scripts/test-unity.ps1`, every test run and build still holds the resource leases above, and their results still pass `scripts/check-settings-noise.ps1`. Do not use it to install, upgrade, or switch editors; the pinned version rule applies.
- `unity:unity-package-management` may inspect packages. Adding, removing, or upgrading a package requires a plan decision and a committed `Packages/manifest.json` and `Packages/packages-lock.json`.
- Scene, prefab, and importer changes a skill proposes still go through Unity tooling, as the asset rules below require.
- The render pipeline changes only through F004. F004 replaces the Built-in Render Pipeline with URP. Until the F004 plan is approved and its tasks are analyzed, the URP skills may only inform specification and planning; they must not add the URP package, create pipeline assets, or convert materials. The plan records the renderer choice (2D Renderer or Universal Renderer), the conversion order, the Android GLES3 and Vulkan targets, and the render and memory budget. Software Architect, Technical Artist, Art Director, Performance Engineer, and Test/QA review the result, as required for render and asset changes.
- The URP switch changes `GraphicsSettings`, `QualitySettings`, and pipeline assets on purpose. Commit those changes as reviewed, deliberate changes. If `scripts/check-settings-noise.ps1` flags them, commit with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1` and record the decision; never let incidental noise into the same commit. Converted materials and shaders are reviewed for unintended serialized changes like any other Unity asset.
- After the switch, evidence must come from a URP build: the Edit Mode and Play Mode suites, the Windows test player, and an Android emulator or device run under the `unity,emulator` leases. Screens without pink or missing materials and readable sprites at 64 PPU are part of the acceptance.
- Not applicable to this project, so do not use without a new spec that introduces the boundary: IAP, LevelPlay, `build-live-game`, multiplayer services, Vivox, TextMeshPro, Localization, AI Navigation, 3D physics, Tilemap, WebGL optimization, and `new-unity-project`.

When an assignment goes to a Claude Code agent, name the applicable skills from the table, or "none", next to the allowed tools.
