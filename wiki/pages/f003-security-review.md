---
title: F003 security and DevOps review
type: decision
sources: [S018]
updated: 2026-10-09
---

# F003 security and DevOps review

T131 (`reviews/security-devops-review.md`, read-only, no Unity run) found no blocking issue: **accept with advisories**. Save files, V1 files and the system clock are untrusted local input; there is no network, account or personal data in the runtime. (S018)

## Threat register decisions

- **SEC-4, accepted local risk**: the manifest carries an unkeyed SHA-256 of the data file. It detects torn writes, bit rot and partial edits, but it is not authentication, so anyone who can write the folder can forge a matching pair. Acceptable while the game has no network, multi-player state, leaderboard or real-money purchase. Revisit if any is added: a server must treat client saves as untrusted, and a key stored on the same device changes nothing. (S018)
- **OD-7 downgrade, accepted in both directions**: a Feature 002 build after an import reads only `v1/` and shows the pre-import factory (Feature 003 progress stays safe in `v2/`). A Feature 003 build never re-imports once a valid `v2/` exists, so `v1/` progress written after the import is lost to it. This never-re-import rule is also the control that stops a second credit of the V1 interval. Manual re-import means copying then deleting `v2/`, which destroys V2 progress. The ready-made release-note text sits in the review for T133 to copy. (S018)
- **DevOps**: the package identity is still `DefaultCompany` / `idle-factory-game`, so renaming later orphans desktop saves; set final names before external testers. Android Auto Backup is unconfigured and unverified. Persistence logs carry stage names only, no bytes, IDs or paths. (S018)

## T250 safety-checkpoint advisories

The desktop focus-loss checkpoint reuses the unchanged generation protocol, so it cannot tear a save, credit time twice or record a departure ([desktop focus](./desktop-focus-lifecycle.md)). Open advisories, none blocking: (S018)

| ID | Item | Disposition |
|---|---|---|
| F-1 | No rate limit: each focus loss is a synchronous promotion, and a burst shallows the previous generation | Optional; if added, put the interval in `RunPersistence.Autosave` (Core, injected clock), not the adapter |
| F-2 | No test drives a failing safety write through `RunLifecycleAdapter`; an unguarded throwing host flush would escape the Unity callback (Architect F-A1) | Add try/catch and a test |
| F-3 | No timer autosave while unfocused; a crash after a long session rolls back to the last focus-loss checkpoint plus at most 8 h | Direct consequence of the T250 owner decision; owner should acknowledge |
| F-4 | Generation lock is in-process only | Document "one instance per profile" |
| F-5 | Android Auto Backup unverified | Check at the first external Android release build |
| F-6 | Confirmed discard overwrites a damaged V2 with no copy | Accepted owner decision |

Also untested: the quit-then-focus-lost order (code guard only). (S018)

See [Save V2 and V1 import](./save-v2-and-v1-import.md), [run persistence](./run-persistence.md), [feature review](./f003-feature-review.md).
