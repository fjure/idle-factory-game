# Readability record (Feature 006, SC-004, SC-005, SC-006, T546)

T546 is a parent-only gate, recorded 2026-11-26. The parent prepared and scored the trials. The blind namers and the orderer were fresh agents (Claude Sonnet, Medium tier) that never saw a key, the captures' producer, or each other's work. The owner was the intended taker of the timed trials. Captures come from T545 (Windows classes, PR #785) and T562 (emulator class emuP and the re-captured P3 tiles, PR #788) on the shipped build. Their method and limits are in [`final-test-evidence.md`](final-test-evidence.md), sections T545 and "T562 emuP captures". The answer keys stayed outside the repository until scoring was finished. They are released here, in [`trials-f006/keys/`](trials-f006/keys/).

## Status

| Criterion | Binding? | Evidence | Result |
|---|---|---|---|
| SC-005 blind naming, every cue state, grayscale and the three CVD filters | binding | [`trials-f006/results/sc005-A-score.json`](trials-f006/results/sc005-A-score.json) (emuP, phoneP) and [`sc005-B-score.json`](trials-f006/results/sc005-B-score.json) (phoneL, tabL); transcripts in [`results/naming/`](trials-f006/results/naming/) | **PASS**: 22/22 tiles on every field, zero confusions, in all 16 sheets (4 classes x 4 filters) |
| SC-005 P3 cues (upgrade marker, production-attention markers, ghost preview, hub dot, card beside its machine), same filters | binding (part of SC-005 final per T546) | same score files, `p3` blocks; hub re-test in [`results/hub-retest/`](trials-f006/results/hub-retest/) | **PASS after re-test**: 110/112 first pass; the 2 misses were one swapped pair (tabL deuteranopia, hub dot vs no dot); the fresh re-test named 16/16 hub pairs correctly, including that pair. No fix task, by the owner's rule (re-test, then decide). |
| SC-005 F004 timed cue trial, 10/10 within 2.0 s, median at most 1.077 s | binding | trial pages `trials-f006/sc005/timed/standard/` and `reduced-motion/`; keys released | **NOT RUN, owner decision 2026-11-26** ("skip the timed trials"). Unmeasured; see Limits |
| SC-005 cue and lock readability at or above the F004 floors per class (DEV-2, DEV-4) | binding | T562 per-class marker visibility ([`final-test-evidence.md`](final-test-evidence.md), T562) and `uiux-f006-final.md` section 5 (pt per world cell, margins) | markers drawn on every captured class; phone classes sit at 24 to 28 pt per cell against the 23.76 pt cutoff (small phone 720 x 1280 at the cutoff) |
| SC-004 owner timed trial: stopped machines and their category, 9/10 within 3.0 s; shared missing resource, 9/10 within 5.0 s | guidance | trial pages `trials-f006/sc004/` (Windows classes) and `sc004-emuP/`; keys released | **NOT RUN, owner decision 2026-11-26**. Not measured against the baseline |
| SC-006 independent ordering of three development stages, at least 9 of 10 | guidance | [`results/sc006-set1-score.json`](trials-f006/results/sc006-set1-score.json) (Windows classes) and [`sc006-set2-score.json`](trials-f006/results/sc006-set2-score.json) (emuP); transcript in [`results/ordering/`](trials-f006/results/ordering/) | **PASS**: 10/10 and 10/10. The owner's "difference is meaningful" confirmation belongs to T549 |

## Method

### SC-005 blind naming

- **Sheets**: the T545/T562 sheets per class (emuP 1080 x 2400, phoneP, phoneL, tabL), 22 tiles per sheet in grayscale, deuteranopia, protanopia and tritanopia. Every filter sheet uses its own seeded shuffle.
- **Vocabulary**: the F004 SC-001 vocabulary (slot condition, machine type, state, markers, selection, certainty), copied to [`results/naming/vocabulary.md`](trials-f006/results/naming/vocabulary.md).
- **Namers**: two fresh agents. Namer A took emuP and phoneP; namer B took phoneL and tabL.
- **P3 tiles**: 7 per class and filter: upgrade marker on a non-boosted, affordable, problem-free machine; shortage marker; store-full marker; ghost preview; hub dot and its no-dot control; card beside its machine. Namers gave a cue name and a certainty for each.
- **Scoring**: `tools/art-pipeline/trials/f006/f006_trials.py score-sc005` against the keys. Tiles the key marks `cueShown: false` are not scored (C-92); there were none in these sets.

### Hub-dot re-test

After the tabL deuteranopia swap, the owner chose "re-test, then decide" (2026-11-26).

- **Material**: 16 pairs of 1x crops of the Office button, one with the hub dot and one without, taken from the P3 sheets of all four classes and filters. The pairs were shuffled with seed 7461, and the label region was masked.
- **Taker**: a fresh namer, who named the dotted button in every pair.
- **Files**: crops, task, answer and key are in [`results/hub-retest/`](trials-f006/results/hub-retest/).

### SC-006 ordering

- **Material**: two sets of 10 trials, each showing three shuffled captures of the same factory at the Early, Partial and Established stages (FR-036). Set 1 is the Windows classes (T545); set 2 is emuP (T562).
- **Taker**: one fresh orderer agent, which logged an order per trial.
- **Scoring**: `score-sc006` against the keys.

## Results

### SC-005 blind naming (binding part)

All 16 sheets: 22/22 on every field, including machine type, and zero confusions. This exceeds the spec's 9/9 per filter.

Namer A recorded that machine type and state meanings were guesses from silhouette and badge shape. They were still all correct, so the cues carry the meaning without a legend.

### SC-005 P3 cues

| Class | Grayscale | Deuteranopia | Protanopia | Tritanopia |
|---|---|---|---|---|
| emuP | 7/7 | 7/7 | 7/7 | 7/7 |
| phoneP | 7/7 | 7/7 | 7/7 | 7/7 |
| phoneL | 7/7 | 7/7 | 7/7 | 7/7 |
| tabL | 7/7 | **5/7** (tile 1 hub dot named "no dot", tile 7 the reverse) | 7/7 | 7/7 |

- **Upgrade marker** (T563, reused `F003-UI-ICON-UPGRADE`): named correctly in all 16 sheets.
- **Hub-dot re-test**: 16/16 correct, including the tabL deuteranopia pair. The namer marked 3 pairs "unsure" but still named them correctly: grayscale and deuteranopia on the phone classes. The dot is 1 to 2 px and low in contrast in those crops.
- **Attribution of the tabL miss**: the tabL P3 sheet is 6168 x 4698 px, because the full-screen card tile sets the sheet size. Viewed scaled down, the hub tile loses its dot (C-102). With the 1x crops the same cue was read correctly.
- **Shortage marker** (C-100): named by elimination in several sheets. The thin pipe tile is a tile-quality limit of the crop, not a confusion.
- **Card tile** (C-99): the card tile is not single-cue.

### SC-006 ordering

| Set | Correct | Required |
|---|---|---|
| set 1, Windows classes (tabP, phoneP, tabL, phoneL; sparse, balanced and dense mixes) | 10/10 | 9 |
| set 2, emuP | 10/10 | 9 |

### Against the baseline

- **SC-005 blind naming**: the F004 SC-001 final result was 22/22 on the gate fields, with machine type swapped by reviewer A in 3 sets. F006 holds 22/22 on all fields, including machine type, so there is no regression.
- **SC-005 timed trial**: the F004 reference median is 1.077 s from the F004 SC-002 standard set, which scored 8/10 within 2.0 s (accepted as not met in F004). F006 has no measurement.
- **SC-004 and SC-006**: no F004 equivalent. SC-004 is unmeasured; SC-006 is above guidance.

## Owner decisions

| Date | Decision |
|---|---|
| 2026-11-26 | Hub-dot miss: re-test, then decide. Result 16/16, so no fix task. The dot is recorded as a weak but readable cue. |
| 2026-11-26 | Skip the owner timed trials as a task (SC-004 both sets, SC-005 timed standard and reduced motion). The SC-005 timed part is binding and stays unmeasured by this decision. |

## Limits

- **SC-005 is not fully evidenced.** Its timed part was not run, so the readability of cues under time pressure on the final presentation rests on the blind naming, which is untimed, and on the F004 result. T549 visual acceptance and any later timed run must take this into account. The trial pages and the now-released keys stay in the repository. A later run would need new seeds, because the keys are public now.
- **All takers except the intended owner were agents.** No human named or ordered a capture.
- **Capture limits from T545 and T562 apply.** The images are forced slot visuals on the W005-L checkpoint, not per-state save files. The emuP images were captured in the player with ScreenCapture, not with adb screencap. Windows-class captures are rendered at 96 dpi and cannot prove the 24 pt cutoff (C-98).
- **The hub dot is the weakest cue.** It is 1 to 2 px in grayscale on the small phone classes.
- **Carries**: C-99 (card tile not single-cue), C-100 (thin shortage tile), C-101 (`Editor/F006EmulatorCaptureBuild.cs` not `#if`-gated, also SECURITY-1 in [`final-acceptance.md`](final-acceptance.md)) and C-102 (P3 sheet layout downscales small cues) stay in [`p3b-followups.md`](p3b-followups.md).
