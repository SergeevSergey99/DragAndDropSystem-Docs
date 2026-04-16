# Slot Hover System - Система наведения на слоты

**Last Updated**: 2026-02-28

## Совместимость с transfer pipeline

Hover-система остается опциональной и независимой от `DropPolicy` / `TransferPlanner` / `TransferPlanExecutor`. Интеграция с drag&drop API без изменений.


Опциональная система для отслеживания наведения курсора на слоты и отображения tooltip с информацией о предметах.

## 📋 Описание

Система предоставляет **три способа** подписки на события наведения курсора:

1. **Статические события** - для глобальных систем (TooltipManager, SoundManager, Analytics)
2. **UnityEvents** - для настройки в Inspector (звуки, анимации, эффекты)
3. **Виртуальные методы** - для переопределения в наследниках (кастомная логика)

## 🏗️ Архитектура

```
SlotHoverEventListener (опциональный компонент на слоте)
├── static event OnAnySlotHoverEnter/Exit  → TooltipManager подписывается
├── UnityEvent onSlotHoverEnter/Exit       → Настраивается в Inspector
└── virtual OnSlotHoverEnterInternal()     → Переопределяется в наследниках

TooltipManager (опциональный глобальный менеджер)
└── Подписывается на статические события → показывает tooltip
```

**Ключевой принцип**:
- ✅ Система полностью **опциональна** - работает только если добавлены компоненты
- ✅ **Нулевая связанность** - основная система drag&drop не зависит от tooltip
- ✅ **Гибкость** - три способа использования на выбор

## 📁 Компоненты системы

### 1. SlotHoverEventArgs
Аргументы события наведения на слот.

**Свойства**:
```csharp
IInventoryItem Item             // Предмет в слоте (null если пустой)
UniversalSlot Slot              // Слот на который навели
Vector2 ScreenPosition          // Позиция курсора
RectTransform SlotRectTransform // RectTransform слота
bool IsEnter                    // true = вход, false = выход
IInventory Inventory            // Инвентарь слота
int SlotIndex                   // Индекс слота
bool Cancel                     // Флаг отмены события
```

**Методы**:
```csharp
bool HasItem                    // Есть ли предмет в слоте
bool IsEmpty                    // Пустой ли слот
Vector3 GetSlotWorldPosition()  // Позиция слота в мире
Vector2 GetSlotSize()           // Размер слота
```

---

### 2. SlotHoverEventListener
Компонент для отслеживания наведения на слот.

**Настройки**:
- `Only When Not Empty` - события только для непустых слотов
- `Ignore While Dragging` - игнорировать наведение во время перетаскивания
- `Debug Log` - логировать события в консоль

**События**:
```csharp
// Глобальные статические (для глобальных систем)
static event Action<SlotHoverEventArgs> OnAnySlotHoverEnter;
static event Action<SlotHoverEventArgs> OnAnySlotHoverExit;

// Локальные UnityEvents (для Inspector)
UnityEvent<SlotHoverEventArgs> onSlotHoverEnter;
UnityEvent<SlotHoverEventArgs> onSlotHoverExit;
```

**Виртуальные методы** (для наследников):
```csharp
protected virtual void OnSlotHoverEnterInternal(SlotHoverEventArgs args);
protected virtual void OnSlotHoverExitInternal(SlotHoverEventArgs args);
```

---

### 3. TooltipManager
Опциональный менеджер для отображения tooltip.

**Возможности**:
- Fade-in/out анимация
- Задержка перед показом
- Позиционирование (курсор, углы слота)
- Автоматическое ограничение границами экрана
- Настраиваемый формат (только имя, имя+тип, полное описание)

## ⚙️ Способы использования

### Способ 1: Глобальные статические события (TooltipManager)

**Для**: Глобальных систем (tooltip, звуки, аналитика)

#### Настройка:

1. **Добавить SlotHoverEventListener на префаб Slot**:
```
Prefabs/Slot.prefab
├── UniversalSlot
├── SlotInputAdapter (уже есть)
└── SlotHoverEventListener (добавить)
    └── Only When Not Empty: ✓
```

2. **Создать TooltipManager на Canvas**:
```
Canvas
├── ... другие UI элементы ...
├── TooltipPanel (GameObject - изначально выключен)
│   ├── Background (Image)
│   ├── ItemIcon (Image)
│   ├── ItemName (TextMeshProUGUI)
│   └── ItemDescription (TextMeshProUGUI)
└── TooltipManager (компонент)
    └── Tooltip Panel: → TooltipPanel
    └── Item Name Text: → ItemName
    └── Item Description Text: → ItemDescription
    └── Item Icon: → ItemIcon
    └── Show Delay: 0.5
    └── Offset: (15, -15)
```

3. **Готово!** Tooltip будет автоматически появляться при наведении на слоты.

#### Код (если нужна своя система вместо TooltipManager):

```csharp
using UniversalDragAndDrop.Slots;

public class MySoundManager : MonoBehaviour
{
    private void OnEnable()
    {
        // Подписываемся на глобальные события
        SlotHoverEventListener.OnAnySlotHoverEnter += OnSlotHoverEnter;
        SlotHoverEventListener.OnAnySlotHoverExit += OnSlotHoverExit;
    }

    private void OnDisable()
    {
        // ВАЖНО: отписываемся!
        SlotHoverEventListener.OnAnySlotHoverEnter -= OnSlotHoverEnter;
        SlotHoverEventListener.OnAnySlotHoverExit -= OnSlotHoverExit;
    }

    private void OnSlotHoverEnter(SlotHoverEventArgs args)
    {
        if (args.HasItem)
        {
            PlayHoverSound(args.Item);
        }
    }

    private void OnSlotHoverExit(SlotHoverEventArgs args)
    {
        // Можно проиграть другой звук при уходе
    }
}
```

---

### Способ 2: UnityEvents в Inspector

**Для**: Локальных реакций на наведение (звуки, анимации, эффекты частиц)

#### Настройка:

1. **Выбрать конкретный слот** в сцене или префабе
2. **Добавить SlotHoverEventListener** если его нет
3. **Настроить UnityEvents** в Inspector:

```
SlotHoverEventListener
├── On Slot Hover Enter (UnityEvent)
│   └── + добавить метод:
│       └── AudioSource.PlayOneShot(hoverSound)
└── On Slot Hover Exit (UnityEvent)
    └── + добавить метод:
        └── ParticleSystem.Stop()
```

#### Пример (звук при наведении):

```
Equipment Slot
├── UniversalSlot
├── SlotHoverEventListener
│   ├── Only When Not Empty: ✓
│   └── On Slot Hover Enter:
│       └── AudioSource.Play() → связать с AudioSource компонентом
└── AudioSource
    └── Clip: hover_equipment_sound
```

---

### Способ 3: Наследование с виртуальными методами

**Для**: Кастомной логики для специфических типов слотов

#### Пример: Особый слот экипировки со свечением

```csharp
using UniversalDragAndDrop.Slots;
using UnityEngine;

public class EquipmentSlotHoverListener : SlotHoverEventListener
{
    [Header("Equipment Specific")]
    [SerializeField] private ParticleSystem _glowEffect;
    [SerializeField] private Color _hoverColor = Color.yellow;

    private Color _originalColor;
    private Image _slotImage;

    private void Awake()
    {
        _slotImage = GetComponent<Image>();
        _originalColor = _slotImage != null ? _slotImage.color : Color.white;
    }

    protected override void OnSlotHoverEnterInternal(SlotHoverEventArgs args)
    {
        // Кастомная логика для слотов экипировки

        // Проверяем что это экипировка
        if (args.Item is EquipmentItemAdapter equipment)
        {
            // Показываем подходит ли предмет для текущего персонажа
            if (!CanEquip(equipment))
            {
                // Красная подсветка если не можем экипировать
                if (_slotImage != null)
                    _slotImage.color = Color.red;

                // Отменяем показ tooltip
                args.Cancel = true;
                return;
            }
        }

        // Включаем эффект свечения
        if (_glowEffect != null)
            _glowEffect.Play();

        // Меняем цвет слота
        if (_slotImage != null)
            _slotImage.color = _hoverColor;

        // Базовая реализация вызовет события и UnityEvents
        base.OnSlotHoverEnterInternal(args);
    }

    protected override void OnSlotHoverExitInternal(SlotHoverEventArgs args)
    {
        // Выключаем эффекты
        if (_glowEffect != null)
            _glowEffect.Stop();

        // Возвращаем цвет
        if (_slotImage != null)
            _slotImage.color = _originalColor;

        base.OnSlotHoverExitInternal(args);
    }

    private bool CanEquip(EquipmentItemAdapter equipment)
    {
        // Проверка уровня, класса и т.д.
        return true;
    }
}
```

#### Пример: Слот с задержанным показом дополнительной информации

```csharp
public class DetailedInfoSlotHover : SlotHoverEventListener
{
    [SerializeField] private float _detailedInfoDelay = 2f;
    [SerializeField] private GameObject _detailedInfoPanel;

    private Coroutine _detailedInfoCoroutine;

    protected override void OnSlotHoverEnterInternal(SlotHoverEventArgs args)
    {
        // Запускаем задержанный показ детальной информации
        _detailedInfoCoroutine = StartCoroutine(ShowDetailedInfoDelayed(args));

        base.OnSlotHoverEnterInternal(args);
    }

    protected override void OnSlotHoverExitInternal(SlotHoverEventArgs args)
    {
        // Отменяем показ детальной информации
        if (_detailedInfoCoroutine != null)
        {
            StopCoroutine(_detailedInfoCoroutine);
            _detailedInfoCoroutine = null;
        }

        _detailedInfoPanel?.SetActive(false);

        base.OnSlotHoverExitInternal(args);
    }

    private IEnumerator ShowDetailedInfoDelayed(SlotHoverEventArgs args)
    {
        yield return new WaitForSeconds(_detailedInfoDelay);

        // Показываем расширенную панель с характеристиками
        if (args.HasItem && _detailedInfoPanel != null)
        {
            _detailedInfoPanel.SetActive(true);
            // Заполняем информацией...
        }
    }
}
```

---

## 🔧 Расширение системы

### Добавление описания к предметам

Создайте интерфейс для предметов с описанием:

```csharp
public interface IDescribableItem : IInventoryItem
{
    string Description { get; }
    ItemRarity Rarity { get; }
}

// Адаптер с описанием
public class DescribableItemSOAdapter : ItemSOAdapter, IDescribableItem
{
    private readonly ItemExampleWithDescriptionSO _itemWithDesc;

    public DescribableItemSOAdapter(ItemExampleWithDescriptionSO item) : base(item)
    {
        _itemWithDesc = item;
    }

    public string Description => _itemWithDesc.Description;
    public ItemRarity Rarity => _itemWithDesc.Rarity;
}
```

Измените TooltipManager:

```csharp
private string GetExtendedDescription(IInventoryItem item)
{
    if (item is IDescribableItem describable)
    {
        return describable.Description;
    }

    return "No description available";
}
```

### Добавление цветов редкости

```csharp
// В TooltipManager.cs
private void PopulateTooltipContent(IInventoryItem item)
{
    // ... существующий код ...

    // Цвет фона по редкости
    if (_backgroundImage != null && item is IDescribableItem describable)
    {
        _backgroundImage.color = GetRarityColor(describable.Rarity);
    }
}

private Color GetRarityColor(ItemRarity rarity)
{
    switch (rarity)
    {
        case ItemRarity.Common: return Color.white;
        case ItemRarity.Uncommon: return Color.green;
        case ItemRarity.Rare: return Color.blue;
        case ItemRarity.Epic: return new Color(0.5f, 0f, 0.5f); // Purple
        case ItemRarity.Legendary: return Color.yellow;
        default: return Color.white;
    }
}
```

### Множественные tooltip менеджеры

Можно создать разные менеджеры для разных целей:

```csharp
// Фильтрация по инвентарю
public class EquipmentTooltipManager : MonoBehaviour
{
    private void OnEnable()
    {
        SlotHoverEventListener.OnAnySlotHoverEnter += OnSlotHover;
    }

    private void OnSlotHover(SlotHoverEventArgs args)
    {
        // Показываем tooltip только для слотов экипировки
        if (args.Inventory is EquipmentInventory)
        {
            ShowEquipmentTooltip(args);
        }
    }
}
```

---

## 📊 Сравнение способов использования

| Критерий | Способ 1<br>(Static Events) | Способ 2<br>(UnityEvents) | Способ 3<br>(Наследование) |
|----------|----------------------------|---------------------------|----------------------------|
| **Простота** | ⭐⭐⭐ Простой код | ⭐⭐⭐ Просто в Inspector | ⭐⭐ Требует код |
| **Гибкость** | ⭐⭐ Глобальная | ⭐ Локальная | ⭐⭐⭐ Максимальная |
| **Использование** | Глобальные системы | Звуки, анимации | Кастомная логика |
| **Настройка** | Один раз на сцене | Для каждого слота | Создать класс |

---

## ✅ Лучшие практики

1. **Используйте Способ 1** для глобальных систем (tooltip, звуки, аналитика)
2. **Используйте Способ 2** для локальных эффектов (частицы, анимации)
3. **Используйте Способ 3** для специфической логики разных типов слотов
4. **Комбинируйте** способы - можно использовать все три одновременно!

### Пример комбинирования:

```
Equipment Slot
├── EquipmentSlotHoverListener (наследник - Способ 3)
│   ├── Custom glow effect
│   ├── On Slot Hover Enter (UnityEvent - Способ 2)
│   │   └── AudioSource.Play(hover_sound)
│   └── [Вызывает base, который вызовет статические события]
│
└── TooltipManager на Canvas (Способ 1)
    └── Показывает глобальный tooltip
```

---

## 🐛 Отладка

### Tooltip не появляется:

1. ✅ Проверьте что `SlotHoverEventListener` добавлен на слот
2. ✅ Проверьте что `TooltipManager` есть на сцене
3. ✅ Проверьте что `Tooltip Panel` изначально **выключен** (SetActive = false)
4. ✅ Включите `Debug Log` в `SlotHoverEventListener` для логов
5. ✅ Проверьте что слот не пустой (если `Only When Not Empty = true`)

### События не вызываются:

1. ✅ Проверьте что у слота есть `Graphic Raycaster` на Canvas
2. ✅ Проверьте что `EventSystem` есть на сцене
3. ✅ Проверьте что слот не перекрыт другими UI элементами

### Tooltip появляется не там:

1. ✅ Настройте `Offset` в `TooltipManager`
2. ✅ Попробуйте другой `Anchor` (Cursor, SlotTopRight и т.д.)
3. ✅ Увеличьте `Screen Padding` чтобы не выходить за границы

---

## 📝 Примеры использования

### Пример 1: Базовый tooltip (Способ 1)

```
1. Добавьте SlotHoverEventListener на Prefabs/Slot.prefab
2. Создайте TooltipManager на Canvas
3. Назначьте ссылки на UI элементы
4. Play!
```

### Пример 2: Звук при наведении (Способ 2)

```
1. Выберите слот в сцене
2. Добавьте AudioSource с клипом
3. Добавьте SlotHoverEventListener
4. В On Slot Hover Enter:
   → AudioSource.PlayOneShot()
5. Play!
```

### Пример 3: Кастомный слот редкости (Способ 3)

```csharp
// 1. Создайте класс
public class RaritySlotHover : SlotHoverEventListener
{
    [SerializeField] private ParticleSystem _particles;

    protected override void OnSlotHoverEnterInternal(SlotHoverEventArgs args)
    {
        if (args.Item is IRarityItem rarity && rarity.Rarity == ItemRarity.Legendary)
        {
            _particles.Play(); // Эффект только для легендарных
        }
        base.OnSlotHoverEnterInternal(args);
    }
}

// 2. Замените компонент на слоте
// 3. Назначьте ParticleSystem
// 4. Play!
```

---

## 🎯 Итого

Система Slot Hover предоставляет три гибких способа реагировать на наведение курсора:

1. **Глобальные события** - TooltipManager, SoundManager, Analytics
2. **UnityEvents** - Звуки, анимации, эффекты (настройка в Inspector)
3. **Наследование** - Кастомная логика для разных типов слотов

Все способы **опциональны** и **не влияют** на основную систему drag & drop!

---

**Автор**: UniversalDragAndDrop
**Версия**: 1.0
