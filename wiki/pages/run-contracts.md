---
title: Run contracts
type: component
sources: [S018]
updated: 2026-09-30
---

# Run contracts

T100 adds the contract commands (`RunCommands.Contracts.cs`) and `ContractsSnapshot` (`RunSnapshots.Contracts.cs`). They follow the command pattern of [run commands and snapshots](./run-commands-and-snapshots.md). Offers are unlocked through [run unlocks](./run-unlocks.md), so the commands check `state.IsUnlocked(offerId)`. A fresh state unlocks its offers only at its first settle. (S018)

## Commands

- `AcceptContract` needs an unlocked offer and no active contract. It fixes `requiredUnits` from the quantity table at the current completion count. For `timedBar` it also sets the deadline to `timeline + durationMs`. (S018)
- `DeliverContract` needs `timeline ≤ deadline` (inclusive) and enough stock. It consumes exactly the required units and grants cash, RP and the Items due at that index once, in one checked transaction, then increments the count. Its settle pre-check subtracts the delivered stock and includes the reward cash. (S018)
- `AbandonContract` applies to untimed contracts only and gives no reward and no penalty; the count is unchanged (OD-4). (S018)
- Quantity and rewards are functions of the completion count of the current run. Cycling accept and abandon therefore gains nothing. (S018)
- A locked or unknown offer and abandoning a timed contract reject with `command.invalid`, because the contract lists no code for them. The snapshot exposes `Unlocked` and `CanAbandon`, so the UI never offers either action. (S018)

## Expiry outcome

- Expiry grants nothing. `ContractBoard.TryExpire` keeps the outcome in `LastExpired`, a value type so the expiry path stays allocation-free, and the next accept clears it. While it is set, `DeliverContract` reports `contract.expired`. (S018)
- `LastExpired` is not persisted. After a load, the same situation reports `contract.none-active`, and the outcome card is gone. It is also sticky, so a non-null value after an offline reconcile is ambiguous. The offline summary (T105) must compare the active contract before and after the reconcile instead. (S018)
- Auto-sell of the contract resource consumes stock before the player delivers. The contracts panel should hint at switching it off. (S018)
