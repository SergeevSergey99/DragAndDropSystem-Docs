# Trading System - Краткое описание

**Last Updated**: 2026-03-26

## Актуальность

- дроп-логика использует pipeline (`DropPolicy` + planner + executor)
- preview и planning используют target-side conversion
- swap настраивается через `Drop Policy` (`BlockedTargetBehavior.Swap`), не manager-флагом
- DataBinding уведомляется напрямую из `UniversalInventory`
- все биндинги используют шаблонные базовые классы

## Файлы

### Данные

- `TradableItemSO.cs`
- `ITradableItem.cs`
- `TradableItemModel.cs`
- `ItemType.cs`

### Адаптеры

- `TradableSoAdapter.cs`
- `TradableItemModelAdapter.cs`

### Экономика

- `PlayerData`
- `MerchantData`
- `TradingEconomyManager.cs`

### DataBindings

- `PlayerInventoryDataBinding.cs`
  - синхронизация с `PlayerData`
  - торговая логика через `TradingHelper`
  - `ModelInventoryItemConverter`: SO -> Model

- `MerchantInventoryDataBinding.cs`
  - синхронизация с `MerchantData`
  - торговая логика в `AddToData` / `RemoveFromData`
  - `MerchantInventoryItemConverter`: Model -> SO

- `EquipmentInventoryDataBinding.cs`
  - `MappedSlotInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`
  - `CreateBindingMap()` со слотами и `canAccept`
  - `ModelInventoryItemConverter`: SO -> Model
  - preview slot validation работает через общий acceptance pipeline

- `TradingHelper.cs`
  - `ValidateMerchantDrop`
  - `ValidatePlayerTransfer` / `ValidateMerchantTransfer`
  - `ApplyPlayerTransferEffects` / `ApplyMerchantTransferEffects`

## Ключевые отличия от первого примера

| Первый пример | Trading пример |
|---------------|----------------|
| Простой `List<ItemSO>` | Централизованный `TradingEconomyManager` |
| Прямое наследование от Base | Шаблоны (`List` + `Mapped`) |
| Нет конвертации | SO ↔ Model через incoming/outgoing conversion |
| Базовые проверки | `TradingHelper` + `canAccept` в слотах |

## Механики

### Покупка (Торговец -> Игрок)

1. `CanStartDrag` / `CanDrop`: проверка денег игрока
2. target preview item резолвится до planning
3. incoming conversion: SO -> Model
4. add/remove data sync и money side effects через bindings + `TradingHelper`

### Продажа (Игрок -> Торговец)

1. `CanDrop`: проверка денег торговца + запрет merchant -> merchant
2. target preview item резолвится до planning
3. incoming conversion: Model -> SO
4. add/remove data sync и money side effects через bindings + `TradingHelper`

### Экипировка (Inventory -> Equipment)

1. `CanDrop`: проверка типа предмета через `canAccept` + покупка при необходимости
2. incoming conversion: SO -> Model, если источник торговец
3. `MappedSlotInventoryDataBinding` обновляет `PlayerData.Equipped*`

### Запреты

- торговля между торговцами
- покупка без денег
- продажа торговцу без денег
- неверный тип предмета в слот экипировки

Подробности см. в `README.md`.
