# World 3D Integration - Выбрасывание предметов в мир

Этот модуль добавляет возможность выбрасывания предметов из UI инвентаря в 3D пространство.

## Компоненты

### 1. IWorld3DAdapter
Интерфейс для связи `IInventoryItem` с 3D префабами.

```csharp
public interface IWorld3DAdapter
{
    GameObject WorldPrefab { get; }
    bool HasWorldRepresentation { get; }
}
```

### 2. WorldDropZone
UI компонент - зона для выбрасывания предметов в 3D мир.

**Особенности:**
- Не привязан к инвентарю (не является `IInventory`)
- Автоматически спавнит 3D префаб в указанной точке
- Удаляет предмет из исходного инвентаря
- Поддерживает настройку силы броска и случайного разброса
- Визуальная подсветка при наведении (зеленый = можно выбросить, красный = нельзя)

### 3. WorldItem
Компонент для предметов в 3D мире (выброшенных).

**Особенности:**
- Хранит ссылку на оригинальный `IInventoryItem`
- Может быть подобран обратно через `TryPickup()`
- Опциональная подсветка при приближении игрока
- Отображает радиус подбора в редакторе (Gizmos)

### 4. ItemExampleWith3DSO
Пример ScriptableObject предмета с поддержкой 3D (данные). Наследуется от `ItemExampleSO` и добавляет поле `WorldPrefab`.

### 5. ItemSOWith3DAdapter
Адаптер для работы ItemExampleWith3DSO с системой инвентаря + 3D. Реализует `IStackableItem` и `IWorld3DAdapter`.

### 6. ItemsSOInventoryDataBinding (обновлён)
Универсальный DataBinding, который автоматически работает с обоими типами:
- `ItemExampleSO` → создаёт `ItemSOAdapter`
- `ItemExampleWith3DSO` → создаёт `ItemSOWith3DAdapter`

## Архитектура (важно!)

Система использует **паттерн Адаптер**:
- `ItemExampleSO` / `ItemExampleWith3DSO` - это **данные** (ScriptableObject)
- `ItemSOAdapter` / `ItemSOWith3DAdapter` - **адаптеры**, которые реализуют интерфейсы для работы с системой
- `ItemsSOInventoryDataBinding` - **универсальный биндинг**, автоматически выбирает нужный адаптер

**Почему через адаптеры?** Это позволяет:
- Хранить и сериализовать чистые SO (данные)
- Использовать любые существующие SO без изменений
- Легко расширять функциональность (добавили IWorld3DAdapter, не меняя базовый ItemExampleSO)

## Как использовать

### Шаг 1: Создайте предмет с 3D представлением

```
ПКМ в Project → Create → DragAndDropSystem/Examples/ItemExampleWith3DSO
```

Назначьте поле `World Prefab` - префаб вашего 3D объекта.

### Шаг 2: Работа через DataBinding (автоматически)

`ItemsSOInventoryDataBinding` автоматически создаст нужный адаптер:

```csharp
// В инспекторе просто добавьте ItemExampleWith3DSO в список items
// DataBinding сам создаст ItemSOWith3DAdapter при синхронизации
```

### Альтернатива: Создание адаптера вручную

Если нужно добавить предмет в код напрямую:

```csharp
// Загрузите ваш SO
ItemExampleWith3DSO itemSO = /* ваш SO из Resources/AssetDatabase */;

// Создайте адаптер
var itemAdapter = new ItemSOWith3DAdapter(itemSO);

// Добавьте в инвентарь
inventory.TryAddItem(itemAdapter, 1);
```

### Шаг 3: Настройте 3D префаб

1. Создайте 3D модель вашего предмета
2. Добавьте компонент `WorldItem` (опционально - будет добавлен автоматически)
3. Настройте Rigidbody если нужна физика
4. Назначьте префаб в поле `World Prefab` вашего SO

### Шаг 4: Добавьте WorldDropZone в UI

1. Создайте UI элемент (например Image) в вашем Canvas
2. Добавьте компонент `WorldDropZone`
3. Настройте параметры:
   - **Spawn Point** - точка создания предметов (Transform в мире)
   - **Randomize Position** - случайный разброс
   - **Apply Force** - сила выбрасывания
   - **Area Highlight** - UI Image для подсветки зоны

### Шаг 5: Перетащите предмет в зону

Просто перетащите предмет из инвентаря в `WorldDropZone` - он автоматически:
- Заспавнится в 3D мире
- Получит компонент `WorldItem`
- Удалится из исходного инвентаря
- Если есть Rigidbody - получит начальную силу

## Пример настройки в сцене

```
Canvas
└── InventoryUI
    └── DropZone (Image + WorldDropZone)
        Settings:
        - Spawn Point: WorldSpawnPoint (пустой Transform в мире)
        - Randomize Position: true
        - Random Radius: 0.5
        - Apply Force: true
        - Throw Force: 5
```

## Расширение функциональности

### Автоматический подбор предметов

```csharp
// В вашем PlayerController
void Update()
{
    if (Input.GetKeyDown(KeyCode.E))
    {
        // Найти ближайший WorldItem
        var worldItems = Physics.OverlapSphere(transform.position, 2f)
            .Select(c => c.GetComponent<WorldItem>())
            .Where(wi => wi != null && wi.CanBePickedUp);

        foreach (var item in worldItems)
        {
            if (item.TryPickup(_playerInventory))
            {
                Debug.Log($"Picked up {item.ItemData.DisplayName}");
                break;
            }
        }
    }
}
```

### Кастомная логика спавна

Вы можете переопределить `WorldDropZone.SpawnItemInWorld()` для своей логики:
- Особые эффекты при спавне
- Звуки
- Партиклы
- Проверка валидности позиции спавна

## Интеграция с DragAndDropManager

`WorldDropZone` полностью интегрирован в существующую систему:
- Реализует `IDropTarget`
- Работает через стек целей (Push/Pop)
- Поддерживает визуальную подсветку
- Совместим со всеми правилами (Rules)

## Ограничения

- Предмет должен реализовать `IWorld3DAdapter` для выбрасывания
- `WorldDropZone` не хранит предметы (это не инвентарь)
- Для обратного подбора нужна дополнительная логика (см. пример выше)

## Что дальше?

Следующие возможные расширения:
1. **Draggable3DItem** - перетаскивание из 3D в UI
2. **World3DInventory** - полноценный 3D инвентарь (сундуки, столы)
3. **Physics Raycast** в DragAndDropManager для drag из 3D
