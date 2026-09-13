# Contract: Development Presentation and Interaction States

**Purpose**: Define what the Unity view must render and which semantic commands it may issue without becoming gameplay authority.

## Interaction modes

| Mode | Inputs | Output / transition | Forbidden overlap |
|---|---|---|---|
| Browse | tap/click, drag, pinch, wheel, zoom buttons | select/clear selection; pan/zoom camera | Drag/pinch/UI pointer cannot place |
| Placement preview | tap/click world, drag, pinch, controls | snapped candidate footprint and validity/reasons | Preview cannot reserve or mutate occupancy |
| Placement confirm | visible Confirm | one `PlaceBuilding` command; success returns Browse/selected instance | Disabled when invalid; no direct grid write |
| Placement cancel | visible Cancel/back action | discard preview and return Browse | No factory mutation |
| Inspector | visible controls | `SetBuildingActive`, explicit development add/remove operations | Never hold mutable building reference |
| Persistence pending | Save/Load/return initiated | disable conflicting persistence action; show progress/status | No speculative success/reward |
| Offline result | durable reconciliation complete | show summary and acknowledge/close | Must distinguish effective budget from actual production |

A pointer that begins on UI remains UI-owned until release/cancel. Orientation change or focus loss cancels an in-progress world gesture. A second world-owned touch cancels the one-finger pan/select candidate and starts pinch; the whole pinch remains consumed through release and cannot select, move a preview or place. Navigation cannot accidentally confirm placement.

## Factory view states

- Factory bounds/grid are visible at useful zoom levels.
- Each building view derives anchor, footprint and placeholder key from a factory snapshot.
- Selected state uses outline/symbol plus color.
- Placement preview renders every footprint cell and a valid/invalid symbol/text reason plus color.
- Production badges distinguish producing, missing input, full output and inactive. Multiple blockers remain accessible in the inspector.
- Recreating all views from the same snapshot changes no logical state.

## Inspector states

| State | Required content |
|---|---|
| Empty selection | Neutral instruction; no stale controls |
| Selected producer | Instance/development name, active state, recipe, progress/duration, input available/required, output quantity/free capacity |
| Producing | Producing label and progress |
| Missing input | Each missing resource and amount |
| Full output | Each constrained output and free/required capacity |
| Inactive | Inactive label; retained progress visible |
| Stale after restore | Resolve persistent ID in new snapshot or clear to empty before enabling commands |

Development supply/removal controls are visibly labelled as test operations and target the selected instance's explicit input/output buffer. They do not imply a player inventory.

## Persistence and return states

| State | Required feedback / actions |
|---|---|
| No save | Explain absence; allow continuing/resetting and saving |
| Saving | Pending indicator; no success yet |
| Save succeeded | Durable confirmation |
| Save failed | Error and retry; current factory and previous checkpoint remain usable |
| Loading/returning | Pending indicator; do not show candidate resources |
| Development load | Explicitly labelled load with zero offline elapsed time; on success cancel preview and resolve/clear selection |
| Invalid/unsupported checkpoint | Explain failure; preserve current world; expose explicit recovery only if a previous generation is available |
| Return blocked | Disable gameplay mutations; offer Retry Return and Reload Authoritative Save, plus explicit previous-generation recovery only when valid |
| Invalid/backward time | Zero credit explanation; no claim of clock protection |
| Offline applied | Measured absence, credited duration (max 8 h), 80% effective-time budget, actual resource deltas, retained progress and limiting reasons |
| Offline had no eligible work | Same duration fields plus inactive/missing/full reason; no misleading “6.4 hours produced” wording |

## Responsive/accessibility validation matrix

Keep the current development autorotation settings. Verify each state in portrait and landscape where applicable:

| Class | Viewport fixtures | Primary evidence |
|---|---|---|
| Compact phone | `360×640` and rotated | Touch targets, no overlap/clipping, safe-area application |
| Tall phone | `390×844` and rotated | Inspector/return summary, pan/zoom space |
| Tablet | `768×1024` and rotated | Layout does not stretch controls or hide state relationships |
| Desktop development | `1280×720`, `1920×1080` | Mouse click/drag/wheel and visible-button parity |

- Apply `Screen.safeArea` after converting Unity screen coordinates to UI Toolkit panel coordinates.
- Text scales without truncating state or numerical meaning.
- Status and placement do not rely on color alone.
- Touch targets and spacing are checked on named physical Android/iOS devices before mobile acceptance; Game View is layout evidence only.
- Every actual development UI control is focusable, shows visible focus, and supports keyboard activation on the desktop path. The touch journey requires no keyboard/right-click.

## Temporary control mapping

| Semantic action | Touch | Mouse / desktop | Visible alternative |
|---|---|---|---|
| Select | Tap building | Left click building | None required |
| Pan | One-finger drag beginning on world | Left/middle drag beginning on world | Optional edge/drag only; no keyboard requirement |
| Zoom | Pinch | Wheel | `+` and `−` buttons |
| Enter build | Build button | Build button | Same |
| Move preview | Tap world cell | Left click world cell | None required |
| Confirm/cancel | Visible buttons | Visible buttons | Same |

Exact gesture thresholds and camera limits are implementation/test-fixture settings, documented beside the Input adapter. They are replaceable and do not become final building UX.

## Placeholder visual brief

**ID/version**: `F001-Development-Placeholders v0.1`

**Owner**: Asset/UI implementation assignment
**Reviewers**: Art Director, Technical Artist, UI/UX Reviewer

- Top-down logical grid; fixed D001 orientation; support D001 2×2 and alternate footprint fixture.
- Before production, the brief lists each asset ID/type, required states, target pixel dimensions, file format, pivot/cell mapping, source/import path, provenance and version. Repository-owned geometric source assets need no generated or licensed external art.
- Required states: normal, selected, placement-valid, placement-invalid, producing, missing-input, full-output, inactive.
- No input/output port drawing is a logistics contract.
- Source/import paths live beneath `Assets/IdleFactory/Art/Development/F001/`; every Unity asset has its `.meta` file.
- Technical Art records pixels-per-unit/cell mapping, pivot, sorting, import/compression, zoom readability and memory observations.
- No animation framework, atlas optimization, ComfyUI, Blender, final Art Bible, production audio or shaders are required.

## Traceability

- Navigation/placement: US1, FR-001–006, SC-001/005
- Inspection/feedback: US2.8, FR-014, FR-028, SC-005
- Persistence return: US3.4–9, FR-027/034–037, SC-007/009/010
- View independence: US4.3, FR-006/021, SC-006
- Temporary content: FR-030, SC-008, A-004
