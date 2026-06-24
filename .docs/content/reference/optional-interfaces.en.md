# Optional Interfaces

These interfaces are not required for basic drag & drop. Use them when you need to add behavior on top of a normal transfer.

## What to Choose

| If you need to... | Use |
|---|---|
| Block a transfer because of currency, permissions, item ownership, or shop state | `ITransferDomainHandler` |
| Ask a server or another external system before a transfer | `IAsyncTransferDomainHandler` |
| Set different stack limits for different items | `IStackSizeLimitable` |
| Show an item description in a tooltip or another UI | `IDescribable` |

## ITransferDomainHandler

Use `ITransferDomainHandler` when the decision depends on game logic, not only on the slot and item.

Examples:

- whether the player has enough gold to buy an item
- whether this item can be sold
- whether the item belongs to the player
- whether the shop is open
- whether items can move between these two containers

The interface is usually implemented on a DataBinding:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
    {
        return _shopIsOpen ? RuleResult.Success() : RuleResult.Failure("Shop is closed");
    }

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

Methods:

| Method | When it runs | Purpose |
|---|---|---|
| `CanStartTransfer` | once before the transfer | reject the whole operation |
| `CanCommitTransfer` | before a concrete placement is committed | check currency, permissions, ownership, and similar logic |
| `OnTransferSucceeded` | after a successful placement | spend currency, send analytics, update an external system |

Do not put regular "can this item type go into this slot" checks here. Use rules, `CanDrop`, or fixed-slot settings for that.

## IAsyncTransferDomainHandler

`IAsyncTransferDomainHandler` is for cases where the transfer must wait for an external answer.

For example:

- a server validates the transfer
- save data is checked from disk
- an external system checks permissions

```csharp
public async Task<RuleResult> CanStartTransferAsync(
    DragContext context,
    IInventory targetInventory,
    CancellationToken cancellationToken)
{
    return await _serverApi.ValidateTransferAsync(context, cancellationToken);
}
```

Important rules:

- the check applies to the whole transfer
- it runs before inventories are changed
- if the check fails, the transfer does not start
- if the check is fast and local, `ITransferDomainHandler` is usually enough

## IStackSizeLimitable

`IStackSizeLimitable` lets a specific item define its own stack limit:

```csharp
public class AmmoAdapter : IItemAdapter, IStackSizeLimitable
{
    public int MaxStackSize => 120;
}
```

Examples:

- potions stack to 20
- arrows stack to 999
- weapons stack to 1
- resources and tools use different limits

For this limit to be used, `_allowItemStackOverride` must be enabled on `UniversalInventory`.

If `_allowItemStackOverride` is disabled, the inventory-wide `_maxStackSize` is used.
If it is enabled and the item implements `IStackSizeLimitable`, the item limit replaces the inventory-wide limit.

Examples:

| Settings | Result |
|---|---|
| inventory `_maxStackSize = 20`, override disabled, item `MaxStackSize = 99` | limit is `20` |
| inventory `_maxStackSize = 20`, override enabled, item `MaxStackSize = 99` | limit is `99` |
| inventory `_maxStackSize = 20`, override enabled, item `MaxStackSize = 5` | limit is `5` |

## IDescribable

`IDescribable` lets an adapter provide an item description for UI.

```csharp
public class ItemAdapter : IItemAdapter, IDescribable
{
    public string Description => _item.Description;
}
```

The standard `DefaultTooltipView` example shows `Description` when the adapter implements `IDescribable`.

You can also use this interface in your own UI:

- tooltip
- inspect panel
- hover card
- context menu details

## Small Custom Interfaces

If plain `IItemAdapter` is not enough, you can add your own small interfaces for project-specific data.

For example:

- `IItemStatsProvider`
- `IRarityProvider`
- `IFlavorTextProvider`

The core inventory does not depend on them. Only the UI or gameplay systems that need this data should read them.
