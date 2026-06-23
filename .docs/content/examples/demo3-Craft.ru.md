# Demo3 Craft

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/lbcTdQGJTIs"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo3 Craft/CraftDemo.unity`

Это пример slot-indexed inventory и крафта, где UI синхронизируется не со списком, а с фиксированными массивами данных.

## Что показывает демо

- `SlotIndexedInventoryDataBinding`
- стол крафта и слот результата
- ограничение максимального стека на слот
- `CraftResultDataBinding` как кастомный тип инвентаря для доставания результата крафта

## Как устроено

Центральная точка логики:

- `Crafting/CraftingManager.cs`

Он хранит:

- `_hotbarItems`
- `_inventoryItems`
- `_craftTableItems`
- список рецептов
- текущий результат и множитель крафта

UI разбит на четыре независимых binding-а:

- `MainInventoryDataBinding`
- `HotbarDataBinding`
- `CraftTableDataBinding`
- `CraftResultDataBinding`

## Как работает

Обычные слоты:

1. Binding перечисляет занятые индексы через `GetOccupiedSlots()`.
2. `AddToSlotData(...)` и `RemoveFromSlotData(...)` вызывают методы `CraftingManager`.
3. На `Awake()` инвентари получает `SetMaxStackSize(CraftingManager.MaxItemsPerSlot)` чтобы ограничить число предметов в слоте

Крафт:

1. Изменение craft table обновляет массив `_craftTableItems`.
2. `CraftingManager.RefreshCraftResult()` ищет подходящий рецепт.
3. `CraftResultDataBinding.OnReloadUI()` показывает результат и настраивает шаг drag-а.
4. Когда пользователь забирает результат, `OnItemRemovedFromUI(...)` вызывает `ConsumeCraftIngredients(...)`.

Это хороший пример read-only слота, который не принимает входящий drop, но в зависимости от внешней логики создает предмет который можно вытащить.

## Когда брать этот пример за основу

- нужен инвентарь с жёсткими индексами
- нужен крафт
- нужен пример выходного слота с эффектом после извлечения
