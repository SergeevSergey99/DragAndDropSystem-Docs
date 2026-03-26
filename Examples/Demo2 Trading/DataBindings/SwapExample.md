# Swap Functionality - Updated Examples

**Last Updated**: 2026-03-26

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
   - вызывает `SwapAttempting` (cancelable через `args.Cancel = true`)
   - выполняет `UniversalInventory.TrySwapSlots(...)`
   - после успешного completion dispatch-ит swap events и вызывает `SwapCompleted`

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
