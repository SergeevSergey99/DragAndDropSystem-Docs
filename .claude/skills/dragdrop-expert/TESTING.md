# Testing Scenarios

**Last Updated**: 2026-03-21

## Core Manual Tests

### Single Drag/Drop
- [ ] drag A -> empty target
- [ ] drag A -> occupied target with `OccupiedTarget=Reject`
- [ ] drag A -> occupied target with `OccupiedTarget=TryAlternativeSlots`
- [ ] drag A -> occupied target with `OccupiedTarget=TrySwap`
- [ ] drag to same slot (must be blocked)

### Batch Transfer
- [ ] batch with `BatchExecution=Atomic`, one invalid entry -> full rollback
- [ ] batch with `BatchExecution=BestEffort`, one invalid entry -> partial success
- [ ] `Capacity=RejectAll` rejects partial placement
- [ ] `Capacity=Partial` allows partial placement

### Swap
- [ ] successful swap with `TrySwap` policy
- [ ] canceled swap via `InventorySwapContext.Cancel = true`
- [ ] swap rejected by reverse-direction rules
- [ ] verify `OnSwapCompleted` fires only on success

### Events and Rollback
- [ ] atomic failure emits no false transfer/swap completion events
- [ ] best effort emits events only for successful entries
- [ ] DataBinding receives direct notifications (HandleItemAdded/HandleItemRemoved)
- [ ] external event subscribers (OnItemAdded/OnItemRemoved) receive consistent payloads
- [ ] IsSyncing guard prevents re-entrant callbacks during ReloadUI

## Integration Tests

### Drop Targets
- [ ] slot target (`SlotInputAdapter`) uses same pipeline
- [ ] area target (`InventoryDropArea`) uses same pipeline
- [ ] manager fallback handler path behaves identically

### Demo Flows
- [ ] Demo1 basic item movement and rules
- [ ] Demo2 trading restrictions + swap constraints
- [ ] Demo3 loot/world interaction still works

## Regression Focus

After transfer/swap changes always re-check:
- [ ] `TransferPlanner` output for policy matrix
- [ ] `TransferPlanExecutor` atomic rollback
- [ ] `InventoryDropProcessor` effective policy resolution
- [ ] no compile errors due to delegate/nullability syntax on Unity C# profile
