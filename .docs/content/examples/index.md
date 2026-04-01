# Примеры

Этот раздел описывает **реальные demo-сцены из папки `Examples/`**: из каких частей они состоят, какие паттерны показывают и какие файлы смотреть в коде.

Если быстрый старт отвечает на вопрос "как поднять первый инвентарь", то раздел примеров отвечает на другие вопросы:

- как конкретный demo устроен по слоям
- где в нём находятся данные, адаптеры, биндинги и UI
- как проходит основной flow внутри сцены
- какие файлы смотреть, если вы хотите вынести паттерн в свой проект

---

## Как читать этот раздел

Каждая страница примера описывает:

1. **Что показывает демо**
2. **Как оно устроено**
3. **Как работает основной сценарий**
4. **Какие файлы смотреть**

Идея не в том, чтобы просто повторить сцену, а в том, чтобы быстро понять её архитектурную форму.

---

## Какие демо входят в пакет

### [Demo1 Inventories](demo1-inventories.md)

Когда использовать:
- нужен самый простой пример списка предметов
- хотите посмотреть `ListInventoryDataBinding` без лишней доменной логики
- нужен пример локальных `CanStartDrag` и `CanDrop`

Что показывает:
- `ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>`
- загрузку списка в UI
- синхронизацию списка обратно в данные
- базовые inventory rules и custom validation hooks

Реальная кодовая база:
- `Examples/Demo1 Inventaries/*`

### [Demo2 Loot](demo2-loot.md)

Когда использовать:
- нужен пример "мир -> событие -> UI -> инвентарь"
- нужны сундуки и открытие панели по взаимодействию
- нужен pickup/drop предметов, связанных с миром

Что показывает:
- mediator между world layer и UI
- два data binding-а для игрока и сундука
- `IInteractable`, `Chest`, `PlayerInteraction`
- интеграцию с world drop / pickup

Реальная кодовая база:
- `Examples/Demo2 Loot/*`

### [Demo3 Minecraft](demo3-minecraft.md)

Когда использовать:
- нужен слот-индексированный inventory
- нужен крафт по сетке и отдельный слот результата
- нужен пример максимального размера стека на слот

Что показывает:
- `SlotIndexedInventoryDataBinding`
- раздельные hotbar / inventory / craft table
- `CraftingManager` как единый источник доменных данных
- `CraftResultDataBinding` как read-only output slot с side effects

Реальная кодовая база:
- `Examples/Demo3 Minecraft/*`

### [Demo4 Trading](demo4-trading.md)

Когда использовать:
- предметы пересекают границу между разными доменными моделями
- нужны converters
- операция зависит от денег, цен и commit-time проверок

Что показывает:
- player / merchant / equipment inventories
- `ListInventoryDataBinding` и `MappedSlotInventoryDataBinding`
- domain hooks и conversion pipeline
- разделение mechanical validation и business validation

Реальная кодовая база:
- `Examples/Demo4 Trading/*`

### [Demo5 Containers](demo5-containers.md)

Когда использовать:
- предмет может сам содержать другой inventory
- нужно открывать контейнер из контекстного меню
- нужны custom occupied-slot drops и защита от циклов

Что показывает:
- item instances вместо простых SO-элементов
- контейнер как предмет и как источник данных одновременно
- `ContainerUIController` и переключение активного контейнера
- ограничения вида "контейнер нельзя положить в самого себя"

Реальная кодовая база:
- `Examples/Demo5 Containers/*`

---

## Как выбрать нужное демо

| Если вам нужно | Начните с |
|---|---|
| Базовый inventory list + простые hooks | [Demo1 Inventories](demo1-inventories.md) |
| UI для сундука и связь с миром | [Demo2 Loot](demo2-loot.md) |
| Сетка крафта и slot-indexed data | [Demo3 Minecraft](demo3-minecraft.md) |
| Торговля, конвертация и золото | [Demo4 Trading](demo4-trading.md) |
| Вложенные контейнеры и context menu | [Demo5 Containers](demo5-containers.md) |

---

## Что важно помнить

- Демки показывают **паттерны интеграции**, а не единственную правильную архитектуру.
- Между демо можно свободно переносить куски: например, взять контейнеры из Demo5 и fixed slots из Demo4.
- Если у вас иная доменная модель, обычно меняются bindings, adapters и converters, а не базовый transfer pipeline.

---

## Куда идти после примеров

- [Quick Start](../getting-started/quick-start.md) — если нужен базовый запуск с нуля
- [Data Binding](../architecture/data-binding.md) — если нужен полный lifecycle hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — если нужно понять planning / execution / rollback
