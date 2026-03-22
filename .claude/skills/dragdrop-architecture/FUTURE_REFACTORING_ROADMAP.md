# Future Refactoring Roadmap

**Last Updated**: 2026-03-22

This document collects architecture changes discussed during the recent refactoring pass.
It focuses on future work, why it is needed, and how it can be implemented incrementally
without breaking the current drag-and-drop pipeline.

## Current State

Recent changes already moved the system in a better direction:
- transfer preview now supports operation objects (`InventoryAcceptanceRequest`)
- target-side preview conversion happens before planning/execution
- mapped-slot preview no longer relies on ad-hoc `null` guards in feature bindings
- operation objects were extracted into dedicated files
- Phase 1 has started: strategy capabilities are now split at the dependency level inside `UniversalInventory`

Main remaining pressure points:
- `IInventoryStrategy` is too wide
- transfer orchestration is still split between `TransferPlanExecutor` and `InventoryTransferService`
- domain-specific workflows like trading still piggyback on `OnItemAdded` / `OnItemRemoved`
- there is no clean extension point for server-authoritative or transaction-like flows

## Recommended Priority

1. Split `IInventoryStrategy` into narrower capabilities
2. Move item conversion out of `DataBinding`
3. Add transfer-domain hooks for post-success logic such as trading
4. Merge `InventoryTransferService` into `TransferPlanExecutor`
5. Add transaction/authority abstraction for server-backed flows only if server authority is actually needed
6. Consider extracting dynamic slot lifecycle out of `UniversalInventory` if it keeps growing

## 1. Split `IInventoryStrategy`

### Problem

`IInventoryStrategy` currently mixes four different responsibilities:
- placement and removal logic
- acceptance preview logic
- drag amount policy
- inventory content queries

This makes `UniversalInventory` depend on one large abstraction and forces every concrete strategy
to know about concerns that are only needed by planner, UI drag logic, or slot placement.

### Goal

Replace the single strategy interface with smaller capability interfaces.

### Proposed Interfaces

```csharp
public interface IPlacementStrategy
{
    bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);
    bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);
    bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, Action ensureFreeSlots, SlotOperationContext operationContext);
    bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item);
    bool UsesPerItemSlotPlanning { get; }
}
```

```csharp
public interface IAcceptanceStrategy
{
    bool CanAcceptItem(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot);
    int GetAcceptableCount(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab);
}
```

```csharp
public interface IDragPolicy
{
    int ResolveDragAmount(int stackCount, UniversalInventory.DragAmountType dragAmount, int customDragAmount);
}
```

```csharp
public interface IInventoryQueryStrategy
{
    int GetItemCount(List<ISlot> slots, IInventoryItem item);
    bool Contains(List<ISlot> slots, IInventoryItem item);
}
```

### Concrete Implementations

Two implementation styles are acceptable.

Option A: keep one concrete class implementing multiple small interfaces
- `UniqueInventoryBehavior : IPlacementStrategy, IAcceptanceStrategy, IDragPolicy, IInventoryQueryStrategy`
- `StackableInventoryBehavior : IPlacementStrategy, IAcceptanceStrategy, IDragPolicy, IInventoryQueryStrategy`
- `SeparableInventoryBehavior : IPlacementStrategy, IAcceptanceStrategy, IDragPolicy, IInventoryQueryStrategy`

Option B: split even concrete implementations
- `UniquePlacementStrategy`
- `UniqueAcceptanceStrategy`
- `SingleItemDragPolicy`

Recommended starting point: Option A. It gives better boundaries without exploding file count.

### UniversalInventory After Split

Instead of:

```csharp
private IInventoryStrategy _strategy;
```

Use:

```csharp
private IPlacementStrategy _placementStrategy;
private IAcceptanceStrategy _acceptanceStrategy;
private IDragPolicy _dragPolicy;
private IInventoryQueryStrategy _queryStrategy;
```

### Benefits

- planner depends on acceptance logic only
- executor depends on placement logic only
- drag UI depends on drag policy only
- read-only inventory queries stop piggybacking on mutation-oriented interfaces
- strategy API becomes easier to understand and extend
- future custom behaviors can override only the capability they need

### Current Implementation Status

Already done:
- `IPlacementStrategy`, `IAcceptanceStrategy`, `IDragPolicy`, and `IInventoryQueryStrategy` exist as separate interfaces
- `UniversalInventory` now keeps separate references for placement, acceptance, drag, and query capabilities
- planner/service runtime calls use narrow capability references instead of the wide strategy where possible

Still intentionally kept for compatibility:
- `IInventoryStrategy` remains as an aggregate interface
- concrete strategy classes still implement the aggregate interface through `InventoryStrategyBase`
- external custom code using `SetStrategy(IInventoryStrategy strategy)` keeps working unchanged

## 2. Move Item Conversion Out of DataBinding

### Problem

`InventoryDataBindingBase` currently owns both:
- data synchronization (`CreateAdapter`, `ExtractData`, list/mapped sync)
- transfer conversion (`ConvertIncomingItem`, `ConvertOutgoingItem`)

This mixes UI/data binding concerns with inter-inventory domain transformation.

### Goal

Keep `DataBinding` responsible only for synchronizing inventory UI with external data.
Move transfer conversion into a dedicated inventory-side collaborator.

### Proposed Interface

```csharp
public interface IInventoryItemConverter
{
    bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted);
    bool TryConvertOutgoing(IInventoryItem item, out IInventoryItem converted);
}
```

### Default Implementation

Provide an identity converter by default so inventories without conversion do not require explicit setup.

```csharp
public sealed class IdentityInventoryItemConverter : IInventoryItemConverter
{
    public bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted)
    {
        converted = item;
        return item != null;
    }

    public bool TryConvertOutgoing(IInventoryItem item, out IInventoryItem converted)
    {
        converted = item;
        return item != null;
    }
}
```

### Example Concrete Converters

- `PlayerInventoryItemConverter`
- `MerchantInventoryItemConverter`
- `EquipmentInventoryItemConverter`

These classes still know their feature-specific types:
- `TradableItemModel`
- `TradableItemModelAdapter`
- `TradableSoAdapter`
- any future custom adapters/models

The point is not to remove type-specific knowledge.
The point is to move it out of UI binding and into transfer/domain infrastructure.

### Integration Options

Option A: store converter directly on `UniversalInventory`

```csharp
public IInventoryItemConverter ItemConverter { get; private set; }
```

Option B: expose converter through a lightweight provider on the inventory owner component

```csharp
public interface IInventoryItemConverterProvider
{
    IInventoryItemConverter GetConverter();
}
```

Recommended starting point: Option A.

### Migration Plan

1. Introduce `IInventoryItemConverter`
2. Mirror current `ConvertIncomingItem` / `ConvertOutgoingItem` implementations into converters
3. Update `TransferItemConversionUtility` and `UniversalInventory` preview/commit conversion to use converters
4. Remove conversion methods from `InventoryDataBindingBase`

### Benefits

- cleaner separation between transfer domain and UI sync
- easier to reason about inventory compatibility
- makes future server/domain hooks independent from bindings

## 3. Add Transfer-Domain Hooks

### Problem

Some workflows do not fit cleanly into independent `remove` and `add` reactions.

Current example: trading.
- purchase/sale logic is currently split across `OnItemAddedToUI` and `OnItemRemovedFromUI`
- `TradingEconomyManager.TryBuyFromMerchant()` already exists as a centralized operation
- current event model makes it awkward to express "this successful transfer means a purchase transaction happened"

### Goal

Keep the default inventory sync flow, but allow feature modules to attach domain-specific logic
to a successful transfer as a whole.

### Important Constraint

Do **not** call current `TradingEconomyManager.TryBuyFromMerchant()` directly after a successful transfer.
That method already performs data mutations:
- removes from merchant
- adds to player
- transfers money

The drag-and-drop pipeline already performs remove/add via inventories and bindings.
Calling it as-is would duplicate data changes.

### Proposed Abstraction

```csharp
public interface ITransferDomainHandler
{
    RuleResult CanCommitTransfer(TransferDomainContext context);
    void OnTransferSucceeded(TransferDomainContext context);
}
```

### Commit Gate Semantics

`ITransferDomainHandler.CanCommitTransfer(...)` should be treated as a **domain veto stage**, not as a replacement
for `RuleValidator` and not as a generic extra rule layer.

Recommended execution order:
1. built-in/global/inventory/slot rules
2. domain validation hook
3. commit / execution
4. domain success hook

This keeps responsibilities clean:
- `RuleValidator` handles inventory mechanics and drop legality
- `ITransferDomainHandler.CanCommitTransfer(...)` handles business meaning of the transfer

### TransferDomainContext Should Include

- source inventory
- target inventory
- source binding
- target binding
- source slot
- target slot
- source item
- target item
- amount
- transfer kind (move, split, merge, swap)

`TransferDomainContext` should be created once per transfer operation at executor level and then reused.
It should not be reconstructed independently by each hook stage.

### How Trading Should Use It

Split current centralized methods into smaller pieces:

- `CanBuyFromMerchant(...)`
- `ApplyPurchaseMoneyEffects(...)`
- `CanSellToMerchant(...)`
- `ApplySellMoneyEffects(...)`

Inventory mutations remain in the transfer pipeline.
Domain hook applies only side effects not already covered by item movement.

### Suggested Invocation Point

Best place: after successful execution in `TransferPlanExecutor`, before final completion notification.

This keeps the hook:
- above low-level slot mutation
- below planner validation
- aware of the final committed transfer outcome

### Benefits

- supports workflows that need transfer-level semantics
- preserves current remove/add-based data binding model
- avoids packing game-specific logic into generic inventory events

### Current Implementation Status

Already done:
- `TransferDomainContext` and `ITransferDomainHandler` exist
- `TransferPlanExecutor` runs domain validation before commit and defers success hooks until the whole plan succeeds
- Demo2 Trading money side effects were moved out of item-added/item-removed reactions into transfer-level hooks
- swap path now also participates in domain validation/success hooks
- trading demo no longer keeps money checks in rule-layer `CanDrop`/`CanStartDrag`

Still pending:
- generic feature modules still discover handlers through `DataBinding` ownership
- trading validation still partially exists in classic binding rules as an additional safety layer

## 4. Add Transaction / Authority Layer For Server Flows

### Problem

Some developers will need server-backed inventories.
In that world, local remove/add plus fire-and-forget events are not enough:
- server request may fail
- local state may need rollback
- client may be predictive or pessimistic

This cannot be modeled cleanly as "send request inside `OnItemAdded` / `OnItemRemoved`".

This should be treated as a separate milestone, not as part of the baseline refactoring,
unless server-authoritative inventory flows are an immediate project requirement.

### Core Principle

Server integration must extend a **transfer transaction**, not individual local item events.

### Recommended Transfer Stages

1. `Plan`
2. `Validate`
3. `Authorize / Commit`
4. `Finalize`
5. `Rollback` on failure

### Proposed Abstraction

Minimal version:

```csharp
public interface ITransferAuthority
{
    Task<TransferCommitResult> TryCommitTransferAsync(TransferTransactionContext context, CancellationToken ct);
}
```

Possible future richer version:

```csharp
public interface ITransferTransactionHandler
{
    Task<TransferAuthorizationResult> AuthorizeAsync(TransferTransactionContext context, CancellationToken ct);
    Task<TransferCommitResult> CommitAsync(TransferTransactionContext context, CancellationToken ct);
    Task RollbackAsync(TransferTransactionContext context, CancellationToken ct);
}
```

### TransferTransactionContext Should Include

- source inventory id
- target inventory id
- source slot id
- target slot id
- source item
- target item
- amount
- operation type
- correlation id / request id
- optional full plan snapshot

`TransferTransactionContext` should also be created once per transaction and passed through the authority pipeline.
Avoid rebuilding it separately in validation, commit, and rollback stages.

### Execution Modes

#### Local Authoritative

Current behavior.
- validate locally
- mutate locally
- emit final events immediately

#### External Authoritative - Pessimistic

Recommended first server-ready mode.
- validate locally
- build transfer transaction context
- send request to authority
- only mutate local state after server success

Pros:
- simpler consistency model
- no local rollback of visible state

Cons:
- less responsive UX

#### External Authoritative - Optimistic

Future advanced mode.
- validate locally
- apply locally with snapshot
- send request to authority
- rollback on failure

Pros:
- responsive UX

Cons:
- more complexity
- needs pending state and rollback-safe events

This mode should be considered optional and deferred until there is a concrete product need for it.
For many inventory-driven games, pessimistic server authority is sufficient and much safer.

### Event Model Changes For Server Mode

Current `OnItemAdded` / `OnItemRemoved` should remain committed-state events.

If optimistic mode is ever added, introduce transaction events such as:
- `OnTransferPending`
- `OnTransferCommitted`
- `OnTransferRolledBack`

This avoids feature code mistaking a predicted local change for a final committed result.

### Suggested Initial Implementation

1. Introduce `TransferTransactionContext`
2. Introduce `ITransferAuthority`
3. Add async execution branch to `TransferPlanExecutor`
4. Support `Local` and `ExternalPessimistic` modes first
5. Keep optimistic mode out until the transaction lifecycle is stable

### UI Impact

Async authority introduces UI concerns:
- drag/drop completion may need pending visuals
- input may need temporary locking for the affected slots
- user feedback for rejected server commits must be explicit

This is another reason to keep server authority as a dedicated milestone.

## 5. Merge InventoryTransferService Into TransferPlanExecutor

### Problem

Transfer orchestration is still split:
- `TransferPlanner` builds the plan
- `TransferPlanExecutor` runs plan entries
- `InventoryTransferService` performs concrete transfer primitive with rollback

This leaves the flow harder to read than necessary.

### Goal

Reduce the execution pipeline to:
- planner
- executor
- inventory facade

### Suggested Change

Move `InventoryTransferService` logic into `TransferPlanExecutor` as internal execution helpers:
- target placement
- acceptable count handling
- snapshot rollback for a single transfer entry

Possible helper names:
- `ExecutePlannedAllocation(...)`
- `ExecuteSingleTransfer(...)`
- `TryPlaceTransferStack(...)`

Important constraint:
- rollback logic should remain isolated in executor-level helpers
- do not spread rollback branches across the main execution path

### Why It Makes Sense Now

Recent refactors already introduced operation objects and clarified preview conversion.
That means `InventoryTransferService` is now mostly an intermediate execution layer rather than a distinct domain boundary.

### Benefits

- simpler execution flow
- fewer classes to jump through during debugging
- easier future integration with domain hooks and server authority

## 6. Dynamic Slot Lifecycle Extraction (Lower Priority)

### Current Concern

`UniversalInventory` still contains:
- slot creation
- free-slot maintenance
- dynamic trimming
- transfer-facing mutation
- strategy selection
- event dispatch

That is a lot of responsibilities for one class.

### Important Note

This does **not** mean `UniversalInventory` should stop being the inventory abstraction.
It should remain the public inventory facade and continue implementing `IInventory`.

### Possible Internal Collaborator

```csharp
public sealed class DynamicSlotController
{
    bool CanCreateMoreSlots();
    void EnsureFreeSlots(List<ISlot> slots);
    ISlot CreateSlot();
    void HandleSlotEmptied(List<ISlot> slots, ISlot slot);
}
```

### Organizational Model

`UniversalInventory` still owns:
- public API
- slot list
- strategy references
- event emission

`DynamicSlotController` owns:
- creation/removal rules for dynamic slots
- prefab-based slot lifecycle
- min/max free slot maintenance

### When To Do This

Only if `UniversalInventory` continues to grow after the higher-priority refactors above.
This is useful, but not urgent.

## Suggested Phased Implementation Plan

### Phase 1

- split `IInventoryStrategy` into capability interfaces
- add `IInventoryQueryStrategy` for read-only inventory queries
- keep existing concrete behavior classes implementing multiple capabilities
- do not change runtime semantics

### Phase 2

- introduce `IInventoryItemConverter`
- move conversion responsibilities out of `DataBinding`
- keep `TransferItemConversionUtility` as the central pipeline entry point

Current status:
- core converter API exists on `UniversalInventory`
- `IdentityInventoryItemConverter` is the default implementation
- `InventoryDataBindingBase` now configures inventory converter via `CreateItemConverter()`
- old `ConvertIncomingItem` / `ConvertOutgoingItem` remain only as compatibility fallback through `LegacyDataBindingItemConverter`
- Demo2 Trading converters were moved into dedicated converter classes

### Phase 3

- add `TransferDomainContext`
- add optional `ITransferDomainHandler`
- migrate trading demo from `OnItemAdded` / `OnItemRemoved` money side effects to transfer-level hooks

### Phase 4

- merge `InventoryTransferService` into `TransferPlanExecutor`
- simplify operation ordering and rollback ownership

### Phase 5

- add `TransferTransactionContext`
- add `ITransferAuthority`
- support external pessimistic execution mode

### Phase 6

- revisit `UniversalInventory`
- extract dynamic slot controller if the class remains too broad

## Design Principles To Preserve

- planner should remain pure and side-effect free
- executor should remain the owner of commit/rollback semantics
- feature code should plug in through explicit extension points, not hidden event coupling
- data binding should synchronize data, not orchestrate transfer transactions
- server integration should operate on transfer transactions, not separate local item-added/item-removed callbacks
- default implementations should exist for optional infrastructure pieces such as converters
- operation contexts should be created once and reused across the pipeline
