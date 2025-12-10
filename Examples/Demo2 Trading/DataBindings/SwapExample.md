# Swap Functionality - Usage Examples

## Базовое использование

### 1. Включение swap в DragAndDropManager

В Inspector найдите компонент `DragAndDropManager` и включите:
- `Auto Swap On Occupied Slot = true`

Теперь при перетаскивании предмета на занятый слот произойдет автоматический обмен.

---

## Кастомная валидация swap в DataBinding

### Пример 1: Запрет swap между торговцем и игроком

```csharp
public class PlayerInventoryDataBinding : TradingInventoryDataBinding
{
    protected override RuleResult CanSwapInternal(InventorySwapEventArgs args)
    {
        // Проверяем: пытается ли игрок обменять предмет с торговцем
        if (args.SourceInventory?.DataBinding is MerchantInventoryDataBinding ||
            args.TargetInventory?.DataBinding is MerchantInventoryDataBinding)
        {
            return RuleResult.Failure("Нельзя обменивать предметы с торговцем! Только покупка/продажа.");
        }

        return RuleResult.Success();
    }
}
```

### Пример 2: Специальная обработка swap экипировки

```csharp
public class EquipmentInventoryDataBinding : TradingInventoryDataBinding
{
    protected override void OnSwapCompleted(InventorySwapEventArgs args)
    {
        base.OnSwapCompleted(args);

        // Определяем какой слот участвует в swap
        bool isOurSource = args.SourceInventory == _inventory;
        bool isOurTarget = args.TargetInventory == _inventory;

        if (isOurSource && isOurTarget)
        {
            // Swap внутри экипировки (например, артефакт 1 ↔ артефакт 2)
            Debug.Log("Swapped equipment within same inventory");

            // Обновляем данные игрока
            UpdateEquipmentData(args.SourceSlot, args.SourceStack.Item);
            UpdateEquipmentData(args.TargetSlot, args.TargetStack.Item);
        }
        else if (isOurSource)
        {
            // Предмет из экипировки ушел в другой инвентарь
            // OnItemRemoved и OnItemAdded уже вызваны, дополнительная логика здесь
            Debug.Log($"Unequipped {args.SourceStack.Item.DisplayName}");
        }
        else if (isOurTarget)
        {
            // Предмет из другого инвентаря попал в экипировку
            Debug.Log($"Equipped {args.SourceStack.Item.DisplayName}");
        }
    }
}
```

### Пример 3: Валидация swap по типу предмета

```csharp
public class EquipmentInventoryDataBinding : TradingInventoryDataBinding
{
    protected override RuleResult CanSwapInternal(InventorySwapEventArgs args)
    {
        // Если swap происходит между слотами экипировки
        if (args.SourceInventory == _inventory && args.TargetInventory == _inventory)
        {
            // Получаем типы предметов
            var sourceAdapter = args.SourceStack.Item as TradableItemModelAdapter;
            var targetAdapter = args.TargetStack.Item as TradableItemModelAdapter;

            if (sourceAdapter != null && targetAdapter != null)
            {
                // Разрешаем swap только если типы совместимы
                if (sourceAdapter.ItemType != targetAdapter.ItemType)
                {
                    return RuleResult.Failure($"Нельзя менять местами {sourceAdapter.ItemType} и {targetAdapter.ItemType}!");
                }
            }
        }

        return base.CanSwapInternal(args);
    }
}
```

---

## Как работает swap под капотом

### Последовательность событий:

1. **PerformTransfer** обнаруживает что целевой слот занят
2. Проверяется флаг `_autoSwapOnOccupiedSlot`
3. **DragAndDropManager.TrySwap()** вызывается:
   - Вызывается **ValidateSwap** (проверка всех правил):
     - ✅ Проверка: можно ли вытащить из целевого слота (CanStartDrag)
     - ✅ Проверка: можно ли положить в исходный слот (CanDrop)
     - ✅ Проверка: можно ли положить в целевой слот (CanDrop)
     - ✅ Все правила (глобальные, инвентаря, слота) применяются
   - Вызывается событие **OnSwapAttempting** (DragAndDropManager):
     - 🔷 DataBinding вызывает `CanSwapInternal()` для кастомной валидации
     - 🔷 Можно отменить через `args.Cancel = true`
4. **UniversalInventory.TrySwapSlots()** выполняет swap:
   - Атомарно меняет предметы местами
   - Обновляет визуалы
   - ✨ **Генерирует события OnItemRemoved / OnItemAdded** для обоих инвентарей
5. DataBinding получает события через **OnItemAddedToUI / OnItemRemovedFromUI**
6. **DragAndDropManager** вызывает событие **OnSwapCompleted**:
   - 🔷 DataBinding вызывает `OnSwapCompleted()` для постобработки

### Архитектура событий:

```
DragAndDropManager               UniversalInventory            DataBinding
      |                                 |                           |
      |--TrySwap()------------------>  |                           |
      |                                 |                           |
      |--OnSwapAttempting--------->    |                           |
      |                                 |                       CanSwapInternal()
      |<--(Cancel?)--------------------|-----------------------    |
      |                                 |                           |
      |                              TrySwapSlots()                |
      |                                 |                           |
      |                                 |--OnItemRemoved-------> OnItemRemovedFromUI()
      |                                 |--OnItemAdded---------> OnItemAddedToUI()
      |                                 |--OnItemRemoved-------> OnItemRemovedFromUI()
      |                                 |--OnItemAdded---------> OnItemAddedToUI()
      |                                 |                           |
      |--OnSwapCompleted---------->    |                           |
      |                                 |                       OnSwapCompleted()
```

---

## Важные замечания

### ⚠️ События OnItemAdded/OnItemRemoved при swap

При swap **UniversalInventory.TrySwapSlots()** генерирует **4 события**:
1. `TargetInventory.OnItemRemoved` (удален целевой предмет)
2. `TargetInventory.OnItemAdded` (добавлен исходный предмет)
3. `SourceInventory.OnItemRemoved` (удален исходный предмет)
4. `SourceInventory.OnItemAdded` (добавлен целевой предмет)

Эти события автоматически вызывают `OnItemAddedToUI` и `OnItemRemovedFromUI` в DataBinding.

**Важно:** События генерируются из **UniversalInventory**, а не из DragAndDropManager!
Это правильная архитектура - каждый компонент отвечает за свои события.

**Рекомендации по обработке:**

#### Вариант 1: Обработка только в OnSwapCompleted (рекомендуется)

```csharp
protected override void OnItemAddedToUI(InventoryItemEventArgs args)
{
    if (_isSyncing) return;

    // Проверяем: это swap или обычное добавление?
    // При swap оба инвентаря участвуют и оба слота известны
    bool isLikelySwap = args.SourceInventory != null &&
                        args.TargetInventory != null &&
                        args.SourceSlot != null &&
                        args.TargetSlot != null;

    if (isLikelySwap)
    {
        // Swap - игнорируем здесь, обработаем в OnSwapCompleted
        return;
    }

    // Обычное добавление (покупка, автоперенос и т.д.)
    PlayerData.AddItem(...);
}

protected override void OnSwapCompleted(InventorySwapEventArgs args)
{
    // Вся логика swap здесь
    bool isOurSource = args.SourceInventory == _inventory;
    bool isOurTarget = args.TargetInventory == _inventory;

    if (isOurSource)
    {
        // Обрабатываем что ушло и что пришло
        PlayerData.RemoveItem(args.SourceStack.Item);
        PlayerData.AddItem(args.TargetStack.Item);
    }
}
```

#### Вариант 2: Обработка в OnItemAdded/Removed (если нужна детальная логика)

```csharp
protected override void OnItemAddedToUI(InventoryItemEventArgs args)
{
    if (_isSyncing) return;

    // Обрабатываем все добавления, включая swap
    PlayerData.AddItem(args.Item);
}

protected override void OnItemRemovedFromUI(InventoryItemEventArgs args)
{
    if (_isSyncing) return;

    // Обрабатываем все удаления, включая swap
    PlayerData.RemoveItem(args.Item);
}

// OnSwapCompleted можно не переопределять - события уже обработаны выше
```

### ⚠️ Swap и синхронизация данных

При swap между разными DataBinding нужно обновлять данные **в обоих**:

```csharp
protected override void OnSwapCompleted(InventorySwapEventArgs args)
{
    bool isOurSource = args.SourceInventory == _inventory;
    bool isOurTarget = args.TargetInventory == _inventory;

    if (isOurSource)
    {
        // Удаляем из наших данных то что ушло
        // Добавляем в наши данные то что пришло
    }

    if (isOurTarget)
    {
        // То же самое для целевого инвентаря
    }
}
```

---

## Отладка

Включите логи для отслеживания swap:

```csharp
DragAndDropManager.Instance.OnSwapAttempting += (sender, args) =>
{
    Debug.Log($"[SWAP] Attempting: {args.SourceStack.Item.DisplayName} ↔ {args.TargetStack.Item.DisplayName}");
};

DragAndDropManager.Instance.OnSwapCompleted += (sender, args) =>
{
    Debug.Log($"[SWAP] Completed: {args.SourceStack.Item.DisplayName} ↔ {args.TargetStack.Item.DisplayName}");
};
```

Все внутренние логи swap доступны в консоли с префиксом `ValidateSwap` и `TrySwap`.
