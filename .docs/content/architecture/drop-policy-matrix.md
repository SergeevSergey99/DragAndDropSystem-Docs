# Матрица Drop Policy

Эта страница нужна для быстрого ответа на вопрос:
"что именно сделает система в моей конкретной комбинации target slot, policy и состояния слота?"

Подробная архитектура описана в [Конвейере переноса](transfer-pipeline.md).
Здесь только прикладная матрица поведения.

---

## Короткая идея

Есть три главных переключателя:

- есть ли конкретный `target slot`
- пуст он или занят
- какой `BlockedTargetBehavior` выбран

Дополнительно влияют:

- `AllowPartial`
- `BatchMode`
- same-inventory это перенос или cross-inventory

---

## Базовая матрица

| Сценарий | `Reject` | `Swap` | `FindAlternative` |
|---|---|---|---|
| `target slot`, слот пуст и подходит | положить в этот слот | положить в этот слот | положить в этот слот |
| `target slot`, слот занят, но merge/placement возможен | положить в этот слот | положить в этот слот | положить в этот слот |
| `target slot`, слот занят и placement невозможен | отказ, если `occupied handler` не перехватил | сначала `occupied handler`, потом swap | сначала `occupied handler`, потом поиск других слотов |
| `target slot`, слот не подходит по rules | отказ | отказ или swap, если это именно occupied target и правила для swap в обе стороны проходят | можно искать другой слот |
| area-drop без конкретного слота | inventory-wide поиск подходящего места | обычно ведёт себя как обычный inventory search, а не как slot-to-slot swap | inventory-wide поиск подходящего места |

---

## Что важно помнить про direct slot drop

Если операция уже идёт в конкретный `target slot`:

- при `Reject` и `Swap` planner не должен сканировать остальные слоты инвентаря
- поиск по всему inventory допустим только для `FindAlternative`
- executor для concrete `targetSlot` не должен повторно делать inventory-wide `GetAcceptableCount()`

Это критично для fixed-slot inventory, например экипировки.
Иначе в логах появляются ложные проверки соседних слотов.

---

## Occupied slot: реальный порядок

Когда целевой слот занят и обычное размещение в него не удалось:

1. planner сначала проверяет `DataBinding.CanHandleOccupiedSlotDrop(...)`
2. если binding говорит "я сам обработаю" -> строится `RequiresOccupiedHandler`
3. если binding не перехватывает:
   - `Reject` -> отказ
   - `Swap` -> строится `RequiresSwap`
   - `FindAlternative` -> стратегия перечисляет другие candidate slots

То есть `occupied handler` имеет приоритет над swap и над поиском альтернатив.

---

## Partial transfer

`AllowPartial` влияет только на количество, но не меняет сам выбор ветки:

- если в target вошло всё -> обычный успех
- если вошла часть и `AllowPartial = false` -> отказ
- если вошла часть и `AllowPartial = true`:
  - при `FindAlternative` остаток может искать другие слоты
  - при `Reject` и `Swap` остаток не должен уходить в inventory-wide search

---

## Same-inventory vs cross-inventory

### Same-inventory

- `FindAlternative` не должен "перераскладывать" предметы по всему инвентарю как сортировка
- если target не подошёл, предмет остаётся на месте
- swap поддерживается только для одиночного entry и полного source stack

### Cross-inventory

- conversion может менять тип adapter'а между границами инвентарей
- swap не должен быть raw exchange стеков
- оба направления должны проходить через conversion отдельно

---

## Batch drag

Для batch drag важно разделять две вещи:

- `BlockedTargetBehavior`
- `BatchMode`

`BatchMode` отвечает на вопрос "что делать, если один из entries не прошёл":

- `Atomic` -> всё или ничего
- `BestEffort` -> переносим то, что получилось

При этом swap не является общим batch-mode.
Текущая реализация swap ориентирована на одиночный entry и полный source stack.

---

## Практические примеры

### Fixed equipment slot

Условия:

- есть конкретный slot
- слот занят
- `BlockedTargetBehavior = Swap`

Ожидаемое поведение:

- planner работает только с этим слотом
- не проверяет соседние weapon/armor/artifact slots
- если обычное размещение невозможно и `occupied handler` не забирает дроп, строится swap

### Inventory area

Условия:

- target slot не указан

Ожидаемое поведение:

- допустим inventory-wide search
- допустим `GetAcceptableCount()`
- стратегия сама решает, merge first или empty first

---

## Что смотреть, если поведение неожиданное

- [Конвейер переноса](transfer-pipeline.md) — общий порядок фаз
- [Логи и отладка](../reference/logs-and-debugging.md) — как читать planner/executor/rules логи
- [Troubleshooting](../reference/troubleshooting.md) — типовые симптомы и причины
