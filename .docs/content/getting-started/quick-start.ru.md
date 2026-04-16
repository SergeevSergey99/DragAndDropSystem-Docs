# Быстрый старт

Это руководство создаёт два обычных инвентаря, между которыми можно перетаскивать предметы. 

Подобное уже реализовано в первом примере и финальный результат можно посмотреть там.

Даже в базовом сценарии вам обычно нужно написать немного интеграционного кода под свои данные. В этом руководстве это будут:

- `ItemSO` как ваши данные предмета
- `ItemSOAdapter` как представление для системы инвентаря
- `SimpleBinding` как мост между UI и вашим списком данных

## Зависимости

- Обязательный пакет: `Unity.ugui`
- Опциональный пакет: `com.unity.inputsystem`

Для этого quick start новый Input System **не требуется**. Базовый drag and drop через указатель работает без него.
Если вам нужны `InputAction` bindings, навигационная модальность от геймпада или `InputActionSelectionTrigger`, установите `com.unity.inputsystem`.

## Шаг 1. Подготовьте сцену

1. Создайте `Canvas` на котором будут располагаться инвентрари, если его ещё нет.
2. Добавьте на сцену `DragAndDropManager`. Можно перетащить на сцену префаб из `Prefabs/DragCanvas.prefab`.
> На сцене нужен один `DragAndDropManager`. Он управляет всеми операциями переноса и контролирует объект переносимого объекта.
3. Убедитесь, что на сцене есть `EventSystem`.


---

## Шаг 2. Создайте два инвентаря

1. Создайте внутри `Canvas` объект `Backpack`.
2. Добавьте на него `UniversalInventory`.
3. Укажите:

    | Поле | Значение |
    |---|---|
    | `Item Behavior` | `Unique` для самого простого старта - каждый предмет в отдельном слоте|
    | `Slot Management` | `Fixed` - фиксированное количество слотов|
    | `Initial Slot Count` | например `10`. При запуске они создасться в `Slot Container`. Если вы сами их там уже создали, можете их закешировать нажав на кнопку |
    | `Slot Prefab` | `Prefabs/Slot.prefab` |
    | `Slot Container` | родитель для слотов (желательно с `GridLayout` или другим компонентом управляющим расположением дочерних объектов) |

4. Скопируйте объект и сделайте второй инвентарь, например `Chest`.

---

## Шаг 3. Опишите тип предмета
Допустим у вас есть тип предметов
```csharp
[CreateAssetMenu(menuName = "Game/Item")]
public class ItemSO : ScriptableObject
{
    [field: SerializeField] public string ItemName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
}
```
Чтобы такой тип можно было отображать в слотах для него нужно сделать адаптер, который реализует `IItemAdapter`, например:

```csharp
using UniversalDragAndDrop.Core;
using UnityEngine;

public class ItemSOAdapter : IItemAdapter
{
    // ссылка на данные для вашего типа
    public readonly ItemSO Data;

    // конструктор
    public ItemSOAdapter(ItemSO data) => Data = data;

    // обязательные поля из интерфейса
    public string ItemId => Data.GetInstanceID().ToString();
    public Sprite Icon => Data.Icon;
    public string DisplayName => Data.ItemName;
}
```

Значения обязательных полей:

- `ItemId` - Нужен чтобы различать можно ли объеденить предметы в один слот для режимов `Stackable` и `SeparableStacks`.
- `Icon` - Нужно чтобы отображать карткинку предмета в слоте
- `DisplayName` - Используется в некоторых дополнительных системах. Можно оставить равным пустой строке

---

## Шаг 4. Добавьте простой binding

```csharp
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.DataBinding;
using System.Collections.Generic;
using UnityEngine;

public class SimpleBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    // Ваш список данных
    [SerializeField] private List<ItemSO> _items;

    // обязательные функции для переопределения
    protected override IReadOnlyList<ItemSO> GetItems() => _items;
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);
    protected override void AddToData(ItemSOAdapter adapter) => _items.Add(adapter.Data);
    protected override void RemoveFromData(ItemSOAdapter adapter) => _items.Remove(adapter.Data);
}
```

Значения обязательных методов:

- `GetItems` - Используется для первичного рендера данных
- `CreateAdapter` - Нужен для создания адаптера и передачи ему необходимых параметров о ваших данных
- `AddToData` - вызывается при переносе предмета **в этот** инвентарь
- `RemoveFromData` - вызывается при выносе предмета **из этого** инвентаря


Повесьте такой binding на оба инвентаря или другие объекты в сцене.
Укажите ссыли на соответсвующие инвентари.
У каждого будет свой список `_items`.

!!! info Данные
    В ваших проектах вы скорее всего будете вместо простого списка `_items` указывать взаимодействие с вашими скриптами в которых хранятся ваши данные. Подробнее как подобное реализовано можно посмотреть в примерах.

---

## Шаг 5. Заполните стартовые данные

1. Создайте несколько `ItemSO`.
2. Добавьте их в списки `_items` у ваших `SimpleBinding`.
3. Запустите сцену.

Если всё настроено правильно:

- оба инвентаря покажут предметы
- предмет можно перетащить из одного инвентаря в другой
- списки данных обновится автоматически при переносе

---

## Что происходит под капотом

```mermaid
flowchart LR
    A["Игрок тащит предмет"] --> B["<b>UniversalInventory</b> обрабатывает перенос"]
    B --> C["В <b>DataBinding</b> вызываются методы"]
    C --> D["Ваш List<ItemSO> обновляется"]
```

## Частые ошибки в первом проекте

- `ItemId` не соответствует вашей логике stacking, и предметы начинают merge-иться или не merge-иться неожиданно
- на сцене нет `DragAndDropManager` или их несколько
- на сцене нет `EventSystem`
- binding привязан не к тому `UniversalInventory`
- у `InventoryDataBinding` не назначена ссылка на inventory
- данные меняются вне pipeline, но `ReloadUI()` не вызывается. Добавьте в вашем DataBinding вызов ReloadUI по событию изменения данных

## Что дальше

- [Примеры](../examples/index.md) — если хотите выбрать из всех 5 демо
- [Привязка данных](../architecture/data-binding.md) — если нужно понять lifecycle и точки расширения
- [Troubleshooting](../reference/troubleshooting.md) — если базовая сцена не завелась с первого раза
