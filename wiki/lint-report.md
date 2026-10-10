# Wiki Lint Report — 2026-10-10

Scope: F005 MAQA batch 0 closeout (branch `docs/005-batch0-closeout`, after ingest commit ab651ec). Pages in scope: `f005-plan-overview.md`, `run-persistence.md`, `factory-run-bounds-and-offline-segmentation.md`, `run-balance-policy-and-f005-matrix.md`, `unity-test-and-maqa-readiness.md`, plus `INDEX.md` and `sources.md`. Config: `lint.stale_after_days` 90, `lint.auto_fix` index-and-links. 58 pages in `pages/`, 27 sources (S001–S027).

Mechanical checks over the whole wiki: `index-drift` 0 (INDEX lists exactly the 58 pages, no dangling lines). `links` 0 (no broken relative links). `citations` unknown S-ids 0. `orphans` 0. Frontmatter (`title`, `type`, `sources`, `updated`) present on all pages. Fixes applied: none needed.

Semantic checks: `contradictions` 1 open `> ⚠ conflict:` marker (outside scope, see row 1). No conflict markers in the five scoped pages. Pairwise semantic comparison was not run at Low tier; shared-source pairs were checked for markers only. `stale` 0 in scope: all five pages are `updated` 2026-10-10, the same day as their last source ingest (S018, S022–S027). Older out-of-scope stale findings from the previous pass are not re-checked here.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md (line 20) | Open `> ⚠ conflict:` marker: S001 says `sw411dp` runs at about 1.68 px/pt in either orientation; S018 (T191, 1080×2400 panel) gives 2.09 px/pt portrait and 1.69 px/pt landscape. Out of scope; owner decision (S001 vs S018). | Owner only: accept the measured S018 values, rewrite the S001 sentence, drop the marker. Lint does not resolve it. |
| 2 | citations | semantic | f005-plan-overview.md (lines 14–22) | Decision bullets D-A…D-L carry no source ID. Content is owner decisions from the F005 plan, so the citation may be S022 (`specs/005-economy-contracts`). | Add `(S022)` or the source that records the decision. Content not changed by lint. |
| 3 | citations | semantic | unity-test-and-maqa-readiness.md (lines 28–33) | Lessons-learned bullets carry no source ID (public API snapshot, merge gate, doc-only QA, ledger renumbering, PowerShell 5.1 pitfalls, batch player suite result). | Add the source (likely S017, S019 or S022) to each synthesized bullet. |
| 4 | size | structural | factory-run-bounds-and-offline-segmentation.md | About 696 words (`wc -w`, including frontmatter), over the 600-word cap. | Owner or next ingest: split into a companion page (for example the W003-B measurement and Option 1 decision) with cross-links. Not split by lint. |
| 5 | size | structural | unity-test-and-maqa-readiness.md | About 687 words, over the cap. | Split the MAQA readiness flow from the Unity test and lessons sections, with cross-links. |
| 6 | size | structural | run-persistence.md | About 675 words, over the cap. | Split the device-set pitfalls or cold start part into a companion page, with cross-links. |
| 7 | size | structural | f005-plan-overview.md | About 612 words, over the cap by a small margin. | Split the batch 0 status or review-wave section when next ingested. |

Counts: contradictions 1 (row 1, out of scope; 0 in scope). citations 2 (rows 2–3). stale 0. size 4 (rows 4–7). index-drift 0. links 0. orphans 0. unknown S-ids 0. frontmatter 0.

No page prose, claims, conflict markers, `sources.md` history, or `INDEX.md` lines were modified by this pass.

Next step: owner resolves the conflict in `ui-toolkit-hud-layout-pitfalls.md` (row 1). Then add source IDs to the scoped pages (rows 2–3).
