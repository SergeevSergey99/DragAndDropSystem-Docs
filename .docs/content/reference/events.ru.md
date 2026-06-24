# Справочник событий

Глобальные события drag / drop / auto-transfer / swap находятся в `UDNDEvents`. События изменения содержимого конкретного инвентаря находятся в `UniversalInventory`.

## Коротко

| Если нужно... | Слушайте |
|---|---|
| Узнать, что игрок начал или закончил drag | `UDNDEvents.OnDragStarted`, `UDNDEvents.OnDragEnded` |
| Отреагировать на успешный drop | `UDNDEvents.OnDropCompleted` |
| Показать причину отмены | `UDNDEvents.OnDragCancelled` |
| Отследить быстрый перенос по клику | `UDNDEvents.OnAutoTransferCompleted`, `UDNDEvents.OnAutoTransferFailed` |
| Отследить изменение содержимого инвентаря | `UniversalInventory.OnItemAdded`, `UniversalInventory.OnItemRemoved` |
| Запретить swap перед выполнением | `UDNDEvents.OnSwapAttempting` |

## Обычный drag / drop

Типичный успешный сценарий:

1. `OnDragAttempting`
2. `OnDragStarted`
3. `OnDragEnterSlot` / `OnDragExitSlot` во время наведения
4. `OnDropAttempting`
5. `OnDropCompleted`
6. `OnDragEnded`

Если drag или drop отменён, вместо `OnDropCompleted` вызывается `OnDragCancelled`, а затем всё равно вызывается `OnDragEnded`.

`OnDragEnded` удобен для очистки временного UI, потому что он срабатывает в конце и успешного, и неуспешного сценария.

## Глобальные события (`UDNDEvents`)

Это `static`-события. Подписка выглядит как `UDNDEvents.OnX += handler`; отписку делайте в `OnDisable` или другом teardown-методе.

`DragAndDropManager` очищает подписчиков из прошлой Play-сессии на старте новой Play-сессии. Это важно для Fast Enter Play Mode.

### Перетаскивание

| Событие | Когда срабатывает | Примечание |
|---|---|---|
| `OnDragAttempting` | Перед началом перетаскивания | Можно отменить через правила |
| `OnDragStarted` | Перетаскивание подтверждено | `DragContext` уже доступен |
| `OnDragEnterSlot` | Курсор входит в слот | Можно обновить hover UI |
| `OnDragExitSlot` | Курсор покидает слот | Можно сбросить hover UI |
| `OnDropAttempting` | Перед обработкой drop | Цель уже определена |
| `OnDropCompleted` | Drop выполнен успешно | Изменения уже применены |
| `OnDragCancelled` | Drag или drop отменён | Используйте для сообщения об ошибке или сброса UI |
| `OnDragStackChanged` | Количество в drag stack изменилось | Например после split drop |
| `OnDragEnded` | Цикл drag завершён | Срабатывает всегда в конце |

### Автоперенос

| Событие | Когда срабатывает |
|---|---|
| `OnAutoTransferAttempting` | Перед быстрым переносом по клику |
| `OnAutoTransferCompleted` | Быстрый перенос выполнен |
| `OnAutoTransferFailed` | Быстрый перенос не удался |

### Swap

| Событие | Когда срабатывает | Примечание |
|---|---|---|
| `OnSwapAttempting` | Перед обменом | Установите `Cancel = true`, чтобы отменить swap |
| `OnSwapCompleted` | Обмен выполнен | Слоты уже содержат итоговые стеки |

## События инвентаря

`UniversalInventory` отправляет события, когда его содержимое действительно изменилось.

| Событие | Когда срабатывает | Контекст |
|---|---|---|
| `OnItemAdded` | Предмет добавлен в инвентарь | `InventoryItemEventContext` |
| `OnItemRemoved` | Предмет удалён из инвентаря | `InventoryItemEventContext` |

### Поля `InventoryItemEventContext`

| Поле | Тип | Описание |
|---|---|---|
| `Stack` | `ItemStack` | Стек, который добавили или удалили |
| `SlotIndex` | `int` | Индекс слота |
| `SourceInventory` | `IInventory` | Откуда пришёл предмет, если известно |
| `TargetInventory` | `IInventory` | Куда ушёл предмет, если известно |
| `SourceSlot` | `BaseSlot` | Исходный слот, если известен |
| `TargetSlot` | `BaseSlot` | Целевой слот, если известен |

Практически:

- `context.Stack.PrimaryAdapter` даёт основной adapter предмета
- `context.Stack.Count` даёт количество
- при переносе между разными типами инвентарей remove обычно содержит предмет до конвертации, а add — предмет после конвертации

## Контекст swap

### Поля `InventorySwapContext`

| Поле | Тип | Описание |
|---|---|---|
| `SourceStack` | `ItemStack` | Стек в исходном слоте перед swap |
| `TargetStack` | `ItemStack` | Стек в целевом слоте перед swap |
| `SourceSlot` | `BaseSlot` | Слот, откуда начали перетаскивание |
| `TargetSlot` | `BaseSlot` | Слот, куда бросили предмет |
| `SourceInventory` | `IInventory` | Исходный инвентарь |
| `TargetInventory` | `IInventory` | Целевой инвентарь |
| `Cancel` | `bool` | Установите `true`, чтобы отменить swap |

Для swap между разными типами инвентарей итоговые adapter-объекты в слотах могут отличаться от тех, что были в `SourceStack` и `TargetStack` до выполнения. Это нормально: предметы проходят конвертацию под целевой инвентарь.

## Когда события отправляются

События отправляются только после успешного изменения инвентаря.

Если перенос не прошёл проверку, не хватило места или операция была отменена, события добавления и удаления не отправляются. Подписчики не увидят временное состояние, которое система затем откатила.

Для группового переноса каждый предмет обрабатывается отдельно:

- успешный предмет отправляет свои события
- неуспешный предмет остаётся на месте и не отправляет события
- ошибка одного предмета не отменяет события уже успешно перенесённых предметов

См. также:

- [Конвейер переноса](../architecture/transfer-pipeline.md)
- [Логи и отладка](logs-and-debugging.md)
