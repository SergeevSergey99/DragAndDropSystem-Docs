# Trading System Example

**Last Updated**: 2026-03-22

## Актуальность под текущую архитектуру

Этот пример работает через актуальный transfer pipeline:

- `DropPolicy` -> `TransferPlanner` -> `TransferPlanExecutor`
- preview/hover проверка использует target-side conversion и `InventoryAcceptanceRequest`
- swap включается через `OccupiedTargetPolicy.TrySwap`, а не через legacy-флаг менеджера
- кастомные ограничения swap реализуются через `InventorySwapContext` в DataBinding
- DataBinding уведомляется напрямую из `UniversalInventory`
- все биндинги используют шаблонные базовые классы (`ListInventoryDataBinding`, `MappedSlotInventoryDataBinding`)

Пример системы торговли с централизованной моделью экономики, демонстрирующий продвинутые возможности DataBinding и conversion pipeline.

## Описание

Этот пример показывает, как создать систему торговли, где:
- у игрока есть инвентарь, экипировка и деньги
- есть несколько торговцев, каждый со своим инвентарем и деньгами
- игрок может покупать товары у торговцев
- игрок может продавать товары торговцам
- игрок может экипировать предметы
- нельзя торговать между торговцами
- все транзакции проверяют наличие достаточных средств

## Архитектура

### Централизованная модель данных

```text
TradingEconomyManager (Singleton)
├── PlayerData
└── MerchantData per merchant id
```

### Два типа адаптеров

Торговцы и игрок используют разные представления предметов:
- торговец: `TradableItemSO` -> `TradableSoAdapter`
- игрок: `TradableItemModel` -> `TradableItemModelAdapter`

Конвертация между ними сейчас выполняется через inventory-side converter classes,
а planner и area-drop preview уже работают с target-side preview item до фактического переноса.

Примеры:

```text
Merchant -> Player:
TradableSoAdapter
  -> source preview outgoing
  -> target preview incoming
  -> TradableItemModelAdapter

Player -> Merchant:
TradableItemModelAdapter
  -> source preview outgoing
  -> target preview incoming
  -> TradableSoAdapter
```

### Компоненты системы

1. `TradableItemSO` - ScriptableObject с ценами и метаданными
2. `ITradableItem` - общий интерфейс для торговых адаптеров
3. `TradableSoAdapter` / `TradableItemModelAdapter` - адаптеры
4. `TradingEconomyManager` - централизованный менеджер экономики
5. `TradingHelper` - статический хелпер с общей торговой логикой

### DataBindings

**PlayerInventoryDataBinding** — `ListInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`:
- синхронизация с `PlayerData`
- торговая логика через `TradingHelper`
- `CanDrop()` — валидация покупки через `TradingHelper.ValidatePurchaseFromMerchant()`
- `ModelInventoryItemConverter` — конвертация SO -> Model

**MerchantInventoryDataBinding** — `ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>`:
- синхронизация с `MerchantData`
- торговая логика в `AddToData()` / `RemoveFromData()`
- `CanStartDrag()` — проверка денег игрока
- `CanDrop()` — проверка денег торговца + запрет торговли между торговцами
- `MerchantInventoryItemConverter` — конвертация Model -> SO

**EquipmentInventoryDataBinding** — `MappedSlotInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`:
- `CreateBindingMap()` — словарь слотов с декларативными `canAccept`
- `CanDrop()` — проверяет тип предмета через `canAccept` + валидацию покупки
- `ModelInventoryItemConverter` — конвертация SO -> Model
- использует shared preview infrastructure, а не отдельные preview guards

## Что демонстрирует этот пример

### 1. Шаблонные DataBindings

Все биндинги используют шаблонные базовые классы, которые автоматизируют:
- `OnReloadUI()`
- `OnItemAddedToUI()` / `OnItemRemovedFromUI()`

### 2. Конвертацию между разными инвентарями

`IInventoryItemConverter` позволяет разным инвентарям работать с разными типами адаптеров.
При этом preview теперь тоже знает о target-side conversion ещё до размещения в слот.

### 3. Декларативную валидацию слотов

`MappedSlotInventoryDataBinding` с `canAccept` в `CreateBindingMap()` позволяет определять правила слотов рядом с их привязкой к данным.

### 4. Общую торговую логику

`TradingHelper` содержит переиспользуемую логику валидации и transfer-level side effects для торговых операций.
Денежные изменения теперь проходят через `ITransferDomainHandler`, а не через item-added/item-removed callbacks.

### 5. Разные типы транзакций

- покупка (`Merchant -> Player`)
- продажа (`Player -> Merchant`)
- экипировка (`Player -> Equipment`)

## Настройка в Unity

### 1. Создание ScriptableObject товаров

1. Создайте несколько `TradableItemSO`
2. Настройте имя, иконку, цены покупки и продажи

### 2. Настройка TradingEconomyManager

1. Создайте GameObject с `TradingEconomyManager`
2. Настройте player data и merchants

### 3. Создание UI инвентарей

1. Инвентарь игрока:
   - `UniversalInventory`
   - `PlayerInventoryDataBinding`

2. Экипировка игрока:
   - `UniversalInventory`
   - `EquipmentInventoryDataBinding`
   - назначенные equipment slots

3. Инвентари торговцев:
   - `UniversalInventory`
   - `MerchantInventoryDataBinding`
   - merchant id

4. Стоимость выделенных к покупке предметов:
   - `SelectedPurchasePriceView` при необходимости

## Ключевые regression checks

- merchant -> player
- player -> merchant
- merchant -> equipment
- корректные `TargetItem` payloads в add events
- swap поведение только через policy и binding rules
