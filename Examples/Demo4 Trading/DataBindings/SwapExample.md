# Swap Functionality - Updated Examples

**Last Updated**: 2026-04-04

## Включение swap

Swap включается не отдельным флагом менеджера, а `Drop Policy`:

- `BlockedTargetBehavior = Swap`

Policy может приходить:
- из override на action / drop target
- из inventory-level `DropPolicySettings`
- после resolution превращается в `ResolvedDropPolicy`

## DataBinding: кастомная валидация swap

В актуальном API используется `InventorySwapContext`.

### Пример 1: запрет swap с торговцем

```csharp
protected override RuleResult CanSwap(InventorySwapContext args)
{
    if (args.SourceInventory?.DataBinding is IMerchantInventory ||
        args.TargetInventory?.DataBinding is IMerchantInventory)
    {
        return RuleResult.Failure("You cannot swap items with a merchant. Use buy/sell instead.");
    }

    return RuleResult.Success();
}
```

### Пример 2: post-processing после swap

```csharp
protected override void OnSwapCompleted(InventorySwapContext args)
{
    base.OnSwapCompleted(args);

    bool isOurSource = args.SourceInventory == _inventory;
    bool isOurTarget = args.TargetInventory == _inventory;

    if (isOurSource && isOurTarget)
    {
        UpdateEquipmentData(args.SourceSlot, args.TargetStack.Item);
        UpdateEquipmentData(args.TargetSlot, args.SourceStack.Item);
    }
}
```

## Актуальный внутренний flow

1. `InventoryDropProcessor` строит `TransferPlan` через `TransferPlanner`
2. Если обычное размещение не удалось и `BlockedTargetBehavior = Swap`, planner помечает entry как `RequiresSwap`
3. `TransferPlanExecutor`:
   - валидирует swap в обе стороны правилами
   - делает target-side conversion для обоих направлений ещё до commit
   - вызывает `SwapAttempting` (cancelable через `args.Cancel = true`)
   - коммитит в слоты уже конвертированные стэки, а не raw exchange
   - после успешного completion dispatch-ит swap events и вызывает `SwapCompleted`

Для cross-inventory swap это критично:
- `source -> target` проходит `source outgoing -> target incoming`
- `target -> source` проходит `target outgoing -> source incoming`
- в слотах после swap остаются adapter-типы, принадлежащие их собственным inventory/DataBinding

## Важное про события

- swap события в pipeline отправляются отложенно, после успешного execution
- это защищает от ложных событий при `BatchMode.Atomic` и rollback

## Отладка

```csharp
DragAndDropManager.Instance.OnSwapAttempting += context =>
{
    Debug.Log($"[SWAP] Attempt: {context.SourceStack.Item.DisplayName} <-> {context.TargetStack.Item.DisplayName}");
};

DragAndDropManager.Instance.OnSwapCompleted += context =>
{
    Debug.Log($"[SWAP] Completed: {context.SourceStack.Item.DisplayName} <-> {context.TargetStack.Item.DisplayName}");
};
```
