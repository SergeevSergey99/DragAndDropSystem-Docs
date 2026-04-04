# Logs and Debugging

This page helps you quickly identify which phase of the pipeline failed.

Core idea:

- `planner` chooses a valid plan
- `executor` handles commit, conversion, and rollback
- `rules` handle mechanical constraints
- `domain hooks` handle business veto right before commit

---

## Short log map

| Where the log appears | What it usually means |
|---|---|
| `RuleResult` | one specific rule check was rejected |
| `TransferPlanner` | planning or target-selection problem |
| `InventoryDropProcessor` | planner failed to build a valid plan |
| `TransferPlanExecutor` | commit, conversion, swap, or rollback problem |
| `GetAcceptableCount` | inventory-wide slot search |
| `CanCommitTransfer` / domain validation | business logic vetoed the commit |

---

## How to read common logs

### `[RuleResult] Validation failed: ...`

This is the rejection of one concrete rule branch.

Important:

- by itself it does not always mean a bug
- sometimes it is a normal rejection of a trial candidate slot
- the call stack matters: look at who initiated the check

If the log comes from:

- `MappedSlotInventoryDataBinding.CanDrop()` -> usually adapter type or slot compatibility
- `CanStartDrag()` -> wrong adapter type in the source slot or source-side drag veto

### `[InventoryDropProcessor] plan failed: ...`

The planner failed to produce a valid plan.
Execution has not started yet.

Common causes:

- target slot is invalid
- policy does not allow a fallback
- there is no valid candidate slot

### `[TransferPlanExecutor] ...`

This is execution-stage logging.
Planning already succeeded, and the problem happened during:

- domain validation
- split/remove
- outgoing/incoming conversion
- placement into the target inventory
- swap commit
- rollback

### `[InventoryName] GetAcceptableCount: ...`

This is inventory-wide acceptance search.

If you expected a direct slot drop and still see this log, usually check:

- whether a concrete `targetSlot` actually existed
- whether the operation fell into `FindAlternative`
- whether this was an area-drop path

---

## Fast diagnosis by phase

### 1. Drag start

Look at:

- `OnDragAttempting`
- `ValidateStartDrag`
- binding `CanStartDrag`

Typical causes:

- source slot is empty
- slot contains the wrong adapter type
- source binding forbids dragging

### 2. Preview / planning

Look at:

- `TransferPlanner`
- `ValidateDrop`
- `InventoryAcceptanceRequest`
- `GetAcceptableCount`

Typical causes:

- target-side conversion failed
- slot rules reject the target adapter
- planner searches candidates more broadly than expected

### 3. Domain validation

Look at:

- `CanCommitTransfer`
- `CanCommitTransferAsync`
- `ValidateDomainHandlers`

Typical causes:

- money
- access rights
- server veto
- external synchronous/asynchronous validation

### 4. Execution

Look at:

- `TransferPlanExecutor`
- conversion utility
- `TryAddToSlot` / `TryAddStack`

Typical causes:

- conversion failed during commit
- placement failed
- rollback restored the previous state

### 5. Swap

Look at:

- `RequiresSwap`
- `ValidateSwapRules`
- `OnSwapAttempting`
- `OnSwapCompleted`

Typical causes:

- one swap direction does not pass rules
- swap was implemented as a raw exchange instead of a conversion-aware commit
- after the first swap, a slot still contains a foreign adapter type

---

## Practical patterns

### Preview passed, commit failed

That usually means the issue is not in rules, but in execution or domain hooks.

Check:

- `CanCommitTransfer`
- `CanCommitTransferAsync`
- executor conversion
- placement / rollback

### Warnings appear for other slots

That usually means some path triggered inventory-wide search.

Check:

- `GetAcceptableCount`
- `FindAlternative`
- area-drop
- incorrect routing for a direct slot drop

### First swap succeeds, second swap fails

This almost always means the slot stores the wrong adapter type after the first swap.

---

## Read this together with

- [Troubleshooting](troubleshooting.md) — symptom -> cause -> where to look
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — phase order
- [Cookbook: Item Conversion](../architecture/item-conversion-cookbook.md) — adapter-boundary problems
