# Selection System — Система выделения слотов

**Last Updated**: 2026-02-28

## Совместимость с transfer pipeline

Selection-модуль не меняет перенос напрямую, но корректно работает с новой моделью drop/swap через `DragAndDropManager` и `InventoryDropHandler`.


Система позволяет выделять один или несколько слотов из разных инвентарей и выполнять над ними произвольные действия: массовый перенос, показ суммарной стоимости, прокачку, удаление и любую другую логику.

---

## 🏗️ Архитектура

```
SlotPointerSelectionTrigger     InputActionSelectionTrigger     ButtonSelectionTrigger
  (Ctrl/Shift/клик на слоте)       (хоткей Ctrl+A, Escape...)      (UI кнопка)
           │                                  │                           │
           └──────────────────────────────────┼───────────────────────────┘
                                              ▼
                                    SelectionOperationBase
                            (ToggleSlot / RangeSelect / SelectAll /
                             ClearSelection / SelectByCondition / ваша логика)
                                              │
                                              ▼
                                    SelectionManager  ←────────── SelectionContext
                                    (синглтон, состояние)         (неизменяемый снимок)
                                              │
                          ┌───────────────────┴───────────────────┐
                          ▼                                         ▼
                 SlotSelectionView                         ваш код: подписка на
              (визуал выделения на слоте)              OnSelectionChanged → действие
                                                      (показать цену, прокачать...)
```

**Ключевой принцип**: триггеры отвечают за *когда*, операции — за *что делать с выделением*. Они не знают друг о друге и свободно комбинируются.

---

## 📋 Компоненты

### SelectionContext — неизменяемый снимок

Создаётся `SelectionManager` при каждом изменении выделения. Публичные свойства только для чтения — изменить состояние можно исключительно через `SelectionManager`.

```csharp
public sealed class SelectionContext
{
    // Сгруппировано по инвентарям — для логики с разными правилами на инвентарь
    public IReadOnlyDictionary<IInventory, IReadOnlyList<ISlot>> ByInventory { get; }

    // Плоский список — для простых действий, которым инвентарь не важен
    public IReadOnlyList<ISlot> AllSlots { get; }

    public bool HasSelection   { get; }
    public int TotalSlotsCount { get; }
    public int InventoryCount  { get; }

    // O(1) — используется в SlotSelectionView на каждом слоте
    public bool Contains(ISlot slot);
}
```

Зачем `ByInventory`? Когда у двух торговцев разные наценки:

```csharp
foreach (var (inventory, slots) in context.ByInventory)
{
    float markup = (inventory.DataBinding as IMerchantBinding)?.Markup ?? 1f;
    foreach (var slot in slots)
        total += slot.Stack.Item is IPriceable p ? p.Price * slot.Stack.Count * markup : 0;
}
```

---

### SelectionManager — синглтон

Управляет состоянием. Внутри — мутабельные `HashSet` и `Dictionary`, снаружи — только `SelectionContext`.

```csharp
// Основное API
SelectionManager.Instance.Select(slot);
SelectionManager.Instance.Deselect(slot);
SelectionManager.Instance.Toggle(slot);
SelectionManager.Instance.SelectRange(toSlot);   // от последнего выделенного до toSlot
SelectionManager.Instance.SelectAll(inventory);
SelectionManager.Instance.Clear();

SelectionManager.Instance.IsSelected(slot);       // bool
SelectionManager.Instance.CurrentContext;          // SelectionContext

// Событие на любое изменение
SelectionManager.Instance.OnSelectionChanged += (sender, args) =>
{
    var context = args.Context;
    Debug.Log($"Выделено {context.TotalSlotsCount} слотов");
};
```

---

## 🔧 Операции выделения (`SelectionOperationBase`)

Операции — это `[Serializable]` классы (не MonoBehaviour), которые назначаются через `[SerializeReference]` в инспекторе. Одна операция может использоваться в нескольких триггерах одновременно.

### Встроенные операции

| Операция | Описание | Типичное использование |
|---|---|---|
| `ToggleSlotOperation` | Добавить/убрать слот из выделения | Ctrl+Click |
| `ClearAndSelectOperation` | Снять всё и выделить один слот | Обычный клик |
| `SelectSlotOperation` | Добавить к выделению без сброса | — |
| `RangeSelectOperation` | Диапазон от последнего до этого | Shift+Click |
| `SelectAllOperation` | Выделить все слоты в инвентаре | Ctrl+A |
| `ClearSelectionOperation` | Снять всё выделение | Escape |
| `SelectByConditionOperation` | Абстрактная база для фильтров | Кнопка "все оружия" |

---

## ⚡ Триггеры (`SelectionTriggerBase`)

Триггеры — MonoBehaviour, определяют *когда* вызывать операцию.

### `SlotPointerSelectionTrigger` — клик на слоте

Добавьте на тот же GameObject что и `UniversalSlot`. Поддерживает комбинации модификаторов.

**Инспектор:**
```
SlotPointerSelectionTrigger
└── Bindings:
    ├── [0] Modifier: None  → ClearAndSelectOperation
    ├── [1] Modifier: Ctrl  → ToggleSlotOperation
    └── [2] Modifier: Shift → RangeSelectOperation
```

Биндинги проверяются сверху вниз, выполняется первый подходящий.

### `InputActionSelectionTrigger` — Input System

```
InputActionSelectionTrigger
├── Action Reference: [SelectAll]   ← Input System action (Ctrl+A)
├── Trigger Phase: Performed
└── Operation: SelectAllOperation → Inventory: PlayerInventory
```

### `ButtonSelectionTrigger` — UI кнопка

```
Button "SelectWeapons"
└── ButtonSelectionTrigger
    └── Operation: SelectByWeaponTypeOperation
```

---

## 🎨 SlotSelectionView — визуал на слоте

Добавьте на тот же GameObject что и `UniversalSlot`. Подписывается на `SelectionManager` и обновляет UI при изменении выделения. Не знает ничего о том, как произошло выделение.

**Инспектор:**
```
SlotSelectionView
├── Slot: [UniversalSlot]
├── Selection Highlight: [GameObject с рамкой]    ← вкл/выкл при выделении
├── Background Graphic: [Image фона слота]         ← меняет цвет
├── Selected Color: (255, 217, 25)
└── Default Color: (255, 255, 255)
```

**Из кода** — подписка на смену состояния конкретного слота:
```csharp
_slotSelectionView.OnSelectionStateChanged += isSelected =>
{
    // Анимация, звук, эффект частиц...
    _animator.SetBool("Selected", isSelected);
};
```

---

## 🛠️ Создание своей операции

### Пример 1: Выделить все предметы определённого типа

```csharp
[Serializable]
public class SelectByItemTypeOperation : SelectByConditionOperation
{
    [SerializeField] private string _itemType;

    protected override bool Matches(ISlot slot) =>
        slot.Stack.Item is ITypedItem typed && typed.ItemType == _itemType;
}
```

Готово. Теперь в инспекторе через `[SerializeReference]` появится `SelectByItemTypeOperation`:
```
ButtonSelectionTrigger
└── Operation: SelectByItemTypeOperation
    ├── Inventory: PlayerInventory
    ├── Item Type: "Weapon"
    └── Clear First: true
```

---

### Пример 2: Показать суммарную стоимость выделенного

Операция не нужна — достаточно подписки на `OnSelectionChanged`:

```csharp
public class SelectionPriceDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text _priceLabel;

    private void OnEnable()  => SelectionManager.Instance.OnSelectionChanged += Refresh;
    private void OnDisable() => SelectionManager.Instance.OnSelectionChanged -= Refresh;

    private void Refresh(object sender, SelectionChangedEventArgs args)
    {
        int total = 0;

        // ByInventory позволяет учесть наценку каждого торговца
        foreach (var (inventory, slots) in args.Context.ByInventory)
        {
            float markup = (inventory.DataBinding as IMerchantBinding)?.Markup ?? 1f;

            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.Item is IPriceable item)
                    total += Mathf.RoundToInt(item.Price * slot.Stack.Count * markup);
            }
        }

        _priceLabel.text = total > 0 ? $"Итого: {total}g" : "";
    }
}
```

---

### Пример 3: Прокачать все выделенные предметы

```csharp
[Serializable]
public class UpgradeSelectedOperation : SelectionOperationBase
{
    public override string DisplayName => "Upgrade Selected";

    public override bool Execute(SelectionManager manager, ISlot contextSlot = null)
    {
        bool any = false;
        foreach (var slot in manager.CurrentContext.AllSlots)
        {
            if (slot.IsEmpty) continue;
            if (slot.Stack.Item is IUpgradeable upgradeable && upgradeable.CanUpgrade)
            {
                upgradeable.Upgrade();
                slot.UpdateVisuals();
                any = true;
            }
        }
        return any;
    }

    public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null) =>
        base.CanExecute(manager, contextSlot) && manager.CurrentContext.HasSelection;
}
```

---

### Пример 4: Массовый перенос в другой инвентарь

```csharp
[Serializable]
public class BatchTransferOperation : SelectionOperationBase
{
    [SerializeField] private UniversalInventory _targetInventory;

    public override string DisplayName => "Batch Transfer";

    public override bool Execute(SelectionManager manager, ISlot contextSlot = null)
    {
        var slots = new List<ISlot>(manager.CurrentContext.AllSlots);
        manager.Clear();  // снимаем выделение до переноса

        var transferService = DragAndDropManager.Instance.TransferService;
        bool any = false;

        foreach (var slot in slots)
        {
            if (slot.IsEmpty) continue;
            var request = new InventoryTransferRequest(
                slot.Inventory, slot, _targetInventory, null, slot.Stack, true);
            var result = transferService.TryExecuteTransfer(request);
            if (result.IsSuccess) any = true;
        }

        return any;
    }

    public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null) =>
        base.CanExecute(manager, contextSlot)
        && manager.CurrentContext.HasSelection
        && _targetInventory != null;
}
```

---

## 🚀 Быстрый старт

### Шаг 1: Настроить префаб слота

```
Prefab "Slot"
├── UniversalSlot
├── DragDropEventListener           ← уже был
│
├── SlotSelectionView               ← добавить
│   ├── Selection Highlight: [рамка]
│   └── Background Graphic: [Image]
│
└── SlotPointerSelectionTrigger     ← добавить
    └── Bindings:
        ├── None  → ClearAndSelectOperation
        ├── Ctrl  → ToggleSlotOperation
        └── Shift → RangeSelectOperation
```

### Шаг 2: Добавить хоткей Ctrl+A (опционально)

```
GameObject "SelectionHandlers"
└── InputActionSelectionTrigger
    ├── Action: SelectAll
    └── Operation: SelectAllOperation → Inventory: PlayerInventory
```

### Шаг 3: Добавить реакцию на выделение

Вариант A — кнопка выполняет действие:
```
Button "SellSelected"
└── ButtonSelectionTrigger
    └── Operation: SellSelectedOperation (ваша реализация)
```

Вариант B — UI реагирует на изменение выделения:
```csharp
SelectionManager.Instance.OnSelectionChanged += (_, args) =>
    _sellButton.interactable = args.Context.HasSelection;
```

---

## ✅ Best Practices

1. **Не держите ссылку на `SelectionContext` надолго** — это снимок, он устаревает при следующем изменении выделения
2. **Проверяйте `slot.IsEmpty`** в операциях — выделенный слот может стать пустым после переноса
3. **`ByInventory` для финансовой/правовой логики**, `AllSlots` для простых операций
4. **Один `SlotPointerSelectionTrigger`** покрывает всё: None/Ctrl/Shift/Alt через список биндингов
5. **`SelectByConditionOperation`** как база для фильтров — не пишите `SelectionOperationBase` с нуля если нужна фильтрация по слотам

---

**Версия**: 1.0
