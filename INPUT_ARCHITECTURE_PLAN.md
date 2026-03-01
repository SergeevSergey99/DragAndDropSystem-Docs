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
│  Геймпад A ──┼──→  InputAction "Interact"                   │
│  Клавиша E ──┘                                              │
│                                                             │
│  Мышь RMB ───┐                                              │
│  Геймпад X ──┼──→  InputAction "SecondaryInteract"          │
│  Клавиша Q ──┘                                              │
│                                                             │
│  Геймпад D-pad/стик ──→  UI Navigation (Selectable)         │
│                      или Virtual Cursor (World Space UI)    │
└─────────────────────────────┬───────────────────────────────┘
                              ↓  Intent
┌─────────────────────────────────────────────────────────────┐
│                  СЛОЙ ВЗАИМОДЕЙСТВИЯ                        │
│                                                             │
│        InventoryInteractionCoordinator  (per-inventory)     │
│                                                             │
│  • Единственный получатель всех интентов                    │
│  • Владеет state machine                                    │
│  • Диспатчит в домен после принятия решения                 │
│  • Конфигурируется биндингами в инспекторе                  │
│                                                             │
│        SlotInputAdapter  (per-slot, тонкий)                 │
│                                                             │
│  • IPointerEnterHandler / IPointerExitHandler               │
│  • ISelectHandler / IDeselectHandler  (геймпад навигация)   │
│  • Только: coordinator.NotifyFocusChanged(slot, true/false) │
│  • Ноль доменной логики                                     │
└──────────┬──────────────────────────┬───────────────────────┘
           ↓                          ↓
┌──────────────────┐      ┌──────────────────────────────────┐
│  DragAndDrop     │      │  SelectionManager                │
│  Manager         │      │  InventoryActionBase             │
└──────────────────┘      └──────────────────────────────────┘
```

---

## State Machine координатора

Явные состояния устраняют гонки: пока занято одно состояние — другие интенты игнорируются.

```
                    ┌──────────────────────────────────┐
                    │              Idle                │
                    └───┬──────────────────────────────┘
                        │ FocusEntered(slot)
                        ▼
                    ┌──────────────────────────────────┐
                    │            Focused               │◄──── FocusChanged(newSlot)
                    └───┬──────────────┬───────────────┘
                        │ Pressed      │ GrabIntent (геймпад)
                        ▼             ▼
    ┌──────────────────────┐   ┌──────────────────────────────┐
    │       Pressed        │   │          GrabMode            │
    └──┬────────────┬──────┘   │  (предмет «поднят», виден    │
       │            │          │   визуал, ждём навигацию)    │
       │ Moved      │ Released └──┬──────────────┬────────────┘
       │ beyond     │ quickly     │ FocusChanged │ Cancel
       │ threshold  │             │ (navigate)   │
       ▼            ▼             ▼              ▼
┌──────────┐  ┌──────────┐  ┌──────────┐    Idle
│ Dragging │  │ Clicked  │  │ DragHover│
│          │  │          │  │ (gamepad)│
│ следует  │  │ выполнить│  └────┬─────┘
│ за       │  │ биндинг  │       │ Confirm
│ курсором │  └──────────┘       ▼
└────┬─────┘               Drop → Idle
     │ Released
     ▼
  Drop → Idle
```

**Правило владения:** `Dragging` и `GrabMode` блокируют все selection-интенты.
`Clicked` блокирует старт drag для текущего события.

---

## Компоненты

### `InventoryInteractionCoordinator` (новый, per-inventory)

```
Поля:
  [SerializeField] UniversalInventory _inventory
  [SerializeField] List<SlotBinding> _bindings   ← конфигурация в инспекторе

  InteractionState _state   ← state machine
  ISlot _focusedSlot        ← текущий слот в фокусе

Получает:
  NotifyFocusChanged(ISlot slot, bool entered)   ← от SlotInputAdapter
  Dispatch(InteractionIntent intent)             ← от InventoryInputHandler и Input System

Диспатчит в домен:
  DragAndDropManager.StartDrag / CompleteDrag / CancelDrag
  SelectionManager.Execute(operation, slot)
  InventoryActionBase.Execute(inventory, slot)
```

### `SlotInputAdapter` (новый, per-slot, заменяет DragDropEventListener + SlotPointerSelectionTrigger)

```
Реализует:
  IPointerEnterHandler   → coordinator.NotifyFocusChanged(slot, true)
  IPointerExitHandler    → coordinator.NotifyFocusChanged(slot, false)
  ISelectHandler         → coordinator.NotifyFocusChanged(slot, true)   // геймпад
  IDeselectHandler       → coordinator.NotifyFocusChanged(slot, false)  // геймпад
  IDropTarget            → остаётся (логика дропа не меняется)

НЕ реализует:
  IPointerDownHandler    ← убирается отсюда, живёт в координаторе
  IPointerClickHandler   ← убирается отсюда, живёт в координаторе
  IBeginDragHandler      ← убирается отсюда, живёт в координаторе
```

### `InventoryInputHandler` (изменяется)

```
Было:
  HandleAction() → _inventory.ResolveAutoTransferSlot() → action.Execute()

Станет:
  HandleAction() → coordinator.Dispatch(new InteractionIntent(action, context))

Координатор сам знает focusedSlot — дублирования нет.
Keyboard и gamepad-кнопки становятся просто ещё одним источником интентов.
```

### `SlotBinding` (новый, конфигурация)

```csharp
// [SerializeReference] в инспекторе — можно выбрать тип биндинга

SlotBinding (abstract)
  ├── InputActionBinding     [InputActionReference] → SlotAction
  ├── PointerButtonBinding   [MouseButton + Modifier] → SlotAction
  └── NavigationBinding      [NavigationEvent] → SlotAction  // Submit, Cancel
```

### `SlotAction` (новый, полиморфный)

```csharp
// [SerializeReference] — переиспользует существующие типы

SlotAction (abstract)
  ├── DragSlotAction           // StartDrag / подтвердить дроп
  ├── SelectionSlotAction      // обёртка над SelectionOperationBase (без изменений)
  └── InventorySlotAction      // обёртка над InventoryActionBase (без изменений)
```

---

## Конфигурация в инспекторе

**Пример: мышь + клавиатура**
```
Bindings:
  PointerButtonBinding  LMB, None   → DragSlotAction
  PointerButtonBinding  LMB, Ctrl   → SelectionSlotAction [ToggleSlotOperation]
  PointerButtonBinding  LMB, Shift  → SelectionSlotAction [RangeSelectOperation]
  PointerButtonBinding  RMB, None   → SelectionSlotAction [ClearAndSelectOperation]
  InputActionBinding    "AutoTransfer" → InventorySlotAction [AutoTransferAction]
```

**Пример: геймпад (grab mode)**
```
Bindings:
  InputActionBinding    "Interact"   → DragSlotAction          // A = поднять/положить
  InputActionBinding    "Secondary"  → SelectionSlotAction [Toggle]  // X = выделить
  NavigationBinding     Submit       → DragSlotAction          // подтвердить дроп
  NavigationBinding     Cancel       → CancelDragAction        // B = отмена
```

Разные конфиги — просто разные биндинги, без изменения кода.

---

## Геймпад: два пути получения фокуса

### Путь 1: UI Selectable (простой)
Слоты наследуют `Selectable`, настраивается стандартная UI Navigation.
`SlotInputAdapter` реализует `ISelectHandler`.
**Требует:** правильной настройки Navigation в инспекторе.

### Путь 2: Virtual Cursor (гибкий)
Кастомный курсор управляется стиком через Input System.
EventSystem raycast по позиции курсора → `SlotInputAdapter.OnPointerEnter`.
**Нужен для:** World Space Canvas, нестандартных раскладок слотов.

Координатор не знает какой путь используется — получает одинаковый `NotifyFocusChanged`.

---

## Что не меняется

| Компонент | Статус |
|---|---|
| `DragAndDropManager` | Без изменений |
| `SelectionManager` | Без изменений |
| `SelectionOperationBase` и все наследники | Без изменений |
| `InventoryActionBase` и все наследники | Без изменений |
| `TransferPlanner` / `TransferPlanExecutor` | Без изменений |
| `DropPolicy`, `DragContext`, правила | Без изменений |
| `IDropTarget` и логика дропа | Без изменений |

---

## Порядок реализации

1. **`SlotInputAdapter`** — тонкий компонент, только нотификации. Можно добавить рядом с существующими компонентами без их удаления.

2. **`InteractionState` state machine** — чистый C# класс, без Unity зависимостей. Покрывается юнит-тестами.

3. **`SlotAction` иерархия** — обёртки над существующими типами. `DragSlotAction`, `SelectionSlotAction`, `InventorySlotAction`.

4. **`SlotBinding` иерархия** — конфигурация биндингов для инспектора.

5. **`InventoryInteractionCoordinator`** — собирает всё вместе. На этом этапе `DragDropEventListener` и `SlotPointerSelectionTrigger` можно удалить.

6. **`InventoryInputHandler`** — рефакторинг на `Dispatch(intent)` вместо прямого вызова.

7. **Геймпад** — добавить `NavigationBinding`, протестировать оба пути фокуса.
