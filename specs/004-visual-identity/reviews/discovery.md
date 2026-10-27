# Discovery and Feasibility Reviews — Feature 004 (2026-10-01)

Read-only reviews run before `/speckit-specify`, at most three roles concurrently, Medium tier. Each role received a compact evidence packet (draft sections, Art Bible v0.1, F003 contracts, relevant wiki pages). The parent reconciled the findings into the spec and the plan.

| Wave | Role | Verdict | Key findings carried forward |
|---|---|---|---|
| Discovery | Game Designer / Product Owner | Split scope; keep restyle as the feature | User stories P1–P4; zero headroom at a 32 ppc floor; integer snapping wastes portrait space; badge labels unreadable at 1×; tool choices belong in the plan; subjective "feel" criteria replaced by measurable ones |
| Discovery | Art Director | Direction sound; four corrections | Integer zoom must be in device pixels; header-band/overhang conflict; spike must include slot plates, all cues and markers; palette budget 48; Art Bible v1.0 checklist and semver; brief-format changes; generator and provenance rules; producers never review their own output |
| Discovery | UI/UX | Proceed with gaps closed | Hit area = footprint only; overlays above all machine art; zoom model in device pixels; fit-all vs integer conflict; bottom-sheet visibility; pixel font only for headings; reduced motion; missing-sprite fallback; re-baseline `ZoomReadabilityTests`/`SlotGridTests` |
| Feasibility | Software Architect (+ DevOps lens) | Feasible with three changes | URP after F003's scene/settings issues; custom integer camera instead of `PixelPerfectCamera`; T110 resolution-agnostic catalog (accepted, merged in #253); Unlit default material and Gamma color space for parity; settings-noise hook does not cover Graphics/Quality settings → add an assertion test; tooling under `tools/art-pipeline/`, sources outside `Assets/` |
| Feasibility | Technical Artist | Feasible with conditions | Blender for machines and frames, scripted outline and palette pass, Standard color management; uncompressed RGBA32 world atlases; flipbook animation; manifest-driven importer; memory ≈ 1.1 MB world art at D = 32; automated checks list |
| Feasibility | Performance Engineer | Feasible; one baseline gap | B1 → B2 → B3 measurement order with same-session controls; visual worst-case workload; metrics and provisional budgets; main cost risk is the pipeline and any upscale render texture, not sprites |

**Owner decisions taken during discovery** (spec Clarifications): URP stays phase 0 of Feature 004; T110 extended now (merged); whole-number scales only; generated art may ship with the tool chosen per asset family by agents; reduced motion follows the OS with an in-game override in a separate file; an independent agent reviews the sheets and the owner performs timed trials and acceptance.

**Omitted roles**: Game Systems/Economy (no formula, balance or progression change, spec FR-001); Online Services (no remote boundary); Sound Designer and Composer (audio out of scope); Security/Privacy reviewed only the new local display-preferences file in the plan wave (no player data, no trust boundary beyond local file validation).
