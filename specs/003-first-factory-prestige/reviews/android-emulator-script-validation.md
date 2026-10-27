# Android emulator journey script: T123 validation (OPS-4)

Scope: `scripts/android-emulator-journey.ps1 -Scenario F003`. Validated without an emulator, Unity or `adb`; the device run is T124. Emulator-only development evidence; no physical-device claim.

## What was added

- `-Scenario Default|F003` (default `Default`: step set, parameters and results unchanged; the diff touches no existing function, only appends the F003 block, the scenario check, the switch cases and one comma in the parameter list).
- F003 steps: `TouchBuild`, `RemoveConfirmRotation`, `PrestigeConfirmRotation`, `W003Push`, `OfflineReturnTiming`, `ProducingFps`, `F003LogScan`. Without `-Steps`, F003 runs `Preflight,Install,Launch` followed by all of them.
- Parses the Development-build line `ResumeAndReconcile ms=<n>` (`FactoryRuntimeHost.MeasureResumeAndReconcile`, T110).
- Verifies the committed W003-S hashes (`Fixtures/SaveV2/README.md`): data `0467d293...ab73`, manifest `84cfe0b4...e7bb`.

## T124 inputs the script cannot know

- Layout-dependent taps: `-BuildTaps`, `-RemoveTaps`, `-PrestigeTaps` (`"x,y"`, values <= 1 are display fractions). Without them those checks are INFO "skipped", not PASS.
- The frame-rate step needs the profiler-free build (fails on any `Autoconnect Profiler` line); the timing step needs a Development build (the reconcile line is `Debug.isDebugBuild` only). One APK cannot serve both if the profiler is autoconnecting: run `OfflineReturnTiming` with the Development APK and `ProducingFps` with the profiler-free one (`-Steps`), or use one Development build without profiler autoconnect.
- Control for the frame-rate step: a fresh empty run (`pm clear`) before and after the W003-S capture.

## Commands and output (lease `issue-203` held the emulator and Unity throughout; it was not touched)

```
Parser::ParseFile                                        -> parse errors: 0
-Steps Bogus                                             -> Unknown step 'Bogus'. Known steps: Preflight, ..., Profile.
-Steps W003Push            (Default scenario)            -> Unknown step 'W003Push'. Known steps: Preflight, ..., Profile.
-Scenario F999                                           -> Unknown scenario 'F999'. Known scenarios: Default, F003.
-Scenario F003 -Steps Nope                               -> Unknown step 'Nope'. Known steps: ... , F003LogScan.
-Scenario F003 -LeaseOwner issue-205-test                -> [FAIL] Environment / emulator lease: BLOCKED emulator: held by 'issue-203' ...; exit 1
(default scenario, same lease conflict)                  -> same BLOCKED result, exit 1
```

The lease conflict clears `$Steps`, so no step and no `adb` call runs (only the report is written). Not tested: the renew-if-owned path (it would need a free or own lease; issue-203's lease was not touched).
