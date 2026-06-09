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
| `ITransferDomainHandler` | usually `InventoryDataBinding` | after rules/planning and immediately before commit, then again after successful completion | transfer-level business validation and side effects |
| `IAsyncTransferDomainHandler` | usually `InventoryDataBinding` | after sync `CanCommitTransfer` and before commit | external async checks: server, file, database |
| `IStackSizeLimitable` | `IItemAdapter` | when a stacking strategy calculates stack capacity | per-item stack limit |
| `IDescribable` | `IItemAdapter` | when UI wants to show a description | extra metadata for tooltips and similar systems |

---

## ITransferDomainHandler

`ITransferDomainHandler` is for domain logic around an already planned transfer.
It is not a replacement for rules and not another generic validation layer.

It is usually implemented on a binding:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
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

For a normal transfer the order is:

1. `CanDrop` and the rest of the rules decide whether the transfer can be planned at all.
2. the planner builds a `TransferPlan` without mutating inventories.
3. the executor creates a `TransferDomainContext` for the specific planned allocation.
4. `CanCommitTransfer` runs on the source binding first and then on the target binding if they implement `ITransferDomainHandler`.
5. if the binding also implements `IAsyncTransferDomainHandler`, `CanCommitTransferAsync` runs after the sync check.
6. only then does the real commit happen: split, conversion, placement, rollback if needed.
7. after the whole operation succeeds, `OnTransferSucceeded` runs.
8. only after that are inventory add/remove notifications and other deferred events dispatched.

So:

- `CanCommitTransfer` happens later than rules
- `CanCommitTransfer` happens before any mutation of that transfer allocation
- `OnTransferSucceeded` happens after a successful commit, but before `OnItemRemoved` / `OnItemAdded`

### What TransferDomainContext Contains

`TransferDomainContext` gives the binding transfer-level context:

- `SourceInventory` / `TargetInventory`
- `SourceBinding` / `TargetBinding`
- `SourceSlot`
- `PlannedTargetSlot`
- `TargetSlot` after commit
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

`IAsyncTransferDomainHandler` extends `ITransferDomainHandler` when the answer cannot be produced immediately.

```csharp
public class ServerInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>,
      ITransferDomainHandler,
      IAsyncTransferDomainHandler
{
    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return ValidateLocalState(context);
    }

    public async Task<RuleResult> CanCommitTransferAsync(
        TransferDomainContext context,
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

- `CanCommitTransferAsync` does not replace the sync version, it comes after it
- if the sync check already fails, the async stage does not run
- async validation runs before local commit
- a failure at the async stage cancels the transfer without mutating inventories

If the check is purely local and fast, regular `CanCommitTransfer` is enough.

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
- the planner through `UniversalInventory.GetMaxStackSizeForItem(...)`

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
