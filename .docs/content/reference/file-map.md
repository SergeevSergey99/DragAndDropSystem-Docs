# Карта файлов

Эта страница нужна тем, кто уже ориентируется в системе и хочет быстро найти точку расширения в коде.

Сначала приведены файлы, которые чаще всего нужны пользователю ассета.
Внутренние служебные классы перечислены отдельно.

---

## Куда обычно смотреть в первую очередь

| Файл | Когда нужен |
|---|---|
| `Scripts/Inventories/UniversalInventory.cs` | настройка и поведение самого инвентаря |
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | базовый жизненный цикл binding'а |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | инвентарь на основе списка |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | фиксированные именованные слоты |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | кастомное поведение размещения |
| `Scripts/Rules/IDragRule.cs` | интерфейс `IDragRule` и базовый `DragRuleBase` |
| `Scripts/Inventories/ITransferDomainHandler.cs` | хуки перед коммитом и после успеха |
| `Scripts/Inventories/IAsyncTransferDomainHandler.cs` | асинхронная pre-commit проверка для сервера, файлов и внешних источников |
| `Scripts/Interaction/InputEventRouter.cs` | кастомные input bindings |

---

## Если вы расширяете конвейер переноса

| Файл | Роль |
|---|---|
| `Scripts/Inventories/TransferPlanner.cs` | планирование без мутации состояния |
| `Scripts/Inventories/TransferPlanExecutor.cs` | commit, rollback и deferred events |
| `Scripts/Inventories/InventoryDropProcessor.cs` | точка входа из UI в конвейер |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | служебные методы конвертации для цели |

---

## Если вы работаете с примерами

| Файл | Роль |
|---|---|
| `Examples/Demo2 Trading/DataBindings/*.cs` | trading bindings |
| `Examples/Demo2 Trading/Converters/*.cs` | item converters для торговли |
| `Examples/Demo2 Trading/TradingHelper.cs` | торговые проверки и side effects |
| `Examples/Demo3 Loot/*.cs` | пример world loot |

---

## Внутренние детали реализации

Эти файлы обычно не нужны пользователю ассета, пока вы не меняете сам конвейер:

| Файл | Роль |
|---|---|
| `Scripts/Inventories/InventoryTransferService.cs` | низкоуровневый сервис переноса |
| `Scripts/Inventories/EntryPlanningOperation.cs` | объект операции для planning |
| `Scripts/Inventories/TargetPlacementOperation.cs` | объект операции размещения |
| `Scripts/Inventories/AlternativeSlotSearchOperation.cs` | поиск альтернативного слота |
| `Scripts/Inventories/VirtualSlotState.cs` | виртуальное состояние слотов при planning |
| `Scripts/Inventories/SlotOperationContext.cs` | контекст low-level операций со слотами |

Если вы только интегрируете ассет в игру, этот раздел можно спокойно пропустить.
