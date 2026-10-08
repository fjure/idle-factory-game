---
title: Run panel conventions
type: howto
sources: [S018, S019, S021]
updated: 2026-10-08
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
