# Project Development Recommendations

**Last Updated**: 2026-03-06

Документ фиксирует рекомендуемые следующие шаги по развитию DragAndDropSystem после стабилизации текущей архитектуры.

## Общее мнение о состоянии проекта

Проект уже близок к состоянию, в котором его можно показывать и готовить к публикации как первую зрелую версию.

Сильные стороны текущего состояния:
- единый input pipeline через `SlotInputAdapter` + `InputEventRouter`;
- отделение selection от input через `SelectionOperationBase` и `SelectionSlotAction`;
- policy/planner/executor модель для transfer pipeline;
- поддержка context menu через asset-based и scene-based entries;
- хорошая основа для расширения системы без переписывания базовых компонентов.

Основной риск сейчас не в отсутствии возможностей, а в том, что дальнейшее развитие без тестов и без четкого API-контракта начнет замедлять проект и увеличивать вероятность регрессий.

## Рекомендуемый порядок развития

### 1. Items of Different Size and Shape

Это наиболее ценный следующий шаг для продукта.

Почему именно это:
- резко расширяет спектр поддерживаемых inventory-дизайнов;
- делает систему заметно сильнее типичных slot-only решений;
- повышает продуктовую ценность проекта перед публикацией.

Что желательно заложить сразу:
- footprint предмета;
- anchor cell;
- поворот предмета;
- ghost preview при drag;
- валидация не одного slot, а набора ячеек;
- планирование размещения по footprint целиком.

Важно: это должно быть не частным хаком поверх текущих слотов, а отдельным архитектурным слоем размещения.

Рекомендуемая модель данных для этой фичи:
- один `ItemStack` на один placement;
- один anchor slot как владелец stack;
- остальные клетки только отражают occupancy.

Не рекомендуется делать один и тот же `ItemStack` общим для нескольких слотов. Для текущей архитектуры это создаст слишком много неоднозначности в selection, transfer, sorting, save/load и context menu.

### 2. Тесты перед релизом

До публикации нужно усилить покрытие по самым чувствительным участкам.

Приоритетные зоны:
- `TransferPlanner` policy matrix;
- `TransferPlanExecutor` rollback и deferred events;
- swap flow;
- pointer phases (`Down`, `Up`, `ClickShort`, `ClickLong`);
- selection operations;
- context menu resolution (`asset + scene entries`);
- `ResolveAutoTransferSlot()` и focus-sensitive actions.

Причина проста: без этого следующая большая фича, особенно shaped items, будет ломать текущую систему слишком дорого.

### 3. Сильные примеры

Перед выкладкой лучше иметь не просто набор сцен, а четкую витрину разных use-case.

Рекомендуемые demo-сценарии:
- базовый RPG/Diablo inventory;
- торговля / merchant inventory;
- loot / chest / world drop;
- tactical grid inventory с предметами разного размера.

Именно последний пример лучше всего покажет отличие проекта от обычных inventory-систем.

### 4. UX polishing

Нужно проверить и довести до ровного поведения:
- взаимодействие hover / tooltip / context menu;
- batch drag preview;
- keyboard/gamepad parity;
- стабильное закрытие context menu;
- понятные причины отказа при drop и rule validation.

### 5. API stabilization

Перед релизом стоит зафиксировать extension points и явно решить, что является публичным API.

Кандидаты на стабильный public surface:
- `SlotInteractionAction`;
- `InventoryActionBase`;
- `SelectionOperationBase`;
- `IContextMenuEntry`;
- `IDropProcessor`;
- rule interfaces;
- `InteractionBindingsProfile` / `InventoryExtraInteractionBinder`.

Если это не определить заранее, потом будет сложно выпускать backward-compatible обновления.

## Релизная рекомендация

Есть два реалистичных пути:

### Вариант A. Release v1 sooner

Сначала:
- добить тесты;
- привести examples к презентабельному виду;
- закрыть UX-polish и мелкие баги;
- стабилизировать документацию и API.

Плюсы:
- быстрый выход в публикацию;
- можно собирать обратную связь раньше;
- shaped items пойдут как сильный апдейт `v1.1` или `v2.0`.

### Вариант B. Release with shaped items

Сначала реализовать grid / shaped items, потом делать релиз.

Плюсы:
- более сильный продуктовый оффер;
- выше отличимость от конкурирующих inventory solutions.

Минусы:
- существенно растет объем работ;
- сильно повышается риск затянуть релиз;
- без тестовой базы легко накопить архитектурный долг.

## Практическая рекомендация

Наиболее прагматичный путь сейчас:

1. Добить тесты и документацию.
2. Подготовить сильные demo-сцены.
3. Выпустить базовую версию.
4. После этого делать shaped items как большой feature update.

Если же цель не скорость релиза, а максимальная продуктовая сила первой публикации, тогда shaped items — правильный следующий крупный блок.
