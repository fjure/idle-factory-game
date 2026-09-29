# Wiki Lint Report — 2026-09-29

Full pass over 19 pages after the S008 re-ingest (wave B: T075–T077) and the new source S017 (`scripts/resource-lease.ps1`).

| Check | Result |
|---|---|
| index-drift | clean: every page is indexed, and no index line points at a missing file |
| links | clean: no broken relative links, and no unknown source IDs |
| orphans | clean: every page is linked from another page |
| contradictions | clean: no `⚠ conflict` markers. The new `json-content-migration-evidence.md` agrees with `json-content-authoring.md`, `player-level-test-gaps.md` and `local-checkpoints-and-offline-return.md` on bootstrap order, the player seam and Save V1 behavior |
| stale | clean: every page citing S008 or S017 has `updated: 2026-09-29`, and no page is older than 90 days |
| citations | clean: the only paragraphs without a citation are numbered step lists under a cited lead paragraph (`android-emulator-profiling.md`, `json-content-migration-evidence.md`) |

No findings. No mechanical fixes were needed.
