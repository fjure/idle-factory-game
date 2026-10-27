# F005 full-suite evidence (T347, issue #412)

Independent Test/QA run of plan step 17a: full Edit Mode and Play Mode suites with 0 skips and a console check. No product code was changed.

| | |
|---|---|
| Commit tested | `cc0afd3` (origin/main after #506; worktree `.maqa-worktrees/issue-412-t347`, clean tree before the runs) |
| Unity | `6000.6.0f1` (pinned), batch mode, Unity CLI only (Unity MCP not connected) |
| Leases | `unity` + `emulator` held by the parent as `issue-412`; not touched here |
| Host | Windows 11 Pro, one editor at a time. Background load during the runs (PF-9): Steam (+8 webhelpers), Riot Client, Docker Desktop, about 50 Chrome processes, other agent sessions' idle shells. No other Unity editor or emulator process. Durations are therefore not quiet-host timings and are not used as measurements. |
| Results root | `test-results/unity/` (git-ignored, not committed) |
| Runs | 2026-10-27 12:17 to 12:38 (+01:00) |

## Commands and totals

All commands run from the worktree. Proxy variables were unset for the shell.

| # | Command | Start to end (+01:00) | Total | Passed | Failed | Skipped | Inconclusive | Ignored / explicit | Results XML |
|---|---|---|---|---|---|---|---|---|---|
| 1a | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1` (Edit Mode part) | 12:17 to 12:21 (186 s) | 2323 | 2323 | 0 | 0 | 0 | 0 | `test-results/unity/20261027T111703219Z/EditMode-results.xml` |
| 1b | same command (Play Mode part) | 12:21 to 12:30 (546 s) | 2230 | 2230 | 0 | 0 | 0 | 0 | `test-results/unity/20261027T111703219Z/PlayMode-results.xml` |
| 2 | `scripts/test-unity.ps1 -StandaloneWindowsPlayer` | 12:31 to 12:33 | 39 | 39 | 0 | 0 | 0 | 0 | `test-results/unity/20261027T113107240Z/PlayerPlayMode-results.xml` |
| 3 | DevTools: temporary `Assets/csc.rsp` with `-define:IDLEFACTORY_DEV`; `Unity.exe -batchmode -projectPath <wt> -runTests -testPlatform PlayMode -assemblyNames IdleFactory.DevToolsTests -testResults ... -logFile ...` | 12:34 to 12:35 (2.5 s of test time) | 68 | 68 | 0 | 0 | 0 | 0 | `test-results/unity/devtools/DevTools-results.xml` |
| 4 | `scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull` (no baseline-write env var) | 12:34 to 12:38 (175 s) | 7 | 7 | 0 | 0 | 0 | 0 | `test-results/unity/20261027T113451853Z/EditMode-results.xml` |

- The `UNITY_TEST_PASS` lines of the script agree with the XML. Skipped, inconclusive, failed, ignored, explicit and not-runnable counts were taken from the `<test-case result=...>` and `runstate` attributes of each XML, not from the script alone. No Play Mode run reported 0 tests.
- Counts match the last known ones: Edit 2323, Play 2230 (after T367), StandalonePlayer 39.
- Reading of plan.md step 17a: the step names only the full Edit Mode and Play Mode suites plus a console check. `BalanceFull` is a named step 16 item ("Then full Play Mode, Windows player and `BalanceFull` runs"), and the default run excludes it. It was run once anyway as extra evidence (7/7, no skips); 17a does not require it. The DevTools assembly is outside the default suite and was run as the assignment required (68/68).
- The tagged Windows player run passed once for this batch. No `PlayerWithTests.exe` or `UnityCrashHandler64.exe` was left running under the worktree after the run (checked with `Get-CimInstance Win32_Process`; only this agent's shells matched the worktree path).
- `Assets/csc.rsp` and its `.meta` were deleted after run 3. ProjectSettings noise from the runs (`ProjectSettings/ProjectSettings.asset`) was discarded with `git checkout`.

## Console check

Scanned: `EditMode.log`, `PlayMode.log`, `PlayerPlayMode.log` (the player build/editor log; the script prints no separate in-player log path), `DevTools.log`, and the BalanceFull `EditMode.log`. Search terms: `error`, `exception`, `warning CS`, `Unknown pseudo`, `[Error]`, `assert`. The Unity test runner fails a test on an unexpected Error/Exception/Assert log, so a green run already means every such log was announced by its test (`LogAssert.Expect`); the table classifies what remains visible in the logs.

| Message (normalised) | Count | Source | Classification |
|---|---|---|---|
| `Unknown pseudo` | 0 | all logs | none |
| `[Error]` | 0 | all logs | none |
| `[IdleFactory.Persistence] ... rejected at ManifestA/B, DataA/B, Pending, Flush (IOException / UnauthorizedAccessException)` | about 250 | EditMode.log; `CheckpointDiagnostics.Report` from fault-injection tests (`CheckpointStoreTests`, `CheckpointGenerationFilesTests`, `RunCheckpointTestSupport`, the SessionLog isolation tests) | Expected, test-provoked write/read faults |
| `[IdleFactory.Persistence] Deserialize rejected at Lexical / Structure / Semantic (SaveStructureException, SaveSemanticException, JsonReaderException, KeyNotFoundException, ArgumentException)` | about 100 (1 in PlayMode.log) | `CheckpointMappingTests.AssertStructural / AssertSemantic / AssertInvalid`, corrupt-save tests | Expected, test-provoked malformed saves |
| `Run safety checkpoint failed and was contained: NullReferenceException` (2), `Run lifecycle callback failed and was contained: NullReferenceException` (1) and `InvalidOperationException` (1), `System.NullReferenceException: injected unexpected failure` (3), `System.InvalidOperationException: Injected host preparation failure.` (1) | 8 | PlayMode.log; `RunLifecycleAdapter.LogContained`; `RunDesktopLifecycleTests`, `LiveGapHostTests` | Expected, injected-failure containment tests |
| `panel.prestige.failed` (2), `panel.build.failed` (1): panel could not be built (ArgumentNullException) | 3 | PlayMode.log; `CoreGameFoundationBootstrap.RecordPanelFailure`; `RunPanelCompositionTests` | Expected, test-provoked panel failure (`StartupHudTests.HudMissingAssetsProducesAttachedDisabledErrorState` is the intended error-state path) |
| `[Licensing::Module] Error: Access token is unavailable; failed to update` | 1 per log (Edit, Play, Player, DevTools, BalanceFull) | Unity editor in batch mode without a signed-in licence session | Expected host/environment message; no test impact |
| `warning CS0618` `EventBase.PreventDefault()` obsolete, `ResearchPanel.cs` lines 550, 651, 663 | 3 distinct, printed 2x per compile; 6 in Player log | product code `Assets/IdleFactory/Unity/UI/Panels/ResearchPanel.cs` | Relevant (low): deprecation warning in shipped F005 UI code, no failure. Finding F-1 |
| `warning CS0618` `PlayerSettings.Android.startInFullscreen` obsolete, `AndroidBackGestureTests.cs(53)` | 2 | test code | Relevant (trivial, test code only) |
| `warning CS0184` expression never of type `Button`, `SessionLogPanelTests.cs(269)` | 2 | test code | Relevant (trivial, test code only; the check is always false) |
| `warning CS0414` field `TraceContext._nextInstance` assigned but never used, `BalanceTraceRunner.cs(88)` | 2 | test code | Relevant (trivial, test code only) |
| `warning CS0414` `RunLifecycleTests.realtime` and `FactoryRuntimeHostTests.runtimeHost` assigned but never used | 2 + 2 | test code, Player build log | Relevant (trivial, test code only) |
| Stack-frame lines naming `UnityLogCheckDelegatingCommand:CaptureException`, `ExecuteEnumerableAndRecordExceptions`, `Assert:That`, `ThrowsConstraint` | about 2500 | Unity Test Framework frames under the logged messages above | Expected, framework frames |
| Asset-size rows `UnexpectedLogMessageException.cs`, `fN-ui-icon-warning-vN.png`, `LogAssemblyErrors (Nms)` | 1 each | import/report summary lines | Not messages; search false positives |

No error-level message, unhandled exception, assertion failure or `Unknown pseudo` is present outside tests that provoke it. DevTools.log has only the licensing line and framework frames. No compile errors.

## Findings

- **F-1 (low, not a blocker):** `ResearchPanel.cs` (lines 550, 651, 663) uses `EventBase.PreventDefault()`, obsolete in Unity 6000.6 ("Use StopPropagation and/or FocusController.IgnoreEvent"). It compiles with warning CS0618. Whether it is new for F005 was not checked against an earlier baseline (no earlier warning count is recorded). Owner decision: replace the call in a follow-up or accept; a later Unity update may turn it into an error.
- **F-2 (trivial, test code):** the unused-field, never-true and obsolete-API warnings in test files listed in the table.
- No failure, skip, inconclusive, ignored or flaky test occurred, so nothing was re-run in isolation.
- Limits: no in-player log was available for the 39 player tests; that result rests on the XML and the build log. These runs are batch-mode editor and Windows-player runs, not an emulator or device; device evidence is T351.

## Verdict

PASS for T347: Edit Mode 2323/2323, Play Mode 2230/2230, StandalonePlayer 39/39, DevTools 68/68, BalanceFull 7/7. 0 failed, 0 skipped, 0 inconclusive, 0 ignored in every suite. The console check shows only test-provoked or environmental messages plus the low-severity warnings F-1 and F-2.
