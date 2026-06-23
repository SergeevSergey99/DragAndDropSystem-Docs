# Примеры

Этот раздел описывает **demo-сцены из папки `Examples/`**: из каких частей они состоят, какие паттерны показывают и какие файлы смотреть в коде.

Используйте эту страницу как карту выбора. Если вы уже знаете, какую задачу решаете, переходите сразу к нужному demo; если нет — начните с таблицы ниже.

## Быстрый выбор

| Задача | Начните с | Ключевые системы | Сложность |
|---|---|---|---|
| Базовый inventory list, простые rules и drop areas | [Demo1 Inventories](demo1-inventories.md) | `ListInventoryDataBinding`, local hooks, rules | Низкая |
| Сундук, pickup/drop и связь UI с объектами мира | [Demo2 Loot](demo2-loot.md) | world interaction, chest binding, filters | Средняя |
| Hotbar, inventory, craft grid и result slot | [Demo3 Craft](demo3-Craft.md) | `SlotIndexedInventoryDataBinding`, `CraftingManager`, craft result | Средняя |
| Торговля, золото, equipment и конвертация моделей | [Demo4 Trading](demo4-trading.md) | converters, domain checks, fixed equipment slots | Высокая |
| Предмет-контейнер с собственным вложенным inventory | [Demo5 Containers](demo5-containers.md) | context menu, nested binding, occupied-slot handler | Высокая |
| Multi-cell предметы, shape, anchor, rotation и preview клеток | [Demo6 Shaped Items](demo6-shaped-items.md) | placement topology, shapes, rotation actions | Высокая |

## Что показывает каждое демо

| Demo | Что показывает | Файлы, с которых начать |
|---|---|---|
| [Demo1 Inventories](demo1-inventories.md) | Самый простой список предметов, базовый binding и локальные проверки drag/drop. | `Examples/Demo1 Inventories/BasicListDataBinding.cs`, `Examples/Demo1 Inventories/ItemAdapterSoAdapter.cs` |
| [Demo2 Loot](demo2-loot.md) | Поток "мир -> событие -> UI -> инвентарь", сундуки, pickup/drop и фильтры. | `Examples/Demo2 Loot/ChestInventoryController.cs`, `Examples/Demo2 Loot/WorldDropManager.cs` |
| [Demo3 Craft](demo3-Craft.md) | Slot-indexed данные, craft grid, hotbar и отдельный result inventory. | `Examples/Demo3 Craft/CraftingManager.cs`, `Examples/Demo3 Craft/Data/CraftResultDataBinding.cs` |
| [Demo4 Trading](demo4-trading.md) | Перенос между разными моделями данных, цены, золото и equipment slots. | `Examples/Demo4 Trading/Domain/TradeDomainHandler.cs`, `Examples/Demo4 Trading/Converters/*` |
| [Demo5 Containers](demo5-containers.md) | Контейнер как item и как источник данных, nested inventory и защита от циклов. | `Examples/Demo5 Containers/ContainerItemData.cs`, `Examples/Demo5 Containers/ContainerUIController.cs` |
| [Demo6 Shaped Items](demo6-shaped-items.md) | Фигурные предметы в grid: footprint, anchor, orientation, rotation и covered-cell preview. | `Examples/Demo6 Shaped Items/ShapedItemSO.cs`, `Examples/Demo6 Shaped Items/ShapedItemAdapter.cs` |

## Как читать страницы demo

Каждая страница примера отвечает на четыре вопроса:

- что показывает demo
- какие runtime-объекты участвуют
- как проходит основной сценарий
- какие файлы смотреть, если вы хотите перенести паттерн в свой проект

---

## Что важно помнить

- Демки показывают **паттерны интеграции**, а не единственную правильную архитектуру.
- Между демо можно свободно переносить куски: например, взять контейнеры из Demo5 и fixed slots из Demo4.
- Если у вас иная доменная модель, обычно меняются bindings, adapters и converters, а не базовый transfer pipeline.

---

## Куда идти после примеров

- [Quick Start](../getting-started/quick-start.md) — если нужен базовый запуск с нуля
- [Data Binding](../architecture/data-binding.md) — если нужен полный lifecycle hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — если нужно понять порядок переноса и rollback
- [Карта файлов](../reference/file-map.md) — если нужно быстро найти конкретный runtime- или example-тип
