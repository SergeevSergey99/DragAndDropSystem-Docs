# Cookbook: конвертация предметов

Эта страница отвечает на практический вопрос:
"как правильно настроить conversion между двумя инвентарями, если у них разные adapter-модели?"

Архитектурный обзор уже есть в [Конвейере переноса](transfer-pipeline.md).
Здесь собраны рабочие правила и типовые ошибки.

---

## Когда вообще нужен converter

Converter нужен, если два инвентаря используют разные представления одного и того же предмета.

Типичные примеры:

- торговец хранит `ScriptableObject`, игрок — runtime-модель
- UI-инвентарь хранит облегчённые adapters, а доменная система — rich instances
- контейнер внутри предмета использует другой adapter boundary

Если обе стороны уже используют один и тот же adapter-тип, converter обычно не нужен.

---

## Где живёт converter

Converter задаётся на стороне inventory binding:

```csharp
protected override IItemAdapterConverter CreateItemConverter()
{
    return new MyItemAdapterConverter();
}
```

То есть binding говорит:

- как этот инвентарь отдаёт предмет наружу
- как этот инвентарь принимает предмет внутрь

По умолчанию используется identity-converter.

---

## Кто вызывает conversion

### Preview

Во время preview конвертацию оркеструет `TransferItemConversionUtility`.

Это нужно, чтобы target rules и binding hooks видели уже target-side adapter, а не source-side adapter.

### Normal execution

При обычном переносе цепочка такая:

1. предмет забирается из source slot
2. выполняется `source outgoing`
3. затем `target incoming`
4. уже после этого предмет размещается в target inventory

### Swap

Swap нельзя воспринимать как одну симметричную операцию.
Это две независимые цепочки:

- `A -> B`
- `B -> A`

Обе проходят через свои `outgoing -> incoming`.

---

## Правильная ментальная модель

Не думайте в терминах "инвентарь отдаёт готовый target object".

Правильнее так:

```text
source adapter
  -> source outgoing
  -> промежуточное представление
  -> target incoming
  -> target adapter
```

Промежуточное представление не обязано быть отдельным типом.
Главное, что boundary source и boundary target независимы.

---

## Что должен сохранять adapter

Если у предметов есть instance-state, adapter обязан корректно переносить его через conversion:

- стабильный `ItemId`, если он участвует в stacking semantics
- runtime-поля конкретного экземпляра
- ссылку на доменную сущность, если предмет уникален
- данные, по которым потом работает `CanStartDrag`, `CanDrop`, tooltips и side effects

Если после переноса у вас "вроде тот же предмет, но потом ломается drag", это почти всегда значит, что target получил не тот adapter-type или не тот instance-state.

---

## Чего делать нельзя

### Нельзя использовать один adapter как representative для всего стека

Если внутри стека лежат разные runtime instances, нельзя "размножать" один adapter через `Repeat`.

Иначе:

- теряется instance-state
- preview и execution начинают расходиться
- remove/add hooks получают искажённые данные

### Нельзя делать raw stack exchange для cross-inventory swap

Если swap просто меняет местами два `ItemStack`, то:

- в target slot попадает чужой adapter-type
- следующий `CanStartDrag` или `CanDrop` начинает падать на типе

### Нельзя считать, что preview и execution делят один и тот же object reference

Preview stack и execution stack могут быть разными объектами.
Стабильность должна держаться на данных и conversion semantics, а не на reference equality.

---

## Когда converter должен возвращать `null`

`null` означает не "не хочу сейчас", а "этот boundary не может принять/отдать такой предмет".

Это уместно, если:

- предмет вообще не должен пересекать эту границу
- binding не умеет материализовать нужный target adapter
- потеря данных была бы недопустима

Если операция просто временно запрещена бизнес-логикой, это не задача converter.
Тогда используйте:

- rules
- `CanStartTransfer` / `CanStartTransferAsync`
- `CanCommitTransfer`

---

## Как диагностировать ошибки conversion

### Симптом: `Неверный тип предмета`

Обычно значит:

- target binding получил source adapter-type
- swap был выполнен как raw exchange
- preview конвертировался, а execution — нет

### Симптом: первый swap работает, второй ломается

Обычно значит:

- commit прошёл с неправильным adapter-type внутри slot
- после первого swap слот физически хранит объект чужого инвентаря

### Симптом: preview проходит, а commit нет

Обычно значит:

- preview stack был собран корректно
- а execution-путь использует другой conversion route

---

## Мини-чеклист для новых converter-ов

- binding действительно переопределяет `CreateItemConverter()`
- outgoing и incoming симметричны настолько, насколько этого требует доменная модель
- каждый adapter в стеке конвертируется индивидуально
- target после commit хранит adapter своего собственного inventory
- swap работает как две отдельные conversion-цепочки

---

## Где смотреть дальше

- [Пайплайн переноса](transfer-pipeline.md) — полный порядок переноса
- [Demo4 Trading](../examples/demo4-trading.md) — рабочий пример conversion между merchant/player/equipment
- [Troubleshooting](../reference/troubleshooting.md) — симптомы и типовые причины
