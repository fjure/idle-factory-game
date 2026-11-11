# Quickstart: Validating Feature 006

This is a validation guide, not an implementation guide. Details live in the [contracts](./contracts/), the [data model](./data-model.md) and [research](./research.md).

## Prerequisites

- Feature 004 complete on `main` (77/77 tasks, T447 and T449; verified 2026-11-11 at `1dab60d`).
- Unity `6000.6.0f1`. Leases through `scripts/resource-lease.ps1`:
  - `unity` for editor and CLI runs;
  - `unity,emulator` for any run whose `Library/` came from an Android build, and for `-StandaloneWindowsPlayer`;
  - `gpu` for Blender and ComfyUI renders;
  - `unity,emulator,gpu` for B4.
- Art tooling per F004: `uv` with the pinned Pillow, Blender 5.2.1 and ComfyUI per `docs/integrations/mcp-readiness.md`, each verified by a real tool call in the session.

## Scenario 0: Baseline and direction gate (US8, FR-040, FR-041, FR-046)

1. On the current build, run the baseline probe and the screenshot matrix (research R02).
   - Expect `reviews/baseline.md` with area shares, interaction counts and label counts per screen class.
2. The World Designer drafts `docs/world/world-bible-v0.1.md` and `reviews/world-copy.md`.
   - Expect a neutral starter site, its name, its mood, the naming rules and the swappable-ground rule.
   - Expect the owner to approve the bible before any direction brief is written (FR-047).
3. Run the `tools/art-pipeline` `mockup` command for each direction. It reads the shared layout-geometry JSON.
   - Expect sheets for portrait 1080x2400, landscape 2400x1080, a 640x360-class screen at 200 % text, and tablet 1536x2048.
   - Expect 6-frame animation strips and a reduced-motion still.
   - Each sheet shows the active factory linked to the depot, the card beside a selected machine, the hub open, the stage triptych and a 200 % variant.
   - Every sheet is in the Art Bible palette at real pixel scale.
4. Independent UI/UX and Art Director sessions review the sheets; the owner decides.
   - Expect `reviews/direction.md` with the comparison, the recommendation and the dated owner decision (OD-9).
   - No P2 or P3 production starts before that decision.

## Scenario 1: Refactor without behaviour change (P1)

1. After the slot-grid split, run `powershell -NoProfile -File scripts/test-unity.ps1`. Then run `scripts/check-settings-noise.ps1 -Commit <sha>`.
   - Expect the same pass set as before.
   - Expect no scene or serialized diff.
   - Expect no test edits beyond `using` lines.
2. Run `WorldLayoutParityTests` (including `LargestFittingScale` parity), `WorldLayoutValidationTests`, `LayoutIndependenceTests`, `SortingLayerGuardTests` and the `CompletedCycles` tests: lifecycle, property and no-observer-effect.
   - Expect the API fixture diff to be recorded.
   - Expect exact parity for 12 locations.
   - Expect the 6-location, 20-location and L-shaped test layouts to frame, hit-test and select correctly (SC-011).

## Scenario 2: World art and environment (US2, FR-011 to FR-015)

1. Run `powershell -NoProfile -File scripts/test-art-pipeline.ps1`.
   - Exit code 0 is a pass. Exit code 3 means the tooling is unavailable; that is a blocker, never a pass.
2. Run `F006AssetContractTests`, `DeckLayoutTests`, `DeckRenderTests`, `DepotLinkModelTests`, `DepotLinkViewTests`, `DepositViewTests`, `StageTriptychDeltaTests` and `ReducedMotionWorldTests`.
3. Run the World Designer conformance check (`reviews/world-bible-conformance.md`).
   - Expect zero conflicts (SC-013).
3. Capture the stage triptych and the per-class captures.
   - Expect one continuous deck.
   - Expect hatch, outline and solid conditions that differ by shape.
   - Expect no prop over cues.
   - Expect links only from machines to the depot.
4. Re-run SC-005: independent blind naming in grayscale and three CVD filters, then the owner timed trial with `tools/art-pipeline/trials/timed_trial.py`.
   - Expect 9/9 naming per filter.
   - Expect at least 10/10 within 2.0 s, with a median of at most 1.077 s.

## Scenario 3: Machine card and visual state (US3, US4)

1. Run `MachinePresentationModelTests` and `AttentionModelTests`, including the shortage mismatch cases.
   - Expect trace and save fixtures to stay byte-identical.
2. Run `MachineCardTests`, `MachineDetailsParityTests`, `CardCameraTests` and `AttentionDisplayTests` on every `ShellScreens.All()` class at 100, 150 and 200 %.
   - Expect the selected machine to be 100 % visible.
   - Expect upgrade in place.
   - Expect Remove only in Details, behind confirmation.
3. SC-004 (guidance): run the owner trial on 10 seeded mixed-state captures. Record the results against the 3.0 s and 5.0 s guidance.

## Scenario 4: Hub, back stack and supporting screens (US6)

1. Run `HubPanelTests`, `ShellNavigationStackTests`, `BackOrderTests`, the re-baselined `AndroidBackGestureTests`, `KeyboardTraversalTests` and `SurfaceConsistencyTests`.
2. Run the scripted interaction journey (R02 probe).
   - Expect every system to be reachable.
   - Expect return to the factory in at most 1 interaction.
   - Record the interaction counts against the baseline.

## Scenario 5: Building, feedback, milestones (US5)

1. Run `BuildCardTests`, `EffectPoolTests`, `MilestoneWatcherTests`, `MilestoneBannerTests` and `DevelopmentStageTests`.
2. Run `ReducedMotionWorldTests` with reduced motion on.
   - Expect zero moving decorative pixels.
   - Expect static completion and milestone feedback.
3. SC-006 (guidance): an independent agent orders 10 shuffled stage triptychs.

## Scenario 6: Full regression, performance and devices (SC-007 to SC-009)

1. Run `scripts/test-unity.ps1` (Edit and Play Mode), then `scripts/test-unity.ps1 -StandaloneWindowsPlayer` (with the `F006AllocationTests` and W006-V 600-frame run), then `scripts/test-art-pipeline.ps1`.
2. Run `scripts/android-emulator-journey.ps1` with the retargeted `Default`, `F003`, `F004` and `F005` scenarios (hub-entry taps), then with `-Scenario F006`. Then complete the manual real-input checklist on the Windows player for the hub, the card, the build card and Back.
   - The journey covers select, upgrade, the hub to Contracts and back, build, and rotation with the card open.
3. Run the B4a and B4b measurements per research R16.
   - Expect every frozen F004 budget to be met.
   - Expect the emulator FPS rows to be marked exploratory.

## Scenario 7: Owner acceptance (SC-012)

1. Produce before/after sheets from real builds (emulator and Windows player) for each screen class and state:
   - no panel;
   - card open;
   - details open;
   - building;
   - hub open;
   - a system view;
   - the offline summary;
   - reduced motion;
   - 200 % text.

   Also produce the stage triptych.
2. Record the guidance measurements against `reviews/baseline.md`.
3. The owner records a verdict per surface in `reviews/final-acceptance.md`, together with the A-9 limitation.
