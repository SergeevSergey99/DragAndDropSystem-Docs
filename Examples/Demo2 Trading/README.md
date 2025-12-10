# Trading System Example

Пример системы торговли с централизованной моделью экономики, демонстрирующий продвинутые возможности DataBinding системы.

## Описание

Этот пример показывает как создать систему торговли где:
- У игрока есть инвентарь и деньги
- Есть несколько торговцев, каждый со своим инвентарем и деньгами
- Игрок может покупать товары у торговцев (перетаскивать из инвентаря торговца в свой)
- Игрок может продавать товары торговцам (перетаскивать из своего инвентаря к торговцу)
- Нельзя торговать между торговцами (торговец → торговец запрещено)
- Все транзакции проверяют наличие достаточных средств

## Архитектура

### Централизованная модель данных

В отличие от первого примера с простыми списками, здесь используется **централизованная модель экономики**:

```
TradingEconomyManager (Singleton)
├── PlayerEconomyData (деньги + инвентарь игрока)
└── Dictionary<MerchantData> (деньги + инвентарь каждого торговца)
```

### Компоненты системы

1. **TradableItemSO** - ScriptableObject с информацией о товаре и ценами
   - `BuyPrice` - цена покупки у торговца (игрок платит)
   - `SellPrice` - цена продажи торговцу (торговец платит)

2. **TradableItemAdapter** - адаптер для интеграции с системой drag and drop

3. **TradingEconomyManager** - централизованный менеджер экономики
   - Хранит данные игрока и всех торговцев
   - Выполняет транзакции
   - Логирует все операции

4. **PlayerInventoryDataBinding** - биндинг для инвентаря игрока
   - Синхронизирует UI с `PlayerEconomyData`
   - Проверяет достаточно ли денег при покупке
   - Отображает количество денег

5. **MerchantInventoryDataBinding** - биндинг для инвентаря торговца
   - Синхронизирует UI с `MerchantData`
   - Проверяет достаточно ли денег при продаже
   - Запрещает торговлю между торговцами
   - Отображает имя и деньги торговца

## Что демонстрирует этот пример

### 1. Работа с централизованной моделью данных
В отличие от первого примера где данные хранились в простых списках, здесь все данные находятся в одном месте - `TradingEconomyManager`. Это демонстрирует:
- Как биндинги могут работать с **разными источниками данных** (PlayerData, MerchantData)
- Как выполнять **транзакции между разными субъектами**
- Как централизованно логировать все операции

### 2. Использование ScriptableObject как источника данных
`TradableItemSO` - это ScriptableObject с ценами, что показывает:
- Как интегрировать SO с системой через адаптеры
- Как хранить дополнительные данные (цены покупки/продажи)
- Удобную настройку в Inspector

### 3. Переопределение методов проверки в DataBinding

**PlayerInventoryDataBinding:**
```csharp
protected override RuleResult CanDropInternal(DragContext context)
{
    // Проверяем что предмет от торговца
    var sourceMerchantBinding = context.SourceInventory.GetComponent<MerchantInventoryDataBinding>();
    if (sourceMerchantBinding != null)
    {
        // Проверяем достаточно ли денег у игрока
        if (!TradingEconomyManager.Instance.CanPlayerAfford(totalPrice))
            return RuleResult.Failure("Недостаточно денег!");
    }
    return RuleResult.Success();
}
```

**MerchantInventoryDataBinding:**
```csharp
protected override RuleResult CanStartDragInternal(DragContext context)
{
    // Проверяем достаточно ли денег у игрока для покупки
    if (!TradingEconomyManager.Instance.CanPlayerAfford(totalPrice))
        return RuleResult.Failure("Недостаточно денег!");
    return RuleResult.Success();
}

protected override RuleResult CanDropInternal(DragContext context)
{
    // Запрещаем торговлю между торговцами
    var sourceIsMerchant = context.SourceInventory.GetComponent<MerchantInventoryDataBinding>();
    if (sourceIsMerchant != null)
        return RuleResult.Failure("Нельзя торговать между торговцами!");

    // Проверяем достаточно ли денег у торговца для покупки
    if (!TradingEconomyManager.Instance.CanMerchantAfford(_merchantId, totalPrice))
        return RuleResult.Failure("У торговца недостаточно денег!");

    return RuleResult.Success();
}
```

### 4. Разные типы транзакций
- **Покупка** (Merchant → Player): проверяем деньги игрока, списываем у игрока, начисляем торговцу
- **Продажа** (Player → Merchant): проверяем деньги торговца, начисляем игроку, списываем у торговца

### 5. Логирование транзакций
Все транзакции логируются в `TradingEconomyManager` с подробной информацией:
```
[14:32:15] BUY Sword x1 for 100g with merchant_weapons
[14:32:20] SELL Potion x3 for 45g with merchant_alchemist
```

## Настройка в Unity

### 1. Создание ScriptableObject товаров

1. Создайте несколько `TradableItemSO`:
   - ПКМ → Create → DragAndDrop → Examples → Trading → Tradable Item
   - Настройте имя, иконку, цены покупки и продажи

Пример:
- Sword: BuyPrice = 100g, SellPrice = 50g
- Potion: BuyPrice = 30g, SellPrice = 15g
- Armor: BuyPrice = 150g, SellPrice = 75g

### 2. Настройка TradingEconomyManager

1. Создайте пустой GameObject и добавьте компонент `TradingEconomyManager`
2. В Inspector настройте:
   - **Player Money** - стартовые деньги игрока (например, 1000g)
   - **Merchants Init Data** - список торговцев:
     - Merchant ID: "merchant_weapons"
     - Display Name: "Оружейник"
     - Start Money: 500g
     - Start Inventory: добавьте несколько TradableItemSO с количеством

Пример конфигурации:
```
Merchant 1:
- ID: "merchant_weapons"
- Name: "Оружейник Гарольд"
- Money: 500g
- Inventory: Sword x3, Bow x2

Merchant 2:
- ID: "merchant_alchemist"
- Name: "Алхимик Мерлин"
- Money: 300g
- Inventory: Potion x10, Elixir x5
```

### 3. Создание UI инвентарей

1. **Инвентарь игрока:**
   - Создайте UniversalInventory
   - Добавьте компонент `PlayerInventoryDataBinding`
   - Настройте:
     - Inventory - ссылка на UniversalInventory
     - Money Text - ссылка на TextMeshProUGUI для отображения денег
     - Money Prefix/Suffix - текст вокруг суммы (например, "Gold: " и "g")

2. **Инвентари торговцев** (создайте по одному для каждого торговца):
   - Создайте UniversalInventory
   - Добавьте компонент `MerchantInventoryDataBinding`
   - Настройте:
     - Merchant ID - выберите из dropdown (должен совпадать с ID в TradingEconomyManager)
     - Inventory - ссылка на UniversalInventory
     - Merchant Name Text - ссылка на TextMeshProUGUI для имени
     - Money Text - ссылка на TextMeshProUGUI для денег

### 4. Правила (опционально)

Вы можете добавить дополнительные правила в каждый биндинг:
- Фильтры по типам предметов (торговец оружием не покупает зелья)
- Ограничения по уровню игрока
- Репутация с торговцем
- и т.д.

## Пример использования

После настройки системы:

1. **Покупка:**
   - Перетащите предмет из инвентаря торговца → в инвентарь игрока
   - Система проверит достаточно ли денег у игрока
   - Если да - спишет деньги с игрока, начислит торговцу, переместит товар

2. **Продажа:**
   - Перетащите предмет из инвентаря игрока → в инвентарь торговца
   - Система проверит достаточно ли денег у торговца
   - Если да - начислит деньги игроку, спишет с торговца, переместит товар

3. **Запрещенные действия:**
   - Попытка торговать между торговцами → ошибка "Нельзя торговать между торговцами!"
   - Недостаточно денег у игрока → ошибка "Недостаточно денег! Нужно Xg, у вас Yg"
   - Недостаточно денег у торговца → ошибка "У торговца недостаточно денег!"

## Отладка

`TradingEconomyManager` предоставляет инструменты отладки в Inspector:

- **Add 100 Gold to Player** - добавить деньги игроку для тестирования
- **Clear Transaction Log** - очистить лог транзакций
- **Reset Economy** - сбросить экономику к начальным значениям
- **Transaction Log** - просмотр всех транзакций

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

3. **Ограничение по типам товаров:**
   ```csharp
   if (merchantType == MerchantType.Weaponsmith && item.Type != ItemType.Weapon)
       return RuleResult.Failure("Оружейник не покупает это!");
   ```

4. **Динамические цены:**
   ```csharp
   int price = CalculateDynamicPrice(item, merchantData.Supply, merchantData.Demand);
   ```

## Сравнение с первым примером

| Аспект | Первый пример (ItemsSOInventoryDataBinding) | Этот пример (Trading) |
|--------|---------------------------------------------|----------------------|
| **Источник данных** | Простой List<ItemExampleSO> | Централизованный TradingEconomyManager |
| **Сложность** | Простая синхронизация | Транзакции между субъектами |
| **Проверки** | Базовые примеры | Проверка денег, типа источника |
| **ScriptableObject** | Базовый ItemSO | TradableItemSO с ценами |
| **Логирование** | Минимальное | Полный лог транзакций |
| **Использование** | Обучение основам | Реальная игровая механика |
