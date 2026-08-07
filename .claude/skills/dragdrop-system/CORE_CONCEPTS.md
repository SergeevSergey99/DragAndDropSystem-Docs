# Core Concepts

**Last Updated**: 2026-08-06

## 1. DragContext Is Runtime Source of Truth

`Scripts/Core/DragContext.cs`

- contains drag entry data, source and target hints
- must be treated as ephemeral runtime state
- should not be duplicated with extra lock flags unless strictly required

## 2. Rule Validation Is Layered

Validation order:
1. global rules
2. inventory-level rules
3. slot-level rules

`RuleEvaluationService` is used by the transfer service to validate candidates and swap directions.

### Domain Boundary (⚠️ CHANGED 2026-08-06)

Start rules see the item in the **source** domain; drop rules see it in the **target** domain.
`RuleEvaluationService.ValidateEntryDrop(...)` performs the conversion itself, once per entry,
before global/inventory/binding/slot drop rules run.

Consequences:
- a typed target binding (for example `MappedSlotInventoryDataBinding<TData,TAdapter>`) receives its
  own adapter type even when the item came from an inventory with a different adapter domain
- an item that cannot cross the boundary is rejected **as a rule failure with a reason**, not by a
  late "conversion failed" during mutation, so the drop preview can show it
- rule authors never call a converter themselves

Only the entry under validation is converted. Other batch entries keep their source domain until
their own turn, which is when their target is known.

## 3. Policy-Driven Transfer Behavior

`Scripts/Core/Drop/DropPolicy.cs`

Policy defines:
- blocked target handling
- partial transfer permission
- batch mode
- alternative placement order

Current policy layers:
- `DropRequestPolicy`
- `DropPolicySettings`
- `ResolvedDropPolicy`

## 4. JIT Transfer Service

`Scripts/Inventories/InventoryTransferEngine.cs`

- validates and executes against current inventory state
- checks an explicit target directly or enumerates ordered automatic candidates
- requests fresh candidates after each mutation
- processes batch entries sequentially with per-entry rollback
- creates dynamic targets through `IDynamicSlotLifecycle`
- emits events only after the current entry commits
- has no materialized plan, virtual occupancy, or batch-wide `Atomic` mode

Important current detail:
- transfer outcomes distinguish `SourceItem` and `TargetItem`
- this keeps event payloads correct for cross-inventory adapter conversion

## 5. Swap Is First-Class in Pipeline

Current flow:
- transfer service enters swap only for a blocked explicit target and a single full entry
- the forward direction is validated by the caller's `ValidateEntryDrop(...)`
- the counterpart (the item travelling the opposite way) is validated by
  `ValidateSwapCounterpart(...)`: it must be allowed to leave the target slot
  (`ValidateEntryStart`) and to land in the source slot (`ValidateEntryDrop`) — ⚠️ ADDED 2026-08-06,
  previously only domain handlers saw the reverse direction, so a swap could place an item where a
  plain drop was refused
- the same counterpart check runs inside `Probe(...)`, so a preview never promises a swap that
  execution would refuse
- counterpart validation happens before any mutation; a refusal leaves both inventories untouched
- `SwapAttempting` callback can cancel
- service mutates both placements inside the current entry transaction
- `SwapCompleted` callback runs after successful commit

## 6. Event Architecture

`UniversalInventory.TryAddToSlot()` is pure mutation, no events emitted internally.

Events are emitted only after an entry commits:
- `EmitItemAdded()` → direct `DataBinding.HandleItemAdded()` call, then `OnItemAdded`
- `EmitItemRemoved()` → direct `DataBinding.HandleItemRemoved()` call, then `OnItemRemoved`

The system prefers deferred event dispatch for consistency:
- no false-positive events on entry rollback
- predictable order for DataBinding consumers

## 7. Preview Acceptance Is Context-Aware

`Scripts/Inventories/InventoryAcceptanceRequest.cs`

- acceptance preview is no longer just `(item, count)`
- request can carry source inventory, source slot, target inventory and original drag context
- strategies validate actual candidate slots through `UniversalInventory.CanAcceptByRules(...)`

This matters for:
- area drops
- mapped-slot inventories
- cross-inventory adapter conversion

## 8. Conversion Happens Once Per Drag, Before The Rules

`Scripts/Inventories/TransferItemConversionUtility.cs`,
`Scripts/Inventories/TransferConversionSession.cs`

- conversion crosses one boundary: `ItemConverter.TryConvertOutgoing` on the source inventory, then
  `TryConvertIncoming` on the target inventory
- conversion lives on the inventory-side `ItemConverter`; `DataBinding` only provides the wiring
- it runs **before** drop rules (see §2), not after

### Drag-Scoped Conversion Session (NEW 2026-08-06)

`TransferConversionSession` memoizes converted adapters for the lifetime of one drag.

- key: `(source inventory, target inventory, source adapter reference)` — reference identity on all
  three; value equality would collapse distinct instances of a stack into one entry
- owned by `DragContext` and shared by every derived context (`WithTarget`, `WithEntries`,
  `CreateDerived` for split drops)
- failed conversions are not cached
- entries are consumed on commit: the converted object belongs to the target inventory afterwards
- a null session is a supported mode — code-driven transfers with no drag convert on the spot

The point is not only allocation. Probe, drop preview and the final mutation resolve the **same
object**, so what the player saw validated is literally what lands in the target inventory.

**Converter contract**: an `IItemAdapterConverter` must be a pure factory for the duration of a
drag — no registry writes, no id counters, no spawning. A conversion that never reaches a drop must
leave no trace.

### Tail-Slice Convention (⚠️ CHANGED 2026-08-06)

`ItemStack.Split(n)` and `ItemStack.CreateCopy(n)` take the **last** n adapters. Everything that
predicts what a transfer will move must follow the same convention, or preview and execution end up
talking about different instances:

- `DragAndDropManager.StartDrag` uses `CreateCopy(dragCount)`
- `AutoTransferService` uses `CreateCopy(dragAmount)` (it used `Take(n)` — the head — before)
- `TransferItemConversionUtility.TryCreatePreviewStack` slices
  `[DesiredCount - count, DesiredCount)`

The `DesiredCount` offset matters for an entry spread over several placements: splits eat the entry
from the tail, so the untransferred remainder is the **head** of the entry stack, and the next split
takes the tail of that remainder. Slicing the tail of the whole entry would re-validate items
already sitting in the target.

## 9. Handler Boundary

`InventoryDropProcessor` is the adapter between UI/manager targets and pipeline internals.

Responsibilities:
- resolve effective target and policy
- expose advisory probe data (`ProbeDrop(...)` stores `LastProbe`)
- execute the JIT transfer with options
- return `DropResult`

Only the processor knows the bound policy override (`DropRequestPolicy.Merge(_boundRequestOverride,
requested)`), so anything that previews a drop must reuse **its** probe rather than resolving one of
its own.

## 9a. One Probe Per Hover Drives All Drop Feedback (NEW 2026-08-06)

`Scripts/Inventories/DropVerdict.cs`, `Scripts/Inventories/DropPreviewController.cs`

- `SlotInputAdapter.OnBecomeActiveTarget` asks `DragAndDropManager.CurrentProcessor` (an
  `InventoryDropProcessor` bound to this slot) for a probe and passes it into
  `IInventoryInteraction.ShowDropPreview(slot, context, probe)`
- `DropPreviewController` turns that probe into one `DropVerdict` and keeps it for as long as the
  highlight it belongs to
- feedback visuals read it back with `IInventoryInteraction.TryGetActiveDropVerdict(slot, out ...)`;
  `CrossFeedbackSlot` does exactly that inside `Highlight(bool)`

`DropVerdict` carries `CanPlace`, `IsRejected` and a `FailureReason` suitable for a tooltip.
`TryGetActiveDropVerdict` returning false means "no opinion" (the slot is not part of a preview),
which is not the same as a refusal and must not be rendered as one.

Slots must never run their own probe: only the processor's probe carries the effective policy, and a
second one can contradict the drop it is supposed to be previewing.

## 10. Asynchronous Transfer-Wide Veto

`IAsyncTransferDomainHandler.CanStartTransferAsync(...)` is optional. The asynchronous execution
path invokes it once before the first mutation, after synchronous transfer-wide validation.
Implementations may perform remote validation or their own simulation; simulation is not required
or supplied by the core.

The synchronous execution API rejects transfers when an async handler is attached, preventing the
check from being silently bypassed. Preview remains synchronous and does not invoke this handler.

Current note:
- same-inventory slot-target fallback still avoids reshuffling unrelated slots
- same-inventory area drops exclude the source slot; dynamic inventories can create a new target slot so split/move operations do not fall back into the source

## 10. Input Layers Are Separated

Input responsibilities are split:
- `InputModalityTracker`
- `InputEventRouter`
- `SlotInputAdapter` / `InventoryDropArea`

This keeps modality detection out of transfer and slot-domain logic.

## 11. Dynamic Slot Lifecycle

`Scripts/Inventories/InventoryRuntimeCapabilities.cs`

`IDynamicSlotLifecycle` is the runtime capability for dynamic slot creation/removal:
- `TryCreateSlot(out BaseSlot)` lets the executor create an explicit target slot when a plan needs a new slot.
- `HandleSlotEmptied(BaseSlot)` trims excess empty slots after committed removals.

Layout components such as `FreeFormSlotLayout` do not mutate stacks. They react to `OnSlotCreated` and position newly created slot transforms.
