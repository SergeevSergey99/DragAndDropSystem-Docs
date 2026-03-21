# Trading System Example

**Last Updated**: 2026-03-21

## Актуальность под новую архитектуру

Этот пример работает через текущий transfer pipeline:

- `DropPolicy` -> `TransferPlanner` -> `TransferPlanExecutor`
- swap включается через `OccupiedTargetPolicy.TrySwap`, а не через legacy-флаг менеджера
- кастомные ограничения swap реализуются через `InventorySwapContext` в DataBinding
- DataBinding уведомляется напрямую из `UniversalInventory` (не через события)
- Все биндинги используют шаблонные базовые классы (`ListInventoryDataBinding`, `MappedSlotInventoryDataBinding`)

Пример системы торговли с централизованной моделью экономики, демонстрирующий продвинутые возможности DataBinding системы.

## Описание

Этот пример показывает как создать систему торговли где:
- У игрока есть инвентарь, экипировка и деньги
- Есть несколько торговцев, каждый со своим инвентарем и деньгами
- Игрок может покупать товары у торговцев (перетаскивать из инвентаря торговца в свой)
- Игрок может продавать товары торговцам (перетаскивать из своего инвентаря к торговцу)
- Игрок может экипировать предметы (перетаскивать в слоты экипировки)
- Нельзя торговать между торговцами (торговец → торговец запрещено)
- Все транзакции проверяют наличие достаточных средств

## Архитектура

### Централизованная модель данных

```
TradingEconomyManager (Singleton)
├── PlayerData (деньги + инвентарь + экипировка игрока)
└── Dictionary<MerchantData> (деньги + инвентарь каждого торговца)
```

### Два типа адаптеров

Торговцы и игрок используют разные типы данных:
- **Торговец**: `TradableItemSO` (ScriptableObject) → `TradableSoAdapter`
- **Игрок**: `TradableItemModel` (runtime модель) → `TradableItemModelAdapter`

Конвертация между ними происходит автоматически через `ConvertIncomingItem`:
```
Торговец → Игрок: TradableSoAdapter → ConvertIncomingItem → TradableItemModelAdapter
Игрок → Торговец: TradableItemModelAdapter → ConvertIncomingItem → TradableSoAdapter
```

### Компоненты системы

1. **TradableItemSO** - ScriptableObject с информацией о товаре и ценами
   - `BuyPrice` - цена покупки у торговца (игрок платит)
   - `SellPrice` - цена продажи торговцу (торговец платит)

2. **ITradableItem** - общий интерфейс для всех торговых адаптеров
   - `BuyPrice`, `SellPrice`, `OriginalSO`

3. **TradableSoAdapter / TradableItemModelAdapter** - адаптеры для SO и Model

4. **TradingEconomyManager** - централизованный менеджер экономики

5. **TradingHelper** - статический хелпер с общей торговой логикой:
   - `ValidatePurchaseFromMerchant()` — проверка денег игрока
   - `ValidateSellToMerchant()` — проверка денег торговца + запрет торговли между торговцами
   - `TryHandlePurchaseFromMerchant()` — списание денег при покупке
   - `TryHandleSellToMerchant()` — начисление денег при продаже

### DataBindings

**PlayerInventoryDataBinding** — `ListInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`:
- 3 примитива: `GetItems()`, `CreateAdapter()`, `ExtractData()`
- `AddToData()` / `RemoveFromData()` — торговая логика через `TradingHelper`
- `CanDrop()` — валидация покупки через `TradingHelper.ValidatePurchaseFromMerchant()`
- `ConvertIncomingItem()` — конвертация SO→Model

**MerchantInventoryDataBinding** — `ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>`:
- 3 примитива: `GetItems()`, `CreateAdapter()`, `ExtractData()`
- `AddToData()` / `RemoveFromData()` — торговая логика (деньги + инвентарь)
- `CanStartDrag()` — проверка денег игрока
- `CanDrop()` — проверка денег торговца + запрет торговли между торговцами
- `ConvertIncomingItem()` — конвертация Model→SO

**EquipmentInventoryDataBinding** — `MappedSlotInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`:
- `CreateBindingMap()` — словарь слотов с декларативными `canAccept`:
  ```csharp
  [_weaponSlot] = new(
      get: () => PlayerData.EquippedWeapon,
      set: item => PlayerData.EquipWeapon(item),
      clear: () => PlayerData.UnequipWeapon(),
      canAccept: item => item.originalSO.ItemType == ItemType.Weapon
          ? RuleResult.Success()
          : RuleResult.Failure("В этот слот можно положить только оружие"))
  ```
- `CanDrop()` — проверяет тип предмета через `canAccept` + валидация покупки
- `ConvertIncomingItem()` — конвертация SO→Model

## Что демонстрирует этот пример

### 1. Шаблонные DataBindings
Все биндинги используют шаблонные базовые классы, которые автоматизируют:
- `OnReloadUI()` — перезагрузка UI из данных
- `OnItemAddedToUI()` / `OnItemRemovedFromUI()` — синхронизация данных при изменении UI

### 2. Конвертация предметов между инвентарями
`ConvertIncomingItem` / `ConvertOutgoingItem` позволяют разным инвентарям работать с разными типами адаптеров.

### 3. Декларативная валидация слотов
`MappedSlotInventoryDataBinding` с `canAccept` в `CreateBindingMap()` позволяет определять валидацию слотов рядом с их привязкой к данным.

### 4. Общая торговая логика
`TradingHelper` содержит переиспользуемую логику валидации и обработки торговых операций.

### 5. Разные типы транзакций
- **Покупка** (Merchant → Player): проверяем деньги игрока, списываем у игрока, начисляем торговцу
- **Продажа** (Player → Merchant): проверяем деньги торговца, начисляем игроку, списываем у торговца
- **Экипировка** (Player Inventory → Equipment): проверяем тип предмета через `canAccept`

## Настройка в Unity

### 1. Создание ScriptableObject товаров

1. Создайте несколько `TradableItemSO`:
   - ПКМ → Create → DragAndDrop → Examples → Trading → Tradable Item
   - Настройте имя, иконку, цены покупки и продажи

### 2. Настройка TradingEconomyManager

1. Создайте пустой GameObject и добавьте компонент `TradingEconomyManager`
2. В Inspector настройте:
   - **Player Money** - стартовые деньги игрока
   - **Merchants Init Data** - список торговцев с инвентарями

### 3. Создание UI инвентарей

1. **Инвентарь игрока:**
   - Создайте UniversalInventory
   - Добавьте компонент `PlayerInventoryDataBinding`

2. **Экипировка игрока:**
   - Создайте UniversalInventory с отдельными слотами
   - Добавьте компонент `EquipmentInventoryDataBinding`
   - Назначьте слоты (weapon, armor, artifact1, artifact2)

3. **Инвентари торговцев:**
   - Создайте UniversalInventory
   - Добавьте компонент `MerchantInventoryDataBinding`
   - Укажите Merchant ID

4. **Стоимость выделенных к покупке предметов (опционально):**
   - Добавьте компонент `SelectedPurchasePriceView` на любой UI объект.

## Расширение системы

Систему легко расширить:

1. **Скидки и наценки:**
   ```csharp
   int totalPrice = (adapter.BuyPrice * discountMultiplier) * count;
   ```

2. **Репутация с торговцем:**
   ```csharp
   if (merchantData.Reputation < requiredReputation)
       return RuleResult.Failure("Недостаточная репутация!");
   ```

3. **Фильтры по типам товаров** — добавьте правила в DataBinding через Inspector

## Сравнение с первым примером

| Аспект | Первый пример (ItemsSOInventoryDataBinding) | Этот пример (Trading) |
|--------|---------------------------------------------|----------------------|
| **Источник данных** | Простой List<ItemExampleSO> | Централизованный TradingEconomyManager |
| **Сложность** | Простая синхронизация | Транзакции между субъектами |
| **DataBinding** | Прямое наследование от Base | Шаблоны (List + Mapped) |
| **Конвертация** | Нет | SO ↔ Model через ConvertIncomingItem |
| **Валидация** | Базовые примеры | TradingHelper + canAccept |
