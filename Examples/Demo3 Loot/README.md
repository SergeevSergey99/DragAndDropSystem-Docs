# Demo3: Loot System - Персонаж взаимодействует с сундуками

**Last Updated**: 2026-02-28

## Актуальность под transfer pipeline

Перетаскивание между инвентарями в UI этого демо выполняется через текущий policy/planner/executor pipeline.


Пример системы взаимодействия с контейнерами (сундуками) для 2D Top-Down игр. Демонстрирует **правильное разделение ответственности**: игровые объекты не знают о UI, UI управляется отдельным контроллером через события.

## 📋 Описание

Этот пример показывает как создать систему лута где:
- Игрок перемещается по миру (WASD, Top-Down 2D)
- Игрок подходит к сундукам и видит подсказку "E - открыть"
- При нажатии E открывается UI с двумя инвентарями (игрок + сундук)
- Можно перетаскивать предметы между инвентарями
- При закрытии UI сохраняется состояние сундука
- Игровая логика полностью отделена от UI

## 🏗️ Архитектура

### Разделение ответственности

```
┌─────────────────────────────────────────────────────────┐
│                 ИГРОВОЙ МИР (No UI!)                     │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  Player                          Chest                   │
│  ├── PlayerController            ├── List<ItemSO>        │
│  ├── PlayerInteraction  ───┐     │   (данные)            │
│  │   (события)             │     └── IInteractable       │
│  └── PlayerInventoryData   │                             │
│      (данные)               │                             │
│                             │                             │
└─────────────────────────────┼─────────────────────────────┘
                              │ Events
                              ▼
┌─────────────────────────────────────────────────────────┐
│                    UI LAYER                              │
├─────────────────────────────────────────────────────────┤
│                                                           │
│  LootUIController (МЕДИАТОР)                             │
│  ├── Слушает события от игрового мира                    │
│  ├── Управляет UI панелями                               │
│  ├── Связывает DataBindings                              │
│  └── Блокирует управление игроком                        │
│                                                           │
│  InteractionPrompt                                       │
│  └── Показывает "E - открыть"                            │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

**Ключевой принцип**:
- ❌ Chest **НЕ** знает о UI
- ❌ Player **НЕ** знает о UI
- ✅ UI **знает** о Chest и Player через LootUIController
- ✅ События идут от игрового мира → к UI (односторонняя зависимость)

### Компоненты системы

#### 1. **Игровая логика (Game World Layer)**

**IInteractable** - интерфейс для всех интерактивных объектов
- `bool CanInteract` - можно ли взаимодействовать
- `string InteractionPrompt` - текст подсказки
- `void Interact(PlayerInteraction player)` - выполнить взаимодействие

**Chest** - сундук с предметами
- Хранит `List<ItemExampleSO>` - содержимое
- Вызывает события: `OnChestOpened`, `OnChestClosed`, `OnChestEmptied`
- Методы: `AddItem()`, `RemoveItem()`, `GetItems()`
- **НЕ знает о UI**

**PlayerInteraction** - система взаимодействия игрока
- Ищет `IInteractable` в радиусе через `Physics2D.OverlapCircle`
- Вызывает события: `OnInteractableEntered`, `OnInteractableExited`, `OnInteracted`
- **НЕ знает о UI**

**PlayerInventoryData** - данные инвентаря игрока
- Хранит `List<ItemExampleSO>` - предметы игрока
- Методы: `AddItem()`, `RemoveItem()`, `HasSpace()`
- **НЕ знает о UI**

#### 2. **DataBinding (связь данных и UI)**

**ChestInventoryDataBinding** - связывает Chest ↔ UniversalInventory
- Наследуется от `InventoryDataBindingBase`
- Метод `BindToChest(Chest)` - привязка к сундуку (динамически)
- Двусторонняя синхронизация:
  - Данные сундука → UI (через `ReloadUI()`)
  - UI → Данные сундука (через `OnItemAddedToUI()`, `OnItemRemovedFromUI()`)

**PlayerInventoryDataBinding** - связывает PlayerInventoryData ↔ UniversalInventory
- Аналогично ChestInventoryDataBinding
- Привязывается статически в Awake (не динамически)

#### 3. **UI Layer**

**LootUIController** - МЕДИАТОР (единственный компонент, знающий о UI)
- Подписывается на события от `PlayerInteraction` и `Chest`
- Управляет показом/скрытием UI панелей
- Блокирует/разблокирует управление игроком
- Связывает DataBindings с нужными данными

**InteractionPrompt** - подсказка взаимодействия
- Слушает события от `PlayerInteraction`
- Показывает текст "E - открыть сундук"

## 🔄 Поток данных

### Открытие сундука:

```
1. PlayerInteraction (Update)
   → Physics2D.OverlapCircle
   → находит Chest

2. PlayerInteraction
   → OnInteractableEntered.Invoke(chest)

3. InteractionPrompt (подписан на событие)
   → показывает "E - открыть"

4. Игрок нажимает E

5. PlayerInteraction
   → chest.Interact(this)
   → OnInteracted.Invoke(chest)

6. Chest
   → _isOpen = true
   → OnChestOpened.Invoke(this)

7. LootUIController (подписан на OnInteracted)
   → OpenLootUI(chest)
   → _chestBinding.BindToChest(chest)
   → _lootPanel.SetActive(true)
   → _playerController.SetInputLocked(true)

8. ChestInventoryDataBinding
   → ReloadUI()
   → загружает предметы из Chest в UI
```

### Перетаскивание предмета (Chest → Player):

```
1. Пользователь перетаскивает предмет

2. DragAndDropManager
   → проверяет правила
   → выполняет перенос

3. UniversalInventory (chest)
   → OnItemRemoved.Invoke(args)

4. ChestInventoryDataBinding
   → OnItemRemovedFromUI(args)
   → _chest.RemoveItem(itemSO)

5. UniversalInventory (player)
   → OnItemAdded.Invoke(args)

6. PlayerInventoryDataBinding
   → OnItemAddedToUI(args)
   → _playerData.AddItem(itemSO)
```

## ⚙️ Настройка в Unity

### 1. Настройка игрока

#### 1.1 Player GameObject:

```
Player
├── SpriteRenderer (спрайт игрока)
├── Rigidbody2D (Gravity Scale = 0, Freeze Rotation)
├── Collider2D (коллайдер игрока)
├── PlayerController (скрипт движения)
│   └── Walk Speed: 5
│   └── Run Speed: 8
│   └── Player Camera: Main Camera
├── PlayerInteraction (новый скрипт)
│   └── Interaction Radius: 2
│   └── Interactable Layer: Everything (или создайте слой "Interactable")
│   └── Interact Key: E
└── PlayerInventoryData (новый скрипт)
    └── Max Capacity: 20
    └── Starting Items: (добавьте ItemExampleSO если нужно)
    └── Add Starting Items On Awake: ✓
```

### 2. Создание префаба сундука

#### 2.1 Chest Prefab:

```
Chest
├── SpriteRenderer
│   └── Sprite: chest_closed
├── CircleCollider2D
│   └── Is Trigger: ✓
│   └── Radius: 2
└── Chest (скрипт)
    └── Items: (добавьте ItemExampleSO)
    └── Max Capacity: 20
    └── Closed Sprite: chest_closed
    └── Open Sprite: chest_open
    └── Interaction Radius: 2
```

**Важно**: Убедитесь что у сундука есть Layer который включен в `Interactable Layer` компонента PlayerInteraction.

#### 2.2 Создание предметов (ItemExampleSO):

1. ПКМ в папке Assets → Create → DragAndDropSystem → Examples → ItemExampleSO
2. Настройте:
   - Item Name: "Sword"
   - Icon: (назначьте спрайт)
   - Item Type: "Weapon" (опционально)

Создайте несколько предметов для тестирования.

### 3. Настройка UI Canvas

#### 3.1 Структура Canvas:

```
Canvas
├── PlayerInventoryPanel (GameObject - всегда активен)
│   ├── Background (Image)
│   ├── Title (TextMeshPro): "Инвентарь игрока"
│   ├── InventoryGrid (GameObject)
│   │   └── UniversalInventory (компонент)
│   │       └── Slot Count: 20
│   │       └── Slot Prefab: Prefabs/Slot.prefab
│   └── PlayerInventoryDataBinding (компонент)
│       └── Inventory: → InventoryGrid/UniversalInventory
│       └── Player Data: → Player/PlayerInventoryData
│
├── LootPanel (GameObject - изначально ВЫКЛЮЧЕН)
│   ├── Background (Image - полупрозрачный черный)
│   ├── Container (GameObject)
│   │   ├── PlayerSide (GameObject)
│   │   │   ├── Title: "Игрок"
│   │   │   ├── InventoryGrid
│   │   │   │   └── UniversalInventory
│   │   │   └── (это ССЫЛКА на PlayerInventoryPanel/InventoryGrid)
│   │   │
│   │   ├── ChestSide (GameObject)
│   │   │   ├── Title: "Сундук"
│   │   │   └── InventoryGrid
│   │   │       └── UniversalInventory
│   │   │           └── Slot Count: 20
│   │   │           └── Slot Prefab: Prefabs/Slot.prefab
│   │   │
│   │   └── CloseButton (Button)
│   │       └── OnClick: → LootUIController.CloseLootUI()
│   │
│   └── ChestInventoryDataBinding (компонент)
│       └── Inventory: → ChestSide/InventoryGrid/UniversalInventory
│
├── InteractionPromptPanel (GameObject)
│   ├── Background (Image)
│   ├── PromptText (TextMeshPro): "E - открыть"
│   └── InteractionPrompt (компонент)
│       └── Player Interaction: → Player/PlayerInteraction
│       └── Prompt Panel: → InteractionPromptPanel
│       └── Prompt Text: → PromptText
│       └── Use Animation: ✓ (опционально)
│
└── LootUIController (компонент на Canvas)
    └── Player Interaction: → Player/PlayerInteraction
    └── Player Controller: → Player/PlayerController
    └── Loot Panel: → LootPanel
    └── Player Inventory UI: → PlayerInventoryPanel/InventoryGrid/UniversalInventory
    └── Chest Inventory UI: → LootPanel/ChestSide/InventoryGrid/UniversalInventory
    └── Player Binding: → PlayerInventoryPanel/PlayerInventoryDataBinding
    └── Chest Binding: → LootPanel/ChestInventoryDataBinding
    └── Close Key: Escape
    └── Auto Close On Distance Exit: ✓
```

**Важно**:
- LootPanel должен быть изначально **выключен** (SetActive = false)
- InteractionPromptPanel тоже должен быть **выключен**

### 4. Настройка сцены

1. Добавьте Player в сцену
2. Добавьте несколько Chest префабов в сцену
3. Убедитесь что у сундуков в Items добавлены ItemExampleSO
4. Настройте Camera (Orthographic, Size = 5-10)
5. Нажмите Play!

## 🎮 Использование

### Управление:
- **WASD** - движение в 8 направлениях
- **Shift** - бег
- **E** - взаимодействие с сундуком (открыть/закрыть)
- **Esc** - закрыть UI лута
- **Мышь** - drag & drop предметов

### Игровой процесс:
1. Подойдите к сундуку
2. Появится подсказка "E - открыть сундук"
3. Нажмите E - откроется UI с двумя инвентарями
4. Перетаскивайте предметы между инвентарями
5. Закройте UI (Esc или отойдите от сундука)
6. Состояние сундука сохранится

## ✅ Преимущества архитектуры

1. **Разделение ответственности**
   - Chest и Player не знают о UI → можно тестировать без UI
   - LootUIController - единая точка управления UI
   - Легко добавлять новые интерактивные объекты

2. **Расширяемость**
   - Добавить NPC → реализовать IInteractable
   - Добавить двери → реализовать IInteractable
   - Добавить запертые сундуки → изменить `CanInteract` в Chest

3. **Тестируемость**
   - Можно тестировать Chest без UI
   - PlayerInteraction работает независимо
   - DataBindings изолированы

4. **Чистый код**
   - События вместо прямых вызовов
   - Односторонняя зависимость (Game World не зависит от UI)
   - Легко понять поток данных

## 🔧 Расширение системы

### Запертые сундуки:

```csharp
// В Chest.cs
[SerializeField] private bool _isLocked = false;
[SerializeField] private ItemExampleSO _requiredKey;

public override bool CanInteract => !_isLocked;

public override string InteractionPrompt
{
    get
    {
        if (_isLocked)
            return "Заперто (нужен ключ)";
        return base.InteractionPrompt;
    }
}

public bool TryUnlock(ItemExampleSO key)
{
    if (key == _requiredKey)
    {
        _isLocked = false;
        return true;
    }
    return false;
}
```

### Разные типы контейнеров:

```csharp
// Barrel.cs, Crate.cs - наследуются от IInteractable
public class Barrel : MonoBehaviour, IInteractable
{
    // Аналогично Chest но с другими параметрами
}
```

### NPC диалоги:

```csharp
public class NPC : MonoBehaviour, IInteractable
{
    public bool CanInteract => true;
    public string InteractionPrompt => "E - поговорить";

    public void Interact(PlayerInteraction player)
    {
        // Открыть диалоговое окно
        DialogueManager.Instance.StartDialogue(this);
    }
}
```

### Автоматическое закрытие через время:

```csharp
// В LootUIController.cs
[SerializeField] private float _autoCloseTime = 30f;
private Coroutine _autoCloseCoroutine;

private void OpenLootUI(Chest chest)
{
    // ...существующий код...

    // Запускаем таймер автозакрытия
    if (_autoCloseTime > 0)
    {
        _autoCloseCoroutine = StartCoroutine(AutoCloseAfterTime());
    }
}

private IEnumerator AutoCloseAfterTime()
{
    yield return new WaitForSeconds(_autoCloseTime);
    Debug.Log("Auto-closing loot UI after timeout");
    CloseLootUI();
}
```

## 🐛 Отладка

### Если подсказка не появляется:
1. Проверьте что у Chest есть CircleCollider2D с isTrigger = true
2. Проверьте что слой сундука включен в Interactable Layer
3. Проверьте радиус взаимодействия (Interaction Radius)
4. Включите Debug в PlayerInteraction (Show Debug = true)

### Если UI не открывается:
1. Проверьте что все ссылки в LootUIController назначены
2. Проверьте консоль на ошибки
3. Убедитесь что LootPanel изначально выключен
4. Проверьте что ChestInventoryDataBinding привязан правильно

### Если предметы не перетаскиваются:
1. Проверьте что DragAndDropManager есть на сцене
2. Проверьте правила в DataBindings (может быть запрет)
3. Проверьте что Canvas имеет GraphicRaycaster
4. Проверьте что EventSystem есть на сцене

## 📊 Сравнение с другими примерами

| Аспект | Demo1 (Basic) | Demo2 (Trading) | Demo3 (Loot) |
|--------|--------------|----------------|--------------|
| **Сложность** | Простая | Средняя | Средняя |
| **Архитектура** | Прямая связь | Централизованная | Event-Driven |
| **UI связь** | Прямая | Через Manager | Через Events |
| **Игровой мир** | Статичный | Торговцы | Интерактивный |
| **Расширяемость** | Низкая | Средняя | Высокая |
| **Для обучения** | ✅ Отлично | ⚠️ Средне | ✅ Хорошо |
| **Для продакшена** | ❌ Базовый | ✅ Хорошо | ✅ Отлично |

## 📚 Что демонстрирует этот пример

1. **Разделение Game Logic и UI** - правильная архитектура с односторонней зависимостью
2. **Event-Driven подход** - компоненты общаются через события, не напрямую
3. **IInteractable интерфейс** - расширяемая система взаимодействий
4. **Dynamic DataBinding** - ChestInventoryDataBinding привязывается динамически к разным сундукам
5. **Mediator Pattern** - LootUIController как посредник между слоями
6. **Physics2D.OverlapCircle** - поиск объектов в радиусе для 2D игр

## 📝 Заметки

- Этот пример идеально подходит для 2D RPG, roguelike, action-adventure игр
- Можно легко портировать на 3D (заменить Physics2D на Physics)
- Архитектура позволяет легко добавлять сохранение/загрузку состояния сундуков
- Можно интегрировать с процедурной генерацией лута

---

**Автор**: DragAndDropSystem
**Версия**: 1.0
**Unity Version**: 2021.3+
