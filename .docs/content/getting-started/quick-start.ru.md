# Быстрый старт

Это руководство создаёт два обычных инвентаря, между которыми можно перетаскивать предметы. 

Подобное уже реализовано в первом примере и финальный результат можно посмотреть там.

Даже в базовом сценарии вам обычно нужно написать немного интеграционного кода под свои данные. В этом руководстве это будут:

- `ItemSO` как ваши данные предмета
- `ItemSOAdapter` как представление для системы инвентаря
- `SimpleBinding` как мост между UI и вашим списком данных

## Шаг 1. Подготовьте сцену

Инвентари — это обычный uGUI, поэтому они живут на UI-канвасе.

1. **Создайте `Canvas` для инвентарей**, если его ещё нет.

    В окне `Hierarchy` кликните правой кнопкой по пустому месту и выберите
    `UI → Canvas`.

    Вместе с `Canvas` Unity создаст объект `EventSystem`.
    Для быстрого старта оставьте у `Canvas` значение `Render Mode` = `Screen Space - Overlay`.

2. **Добавьте на сцену `DragAndDropManager`.**

    Найдите в `Project` префаб `Prefabs/DragCanvas.prefab` и перетащите его в `Hierarchy`
    на пустое место — он должен стать корневым объектом сцены, рядом с вашим `Canvas`.

    У `DragCanvas` свой `Canvas` со значением `Sorting Order = 100` — за счёт этого
    перетаскиваемый предмет рисуется поверх остального интерфейса.
    Кроме `DragAndDropManager` на префабе лежат `InputEventRouter` и `DragVisualPresenter`.
    На сцене нужен ровно один такой объект: он управляет всеми операциями переноса и
    отвечает за визуал предмета «в руке».

3. **Проверьте `EventSystem`.**

    Если в `Hierarchy` его нет, кликните правой кнопкой по пустому месту и выберите
    `UI → Event System`.

    Выделите `EventSystem` и посмотрите в `Inspector`, какой модуль ввода на нём стоит:

    - проект на legacy input — нужен `StandaloneInputModule`;
    - проект на New Input System — нужен `InputSystemUIInputModule`.

    Ассет поддерживает оба варианта. В `DragAndDropManager` по умолчанию указан минимальный
    рабочий конфиг действий: можете поправить его под себя или создать свою копию, не забыв
    заменить её в сцене.

---

## Шаг 2. Создайте два инвентаря

1. В `Hierarchy` кликните правой кнопкой по объекту `Canvas` и выберите `Create Empty`.
   Назовите новый объект `Backpack`.

    Так как родитель — `Canvas`, Unity сама даст объекту `RectTransform`.
    Если сразу нужен фон, вместо `Create Empty` выберите `UI → Panel`.

2. Задайте `Backpack` размер и положение.

    У UI-объекта вместо `Transform` стоит `RectTransform`, и после `Create Empty` его
    `Width` и `Height` равны нулю. Размер задаётся здесь, а не масштабом:

    - выделите `Backpack` и в `Inspector` в компоненте `RectTransform` укажите
      `Width` = `540`, `Height` = `240`;
    - положение удобно задать кнопкой пресетов якорей — это квадрат с крестом в левом
      верхнем углу `RectTransform`. Нажмите его и для быстрого старта выберите
      `middle center`, затем сдвиньте объект полями `Pos X` и `Pos Y`.

    Если вы делали `UI → Panel`, размер уже растянут на весь экран — тогда просто
    поставьте нужные `Width` и `Height` тем же способом.

3. Кликните правой кнопкой по `Backpack`, выберите `Create Empty` и назовите объект
   `SlotContainer`.

    Растяните его на весь `Backpack`: нажмите кнопку пресетов якорей и с зажатыми
    `Alt` и `Shift` выберите правый нижний вариант `stretch / stretch`.

    Затем нажмите `Add Component → Layout → Grid Layout Group` и заполните его поля:

    | Поле `Grid Layout Group` | Значение |
    |---|---|
    | `Cell Size` | `100` × `100` — размер слота из `Prefabs/Slot.prefab` |
    | `Spacing` | `5` по X и Y |
    | `Constraint` | `Fixed Column Count` |
    | `Constraint Count` | `5` — тогда 10 слотов лягут сеткой 5 × 2 |

    Именно этот компонент раскладывает слоты сеткой.

4. Добавьте на `Backpack` компонент `UniversalInventory` и заполните поля.

    В инспекторе они разложены по сворачиваемым группам:

    **Группа `Slot Setup`:**

    | Поле | Значение |
    |---|---|
    | `Slot Container` | объект `SlotContainer` из шага 3 — родитель, внутри которого создаются слоты |
    | `Slot Prefab` | префаб слота, для старта подойдёт `Prefabs/Slot.prefab` |
    | `Initial Slot Count` | например `10`. При запуске столько слотов будет создано в `Slot Container`. Если вы создали их сами, их можно закэшировать кнопкой `Cache Slots` |

    **Группа `Strategy`** — здесь два поля без подписи, каждое выбирается выпадающим списком:

    | Что выбрать | Значение |
    |---|---|
    | верхний список (стратегия инвентаря) | `UniqueItemStrategy` для самого простого старта, чтобы каждый предмет лежал в отдельном слоте |
    | нижний список (управление слотами) | `FixedSlotManagementSettings` для фиксированного количества слотов |

    Остальные группы (`Rules`, `Drop Policy`, `Placement`) для быстрого старта можно не трогать.

5. Скопируйте `Backpack` и сделайте второй инвентарь, например `Chest`.
   Разведите их по экрану полем `Pos X`, чтобы оба были видны одновременно.

!!! info Инициализация
    Инвентарь на старте пробует найти уже созданные слоты, дочерние к `Slot Container`, и
    закэшировать их для управления. Если их оказалось недостаточно, он создаст слоты из
    `Slot Prefab` до количества `Initial Slot Count`. Вы также можете создать все слоты вручную
    в режиме редактирования и закэшировать их кнопкой `Cache Slots`, чтобы этой операции не
    происходило во время игры.
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

- `ItemId` - Нужен чтобы различать, можно ли объединить предметы в один слот, для стратегий `Stackable` и `SeparableStacks`.
- `Icon` - Нужно чтобы отображать картинку предмета в слоте
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
`ListInventoryDataBinding` это специальный шаблон DataBinding для работы с данными, хранящимися в виде списка. У него при наследовании указывается тип вашего предмета и адаптера для него.

Значения обязательных методов:

- `GetItems` - Используется для первичного рендера данных
- `CreateAdapter` - Нужен для создания адаптера и передачи ему необходимых параметров о ваших данных
- `AddToData` - вызывается при переносе предмета **в этот** инвентарь
- `RemoveFromData` - вызывается при выносе предмета **из этого** инвентаря


Повесьте такой binding на оба инвентаря или другие объекты в сцене.
Укажите ссылки на соответствующие инвентари.
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
- списки данных обновятся автоматически при переносе

---

## Что происходит под капотом

```mermaid
flowchart LR
    A["Игрок тащит предмет"] --> B["<b>UniversalInventory</b> обрабатывает перенос"]
    B --> C["В <b>DataBinding</b> вызываются методы"]
    C --> D["Ваш List<ItemSO> обновляется"]
```

## Частые ошибки в первом проекте

- инвентарь создан как 2D- или обычный объект сцены, а не как UI внутри `Canvas`: в инспекторе у него `Transform` вместо `RectTransform`
- у `Backpack` или `SlotContainer` в `RectTransform` остались нулевые `Width` и `Height`, поэтому инвентарь не видно или он стоит не там, где ожидалось
- размер пытались задать через `Scale` вместо `Width` и `Height`
- `DragCanvas` положили внутрь своего `Canvas`, и перетаскиваемый предмет пропадает под интерфейсом
- `Slot Container` указывает на сам инвентарь, а не на дочерний объект с `Grid Layout Group`
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
