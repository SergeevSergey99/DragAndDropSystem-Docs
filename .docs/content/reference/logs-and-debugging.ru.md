# Логи и отладка

Эта страница помогает быстро понять, в какой фазе конвейера произошёл отказ.

Главная идея:

- `planner` отвечает за выбор допустимого плана
- `executor` отвечает за commit, conversion и rollback
- `rules` отвечают за механические ограничения
- `domain hooks` отвечают за бизнес-veto перед commit

---

## Короткая карта логов

| Где появился лог | Что это обычно значит |
|---|---|
| `RuleResult` | отказ конкретной rule-проверки |
| `InventoryTransferService` | проблема planning или выбора target |
| `InventoryDropProcessor` | planner не смог построить валидный план |
| `InventoryTransferService` | проблема commit, conversion, swap или rollback |
| `GetAcceptableCount` | inventory-wide search по слотам |
| `CanCommitTransfer` / domain validation | бизнес-логика запретила commit |

---

## Как читать типовые логи

### `[RuleResult] Validation failed: ...`

Это лог отказа одной конкретной rule-ветки.

Важно:

- сам по себе он ещё не гарантирует баг
- иногда это нормальный отказ пробного candidate slot
- смотреть надо на стек вызовов: кто именно запустил эту проверку

Если лог идёт из:

- `MappedSlotInventoryDataBinding.CanDrop()` -> обычно проблема типа adapter или slot compatibility
- `CanStartDrag()` -> в source slot лежит не тот adapter-type или drag запрещён логикой binding

### `[InventoryDropProcessor] plan failed: ...`

Planner не построил валидный план.
До execution дело не дошло.

Чаще всего причины:

- target slot не подходит
- policy не позволяет fallback
- нет допустимого candidate slot

### `[InventoryTransferService] ...`

Это уже execution-stage.
Значит planning прошёл, но проблема возникла при:

- domain validation
- split/remove
- outgoing/incoming conversion
- placement в target inventory
- swap commit
- rollback

### `[InventoryName] GetAcceptableCount: ...`

Это inventory-wide acceptance search.

Если ты ожидал direct slot drop, а видишь этот лог, почти всегда стоит проверить:

- действительно ли был concrete `targetSlot`
- не ушла ли операция в `FindAlternative`
- не срабатывает ли area-drop path

---

## Быстрая диагностика по фазам

### 1. Drag start

Смотреть:

- `OnDragAttempting`
- `ValidateStartDrag`
- binding `CanStartDrag`

Типовые причины:

- source slot пуст
- slot содержит не тот adapter-type
- source binding запрещает drag

### 2. Preview / planning

Смотреть:

- `InventoryTransferService`
- `ValidateDrop`
- `InventoryAcceptanceRequest`
- `GetAcceptableCount`

Типовые причины:

- target-side conversion не сработал
- slot rules отклоняют target adapter
- planner ищет candidates шире, чем ты ожидал

### 3. Domain validation

Смотреть:

- `CanCommitTransfer`
- `CanCommitTransferAsync`
- `ValidateDomainHandlers`

Типовые причины:

- деньги
- права доступа
- серверный veto
- внешняя синхронная/асинхронная проверка

### 4. Execution

Смотреть:

- `InventoryTransferService`
- conversion utility
- `TryAddToSlot` / `TryAddStack`

Типовые причины:

- conversion failed during commit
- placement не удался
- rollback вернул исходное состояние

### 5. Swap

Смотреть:

- `RequiresSwap`
- `ValidateSwapRules`
- `OnSwapAttempting`
- `OnSwapCompleted`

Типовые причины:

- один из направлений swap не проходит rules
- swap сделан как raw exchange, а не conversion-aware commit
- после первого swap в slot остался чужой adapter-type

---

## Практические паттерны

### Preview прошёл, commit упал

Значит проблема не в rules, а в execution или domain hooks.

Ищи в:

- `CanCommitTransfer`
- `CanCommitTransferAsync`
- executor conversion
- placement / rollback

### Сыпятся warning-и по другим слотам

Значит где-то пошёл inventory-wide search.

Ищи в:

- `GetAcceptableCount`
- `FindAlternative`
- area-drop
- неправильный route для direct slot drop

### Первый swap успешен, второй ломается

Почти всегда это означает, что после первого swap slot хранит не свой adapter-type.

---

## Что смотреть вместе с этой страницей

- [Troubleshooting](troubleshooting.md) — симптом -> причина -> куда смотреть
- [Конвейер переноса](../architecture/transfer-pipeline.md) — порядок фаз
- [Cookbook: конвертация предметов](../architecture/item-conversion-cookbook.md) — если проблема в adapter boundary
