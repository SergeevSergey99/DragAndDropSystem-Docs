# Быстрый старт

Это руководство создаёт два обычных инвентаря, между которыми можно перетаскивать предметы.

Сразу настрой ожидания правильно: даже в базовом сценарии вам обычно нужно написать немного интеграционного кода под свои данные. В этом руководстве это будут:

- `ItemSO` как ваши данные предмета
- `ItemSOAdapter` как представление для системы инвентаря
- `BackpackBinding` как мост между UI и вашим списком данных

Результат:

```mermaid
flowchart LR
    A["Рюкзак"] <-->|drag & drop| B["Сундук"]
```

---

## Что понадобится

- `DragAndDropManager` на сцене
- `EventSystem`
- `Canvas`
- два объекта с `UniversalInventory`
- один тип предмета
- один `ListInventoryDataBinding`

---

## Шаг 1. Подготовьте сцену

1. Добавьте на сцену `DragAndDropManager` из `Prefabs/DragingObj`.
2. Убедитесь, что на сцене есть `EventSystem`.
3. Создайте `Canvas`, если его ещё нет.

> На сцене нужен один `DragAndDropManager`. Он управляет всеми операциями переноса.

---

## Шаг 2. Создайте два инвентаря

1. Создайте внутри `Canvas` объект `Backpack`.
2. Добавьте на него `UniversalInventory`.
3. Укажите:

| Поле | Значение |
|---|---|
| `Item Behavior` | `Unique` для самого простого старта |
| `Slot Management` | `Fixed` |
| `Initial Slot Count` | например `10` |
| `Slot Prefab` | `Prefabs/Slot` |
| `Slot Container` | родитель для слотов |

4. Скопируйте объект и сделайте второй инвентарь, например `Chest`.

> Если нужен самый простой первый запуск, не начинайте со `Stackable` и `Dynamic`.

---

## Шаг 3. Опишите тип предмета

```csharp
[CreateAssetMenu(menuName = "Game/Item")]
public class ItemSO : ScriptableObject
{
    [field: SerializeField] public string ItemName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
}
```

И добавьте адаптер, который реализует `IInventoryItem`:

```csharp
using DragAndDropSystem.Core;
using UnityEngine;

public class ItemSOAdapter : IInventoryItem
{
    public readonly ItemSO Data;

    public ItemSOAdapter(ItemSO data) => Data = data;

    public string ItemId => Data.GetInstanceID().ToString();
    public Sprite Icon => Data.Icon;
    public string DisplayName => Data.ItemName;
}
```

---

## Шаг 4. Добавьте простой binding

```csharp
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using System.Collections.Generic;
using UnityEngine;

public class BackpackBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    [SerializeField] private List<ItemSO> _items;

    protected override IReadOnlyList<ItemSO> GetItems() => _items;
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);
    protected override ItemSO ExtractData(ItemSOAdapter adapter) => adapter.Data;
    protected override void AddToData(InventoryItemEventContext ctx, ItemSO item) => _items.Add(item);
    protected override void RemoveFromData(InventoryItemEventContext ctx, ItemSO item) => _items.Remove(item);
}
```

Повесьте такой binding на оба инвентаря. У каждого будет свой список `_items`.

---

## Шаг 5. Заполните стартовые данные

1. Создайте несколько `ItemSO`.
2. Добавьте их в список `_items` у `BackpackBinding` и `ChestBinding`.
3. Запустите сцену.

Если всё настроено правильно:

- оба инвентаря покажут предметы
- предмет можно перетащить из одного инвентаря в другой
- список данных обновится автоматически

---

## Что происходит под капотом

```mermaid
flowchart LR
    A["Игрок тащит предмет"] --> B["UniversalInventory выполняет перенос"]
    B --> C["DataBinding получает событие"]
    C --> D["Ваш List<ItemSO> обновляется"]
```

На этом этапе вам не нужно думать про стратегии, конвертеры и хуки переноса.

---

## Что дальше

- [Экипировка](../examples/equipment.md) — если слоты должны иметь назначение
- [Торговля](../examples/trading.md) — если инвентари используют разные типы данных
- [Привязка данных](../architecture/data-binding.md) — если нужно понять lifecycle и точки расширения
