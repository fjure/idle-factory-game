# Readability record (Feature 004, SC-001 and SC-002)

Record per research R17 and FR-010. Evidence author for the S3/S4 stage: Test/QA session (Sonnet), 2026-10-29, task T422 (#534). The author did not produce the art and gives no Art Director verdict. Independent naming transcripts and the owner's timed trials are appended here as they happen; no entry below is a verdict.

## Status

| Gate | Stage | State |
|---|---|---|
| SC-001 blind naming, grayscale and three CVD filters, zero confusions | S1 | done earlier (state cues only; see `style-spike.md` § S1) |
| SC-001 | S2 (D16, D24) | done by T420; failed on warning versus paused and the bare-plate slot conditions (see § S2) |
| SC-001 | S3/S4 (cue variants a and b, sizes 17 and 23) | named by an independent session on six sets: 9/9 in every set and filter, zero confusions (below); owner visual confirmation open |
| SC-002 timed trial, 9 of 10 within 2 s | S3/S4 | harness built and trial sets generated; **owner run pending (T423)** |

## SC-001 method (S3/S4)

1. In-game captures: the temporary viewer of T422 (never committed) renders the F003 rectangular lattice at the authoritative `n_min` (Windows classes offscreen, emulator on device) with the S3 cue sprites; details in [`style-spike.md`](style-spike.md) § S3/S4.
2. Per set, nine tiles are cropped from isolated in-game captures at `n_min` of the 320 dpi phone portrait (n = 3; extra sets at tablet-landscape n = 2 and fit-all n = 6): miner running, the five cued miner states, assembler running (all as plate-with-machine), the empty plate and the locked plate. Tile order is a seeded shuffle (3 x 3, row-major).
3. Each set is rendered in grayscale and in deuteranopia, protanopia and tritanopia (`art_pipeline.checks`, the S1/S2 tooling). Keys are written next to the sheets by the script; the evidence author did not open them.
4. An independent agent session names every tile with the key withheld. Candidate vocabulary for the reviewer: **state** (running, waiting, missing input, storage full, paused, inactive), **slot condition** (empty plate, locked plate, occupied = plate with machine), **machine type** (miner, assembler).
5. Zero confusions in grayscale and in all three CVD filters, per set, is the gate (Art Director condition 3); the evidence author then filters the answers against the keys.

Sheets and keys: `style-spike/s3/sheets/` (sets `a23`, `b23`, `a17`, `b17` required; `a23-tabLmin`, `b23-tabLmin`, `a23-tabLfit`, `b23-tabLfit` optional). Table with links: `style-spike.md` § S3/S4, "Contact sheets".

### S3/S4 blind naming (independent session)

One independent Sonnet session (read-only, keys withheld) named the 24 sheets of `a23`, `a17`, `b23`, `b17`, `a23-tabLmin`, `b23-tabLmin`; its full naming tables, certainty, cues, hard pairs and overall note, and the confusion lists, are in `style-spike.md` § S3/S4, "Blind naming (independent session)". Names were identical in all four filters; "unsure" counts as named-as-given; the only assembler tile was named "running" by assumption.

| Set | Grayscale | Deuteranopia | Protanopia | Tritanopia | Confusions | Unsure tiles | Gate |
|---|---|---|---|---|---|---|---|
| `a23` | 9/9 | 9/9 | 9/9 | 9/9 | none | 3 | PASS |
| `a17` | 9/9 | 9/9 | 9/9 | 9/9 | none | 3 | PASS |
| `b23` | 9/9 | 9/9 | 9/9 | 9/9 | none | 0 | PASS |
| `b17` | 9/9 | 9/9 | 9/9 | 9/9 | none | 0 | PASS |
| `a23-tabLmin` | 9/9 | 9/9 | 9/9 | 9/9 | none | 3 | PASS |
| `b23-tabLmin` | 9/9 | 9/9 | 9/9 | 9/9 | none | 0 | PASS |

Result: no confusion in any set and filter, so the SC-001 zero-confusion rule is met by all six named sets. In variant a, three tiles per set (storage full, missing input, waiting; `a23`, `a17`, `a23-tabLmin`) were marked unsure by the reviewer and were separable only by the small glyph (a semantic guess); variant b had none. `a23-tabLfit` and `b23-tabLfit` were not named. Facts only; the owner decides S3 and S4 in T423.

## SC-002 method (S3/S4)

The owner points out the stuck machine in 10 randomized 12-slot screenshots shown on a phone-sized view, within 2.0 s in at least 9 of 10 trials (spec SC-002). `tools/art-pipeline/trials/timed_trial.py` generates the screenshots (seeded; exactly one problem machine per screen; problem states waiting, missing input and storage full balanced 4/3/3; one or two benign distractors paused or inactive; running machines, empty and locked plates otherwise), the answer key, a local clickable timing page and a scorer. Trial sets: `style-spike/s3/trial/a` and `trial/b` (variant a and b, 23 px icons, emulator portrait 1080x2400 at `n_min` = 3). The command lines and the pass rule are in `style-spike.md` § S3/S4, "SC-002 timed-trial harness". The harness is tested (determinism, balance, one problem per screen, key consistency, pixel identity of the composition with the in-game captures, score rule): `tools/art-pipeline/tests/test_timed_trial.py`.

### S3/S4 timed trials (owner)

*Not yet run (T423).* Log, per-trial times, misses and the pass or fail result per variant will be recorded here.

## Effort per asset

Table in `style-spike.md` § S3/S4, "Effort per asset": generation seconds are measured by the pipelines; agent minutes are estimates by the T421 author; human review effort is not measured anywhere.

## Limits

Emulator and Windows offscreen captures only; no physical device (none available); the viewer approximates the HUD-free whole-screen fit, so fit-all and the 1280x720 phone follow the § S2 zoom table; the owner knows the art; naming and timed trials have not run yet.
