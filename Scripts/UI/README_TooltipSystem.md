# Tooltip System - Система кастомных tooltip карточек

Обновленная система tooltip с возможностью использовать **разные префабы** tooltip для разных типов предметов (аналогично IDragVisual).

## 🎯 Архитектура

```
TooltipManager (менеджер)
├── Default Tooltip Prefab (ITooltipView)
├── Custom Tooltip Prefabs (List<ITooltipView>)
└── Pooling System

ITooltipView (интерфейс)
├── DefaultTooltipView (базовая реализация)
└── CustomTooltipView (ваши кастомные варианты)

ITooltipProvider (опционально на предметах)
├── GetTooltipDescription()
└── GetCustomTooltipPrefab() → указать свой префаб
```

## 📋 Компоненты

### 1. ITooltipView - интерфейс визуализации

```csharp
public interface ITooltipView
{
    void Show(IInventoryItem item);
    void Hide();
    void UpdatePosition(Vector2 position);
    void UpdateContent(IInventoryItem item);
    bool IsVisible { get; }
    RectTransform RectTransform { get; }
    Vector2 GetSize();
}
```

### 2. DefaultTooltipView - базовая карточка

Простая карточка с:
- Названием предмета
- Описанием
- Иконкой
- Fade-in/out анимацией
- Настройками формата (NameOnly, NameAndType, Full)

### 3. ITooltipProvider - интерфейс для предметов

```csharp
public interface ITooltipProvider
{
    string GetTooltipDescription();
    GameObject GetCustomTooltipPrefab(); // null = использовать дефолтный
}
```

### 4. TooltipManager - менеджер с пулингом

- Управляет показом/скрытием tooltip
- Поддерживает разные префабы для разных типов
- Пулинг tooltip для производительности
- Позиционирование (курсор, углы слота)

## ⚙️ Способы использования

### Способ 1: Дефолтный tooltip для всех

```
TooltipManager
└── Default Tooltip Prefab: DefaultTooltipView.prefab
```

Все предметы будут показывать один и тот же tooltip.

---

### Способ 2: Разные tooltip для разных типов

```csharp
TooltipManager
├── Default Tooltip Prefab: DefaultTooltipView.prefab
└── Custom Tooltip Prefabs:
    ├── [0] ItemType: EquipmentItemAdapter → EquipmentTooltip.prefab
    ├── [1] ItemType: ConsumableItemAdapter → ConsumableTooltip.prefab
    └── [2] ItemType: QuestItemAdapter → QuestTooltip.prefab
```

Каждый тип предмета получит свой tooltip!

---

### Способ 3: Предмет указывает свой tooltip (ITooltipProvider)

```csharp
public class LegendaryItemAdapter : ItemSOAdapter, ITooltipProvider
{
    private GameObject _legendaryTooltipPrefab;

    public string GetTooltipDescription()
    {
        return "⚔️ Legendary Item! ⚔️\nThis weapon was forged by ancient gods...";
    }

    public GameObject GetCustomTooltipPrefab()
    {
        // Возвращаем специальный tooltip для легендарных предметов
        return _legendaryTooltipPrefab;
    }
}
```

---

## 🔨 Создание кастомного tooltip

### Шаг 1: Создать класс наследник

```csharp
using DragAndDropSystem.Core;
using DragAndDropSystem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentTooltipView : MonoBehaviour, ITooltipView
{
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _stats;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Image _rarityBorder;

    private RectTransform _rectTransform;

    public bool IsVisible => gameObject.activeSelf;
    public RectTransform RectTransform => _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Show(IInventoryItem item)
    {
        // Заполняем UI
        _itemName.text = item.DisplayName;
        _itemIcon.sprite = item.Icon;

        // Извлекаем данные экипировки
        if (item is EquipmentItemAdapter equipment)
        {
            _stats.text = $"+{equipment.Attack} ATK\n+{equipment.Defense} DEF";
            _rarityBorder.color = GetRarityColor(equipment.Rarity);
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdatePosition(Vector2 position)
    {
        _rectTransform.position = position;
    }

    public void UpdateContent(IInventoryItem item)
    {
        Show(item); // Просто обновляем содержимое
    }

    public Vector2 GetSize()
    {
        return _rectTransform.rect.size;
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return Color.white;
            case ItemRarity.Rare: return Color.blue;
            case ItemRarity.Legendary: return Color.yellow;
            default: return Color.white;
        }
    }
}
```

### Шаг 2: Создать префаб

```
EquipmentTooltip.prefab
├── Background (Image - рамка редкости)
├── Icon (Image)
├── ItemName (TextMeshPro)
├── Stats (TextMeshPro - характеристики)
└── EquipmentTooltipView (скрипт)
```

### Шаг 3: Настроить TooltipManager

```
TooltipManager
├── Default Tooltip Prefab: DefaultTooltipView.prefab
└── Custom Tooltip Prefabs:
    └── [0] ItemType: EquipmentItemAdapter
        └── Tooltip Prefab: EquipmentTooltip.prefab
```

Готово! Теперь экипировка будет показывать кастомный tooltip!

---

## 🎨 Примеры кастомных tooltip

### Пример 1: Tooltip с анимацией появления

```csharp
public class AnimatedTooltipView : DefaultTooltipView
{
    [SerializeField] private float _scaleAnimationDuration = 0.2f;

    public override void Show(IInventoryItem item)
    {
        base.Show(item);

        // Анимация scale
        transform.localScale = Vector3.zero;
        LeanTween.scale(gameObject, Vector3.one, _scaleAnimationDuration)
            .setEaseOutBack();
    }
}
```

### Пример 2: Tooltip с 3D моделью

```csharp
public class Model3DTooltipView : MonoBehaviour, ITooltipView
{
    [SerializeField] private RawImage _modelRenderTexture;
    [SerializeField] private Camera _renderCamera;
    [SerializeField] private Transform _modelSpawnPoint;

    private GameObject _current3DModel;

    public void Show(IInventoryItem item)
    {
        // Уничтожаем предыдущую модель
        if (_current3DModel != null)
            Destroy(_current3DModel);

        // Создаем 3D модель предмета
        if (item is ItemSOWith3DAdapter item3D && item3D.item.WorldPrefab != null)
        {
            _current3DModel = Instantiate(item3D.item.WorldPrefab, _modelSpawnPoint);
            _current3DModel.transform.localPosition = Vector3.zero;

            // Вращаем модель
            LeanTween.rotateAround(_current3DModel, Vector3.up, 360f, 3f)
                .setLoopClamp();
        }

        gameObject.SetActive(true);
    }

    // ... остальные методы ITooltipView
}
```

### Пример 3: Tooltip для квестовых предметов

```csharp
public class QuestItemTooltipView : MonoBehaviour, ITooltipView
{
    [SerializeField] private TextMeshProUGUI _questName;
    [SerializeField] private TextMeshProUGUI _questProgress;
    [SerializeField] private Image _questIcon;

    public void Show(IInventoryItem item)
    {
        if (item is QuestItemAdapter questItem)
        {
            _questName.text = questItem.RelatedQuest.QuestName;
            _questProgress.text = $"Progress: {questItem.RelatedQuest.Progress}%";
            _questIcon.sprite = questItem.RelatedQuest.Icon;

            // Подсветка если квест можно сдать
            if (questItem.RelatedQuest.CanComplete)
            {
                _questName.color = Color.green;
            }
        }

        gameObject.SetActive(true);
    }

    // ... остальные методы
}
```

---

## 🔄 Приоритет выбора tooltip

TooltipManager выбирает tooltip в следующем порядке:

1. **ITooltipProvider.GetCustomTooltipPrefab()** - предмет сам указывает свой tooltip
2. **Custom Tooltip Prefabs** - маппинг по типу предмета
3. **Default Tooltip Prefab** - дефолтный tooltip

```csharp
// Пример приоритета:
LegendaryItem (ITooltipProvider) → GetCustomTooltipPrefab() = LegendaryTooltip ✅ (используется этот)
EquipmentItem → CustomTooltipPrefabs[EquipmentItem] = EquipmentTooltip ❌
AnyItem → DefaultTooltipPrefab = DefaultTooltip ❌
```

---

## 💡 Расширенные возможности

### Наследование от DefaultTooltipView

Можно наследоваться от базового и переопределять методы:

```csharp
public class EnhancedTooltipView : DefaultTooltipView
{
    protected override string GetItemDescription(IInventoryItem item)
    {
        // Добавляем дополнительную информацию
        string baseDesc = base.GetItemDescription(item);

        if (item is IDescribableItem describable)
        {
            return $"{baseDesc}\n\nRarity: {describable.Rarity}";
        }

        return baseDesc;
    }
}
```

### Пулинг автоматический

TooltipManager автоматически пулит tooltip:
- Размер пула настраивается (`Pool Size`)
- Работает для всех типов tooltip
- Производительность: не создает новые объекты каждый раз

---

## 📊 Сравнение со старой системой

| Аспект | Старая система | Новая система (ITooltipView) |
|--------|----------------|------------------------------|
| **Карточки** | Одна на всех | Разные для разных предметов |
| **Кастомизация** | Только настройки | Полный контроль через код |
| **Анимации** | Fade-in/out | Любые анимации |
| **3D модели** | ❌ Нет | ✅ Можно |
| **Производительность** | Хорошая | Отлично (пулинг) |
| **Гибкость** | Низкая | Максимальная |

---

## ✅ Best Practices

1. **Используйте DefaultTooltipView** для большинства случаев
2. **Создавайте кастомные** только для специфических типов (экипировка, квесты)
3. **Включайте пулинг** если tooltip часто показываются
4. **Наследуйтесь от DefaultTooltipView** вместо реализации ITooltipView с нуля
5. **ITooltipProvider** используйте для уникальных предметов (legendary, mythic)

---

**Автор**: DragAndDropSystem
**Версия**: 2.0 (ITooltipView refactoring)
