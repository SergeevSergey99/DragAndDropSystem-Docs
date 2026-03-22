# Контекстное меню

Контекстное меню показывает список действий при правом клике (или долгом нажатии) на слот инвентаря. Пункты меню настраиваются через ScriptableObject-пресеты и могут быть разными для каждого инвентаря.

---

## Как работает

```mermaid
flowchart LR
    A["ПКМ по слоту"] --> B["Собрать\nдоступные\nпункты"]
    B --> C["Показать\nменю"]
    C --> D["Игрок\nвыбирает\nпункт"]
    D --> E["Выполнить\nдействие"]

```

Система автоматически фильтрует пункты через `CanShow()` --- в меню попадают только те действия, которые применимы к текущему слоту. Пункты сортируются по полю `Order`.

---

## Настройка

1. **Добавьте `ContextMenuManager`** на сцену (синглтон). Назначьте ему префаб вьюшки меню.
2. **Создайте пресет** через меню: *Create > DragAndDrop > ContextMenu > Preset*. Добавьте в него нужные пункты.
3. **Добавьте `ContextMenuBinder`** на GameObject инвентаря. Назначьте пресет для непустых слотов и (опционально) отдельный пресет для пустых слотов.
4. **Привяжите действие** `ShowContextMenuAction` к правой кнопке мыши (или долгому нажатию) в `InteractionBindingsProfile`.

---

## Свой пункт меню

Создайте ScriptableObject, унаследовавшись от `ContextMenuEntryDefinitionSO`:

```csharp
[CreateAssetMenu(menuName = "Game/Context Menu/Use Item")]
public class UseItemEntry : ContextMenuEntryDefinitionSO
{
    public override bool CanShow(ContextMenuContext ctx)
    {
        // Показывать только если есть предмет
        return ctx.Item != null;
    }

    public override void Execute(ContextMenuContext ctx)
    {
        // Ваша логика использования предмета
        Debug.Log($"Используем: {ctx.Item.DisplayName}");
    }
}
```

Затем создайте ассет через *Create* и добавьте его в пресет меню.

---

## Встроенные пункты

| Пункт | Описание |
|-------|----------|
| **Debug** | Выводит информацию о предмете в консоль (имя, количество) |
| **Sort** | Сортирует предметы в инвентаре (по имени, категории, редкости) |

---

## Контекст

При вызове `Execute()` и `CanShow()` вы получаете структуру `ContextMenuContext` с полной информацией:

| Поле | Тип | Описание |
|------|-----|----------|
| `Slot` | `ISlot` | Слот, на котором вызвали меню |
| `Item` | `IInventoryItem` | Предмет в слоте (null если пустой) |
| `ItemCount` | `int` | Количество предметов в стаке |
| `Inventory` | `UniversalInventory` | Инвентарь, которому принадлежит слот |
| `ScreenPosition` | `Vector2` | Экранная позиция клика |
| `InputSource` | `FocusSource` | Источник ввода (мышь / геймпад) |

Закрытие меню выполняется автоматически через `ContextMenuManager.Instance.Hide()`.

---

## Справочник классов

| Класс | Роль |
|-------|------|
| `ContextMenuManager` | Синглтон-менеджер: показывает/скрывает меню |
| `ContextMenuPreset` | ScriptableObject-пресет со списком пунктов |
| `ContextMenuBinder` | Привязывает пресеты к инвентарю |
| `ContextMenuEntryDefinitionSO` | Базовый SO для пункта меню (наследуйте) |
| `IContextMenuEntry` | Интерфейс пункта меню |
| `ContextMenuContext` | Структура с данными о контексте вызова |
| `ContextMenuViewBase` | Базовый класс вьюшки меню (реализуйте свою) |
| `UniversalContextMenuView` | Готовая UGUI-реализация вьюшки |
| `ShowContextMenuAction` | Действие для привязки к PointerBinding |
