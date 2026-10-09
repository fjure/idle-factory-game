---
title: Run panel conventions
type: howto
sources: [S018, S019, S021]
updated: 2026-10-09
---

# Run panel conventions

Rules every [run panel](./run-panels.md) follows, and the pitfalls the panel tasks T113–T118, T196 and T243 hit. (S018)

## Shared conventions

- Soft-disabled controls stay focusable and show their reason. A press still sends the command, and the rejection copy appears next to the control, matching `contracts/ui-surfaces.md` › Rejections. (S018, S019)
- Panel USS uses only the `--run-fs-*` sizes. Every panel suite runs a worst-case layout test at all screen classes, rotations included, at 100 % and 200 % text. (S021)
- Lock and warning icons go through `PanelIcons.Mark` (defined in `BuildSheetPresenter.cs`): the icon is the label's background image inside a 30 pt left gutter, so it shows and hides with the text and the label keeps no child element. Text buttons never get an icon element. A panel sets a rejection through one `SetReason` helper so text and icon cannot diverge; without a resolver or sprite the text stays plain. (S019, S021)
- Panel tests that load assets through `AssetDatabase` sit inside `#if UNITY_EDITOR` (see [Unity player validation](./unity-player-validation.md)). (S021)

## Authoring pitfalls

- A sole wrapped `Label` in a short sheet is clamped to the viewport height. T115 fits it with a small geometry handler; T117 keeps a private copy, so the names do not collide. (S019)
- Non-ASCII literals such as `…` and `—` in `.cs` files broke the text in a T117 test run. Use `\u` escapes in C# and character references in UXML. (S019)
- One arrow press can arrive as both `KeyDown` and `NavigationMove`. The Research graph handles both in trickle-down, calls `PreventDefault` and de-duplicates per frame. (S019)
- A node or card larger than its viewport (200 % text on a 640x360 sheet) can only be aligned at its top-left corner. (S019)
- A `ScrollView` with both scrollers hidden also loses its own wheel handling (UI Toolkit). The Research graph (T248, #336, `5bfa6b1`) takes `WheelEvent` in trickle-down: wheel scrolls the sheet, Shift+wheel or a sideways wheel scrolls the graph, and a left-button drag on empty canvas pans pointer devices. Touch and keyboard are unchanged. Known gap: a plain wheel over the graph is consumed even when the sheet cannot scroll, and the feel with a real wheel is left to T128. (S018, S019)

## Stable layout under changing state (T249)

- Sections that appear and disappear above the controls move those controls. At a full store the "Storage full" blocker (151 pt at 100 %, up to 360 pt at 200 %) toggled above Upgrade, Switch off and Remove, so the panel jumped (#337, `bb6bbb5`, `fb1a29f`). Owner decision 2026-10-09: no reserved blank space; transient blocker content goes below the actions and grows only the panel's end. Small reserves keep the status, last-batch and progress text from changing height. (S018)
- A scrollbar that appears narrows the content and rewraps every line. The machine panel keeps the sheet scrollbar always visible while it is open and restores it on hide and dispose. On small screens at 200 % the blocker needs scrolling. (S018, S019)
- Layout stability is a presentation rule: no simulation or blocker rule changes, and a persisting blocker is never hidden. (S018)
