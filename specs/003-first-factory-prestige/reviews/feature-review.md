# Feature 003: Independent feature review (T132)

Task T132 (Issue #212, work unit WU68) records four independent, read-only verdicts on the exact merged commit `6fbdafa` (origin/main after the T128 and T129 merges). Covers Constitution IX and all FR and SC of `spec.md`.

## How this record was produced

- Each verdict was written by a separate agent session with its own role lens. None of the four sessions implemented Feature 003 code, and none read another verdict before writing its own.
- No session ran Unity, a player, a build or adb. The verdicts rest on code, test sources, contracts and the recorded review evidence. The full suites, the console check and the Windows test player are rerun on the accepted commit in T133.
- The parent orchestrator only assembled the four sections below in this file. It did not change their wording or verdicts.

## Summary

| Role | Verdict | Blocking items | Main conditions |
|---|---|---|---|
| Game Designer / Product Owner | ACCEPT WITH CONDITIONS | none | Owner decision on SC-008 (W003-B) and the HUD allocation; F-9 exemption and N-5 decision recorded in T133; SC-003/SC-004 labelled as owner judgment; emulator-only labels; follow-up Issues |
| Software Architect | ACCEPT WITH CONDITIONS | none | PR-1 as owner decision (measure first; steady-state detection only after an Architect-reviewed design); F-A1 with the failing safety-checkpoint test; three reason codes added to the contract; split `RunPersistence`/`RunSnapshots` and tighten `RunState` mutators before F005 |
| Game Systems / Economy | ACCEPT WITH CONDITIONS | none | SC-003/SC-004 times labelled as scripted-trace targets, not observed; Ore-only probes for runs 2 to 5 and a contracts-only probe; F005 telemetry and playtest list carried into the F005 spec |
| Test/QA | ACCEPT WITH CONDITIONS | none (becomes REJECT if the T133 full or player suite fails) | T133 rerun list: full Edit and Play Mode, `-StandaloneWindowsPlayer`, console, settings noise, quickstart §1 to §9, emulator figures re-measured or labelled as carried forward |

Overall: **ACCEPT WITH CONDITIONS.** No role found a blocking item. The conditions are carried into T133 (`reviews/final-acceptance.md`) and into follow-up Issues.

## Roles

Run as separate review Issues before this record: UI/UX Reviewer (T128, `reviews/ui-ux-review.md`), Art Director and Technical Artist (T129, `reviews/art-direction-review.md`), Performance Engineer (T130, `reviews/performance-review.md`), Security/Privacy and DevOps/Operations (T131, `reviews/security-devops-review.md`).

Omitted, with reasons:

| Role | Reason |
|---|---|
| Online Services Engineer | Feature 003 has no remote API, account, cloud save, analytics or other online service. |
| Sound Designer, Composer | Feature 003 adds no audio. |
| 3D and Render Artist | Deferred by the role-activation rules; Feature 003 uses 2D sprites from the approved source pipeline. |
| Asset Artist | No new source assets in this phase; the 78 F003 sources were approved in T109/T143 and integrated in T111. |

## Game Designer / Product Owner verdict

### Scope and commit

- Issue #212 (T132, WU68), Feature 003 `specs/003-first-factory-prestige`. Role: Game Designer / Product Owner, verdict lens, read-only, Medium tier. Independent of the implementers and of the Architect, Game Systems and Test/QA verdicts.
- Commit reviewed: `6fbdafa` (origin/main, merge of PR #346). No Unity, player, build or adb was run; no lease taken. The verdict rests on the review records, the test names (checked by grep in the worktree) and the code at that commit.
- Shipped content: `Assets/IdleFactory/Content/Iron/iron-factory-content.json` has `contentRevision` 2 and git blob `66f41fb`, equal to the revision the owner approved in `reviews/content-approval.md` (T127). Verified.
- Evidence ages: the full Edit/Play Mode suites (T120, `be29326`), the Windows test player (T121, `5f738e6`) and the emulator rerun (T124) predate T246 to T250 and later merges. The UI/UX final review (`872984a`) re-ran the affected Play Mode files. T133 must rerun everything on the accepted commit; this review treats the older runs as supporting, not final.
- Scope note: spec.md has six user stories (US1 to US6, US6 is content authoring); all six are covered below, not only US1 to US5.
- Evidence key: EM = Edit Mode test, PM = Play Mode test, PL = tagged Windows player test. Review records are under `specs/003-first-factory-prestige/reviews/`.

### Acceptance scenarios

Status: Met / Met with caveat / Not met / Unproven.

| Story | Scenario | Evidence | Status |
|---|---|---|---|
| US1 | 1 Build charges once, duplicates allowed | EM `RunCommandTests.Build_ChargesTheCostOnce_AndCreatesALevelOneMachineThatJoinsThePriorityLast`; PM `BuildSheetPanelTests`; emulator touch build (cash 25 to 15, `android-emulator-evidence.md` "Touch journey") | Met |
| US1 | 2 No slot, locked type or low Cash: nothing changes, blocker explained | EM `Build_RejectsWithTheNamedReason_AndChangesNothing`, `EveryCommand_IsAtomic_AtEveryHeadroomNearTheCashBound`; PM `BuildSheetPanelTests`; locked-slot notice in `art-direction-review.md` Integration (AD-2) | Met |
| US1 | 3 Saved priority decides contention, not position or frame rate | EM `RunSimulatorTests.Priority_DecidesContention_RegardlessOfSlotOrBuildOrder`, `RunSimulationEquivalenceTests.CaseC_ChangingThePriorityChangesWhoWinsContention`; PM `PrioritiesPanelTests` | Met |
| US1 | 4 Full store: net-fitting conversion finishes, input-free Miner blocked | EM `RunSimulatorTests.FullStore_NetFittingSmelterCompletes_InputFreeMinerStaysBlocked`, `CaseF_FullStore_EveryPartitionGivesTheSameState`; PM `InventoryRatesPanelTests.AFullStoreSaysSoWithTextAndNotOnlyColor`; FULL badge seen in `art-direction-review.md` Integration | Met |
| US1 | 5 Upgrade at a milestone changes rate once, next cost visible | EM `RunCommandTests.Upgrade_AtAMilestone_ReportsTheMilestoneOnce_AndTheNextOne`, `Upgrade_MidCycle_KeepsTheCompletedFraction`; PM `MachinePanelTests` (89 pass, `ui-ux-review.md` Final review) | Met |
| US2 | 1 One auto-sell setting changes only that setting | EM `RunCommandTests.SetAutoSell_ChangesOnlyTheSetting_ThenSellsTheStockAtOnce`, `RunSimulationEquivalenceTests.CaseG_AutoSellOfTheInput_StarvesTheSmelter_UntilItIsSwitchedOff`; PM `InventoryRatesPanelTests.TurningAutoSellOnSellsTheStockThroughTheCommand` | Met |
| US2 | 2 Delivery consumes exactly the stock, reward once | EM `RunContractTests.Deliver_ConsumesExactlyTheRequiredUnits_AndGrantsEveryRewardOnce`, `Deliver_ARepeatDeliveryCannotGrantTwice`; PM `ContractsPanelTests.DeliveringGrantsTheRewardsAndShowsTheOutcome`; PL journey delivers a contract (`full-suite-evidence.md` T121) | Met |
| US2 | 3 Timed contract expiring offline: late delivery unavailable, outcome visible, no duplicate reward | EM `OfflineSegmentationTests.ContractDeadline_IsInclusive_AlsoOffline`, `RunContractTests.Expiry_Online_AdvancesNothingGrantsNothing_AndKeepsTheOutcome`, `Deliver_OneMillisecondAfterTheDeadline_IsExpired_AndGrantsNothing`; PM `OfflineSummaryPanelTests.TheSummaryListsDurationsCashStockSalesEffectsContractAndLimits`, `ContractsPanelTests.ADeliverTapAfterTheDeadlineIsRejectedWithTheExpiredReason` | Met with caveat: no player or device capture of a real expired-contract summary; the owner rates the offline popup "too much text, too little color" (`playtest-flo-20261009.md` note 12, F004) |
| US3 | 1 Activate consumes exactly one Item; effect and countdown visible | EM `RunItemTests.Activate_ConsumesExactlyOne_AndAddsTheEffectOverTheItemDuration`, `Effect_IsActiveUntilTheLastMillisecond_AndEndsAtTheExactInstant`; PM `ItemsPanelTests.ChoosingATargetAsksForAConfirmationAndConfirmingActivatesTheItemOnce`, `ActiveEffectsCountDownOncePerSecond`; active-effect rows seen in `art-direction-review.md` Integration | Met |
| US3 | 2 Invalid target or conflict: Item kept, reason shown | EM `RunItemTests.Activate_ATargetOutsideTheContract_IsItemTargetInvalid_AndKeepsTheItem`, `Activate_WhileTheSameConflictKeyRuns_IsEffectConflict_AndKeepsTheItem`; PM `ItemsPanelTests.AConflictingTargetIsShownDisabledWithItsReasonAndATapKeepsTheItem` | Met |
| US3 | 3 Research is immediate; rejected purchase changes nothing | EM `RunResearchTests.Purchase_AppliesTheEffectImmediately_WithARescaleKeepingTheFraction`, `Purchase_WithTooLittleRp_IsRpInsufficient_AtTheBoundaryOfTheCost`, `Purchase_AtTheMaximumLevel_IsLevelMax_WithoutSpendingRp`; PM `ResearchPanelTests`, `ResearchPanelScrollTests` (10 pass); PL journey buys Research | Met |
| US3 | 4 Prestige keeps unused Items, drops active boosts | EM `PrestigeTransitionTests.ActiveBoostsAreLost_ButTheUnusedItemsSurvive_AndTheBoostCanBeUsedAgainAtOnce`, `Promote_PublishesTheFreshRun_KeepsTheItemsIndustryAndIp_AndResetsEverythingElse` | Met |
| US4 | 1 Below 10M: sale disabled, missing value shown | EM `PrestigeTransitionTests.Threshold_OneDollarBelowIsRejectedAndTheThresholdItselfIsAccepted`; PM `PrestigePanelTests.BelowTheThresholdTheMissingValueShowsAndConfirmIsDisabled`; emulator "Ready to sell, 21.1M of 10.0M" (`android-emulator-evidence.md` prestige rotation) | Met |
| US4 | 2 Cancel changes nothing; confirmation publishes one coherent reset and IP after durable save | EM `PrestigeTransitionTests.Cancel_ChangesNothing_AtEveryStage`, `Confirm_BuildsTheCompleteCandidate_AndLeavesTheLiveRunUntouchedWhilePending`; PM `PrestigePanelTests.ConfirmingSellsTheFactoryThroughSavingAndStartsTheNextRun`, `DecliningTheConfirmationPrestigesNothing`; PL `RunJourneyPlayerTests` (durable prestige, award once) | Met with caveat: the prestige is proven by scripted journeys at a scaled threshold (`full-suite-evidence.md` T121-F2) and by the emulator confirmation rotation; no human played to the real $10M sale (`playtest-flo-20261009.md` stopped near $40,000) |
| US4 | 3 Interrupted save/promotion: old run no award, or new run exactly one award | EM `PrestigeTransitionTests.EveryProtocolStepFailingOnce_ThenRetry_EndsWithTheNewRunAndExactlyOneAward`, `EveryProtocolStepFailingOnce_ThenRestart_LoadsTheOldRunWithoutAwardOrTheNewRunWithOne`; PL `RunTransitionPlayerTests` (three cases); PM `PrestigePanelTests.AFailedDurableTransitionShowsTheMessageKeepsTheRunAndRetryCompletesIt` | Met |
| US4 | 4 Industry upgrade shows its benefit; equivalent milestone sooner under the reference policy | EM `RunIndustryTests.Purchase_MachineSpeed_AppliesNow_WithARescale`; `Balance/BalanceTraceTests` (SC-001 chain) and second run 4640 s against 6940 s (`balance-trace-r2.md` section 1); PL journey run 2 at 2340 s against 2380 s | Met with caveat: the faster second run is proven by the scripted policy only; the player journey gain is 1.7 percent (T121-F3); no human second run; the owner's feel judgment is the SC-004 basis (`content-approval.md`) |
| US5 | 1 At most 8 h credited, 6.4 h productive; real-time deadlines at their instants | EM `OfflineReconciliationTests.CapsCreditAtEightHoursAndProducesAtMostSixPointFourEffectiveHours`, `OfflineSegmentationTests.Absence_MapsEveryBoundary_AndEqualsTheUnsegmentedAndSegmentedAdvance`, `Boundary_AtTheCap_AndBeyond_MapToTheEndOfProduction`; emulator 8 h return 746 ms (`android-emulator-evidence.md` rerun) | Met |
| US5 | 2 Fitting V1 import maps IDs, stock, machines, progress; published once | EM `V1ImportTests.AcceptedFixture_ImportsExactlyOnce_WithMappedStockMachinesAndProportionalProgress`, `Import_NeverCreditsTheV1IntervalTwice_ARestartSelectsThePromotedV2`; PL `RunTransitionPlayerTests.TheV1ImportFromThePackagedFixtureSucceedsOnceAndTheV1DirectoryStaysByteIdentical` | Met |
| US5 | 3 Invalid or unrepresentable import: V1 bytes identical, nothing published, rule named | EM `V1ImportTests.FailureFixture_FailsWithItsRuleCode_LeavesV1ByteIdenticalAndPublishesNothing`, `W001ScaleFixture_ExceedsTheLevelOneSlots_AndFailsWithSlotsExceeded`, `UnmappedIdFixture_WithALongReturn_StillFailsWithIdUnsupported`; PM `StartupStatusPanelTests`; `security-devops-review.md` scenarios 1 and 2 Accept | Met |
| US5 | 4 Rotation without departure: no offline summary, no second reward | PM `RunLifecycleTests.ARotationWithoutDepartureCreatesNoOfflineSummaryAndNoSecondReward`, `OfflineSummaryPanelTests.ARealReturnShowsItsSummaryAndARotationWithoutDepartureShowsNone`; emulator rotation runs ("Cancelled by rotation", no Welcome back) | Met |
| US6 | 1 Canonical content resolves and drives runtime and traces | EM `RunContentCanonicalTests`, `RunCatalogTests.LiteralIronFixtureMatchesFr002`, `BalanceTraceTests`; PM `JsonContentPlayerTests`, `RunBootstrapTests`; blob `66f41fb` equals the approved revision | Met |
| US6 | 2 Invalid content reports entry, field and rule; no partial effect | EM `RunContentValidatorTests.RuleCodeIsReportedForItsViolation`, `EveryContractRuleCodeHasATestCase`, `MultipleViolationsAreReportedInDocumentOrder`; PM `RunBootstrapTests` (each rejection on the run path) | Met |

Counts: 22 scenarios. Met 19, Met with caveat 3 (US2.3, US4.2, US4.4), Not met 0, Unproven 0.

### FR/SC traceability

"Direct" means a named test or a review record proves the requirement. "Indirect" means the proof rests on a trace, a judgment or a negative (absence) argument. Code-structure items (boundaries, no economy rules in views) belong to the Architect verdict and are cited here only where the product requirement depends on them.

| ID(s) | Evidence | Strength |
|---|---|---|
| FR-001, FR-031 | `RunCatalogTests.LiteralIronFixtureMatchesFr002`; `RunAuthorityTests`; scope held in `docs/product/ideas.md` | Indirect for the negative part (no belts, adjacency, other families): no test asserts absence; Architect verdict covers it |
| FR-002 | `RunSimulatorTests.LiteralFr002Chain_*` (hand trace; Assembler bottleneck at 5/8); `content-record-r2.md` | Direct |
| FR-003, FR-004 | `RunSimulationEquivalenceTests.CaseK_PerStepConservation_*`, `CaseF_*`; `RunSimulatorTests.PartialBatch_*` | Direct |
| FR-005, SC-002 | `RunSimulationEquivalenceTests.CaseA` to `CaseK`, `OfflineSegmentationTests.Offline_EqualsTheOnlineSession_*`, `RandomFactories_*` | Direct |
| FR-006 | `RunCommandTests.ReorderPriority_*`; `PrioritiesPanelTests` | Direct |
| FR-007, FR-008 | `RunCommandTests.UpgradeStorage_AndFactory_*`, `Build_WhenTheFactoryLevelOpensMoreSlots_*`; approved values in `content-record-r2.md`, `content-approval.md` | Direct (values are the approved tuning, not the spec's initial targets) |
| FR-009 | `RunCommandTests.Remove_*`, `Replace_IsRemoveThenBuild_*`, `BuildThenRemove_AlwaysLowersFactoryValue_*`, `Upgrade_MidCycle_*` | Direct |
| FR-010 | `RunCatalogTests.CurvesIterateWithDeclaredRoundingAndBound`, `MachineTablesIncludeMilestonesInLevelOrder`; `RunContentValidatorTests.SpeedTableMustStrictlyIncreaseAndEveryMilestoneMustBeLive` | Direct |
| FR-011 | `InventoryRatesPanelTests` (net rate, price, store bar), `MachinePanelTests` (blockers, hints) | Direct. Caveat: the owner wants a Stats view and a better rate readout (playtest notes 11, 14; F004) |
| FR-012, FR-013 | `RunCommandTests.SetAutoSell_*`; `RunUnlockTests`; `balance-trace-r2.md` (first Gear 720 s) | Direct for selling and unlocks; the 10 to 15 minute Gear timing is trace only (the human never reached Gear) |
| FR-014, FR-015 | `RunContractTests` (deadline inclusive, one active, re-accept scaling); `RunContentValidatorTests.CashRewardMustStayBetweenMarketValueAndTheRatioBound` | Direct. Product caveat: reward ratios 1.67 and 1.75 leave no headroom (`content-approval.md` item 5, F005) |
| FR-016 | `RunResearchTests`, `ResearchPanelTests`; `RunContentValidatorTests.ResearchNeedsSomeContractThatPaysRp*` | Direct |
| FR-017, FR-018 | `RunItemTests.EveryTier_IsObtainedFromAContractReward_AndActivates`; `RunContentValidatorTests.EveryItemTierNeedsAnObtainableAndUsableDefinition`; `ItemsPanelTests` | Direct |
| FR-019 | `RunOverdriveTests` (30 s, five-minute cooldown, boundaries); `OverdriveControlTests` | Direct |
| FR-020, FR-021 | `PrestigeTransitionTests.Preview_AwardsTheTableIp`, `Preview_ReportsTheListsTheNextRunAndWhatIsLost`; `RunContentValidatorTests.PrestigeCurveMustBeSqrtWithPositiveParameters`; `PrestigePanelTests.EligibleShowsTheBreakdownTheIpPreviewAndBothListsWithConfirmEnabled` | Direct |
| FR-022 | `PrestigeTransitionTests.Promote_PublishesTheFreshRun_*`, `AnActiveUntimedContractIsDroppedWithoutRewardOrPenalty_*` | Direct |
| FR-023 | `RunContentValidatorTests.FirstIndustryCostMustBeAffordableAtTheThresholdIp`; `RunIndustryTests`; PL journey (one Industry purchase, run 2 sooner) | Direct for affordability; "measurably faster" is small in the player journey (1.7 percent) and relies on the trace (4640 s against 6940 s) |
| FR-024 | `PrestigeTransitionTests.EveryProtocolStepFailingOnce_*`; PL `RunTransitionPlayerTests` | Direct |
| FR-025, FR-026 | `OfflineReconciliationTests`, `OfflineSegmentationTests`, `OfflineReturnDurabilityTests`; `RunDesktopLifecycleTests` (6 pass, T250); `security-devops-review.md` scenario 6 | Direct |
| FR-027 | `SaveV2StoreTests`, `SaveV2MapperTests`, `SaveV2LifecycleTests`; `security-devops-review.md` (ACCEPT WITH ADVISORIES) | Direct |
| FR-028 | `V1ImportTests` (about 55 cases); PL `RunTransitionPlayerTests`; `security-devops-review.md` scenarios 1 to 3 | Direct |
| FR-029, FR-030 | `RunContentValidatorTests`, `RunContentReaderTests`, `RunContentSchemaConsistencyTests`, `RunContentAuthoringCheckTests` | Direct |
| FR-032 | `ui-ux-review.md` Final review (PASS WITH FINDINGS, N-1 to N-11, F-9 open); `RunKeyboardFocusTests` 51, `RunTextScaleTests` 150, `RunHudLayoutTests` 178 pass; emulator 7 mm check (F-10) | Direct with an open item: world slots are below 44 pt at fit-all and at the zoom minimum (F-9, no decision recorded) |
| FR-033 | `RunLifecycleTests`; emulator rotation runs and Prestige confirmation rotation (`android-emulator-evidence.md`); `RunDeviceProfileTests` | Direct, emulator-only; other screen classes by test |
| FR-034 | `art-direction-review.md` Integration (Art Director and Technical Artist PASS WITH FINDINGS, 78 of 78 keys resolve); T090 and T111 records | Direct, provisional placeholder art. Open: N3, C1, AD-1, AD-4, AD-6 |
| FR-035 | `RunCommandTests.*NearTheCashBound*`, `RunSimulatorTests.Overflow_*`, `RunContentValidatorTests.CurveOverflowIsReportedAtTheFirstValueAboveTenToTheEighteenth` | Direct |
| SC-001 | `BalanceTraceTests.Sc001Chain...FasterNextRunWithNoUnintendedChange`; PL `RunJourneyPlayerTests` | Direct (scripted; scaled threshold in the player) |
| SC-002 | see FR-005 | Direct |
| SC-003 | `balance-trace-r2.md` (Gear 720 s, eligibility 6940 s = 115.7 min); `playtest-flo-20261009.md` (partial); `content-approval.md` and spec Session 2026-10-09 (bands are orientation, owner feel decides) | Indirect by owner decision: no human reached Gear or prestige; the human was about 2.5 times behind the policy at minute 15; "1 million feels far away" |
| SC-004 | `balance-trace-r2.md` (second run 4640 s = 77.3 min); same owner decision | Indirect by owner decision: no human second run |
| SC-005 | `V1ImportTests`, `SaveV1PlayerFixtureParityTests`, PL `RunTransitionPlayerTests` | Direct |
| SC-006 | `full-suite-evidence.md` T120 (Edit 1497, Play 1475, 0 skipped), T121 (Edit 1499, Play 1480, player 11 of 11); emulator labeled emulator-only | Direct but on older commits; T133 must rerun on the accepted commit |
| SC-007 | `ui-ux-review.md` Final review; `RunHudLayoutTests`, `RunTextScaleTests`, per-panel "contained at every screen class" tests; emulator captures | Direct with caveats: F-9 (slots), N-9 (blocker below the fold at 200 percent text on small screens), emulator only, no physical device |
| SC-008 | `performance-review.md` (MISS on W003-B 8 h reconcile: 951.7 and 1017.6 ms against 500 ms; HUD path allocates once per second); `w003-measurements.md`; emulator W003-S 746 ms and 61.145 FPS | Direct evidence of a miss. Not met as written; depends on the pending owner decision A/B/C |

FR/SC without direct evidence: SC-003 and SC-004 (indirect by owner decision), FR-031 (negative scope, indirect), and the timing part of FR-013 (trace only). SC-008 has direct evidence, but it shows a miss, so it cannot be reported as passing.

### Open owner decisions

| Item | Source | Blocks acceptance? | Recommendation |
|---|---|---|---|
| SC-008 W003-B 8 h reconcile miss, options A (measure first), B (fund optimization), C (accept and re-baseline) | `performance-review.md` sections 6 and 7 | Blocks reporting SC-008 as passing; does not block feature acceptance if the decision is recorded. The review says SC-008 must not be reported passing in `final-acceptance.md` before the decision | Owner records a decision before T133. Product view: the realistic shipped-balance absence is far below the budget (W003-S 746 ms on the emulator, 320 ms on Windows; W003-R 175 ms); W003-B is a synthetic 2.76 M event worst case. Recommend C for this feature combined with A as a follow-up Issue (quiet-host and IL2CPP re-measure, optional emulator B): label SC-008 "met for the shipped workloads, W003-B miss accepted as a risk with a follow-up". Option 1 or 3 optimization goes to F005, whose content redesign changes the workloads |
| HUD path allocates about once per second (PR-2) | `performance-review.md` 4.2 | No | Accept option (a): amend the budget wording; measure on device only if a hitch appears |
| N-5: cold start after a short unclean exit opens the Welcome back summary at any length (the 60 s threshold of T246 covers lifecycle returns only) | `ui-ux-review.md` Final review | No | Record the decision. Recommend keeping "restarts always show the summary" for now (the credit is real, a restart is rare) and handle brevity in the F004 offline popup rework (playtest note 12). One line in `final-acceptance.md` |
| F-9: world slots below 44 pt at fit-all (minimum 29.8 pt) and at zoom minimum (32 pt, 40 pt pitch); badge text about 4.5 pt at Z24 | `ui-ux-review.md` Final review and shell gate; `tasks.md` line 41 | Does not block, but FR-032 and SC-007 state 44 pt for actionable controls, so the exemption must be written down | Product accepts the exemption: the slot is a world object whose state and actions are repeated in text and in 44 pt controls in the panel, and the shell starts at fit-all. Record it in `final-acceptance.md` (Art Director concurrence is in AD-3); do not raise the zoom minimum now |
| Item f / N-9: blocker section below the fold at 200 percent text on small screens | `ui-ux-review.md` Items e and f | No (the status line near the top already names the cause) | Defer to F004 (a "Waiting: see below" pointer or a reserved headline row) |
| AD-1: world background `#090C0E` against the Art Bible `#111827` | `art-direction-review.md` Integration | No (raises contrast) | Record an exception note now; decide in the F004 art refresh |
| AD-4: action buttons are text only; 27 of 49 UI icons and the 3 tier chips never rendered (TA-2) | `art-direction-review.md` Integration | No | Defer to F004 (UI overhaul). Record the intended consumer of each unused icon before F004 |
| AD-6 / N1: Iron Ore icon at 2.05:1 on the raised card (label beside it) | `art-direction-review.md` Integration | No | Defer to F004; accept for the placeholder art |
| C1 (WAIT and PAUS badges never observed), N3 (two-reviewer shuffled-sheet pass) | `art-direction-review.md` Integration | No for the placeholder art; N3 needs a second reviewer | Defer N3 and the tier chip pass to the F004 art refresh; C1 is covered by `SlotGridTests` rank assertions. Not a gate for FR-034 on provisional assets |
| AD-2, AD-3, AD-5, AD-7, AD-8, AD-9, TA-1, TA-3, N-1 to N-4, N-6 to N-8, N-10, N-11 | `art-direction-review.md`, `ui-ux-review.md` | No | Non-blocking polish; batch into F004. N-10 (no end-to-end test of a short return with an expired effect) could be a small follow-up test Issue |
| Security F-1 (safety checkpoint not rate-limited), F-2 (no adapter test for a failing safety write), F-3 (no timer autosave while the desktop window is unfocused) | `security-devops-review.md` | No. F-3 is a direct consequence of the owner's T250 decision, so the owner should acknowledge it | Acknowledge F-3 in `final-acceptance.md`; open one follow-up Issue for F-1, F-2 and a coarse timer autosave (candidate for F004 or F005) |
| Security F-4 (generation lock in-process only), F-5 (Android Auto Backup not verified), F-6 (discard overwrites damaged V2 with no copy) | `security-devops-review.md` | No | F-4 document; F-5 check at the first external Android release build; F-6 is an accepted owner decision, no action |
| OD-7 downgrade limitation, release-note text | `security-devops-review.md` scenario 3 | No, but T133 must carry it | Copy the release-note text into `final-acceptance.md`, as T131 and T133 require |
| Gameplay decisions of 2026-09-29 | `remaining-decisions.md` | No: all approved by the owner and recorded in spec Clarifications (removal uses the 50 percent refund) | None |
| Content approval items 1 to 7 (price-80 rounding artifact, contracts-off and returns bounds, later-progression bands, Ore-only dominance, contract ratios, trace noise, fit to policy r1) | `content-approval.md`, `balance-trace-r2.md` section 6 | No: accepted or deferred to F005 by the owner | Keep the gate pinning the failures; the shipped blob equals the approved `66f41fb` |
| Playtest was partial (about $40,000 Factory Value; no first prestige, no second run); owner notes 4 to 16 (sell command, auto-deliver, monotony, price scaling, Stats view, popup text, tree split, menu list, deliverable cue) | `playtest-flo-20261009.md`, `docs/product/ideas.md` | No: the owner accepted the partial record, and the F003 items T248 to T250 are done | The notes are F004/F005 backlog. Product risk to state plainly: the loop monotony and pacing concern (notes 5 to 9) is real player feedback that this feature does not address, and the first prestige has never been human-played. Claim no more than "the owner judged the feel good on the played part" |
| Remove tap ignored once after HOME and return; edge swipe near the Android back gesture | `ui-ux-review.md` handoff items (c), (d) | No | Not reproduced in three trials; keep as a low-risk note |

### Verdict

**ACCEPT WITH CONDITIONS.**

User value is delivered: all 22 acceptance scenarios across US1 to US6 are met or met with a stated caveat, none is unproven, every FR has a named test or review record, and 6 of 8 SC have direct evidence of a pass (SC-003 and SC-004 rest on the owner's decision; SC-008 shows a miss). The loop (build, sell, contract, Item, Research, prestige, Industry, faster next run) is proven end to end in the scripted trace and in the Windows player, V1 import and the retry-safe prestige are well covered, and the owner approved the exact shipped content (`66f41fb`) and judged the played part good. SC-003 and SC-004 are accepted on the owner's feel judgment as recorded in the spec, not on a human measurement.

Conditions:

1. **SC-008:** the owner records a decision on the W003-B miss (A, B or C) before T133, and `final-acceptance.md` reports SC-008 as "met for the shipped workloads, W003-B miss accepted or tracked", never as plainly passing. The PR-2 allocation wording is amended or the item is accepted.
2. **FR-032/SC-007:** record the F-9 exemption (world slots below 44 pt, status repeated in panel text) and the N-5 decision in `final-acceptance.md`.
3. **T133 labels:** SC-003 and SC-004 as owner judgment (the human never reached first Gear, first prestige or a second run); everything on Android labeled emulator-only; iOS and physical devices not claimed; the scripted prestige in the player uses a scaled threshold.
4. **T133 reruns** the full Edit/Play suites, the console check and `-StandaloneWindowsPlayer` on the accepted commit (the existing runs predate T246 to T250 and later merges), and copies the OD-7 release-note text and the open risks F-1 to F-6 (the owner acknowledges F-3).
5. The shipped content blob stays `66f41fb` (the gate pins it); any later change reopens T125 and T127.
6. Follow-up Issues are opened for: W003-B measurement or optimization (SC-008), the safety-checkpoint throttle, test and timer autosave (F-1 to F-3), the F004 art and UI items (AD-1, AD-4, AD-6, N3, C1, N-9, TA-2) and the F005 backlog from the playtest.

There is no Not met scenario and no hard blocker. If the owner declines to record the SC-008 decision (condition 1), SC-008 stays open and the feature closes with that criterion explicitly marked unmet.

## Software Architect verdict

### Scope and commit
Independent, read-only review of Feature 003 at `6fbdafa` (issue-212 worktree) for T132 / WU68. Deterministic evidence only (grep, asmdef inspection, git). No Unity, player, build or adb was run, and no lease was taken. Lens: boundaries, dependency direction, data ownership, contracts, failure modes, operability, evolution cost (Constitution IX). I did not take part in implementing Feature 003.

### Assembly and API boundary checks

| Check | Command / inspection | Result |
|---|---|---|
| Core has no engine reference | `Assets/IdleFactory/Core/IdleFactory.Core.asmdef` | `references: []`, `noEngineReferences: true`. Compiler-enforced, not just convention. |
| No Unity API in Core | `grep -rnE "UnityEngine\|UnityEditor\|MonoBehaviour\|ScriptableObject\|Debug\.Log\|\bApplication\.\|\bTime\.(time\|deltaTime\|unscaled\|realtime)" Assets/IdleFactory/Core --include=*.cs` | 0 hits (68 files: Application, Domain, Numerics, Simulation, Run/{Application,Content,Domain,Simulation}). |
| No wall clock / RNG / IO in simulation | `grep -rnE "System\.IO\|DateTime\.(Now\|UtcNow)\|new Random\|Stopwatch\|Environment\."` over Core | Only `using System.IO;` in `Core/Run/Application/RunPersistence.cs` (unused import; no file API used) and an `IOException` type test in `V1Import.cs`. Time arrives through the clock port (`DeviceUtcClock` lives in Unity). |
| Core does not reference Unity layer | `grep -rn "IdleFactory\.Unity" Assets/IdleFactory/Core` | 0 hits. |
| Dependency direction (asmdef) | Core -> nothing; Unity -> Core + Unity.InputSystem; Editor -> nothing; tests -> Core/Unity/Editor | Unity -> Application -> Domain only. A reverse edge is impossible by asmdef. |
| Legacy -> Run reverse edge | `grep -rn "IdleFactory\.Run" Core/Application Core/Domain Core/Simulation` | 0 hits. Legacy code does not know the run model. |
| Mutable static state in Core | regex for non-readonly, non-const `static` fields | 0 hits. `LifecyclePlatform` (Unity assembly) holds the only test-override static (`overrideIsMobile`), scoped via `IDisposable`. |
| Unity APIs only at the edge | Unity-assembly `Time.` / `Application.` hits | `Bootstrap/CoreGameFoundationBootstrap.cs`, `Bootstrap/RunLifecycleAdapter.cs` (injectable `readRealtimeSeconds`), `UI/Panels/InventoryRatesPanel.cs` and `UI/RunShellDocument.cs` (render throttling only), `Bootstrap/LifecyclePlatform.cs`. All presentation, lifecycle or infrastructure. |
| Run state mutation from Unity | `grep -rnE "\.(TryAdd\|TrySpend\|TrySell\|TryUnlock\|TryBuild\|TryUpgrade\|TryRemove\|TryDeliver\|TryReorder\|TrySetAutoSell\|TryRestore)"` over `Unity/` | Only `Persistence/SaveV2Mapper.cs` (state reconstruction on load, which is correct). No UI or presentation file mutates run state. |
| Simulator / catalog internals in views | `grep "RunSimulator\|ProductionSimulator\|SpeedComposer\|OfflineSegmenter\|new RunSession"` over UI, Presentation, Input | 0 hits. |

### Views and economy rules
- `Unity/UI/**` and `Unity/Presentation/**` consume `RunSession` snapshots and `RunCommands`. Affordability, unlock and eligibility flags come from the application layer (`type.Affordable`, `type.Unlocked`, `UnlockKind`, prestige preview). The disable decision in `BuildSheetPresenter` (`!type.Unlocked || !type.Affordable`) uses those flags, not its own comparison. Every command is re-validated by `RunCommands`.
- Display-only derivations in the view layer: `BuildSheetPresenter.CashShortfall(cost, cash)` and `ResearchPanel.cs:895` (`Math.Max(0, node.NextCost - data.Rp)`) compute "need N more". They gate nothing, but they are two copies of an arithmetic rule that could drift (F-A3).
- `RunShellPresenter`, `ContractsPanel`, `FactoryRuntimeHost` read `session.State.*` (inventory, overdrive, contracts, auto-sell) directly. `RunState` mutators are `public`, so "views never mutate" is enforced by convention and review only (F-A2). No violation exists today.
- Result for this check: no economy rules in views. Two cosmetic derivations noted.

### Contracts vs code
- `run-application.md`: all listed commands exist with matching names and result types (`Advance`, `Build`, `Upgrade`, `Remove(id, confirmed)`, `SetActive`, `ReorderPriority`, `SetAutoSell`, `UpgradeStorage/Factory`, the contract trio, `ActivateItem`, `PurchaseResearch`, `ActivateOverdrive`, `PurchaseIndustry`, `GetPrestigePreview`, `ConfirmPrestige`, `PromotePrestige`, `CancelPendingTransition`, `RetryPendingTransition`, and the persistence commands including `Autosave`, `ReloadAuthoritativeGeneration`, `RecoverPreviousCheckpoint`). All 24 stable rejection codes plus `transition.committed`, `persistence.failed`, `return.blocked`, `lifecycle.noop`, `checkpoint.none/invalid/version` and `offline.value-limit` are present as string constants in `RunReasons` / `RunPersistence`. `RunCommandResult<T>` is a struct as the contract and wiki say.
- Drift (documentation, minor): codes in code but not in the contract's stable list: `command.invalid` (`RunReasons.CommandInvalid`), `checkpoint.content` and `discard.unavailable` (`RunPersistence`). The `import.*` family is defined in `save-v2-and-v1-import.md`. Record the three in the contract (F-A4).
- The contract says `RunPersistence.CommitPendingTransition()` backs `PromotePrestige`; `RetryPendingTransition() => CommitPendingTransition()` confirms the idempotent-retry design. Cancel after a promotion attempt fails closed (`transition.committed` / `persistence.failed`), as specified.
- `gameplay-content-v2.md`: the dispatcher routes version 1 to the legacy loader (import catalog only) and version 2 to `RunContentReader -> RunContentValidator -> RunCatalogBuilder` in the Unity assembly, producing a plain-C# `RunCatalog` in Core. `RunAuthorityTests` pins the single gameplay-content input. Matches.
- `ui-surfaces.md`: one panel or presenter per surface exists under `Unity/UI/Panels` (build sheet, machine, inventory and rates, priorities, expansion, contracts, items, overdrive, research, prestige, industry, offline summary, startup status). Spot check only; behaviour is the UI/UX reviewer's lane.
- `save-v2-and-v1-import.md`: the store port `IRunCheckpointStore` (`ReadActive/ReadPrevious/Read/Promote/RecoverPrevious/NextSupersedeGeneration/SupersedeDamaged/ProbeFiles`) is implemented by `Unity/Persistence/JsonRunCheckpointStore` over `CheckpointGenerationFiles` (generation protocol and in-process `CheckpointOperationGates`). Core owns the protocol decisions (`RunPersistence`), Unity owns bytes and files. Correct boundary.

### Retirement
- Retired Feature 001 types: 0 source references for `FactorySession`, `PlacementCommands`, `ProductionCommands`, `PersistenceCommands`, `DevelopmentSessionFactory`, `PlacementInputController`, `FactoryViewRegistry`, `ApplicationLifecycleAdapter`, `MachineInspectorPresenter`, `PersistencePresenter` in Core, Unity or Editor.
- `RunAuthorityTests` (4 guards): one gameplay content input; bootstrap never names `JsonCheckpointStore` / `ICheckpointStore` (source and reflection); no retired GUID in assets; retired types absent. These are structural, not only textual, which I accept as adequate.
- Quarantine, as recorded in `wiki/pages/feature-001-retirement.md`: `IdleFactory.Domain`, `DefinitionCatalog`, `FactoryState`, `ProductionSimulator`, `OfflineReconciler`, the V1 loader, `SaveV1*` and `JsonCheckpointStore` remain only for import and V1 suites. The bootstrap composes `SaveV1CheckpointSource` solely as a legacy import source.
- Residual coupling (not a violation, an evolution cost): `Core/Run/Application/{IRunCheckpointStore,RunPersistence,V1Import}.cs` import `IdleFactory.Domain` (`CheckpointId`, `DefinitionCatalog`, legacy `CommandResult`/`CommandOutcome`), and `GridWorldMapper` in Presentation uses legacy `IdleFactory.Domain` geometry. The run model still depends on the quarantined namespace for identifiers and results (F-A5).

### Positions on PR-1 / F-1 / F-4
- **PR-1 (W003-B 8 h reconcile, about 1 s against 500 ms).** I agree with the review's order: Option 0 (measure, profile, IL2CPP and emulator) first, with no code change. Option 1 (index-based hot loop inside `RunSimulator`/`SpeedComposer`/`GlobalInventory`) is architecturally acceptable: it stays in plain-C# Core, preserves per-event semantics and the `Last*` counters, and is verified by the existing equivalence suites. Condition: `GlobalInventory` is shared with commands, so the fast path must be an additional index-based API, not a change to command-path semantics. **Option 3 (steady-state jump) is not approved by this verdict.** It adds a second semantic path whose exactness rests on a guard set, so approval requires a written design (guard list, fall-back rule, equivalence argument against the per-event model, differential test plan with fixed seeds) reviewed by me before any code, as R04 and the performance review itself state. Option 2 alone is not worth its complexity. A "restoring" state or off-thread reconcile (Option 4 variant) crosses the `RunPersistence` cold-start and staged-commit boundary and the single-threaded `RunSession` ownership. I would reject an off-thread reconcile (the session has no thread-safety contract) and accept only a visible, blocking "restoring" state on the main thread. For the Feature 003 gate, PR-1 is a performance acceptance question for the owner (accept and re-baseline, or fund Option 0/1), not an architecture defect. The architecture does not block acceptance with the miss recorded as an accepted risk.
- **F-1 (safety checkpoint not rate-limited).** Accept as advisory. The cost is a bounded synchronous promotion per focus-loss edge, edge-detected in `RunLifecycleAdapter.NotifyDesktopFocusChanged`. If throttled later, put the minimum interval in `RunPersistence.Autosave` (Core, injected clock), not in the adapter, so the rule is unit-testable and shared with any future timer autosave. Do not throttle in Unity code.
- **F-4 (in-process generation lock only).** Accept and document "one instance per profile" for this feature. A cross-process guard (OS file lock or lock file in `persistentDataPath`) belongs in `CheckpointGenerationFiles` / `CheckpointOperationGates` and must be designed with the V2 manifest sequence. Torn results are already caught by manifest and readback verification, so the failure mode is lost progress, not corruption. Not blocking.
- Related, from the security record: F-2 (no test where `prepareForSafetyCheckpoint` throws) is real; `RunLifecycleAdapter.WriteSafetyCheckpoint` invokes the delegate without a guard inside a Unity callback (F-A1).

### Evolution cost
Seams that are ready:
- Content-driven catalog (`RunCatalog` plus validator and builder): new machines, resources, recipes, contracts, research and Industry upgrades are data, and `presentationKey` already decouples art from rules. This is the right seam for F005 Economy & Contracts.
- Single mutation API (`RunCommands` + `RunCommandResult<T>` with stable reason codes) and revisioned read models: F004 UI/UX and graphics can replace the UI Toolkit shell without touching rules.
- Persistence port (`IRunCheckpointStore`) plus the content and save version dispatch: a Save V3 or cloud store is an adapter plus a mapper, with the exactly-once keys (`reconciledFromCheckpointId`, `previousRunId`) kept in Core.
- `LifecyclePlatform` seam isolates desktop and mobile focus semantics; the injectable realtime reader keeps the adapter testable.

Coupling that will hurt:
- `RunPersistence.cs` is 1,919 lines in one class (cold start, reconcile, prestige promotion, discard, recovery). F005 will extend offline and contract settlement through it. Split by responsibility (start-up and recovery, offline return, prestige transition) before F005 adds branches.
- `RunSnapshots.cs` (1,023 lines) and `RunSession.cs` (581) concentrate every read model; F005 contract changes touch all three. Prefer per-surface snapshot builders behind the existing types.
- Public `RunState.Try*` mutators and public `RunSession.State` let any assembly mutate without settling or a revision bump. Make mutators `internal` (with `InternalsVisibleTo` for the mapper and tests) or expose read-only views before more UI code arrives.
- The run model still imports legacy `IdleFactory.Domain` identifiers and `CommandResult`; F005 or Save V3 will want `CheckpointId` and results inside the Run namespace so legacy Domain can be deleted when V1 import support ends.
- Presentation glue is large for F004 (`RunShellDocument.cs` about 1,900 lines, `CoreGameFoundationBootstrap.cs` about 1,250); plan a split along panel and composition boundaries before a visual overhaul.
- Rejection copy lives in `RunRejectionCopy` in the UI assembly, keyed by stable codes. That is the right shape; localisation (not planned) would need the table moved to content.

### Findings

| ID | Severity | Path | Evidence |
|---|---|---|---|
| F-A1 | Low | `Assets/IdleFactory/Unity/Bootstrap/RunLifecycleAdapter.cs` (`WriteSafetyCheckpoint`, about line 259) | `prepareForSafetyCheckpoint?.Invoke()` is unguarded inside a Unity focus callback; a throwing host flush would escape. No test drives the failing path (security F-2). Fix with try/catch that records a persistence-status failure, and add a test. |
| F-A2 | Low (maintainability) | `Assets/IdleFactory/Core/Run/Domain/RunState.cs`, `Assets/IdleFactory/Core/Run/Application/RunSession.cs:94` | `RunSession.State` and all `RunState.Try*` mutators are public; "only RunCommands mutates" is by convention. Grep shows no violation (only `SaveV2Mapper` restores state). Consider `internal` mutators. |
| F-A3 | Info | `Assets/IdleFactory/Unity/UI/Panels/BuildSheetPresenter.cs:75-80`, `Assets/IdleFactory/Unity/UI/Panels/ResearchPanel.cs:895` | Shortfall ("Need N more") is computed in views from cost and balance. Display only; better supplied by the snapshot. |
| F-A4 | Low (doc) | `specs/003-first-factory-prestige/contracts/run-application.md` | `command.invalid`, `checkpoint.content`, `discard.unavailable` exist in `RunReasons` / `RunPersistence` but not in the stable-code list. Update the contract. |
| F-A5 | Info | `Assets/IdleFactory/Core/Run/Application/IRunCheckpointStore.cs`, `RunPersistence.cs`, `V1Import.cs` | Run layer depends on legacy `IdleFactory.Domain` (`CheckpointId`, `CommandResult`, `DefinitionCatalog`). Documented quarantine; plan its removal. |
| F-A6 | Info | `Assets/IdleFactory/Core/Run/Application/RunPersistence.cs:2` | Unused `using System.IO;` in a no-engine, no-IO assembly. Harmless; remove so it does not invite file access. |
| F-A7 | Advisory | `Assets/IdleFactory/Core/Run/Application/RunPersistence.cs` (1,919 lines) | Single-class concentration; evolution cost for F005. |
| F-A8 | Advisory | `specs/003-first-factory-prestige/reviews/performance-review.md` (PR-1) | Option 3 requires an Architect-approved design before implementation; not given here. |

No Critical, High or Medium finding. No boundary, dependency-direction or data-ownership violation was found.

### Verdict
**ACCEPT WITH CONDITIONS.** The conditions are non-blocking for T132 and should be recorded as open risks or follow-ups in `reviews/final-acceptance.md`:
1. Record PR-1 as an owner decision (Option 0 first; Option 1 acceptable under the stated condition; Option 3 only after an Architect-reviewed design). The architecture does not require a fix before acceptance.
2. Carry security F-1 and F-4 as documented advisories ("one instance per profile"; throttle later in `RunPersistence.Autosave`), and fix or schedule F-A1 together with the missing failing-safety-checkpoint test (security F-2).
3. Add the three missing reason codes to the contract (F-A4).
4. Schedule, before F005, the split of `RunPersistence` / `RunSnapshots` and the tightening of `RunState` mutator visibility (F-A2, F-A7).

Blocking items: none.

## Game Systems / Economy verdict

### Scope and commit

- Role: Game Systems / Economy Designer, independent verdict lens (Medium tier, read-only). Commit `6fbdafa` (origin/main), read-only checkout. No Unity run, no player, no build, no adb. I did not run any test, so "Enforced" below means the enforcing test exists and asserts the invariant, not that I saw it pass. Green runs are T133's evidence (`specs/003-first-factory-prestige/reviews/full-suite-evidence.md`).
- Read: `reviews/content-approval.md`, `balance-trace-r2.md` (sections 1 to 5), `playtest-flo-20261009.md`, `security-devops-review.md` (S1-6, T250 rows), the shipped `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2.csv`, the Core run code (`FactoryValuation`, `PrestigeTransition`, `RunCommands.*`, `RunSession`), the lifecycle adapter, and the test class and method lists. Not re-read in full: `research.md`, `data-model.md`, the contracts and the wiki pages; economy rules were checked against code and tests instead.
- Offline arithmetic (scratchpad, not committed): IP curve, curve tables, caps, and an independent fluid model of an Ore-only player.

### Invariants

Test files are under `Assets/IdleFactory/Tests/EditMode/` unless stated.

| # | Invariant | Enforcing test (file) | Status |
|---|---|---|---|
| 1 | Quantities stay in `[0, 10^18]`; add and multiply are checked; overflow rejects the whole command or advance atomically | `ExactBoundIsTenToTheEighteenth`, `AddRejectsOutOfRangeOperandsIncludingInt64Extremes`, `MultiplyEdges`, `SumIsAtomicAtTheBound`, `MulDivUsesA128BitIntermediate` (`CheckedMathTests.cs`); `EveryCommand_IsAtomic_AtEveryHeadroomNearTheCashBound`, `Remove_WhoseRefundWouldOverflowCash_Rejects_AndKeepsTheMachine` (`RunCommandTests.cs`); `Deliver_RejectsAnOverflowingCashReward_Atomically` (`RunContractTests.cs`); `AnOverflowingAdvance_ThatWouldHaveUnlocked_RejectsAtomically` (`RunUnlockTests.cs`); `ValueLimit_ForfeitsTheCredit_KeepsTheState_AdvancesTheDeparture_AndReportsTheStaticCode` (`OfflineReturnDurabilityTests.cs`) | Enforced |
| 2 | Factory Value = Cash + stock at the effective sale price + Cash actually paid for standing machines + storage and factory investment; nothing counted twice | `Breakdown_AddsCashStockMachinesAndBothExpansions`, `StockValue_EqualsTheIncomeOfARealSale`, `RemovedMachineRpResearchAndItemsAddNothing`, `Overflow_IsReportedNotWrapped` (`FactoryValuationTests.cs`) | Enforced |
| 3 | IP = `floor(10 x sqrt(FV / 10^7))`, integer-exact; 10 at $10M, 20 at $40M, 30 at $90M, 100 at $1B | `Ip_IsTheFlooredSquareRootOfTheScaledValue` (`FactoryValuationTests.cs`); `Preview_AwardsTheTableIp`, `Threshold_OneDollarBelowIsRejectedAndTheThresholdItselfIsAccepted` (`PrestigeTransitionTests.cs`). I recomputed the table and the trace's `ip_preview` column and all match (9 at $9,999,999; 10 at $10,000,000; 6 at $4,287,402; 3 at $1,120,003; 1 at $109,261) | Enforced |
| 4 | Prestige reset and keep rules: IP, Industry levels and unspent Items keep; cash, stock, machines, priorities, auto-sell, expansion, contracts (counts and `LastExpired`), Research, RP, effects, Overdrive and unlocks reset | `Promote_PublishesTheFreshRun_KeepsTheItemsIndustryAndIp_AndResetsEverythingElse`, `ActiveBoostsAreLost_ButTheUnusedItemsSurvive_AndTheBoostCanBeUsedAgainAtOnce`, `AnExpiredContractOutcome_DoesNotSurviveThePrestige`, `Preview_ReportsTheListsTheNextRunAndWhatIsLost` (`PrestigeTransitionTests.cs`) | Enforced |
| 5 | Prestige commit is idempotent and awards IP exactly once, also through crashes (key: `Run.PreviousRunId`) | `ASecondConfirmWhilePending_IsRejectedAsTransitionPending_AndAwardsNothingTwice`, `EveryProtocolStepFailingOnce_ThenRetry_EndsWithTheNewRunAndExactlyOneAward`, `EveryProtocolStepFailingOnce_ThenRestart_LoadsTheOldRunWithoutAwardOrTheNewRunWithOne`, `ARetryAfterAVisiblePromotion_PublishesWithoutASecondAward`, `ARestartAfterAVisiblePromotion_ThroughTheRealStore_LoadsTheNewRunWithExactlyOneAward`, `ASecondPrestige_RunTwoToThree_IsNotMistakenForAnAlreadyDurableTransition` (`PrestigeTransitionTests.cs`) | Enforced |
| 6 | Eligibility re-checked at the command instant; the award uses the values at confirmation | `Eligibility_IsRecheckedAtTheCommandInstant`, `Confirm_BuildsTheCompleteCandidate_AndLeavesTheLiveRunUntouchedWhilePending` (`PrestigeTransitionTests.cs`) | Enforced |
| 7 | Offline credit: 80 % efficiency (4/5, policy revision 1), cap 8 h of absence so at most 6.4 effective hours, rational floor, exactly once | `UsesPolicyRevisionAndFloorsTheRationalEffectiveTime`, `CapsCreditAtEightHoursAndProducesAtMostSixPointFourEffectiveHours` (`OfflineReconciliationTests.cs`); `E_IsFloorOfMinMaxTimesFourFifths`, `ReturnAtTheCap_AndJustPastIt`, `Production_AdvancesByExactlyE_OfTheReturn` (`OfflineSegmentationTests.cs`). Content check: `maxCreditedTicks` 288,000,000,000 at 10^7 ticks per second is exactly 8.0 h | Enforced |
| 8 | Offline equals online over the same effective ticks (no offline-only bonus); segmentation conserves stock | `Offline_EqualsTheOnlineSession_OverTheSameEffectiveTicks`, `Offline_CapacityContention_EqualsOnline_QA6`, `RandomFactories_TwelveHourAbsence_SegmenterEqualsOracle_AndTheSummaryConservesStock` (`OfflineSegmentationTests.cs`) | Enforced |
| 9 | Exactly-once offline credit across retries, failure at every backend call, and restarts | `Return_CreditsTheSegmenterResultOnce_AdvancesTheDeparture_AndLinksTheSource`, `Restart_AfterAReturn_CreditsOnlyTheNewAbsence_NeverTheOldOneAgain`, `ReturnFailingAtEveryBackendCall_ThenRetry_CreditsExactlyOnce`, `ReturnFailingAtEveryBackendCall_ThenRestart_CreditsExactlyOnce` (`OfflineReturnDurabilityTests.cs`); `AReturnThatFindsTheVisiblePromotion_CreditsTheNewRunFromTheConfirmedInstant_ExactlyOnce` (`PrestigeTransitionTests.cs`) | Enforced |
| 10 | Backward, missing or unrepresentable clock gives zero credit; the timeline never moves back; a future departure is never rewritten earlier | `BackwardClock_GrantsZeroCredit_AndChangesNothing`, `BackwardClock_AfterAnExpiredReturn_NeverUnExpiresAnything`, `MissingClock_GrantsZeroCredit_AndChangesNothing`, `UnrepresentableClock_GrantsZeroCredit_AndChangesNothing` (`OfflineSegmentationTests.cs`); `MissingTimeCannotPromoteAndBackwardsTimeKeepsTheTimestampNondecreasing` (`OfflineReconciliationTests.cs`); `ADepartureInTheFuture_CreditsNothing_AndIsNeverRewrittenEarlier` (`OfflineReturnDurabilityTests.cs`) | Enforced |
| 11 | Remove refunds `floor(invested x 500,000 / 10^6)` of Cash actually paid (discounts included), so build-and-remove always loses at least half; Replace is refund first, then the new cost | `Remove_RefundsFloorOfInvestedTimesRefundPpm_FreesTheSlot_AndClosesThePriority`, `BuildThenRemove_AlwaysLowersFactoryValue_AlsoUnderAnUpgradeCostDiscount`, `Replace_IsRemoveThenBuild_RefundFirst_ThenTheNewCost` (`RunCommandTests.cs`); `BuildThenRemove_AlwaysLowersTheValue_AlsoUnderTheDiscountsAndTheHighestRefund` (`FactoryValuationTests.cs`) | Enforced |
| 12 | Build, upgrade and expansion discounts round up (a price never reaches 0); stacked cost factor stays at or above 0.5 | `Discount_AppliesToBuildUpgradeAndExpansion_WithRoundingUp` (`RunCommandTests.cs`); `Purchase_UpgradeCost_LowersTheNextBuild` (`RunIndustryTests.cs`); `Purchase_UpgradeCostResearch_LowersTheNextBuildCost` (`RunResearchTests.cs`); the ECO-3 cap check in `RunContentCanonicalTests.cs` (around line 267) and the trace bands. I recomputed the shipped worst stack: Toolmaker 0.639 x Tool Discount 0.95^3 = 0.548 | Enforced (the cap as content validation plus trace band, see note b) |
| 13 | Contracts: at most one active; accept fixes the quantity at the completion count; deliver consumes exactly the required units and pays every reward once; deadline inclusive; expiry pays nothing and does not advance the index | `Accept_RejectsAnActiveContract_AnUnknownOrLockedOffer_AndABlockedLifecycle`, `Deliver_ConsumesExactlyTheRequiredUnits_AndGrantsEveryRewardOnce`, `Deliver_ARepeatDeliveryCannotGrantTwice`, `Deliver_ExactlyAtTheDeadlineSucceeds`, `Deliver_OneMillisecondAfterTheDeadline_IsExpired_AndGrantsNothing`, `Expiry_Online_AdvancesNothingGrantsNothing_AndKeepsTheOutcome` (`RunContractTests.cs`); `ContractDeadline_IsInclusive_AlsoOffline` (`OfflineSegmentationTests.cs`) | Enforced |
| 14 | Abandon: untimed only, no reward, no penalty, completion count unchanged | `Abandon_ClearsAnUntimedContract_WithNoRewardNoPenaltyAndTheCountUnchanged`, `Abandon_RejectsWithoutAContract_AndATimedContract` (`RunContractTests.cs`) | Enforced |
| 15 | Reward and quantity scale with the completion count of the current run; cash reward to market value within `[1.0, maxRewardRatio 2.0]` | `Deliver_ScalesTheQuantityAndTheRewardsWithTheCompletionCount_OfTheCurrentRun` (`RunContractTests.cs`); validator bound `maxRewardRatioPpm >= 10^6` (`RunContentSchemaConsistencyTests.cs`, `RunContentCanonicalTests.cs` line 486); realised ratio asserted in `Balance/BalanceTraceTests.cs` line 259. Shipped ratios recomputed: Gear 4000/2400 = 1.67, Plate 1400/800 = 1.75, Bar 510/300 = 1.70 | Enforced |
| 16 | Items: one unit consumed per activation, effect lasts exactly the duration, conflict key `(kind, target)` keeps the Item, every tier is obtainable from a contract reward | `Activate_ConsumesExactlyOne_AndAddsTheEffectOverTheItemDuration`, `Activate_WhileTheSameConflictKeyRuns_IsEffectConflict_AndKeepsTheItem`, `Effect_IsActiveUntilTheLastMillisecond_AndEndsAtTheExactInstant`, `EveryTier_IsObtainedFromAContractReward_AndActivates` (`RunItemTests.cs`) | Enforced |
| 17 | Research and Industry effects apply as authored (per level, compounded, prerequisites, max level, run-scoped immediately, start effects next run) | `Purchase_AppliesTheValuePerLevel_InCompoundedSteps`, `Purchase_WithAMissingPrerequisite_IsResearchPrerequisite` (`RunResearchTests.cs`); `Purchase_MachineSpeed_AppliesNow_WithARescale`, `Purchase_StartingEffects_OnlyChangeThePermanentLevel_AndExposeTheNextRun`, `Purchase_AtTheMaximumLevel_IsLevelMax` (`RunIndustryTests.cs`) | Enforced |
| 18 | Overdrive: `cooldownUntil = start + duration + cooldown`, doubles speed only inside the window | `Activate_SetsActiveUntilAndCooldownUntil_FromTheTimeline`, `Overdrive_ProducesDoubleOverItsWindow`, `Activate_AtTheExactEndOfTheBoost_IsCoolingDown_UntilTheExactCooldownInstant` (`RunOverdriveTests.cs`); reset on prestige is in the invariant 4 tests | Enforced |
| 19 | SpeedComposer stacking: fixed fold order with floor at each step, combined speed clamped to `maxCombinedSpeedPpm`, output factor clamped to 4; speed beyond the 0.1 s event bound becomes a batch (OD-6); partial batches limited by inputs and capacity (OD-9) | `ComposeFoldsInFixedOrderWithFloorAtEachStep`, `ItemFactorsFoldInTheGivenOrderSoTheItemIdOrderMatters`, `ComposeClampsToMaxCombinedSpeedDefensivelyAndRejectsOverflow`, `BatchFactorIsOneUpToTheEventBoundAndGrowsBeyondIt`, `BatchRateEqualsTheFullMultiplierWithinOneTickPerCycle`, `PartialBatchTakesTheMinimumOfBatchInputsAndCapacity`, `OutputFactorClampsToTheContentMaximum` (`SpeedComposerTests.cs`); `Effects_OfDifferentCategories_AndOverdrive_ComposeMultiplicatively`, `SpeedComposition_ClampsToMaxCombinedSpeed` (`RunItemTests.cs`) | Enforced |
| 20 | Unlock conditions: monotone, settled at one instant, split-independent, equal online and offline, only the current run's completions count | `TheUnlockedSet_OnlyGrows_EvenWhenTheConditionStopsHolding`, `ResourceProduced_CountsOnlyCompletionsOfTheCurrentRun`, `EverySplit_AroundTheUnlockInstant_GivesTheSameStateAndTheSameInstant`, `OfflineSegmentation_UnlocksAtTheSameSimulatedInstant_AsTheOnlineAdvance`, `UnreachableConditions_StayLocked_WithoutAnInventedRule` (`RunUnlockTests.cs`) | Enforced |
| 21 | Auto-sell: sells at the folded sale price, only unlocked resources, atomic near the Cash bound; a contract needs its stock, so auto-sell must be off for that resource | `SetAutoSell_ChangesOnlyTheSetting_ThenSellsTheStockAtOnce`, `SetAutoSell_RejectsALockedOrUnknownResource`, `SetAutoSell_NearTheCashBound_RejectsBeforeMutating_AndAcceptsTheExactFit` (`RunCommandTests.cs`); `AutoSell_SellsTheContractStockAtOnce_SoDeliveryNeedsItSwitchedOff`, `AutoSell_OnDuringDelivery_TheSettleSellsOnlyWhatTheContractLeft` (`RunContractTests.cs`); `StockValue_EqualsTheIncomeOfARealSale` (`FactoryValuationTests.cs`) | Enforced |
| 22 | Desktop focus-loss safety checkpoint does not stack with offline credit: no departure on focus loss, the run stays live, stored departure is the maximum of clock, timeline and prior departure, an unclean exit credits from the last checkpoint once | PlayMode `Assets/IdleFactory/Tests/PlayMode/RunDesktopLifecycleTests.cs`: `DesktopFocusLossKeepsTheRunLiveAndRecordsNoDeparture`, `DesktopFocusLossWritesOnePlainSafetyCheckpointWithoutDepartureSemantics`, `AnUncleanDesktopExitCreditsFromTheLastSafetyCheckpointOnce`, `DesktopRegularShutdownRecordsTheDepartureAndTheNextStartCreditsTheIntervalOnce`; design review row T250-7 in `reviews/security-devops-review.md` | Enforced (PlayMode only; not run by me) |
| 23 | Formulas live in Core and content, not in views | Static read below; the Architect verdict owns the assembly-boundary check | Confirmed by reading |
| 24 | Pacing bands and the shipped content pin | `Balance/BalanceTraceTests.cs` byte-compares `Fixtures/Balance/trace-r2.csv` and asserts the ECO-2 and ECO-3 bands; blob identity checked below | Enforced for revision 2, as a point sample |
| 25 | The sensitivity gate pins exactly three recorded misses (`var.contracts-off`, `var.price-80`, `var.returns`) | `RecordedSensitivityDeviations` in `Balance/BalanceTraceTests.cs` | Enforced; owner-accepted deviations |

No invariant in the requested list is untested. Three are proven more coarsely than I would like:

- a) Invariant 22 lives in PlayMode with a platform seam, not on a real window manager.
- b) Invariant 12's "stack never below 0.5" is content validation plus a trace band, not a property test over every purchasable combination; I verified the reachable maximum by hand (0.548 for machines).
- c) Laptop sleep while the process stays alive is not covered by any test in my sources. The active-time source is a monotonic stopwatch in `Assets/IdleFactory/Unity/Bootstrap/FactoryRuntimeHost.cs`, so whether sleep is credited at 100 % or 0 % depends on the OS counter. It can only be less generous than offline, so this is a note for the Security and Architect lenses, not an economy blocker.

### Exploit review

| Attempt | Result | Why |
|---|---|---|
| Build, remove, repeat to mint Cash | Closed | Refund is 50 % of Cash actually paid, floored; each cycle loses at least half. A discounted price is at least 1 by round-up, and the refund never exceeds what was paid. Factory Value falls on every cycle (invariant 11). |
| Build under a temporary discount, remove later | Closed | Refund keys on Cash paid, not the current price. |
| Prestige timing: spike Factory Value just before confirming | Closed | Cash becomes invested Cash 1:1, stock is valued exactly as a sale would be, and RP, Research, spent Items and unpaid rewards are excluded. Nothing raises Factory Value above the Cash put in apart from production. The award is computed at the confirmation instant and eligibility is re-checked there. |
| Prestige repeatedly for IP | Bounded | Each run needs $10M Factory Value from a start of 37 plus 300 per Seed Capital level; IP grows with the square root, so waiting pays (4x the value gives 2x the IP). |
| Double award by retry, crash or restart | Closed | Idempotency key and the fail-at-every-step tests (invariant 5). |
| Contract abandon cycling | Closed | Abandon changes neither stock nor the completion index and pays nothing. Item rewards key on the completion index of a delivery, so abandon and re-accept cannot farm them. Expiry does not advance the index. |
| Contracts versus selling (reward is 1.67 to 1.75 times market value) | Balance question, not an exploit | Not free value, since the goods must be produced. Delivering is strictly better than selling and has no price decay, so "deliver forever, never sell" is dominant, as the owner reported (playtest notes 6 and 7). Owner-accepted and assigned to F005. |
| Ore-only dominance (skip the chain) | Balance finding, accepted for run 1 | See the pacing section; my coarse model suggests the gap widens in later runs (E-2). |
| Focus-loss safety checkpoint credited as offline time | Closed | No departure is recorded on desktop focus loss, so the live run is not double-credited; an unclean exit credits from the last checkpoint once at the same 80 % efficiency (invariants 9, 22). Mobile still records a departure. |
| Clock moved backward | Closed | Zero credit, timeline never moves back, a ratcheted future departure never yields extra credit (T250-7). |
| Clock moved forward, repeatedly | Accepted local risk | Each return is bounded at 8 h x 80 % = 6.4 effective hours, but there is no cumulative bound, so a player who keeps jumping the clock can fast-forward without limit. This is the documented S1-6 decision (offline-first, no server). |
| Item stacking beyond intent | Closed | Conflict key per `(kind, target)`, combined speed clamped, output factor clamped to 4, validated in content. |
| Overflow wrapping into small or negative values | Closed | Checked arithmetic with 128-bit intermediates and the 10^18 bound; overflow rejects atomically (invariant 1). |

### Content and pacing check

**Content identity.** `git hash-object Assets/IdleFactory/Content/Iron/iron-factory-content.json` returns `66f41fb9bef01326680b5eca4c0dec35837839e4`, which starts with `66f41fb`, the blob approved in `reviews/content-approval.md` and played in `reviews/playtest-flo-20261009.md`. `contentRevision` is 2. The file's last commit is `9e96cf9` (T125), so no later commit on `6fbdafa` touched it. The shipped content equals the approved revision.

**Hand-checked content properties (recomputed from the JSON).**

| Property | Value | Check |
|---|---|---|
| Starting Cash vs Miner base | 37 vs 30 | holds the gate `startingCash >= Miner base cost` (trace record finding 3) |
| Chain value per step | Ore $1, Bar $3, Plate $8, Gear $24 | each step adds value (2 Ore -> Bar +$1, 2 Bar -> Plate +$2, 2 Plate -> Gear +$8); integer prices explain `var.price-80` |
| First Industry purchase | Calibration 1+2+3 = 6 IP, Market Contacts 1+2 = 3 IP, total 9 of the 10 IP awarded at 10,019,139 | matches the trace (`industry_ip_spent` 9, 5 purchases) |
| ECO-3 caps with all purchases | speed 1.22^3 = 1.816 (cap 2), price 1.19^2 = 1.416 (cap 2), cost 0.639 x 0.857 = 0.548 (floor 0.5) | inside the caps |
| Seed Capital IP ladder | 10, 15, 23, 34 for +$300 each | matches the trace (run 3 spends 10, run 4 spends 15) |
| Offline policy | 4/5, 288e9 ticks = 8.0 h | matches invariant 7 |
| Contract reward ratio | Gear 1.67, Plate 1.75, Bar 1.70 | below the 2.0 bound; record item 5 says there is no headroom, and I agree |

**Independent pacing recomputation.** I cannot rerun the scripted policy without Unity, so I built a coarse independent fluid model: Ore only, auto-sold at $1, greedy best payback over Miner build, Miner upgrade and factory slot expansion; speed `1.08^(L-1)` times the 2/2/3/5 milestones; price `ceil(30 x 1.15^(L-1))`; factory expansion costs from the JSON; no storage cap, no contracts.

| Case | My model | Repository trace | Reading |
|---|---|---|---|
| Ore-only to $10M Factory Value, run 1 | 5,986 s | `probe.ore-only` eligibility 6,020 s | Agree within 0.6 %. This independently corroborates the cost curve, milestone scaling and the trace's economics. |
| Chain, run 1 (SC-003 target "near 120 min") | not modelled | 6,940 s = 115.7 min | Inside the 110 to 130 min band; the chain is 13 to 15 % slower than Ore only (record section 2). |
| First Gear | lower bound: 2,220 for factory levels 2 and 3 plus about 108 for the Press, Smelter and Assembler, so about $2,330, an average of $3.2 per second over 720 s, which 3 Miners at level 11 to 16 supply | 720 s = 12 min | Plausible; inside the 600 to 900 s band. |
| First Industry purchase | 10 IP only at the first prestige | one burst of 5 purchases at about 116 min | The trace shows IP preview 1 at minute 30 and 3 at minute 60, but IP is awarded only at prestige, so the first Industry purchase is at the first prestige. No earlier IP source exists. |
| Run 2 (SC-004 "70 to 80 min"), chain | not modelled | 4,640 s = 77.3 min | Inside the band. |
| Run 2, Ore-only (x1.816 speed, x1.416 price) | 2,331 s, about 39 min | not in the repository | See E-2. |
| Run 3, Ore-only (plus Toolmaker 0.639 cost, +$300 start) | 1,615 s, about 27 min | not in the repository | See E-2. |

**SC-003 and SC-004 bands.** First Gear 12.0 min, first prestige 115.7 min and second run 77.3 min are all inside the bands. The record's caveat stands: a single-value change of 4 to 20 % moves the first prestige by up to 6 % and the second run by up to 9 % (record sections 1 and 6), so these are point samples of one scripted player. The spec (Session 2026-10-09) already calls the bands orientation, not thresholds, and the owner approved on feel.

**Owner playtest comparison.** The owner had about $5,200 Factory Value at minute 15 against 13,034 for the scripted player (2.5x behind) and about $40,000 at the stop. No Gear, no prestige and no second run were reached, and no stopwatch times were taken. The 115.7 and 77.3 minute figures therefore stay unmeasured by any human, as the playtest record itself says. A human at 2.5x the script's early time would not reach the prestige in about two hours unless the gap closes, so T133 must not present 115.7 minutes as an observed or expected human time.

### Data gaps for F005 (Economy and Contracts)

The data is too thin to tune from. One playtester stopped at about 0.4 % of the prestige threshold; the trace is one deterministic reference policy that the owner approved only as a fit to policy r1, with an Ore-only probe for run 1 only; and W003-B is a theoretical event-bound stress (every machine at the 0.1 s bound over 8 h), not a player-reachable state, so it says nothing about what offline returns pay in practice.

F005 should gather, in priority order:

1. Human timelines on the shipped build: Factory Value at 10, 15, 30, 60, 90 and 120 minutes, time to first Gear, first contract, first Item, first prestige and second-run eligibility, with at least 5 players including some new to the game. This yields the human-to-script ratio for SC-003 and SC-004.
2. Strategy mix: share of play spent selling versus delivering contracts, auto-sell usage, and how many players skip the chain. Extend the trace probes to Ore only for runs 2 to 5 and add a contracts-only probe.
3. Prestige timing: Factory Value at confirmation versus the threshold, and whether players wait for the square-root payoff or prestige at the threshold.
4. Offline: absence length distribution, how often the 8 h cap is hit, realised credit in Cash terms, and what players do after the summary. The trace shows an 8 h absence removes only 4.5 % of play time to the $250,000 milestone, and `var.returns` is x2.12, so a return is currently worth little; F005 needs real return patterns before redesigning it.
5. Bottleneck stats: time machines spend stopped on a full store, partial-batch loss (40.8 % of sampled completions in run 1), and where players queue on Cash, slots or unlocks.
6. Per-purchase data: build price scaling (owner note 8, "$69 for an Assembler is practically free"), Industry order, whether Seed Capital at 10+ IP is ever chosen, and how many players hit the ECO-3 caps (runs 3 to 5 are flat because the first prestige uses them up).
7. Clock events: how often the device clock moves backward or jumps forward at start and return, to size the accepted S1-6 risk.

### Findings

| ID | Severity | Finding |
|---|---|---|
| E-1 | Condition (documentation) | The approved bands (115.7 min, 77.3 min) are scripted-policy times. The only human data point is 2.5x slower at minute 15, and the human stopped at 0.4 % of the threshold. T133 and the release notes must label pacing as "target versus scripted trace, human unmeasured". |
| E-2 | Condition (carry to F005) | Ore-only dominance, accepted by the owner for run 1 (13 % faster), is probably much larger in later runs. My coarse model gives about 39 min for run 2 and about 27 min for run 3, against 77 min for the chain policy. This is an uncorroborated estimate that ignores storage caps and player friction, so it is not a defect claim, but it fits the owner's "monotonous" and "free Assembler" feel. F005 must tune chain value-add, Industry caps and selling together, and the trace needs Ore-only and contracts-only probes for runs 2 to 5. |
| E-3 | Accepted, carried | The three sensitivity misses (`var.price-80`, `var.contracts-off`, `var.returns`) and the later-progression bands (runs 3 to 5 flat at 72 to 77 min) are recorded, pinned in the gate and owner-accepted. Nothing to change in F003. |
| E-4 | Note | Contracts pay 1.67 to 1.75 times market value against a 2.0 cap, which leaves little room to rebalance without raising the cap. This matters because the owner also wants contracts not to replace selling. |
| E-5 | Residual risk, accepted | Repeated forward clock jumps grant 6.4 effective hours each, with no cumulative cap (S1-6). Acceptable for a local single-player game; revisit if a trusted time source or competitive features arrive. |
| E-6 | Note | Item rewards fire at completion indices 0, 3, 6, 10 on the timed Bar but 1, 5, 10 on Gear and 1, 4, 9 on Plate. Tests pin this (`EveryTier_IsObtainedFromAContractReward_AndActivates`, `Deliver_EachKind_GearAndTimedBar_PayTheirOwnRewards`), so it is not a defect, but F005 may want a uniform rule. |
| E-7 | Note | Playtest follow-ups T248 and T249 (UI) and T250 (desktop focus loss) are listed in `tasks.md`; the economy effect of T250 is covered by invariant 22. Whether they are merged is the parent's check. |
| E-8 | Static check passed | A grep of `Assets/IdleFactory/Unity/UI/` (panels, shell) and `Unity/Bootstrap/` found no pricing, refund, IP, reward, speed or cost arithmetic. What is there is layout, camera, store-percent display (`StorePercent`, decimal, display only) and "need N more RP" subtraction text. Prices, rates and affordability come from Core snapshots (`RunSnapshots`, `PriceValid`). Scope note: this is a pattern search plus targeted reads, not a line-by-line read of every panel. |

No finding blocks acceptance.

### Verdict

**ACCEPT WITH CONDITIONS.**

Every requested economy invariant has an enforcing test or a recomputation I could check, the exploit review found no free-resource or infinite-loop path (only the documented, bounded forward-clock risk), the shipped content blob is exactly the owner-approved revision 2, and my independent model reproduces the trace's Ore-only run-1 time within 0.6 %. Formulas live in Core and content, not in the views.

Conditions (none changes code or blocks T133 execution):

1. **C-1 (E-1)**: `reviews/final-acceptance.md` and the release note label SC-003 and SC-004 as "targets versus scripted-policy trace; human times unmeasured (owner playtest stopped at about $40,000 Factory Value)", and do not claim 115.7 or 77.3 minutes as observed.
2. **C-2 (E-2)**: record in `docs/product/ideas.md` or the F005 specification inputs the request to extend the Ore-only probe to runs 2 to 5 and add a contracts-only probe, with my numbers marked as an unverified estimate.
3. **C-3**: carry the telemetry and playtest list above into the F005 specification as input, and keep the three pinned sensitivity deviations and the revision 2 trace as history, not as claims.

Blocking items: none.

## Test/QA verdict

### Scope and commit

- Role: Test and QA Engineer, feature-level verdict for T132 (Issue #212, WU68), Feature 003. Read-only, independent of the implementers and of the other T132 verdicts.
- Commit: `6fbdafa` (origin/main, merge of PR #346). No Unity, player, build or adb run was made (no leases). The verdict rests on test sources under `Assets/IdleFactory/Tests/` and the recorded evidence in `specs/003-first-factory-prestige/reviews/`.
- Test inventory by grep: 79 EditMode and 46 PlayMode test files (plus fixtures and support classes) and 17 `[Category("StandalonePlayer")]` markers. Last recorded counts: Edit Mode 1502, Play Mode 1492, StandalonePlayer 23 (T122, base `3cf078d`). Test names below were verified by grep on this commit. Pass state is taken from the recorded runs only.
- Skips: no `[Ignore]`, `[Explicit]` or `Assume.` in `Assets/IdleFactory/Tests/`. Nine `Assert.Ignore` calls exist, all editor-seam guards that fire only in a player (`PlayMode/FactoryRuntimeHostTests.cs` x4, `RunBootstrapTests.cs`, `RunDesktopLifecycleTests.cs`, `RunLifecycleTests.cs`, `RunPanelCompositionTests.cs`, `StartupHudTests.cs`). The Editor runs recorded 0 skipped, and in the player suite only tagged tests run, so there is no unexplained skip.

### FR/SC test matrix

Levels: **D** direct (a test asserts the requirement's own rule), **I** indirect (composed or evidence-only coverage), **N** none. Paths are under `Assets/IdleFactory/Tests/`. E = EditMode, P = PlayMode, S = StandalonePlayer, A = Android emulator evidence. Counts in parentheses are test-method counts.

| Req | Tests (file :: test) | Level | Negative paths |
|---|---|---|---|
| FR-001 | E `RunAuthorityTests` (4: single content input, no F001 writer, retired types absent); E `RunContentValidatorTests` (closed kinds and effects) | I | Retired runtime absent |
| FR-002 | E `RunSimulatorTests::LiteralFr002Chain_Miner_Smelter_Press_Assembler_MatchesTheHandTrace`, `LiteralFr002Chain_AssemblerBottleneckIsFiveEighthsOfItsCapacity`; E `RunSimulationEquivalenceTests::CaseE_LiteralChain_EveryPartitionGivesTheSameState_AndTheBottleneckIsVisible` | D | Bottleneck visible |
| FR-003 | E `RunStateTests::ExchangeMovesInputsAndOutputsAtomicallyWithTheOutputFactor`, `ExchangeRejectsWithoutPartialChange`, `ExchangeRejectsCheckedOverflowAtomically`, `A3_SellIsAtomicAcrossStockAndCash`; E `RunSimulationEquivalenceTests::CaseK_PerStepConservation_...` | D | Overflow, partial change |
| FR-004 | E `RunSimulatorTests::FullStore_NetFittingSmelterCompletes_InputFreeMinerStaysBlocked`, `PartialBatch_*` (11), `PausedMachineKeepsItsProgress_WithoutBacklog`, `InactiveMachineNeitherProgressesNorCompletes`; E `RunSimulationEquivalenceTests::CaseF_FullStore_...`, `CaseK_*` | D | Full store, zero units, overflow |
| FR-005 | E `RunSimulationEquivalenceTests` (19: partitions, definition order, build and slot order, oracle seeds); E `RunSimulatorTests::EffectExpiry_IsProcessedBeforeTheCompletionAtTheSameInstant`, `Od2_AutoSaleFreesCapacity...`, `Priority_DecidesContention_RegardlessOfSlotOrBuildOrder`; E `RunSessionTests::SessionAdvance_EqualsTheDirectSimulatorPath_ForAnyPartitionIncludingEventInstants` | D | Off-by-one cuts, rejected advance |
| FR-006 | E `RunCommandTests::ReorderPriority_ReplacesTheOrder_...`, `ReorderPriority_RejectsANonPermutation_...`, `Build_ChargesTheCostOnce_...JoinsThePriorityLast`, `Remove_...ClosesThePriority`; E `RunStateTests::PriorityIsAPermutationOfTheMachinesAndReorderIsAllOrNothing`; P `PrioritiesPanelTests` (15) | D | Non-permutation |
| FR-007 | E `RunCommandTests::UpgradeStorage_AndFactory_RaiseTheLevels_AndRejectAtTheEndOrWithoutCash`; E `RunContentValidatorTests::ExpansionTablesFollowTheirSequenceAndMonotonicityRules`; P `ExpansionPanelTests` (10); reset on prestige via `PrestigeTransitionTests::Promote_...ResetsEverythingElse` | D | End level, no cash |
| FR-008 | E `RunCommandTests::Build_WhenTheFactoryLevelOpensMoreSlots_AcceptsThem`; E `RunStateTests::MachineSlotsAreUniqueWithinSlotCountAndRejectedAtomically`; P `SlotGridTests` (26), `RunHudLayoutTests` | D | Duplicate or occupied slot |
| FR-009 | E `RunCommandTests` (27: Build, Remove, Replace, Upgrade atomic, rejection reasons, confirmation, refund, `Upgrade_MidCycle_KeepsTheCompletedFraction`); P `BuildSheetPanelTests` (17), `MachinePanelTests` (22) | D | Rejections, overflow, no confirmation |
| FR-010 | E `RunCommandTests::Upgrade_AtAMilestone_...`, `Upgrade_AtTheLastLevel_...`; E `RunContentValidatorTests`, `RunContentReaderTests`; E `SpeedComposerTests` (17) | D | Last level, overflow |
| FR-011 | E `RatesSnapshotTests` (20); E `RunSessionTests::MachineState_ReportsAFullStoreOnlyWhenNotEvenOneUnitFits`; P `InventoryRatesPanelTests` (16), `MachinePanelTests` (blockers, T249) | D | Blocked states |
| FR-012 | E `RunCommandTests::SetAutoSell_*` (3); E `RunSimulatorTests::AutoSale_UsesTheEffectivePrice_FlooredPerSale`; E `RunIndustryTests::Purchase_SalePrice_RaisesTheAutoSellIncome_FromNow`; reset via `PrestigeTransitionTests` | D | Locked resource |
| FR-013 | E `RunUnlockTests` (21); E `BalanceTraceTests` (`Eco2BandsAndTheEco3RunTimesHoldForRevision2`); owner partial playtest `playtest-flo-20261009.md` | D (deterministic), I (human timing; owner waived bands) | Unreachable conditions |
| FR-014 | E `RunContractTests` (21: deliver once, atomic overflow, inclusive deadline, expiry, abandon); E `RunSessionTests::ContractDeadline_IsInclusive_AndExpiresOneMillisecondLater`; P `ContractsPanelTests` (27) | D | Repeat delivery, expiry, overflow |
| FR-015 | E `RunContractTests::Snapshot_ListsOffersInOrdinalOrder_...`, `Deliver_ScalesTheQuantityAndTheRewardsWithTheCompletionCount_...`, `Accept_RejectsAnActiveContract_...`; E `RunContentValidatorTests::CashRewardMustStayBetweenMarketValueAndTheRatioBound`, `ContractKindsMustEachAppear` | D | Active contract, locked offer |
| FR-016 | E `RunResearchTests` (15); P `ResearchPanelTests` (24), `ResearchPanelScrollTests` (4); reset via `PrestigeTransitionTests` | D | Prerequisite, RP short, max level |
| FR-017 | E `RunItemTests::EveryTier_IsObtainedFromAContractReward_AndActivates`, `Activate_ConsumesExactlyOne_...`, `Activate_WithoutTheItem_IsItemNone_...`; E `RunContentValidatorTests::EveryItemTierNeedsAnObtainableAndUsableDefinition`; E `PrestigeTransitionTests::ActiveBoostsAreLost_ButTheUnusedItemsSurvive_...`; P `ItemsPanelTests` (21) | D | No item, bad target, conflict |
| FR-018 | E `RunItemTests::Effect_IsActiveUntilTheLastMillisecond_...`, `Effects_OfDifferentCategories_AndOverdrive_ComposeMultiplicatively`, `SpeedComposition_ClampsToMaxCombinedSpeed`, `Activate_WhileTheSameConflictKeyRuns_IsEffectConflict_...`; E `RunSimulatorTests::MidCycleItemEffect_*`, `SpeedModifiers_FoldInTheDocumentedOrder_...` | D | Conflict, value limit |
| FR-019 | E `RunOverdriveTests` (10: window, cooldown boundaries, doubles production); P `OverdriveControlTests` (9) | D | Active, cooling, locked |
| FR-020 | E `FactoryValuationTests` (17: breakdown, rounding, overflow, build-then-remove lowers value); P `PrestigePanelTests` (15) | D | Overflow, rounding |
| FR-021 | E `FactoryValuationTests::Ip_IsTheFlooredSquareRootOfTheScaledValue`; E `PrestigeTransitionTests::Preview_AwardsTheTableIp`; E `RunContentValidatorTests::PrestigeCurveMustBeSqrtWithPositiveParameters` | D | Boundary |
| FR-022 | E `PrestigeTransitionTests::Promote_PublishesTheFreshRun_KeepsTheItemsIndustryAndIp_AndResetsEverythingElse`, `NextRunStart_ClampsTheStorageLevelAndSaturatesTheCash`, contract and effect drop cases | D | Active or expired contract |
| FR-023 | E `RunIndustryTests` (11); E `RunContentValidatorTests::FirstIndustryCostMustBeAffordableAtTheThresholdIp`; E `BalanceTraceTests::Sc001...FasterNextRun...`; S `RunJourneyPlayerTests` (run 2 faster, 40 s of 2380 s); P `IndustryPanelTests` (14) | D (margin thin, T121-F3) | IP short, max level |
| FR-024 | E `PrestigeTransitionTests` (40: `EveryProtocolStepFailingOnce_ThenRestart/Retry_...`, `ASecondConfirmWhilePending_...`, `Cancel_*`); S `RunTransitionPlayerTests` (4: pending restart, interruption after durable write, restart once, award once) | D | Cancel, failed write, restart, retry, double confirm |
| FR-025 | E `OfflineSegmentationTests` (45: cap, 4/5, departure, clock failures), `OfflineReconciliationTests` (12), `OfflineReturnDurabilityTests` (11: forged cycle, future departure, exactly once at every backend call); P `OfflineSummaryPanelTests` (18), `RunLifecycleTests::AReturnReconcilesTheOfflineIntervalExactlyOnce`; A 8 h return 746 ms | D | Backward, missing, unrepresentable clock; future departure |
| FR-026 | E `OfflineSegmentationTests::Absence_MapsEveryBoundary_...`, `Boundary_At*`, `ContractDeadline_IsInclusive_AlsoOffline`, `SimultaneousRealBoundaries_...`; E `OfflineReturnDurabilityTests::AMidRunWhoseEffectOverdriveAndContractArePastAtLoad_...` | D | Boundaries at departure, return, cap |
| FR-027 | E `SaveV2MapperTests` (28), `SaveV2StoreTests` (27), `SaveV2LifecycleTests` (30), `SaveV2FixtureTests` (5); S/P `PlayerCheckpointRoundTripTests` | D | Corrupt, unknown version, torn data, every backend failure |
| FR-028 | E `V1ImportTests` (62: mapping, proportional progress, failure fixtures leave V1 byte-identical, torn manifests, discard rules); S `RunTransitionPlayerTests::TheV1ImportFromThePackagedFixtureSucceedsOnceAndTheV1DirectoryStaysByteIdentical`; E `SaveV1PlayerFixtureParityTests` | D | Unsupported ID, over-capacity, clock, arithmetic, interrupted |
| FR-029 | E `RunContentReaderTests` (37), `RunContentValidatorTests` (44), `RunContentCanonicalTests` (21), `GameplayContentDispatcherTests` (19), `RunContentSchemaConsistencyTests` (10), `RunContentAuthoringCheckTests`; P `RunBootstrapTests` (rejection on the run path) | D | Each rule code has a case (`EveryContractRuleCodeHasATestCase`) |
| FR-030 | E `RunContentValidatorTests` (reserved IDs, duplicates, unknown references, cycles, presentation keys), `RunContentOrderTests`, `RunCatalogTests` | D | Unknown, duplicate, cyclic, malformed curve |
| FR-031 | E `RunAuthorityTests` (no Feature 001 runtime); closed effect and kind tables; no belt or logistics code exists | I (negative scope; no test names "decorative transport") | n/a |
| FR-032 | P `RunHudLayoutTests` (20 x screen classes), `RunKeyboardFocusTests` (15), `RunTextScaleTests` (22 x classes x 100-200 %), `RunPanelCompositionTests`, `StartupStatusPanelTests`, per-panel suites; `ui-ux-review.md` (Shell gate, Final review); A 7 mm targets | D (layout, focus, scale), I (empty and error states per panel; no physical device) | Error and empty panel states, worst-case content |
| FR-033 | P `RunDeviceProfileTests` (21), `RunLifecycleTests::ARotationCancelsTransientActionsWithoutSpendingPlacingOrPrestiging...`, `ARotationWithoutDepartureCreatesNoOfflineSummaryAndNoSecondReward`, `RunLifecycleClassificationTests` (4), `NavigationInputTests` (24); E `AndroidRuntimeRotationTests` (5), `AndroidBackGestureTests` (3, config pins); A rotation cancels Remove and Prestige, back gesture on second swipe | D (Editor), I (real Android: emulator only) | Rotation with open dialog, no false summary |
| FR-034 | E `F003AssetContractTests` (5), `PresentationSpriteCatalogTests` (19), `F001AssetContractTests`; `art-direction-review.md` (Approval, Integration) | D (import contract), I (visual approval is review evidence) | 4 of 6 badges observed |
| FR-035 | E `CheckedMathTests` (15: 1e18 bound, 128-bit MulDiv), `QuantityFormatterTests` (30), `SaveV2MapperTests::Envelope_ExactBoundaryOfTenToTheEighteenth_IsAccepted`, overflow cases in `RunSimulatorTests` and `RunCommandTests` | D | Overflow rejected atomically everywhere |
| SC-001 | E `BalanceTraceTests::Sc001ChainOreBarPlateGearContractItemResearchPrestigeIndustryFasterNextRunWithNoUnintendedChange`; S `RunJourneyPlayerTests::TheIronChainContractItemResearchPrestigeAndIndustryJourneyMakesTheSecondRunFaster` | D | Zero unintended changes asserted |
| SC-002 | E `RunSimulationEquivalenceTests` (21), `RunSessionTests::SessionAdvance_Equals...`, `OfflineSegmentationTests::Offline_EqualsTheOnlineSession_...`, `Offline_CapacityContention_EqualsOnline_QA6`, `SaveV2MapperTests::RoundTrip_ALoadedRunContinuesLikeTheOriginal_SC002`, `SaveV2LifecycleTests::SaveLoadContinue_EqualsAnUninterruptedRun_AcrossRepeatedRestarts` | D | Partitions at event instants |
| SC-003 | E `BalanceTraceTests` (19; golden trace byte for byte; bands for content revision 2); `playtest-flo-20261009.md` (partial run, about 2.5x behind the policy at minute 15; bands are orientation only per spec session 2026-10-09) | D (trace), I (human, waived) | n/a |
| SC-004 | same trace (`Eco3RunTimes...`); human second run not measured | D (trace), N (human) | n/a |
| SC-005 | E `V1ImportTests` (62), `SaveV1PlayerFixtureParityTests`; S `RunTransitionPlayerTests::TheV1ImportFromThePackagedFixture...` | D | Unsupported, over-capacity or slot, interrupted |
| SC-006 | Edit Mode, Play Mode and Standalone suites (T120 `be29326`, T121, T122 `3cf078d`); A emulator labelled emulator-only | D, but not yet shown green on `6fbdafa` (see currency) | 0 skips recorded |
| SC-007 | P `RunHudLayoutTests` (178 runs at T128), `RunTextScaleTests` (150), `RunKeyboardFocusTests` (51), `...AtEveryScreenClass` panel cases; `ui-ux-review.md` Final review; A 44 pt = 7 mm | D (Editor, all 8 classes), I (emulator only) | Clipping and overlap asserts, worst-case content |
| SC-008 | S `W003PlayerTimingTests` (6, record-only); E `W003PerformanceTests` (zero-allocation asserted; 8 h timing informational), `OfflineSegmentationTests::EightHourWorstCase_*_IsMeasureOnly`; A 746 ms and 61.145 fps; `w003-measurements.md`, `performance-review.md` | I (measured evidence; **no test asserts a budget**) | W003-B 8 h MISS (about 950-1020 ms vs 500 ms), unresolved |

**FR/SC with no direct test:** FR-031 (negative scope, indirect only), SC-004 human measurement (none; trace is direct; owner waived), SC-003 human timing (partial), SC-008 (evidence only: no test asserts a budget, and W003-B misses its budget). No FR is at level N.

### Evidence currency and T133 rerun list

| Evidence | Commit under test | Current for `6fbdafa`? |
|---|---|---|
| T119/T120 full suite (Edit 1497, Play 1475) | `c7fb57a`, `be29326` | Superseded |
| T121 Standalone 11/11, Edit 1499, Play 1480 | `5f738e6` | Superseded |
| T122 W003 timings; Edit 1502, Play 1492, Player 23/23 (last recorded full counts) | `3cf078d` | Stale: eight Asset-touching merges after it |
| T124 emulator run (APK built from `629b630`), T247 baseline APK | `629b630` and the T247 worktree | Stale for content revision 2 and T248-T250 (T128 argues the mobile paths are unaffected; not re-proven) |
| T128 targeted Play Mode runs (RunHudLayout 178, RunKeyboardFocus 51, SlotGrid 28, RunTextScale 150, OfflineSummaryPanel 62, ResearchPanelScroll 10, MachinePanel 89, RunDesktopLifecycle 6, RunLifecycle 7) | `872984a` | Current for those files only; the reviewer states Edit Mode, other panels and Player were not re-run |
| T129 art integration review (harness captures) | before `6fbdafa` | Review evidence, no test run |
| `ac4d719` player fix, PlayerPlayMode 23/23 | after T125 (`9e96cf9`) | Predates T246-T250 |

Implementation merges (changes under `Assets/`, `ProjectSettings/`, `Packages/`) after the T122 run (`3cf078d`), oldest first:

| Merge | Task | Touches |
|---|---|---|
| `45d23f3` (#324) | T245 shipped-catalog device sets | tests, fixtures, journey script |
| `5a00188` (#329) | T125 content revision 2 (`iron-factory-content.json`; 87 Play Mode expectations rewritten) | content, tests |
| `2f03f8a` (#330) | player compile fix | test support |
| `0dd7d29` (#332) | T246 short-return summary (60 s threshold) | `OfflineSummaryPanel`, lifecycle |
| `55dc84d` (#333) | T247 Android back gesture | manifest guard tests |
| `063590c` (#339) | T248 Research graph scroll | Research panel |
| `488cd33` (#340) | T249 stable Machine panel at a full store | Machine panel |
| `dd929d1` (#341) | T250 desktop focus is not a departure | `RunLifecycleAdapter`, bootstrap |

Everything from `872984a` to `6fbdafa` is docs or review records (#342-#346). **No complete Edit Mode + Play Mode + Player run is recorded on any commit after T122** in the review files (per-Issue QA reports were not examined). The player compile break found after T125 shows that Editor green does not imply player green, and T246-T250 changed player-relevant code with no recorded player run.

**T133 must rerun on the accepted commit (`6fbdafa` or later), under the `unity,emulator` leases:**

1. `scripts/test-unity.ps1` full Edit Mode then Play Mode. Reconcile counts against T122 (1502 and 1492) plus the tests added since (T125 to T250). Require 0 skipped, no 0-test run, no `error CS`, missing-script or null-reference entry.
2. `scripts/test-unity.ps1 -StandaloneWindowsPlayer` once for the batch (at least 23 tests, compiles for the player, includes `RunJourneyPlayerTests` and `RunTransitionPlayerTests`).
3. The console check (expected benign entries only: negative-path persistence diagnostics, `panel.*.failed` cases, Hub licensing).
4. `scripts/check-settings-noise.ps1 -Commit <accepted sha>`.
5. Quickstart sections 1 to 9, including the W003-S 8 h return and producing FPS on an APK built from the accepted commit (T124 predates content revision 2 and T246-T250), or an explicit statement that those emulator figures are carried forward from `629b630`.

### Test gaps (prioritized)

| # | Gap | Source | Risk | Priority |
|---|---|---|---|---|
| G1 | No full-suite or player run recorded on a post-T122 commit; eight implementation merges since. | currency table | Undetected regression; SC-006 gate | High, closed by T133 |
| G2 | SC-008 has no asserting test. `W003PlayerTimingTests` and `*EightHourWorstCase*` are record-only. W003-B misses 500 ms on Windows Mono (about 951-1018 ms) and is unmeasured on IL2CPP, release and emulator. The emulator 746 ms is a different workload (W003-S). | `performance-review.md`, `w003-measurements.md` | A budget regression would pass every suite; owner decision pending (options A and B in the review) | High: record the decision, or measure, before acceptance; label as MISS and unresolved in `final-acceptance.md` |
| G3 | Security F-2: no test drives a failing safety checkpoint through `RunLifecycleAdapter` (focus-loss write when storage fails or the host flush throws). Only store-level `EveryStorageFailure_IsATypedRejection_NotAnException` and `SaveNow_WithFailingStorage_...` exist. | `security-devops-review.md` T250-5 | Exception inside a Unity callback on a disk-full desktop | Medium |
| G4 | Quit-then-focus-lost ordering untested. `RunDesktopLifecycleTests` calls `SendQuit` only inside `DesktopRegularShutdownRecordsTheDepartureAndTheNextStartCreditsTheIntervalOnce`, never followed by a focus-lost callback; the guard in `WriteSafetyCheckpoint` is code-only. | `security-devops-review.md` T250-4 | A safety write could overwrite the departure checkpoint | Medium; small test |
| G5 | UI/UX N-10: no end-to-end test of a short lifecycle return with an expired effect or contract; only the pure `OnlyACleanAppliedReturnBelowTheThresholdIsSuppressed` / `ShouldOpenSummary` check. | `ui-ux-review.md` N-10, T128 item (b) | A short return that expired an effect could skip a needed summary | Medium |
| G6 | UI/UX N-5: a cold start after a short unclean exit (18-56 s) opens the modal summary; no test pins the intended behavior and the owner decision is open. | `ui-ux-review.md` N-5 | Behavior ambiguity, not a defect | Medium (decide, then pin) |
| G7 | Performance W003-B on emulator and IL2CPP or release unmeasured; no quiet-host median. | `performance-review.md` | Half of SC-008 unresolved | Medium (Option 0 of the review) |
| G8 | Art C1: WAIT and PAUS badges never observed; `SlotGridTests` asserts only the rank order. N3 (two-reviewer shuffled-sheet pass) open. | `art-direction-review.md` C1, N3 | Defensive badges may be unreadable | Low-Medium |
| G9 | `AndroidBackGestureTests` pins settings only (predictive back default, no back handling, immersive start); the gesture itself was emulator-injected and cancels only on the second swipe. No physical device. | T247 evidence | Platform claim limited to emulator | Low (documented limitation) |
| G10 | Machine panel at a truly full store (T249) is evidenced by tests only; Alt-Tab and Shift+wheel were not hand-driven. | T128 | Visual only | Low |
| G11 | SC-003/004 human bands: first-run playtest partial (about 2.5x slower than the policy at minute 15); second run and prestige time not measured; owner waived bands. | `playtest-flo-20261009.md` | Balance feel, owner-accepted | Low |
| G12 | Nine `Assert.Ignore` editor seams give no player coverage for those bodies; mitigated by tagged player tests. | grep | None | Info |
| G13 | `RunAuthorityTests::NoAssetReferencesARetiredGuid` fragility on a stale `Library/` (T120-F1); wiki says T244 fixed it. T133 should still use a clean worktree. | `full-suite-evidence.md` | False failure | Info |

### Latest-batch regression guard

| Task | What changed | Guarding tests | Residual |
|---|---|---|---|
| T246 | Lifecycle returns under 60 s open no summary (`ShortAbsenceThresholdMs = 60_000`) | `PlayMode/OfflineSummaryPanelTests`: `AShortHomeStyleReturnOpensNoSummaryAndKeepsFocusAndTheOpenSurface`, `AReturnAtAndAboveTheThresholdShowsTheSummaryWithIdenticalAccounting`, `OnlyACleanAppliedReturnBelowTheThresholdIsSuppressed`, `AShortAbsenceSaysNoProductionWhileAway`; `RunLifecycleTests` (rotation and exactly-once) | G5, G6 |
| T247 | Back gesture investigation, manifest pins, journey script follow-ups | `EditMode/AndroidBackGestureTests` (3), `AndroidRuntimeRotationTests` (5), `OfflineSummaryPanelTests::EscapeAndAndroidBackCloseASummary`; emulator evidence | G9 |
| T248 | Research graph drag, wheel and Shift+wheel scroll | `PlayMode/ResearchPanelScrollTests` (4 cases, 10 runs), `ResearchPanelTests` (24) | Shift+wheel by hand unverified |
| T249 | Reserved rows, blockers below actions, no jump at a full store | `PlayMode/MachinePanelTests` (22 cases, 89 runs across classes and text scales), `RunTextScaleTests`, `RatesSnapshotTests` | G10; blocker below the fold at 200 % text (N-9, owner decision) |
| T250 | Desktop focus loss is not a departure; one plain safety checkpoint per focus-loss edge; regular shutdown records the departure; phones and tablets unchanged | `PlayMode/RunDesktopLifecycleTests`: `DesktopFocusLossKeepsTheRunLiveAndRecordsNoDeparture`, `DesktopFocusLossWritesOnePlainSafetyCheckpointWithoutDepartureSemantics`, `DesktopRegularShutdownRecordsTheDepartureAndTheNextStartCreditsTheIntervalOnce`, `AnUncleanDesktopExitCreditsFromTheLastSafetyCheckpointOnce`, `TheMobileClassStillRecordsADepartureOnFocusLossAndReconcilesTheReturn`, `ThePlatformSeamFollowsTheOverrideAndRestoresThePreviousValue`; `RunLifecycleTests` (8), `RunLifecycleClassificationTests` (4) | G3, G4; no throttle on rapid focus toggling (Security F-1, advisory, no test) |

Regression risk is moderate. Each task has focused tests, and the T128 reviewer ran those files green on `872984a`. The residual risk is cross-cutting: T125 rewrote 87 Play Mode expectations, `LifecyclePlatformTestDefault.cs` changes the default platform seam for every lifecycle test, and T250 touches the bootstrap that every scene-based Play Mode test composes. Only a full run shows these.

### Verdict

**ACCEPT WITH CONDITIONS.**

Basis: every FR has at least one direct test or a deliberate indirect one; determinism, atomicity, offline, Save V2, V1 import, prestige retry and content validation have strong direct and negative-path coverage; no test is ignored, explicit or assumed away; the player journeys for SC-001, FR-024, FR-028 and SC-005 exist and passed at T121 and T122. What prevents an unconditional accept is evidence currency (no complete run after T122, eight implementation merges since) and SC-008, which has no asserting test and a recorded W003-B MISS.

T133 must prove:

1. Full Edit Mode and Play Mode green on the accepted commit, counts reconciled to 1502 and 1492 plus the tests added since, 0 skipped, console clean.
2. `-StandaloneWindowsPlayer` green (at least 23 tests, compiles for the player) on the accepted commit.
3. Quickstart sections 1 to 9 executed, with the emulator W003-S return time and producing FPS re-measured on an APK built from the accepted commit, or explicitly labelled as carried forward from `629b630`.
4. `final-acceptance.md` records SC-008 honestly: the W003-B Windows 8 h reconcile MISS (about 0.95-1.02 s vs 500 ms) as owner-accepted or funded, W003-B on emulator and IL2CPP unmeasured, and the labels emulator-only, no physical device, iOS deferred.
5. Open risks carried: G3, G4, G5, G6, G8 (C1 and N3), UI/UX F-9, N-5 and N-9 owner decisions, Security F-1 to F-3, and the waived SC-003/004 human bands.

Recommended before T133 and not blocking: add tests for G3, G4 and G5. If condition 1 or 2 fails on the accepted commit, the verdict becomes REJECT until fixed.

