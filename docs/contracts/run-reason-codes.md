# Run reason codes

This table is the authoritative list of the stable reason codes of the run application. `ReasonCodeContractTests` pins it: the set of codes in this table must equal the set of public string constants of `RunReasons`, `RunPersistenceReasons` and `RunOfflineSummary.ValueLimitCode`, and every code whose Source is `RunReasons` (a command rejection) must have player copy in `RunRejectionCopy`. UI copy keys off these strings, so a code never changes once it ships.

How to change it: a task that introduces a code adds the constant in code and one row here in the same commit. F005 codes (`sell.quantity-invalid`, `build.price.changed`, `contract.cooldown`, `contract.bonus.unavailable`, `contract.capacity`, `storage.full`, `contract.dropped`; see `specs/005-economy-contracts/contracts/run-application-f005.md`) are appended by the task that adds each constant, not before.

Parsing rule (used by the test): every line of this file that starts with `` | `<code>` | <Source> | `` is parsed as a table row, wherever it appears, and its second column is the Source. Other text is ignored, so keep code-shaped table lines out of the prose. The `import.*` family stays in `specs/003-*/contracts/save-v2-and-v1-import.md`.

Sources: `RunReasons` is a command rejection (shown next to the control, needs `RunRejectionCopy`). `RunPersistenceReasons` is a persistence command outcome (constant in `RunPersistenceReasons`; shown by the startup and prestige panels with their own copy). `RunOfflineSummary` is the summary code of a forfeited offline credit (not a rejection). Alias rule: when a code is defined in both `RunReasons` and `RunPersistenceReasons` (`transition.pending`, `command.invalid`), its Source is `RunReasons`.

| Code | Source | Since |
|---|---|---|
| `slot.occupied` | RunReasons | F003 |
| `slot.unavailable` | RunReasons | F003 |
| `machine.locked` | RunReasons | F003 |
| `cash.insufficient` | RunReasons | F003 |
| `level.max` | RunReasons | F003 |
| `confirmation.required` | RunReasons | F003 |
| `priority.not-permutation` | RunReasons | F003 |
| `resource.locked` | RunReasons | F003 |
| `contract.active-exists` | RunReasons | F003 |
| `contract.none-active` | RunReasons | F003 |
| `contract.expired` | RunReasons | F003 |
| `stock.insufficient` | RunReasons | F003 |
| `item.none` | RunReasons | F003 |
| `item.target.invalid` | RunReasons | F003 |
| `effect.conflict` | RunReasons | F003 |
| `research.prerequisite` | RunReasons | F003 |
| `rp.insufficient` | RunReasons | F003 |
| `overdrive.cooling-down` | RunReasons | F003 |
| `overdrive.active` | RunReasons | F003 |
| `ip.insufficient` | RunReasons | F003 |
| `prestige.below-threshold` | RunReasons | F003 |
| `transition.pending` | RunReasons | F003 |
| `value.limit` | RunReasons | F003 |
| `lifecycle.blocked` | RunReasons | F003 |
| `build.price.changed` | RunReasons | F005 (added by #383) |
| `sell.quantity-invalid` | RunReasons | F005 (added by #380) |
| `command.invalid` | RunReasons | F003 (added to the contract by #355) |
| `checkpoint.content` | RunPersistenceReasons | F003 (added by #355) |
| `discard.unavailable` | RunPersistenceReasons | F003 (added by #355) |
| `transition.committed` | RunPersistenceReasons | F003 |
| `persistence.failed` | RunPersistenceReasons | F003 |
| `return.blocked` | RunPersistenceReasons | F003 |
| `lifecycle.noop` | RunPersistenceReasons | F003 |
| `checkpoint.none` | RunPersistenceReasons | F003 |
| `checkpoint.invalid` | RunPersistenceReasons | F003 |
| `checkpoint.version` | RunPersistenceReasons | F003 |
| `offline.value-limit` | RunOfflineSummary | F003 |
