---
title: F003 final acceptance
type: decision
sources: [S018]
updated: 2026-10-10
---

# F003 final acceptance

T133 (PR #352, merge `9f9ce52`, record `reviews/final-acceptance.md`) accepted Feature 003 on `ac4256c` (merge of T251): **accepted with recorded deviations**. A first run on `8a8e518` found the Android Back regression, so the whole run was repeated after the fix ([Android Back and runInBackground](./f003-android-back-run-in-background.md)). (S018)

## Evidence on `ac4256c`

Edit Mode 1522, Play Mode 1530, tagged Windows player suite 24, all green with 0 skips; console without relevant errors; settings-noise check OK; quickstart 1 to 9 run. The manual keyboard walkthrough was not repeated (named automated tests instead) and the first prestige has never been human-played. (S018)

## Owner decisions (2026-10-09)

| ID | Decision |
|---|---|
| D1 | SC-008: accept the W003-B miss (option C); measure first as follow-up #353 ([baseline](./w003-player-timing-baseline.md)) |
| D2 | PR-2: HUD allocation once per simulated second accepted |
| D3 | F-9: world slots below 44 pt exempt for F003; F004 owns touch targets |
| D4 | N-5: keep Welcome back after a short unclean exit; F004 popup rework |
| D5 | F-3: no timer autosave while unfocused, residual risk acknowledged ([security review](./f003-security-review.md)) |
| D6 | Blocker section below the fold at 200 % text: leave for F004 |
| D7 | AD-1, AD-4, AD-6 art deviations deferred to F004 |
| D8 | Fix the Back regression before acceptance (T251) |

(S018)

## Status labels

- **SC-008**: met for the shipped workloads (Windows S 320 ms and R 175 ms within 500 ms; emulator 8 h return 735 ms within 2 s; 61.4 fps), W003-B miss (966 ms) accepted, never plainly passing. (S018)
- **SC-003 and SC-004**: scripted-trace targets (first Gear 12 min, eligibility 115.7 min, run 2 77.3 min), not observed human times. (S018)
- All Android figures are emulator-only; physical Android and iOS (#39) are not claimed. FR-032 and SC-007 hold with the D3 exemption. (S018)

## Follow-up Issues

#353 W003-B measurement and optimization decision; #354 safety checkpoint failing-write test, quit-then-focus-lost order, throttle (F-1, F-2, F-A1); #355 three missing reason codes (F-A4); #356 split `RunPersistence` and `RunSnapshots`, tighten `RunState` mutators before F005; #357 Ore-only probes for runs 2 to 5; #358 carry the telemetry and playtest list into the F005 spec. (S018)

## Lessons

- Time on a quiet host: the emulator running inflated the reconcile 13 to 18 percent ([baseline](./w003-player-timing-baseline.md)). (S018)
- Call the journey script with `-Command "& ./scripts/android-emulator-journey.ps1 ..."` so tap arrays stay arrays ([emulator validation](./android-emulator-validation.md)). (S018)
- A restarted acceptance supersedes the first run ([evidence rules](./feature-acceptance-evidence.md)). (S018)

Related: [F003 feature review](./f003-feature-review.md).
