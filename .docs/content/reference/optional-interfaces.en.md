# Optional Interfaces

These interfaces are not required for the basic drag & drop flow.
They enable extra behavior when a specific subsystem knows how to read them.

Usually this falls into one of two categories:

- a binding wants to participate in transfer business logic
- an item adapter wants to expose extra metadata for UI or strategy logic

---

## Quick Map

| Interface | Usually implemented on | When it is used | Purpose |
|---|---|---|---|
| `ITransferDomainHandler` | usually `InventoryDataBinding` | once before the first mutation (`CanStartTransfer`), before each placement commits (`CanCommitTransfer`), and after a successful commit (`OnTransferSucceeded`) | transfer-level business validation and side effects |
| `IAsyncTransferDomainHandler` | usually `InventoryDataBinding` | once before the first mutation, async execution path only (`CanStartTransferAsync`) | external async transfer-wide veto: server, file, database |
| `IStackSizeLimitable` | `IItemAdapter` | when a stacking strategy calculates stack capacity | per-item stack limit |
| `IDescribable` | `IItemAdapter` | when UI wants to show a description | extra metadata for tooltips and similar systems |

---

## ITransferDomainHandler

`ITransferDomainHandler` is for domain logic around a transfer.
It is not a replacement for rules and not another generic validation layer.

The interface has three methods: a transfer-wide veto (`CanStartTransfer`), a
per-placement check (`CanCommitTransfer`), and a success hook (`OnTransferSucceeded`).
All three must be implemented.

It is usually implemented on a binding:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    // Transfer-wide veto, once before anything is mutated.
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
    {
        return _shopIsOpen ? RuleResult.Success() : RuleResult.Failure("The shop is closed");
    }

    // Per-placement check, before each concrete placement commits.
    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return HasEnoughMoney(context)
            ? RuleResult.Success()
            : RuleResult.Failure("Not enough money");
    }

    public void OnTransferSucceeded(TransferDomainContext context)
    {
        SpendMoney(context);
    }
}
```

### Exact Pipeline Order

The transfer engine processes entries sequentially against the real inventory state,
without precomputing a plan. For a single entry the order is:

1. `CanDrop` and the rest of the rules decide whether the drop is mechanically valid.
2. `CanStartTransfer` runs once, before the first mutation, and may veto the whole
   operation. `CanStartTransferAsync` does the same on the async execution path.
3. the engine resolves a concrete placement candidate and creates a `TransferDomainContext` for it.
4. `CanCommitTransfer` runs before that placement is mutated (source binding first, then target binding if they implement `ITransferDomainHandler`).
5. only then does the real commit happen: split, conversion, placement, rollback if needed.
6. after a placement commits, `OnTransferSucceeded` runs.
7. only after that are inventory add/remove notifications and other deferred events dispatched.

So:

- `CanStartTransfer` happens once, before any mutation of the whole operation
- `CanCommitTransfer` happens before any mutation of a specific placement
- `OnTransferSucceeded` happens after a successful commit, but before `OnItemRemoved` / `OnItemAdded`

### What TransferDomainContext Contains

`TransferDomainContext` gives the binding transfer-level context:

- `SourceInventory` / `TargetInventory`
- `SourceBinding` / `TargetBinding`
- `SourceBaseSlot`
- `PlannedTargetBaseSlot`
- `TargetBaseSlot` after commit
- `SourceItemAdapter`
- `PreviewTargetItemAdapter`
- `TargetItemAdapter` after commit
- `RequestedAmount`
- `CommittedAmount`
- `Kind`
- `IsCommitted`

This matters for cases where the business decision depends on the meaning of the operation, not just the slot contents:

- buying from a merchant
- selling an item
- moving across faction/container/authority boundaries
- validating an external restriction before commit

### Good Fits

- currency checks
- permission checks
- server validation
- side effects that are not normal data sync

### Poor Fits

- slot compatibility
- normal inventory restrictions
- regular `AddToData` / `RemoveFromData` sync
- UI preview logic

If the question is "can this item type go into this slot at all?", that is usually rules.
If the question is "can this already planned operation be committed right now?", that is a candidate for `ITransferDomainHandler`.

---

## IAsyncTransferDomainHandler

`IAsyncTransferDomainHandler` extends `ITransferDomainHandler` with an asynchronous
**transfer-wide** veto, `CanStartTransferAsync`, for when the answer cannot be produced
immediately. It is the async counterpart of `CanStartTransfer`, not of `CanCommitTransfer`.

```csharp
public class ServerInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>,
      ITransferDomainHandler,
      IAsyncTransferDomainHandler
{
    public RuleResult CanCommitTransfer(TransferDomainContext context)
        => ValidateLocalState(context);

    public void OnTransferSucceeded(TransferDomainContext context) { }

    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
        => RuleResult.Success();

    public async Task<RuleResult> CanStartTransferAsync(
        DragContext context,
        IInventory targetInventory,
        CancellationToken cancellationToken)
    {
        return await _serverApi.ValidateTransferAsync(context, cancellationToken);
    }
}
```

Use it when you must wait for:

- a server response
- a file or save-data read
- a database
- an external profile or authority layer

Important:

- `CanStartTransferAsync` is transfer-wide and runs once, before the first mutation
- it runs only on the asynchronous execution path; a synchronous transfer is rejected
  when an async handler is present, so the check is never silently skipped
- a failure at the async stage cancels the whole transfer without mutating inventories

If the check is purely local and fast, `CanStartTransfer` / `CanCommitTransfer` are enough.

---

## IStackSizeLimitable

`IStackSizeLimitable` lets an item adapter define its own stack cap:

```csharp
public class AmmoAdapter : IItemAdapter, IStackSizeLimitable
{
    public int MaxStackSize => 120;
}
```

This is useful for systems such as:

- Craft-style inventories where different item types have different stack caps
- RPG inventories where potions stack to 20, arrows to 999, weapons to 1
- survival/crafting inventories where containers and tools do not stack but resources do

### Where It Is Actually Read

The interface is read by inventory strategies when they calculate stack capacity:

- `StackableItemStrategy`
- `SeparableStacksStrategy`
- the transfer engine through `UniversalInventory.GetMaxStackSizeForItem(...)`

### Important Note About _allowItemStackOverride

In the current implementation, `IStackSizeLimitable` is used only when `_allowItemStackOverride` is enabled on `UniversalInventory`.

The current behavior is:

- if `_allowItemStackOverride == false`, only inventory `_maxStackSize` is used
- if `_allowItemStackOverride == true` and the item implements `IStackSizeLimitable`, the item's `MaxStackSize` fully replaces inventory `_maxStackSize`

So in the current code this is not "the item can only raise the cap".
It is a full item-level override, and it can be:

- lower than the inventory limit
- equal to the inventory limit
- higher than the inventory limit

Examples:

- inventory `_maxStackSize = 20`, `_allowItemStackOverride = false`, item `MaxStackSize = 99` -> effective limit is still `20`
- inventory `_maxStackSize = 20`, `_allowItemStackOverride = true`, item `MaxStackSize = 99` -> effective limit is `99`
- inventory `_maxStackSize = 20`, `_allowItemStackOverride = true`, item `MaxStackSize = 5` -> effective limit is `5`

If you need different semantics, such as "an item may only lower the cap" or "an item may exceed the cap only upward", that requires a custom strategy.

---

## IDescribable

`IDescribable` does not affect the core transfer pipeline.
It is a simple example of extending `IItemAdapter` with extra data for UI.

```csharp
public class ItemAdapter : IItemAdapter, IDescribable
{
    public string Description => _item.Description;
}
```

The standard use case in the asset right now is:

- `DefaultTooltipView` shows `Description` if the adapter implements `IDescribable`

But the idea is broader:

- custom tooltip
- inspect panel
- hover card
- context menu details
- any other optional UI system

So `IDescribable` is better understood not as a special required tooltip interface,
but as a pattern for extending adapters with small focused interfaces.

If plain `IItemAdapter` is no longer enough, you can add interfaces like:

- `IDescribable`
- `IFilterable`
- `ISortable`
- your own `IItemStatsProvider`, `IRarityProvider`, `IFlavorTextProvider`, and so on

The core inventory does not depend on them.
Only the systems that need them should query them.
