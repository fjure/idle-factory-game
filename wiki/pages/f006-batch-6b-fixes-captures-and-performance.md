---
title: F006 batch 6 part B fixes, captures and B4b performance
type: decision
sources: [S063]
updated: 2026-11-26
---

# F006 batch 6 part B: T542, T561, T563, T545, T562, T544

Records: `specs/006-world-first-factory-experience/reviews/` (`p3b-followups.md` C-77..C-107, `final-test-evidence.md`, `performance-b4.md` part B4b). All device runs used the dedicated AVD `IdleFactory_Test_API_36`, input by `adb` only. (S063)

## Task chain and what each did

| Task | Issue / PR | Result |
|---|---|---|
| T542 | #714 / #781 | Verification of product and journeys. Its gaps went to the added fix task T561 (owner 2026-11-25). Counts then: Edit Mode 3167, Play Mode 3483, player 82. |
| T561 | #782 / #784 | Camera hand-over fix (no recentre on an unchanged band, C-80), 48 pt target asserted at device scale (126 px at 420 dpi, C-81), `RunShellDocument.EscapePressedThisFrame()` reads every keyboard because the AVD exposes 16 (C-85), F006 journey fixtures. C-77 and C-78 were a T542 misread: the red arrow is the per-machine Missing-input badge, not the attention marker. |
| T545 | #717 / #785 | Capture driver and Windows-class captures. First QA FAIL: the P3 cue tiles did not show their cues; fixed by capturing from real session states. |
| T563 | #786 / #787 | FR-027 fix: the world had no upgrade marker (the model existed, no view drew it). Draws the reused `F003-UI-ICON-UPGRADE` when an upgrade is affordable and no problem badge shows; yields to an Item boost (ART-2), hides below 24 px per cell, is not a tap target. A per-frame allocation-free affordability mask in `RunShellPresenter` rebuilds visuals when cash crosses a cost. |
| T562 | #783 / #788 | Device-class (emuP) captures from a capture-only development APK (`F006EmulatorCaptureBuild`, define `IDLEFACTORY_T562_CAPTURE`, test assemblies included). They are in-player `ScreenCapture` images with the watermark visible, not `screencap` of a shipping APK. |
| T544 | #716 / #789 | B4b performance measurement (below). |

(S063)

## Decisions worth keeping

- **Fix tasks follow findings, again.** T561 and T563 were added as tasks because verifier roles are read-only; T562 ran before T544 so the timing run happens on a quiet host. (S063)
- **The crispness analyzer crop moved to x 0.10 to 0.90**, because the landscape HUD columns sat inside the band. The owner accepted it as a measurement-window change with no threshold relaxed (C-79). (S063)
- **A model without a renderer is a gap.** FR-027 passed unit tests for a whole batch because `UpgradeMarkerVisible` was computed and tested but never drawn; the parent confirmed it against the real scene. (S063)

## B4b results (W006-V, base main 4d795384)

- Passes: budgets 4, 6, 7, 10, 11 (APK 46.78 MB, 356 variants, largest page 512x512, art 3.54 MB). Budget 8 passes on the same-state delta reading, F006 +8.5 to +11.4 over the F004-only value; with the marker forced on all 12 slots +9.7 to +11.4. Budget 9 not measured. (S063)
- Budget 5: `RunShellPresenter.Refresh` over an unchanged run allocates 0 (600 calls, asserted, packaged scene, player and Play Mode); the whole real-scene frame still allocates about 15 to 21 per steady frame (C-68, unattributed). (S063)
- Emulator FPS is about 46 to 47 on a quiet host with a cold-booted emulator, equal for the B4a-era build (ABA 44.8, 44.6, 43.8) and the fresh-run controls. B4a's 61.4 is not reproducible, F006 code costs nothing measurable, and a loaded host was not the cause of C-83. (S063)
- Autosave cost (C-66): a burst of 27 writes a minute is about 0.56 MB a minute on emulator storage. C-65 is closed by a real thread-pool player test (`AutosavePoolThreadPlayerTests`). (S063)

Related: [batch 6 part A](./f006-batch-6-verification-and-polish.md), [final gates and acceptance](./f006-final-gates-and-acceptance.md), [mobile performance targets](./mobile-performance-targets.md), [part B pitfalls](./f006-batch-6b-workflow-pitfalls.md), [plan overview](./f006-plan-overview.md).
