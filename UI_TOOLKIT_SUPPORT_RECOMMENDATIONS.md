# UI Toolkit Support Recommendations

**Last Updated**: 2026-03-13

Документ фиксирует практические рекомендации по добавлению поддержки Unity UI Toolkit в `DragAndDropSystem`.

## Короткий вывод

Поддержка UI Toolkit в текущем проекте **реалистична**, но это не "маленькая интеграция".

Сложность:
- **Средняя**, если делать только базовый drag & drop без полного parity с текущим uGUI UX.
- **Выше средней / высокая**, если нужна полноценная поддержка:
  - pointer + keyboard/gamepad;
  - tooltip;
  - context menu;
  - drag visual;
  - batch drag;
  - area drop;
  - сохранение совместимости текущего API.

Главный вывод:
- **core transfer pipeline уже достаточно хорошо отделен от UI**;
- **input/presentation слой пока сильно привязан к uGUI**;
- поэтому UI Toolkit лучше добавлять **как отдельный адаптерный слой**, а не как набор условных `if` внутри существующих uGUI-компонентов.

## Что уже хорошо подготовлено

Текущая архитектура уже дает сильную базу для UI Toolkit:

- `DragAndDropManager` управляет lifecycle drag и routing в `IDropProcessor`;
- `InventoryDropProcessor` уже выступает границей между UI target layer и transfer core;
- `TransferPlanner` / `TransferPlanExecutor` / `InventoryTransferService` в целом не зависят от UI-фреймворка;
- правила (`Global`, `Inventory`, `Slot`) уже отделены от конкретного UI;
- `InputEventRouter` концептуально отделяет raw input от действий;
- `.claude`-документация прямо фиксирует намерение держать input отдельно от transfer/domain logic.

Это значит, что **переписывать ядро drag/drop не нужно**.

## Что сейчас мешает UI Toolkit

### 1. `ISlot` является `MonoBehaviour`

Файл: `Scripts/Slots/ISlot.cs`

Сейчас слот одновременно является:
- доменной сущностью;
- view-компонентом;
- сценовым объектом;
- объектом, на который завязаны selection, drag source и drop target.

Для uGUI это удобно.
Для UI Toolkit это ограничение, потому что `VisualElement` не может стать наследником `MonoBehaviour`.

Следствие:
- текущий слот нельзя напрямую переиспользовать как UITK element;
- либо нужен отдельный bridge-слой;
- либо нужно разделить `slot state` и `slot view`.

### 2. `UniversalInventory` ожидает scene/prefab slots

Файл: `Scripts/Inventories/UniversalInventory.cs`

Сейчас инвентарь:
- кэширует слоты через `GetComponentsInChildren<ISlot>()`;
- инстанцирует `UniversalSlot` prefab;
- хранит список `ISlot`, которые фактически являются `GameObject`-компонентами.

Это означает, что текущий `UniversalInventory` спроектирован вокруг object-based scene UI, а не вокруг data-driven UITK tree.

### 3. Input pipeline привязан к `PointerEventData` и `Selectable`

Файлы:
- `Scripts/Interaction/SlotInputAdapter.cs`
- `Scripts/Interaction/InputEventRouter.cs`
- `Scripts/Interaction/SlotInteractionActions.cs`

Сейчас маршрут выглядит так:

`EventSystem -> SlotInputAdapter -> InputEventRouter -> PointerBinding -> SlotInteractionAction`

Проблемы для UI Toolkit:
- `SlotInputAdapter` наследуется от `Selectable`;
- pointer flow использует `PointerEventData`;
- focus/navigation опираются на `EventSystem.currentSelectedGameObject`;
- actions принимают `SlotInputAdapter` и `PointerEventData`, то есть контракты уже завязаны на uGUI.

Это главный участок, который придется рефакторить, если нужна качественная cross-UI поддержка.

### 4. Presentation layer тоже uGUI-specific

Файлы:
- `Scripts/UI/DragVisualPresenter.cs`
- `Scripts/UI/TooltipManager.cs`
- `Scripts/ContextMenu/UI/UniversalContextMenuView.cs`
- `Scripts/UI/InventoryDropArea.cs`

Сейчас здесь используются:
- `Canvas`;
- `RectTransform`;
- `Image`;
- `Selectable`;
- `TextMeshProUGUI`;
- позиционирование через экранные координаты и `RectTransformUtility`.

Для UI Toolkit эти компоненты почти не переиспользуются.

Следствие:
- drag ghost;
- tooltip;
- context menu;
- area drop highlight

нужно будет или переписывать, или абстрагировать.

## Реальные варианты реализации

Ниже 3 реалистичных варианта.

---

## Вариант A. Быстрый bridge поверх текущей архитектуры

### Идея

Оставить текущие core-классы почти без изменений и добавить слой адаптеров для UI Toolkit, который:
- строит визуальные `VisualElement`-слоты;
- хранит связь `VisualElement <-> ISlot`;
- вручную конвертирует UITK события в вызовы `InputEventRouter`;
- отдельно реализует tooltip / drag ghost / context menu.

### Что потребуется

Примерно такие новые классы:

- `UIToolkitInventoryView`
- `UIToolkitSlotView`
- `UIToolkitInputAdapter`
- `UIToolkitDragVisualPresenter`
- `UIToolkitTooltipPresenter`
- `UIToolkitContextMenuView`
- `UIToolkitDropArea`

### Плюсы

- самый быстрый путь к рабочему прототипу;
- почти не затрагивает текущий uGUI-код;
- низкий риск сломать existing demos;
- можно быстро показать support в маркетинговом смысле.

### Минусы

- архитектурный долг;
- сохранится сильная зависимость core API от uGUI-типов;
- часть логики будет дублироваться между uGUI и UITK;
- navigation/focus будет неудобно поддерживать одинаково;
- future shaped items и сложные grid-сценарии будет труднее развивать.

### Когда выбирать

Подходит, если цель:
- быстро получить "UI Toolkit supported" в ограниченном scope;
- выпустить experimental support;
- проверить спрос до большого рефакторинга.

### Оценка

- Базовый drag/drop: **5-10 рабочих дней**
- C tooltip/context menu/navigation parity: **2-4 недели**

---

## Вариант B. Рефакторинг interaction contracts + две реализации view layer

### Идея

Сохранить transfer core, но вынести из input/action слоя все зависимости на uGUI в framework-neutral контракты.

После этого сделать две реализации:
- `uGUI adapter layer`
- `UI Toolkit adapter layer`

Это наиболее сбалансированный вариант.

### Что именно нужно абстрагировать

Вместо связки:
- `SlotInputAdapter`
- `PointerEventData`
- `GameObject selected`

ввести нейтральные структуры вроде:

- `InventoryInteractionContext`
- `PointerLikeEvent`
- `NavigationAnchor`
- `IInventorySlotViewHandle`

Пример состава `InventoryInteractionContext`:
- `UniversalInventory Inventory`
- `ISlot Slot`
- `Vector2 ScreenPosition`
- `FocusSource FocusSource`
- `PointerButton Button`
- `ModifierState Modifiers`
- `InteractionPhase Phase`
- `object SourceHandle`

Тогда:
- `SlotInteractionAction` перестанет принимать `SlotInputAdapter`;
- `InputEventRouter` будет работать не с `PointerEventData`, а с нейтральным input event;
- uGUI и UITK будут только поставщиками этих данных.

### Плюсы

- хорошая архитектурная чистота;
- намного легче поддерживать два UI backend;
- меньше дублирования логики;
- проще тестировать input pipeline;
- лучше база под shaped items, grid placement и complex previews.

### Минусы

- заметный объём рефакторинга;
- затрагивает публичные extension points;
- потребует аккуратной миграции `InteractionBindingsProfile` и `SlotInteractionAction`;
- нужна регрессионная проверка существующих сцен.

### Когда выбирать

Это **рекомендуемый вариант**, если цель:
- качественная долгосрочная поддержка UI Toolkit;
- сохранение развития проекта без хака;
- подготовка к релизу `v2` или крупному feature update.

### Оценка

- Архитектурный рефакторинг contracts: **1-2 недели**
- Базовый UITK backend: **1-2 недели**
- Tooltip/context menu/navigation parity: **1-2 недели**

Итого:
- **3-6 недель** на качественную поддержку.

---

## Вариант C. Полное разделение data model и visual slots

### Идея

Сделать более фундаментальный шаг:
- перестать считать `ISlot` одновременно data-unit и visual component;
- ввести отдельную модель слота;
- представление слота в uGUI и UITK сделать чисто визуальным.

Пример целевой модели:

- `InventorySlotState` или `InventorySlotModel`
- `IInventorySlotView`
- `UguiSlotView`
- `UitkSlotView`

Тогда:
- `UniversalInventory` работает с data slot list;
- view layer только отражает состояние;
- selection/focus/input держатся на runtime handles, а не на `MonoBehaviour`.

### Плюсы

- наиболее правильная архитектура;
- отличная база для shaped items;
- проще виртуализация списков и большие inventory grids;
- легче тестировать и сериализовать;
- лучшая совместимость с любым UI backend.

### Минусы

- это уже почти redesign значительной части current inventory presentation architecture;
- большой риск регрессий;
- существенно дороже по времени;
- не лучший шаг прямо перед релизом.

### Когда выбирать

Подходит, если:
- планируется крупный `v2`;
- параллельно всё равно будет большой рефакторинг под shaped items;
- приоритетом является долгосрочная архитектура, а не быстрый rollout.

### Оценка

- **4-8+ недель**, в зависимости от глубины переписывания и обратной совместимости.

---

## Рекомендуемый путь

Наиболее прагматичный маршрут для текущего проекта:

1. **Не делать полный redesign сразу.**
2. Сделать **Вариант B** как основную стратегию.
3. Внутри него сначала реализовать **минимальный scope UITK**:
   - single-slot drag/drop;
   - pointer support;
   - drag visual;
   - basic hover;
   - без полного context menu/navigation parity на первом этапе.
4. После стабилизации добавить:
   - selection;
   - keyboard/gamepad navigation;
   - tooltip;
   - context menu;
   - area drop.

Причина:
- Вариант A слишком быстро превращается в два почти разных input UI стека;
- Вариант C сейчас слишком дорогой;
- Вариант B дает наилучший баланс качества и скорости.

## Что не стоит делать

### 1. Не смешивать uGUI и UITK условными `if` по всему коду

Плохой путь:
- в `SlotInputAdapter`, `InputEventRouter`, `TooltipManager`, `ContextMenuView` добавлять ветки:
  - "если uGUI";
  - "если UITK".

Почему плохо:
- код быстро станет хрупким;
- сложнее тестировать;
- regressions будут дороже.

### 2. Не делать `VisualElement` эквивалентом `MonoBehaviour`-слота без нового контракта

Если просто сделать UITK view, но продолжить таскать через всю систему `SlotInputAdapter` и `PointerEventData`, получится ломкий bridge, который сложно расширять.

### 3. Не переписывать transfer core ради UI Toolkit

`TransferPlanner`, `TransferPlanExecutor`, `InventoryDropProcessor` и правила сейчас как раз уже достаточно отделены.

UI Toolkit support не должен приводить к переписыванию policy/planner/executor слоя.

## Конкретный план реализации

Ниже минимальный рекомендуемый поэтапный план.

### Этап 1. Выделить framework-neutral interaction context

Цель:
- отвязать actions и router от `SlotInputAdapter` и `PointerEventData`.

Сделать:
- ввести нейтральную модель interaction event;
- адаптировать `PointerBinding`;
- адаптировать `SlotInteractionAction`;
- убрать прямую зависимость action contracts от uGUI типов.

Результат:
- uGUI продолжит работать через adapter;
- UITK сможет подключиться через другой adapter.

### Этап 2. Выделить slot view handle

Цель:
- не использовать `GameObject` и `EventSystem.currentSelectedGameObject` как универсальную опору.

Сделать:
- ввести handle/anchor abstraction для focused/hovered slot;
- хранить `ISlot` отдельно от view reference;
- дать router возможность работать без `GameObject`.

Результат:
- navigation и quick actions станут переносимее.

### Этап 3. Сделать UITK inventory view

Сделать:
- `UIDocument`/`VisualElement`-based inventory root;
- visual slots;
- биндинг `ISlot -> VisualElement`;
- hover/pressed/focus state mapping;
- отправку событий в router.

Первый scope:
- pointer enter/exit/down/up;
- drag start;
- drop on slot.

### Этап 4. Сделать UITK drag visual presenter

Сделать отдельный presenter для:
- single drag visual;
- batch drag visual;
- follow pointer / navigation anchor.

Важно:
- не пытаться переиспользовать `Canvas` presenter через слой костылей;
- сделать отдельную реализацию поверх `VisualElement` overlay.

### Этап 5. Добавить UITK tooltip

Сделать отдельный presenter:
- show/hide;
- delayed display;
- screen clamping;
- позиционирование относительно курсора или anchor element.

### Этап 6. Добавить UITK context menu

Сделать:
- view для списка entries;
- keyboard/gamepad navigation;
- позиционирование около slot element;
- закрытие через global action.

### Этап 7. Выравнять navigation parity

Самый сложный UX-этап.

Нужно отдельно проверить:
- current focused slot;
- drag completion при navigation mode;
- context menu loop;
- selection operations;
- quick actions;
- active inventory switching.

## Рекомендуемый минимальный API для абстракции

Ниже не окончательная реализация, а направление.

### `InventoryPointerEvent`

Поля:
- `Vector2 ScreenPosition`
- `PointerButton Button`
- `ModifierState Modifiers`
- `InteractionPhase Phase`
- `bool IsPrimary`

### `InventoryInteractionContext`

Поля:
- `UniversalInventory Inventory`
- `ISlot Slot`
- `FocusSource FocusSource`
- `InventoryPointerEvent Pointer`
- `object ViewHandle`

### `IInventoryViewBridge`

Ответственность:
- сообщать focused/hovered slot;
- предоставлять anchor position;
- отправлять raw UI events в router.

### `IInventoryOverlayPresenter`

Ответственность:
- drag visual;
- tooltip;
- context menu.

Можно иметь две реализации:
- `UguiInventoryOverlayPresenter`
- `UitkInventoryOverlayPresenter`

## Риски

### 1. Поломка текущего API расширений

Особенно чувствительные точки:
- `SlotInteractionAction`
- `PointerBinding`
- `InteractionBindingsProfile`
- `InventoryExtraInteractionBinder`

Если их менять, нужно заранее решить:
- что считать стабильным public API;
- как сохранить compatibility;
- где использовать deprecated-path.

### 2. Navigation parity

Pointer support сделать сравнительно просто.

Сложнее всего:
- keyboard/gamepad navigation;
- active inventory focus;
- drag completion без pointer hover;
- context menu navigation loop;
- quick actions через "current slot".

### 3. Дублирование логики presentation

Если не ввести нормальные контракты, tooltip/context menu/drag visual будут почти продублированы между uGUI и UITK.

### 4. Рост стоимости будущих shaped items

Если UI Toolkit внедрить хаком, shaped items потом станут дороже.

Если делать аккуратно через abstraction layer, наоборот получится хорошая база.

## Связь с roadmap проекта

С учетом текущих проектных рекомендаций, UI Toolkit support выглядит как **крупное расширение платформы UI**, а не как мелкий polishing task.

Практически это значит:
- если приоритетом является быстрый релиз, UI Toolkit лучше вводить после стабилизации тестов и API;
- если приоритетом является сильная техническая платформа следующей версии, UI Toolkit можно делать как часть большого архитектурного update.

Особенно важно не начинать эту работу без:
- regression checks для transfer pipeline;
- tests для interaction flows;
- ясного понимания stable extension points.

## Практическая рекомендация

Если нужен самый разумный путь:

### Рекомендуемый сценарий

1. Зафиксировать current public interaction API.
2. Добавить несколько тестов на pointer phases и focus-sensitive actions.
3. Провести умеренный рефакторинг interaction contracts.
4. Сделать experimental UITK backend.
5. Довести parity только после подтверждения, что backend действительно удобен.

### Не рекомендуемый сценарий

1. Сразу писать UITK поверх текущих `Selectable`/`PointerEventData` assumptions.
2. Дублировать router behavior в отдельном UITK-only pipeline.
3. Поддерживать два разных action contract для uGUI и UITK.

Это почти гарантированно усложнит проект.

## Итог

Поддержка UI Toolkit:
- **вполне достижима**;
- **лучше всего реализуется через adapter/refactor approach**;
- **не требует переписывания transfer core**;
- **требует рефакторинга interaction contracts и presentation layer**.

Если делать аккуратно, это может стать хорошим шагом к более зрелой архитектуре.
Если делать быстро и локально, получится рабочий prototype, но с заметным архитектурным долгом.
