---
title: F003 feature review and evidence currency
type: decision
sources: [S018]
updated: 2026-10-09
---

# F003 feature review and evidence currency

T132 (`reviews/feature-review.md`) holds four independent read-only verdicts on merge `6fbdafa`, each from a separate session that did not read the others: Product, Architect, Game Systems and Test/QA. All four say **accept with conditions**, with no blocker; any of them becomes reject if the T133 full or player suite fails. 22 acceptance scenarios across US1 to US6 are met or met with a stated caveat; none is unproven. (S018)

## Conditions carried into T133

1. **SC-008** is reported only as met for the shipped workloads with the W003-B miss accepted or tracked, never as plainly passing, once the owner records A, B or C ([W003 baseline](./w003-player-timing-baseline.md)). (S018)
2. **FR-032 / SC-007**: write down the F-9 exemption and the N-5 decision ([UI/UX final review](./f003-ui-ux-final-review.md)). (S018)
3. **Labels**: SC-003 and SC-004 are owner judgment (the playtest never reached first Gear, first prestige or a second run, and ran about 2.5x slower than the policy at minute 15); Android evidence is emulator-only; iOS and physical devices are not claimed; the scripted player prestige uses a scaled threshold. (S018)
4. **Carry** the OD-7 release-note text and security F-1 to F-6 ([security review](./f003-security-review.md)); the owner acknowledges F-3. The shipped content blob stays `66f41fb`. (S018)

## Architect findings

No boundary violation. F-A1 unguarded safety-flush; F-A2 public `RunState` mutators; F-A4 three reason codes (`command.invalid`, `checkpoint.content`, `discard.unavailable`) missing from the contract. Before F005, split `RunPersistence` (1,919 lines) and `RunSnapshots`, and tighten mutator visibility. (S018)

## Evidence currency gap

The newest complete Edit, Play and Player run (T122, `3cf078d`) predates eight Asset-touching merges (T245, T125 content revision 2, a player compile fix, T246 to T250). The T124 emulator APK predates content revision 2 and T248 to T250. No complete run is recorded on any later commit, and the player compile break after T125 shows editor green does not imply player green. So the earlier runs count as supporting evidence only, and T133 must rerun the full suites, `-StandaloneWindowsPlayer`, the console check, settings-noise and the quickstart on the accepted commit, or label the emulator figures carried forward. (S018)

Test gaps G3 to G6: no failing safety write through the adapter, quit-then-focus-lost untested, no short-return expired-effect test, and N-5 unpinned. SC-008 has no asserting test: the W003 player tests only record. (S018)

## Review pattern for read-only reviewers

A reviewer may run a temporary, uncommitted harness. T129 isolated saves by setting `PlayerSettings.productName` to a throwaway name, so runs wrote to a separate `persistentDataPath`, refused to start if the path contained `idle-factory-game`, and reverted `ProjectSettings` afterwards. Disclose every grant the harness made beyond public commands. (S018)

Related: [feature acceptance evidence](./feature-acceptance-evidence.md), [F003 art pipeline](./f003-art-pipeline.md).
