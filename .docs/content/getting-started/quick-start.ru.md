# Быстрый старт

Это руководство создаёт два обычных инвентаря, между которыми можно перетаскивать предметы. 

Подобное уже реализовано в первом примере и финальный результат можно посмотреть там.

Даже в базовом сценарии вам обычно нужно написать немного интеграционного кода под свои данные. В этом руководстве это будут:

- `ItemSO` как ваши данные предмета
- `ItemSOAdapter` как представление для системы инвентаря
- `SimpleBinding` как мост между UI и вашим списком данных

## Шаг 1. Подготовьте сцену

1. Создайте `Canvas` на котором будут располагаться инвентрари, если его ещё нет.
2. Добавьте на сцену `DragAndDropManager`. Можно перетащить на сцену префаб из `Prefabs/DragCanvas.prefab`.
> На сцене нужен один `DragAndDropManager`. Он управляет всеми операциями переноса и контролирует объект переносимого объекта.
3. Убедитесь, что на сцене есть `EventSystem`.
   Если проект использует legacy input, на `EventSystem` должен быть `StandaloneInputModule`. Также ассет поддерживает New Input System. В `DragAndDropManager` по умолчанию указан минимальный рабочий конфиг действий. Можете его поправить под себя или создать свою копию, не забыв заменить её в сцене.

---

## Шаг 2. Создайте два инвентаря

1. Создайте внутри `Canvas` объект `Backpack`.
2. Добавьте на него `UniversalInventory`.
3. Укажите:

    | Поле | Значение |
    |---|---|
    | `Slot Container` | родитель для слотов (желательно с `GridLayout` или другим компонентом управляющим расположением дочерних объектов) |
    | `Slot Prefab` | Префаб который вы хотите использовать для слотов `Prefabs/Slot.prefab` |
    | `Initial Slot Count` | например `10`. При запуске они создасться в `Slot Container`. Если вы сами их там уже создали, можете их закешировать нажав на кнопку |
    | `Inventory Strategy` | `UniqueItemStrategy` для самого простого старта, чтобы каждый предмет был в отдельном слоте |
    | `Slot Management` | `FixedSlotManagementSettings` для фиксированного количества слотов |

4. Скопируйте объект и сделайте второй инвентарь, например `Chest`.

!!! info Инициализация
    Инвентарь на старте пробует найти уже созданные слоты, дочерные к `Slot Container` и закэшировать их для управления. Если их оказалось не достаточно создаст слоты `Slot Prefab` в указанном до `Initial Slot Count` количестве. Вы также можете создать все сдоты вручную в режиме редактирования и закэшировать их кнопкой `Cache Slots` чтобы этой операции не происходило во время игры
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
using UDND.Core;
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

- `ItemId` - Нужен чтобы различать можно ли объеденить предметы в один слот для стратегий `Stackable` и `SeparableStacks`.
- `Icon` - Нужно чтобы отображать карткинку предмета в слоте
- `DisplayName` - Используется в основном в логах и некоторых дополнительных системах вроде примера подсказок. Можно оставить равным пустой строке

---

## Шаг 4. Добавьте простой binding

Чтобы отобразить ваши данные в слотах нужно системе показать эти данные и дать возможности взаимодействия с ними. Обычно это будут наборы данных хранящиеся где то в ваших скриптах игрока, персонажей и объектов. Тут в примере будет просто список в самом этом компоненте

```csharp
using UDND.Core;
using UDND.DataBinding;
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
`ListInventoryDataBinding` это специальный шаблон DataBinding для работы с данными хранящемися в виде списка. У него при наследовании указывается тип вашего предмета и адаптера для него.

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
- на сцене нет `DragAndDropManager`
- на сцене нет `EventSystem`
- проект использует legacy input, но на `EventSystem` нет `StandaloneInputModule`
- binding привязан не к тому `UniversalInventory`
- у `InventoryDataBinding` не назначена ссылка на inventory
- данные меняются вне pipeline, но `ReloadUI()` не вызывается. Добавьте в вашем DataBinding вызов ReloadUI по событию изменения данных

См. также:

- [Примеры](../examples/index.md) — если хотите выбрать из всех 6 демо
- [Привязка данных](../architecture/data-binding.md) — если нужно понять lifecycle и точки расширения
- [Стратегии размещения](../architecture/strategies.md) — если хотите добавить свою стратегию или свой режим slot management
- [Troubleshooting](../reference/troubleshooting.md) — если базовая сцена не завелась с первого раза
