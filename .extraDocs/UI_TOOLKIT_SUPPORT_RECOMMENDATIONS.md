# UI Toolkit Support Recommendations

**Last Updated**: 2026-07-25
**Status**: актуализировано по текущему runtime-коду и policy-driven JIT transfer architecture;
Этап 0 частично выполнен (см. раздел ниже)

## Короткий вывод

Поддержка UI Toolkit реалистична, но это отдельный UI backend, а не локальная замена
`Image` на `VisualElement`.

Рекомендуемый путь:

1. Не менять transfer semantics ради UI Toolkit.
2. Сначала сделать ограниченный vertical slice поверх существующих `DragAndDropManager`,
   `DragContext` и `InventoryDropProcessor`.
3. По результатам spike выделить framework-neutral slot identity, interaction event и view geometry.
4. Сохранить uGUI как отдельный adapter и добавить UI Toolkit adapter.
5. Разделять assembly только вместе с продуманной миграцией публичного API.

Полный рефакторинг `BaseSlot` до первого работающего прототипа не рекомендуется: сейчас этот тип
является частью почти всех inventory, placement и transfer contracts, поэтому преждевременная
массовая замена создаст большой риск регрессий и не подтвердит, что выбран правильный UI seam.

## Архитектурная база, которую нужно сохранить

Текущий transfer pipeline уже достаточно отделён от конкретной отрисовки:

1. `DropRequestPolicy`, `DropPolicySettings` и `ResolvedDropPolicy` разрешают поведение drop.
2. `InventoryDropProcessor` является границей inventory drop.
3. `InventoryTransferService` обрабатывает entries последовательно и just-in-time против текущего
   состояния inventory.
4. `IStrategy.TryGetCandidate(...)` проверяет explicit target.
5. `IStrategy.GetCandidates(...)` и `PlacementCandidateOrderer` используются только для
   автоматического размещения.
6. `InventoryAcceptanceRequest` и `TransferItemConversionUtility` обеспечивают target-aware preview
   до mutation.
7. Swap исполняется в том же pipeline, что и обычный transfer.
8. Rollback ограничен текущим неуспешным entry; batch остаётся best-effort.
9. `IAsyncTransferDomainHandler` вызывается один раз до любых mutation через async execution path.
10. `IInventoryTopology` владеет footprint projection, orientation count, rotation, visual angle и
    преобразованием grab offset.

В архитектуре больше нет актуального `TransferPlanner` / `TransferPlanExecutor` split и не должно
появляться materialized transfer plan или virtual inventory state. UI Toolkit backend обязан
использовать существующий JIT pipeline, а не строить параллельный resolver/executor.

Ключевые файлы для сверки:

- `Scripts/DragAndDropManager.cs`
- `Scripts/Core/Models/DragContext.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/InventoryTransferEngine.cs`
- `Scripts/Inventories/InventoryTransferService.cs`
- `Scripts/Inventories/Strategies/IStrategy.cs`
- `Scripts/Inventories/IPlacementInventory.cs`
- `Scripts/Core/Models/InventoryTopology.cs`

## Что уже пригодно для второго UI backend

### Drag lifecycle и drop routing

`DragAndDropManager` уже хранит `DragContext`, активный `IDropTarget` и stack drop targets. Он умеет:

- начинать single и batch drag;
- менять topology-defined orientation через `RotateCurrentDrag(...)`;
- завершать и отменять drag;
- выполнять split drop;
- маршрутизировать inventory drop через `InventoryDropProcessor`.

UI Toolkit adapter должен вызывать этот lifecycle, а не заводить собственное drag state.

### Transfer boundary

`InventoryDropProcessor` уже принимает target slot или inventory area и возвращает advisory
`TransferProbe` для preview. Его async метод `ProcessDropWithReportAsync(...)` сохраняет
transfer-wide domain validation.

Важно: UITK drop completion не должен напрямую вызывать синхронный `ProcessDrop(...)`, если
операция может иметь `IAsyncTransferDomainHandler`. Безопасный путь — завершать drag через
`DragAndDropManager`, как это делает текущий uGUI flow.

### Placement и shaped items

Shaped placement уже является текущей возможностью, а не будущим расширением. UI backend не должен
сам вычислять footprint или предполагать четыре поворота по 90 градусов. Он получает orientation и
covered slots/cells через активную topology и существующий preview pipeline.

### Selection state

`SelectionManager` и `SelectionContext` отделяют состояние selection от его отображения, хотя
по-прежнему используют `BaseSlot` как identity. UITK понадобится собственный visual presenter,
но отдельный selection algorithm не нужен.

## Реальные блокеры в текущем коде

### 1. `BaseSlot` одновременно domain identity и uGUI scene component

`BaseSlot : MonoBehaviour, ISlot` сейчас одновременно:

- идентифицирует слот по `Index` и `Inventory`;
- предоставляет stack и slot rules;
- участвует в `DragEntry`, `DragContext`, `DropResult`, transfer requests и events;
- используется placement topology и strategies;
- управляет visual hooks (`UpdateVisuals`, dragged/highlight state);
- обнаруживается через `GetComponent`;
- существует как scene/prefab component.

`VisualElement` не может наследоваться от `MonoBehaviour`, поэтому полноценный UITK slot не может
быть `BaseSlot`.

Существующий `ISlot` нельзя считать готовым решением. Это узкий read-only contract, используемый
filter/sorter rules. Согласно текущим design rules slot-domain code остаётся на `BaseSlot`, а
`ISlot` не является общей mutable slot identity. Его расширение ради UITK было бы скрытым breaking
change. Для разделения потребуется новый, явно названный контракт или model type.

### 2. `IInventory` и runtime capabilities завязаны на `BaseSlot`

`IInventory.Slots`, `GetSlot`, mutation methods, `IPlacementInventory`,
`IDynamicSlotLifecycle`, `IInventoryEventSink` и acceptance APIs используют `BaseSlot`.
Сюда же относится `IInventoryRuntimeCapabilities.ExecuteOccupiedSlotDrop(DragEntry, BaseSlot)`.
Следовательно, простое добавление `IInventorySlotModel` затронет большой публичный API.

`UniversalInventory` дополнительно:

- ищет slots через `GetComponentsInChildren<BaseSlot>()`;
- хранит `List<BaseSlot>`;
- создаёт slot prefab через `Instantiate`;
- exposes `Transform SlotContainer`.

Для production UITK backend создание domain slots и создание visual elements должны стать разными
lifecycle, при этом view/layout не должны владеть item mutation.

### 3. Interaction pipeline привязан к uGUI

Текущий путь:

`EventSystem -> SlotInputAdapter -> InputEventRouter -> binding/action -> DragAndDropManager`

Зависимости:

- `SlotInputAdapter : Selectable`;
- `PointerEventData` и `PointerEventData.InputButton`;
- `Selectable.allSelectablesArray`;
- `EventSystem.currentSelectedGameObject`;
- `GameObject`, `RectTransform` и `InventoryDropArea` внутри interaction state.

`RuntimeInteractionSnapshot` уже является полезной границей для actions, но всё ещё содержит
`BaseSlot`, `DropAreaBase` и `PointerEventData`. Поэтому его нужно эволюционировать, а не создавать
второй UITK-only action contract.

### 4. `IDropTarget` частично framework-neutral

Сам интерфейс не наследуется от Unity UI типов, но `GetTargetSlot()` возвращает `BaseSlot`, а
`DragAndDropManager.ActivateDropTargetForSlot(...)` ищет target через
`targetBaseSlot.GetComponent<IDropTarget>()`.

Ручные `PushDropTarget(...)` / `PopDropTarget(...)` позволяют сделать UITK bridge, однако
production API должен отделить target identity от способа поиска view component.

### 5. Presentation layer является uGUI-specific

Drag visual, tooltip, context menu, selection feedback, drop preview overlay и drop areas используют
`Canvas`, `RectTransform`, `Graphic`, `Selectable`, `Image` и TextMeshProUGUI. Для UI Toolkit нужны
отдельные presenters поверх `VisualElement`.

Не стоит объединять drag visual, tooltip и context menu в один крупный
`IInventoryOverlayPresenter`: у них разные lifecycle, input ownership и правила позиционирования.
Предпочтительны небольшие отдельные contracts.

### 6. Runtime assembly напрямую зависит от uGUI

`Scripts/DragAndDropSystem.Runtime.asmdef` содержит reference на `Unity.ugui`. Одних C# interfaces
недостаточно, чтобы получить framework-neutral package boundary.

Целевое направление:

- neutral core/domain assembly;
- uGUI adapter assembly;
- UI Toolkit adapter assembly;
- compatibility facade или migration layer для существующего public API.

Физическое разделение assembly не следует делать первым коммитом: пока contracts принимают
`BaseSlot`, оно приведёт к циклическим зависимостям или массовому breaking change.

## Рекомендуемая стратегия

Основная стратегия — refactor + два view adapters, но через vertical slice перед широким изменением
API.

### Почему не чистый быстрый bridge

Bridge поверх текущего `BaseSlot` пригоден для spike, но как финальная реализация он сохранит
двойную identity (`VisualElement <-> hidden BaseSlot`) и заставит UITK подстраиваться под
`PointerEventData` и GameObject focus.

### Почему не полный redesign сразу

Полное разделение inventory data model, slots и views затронет transfer, DataBinding, placement,
selection, events, examples и пользовательские extensions. Без работающего vertical slice трудно
понять, какие abstractions действительно нужны.

### Выбранный компромисс

1. Временно использовать существующий `BaseSlot` как domain identity в spike.
2. Связать его с `VisualElement` через registry/bridge.
3. Маршрутизировать drag и drop через существующий manager и processor.
4. После проверки сценариев выделить минимальные neutral contracts.
5. Перевести uGUI и UITK на эти contracts постепенно.

Временный hidden/headless `BaseSlot` допустим только как исследовательский scaffolding. Он не должен
становиться обещанным production API.

## Scope первого vertical slice

Поддерживается:

- pointer enter/leave/down/up;
- single-slot drag;
- drop на explicit target slot;
- area drop;
- drag visual;
- basic hover и accept/reject preview;
- текущая drop policy;
- same-inventory move;
- cross-inventory move;
- swap;
- async transfer-wide veto;
- topology-defined rotation;
- shaped placement preview и execution;
- best-effort batch execution как минимум на уровне domain integration test.

Временно не поддерживается:

- полная keyboard/gamepad navigation parity;
- UITK context menu parity;
- tooltip parity;
- virtualization большого списка;
- drag между uGUI и UITK в одной сцене;
- сохранение UITK view state как часть inventory snapshot.

Если shaped items исключаются из первого пользовательского релиза, это должно быть явным
ограничением backend. Но adapter всё равно не должен вводить grid-specific flags или предполагать
четыре orientation steps.

## Поэтапный план

### Этап 0. Зафиксировать compatibility contract и тестовый baseline

Перед изменениями:

- перечислить public interaction и slot APIs, которые считаются стабильными;
- определить поддерживаемые версии Unity/UI Toolkit;
- зафиксировать feature matrix первого релиза;
- добавить regression tests для pointer phases, focused/hovered/pressed resolution и drag
  completion;
- сохранить существующие transfer, swap, async и shaped placement tests как обязательный baseline.

Результат: известен допустимый размер breaking changes и есть защита текущего uGUI поведения.

#### Что уже сделано

Исходный baseline (256 тестов) плотно закрывал transfer, placement и policy, но на границе,
которую UI Toolkit будет заменять, покрытия почти не было: `CompleteDrag`, `PushDropTarget`,
`PopDropTarget`, `IDropTarget`, `DropAreaBase`/`InventoryDropArea`, `RotateCurrentDrag` и
`TransferProbe` не упоминались ни в одном тесте.

Добавлено:

- `Tests/Editor/Core/DragLifecycleTests.cs` — completion, cancel, повторное завершение,
  async veto через manager, topology-defined rotation, уничтоженные drop targets;
- `Tests/Editor/Core/DropTargetStackTests.cs` — push/pop/дубликаты/вложенность и порядок
  drag enter/exit;
- `Tests/Editor/Core/DropAreaRoutingTests.cs` — area drop на границе UI: enter/exit, отказ,
  detach, slot-поверх-area, dynamic slots;
- `Tests/Editor/Inventories/TransferProbeTests.cs` — advisory-контракт preview;
- `Tests/Runtime/FakeDropTarget.cs`, `Tests/Runtime/FakeDropTargetBehaviour.cs` — тестовые
  реализации `IDropTarget`.

#### Найденный и исправленный баг

`EndDrag` вызывал `OnBecomeInactiveTarget()` на всех элементах стека без проверки живости.
Уничтоженный drop target бросал `MissingReferenceException` до `_dropTargetStack.Clear()` и
`_currentContext = null`, а поскольку `EndDrag` вызывается из `finally` в `CompleteDragAsync`,
менеджер оставался с залипшим `IsDragging` навсегда — все последующие `StartDrag` отклонялись.

В uGUI это маскировалось тем, что `DropAreaBase.OnDisable` попает себя, но сценарий
`Destroy(gameObject)` инвентаря во время drag оставался дырявым. Для UI Toolkit прикрытия нет
вовсе: у `VisualElement` нет `OnDisable`.

Исправлено в `DragAndDropManager` — `IsTargetAlive`/`PruneDeadDropTargets` плюс защита цикла в
`EndDrag`. Стек хранит `IDropTarget`, поэтому Unity-овская перегрузка `==` сама не срабатывает и
проверка обязана быть явной. Это же требование распространяется на любой будущий адаптер.

#### Что в Этапе 0 осталось

- **Завести PlayMode test assembly.** Вся текущая тестовая сборка — `includePlatforms: ["Editor"]`.
  UITK-панель и `PointerEventBase` в EditMode не поднимаются, поэтому сборка понадобится уже в
  Этапе 1; заводить её в середине vertical slice дороже, чем сейчас.
- **Учесть ловушку `[ExecuteAlways]`.** В EditMode Unity не вызывает `Awake`/`OnEnable`/`OnDisable`
  для обычных `MonoBehaviour`. `DropAreaBase` получает их только потому, что `Selectable` помечен
  этим атрибутом. UITK drop target наследовать `Selectable` не будет, поэтому любой EditMode-тест
  его detach-логики обязан явно ставить `[ExecuteAlways]` — иначе тест зелёный и не проверяет
  ничего. По этой же причине `InventoryBuilder` вызывает `Start` рефлексией.
- Перечислить стабильные public APIs, зафиксировать версии Unity и feature matrix — не начато.

### Этап 1. Сделать experimental UITK vertical slice

Добавить экспериментальные компоненты:

- UITK inventory root/bridge;
- registry `VisualElement <-> BaseSlot`;
- UITK slot view;
- UITK pointer adapter;
- UITK drag visual;
- UITK drop target adapter.

Adapter вручную вызывает `PushDropTarget` / `PopDropTarget`, `StartDrag`, `CompleteDrag` и
`CancelDrag`. Inventory drop создаётся существующим `InventoryDropProcessor`; transfer logic не
дублируется.

Результат: проверена жизнеспособность UI Toolkit event flow и собраны реальные требования к
neutral contracts.

### Этап 2. Выделить framework-neutral interaction event

Нужна neutral model как минимум с:

- pointer id и button;
- pointer phase;
- screen/panel position;
- modifier state;
- primary-pointer flag;
- consumed/handled semantics;
- optional native payload только на adapter boundary.

`PointerBinding` и actions должны сопоставлять neutral event. uGUI adapter преобразует
`PointerEventData`, UITK adapter — `PointerEventBase`/конкретные UITK events.

`RuntimeInteractionSnapshot` сохраняется как единый action snapshot, но перестаёт требовать uGUI
types для pointer-driven actions.

### Этап 3. Разделить slot identity и slot view

Не расширять существующий `ISlot` автоматически. Спроектировать отдельный минимальный contract,
например `IInventorySlotHandle` или immutable slot id + inventory lookup.

Contract должен покрывать только реально нужную domain identity:

- inventory ownership;
- stable index/id;
- получение read-only stack/rules через inventory API;
- equality/lifetime semantics;
- dynamic slot creation/removal.

View contract хранится отдельно и предоставляет:

- visibility/attachment state;
- geometry anchor;
- visual state application;
- focus request;
- framework-specific object только внутри adapter.

`BaseSlot` остаётся uGUI compatibility implementation. Core APIs переводятся постепенно, начиная с
новых contracts и boundary types, а не механической заменой всех сигнатур.

### Этап 4. Ввести slot registry и framework-neutral lifecycle

`UniversalInventory` не должен создавать UITK elements через domain mutation. Нужны:

- slot identity registry;
- lifecycle notifications о create/remove/reindex;
- uGUI prefab factory;
- UITK element factory;
- layout reaction без ownership item mutation.

Динамические slots должны по-прежнему создаваться transfer pipeline через runtime capability.
View только отражает результат lifecycle.

### Этап 5. Выделить view geometry и focus services

Одного `Vector2 ScreenPosition` недостаточно. Contract должен учитывать:

- panel/world/local coordinates;
- panel scale;
- attachment к конкретному `Panel`/`UIDocument`;
- bounds, а не только одну anchor point;
- отсутствующий visual element при virtualization;
- conversion в координаты overlay presenter.

Focus/navigation service заменяет предположение о `Selectable.allSelectablesArray` и
`EventSystem.currentSelectedGameObject`. uGUI и UITK предоставляют разные реализации.

### Этап 6. Production UITK inventory и placement preview

Реализовать:

- fixed и dynamic slot views;
- binding slot identity -> `VisualElement`;
- hover/pressed/focused/selected states;
- preview всех covered cells;
- invalid/out-of-bounds preview;
- topology-defined visual rotation;
- grab-offset-aware target anchor;
- area drop.

UI layer использует `TransferProbe`, `TryGetDropPreviewSlots` или их будущий neutral equivalent и
не резервирует состояние inventory.

### Этап 7. Разделить assemblies

После стабилизации contracts:

- вынести uGUI-specific types из neutral assembly;
- создать uGUI adapter assembly;
- создать UI Toolkit adapter assembly;
- обновить examples и tests;
- предоставить migration notes для extensions, наследующихся от `BaseSlot`, `SlotInputAdapter` или
  `DropAreaBase`.

### Этап 8. Довести UX parity

В рекомендуемом порядке:

1. selection и batch visual;
2. keyboard/gamepad navigation;
3. tooltip;
4. context menu;
5. quick actions и active inventory switching;
6. virtualization;
7. hybrid uGUI/UITK drag, только если подтверждён реальный спрос.

## Правила реализации

### Нельзя

- добавлять `if (uGUI/UITK)` по всему `InputEventRouter` и presentation code;
- создавать UITK-only transfer pipeline;
- precompute batch transfer plan;
- валидировать explicit target через enumeration всех candidates;
- обходить `InventoryDropProcessor` или async domain veto;
- мутировать inventory из layout/view;
- вычислять shaped footprint в UI;
- считать orientation углом, кратным 90 градусам;
- делать `GridTopology` или `is grid` частью общего inventory/view contract;
- использовать существующий filter/sorter `ISlot` как новый mutable domain API без миграции.

### Нужно

- держать `DragContext` единственным drag state;
- сохранять policy resolution;
- использовать JIT validation против текущего состояния;
- сохранять swap в основном pipeline;
- сохранять per-entry rollback и best-effort batch;
- выполнять target-aware preview conversion до mutation;
- позволять topology владеть projection и orientation;
- держать UI target тонким;
- отделять slot identity, view geometry, input adapter и presenters.

## Минимальная тестовая матрица

### Interaction

- pointer enter/leave не оставляет stale active target;
- press на одном slot и release на другом разрешаются предсказуемо;
- cancel/capture loss завершает drag корректно;
- повторный pointer не крадёт primary drag;
- disabled/detached `VisualElement` не остаётся target;
- focus и hover не смешиваются.

### Transfer invariants

- explicit target проходит через `TryGetCandidate`;
- batch entries видят mutations предыдущих entries;
- failed entry откатывается без отката успешных entries;
- swap проверяет обе стороны и остаётся в основном pipeline;
- async veto вызывается один раз до mutation;
- sync UITK callback не обходит async handler;
- dynamic area drop создаёт slot только во время execution.

### Placement

- single-cell и shaped items;
- grab offset;
- rotation с 4-step topology;
- custom topology с 6 orientation steps;
- partially out-of-bounds preview;
- occupied covered cell;
- same-inventory relocation;
- preview conversion отличается от source adapter, если DataBinding это требует.

### Compatibility

- существующие uGUI examples компилируются и работают;
- public bindings/actions продолжают загружаться из serialized assets;
- selection, auto-transfer, tooltip и context menu uGUI не получают regressions;
- assembly references не создают cycle;
- UITK backend может быть исключён без поломки uGUI runtime.

## Оценка сложности

Оценки зависят от требований к обратной совместимости и поддерживаемым версиям Unity:

- experimental pointer vertical slice: **1–3 недели**;
- neutral interaction contracts и два adapters: **2–4 недели**;
- slot identity/lifecycle migration: **3–6+ недель**;
- shaped preview, dynamic slots и production hardening: **2–4 недели**;
- navigation, tooltip и context menu parity: **3–6 недель**;
- assembly split, migration и полная regression pass: **2–4 недели**.

Рабочий экспериментальный backend можно получить сравнительно быстро. Production parity с
сохранением текущего API — крупное архитектурное расширение ориентировочно на **8–16+ недель**, а
не небольшая интеграция.

## Критерии готовности первого релиза

UI Toolkit support можно считать готовой к экспериментальному релизу, если:

- UITK adapter не содержит собственной transfer semantics;
- все inventory drops завершаются через безопасный manager/async path;
- policy, swap, rollback и partial transfer совпадают с uGUI;
- shaped preview использует topology-owned projection;
- нет предположения о четырёх orientation steps;
- view не мутирует inventory;
- lifecycle attach/detach не оставляет stale targets;
- существующий uGUI test baseline проходит;
- ограничения navigation, hybrid scenes и virtualization явно документированы.

## Итоговая рекомендация

UI Toolkit следует развивать как второй adapter/view backend над существующим policy-driven JIT
transfer core.

Первый практический шаг — не массовый `BaseSlot -> interface` refactor, а тестируемый vertical slice,
который использует текущие manager, context, processor, topology и async execution. После него нужно
последовательно отделить interaction event, slot identity, slot lifecycle, view geometry и focus
service, сохранив `BaseSlot` как совместимую uGUI implementation.

Такой порядок минимизирует архитектурные догадки, не создаёт параллельный transfer pipeline и даёт
реальный путь к независимым uGUI/UI Toolkit assemblies.
