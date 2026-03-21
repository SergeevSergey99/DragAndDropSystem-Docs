# Trading System - Краткое описание

**Last Updated**: 2026-03-21

## Актуальность

- Дроп-логика использует pipeline (`DropPolicy` + planner + executor).
- Swap настраивается policy (`OccupiedTargetPolicy.TrySwap`), не manager-флагом.
- DataBinding уведомляется напрямую из `UniversalInventory` (не через события).
- Все биндинги используют шаблонные базовые классы.

## Файлы

### Данные
- **TradableItemSO.cs** — SO для торговых предметов с ценами (BuyPrice, SellPrice)
- **ITradableItem.cs** — общий интерфейс торговых адаптеров
- **TradableItemModel.cs** — runtime модель предмета
- **ItemType.cs** — типы предметов (Weapon, Armor, Artifact и т.д.)

### Адаптеры
- **TradableSoAdapter.cs** — адаптер для TradableItemSO (используется торговцами)
- **TradableItemModelAdapter.cs** — адаптер для TradableItemModel (используется игроком)

### Экономика
- **PlayerData** — деньги, инвентарь и экипировка игрока
- **MerchantData** — деньги и инвентарь торговца
- **TradingEconomyManager.cs** — синглтон, хранит данные всех участников

### DataBindings
- **PlayerInventoryDataBinding.cs** — `ListInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`
  - Синхронизация с PlayerData
  - Торговая логика через TradingHelper
  - ConvertIncomingItem: SO → Model

- **MerchantInventoryDataBinding.cs** — `ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>`
  - Синхронизация с MerchantData
  - Торговая логика в AddToData/RemoveFromData
  - ConvertIncomingItem: Model → SO

- **EquipmentInventoryDataBinding.cs** — `MappedSlotInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`
  - CreateBindingMap() со слотами и canAccept
  - ConvertIncomingItem: SO → Model

- **TradingHelper.cs** — статический хелпер:
  - ValidatePurchaseFromMerchant / ValidateSellToMerchant
  - TryHandlePurchaseFromMerchant / TryHandleSellToMerchant

### UI
- **SelectedPurchasePriceView.cs** — показывает суммарную стоимость выделенных предметов
- **IMerchantInventory.cs** — маркер-интерфейс для определения инвентарей торговцев

## Ключевые отличия от первого примера

| Первый пример | Trading пример |
|---------------|----------------|
| Простой List<ItemSO> | Централизованный TradingEconomyManager |
| Прямое наследование от Base | Шаблоны (List + Mapped) |
| Нет конвертации | SO ↔ Model через ConvertIncomingItem |
| Базовые проверки | TradingHelper + canAccept в слотах |

## Механики

### Покупка (Торговец → Игрок)
1. CanStartDrag: проверка денег игрока (TradingHelper)
2. CanDrop: проверка денег игрока (TradingHelper)
3. ConvertIncomingItem: SO → Model
4. AddToData: списание денег, добавление в PlayerData
5. RemoveFromData: начисление торговцу, удаление из MerchantData

### Продажа (Игрок → Торговец)
1. CanDrop: проверка денег торговца + запрет торговец→торговец
2. ConvertIncomingItem: Model → SO
3. AddToData: списание денег торговца, добавление в MerchantData
4. RemoveFromData: начисление игроку, удаление из PlayerData

### Экипировка (Инвентарь → Слот)
1. CanDrop: проверка типа предмета через canAccept + покупка
2. ConvertIncomingItem: SO → Model (если из торговца)
3. Set: привязка к PlayerData.EquippedWeapon и т.д.

### Запреты
- Торговля между торговцами
- Покупка без денег
- Продажа торговцу без денег
- Неверный тип предмета в слот экипировки

Подробная инструкция в **README.md**
