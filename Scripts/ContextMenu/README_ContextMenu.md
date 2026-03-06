# Context Menu System

Система контекстного меню для слотов инвентаря. Меню вызывается через существующую систему биндингов (правый клик мышью, кнопка геймпада и т.д.) и наполняется пунктами из ScriptableObject-ов, условно зависящих от инвентаря и предмета в слоте.

---

## Архитектура

```
ContextMenuEntryDefinitionSO   — один пункт меню (SO, субклассируется в проекте)
ContextMenuPreset              — список пунктов (SO, назначается на инвентарь)
ContextMenuBinder              — компонент на GO инвентаря, хранит пресеты
ContextMenuContext             — контекст клика (inventory / slot / item / позиция / устройство)
ContextMenuManager             — синглтон, фильтрует CanShow, сортирует, передаёт во View
ContextMenuViewBase            — абстрактный MonoBehaviour-вью (реализуется в проекте)
ShowContextMenuAction          — SlotInteractionAction, запускает весь pipeline
```

### Поток выполнения

```
Пользователь кликает (ПКМ / геймпад)
  → InputEventRouter
    → ShowContextMenuAction.Execute(inventory, adapter, eventData)
      → ContextMenuBinder.GetEntries(slotIsEmpty)        // выбор пресета
        → ContextMenuManager.Show(entries, ctx)
          → entries.Where(e => e.CanShow(ctx))           // фильтрация
            → entries.OrderBy(e => e.Order)              // сортировка
              → ContextMenuViewBase.Show(visible, ctx)   // отображение UI
```

При выборе пункта View вызывает `entry.Execute(ctx)` напрямую.

---

## Быстрый старт

### 1. ContextMenuManager на сцене

Создайте пустой GO, добавьте компонент `ContextMenuManager`. Назначьте свою реализацию `ContextMenuViewBase` в поле **View**.

```
[ContextMenuManager]
  └── View → MyContextMenuView
```

### 2. Создайте пункты меню (SO)

`Assets → Create → DragAndDrop → ContextMenu → Built-in → Sort`
или свой класс (см. раздел [Создание пунктов](#создание-пунктов-меню)).

### 3. Создайте пресет

`Assets → Create → DragAndDrop → ContextMenu → Preset`

Добавьте нужные SO-пункты в список **Entries**.

### 4. ContextMenuBinder на инвентаре

Добавьте компонент `ContextMenuBinder` на тот же GO, что и `UniversalInventory`.

| Поле | Назначение |
|---|---|
| **Preset** | Пресет для непустых слотов |
| **Empty Slot Preset** | Пресет для пустых слотов (опционально) |

### 5. Привяжите действие к вводу

В `InventoryExtraInteractionBinder` (или в глобальном `InteractionBindingsProfile`) добавьте биндинг:

**Мышь — правый клик:**
```
Pointer Bindings → [+]
  Button:       Right
  Trigger Phase: Click Short
  Action:       ShowContextMenuAction
```

**Геймпад / клавиатура:**
```
Navigation Bindings → [+]
  Event Type: Cancel   (или другая кнопка)
  Action:     ShowContextMenuAction
```

---

## Создание пунктов меню

Субклассируйте `ContextMenuEntryDefinitionSO` в своём проекте:

```csharp
[CreateAssetMenu(menuName = "MyGame/ContextMenu/Use Item")]
public class UseItemMenuEntrySO : ContextMenuEntryDefinitionSO
{
    // Показывать только для расходуемых предметов
    public override bool CanShow(ContextMenuContext ctx)
        => ctx.Item is IConsumable;

    // Динамический лейбл: "Использовать (x3)"
    public override string GetLabel(ContextMenuContext ctx)
        => $"Использовать (x{ctx.ItemCount})";

    public override void Execute(ContextMenuContext ctx)
        => GameManager.Instance.UseItem(ctx.Inventory, ctx.Slot);
}
```

### ContextMenuContext

| Поле | Тип | Описание |
|---|---|---|
| `Inventory` | `UniversalInventory` | Инвентарь, на котором открыто меню |
| `Slot` | `ISlot` | Слот под курсором (может быть null) |
| `Item` | `IInventoryItem` | Предмет в слоте (null если пусто) |
| `ItemCount` | `int` | Количество в стаке |
| `ScreenPosition` | `Vector2` | Экранная позиция клика |
| `InputSource` | `FocusSource` | Mouse / Gamepad / VirtualCursor |

### Порядок пунктов

Поле **Order** (int) в каждом SO определяет позицию в меню — меньше = выше. Пункты с одинаковым Order сохраняют порядок из пресета.

---

## Встроенные пункты

### SortContextMenuEntrySO

`Assets → Create → DragAndDrop → ContextMenu → Built-in → Sort`

| Поле | Описание |
|---|---|
| **Label** | Текст пункта |
| **Sort Type** | ByName / ByItemId / ByStackSize |
| **Reverse** | Обратный порядок сортировки |
| **Order** | Позиция в меню |

Показывается только если в инвентаре есть хотя бы один предмет.

---

## Реализация View

Наследуйтесь от `ContextMenuViewBase` и реализуйте два метода:

```csharp
public class MyContextMenuView : ContextMenuViewBase
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Transform  _itemsContainer;
    [SerializeField] private MenuItemWidget _itemPrefab;

    private ContextMenuContext _ctx;

    public override void Show(IReadOnlyList<ContextMenuEntryDefinitionSO> entries, ContextMenuContext ctx)
    {
        _ctx = ctx;

        // Очистить старые кнопки
        foreach (Transform child in _itemsContainer)
            Destroy(child.gameObject);

        // Создать кнопку для каждого пункта
        foreach (var entry in entries)
        {
            var widget = Instantiate(_itemPrefab, _itemsContainer);
            widget.Setup(entry.GetLabel(ctx), entry.GetIcon(ctx), () =>
            {
                entry.Execute(_ctx);
                Hide();
            });
        }

        // Позиционировать у курсора
        _root.transform.position = ctx.ScreenPosition;
        _root.SetActive(true);
    }

    public override void Hide()
    {
        _root.SetActive(false);
    }
}
```

---

## Несколько пресетов на разные инвентари

Каждый `ContextMenuBinder` имеет свой пресет — пресеты не зависят друг от друга.

```
[PlayerInventory GO]
  ├── UniversalInventory
  └── ContextMenuBinder
        Preset           → PlayerContextMenu.asset   (Use, Equip, Drop)
        Empty Slot Preset → (не задан)

[MerchantInventory GO]
  ├── UniversalInventory
  └── ContextMenuBinder
        Preset           → MerchantContextMenu.asset  (Buy)
        Empty Slot Preset → (не задан)
```

---

## Закрытие меню

`ContextMenuManager.Instance.Hide()` — закрывает меню программно.

Типичные места вызова:
- При начале перетаскивания (`DragAndDropManager.OnDragStarted`)
- При клике вне меню (обработка в самом View)
- В `ContextMenuViewBase.Hide()` после выбора пункта
