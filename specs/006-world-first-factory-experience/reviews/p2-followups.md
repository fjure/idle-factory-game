# F006 batch P2 follow-ups (parent record)

**Batch**: P2, art families and the first ready gameplay and UI Issues (T519–T524, T532, T533, T538), 2026-11-12 and 2026-11-13.
**Recorded by**: the parent orchestrator, from the worker reports, the independent QA reports and the independent reviews of the merged Issues.

None of these items blocked a merge. Each one names the later task that carries it, and the assignment for that task cites this file. The P1 follow-ups in [p1-followups.md](p1-followups.md) still apply. This batch closes P1-F9 (trunk recolour, by T522), P1-F10 (production tail brief, by T524) and P1-F5 at source level (front-row-only wall placement, by T521). The composite C8 proof stays open.

## Art families (T519–T524)

| Id | Source | Follow-up | Carried by |
|---|---|---|---|
| P2-F1 | T519 (#692) QA | The T519 task text names ground "edge/corner/T tiles". The signed-off §21.4 table (OD-10) puts all edge, corner and quiet art in F006-EDGE (T521), and no ground edge sprites are owed. Align the T519 wording or note it in the Art Bible. | T525 (Art Director) |
| P2-F2 | T522 (#695) worker and QA | Add `f006-fx-completion-highlight` (68×36, 1 frame, the static reduced-motion pop) to the §21.4 FX table. Refresh the FX atlas arithmetic (43 frames, +2,448 px; still fits 256²). Name the highlight in §21.7. | T525, then an Art Bible revision by the parent |
| P2-F3 | T522 QA | §21.7 and §21.8 item 2 say the Assembler loop is "already at 3 (R12)". The T522 `check_flash` matrix measures 0–2/s for the Assembler with its FX. Reconcile the wording, or cite the measure R12 used. Record the pace caps per family: Miner, Smelter and Press 10 fps; Assembler 8 fps. | T525 |
| P2-F4 | T522 (P1-F9) | Research R12 says lamps "use steel or teal tones". Replace this with: lamps are static, with a `copper_2` head and a `steel_3` cap, and no `teal_*` or `lit_*` (Art Bible §21.2 and §21.8 item 5). | Parent, with the P2-F2 revision |
| P2-F5 | T524 (#697) worker and QA | The production K4 tail sprites (`art-source/f006/cue/`) are not yet swapped into the imported F004 cue sprites under `Assets/IdleFactory/Art/F004/cues/`. Swap them through Unity tooling after the T525 Art Director pixel review. In the same change, switch `FactoryWorldLayout.KeepOutOf` from `StateCueGeometry.BadgeBox()` (23×23) to `BadgeExtentBox()` (23×27), or document why not, and re-check the layout and lattice-export tests. | T526 (import), after T525 |
| P2-F6 | T520 (#693) QA | The depot (80×76 canvas, 80×40 footprint, pivot (40,20)) and the deposit carry `steel_*` by allowance. Prove that no depot or deposit pixel enters a C8 ring in the composite. Confirm hopper, collar and lamp visibility at 640×360 and 200 % text. Sum the vein chip loop (2/s alone) with any other FX in view against 3/s. | T527 (composite), T534 (camera, with P1-F2) |
| P2-F7 | T521 (#694) worker and QA | Edge placement questions for the Art Director and World Designer: (a) fl/fr/corner read as front-left, front-right and front-apex pieces for every stage (concept B had its wall on the back edges, and the back row fails keep-out); (b) wall and gantry on the front margin row only, so is a front-only wall an acceptable "Walled yard"? (side rows are 1–2 px from the machine column); (c) the deck top is `floor_0`, the same as the field; (d) a seam every 35 px. Add the front-row-only rule and the iso-diagonal layout of the pieces to §21.4 once decided. The stage-2 wall stays provisional until the composite C8 proof (P1-F5). | T525 (review), T527 (placement) |
| P2-F8 | T519–T521 | The Blender MCP bridge was unreachable in every P2 render task. Iteration ran headless only, and each provenance record says so. `art-source/f006/pipeline_ground_foundation.py` (T519) is now a shared helper for the T521 driver, so treat it as frozen or move its helpers into `tools/art-pipeline`. | Owner note; any later art task |
| P2-F9 | T523 (#696) | All three F006-UI elements are recorded as "not needed": the hub icon reuses the contracts glyph, the attention dot is a USS circle, and the banner is a USS box. The choice of the contracts glyph for "Office" is a metaphor, and the banner's plainness is left to the Art Director. | T525, then T535 and T540 |

## Attention and presentation models (T532, #701)

The independent reviews were Software Architect APPROVE_WITH_NOTES and Game Systems CONFORMS_WITH_NOTES.

| Id | Follow-up | Carried by |
|---|---|---|
| P2-F10 | **Delta repeat risk.** `MachinePresentationModel` clears `CompletionsDelta` only inside `Refresh`. A caller that skips `Refresh` when `NeedsRefresh` is false sees the same delta every frame. Call `Refresh` every frame, which is cheap and allocation-free (the snapshots are built only when `NeedsRefresh` is true). Fix the class doc wording. Add a model test that reads the buffer twice without a revision change, and a T529 test over N frames. | T529 |
| P2-F11 | Contract `presentation-model.md` revision 3 should state: at most 2 shortage markers plus 1 store-full marker; the completion-delta display cap of 3; the 22 px shift of a second marker in the same row; "+N" counts shortage resources only; and what `IsSharedCause` means for store full. | Parent, with T528 |
| P2-F12 | On a session replace (restore, prestige, new run), call `MachinePresentationModel.ResetBaseline()` and `AttentionModel.Invalidate()`. Pass `suppressDeltas` on the first refresh after an offline return. Add a real-session test that cycle completions bump `QuantityRevision`. | T528, T529, T534 |
| P2-F13 | Decide the tap priority between attention-marker hits (a circle of at least 48 pt) and machine tap shapes, and test it. The T534 card cause line reads `LimitingResourceId` and `ShortOnCopy` from these models instead of deriving them again. | T528, T534 |
| P2-F14 | The per-family fps cap table is hardcoded for `machine.assembler` in presentation. Move it to content data. | T529 |
| P2-F15 | Game Systems notes: (N1) the domain also emits `MissingInput` blockers for disallowed outputs; these are normally locked and therefore skipped, but add a test or count recipe inputs only. (N2) On the derivation-overflow path a blocked machine adds no demand, so `HasDemand` can hide a shortage; add a comment or test. (N3) Document in R09 that a locked input gives no shortage. The upgrade marker's "never red" rule goes to the marker art task. | T528 or T529 (tests), parent (R09 note) |

## Panel scaffold (T533, #705)

The independent UI/UX review was ACCEPT_WITH_NOTES.

| Id | Follow-up | Carried by |
|---|---|---|
| P2-F16 | The scaffold root is a full-screen, opaque absolute fill. It fits full panels but not the docked machine card (OD-9b C2: the card never covers machines) or a bottom sheet. Either add a documented docked variant (transparent root, optional header, no full-screen fill), or record that T534 and T535 use only the scaffold's classes and helpers. | T534, T535 |
| P2-F17 | The scaffold has no sheet mount point and no row class. Add a shared `.scaffold-row` (48 dp, the shell focus ring) for the hub rows (C3). | T535 |
| P2-F18 | The static loading bar is filled to 33 %, which can read as progress. Make it an indeterminate-looking block, or animate it in normal motion with this static form as the reduced-motion fallback (C5). | T536 |
| P2-F19 | The state card's Back ignores `BackVisible`, so the hub root would offer Back. A disabled control's reason reaches touch users only through `reasonLabel`, so callers must pass it. Check that notices (up to 3) leave Upgrade and Back usable at 640×360 and 200 %. Chrome at 150 % text is held to the 20 % limit; the binding wording names only 100 % and 200 %. | T535, T534, T536 |

## Display preferences v2 (T538, #709)

| Id | Follow-up | Carried by |
|---|---|---|
| P2-F20 | `reviews/world-copy.md` does not record the copy of the new Settings toggle: the label "Show zoom buttons" and the hint "Adds zoom buttons to the factory view. Pinch zoom still works." It needs a World Designer read, and the text belongs in the copy list. | T547 (World Designer conformance), parent |
| P2-F21 | Security/Privacy notes: the v1 parser now runs through the shared token walk with v2 off, and the v1 tests cover it. A symlink at `display-v2.json` is followed on read, but the read stays bounded and the directory is the trust boundary. The replace retry blocks the main thread. | Note only |
