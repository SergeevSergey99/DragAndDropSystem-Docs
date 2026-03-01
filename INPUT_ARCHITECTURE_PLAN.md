# План рефакторинга системы ввода

## Проблема

Текущая архитектура имеет три независимых канала ввода, которые не знают друг о друге:

- `DragDropEventListener` — мышь (EventSystem pointer events) + домен (DragAndDropManager)
- `SlotPointerSelectionTrigger` — мышь (EventSystem click) + домен (SelectionManager)
- `InventoryInputHandler` — клавиатура/геймпад (Input System) + домен (InventoryActionBase)

**Следствия:**
- Один клик может одновременно запустить авто-перенос и изменить выделение
- Добавление геймпада потребует дублирования логики во всех трёх местах
- Нет единого «владельца» текущего интента — гонки неизбежны

---

## Целевая архитектура: три слоя

```
┌─────────────────────────────────────────────────────────────┐
│                       СЛОЙ ВВОДА                            │
│                                                             │
│  Мышь LMB ───┐                                              │
│  Геймпад A ──┼──→  InputAction "Interact"   ┐               │
│  Клавиша E ──┘                              │ только для    │
│                                             │ keyboard /    │
│  Мышь RMB ───┐                              │ gamepad       │
│  Геймпад X ──┼──→  InputAction "Secondary" ─┘               │
│  Клавиша Q ──┘                                              │
│                                                             │
│  Мышь (pointer) ──→  Unity EventSystem  ┐                   │
│  Touch          ──→  Unity EventSystem  ┘ pointer-события   │
│                                                             │
│  Геймпад D-pad/стик ──→  UI Navigation / Virtual Cursor     │
└──────────┬──────────────────────────┬───────────────────────┘
           │ Input System events      │ EventSystem pointer events
           ▼                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  СЛОЙ ВЗАИМОДЕЙСТВИЯ                        │
│                                                             │
│   InputEventRouter  ←─────────────────────────────────────┐ │
│   (static/singleton)           SlotInputAdapter (per-slot) │ │
│   anti-dup only                пересылает raw события      │ │
│          ↓                     в router                    │ │
│   InventoryInteractionCoordinator  (per-inventory)         │ │
│   • единственный получатель всех интентов                  │ │
│   • владеет state machine                                  │ │
│   • диспатчит в домен после принятия решения               │ │
│   • управляет PushDropTarget / PopDropTarget               │ │
│   • конфигурируется биндингами в инспекторе                │ │
└──────────┬──────────────────────────┬───────────────────────┘
           ↓                          ↓
┌──────────────────┐      ┌──────────────────────────────────┐
│  DragAndDrop     │      │  SelectionManager                │
│  Manager         │      │  InventoryActionBase             │
│  (контракт ниже) │      └──────────────────────────────────┘
└──────────────────┘
```

---

## [HIGH] InputEventRouter — как slot-события доходят до координатора

Координатор per-inventory физически не висит на слотах. Unity EventSystem отправляет
pointer-события только тому GameObject, на котором они произошли.
Поэтому нужен явный механизм пересылки.

### Решение: SlotInputAdapter пересылает все raw-события

`SlotInputAdapter` остаётся тонким — никакой доменной логики, только пересылка:

```
SlotInputAdapter реализует:
  IPointerEnterHandler  → router.RoutePointerEnter(slot, eventData)
  IPointerExitHandler   → router.RoutePointerExit(slot, eventData)
  IPointerDownHandler   → router.RoutePointerDown(slot, eventData)
  IPointerUpHandler     → router.RoutePointerUp(slot, eventData)
  IBeginDragHandler     → router.RouteBeginDrag(slot, eventData)
  ISelectHandler        → router.RouteFocusEnter(slot, FocusSource.Gamepad)
  IDeselectHandler      → router.RouteFocusExit(slot, FocusSource.Gamepad)
  IDropTarget           → остаётся (см. ниже)
```

### InputEventRouter

Принимает raw-события от всех адаптеров, применяет anti-dup политику
и передаёт в нужный координатор:

```
InputEventRouter
  • знает все координаторы (регистрация при Awake)
  • маршрутизирует событие в координатор слота
  • применяет anti-dup (frame gating) — единственная ответственность по фильтрации
```

**НЕ занимается** source priority — у Router нет контекста для этого решения.
Он не знает текущее состояние взаимодействия и не может судить что важнее.

Может быть singleton или инжектироваться через DI.

---

## [HIGH] IDropTarget и PushDropTarget / PopDropTarget

Текущая проблема: `DragDropEventListener` делает PushDropTarget при PointerEnter
и PopDropTarget при PointerExit. В новой архитектуре `SlotInputAdapter`
перестаёт это делать, и дроп-стек в `DragAndDropManager` перестанет работать.

### Решение: координатор управляет дроп-стеком

`SlotInputAdapter` **остаётся** реализовывать `IDropTarget` (только фабрика хэндлера):

```
SlotInputAdapter : IDropTarget
  GetTargetSlot()    → _slot
  GetDropHandler()   → new InventoryDropHandler(...)   // как сейчас
  OnBecomeActiveTarget()    → _slot.Highlight(true)
  OnBecomeInactiveTarget()  → _slot.Highlight(false)
```

Но **PushDropTarget / PopDropTarget вызывает координатор**, когда получает
FocusEnter/Exit и `DragAndDropManager.IsDragging == true`:

```
Coordinator.OnFocusEnter(slot):
  if (dragManager.IsDragging && slot.IsInteractable)
    dragManager.PushDropTarget(slot.Adapter)

Coordinator.OnFocusExit(slot):
  if (dragManager.IsDragging)
    dragManager.PopDropTarget(slot.Adapter)
```

Таким образом IDropTarget и дроп-стек работают как прежде, просто инициатор — координатор.

---

## [MEDIUM] Контракт интеграции с DragAndDropManager

«Без изменений» неверно. Координатор вызывает методы менеджера,
и часть внутренней логики менеджера перестаёт быть нужной.

### Что координатор вызывает у менеджера (публичный контракт — без изменений)
```
DragAndDropManager.StartDrag(IReadOnlyList<ISlot>)   ← старт drag из координатора
DragAndDropManager.CompleteDrag()                    ← координатор решил завершить
DragAndDropManager.CancelDrag()                      ← координатор решил отменить
DragAndDropManager.PushDropTarget(IDropTarget)       ← через координатор
DragAndDropManager.PopDropTarget(IDropTarget)        ← через координатор
```

### Что меняется внутри менеджера
```
SetHoveredSlot(slot, inventory)  — вероятно можно удалить:
  координатор управляет дроп-стеком напрямую через PushDropTarget,
  этот метод был legacy-путём до рефакторинга

Внутренняя quick-click корутина:
  логика "quick click → auto-transfer" переезжает в state machine координатора,
  из менеджера убирается
```

### Обратная связь менеджера → координатор
Менеджер должен уметь сообщать о внешней отмене drag
(например, слот стал недоступен во время drag):
```
DragAndDropManager.OnDragCancelled  ← координатор подписывается,
                                       чтобы сбросить state в Idle
```

---

## [MEDIUM] Anti-dup политика: EventSystem + Input System

Один физический клик мышью может прийти через **оба** канала:
- EventSystem: PointerDown → SlotInputAdapter → Router
- Input System: "Interact" action performed → InventoryInputHandler → Coordinator

### Решение: разделение каналов по типу устройства

**Правило**: мышь и touch — **только через EventSystem**. Input System биндинги
для мыши не используются (документируется как ограничение).

```
Input System bindings:
  Mouse/leftButton    ← НЕ использовать в InventoryInputHandler
  Keyboard/*          ← ОК
  Gamepad/*           ← ОК

EventSystem:
  pointer events      ← всё мышиное/touch идёт сюда
```

Если всё же нужна поддержка мыши в обоих каналах (edge case):
**Frame gating** — Router хранит `_lastHandledFrame[IntentType]`
и игнорирует дублирующий интент в том же frame:

```
Router.RouteIntent(intent, source):
  var key = (intent.Type, intent.Slot)
  if (_handledThisFrame.Contains(key)) return   // дубль — игнорируем
  _handledThisFrame.Add(key)
  coordinator.Dispatch(intent)
  // очищается в LateUpdate
```

---

## [MEDIUM] Focus source-awareness

При одновременном использовании мыши и геймпада (Steam Deck, гибридный ввод)
фокус может прийти из разных источников и создать ложный расфокус.

**Владелец: только Coordinator.** Router форвардит все события как есть,
не фильтрует по источнику — у него нет контекста для этого решения.

### Решение: FocusSource в Coordinator

```csharp
enum FocusSource { None, Mouse, Gamepad, VirtualCursor }
```

Координатор хранит `_activeFocusSource`. Вся логика приоритета сосредоточена здесь:

```
OnFocusEnter(slot, source):
  // Политика: последнее активное устройство вытесняет предыдущее.
  // Mouse движение всегда вытесняет геймпад-навигацию — и наоборот.
  _activeFocusSource = source
  _focusedSlot = slot
  → state machine: FocusEntered

OnFocusExit(slot, source):
  if (source != _activeFocusSource) return  // exit от неактивного источника — игнор
  _focusedSlot = null
  → state machine: FocusLost
```

**Итог**: ложный расфокус от «старого» источника не проходит.
Единственное место с логикой приоритета — `Coordinator.OnFocusEnter/Exit`.

---

## State Machine координатора

```
                    ┌──────────────────────────────────┐
                    │              Idle                │
                    └───┬──────────────────────────────┘
                        │ FocusEntered(slot)
                        ▼
                    ┌──────────────────────────────────┐
              ┌────►│            Focused               │◄── FocusChanged(newSlot)
              │     └───┬──────────────┬───────────────┘
              │         │ Pressed      │ GrabIntent (геймпад)
              │         ▼             ▼
              │  ┌────────────┐  ┌──────────────────────────┐
FocusLost     │  │  Pressed   │  │        GrabMode          │
──────────────┘  └──┬─────┬──┘  │  предмет «поднят»        │
                    │     │     │  ждём навигацию геймпадом │
             Moved  │     │     └──┬──────────────┬─────────┘
             beyond │     │ Up     │ FocusChanged │ Cancel/B
             thresh │     │ quickly│              │
                    ▼     ▼        ▼              ▼
             ┌─────────┐ ┌──────┐ ┌──────────┐  Idle
             │Dragging │ │Click │ │DragHover │
             │следует  │ │выпол-│ │(gamepad) │
             │за       │ │нить  │ └────┬─────┘
             │курсором │ │бинд. │      │ Confirm/A
             └────┬────┘ └──────┘      ▼
                  │ Up           Drop → Idle
                  ▼
            Drop → Idle
```

**Правила владения (ownership rules):**
- `Dragging` / `GrabMode` → selection-интенты игнорируются
- `Clicked` → drag не стартует для текущего события
- `Pressed` → selection не выполняется до Up

---

## Компоненты

### `SlotInputAdapter` (новый, per-slot)
Заменяет `DragDropEventListener` и `SlotPointerSelectionTrigger`.
Только пересылка событий, ноль доменной логики:

```
Реализует:
  IPointerEnterHandler, IPointerExitHandler   → router.RoutePointer*(slot, data)
  IPointerDownHandler, IPointerUpHandler      → router.RoutePointer*(slot, data)
  IBeginDragHandler                           → router.RouteBeginDrag(slot, data)
  ISelectHandler, IDeselectHandler            → router.RouteFocus*(slot, Gamepad)
  IDropTarget                                 → остаётся без изменений
```

### `InputEventRouter` (новый, singleton или DI)
Anti-dup + маршрутизация в нужный координатор:

```
Регистрация: coordinator.Register() при Awake
Маршрутизация: по ISlot → находит координатор инвентаря слота
Anti-dup: frame gating через HashSet, очистка в LateUpdate
```

Router **не знает** о FocusSource и не принимает решений по приоритету.
Он форвардит событие как есть — координатор решит принять или проигнорировать.

### `InventoryInteractionCoordinator` (новый, per-inventory)

```
Поля:
  [SerializeField] UniversalInventory _inventory
  [SerializeField] List<SlotBinding> _bindings

  InteractionState _state
  ISlot _focusedSlot
  FocusSource _activeFocusSource

Публичный API (вызывается из Router):
  RouteRawEvent(SlotRawEvent event)

Управляет:
  dragManager.StartDrag / CompleteDrag / CancelDrag
  dragManager.PushDropTarget / PopDropTarget
  selectionManager.Execute(operation, slot)
  action.Execute(inventory, slot)
```

### `SlotBinding` и `SlotAction` (новые, конфигурация)

```
SlotBinding (abstract, [SerializeReference])
  ├── InputActionBinding     [InputActionReference + Modifier] → SlotAction
  ├── PointerButtonBinding   [MouseButton + Modifier] → SlotAction
  └── NavigationBinding      [Submit | Cancel] → SlotAction

SlotAction (abstract, [SerializeReference])
  ├── DragSlotAction         StartDrag или Drop в зависимости от state
  ├── CancelDragAction       CancelDrag
  ├── SelectionSlotAction    обёртка над SelectionOperationBase
  └── InventorySlotAction    обёртка над InventoryActionBase
```

### `InventoryInputHandler` (изменяется)

```
Было:   action.Execute(_inventory, _inventory.ResolveAutoTransferSlot())
Станет: router.RouteInputAction(actionBinding, context)
        // Router передаст в coordinator с текущим focusedSlot
```

---

## Конфигурация в инспекторе

**Мышь + клавиатура:**
```
  PointerButtonBinding  LMB, None   → DragSlotAction
  PointerButtonBinding  LMB, Ctrl   → SelectionSlotAction [ToggleSlotOperation]
  PointerButtonBinding  LMB, Shift  → SelectionSlotAction [RangeSelectOperation]
  PointerButtonBinding  RMB, None   → SelectionSlotAction [ClearAndSelectOperation]
  InputActionBinding    "AutoTransfer" → InventorySlotAction [AutoTransferAction]
  InputActionBinding    "SortInventory" → InventorySlotAction [SortInventoryAction]
```

**Геймпад (grab mode):**
```
  InputActionBinding    "Interact"  → DragSlotAction      // A = поднять/положить
  InputActionBinding    "Secondary" → SelectionSlotAction [ToggleSlotOperation]
  NavigationBinding     Submit      → DragSlotAction       // подтвердить дроп
  NavigationBinding     Cancel      → CancelDragAction     // B = отмена
```

Разные инвентари на сцене могут иметь разные конфиги координатора.

---

## Геймпад: два пути фокуса

### Путь 1: UI Selectable Navigation
Слоты наследуют `Selectable`, настраивается стандартная UI Navigation.
`SlotInputAdapter` получает `ISelectHandler` от EventSystem.
**Требует:** правильной настройки Explicit Navigation в инспекторе на каждом слоте.

### Путь 2: Virtual Cursor
Кастомный курсор управляется стиком через Input System, двигается по экрану.
EventSystem raycast по позиции курсора → `IPointerEnterHandler` срабатывает как обычно.
`FocusSource = VirtualCursor`.
**Нужен для:** World Space Canvas, нестандартных раскладок.

Router не знает какой путь используется — получает одинаковый `RouteFocusEnter`.

---

## Что не меняется

| Компонент | Статус |
|---|---|
| `SelectionManager` | Без изменений |
| `SelectionOperationBase` и все наследники | Без изменений |
| `InventoryActionBase` и все наследники | Без изменений |
| `TransferPlanner` / `TransferPlanExecutor` | Без изменений |
| `DropPolicy`, `DragContext`, правила | Без изменений |
| `IDropTarget` интерфейс | Без изменений |
| `DragAndDropManager` публичный API | Без изменений (SetHoveredSlot — под вопросом) |

| Компонент | Статус |
|---|---|
| `DragAndDropManager` внутренняя quick-click логика | Переезжает в state machine |
| `InventoryInputHandler` | Рефакторинг: отправляет в Router |
| `DragDropEventListener` | Удаляется |
| `SlotPointerSelectionTrigger` | Удаляется |

---

## Порядок реализации

### Шаг 1 — `SlotInputAdapter` (без удаления старых компонентов)
Тонкий компонент-пересылка. Можно добавить рядом с существующими.
Старые компоненты пока не удалять — они продолжают работать параллельно.

### Шаг 2 — `InputEventRouter`
Singleton. Пока только принимает события и логирует — без реальной маршрутизации.
Тестируем что все slot-события доходят корректно.
Добавляем frame gating.

### Шаг 3 — `InteractionState` state machine
Чистый C# класс, без Unity-зависимостей.
Покрывается unit-тестами отдельно от Unity.

### Шаг 4 — `SlotAction` иерархия
`DragSlotAction`, `CancelDragAction`, `SelectionSlotAction`, `InventorySlotAction`.
Обёртки над существующими типами — минимальный код.

### Шаг 5 — `SlotBinding` иерархия
`InputActionBinding`, `PointerButtonBinding`, `NavigationBinding`.

### Шаг 6 — `InventoryInteractionCoordinator`
Собирает state machine + биндинги + домен.
На этом шаге подключаем реальную маршрутизацию в Router.
Тестируем мышь — старые компоненты ещё живы как fallback.

### Шаг 7 — Интеграция с `DragAndDropManager`
Убираем quick-click корутину из менеджера.
Проверяем что PushDropTarget/PopDropTarget работают через координатор.
Удаляем `SetHoveredSlot` если он больше не нужен.

### Шаг 8 — Рефакторинг `InventoryInputHandler`
Переводим на Router.Dispatch вместо прямого вызова действий.

### Шаг 9 — Удаление старых компонентов
`DragDropEventListener` и `SlotPointerSelectionTrigger` удаляются.
Обновляем все префабы слотов.

### Шаг 10 — Геймпад
Добавляем `NavigationBinding`. Тестируем оба пути фокуса.
