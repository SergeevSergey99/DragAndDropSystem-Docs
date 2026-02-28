# Swap Functionality - Updated Examples

**Last Updated**: 2026-02-28

## Включение swap

Swap включается не отдельным флагом менеджера, а политикой дропа:

- `DropPolicy.OccupiedTarget = OccupiedTargetPolicy.TrySwap`

Policy может приходить:
- из override drop-area/slot target
- из inventory-level настроек
- из policy в `DragContext`/default

## DataBinding: кастомная валидация swap

В актуальном API используется `InventorySwapContext`.

### Пример 1: запрет swap с торговцем

```csharp
protected override RuleResult CanSwapInternal(InventorySwapContext args)
{
    if (args.SourceInventory?.DataBinding is MerchantInventoryDataBinding ||
        args.TargetInventory?.DataBinding is MerchantInventoryDataBinding)
    {
        return RuleResult.Failure("Нельзя обменивать предметы с торговцем. Используйте покупку/продажу.");
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
        // swap внутри одного инвентаря
        UpdateEquipmentData(args.SourceSlot, args.TargetStack.Item);
        UpdateEquipmentData(args.TargetSlot, args.SourceStack.Item);
    }
}
```

## Актуальный внутренний flow

1. `InventoryDropHandler` строит `TransferPlan` через `TransferPlanner`.
2. Если обычное размещение не удалось и policy = `TrySwap`, planner помечает entry как `RequiresSwap`.
3. `TransferPlanExecutor`:
   - валидирует swap в обе стороны правилами;
   - вызывает `SwapAttempting` (cancelable через `args.Cancel = true`);
   - выполняет `UniversalInventory.TrySwapSlots(...)`;
   - после успешного завершения execution dispatch-ит swap events и вызывает `SwapCompleted`.

## Важное про события

- Swap события в pipeline отправляются отложенно (после успешного execution).
- Это защищает от ложных событий при `BatchExecutionPolicy.Atomic` и rollback.

## Отладка

```csharp
DragAndDropManager.Instance.OnSwapAttempting += context =>
{
    Debug.Log($"[SWAP] Attempt: {context.SourceStack.Item.DisplayName} <-> {context.TargetStack.Item.DisplayName}");
    // context.Cancel = true; // если нужно отменить
};

DragAndDropManager.Instance.OnSwapCompleted += context =>
{
    Debug.Log($"[SWAP] Completed: {context.SourceStack.Item.DisplayName} <-> {context.TargetStack.Item.DisplayName}");
};
```
