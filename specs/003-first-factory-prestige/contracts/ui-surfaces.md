# Contract: UI Surfaces and States

**Consumers**: UI/UX Developer, independent UI/UX Reviewer, Art Director, Test/QA.
**Normative for**: FR-011, FR-032–FR-034, SC-007 and the player-visible parts of US1–US5.

## Global rules (carried from Feature 001 and FR-032/FR-033)

- **Supported screen classes**: the existing matrix `360x640`, `390x844` and `768x1024`, each with its rotated form, plus desktop `1280x720` and `1920x1080`. The existing `FactoryHudLayout` classes apply (phone, tablet portrait, tablet landscape, desktop). Safe areas are respected, and layout works in panel points, never pixels (wiki: UI Toolkit HUD layout pitfalls).
- **Controls**:
  - Every actionable control is at least **44×44 points**, activatable by touch and mouse, and reachable and activatable from the desktop keyboard with a visible focus ring.
  - A clipped control scrolls into view when it receives focus.
  - Text scales, and no state is conveyed by color alone (icon, text or shape plus color).
- UI-owned pointers stay UI-owned. A world gesture never activates a panel control, and a panel press never pans the world.
- **Rotation** cancels any open confirmation, drag-to-reorder or pending selection without spending, placing or confirming prestige, and it creates no offline summary (wiki: Android runtime rotation).
- **Rejections**: every rejected command shows its reason code's copy next to the control that was used, and the state stays unchanged. Affordability is shown before the tap (disabled with the reason visible), and the command is still re-validated.
- **Numbers**: compact display is floored (R06). Inspection (focus, hover or long-press) shows the exact value.

## Surfaces

| Surface | Normal content | Loading | Empty | Disabled | Error / recovery |
|---|---|---|---|---|---|
| Slot grid (world) | Fixed slots: empty (buildable), locked (the level that unlocks it), occupied (machine sprite with a running, paused, waiting, missing-input, storage-full or inactive badge; boosted and Overdrive markers) | Startup overlay until `StartupStatus` resolves | 4 empty slots in a fresh run, with a "Build a Miner" hint | Locked slots show the requirement | — |
| Build sheet (slot selected) | Unlocked machine types with cost, rate, inputs and outputs; duplicates allowed | — | "No machine types unlocked" (unreachable with valid content; defensive) | Unaffordable or locked types show the reason | Rejection copy inline |
| Machine panel | Level, effective rate, current cycle progress, blockers with corrective hints, upgrade (cost, next rate, milestone), active toggle, remove | — | — | Upgrade at max level or unaffordable | Remove needs a confirmation dialog that shows the refund and the FV change |
| Inventory & rates | Per resource: stock, net rate, price, auto-sell toggle; store usage bar with number | — | "Nothing stored yet" | Locked resources hidden; auto-sell disabled for locked | Overflow shows `value.limit` |
| Priorities | Ordered list with move-up and move-down buttons (keyboard and touch), plus drag on pointer devices | — | "Build machines to set priorities" | — | Stale order rejects and re-renders |
| Expansion | Storage and factory level, next capacity or slots, cost | — | — | Max level | — |
| Contracts | Offers (kind, required units, rewards, duration); the active contract with progress against stock, a deadline countdown and Deliver | — | "Contracts unlock after …" (content unlock) | Deliver disabled while stock is insufficient (shows the missing units); Accept disabled while a contract is active | Expiry outcome card; delivery at the deadline instant succeeds |
| Items | Stacks by tier with count; activate flow → target choice (machine type) → confirm; active effects with countdowns | — | "No Items yet — earn them from contracts" | Invalid target or conflicting effect, with the reason; the Item is kept | — |
| Overdrive control (HUD) | Ready / Active (remaining) / Cooldown (remaining) | — | — | Locked until its unlock | — |
| Research | Graph or list with level, cost, effect, prerequisites | — | "Earn RP from contracts" | Missing prerequisite or RP, with the reason | — |
| Prestige | FV breakdown (Cash, stock value, machines, expansion), IP preview, missing value, reset and preserve lists, Confirm and Cancel | "Saving…" during a durable transition | — | Below the threshold: disabled with the missing FV | Failure: "Factory Sale was not completed — your run is unchanged" + Retry |
| Industry | IP balance; upgrades with level, cost and effect; "applies next run" tag for starting effects | — | — | Unaffordable or at max | — |
| Offline summary | Measured, credited and effective durations, stock and Cash deltas, sales, expired effects, contract outcomes, limits | — | Short absence: "No production while away" | — | Invalid clock: explanation and Retry (Feature 001) |
| Startup / import status | "Importing your development save…" | Spinner with text | — | — | Import blocked: failed rule copy and the OD-3 actions; corrupt V2: Feature 001 recovery (previous generation) |

## Evidence required (QA and UI/UX review)

- A Play Mode test per surface for normal, empty, disabled and error states, reached through application seams without editing the scene.
- A layout test at every screen class asserting that there is no overlap, that clipping is contained and that the focused control is visible (extending `HudLayoutOverlapTests` and `ZoomReadabilityTests`).
- Keyboard traversal: every control is reachable, and Enter/Space activates it.
- Touch-target measurement of at least 44 pt on every actionable element.
- An independent UI/UX Reviewer record in `reviews/ui-ux-review.md` citing the screen, state and acceptance scenario.
