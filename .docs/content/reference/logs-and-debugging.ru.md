# Логи и отладка

Эта страница помогает быстро понять, в какой фазе конвейера произошёл отказ.

Главная идея:

- `InventoryTransferService` валидирует кандидатов по текущему состоянию и коммитит по одной записи
- `rules` отвечают за механические ограничения
- `domain hooks` могут наложить вето на весь перенос до обработки записей

---

## Короткая карта логов

| Где появился лог | Что это обычно значит |
|---|---|
| `RuleResult` | отказ конкретной rule-проверки |
| `InventoryTransferService` | проблема выбора target, conversion, placement, swap или rollback |
| `InventoryDropProcessor` | разрешение policy или отказ переноса |
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

### `[InventoryDropProcessor] ...`

Drop отклонён до того, как зафиксировалась запись.

Чаще всего причины:

- target slot не подходит
- policy не позволяет fallback
- нет допустимого candidate slot

### `[InventoryTransferService] ...`

Это уже execution-stage.
Значит preview прошёл, но проблема возникла при:

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

### 2. Preview / разрешение кандидатов

Смотреть:

- `InventoryTransferService`
- `ValidateDrop`
- `InventoryAcceptanceRequest`
- `GetAcceptableCount`

Типовые причины:

- target-side conversion не сработал
- slot rules отклоняют target adapter
- движок переноса ищет candidates шире, чем ты ожидал

### 3. Domain validation

Смотреть:

- `CanStartTransfer` / `CanStartTransferAsync`
- `CanCommitTransfer`
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
- `TryAddStack` / примитивы мутации placement

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

- `CanStartTransfer` / `CanStartTransferAsync`
- `CanCommitTransfer`
- conversion
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
