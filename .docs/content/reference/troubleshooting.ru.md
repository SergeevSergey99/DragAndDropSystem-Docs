# Troubleshooting

Эта страница собрана как справочник симптомов.

Формат простой:

- что вы видите
- что это обычно значит
- где смотреть в коде и в настройках

---

## `Неверный тип предмета`

Обычно значит, что до binding дошёл adapter не того inventory-boundary.

Типовые причины:

- target-side conversion не сработал
- swap был выполнен как raw exchange
- slot хранит adapter чужого инвентаря после предыдущей операции

Где смотреть:

- `CreateItemConverter()` в binding
- `TransferItemConversionUtility`
- `MappedSlotInventoryDataBinding.CanDrop()`
- `MappedSlotInventoryDataBinding.CanStartDrag()`

---

## Warning-и про другие слоты при дропе в один слот

Обычно значит, что система ушла в inventory-wide search, хотя вы ожидали direct slot path.

Типовые причины:

- операция пошла через `FindAlternative`
- активировался area-drop вместо slot-drop
- executor повторно позвал `GetAcceptableCount()`
- planner не получил concrete `targetSlotHint`

Где смотреть:

- `BlockedTargetBehavior`
- `InventoryDropProcessor`
- `TransferPlanner`
- логи `GetAcceptableCount`

---

## Preview проходит, а commit падает

Обычно проблема не в rules, а в execution или domain hooks.

Типовые причины:

- `CanCommitTransfer` veto
- `CanCommitTransferAsync` veto
- conversion failed during execution
- placement failed after split

Где смотреть:

- `ITransferDomainHandler`
- `IAsyncTransferDomainHandler`
- `TransferPlanExecutor`

---

## Первый swap работает, второй ломается

Почти всегда это означает, что после первого swap в slot остался adapter чужого типа.

Типовые причины:

- swap был raw exchange
- conversion применился только к preview, но не к commit
- add/remove events синхронизировали данные в одном формате, а slot физически хранит другой

Где смотреть:

- swap execution path
- `ValidateSwapRules`
- conversion in both directions

---

## Drag вообще не стартует

Типовые причины:

- source slot пуст
- `CanStartDrag` вернул отказ
- slot содержит не тот adapter-type
- binding не загрузил данные в UI

Где смотреть:

- `OnDragAttempting`
- `ValidateStartDrag`
- `ReloadUI()`
- `GetItems()`

---

## Данные не синхронизировались после успешного переноса

Типовые причины:

- до deferred events дело не дошло
- binding не подключён к правильному inventory
- `AddToData` / `RemoveFromData` работают не с тем backing source

Где смотреть:

- `DispatchTransferEvents`
- `DispatchSwapEvents`
- `OnItemAdded` / `OnItemRemoved`
- конкретный binding

---

## Стак ведёт себя как один и тот же предмет, хотя экземпляры должны быть разными

Типовые причины:

- один adapter повторно используется как representative для всего stack
- конвертация не сохраняет instance-state
- `ItemId` не соответствует реальной логике stacking

Где смотреть:

- adapter implementation
- conversion cookbook
- `ItemId`

---

## `CanDrop` вызывается много раз

Это может быть нормой, если:

- идёт preview candidate search
- работает `FindAlternative`
- идёт area-drop
- правила проверяются в обе стороны для swap

Это не норма, если:

- вы делаете direct slot drop в конкретный слот без `FindAlternative`
- а в логах всё равно видно обход соседних slots

Тогда искать нужно route/policy ошибку.

---

## С чего начать отладку

1. Определите фазу: drag start, preview/planning, domain validation, execution, swap.
2. Посмотрите первый meaningful лог в стеке, а не последний.
3. Проверьте, есть ли concrete `targetSlot`.
4. Проверьте, какой adapter-type реально лежит в slot после операции.

---

## Связанные страницы

- [Логи и отладка](logs-and-debugging.md)
- [Конвейер переноса](../architecture/transfer-pipeline.md)
- [Cookbook: конвертация предметов](../architecture/item-conversion-cookbook.md)
- [Матрица Drop Policy](../architecture/drop-policy-matrix.md)
