# Свободное размещение слотов

`FreeFormSlotLayout` показывает, как сделать инвентарь, где предмет появляется примерно в точке drop, а не в очередной ячейке сетки.

Это пример UI-раскладки поверх обычного переноса. Сам перенос предметов не меняется: инвентарь всё так же решает, можно ли принять предмет, создаёт слот и кладёт туда стек. `FreeFormSlotLayout` только выбирает позицию созданного слота на экране.

## Что получается

- игрок бросает предмет на область инвентаря
- инвентарь создаёт динамический слот
- слот ставится рядом с точкой drop
- если место занято, слот сдвигается в ближайшее свободное место
- при загрузке или `ReloadUI` все слоты раскладываются без наложений

## Как это устроено

Координаты drop не передаются внутрь логики переноса. Это остаётся задачей UI.

Компонент использует два события:

| Событие | Для чего |
|---|---|
| `UDNDEvents.OnDropAttempting` | Запомнить позицию мыши перед обработкой drop |
| `UniversalInventory.OnSlotCreated` | Поставить новый слот в запомненную позицию |

Если слот создаётся не из drop-сценария, компонент раскладывает его стандартным способом.

## Компоненты

| Компонент | Назначение |
|---|---|
| `FreeFormSlotLayout` | Позиционирует динамически созданные слоты и убирает наложения |
| `InventoryDropArea` | Принимает drop на область инвентаря |
| `UniversalInventory` | Работает в режиме `Dynamic` и создаёт слоты по необходимости |

## Настройка

### 1. Настройте `UniversalInventory`

Установите:

- **Slot Management** = `Dynamic`
- **Max Free Slots** = `0`, если слоты должны появляться только при drop
- **Max Dynamic Slots** = максимальное количество предметов в этом инвентаре

### 2. Уберите `LayoutGroup`

На контейнере слотов (`_slotContainer`) не должно быть `HorizontalLayoutGroup`, `VerticalLayoutGroup` или `GridLayoutGroup`.

Unity `LayoutGroup` перезаписывает позиции детей, поэтому свободная раскладка с ним конфликтует.

### 3. Добавьте `FreeFormSlotLayout`

Добавьте компонент на тот же GameObject, где находится `UniversalInventory`.

Настройки:

| Поле | Что делает |
|---|---|
| **UI Camera** | Камера для UI. Оставьте пустым для Screen Space - Overlay |
| **Slot Spacing** | Минимальный отступ между слотами |
| **Bounds Override** | RectTransform, внутри которого должны оставаться слоты. Если не задан, используется контейнер слотов |

### 4. Добавьте `InventoryDropArea`

Стандартного `InventoryDropArea` достаточно. Писать отдельный drop target для этого сценария не нужно.

## Сохранение позиций

`FreeFormSlotLayout` умеет переводить позиции в нормализованные координаты `0..1`. Их удобно хранить в ваших данных.

```csharp
// Save: local position -> normalized 0..1
Vector2 normalized = layout.GetNormalizedPosition(slot);
myModel.SavePosition(slot.Index, normalized);

// Load: normalized 0..1 -> local position
Vector2 local = layout.NormalizedToLocal(savedNormalized);
layout.SetSlotPosition(slot, local);
```

Нормализованные координаты переживают изменение размера контейнера: позиция остаётся примерно в том же месте относительно области инвентаря.

## Как убираются наложения

Если точка drop занята, компонент проверяет соседние позиции вокруг неё и выбирает ближайшую свободную позицию внутри допустимых границ.

Это поведение подходит для примера и небольших инвентарей. Если вам нужен другой алгоритм, используйте те же события и замените расчёт позиции.

## Как сделать свою раскладку

Обычно достаточно такого паттерна:

1. Создать компонент рядом с `UniversalInventory`.
2. Подписаться на `UniversalInventory.OnSlotCreated`.
3. При необходимости подписаться на `UDNDEvents.OnDropAttempting`, чтобы запомнить позицию drop.
4. В `ArrangeAllSlots()` пересчитать позиции всех слотов после загрузки или `ReloadUI`.

```csharp
[RequireComponent(typeof(UniversalInventory))]
public class MyCustomLayout : MonoBehaviour
{
    private UniversalInventory _inventory;

    void Awake() => _inventory = GetComponent<UniversalInventory>();

    void OnEnable()
    {
        _inventory.OnSlotCreated += HandleSlotCreated;
    }

    void OnDisable()
    {
        _inventory.OnSlotCreated -= HandleSlotCreated;
    }

    void HandleSlotCreated(BaseSlot slot)
    {
        var rectTransform = slot.Transform as RectTransform;
        rectTransform.anchoredPosition = CalculatePosition(slot);
    }

    public void ArrangeAllSlots()
    {
        foreach (var slot in _inventory.Slots)
        {
            var rectTransform = slot.Transform as RectTransform;
            rectTransform.anchoredPosition = CalculatePosition(slot);
        }
    }

    Vector2 CalculatePosition(BaseSlot slot)
    {
        return Vector2.zero;
    }
}
```

## Идеи для других раскладок

| Вариант | Идея |
|---|---|
| Circular | Расставить слоты по окружности |
| Snap Grid | Бросать свободно, но привязывать позицию к ближайшей ячейке |
| Physics | Дать слотам физическое поведение |
| Radial Menu | Раскладывать слоты веером от центра |

## Справочник

| Класс | Роль |
|---|---|
| `FreeFormSlotLayout` | Пример компонента для свободной UI-раскладки слотов |
| `UniversalInventory.OnSlotCreated` | Главная точка для позиционирования нового слота |
| `UniversalInventory.SlotContainer` | Контейнер, относительно которого считаются координаты |
| `InventoryDropArea` | Стандартная область drop для инвентаря |
| `DynamicSlotManagementSettings` | Режим, который создаёт слоты по необходимости |
