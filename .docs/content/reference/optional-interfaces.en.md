# Optional Interfaces

These interfaces are not required for basic drag & drop. Use them when you need to add behavior on top of a normal transfer.

## What to Choose

| If you need to... | Use |
|---|---|
| Block a transfer because of currency, permissions, item ownership, or shop state | `ITransferDomainHandler` |
| Ask a server or another external system before a transfer | `IAsyncTransferDomainHandler` |
| Add special behavior for dropping onto an occupied slot: insert into a container, equip, open an item | `IPreRuleOccupiedSlotDropHandler` or `IPostRuleOccupiedSlotDropHandler` |
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

## IOccupiedSlotDropHandler

`IOccupiedSlotDropHandler` is for cases where dropping onto an occupied slot should mean your custom action, not regular swap or alternative placement.

Examples:

- drop an item onto a bag to put it inside
- drop an item onto an equipped slot to perform a special replacement
- drop a key onto a container to open it

Do not implement the base `IOccupiedSlotDropHandler` directly. Implement one of the timing interfaces:

| Interface | When it runs | Purpose |
|---|---|---|
| `IPreRuleOccupiedSlotDropHandler` | before target slot drop rules | the drop is effectively addressed to the object inside the slot, such as a container |
| `IPostRuleOccupiedSlotDropHandler` | after target slot drop rules | the target's regular rules must allow the drop first |

The interface is usually implemented on the target inventory DataBinding:

```csharp
public class ContainerInventoryBinding
    : SlotIndexedInventoryDataBinding<ItemModel, ItemModelAdapter>,
      IPreRuleOccupiedSlotDropHandler
{
    public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        return occupiedSlot.Stack?.PrimaryAdapter is ContainerAdapter;
    }

    public OccupiedSlotDropResult ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        return TryPutIntoContainer(entry, occupiedSlot)
            ? OccupiedSlotDropResult.Handled
            : OccupiedSlotDropResult.Rejected;
    }
}
```

`ExecuteOccupiedSlotDrop` returns:

| Result | What the pipeline does |
|---|---|
| `Handled` | the handler performed the action; regular drop, swap, and alternative placement do not run |
| `Rejected` | the handler rejected the action; the transfer is rolled back |
| `Fallthrough` | the handler chose not to intercept; the transfer continues as a regular drop onto an occupied slot |

If the handler returns `Handled` or `Rejected`, it finishes that transfer attempt. The system does not run swap or alternative placement afterward.

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
