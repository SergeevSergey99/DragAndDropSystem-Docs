# Справочник событий

Все события, предоставляемые `DragAndDropManager` и `UniversalInventory`.

---

## События жизненного цикла перетаскивания

```mermaid
stateDiagram-v2
    classDef dragEnded fill:#f4f4,stroke-width:2px
    classDef dragCancelled fill:#f444,stroke-width:2px
    classDef dragStarted fill:#4F44,stroke-width:2px
    classDef dragOver fill:#8884,stroke-width:0px

    DragOver: OnDragEnterSlot/OnDragExitSlot

    [*] --> OnDragStarting: Игрок начинает перетаскивание
    OnDragStarting --> OnDragStarted: Не отменено
    OnDragStarting --> OnDragCancelled: Отменено
    OnDragStarted --> DragOver: Перетаскивание над слотами
    DragOver --> OnDropAttempting: Отпускание над слотом
    OnDragStarted --> OnDragCancelled: Нет подходящей цели
    OnDropAttempting --> OnDropCompleted: Успешно
    OnDropAttempting --> OnDragCancelled: Неудача
    OnDropCompleted --> OnDragEnded
    OnDragCancelled --> OnDragEnded

    class DragOver dragOver
    class OnDragStarting dragStarted
    class OnDragCancelled dragCancelled
    class OnDragEnded dragEnded
```

---

## События DragAndDropManager

### Перетаскивание

| Событие | Когда срабатывает | Примечание |
|---------|-------------------|------------|
| `OnDragStarting` | Перед началом перетаскивания | Можно отменить через правила |
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
| `OnSwapCompleted` | Обмен выполнен | Оба предмета поменялись местами |

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
| `Item` | `IInventoryItem` | Затронутый предмет |
| `Count` | `int` | Количество добавленных/удалённых |
| `SlotIndex` | `int` | Индекс целевого/исходного слота |
| `SourceInventory` | `IInventory` | Откуда взяли предмет (null если не из другого инвентаря) |
| `TargetInventory` | `IInventory` | Куда положили предмет (null если не в другой инвентарь) |
| `SourceSlot` | `ISlot` | Слот-источник (null если неизвестен) |
| `TargetSlot` | `ISlot` | Слот-назначение (null если неизвестен) |

---

## Контекст обмена

### Поля InventorySwapContext

| Поле | Тип | Описание |
|------|-----|----------|
| `SourceStack` | `ItemStack` | Стак из исходного слота (будет перемещён в целевой) |
| `TargetStack` | `ItemStack` | Стак из целевого слота (будет перемещён в исходный) |
| `SourceSlot` | `ISlot` | Исходный слот (откуда начали перетаскивание) |
| `TargetSlot` | `ISlot` | Целевой слот (куда хотим бросить) |
| `SourceInventory` | `IInventory` | Исходный инвентарь |
| `TargetInventory` | `IInventory` | Целевой инвентарь |
| `Cancel` | `bool` | Установите в `true` чтобы отменить обмен |

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
