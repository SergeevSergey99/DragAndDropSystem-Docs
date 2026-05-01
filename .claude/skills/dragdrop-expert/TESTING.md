# Testing Scenarios

**Last Updated**: 2026-05-01

## Core Manual Tests

### Single Drag/Drop
- [ ] drag A -> empty target
- [ ] drag A -> occupied target with `BlockedTargetBehavior=Reject`
- [ ] drag A -> occupied target with `BlockedTargetBehavior=FindAlternative`
- [ ] drag A -> occupied target with `BlockedTargetBehavior=Swap`
- [ ] drag to same slot (must be blocked)

### Batch Transfer
- [ ] batch with `BatchMode=Atomic`, one invalid entry -> full rollback
- [ ] batch with `BatchMode=BestEffort`, one invalid entry -> partial success
- [ ] `AllowPartial=false` rejects partial placement
- [ ] `AllowPartial=true` allows partial placement

### Swap
- [ ] successful swap with `BlockedTargetBehavior=Swap`
- [ ] canceled swap via `InventorySwapContext.Cancel = true`
- [ ] swap rejected by reverse-direction rules
- [ ] verify `OnSwapCompleted` fires only on success

### Events and Rollback
- [ ] atomic failure emits no false transfer/swap completion events
- [ ] best effort emits events only for successful entries
- [ ] DataBinding receives direct notifications (HandleItemAdded/HandleItemRemoved)
- [ ] external event subscribers (OnItemAdded/OnItemRemoved) receive consistent payloads
- [ ] IsSyncing guard prevents re-entrant callbacks during ReloadUI
- [ ] cross-inventory add/remove events use correct `SourceItem` vs `TargetItem` payloads

## Integration Tests

### Drop Targets
- [ ] slot target (`SlotInputAdapter`) uses same pipeline
- [ ] area target (`InventoryDropArea`) uses same pipeline
- [ ] manager fallback handler path behaves identically

### Demo Flows
- [ ] Demo1 basic item movement and rules
- [ ] Demo2 trading restrictions + swap constraints
- [ ] Demo2 merchant -> player / equipment adapter conversion remains correct
- [ ] Demo3 loot/world interaction still works

## Regression Focus

After transfer/swap changes always re-check:
- [ ] `TransferPlanner` output for policy matrix
- [ ] `TransferPlanExecutor` atomic rollback
- [ ] `InventoryAcceptanceRequest` path for area-drop and planner preview
- [ ] `InventoryDropProcessor` effective policy resolution
- [ ] same-inventory `FindAlternative` leaves item in place
- [ ] no compile errors due to delegate/nullability syntax on Unity C# profile
