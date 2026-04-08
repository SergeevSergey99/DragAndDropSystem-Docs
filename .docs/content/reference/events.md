# Справочник событий

Все события, предоставляемые `DragAndDropManager` и `UniversalInventory`.

---

## События жизненного цикла перетаскивания

```mermaid
---
config:
  flowchart:
    curve: monotoneY 
---
flowchart TD
    StartPoint@{ shape: sm-circ, label: "Start" }
    NoTargetPoint@{ shape: sm-circ, label: "Start" }
    OnDragAttempting@{ shape: rounded, label: "OnDragAttempting" }
    style OnDragAttempting fill:#FF44
    OnDragStarted@{ shape: rounded, label: "OnDragStarted" }
    OnDragCancelled@{ shape: rounded, label: "OnDragCancelled" }
    style OnDragCancelled fill:#f444
    DragOver@{ shape: rounded, label: "OnDragEnterSlot/OnDragExitSlot" }
    style DragOver fill:#8884, stroke-dasharray: 5 5
    OnDropAttempting@{ shape: rounded, label: "OnDropAttempting" }
    OnDropCompleted@{ shape: rounded, label: "OnDropCompleted" }
    style OnDropCompleted fill:#4F44
    OnDragEnded@{ shape: rounded, label: "OnDragEnded" }
    style OnDragEnded fill:#f4f4

    StartPoint --> |Игрок начинает перетаскивание|OnDragAttempting
    OnDragAttempting --> |Не отменено| OnDragStarted
    NoTargetPoint --> |Нет подходящей цели| OnDragCancelled
    OnDragStarted --> |Перетаскивание над слотами| DragOver
    DragOver --> |Отпускание над слотом| OnDropAttempting
    OnDragAttempting --- NoTargetPoint
    OnDragStarted --- NoTargetPoint
    DragOver --- NoTargetPoint
    OnDragAttempting --> |Отменено| OnDragCancelled
    OnDropAttempting --> |Успешно| OnDropCompleted
    OnDropAttempting --> |Неудача| OnDragCancelled
    OnDropCompleted --> OnDragEnded
    OnDragCancelled --> OnDragEnded
    
```

---

## События DragAndDropManager

### Перетаскивание

| Событие | Когда срабатывает | Примечание |
|---------|-------------------|------------|
| `OnDragAttempting` | Перед началом перетаскивания | Можно отменить через правила |
| `OnDragStarted` | Перетаскивание подтверждено | `DragContext` доступен |
| `OnDragEnterSlot` | Курсор входит в слот | Информация о целевом слоте |
| `OnDragExitSlot` | Курсор покидает слот | Информация о предыдущем слоте |
| `OnDropAttempting` | Перед выполнением сброса | Цель определена |
| `OnDropCompleted` | Сброс выполнен успешно | Информация о результате |
| `OnDragCancelled` | Перетаскивание отменено или не удалось | Причина отмены |
| `OnDragEnded` | Цикл перетаскивания завершён | Срабатывает всегда, в конце |

### Автоперенос

| Событие | Когда срабатывает |
|---------|-------------------|
| `OnAutoTransferAttempting` | Перед быстрым переносом по клику |
| `OnAutoTransferCompleted` | Быстрый перенос выполнен |
| `OnAutoTransferFailed` | Быстрый перенос не удался |

### Обмен (swap)

| Событие | Когда срабатывает | Примечание |
|---------|-------------------|------------|
| `OnSwapAttempting` | Перед выполнением обмена | Установите `Cancel = true` для отмены |
| `OnSwapCompleted` | Обмен выполнен | Слоты уже содержат итоговые target-side стеки |

---

## События инвентаря

События `UniversalInventory`, срабатывающие при изменении содержимого.

| Событие | Когда срабатывает | Контекст |
|---------|-------------------|----------|
| `OnItemAdded` | Предмет добавлен в инвентарь | `InventoryItemEventContext` |
| `OnItemRemoved` | Предмет удалён из инвентаря | `InventoryItemEventContext` |

### Поля InventoryItemEventContext

| Поле | Тип | Описание |
|------|-----|----------|
| `Stack` | `ItemStack` | Полный стек события |
| `SlotIndex` | `int` | Индекс целевого/исходного слота |
| `SourceInventory` | `IInventory` | Откуда взяли предмет (null если не из другого инвентаря) |
| `TargetInventory` | `IInventory` | Куда положили предмет (null если не в другой инвентарь) |
| `SourceSlot` | `BaseSlot` | Слот-источник (null если неизвестен) |
| `TargetSlot` | `BaseSlot` | Слот-назначение (null если неизвестен) |

Практически:

- `context.Stack.PrimaryAdapter` даёт representative adapter
- `context.Stack.Count` даёт количество
- для cross-inventory переноса remove обычно публикует stack до conversion, а add — stack после conversion

---

## Контекст обмена

### Поля InventorySwapContext

| Поле | Тип | Описание |
|------|-----|----------|
| `SourceStack` | `ItemStack` | Pre-commit стак из исходного слота |
| `TargetStack` | `ItemStack` | Pre-commit стак из целевого слота |
| `SourceSlot` | `ISlot` | Исходный слот (откуда начали перетаскивание) |
| `TargetSlot` | `ISlot` | Целевой слот (куда хотим бросить) |
| `SourceInventory` | `IInventory` | Исходный инвентарь |
| `TargetInventory` | `IInventory` | Целевой инвентарь |
| `Cancel` | `bool` | Установите в `true` чтобы отменить обмен |

Важно:

- для cross-inventory swap это не обязательно те же adapter-объекты, которые будут лежать в слотах после commit
- итоговые слоты уже содержат target-side converted stacks

---

## Когда отправляются события

```mermaid
flowchart TB
    ПЛАН["Конвейер выполняет план"] --> ОТКЛ["События откладываются"]
    ОТКЛ --> УСПЕХ{"План завершился\nуспешно?"}
    УСПЕХ -->|"Да"| ОТПР["Отправить все события"]
    УСПЕХ -->|"Нет, атомарный режим"| ОТКАТ["Откат, события не отправляются"]
    УСПЕХ -->|"Нет, частичный режим"| ЧАСТЬ["Отправить события\nтолько для успешных записей"]

    ОТПР --> ПД["Привязка данных:\nуведомляется напрямую"]
    ОТПР --> ВП["Внешние подписчики:\nчерез события"]
```

!!! info "Безопасность по умолчанию"
    В атомарном режиме ни одно событие не отправляется, пока вся операция не завершится успешно. Это предотвращает реакцию подписчиков на изменения, которые могут быть откачены.

См. также:

- [Конвейер переноса](../architecture/transfer-pipeline.md)
- [Логи и отладка](logs-and-debugging.md)
