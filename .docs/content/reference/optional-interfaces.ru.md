# Опциональные интерфейсы

Эти интерфейсы не нужны для базового drag & drop.
Они подключают дополнительные возможности, когда конкретный subsystem умеет их читать.

Обычно это один из двух сценариев:

- binding хочет участвовать в бизнес-логике переноса
- item adapter хочет отдать дополнительную метаинформацию для UI или стратегий

---

## Короткая карта

| Интерфейс | Где реализуется | Когда используется | Для чего нужен |
|---|---|---|---|
| `ITransferDomainHandler` | обычно на `InventoryDataBinding` | после rules/planning и прямо перед commit, а затем после успешного завершения операции | бизнес-валидация и side effects уровня переноса |
| `IAsyncTransferDomainHandler` | обычно на `InventoryDataBinding` | после sync `CanCommitTransfer` и перед commit | внешние async-проверки: сервер, файл, БД |
| `IStackSizeLimitable` | на `IItemAdapter` | когда стратегия считает лимит стака | per-item лимит стака |
| `IDescribable` | на `IItemAdapter` | когда UI хочет показать описание | дополнительная метаинформация для tooltip и похожих систем |

---

## ITransferDomainHandler

`ITransferDomainHandler` нужен для domain-логики вокруг уже спланированного переноса.
Это не замена rules и не ещё один generic validation layer.

Реализуется обычно на binding'е:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return HasEnoughMoney(context)
            ? RuleResult.Success()
            : RuleResult.Failure("Not enough money");
    }

    public void OnTransferSucceeded(TransferDomainContext context)
    {
        SpendMoney(context);
    }
}
```

### Точный порядок в конвейере

При обычном переносе порядок такой:

1. `CanDrop` и остальные rules проверяют, можно ли в принципе строить перенос.
2. planner строит `TransferPlan` без изменения инвентарей.
3. executor создаёт `TransferDomainContext` для конкретного planned allocation.
4. `CanCommitTransfer` вызывается на source binding, затем на target binding, если они реализуют `ITransferDomainHandler`.
5. если binding дополнительно реализует `IAsyncTransferDomainHandler`, после sync-проверки вызывается `CanCommitTransferAsync`.
6. только после этого выполняется реальный commit: split, conversion, placement, rollback при необходимости.
7. после успешного завершения всей операции вызывается `OnTransferSucceeded`.
8. только потом dispatch'атся inventory add/remove notifications и остальные deferred events.

То есть:

- `CanCommitTransfer` происходит позже rules
- `CanCommitTransfer` происходит раньше любых мутаций конкретного transfer allocation
- `OnTransferSucceeded` происходит уже после успешного commit, но раньше `OnItemRemoved` / `OnItemAdded`

### Что лежит в TransferDomainContext

`TransferDomainContext` даёт binding'у контекст именно transfer-level операции:

- `SourceInventory` / `TargetInventory`
- `SourceBinding` / `TargetBinding`
- `SourceSlot`
- `PlannedTargetSlot`
- `TargetSlot` после commit
- `SourceItemAdapter`
- `PreviewTargetItemAdapter`
- `TargetItemAdapter` после commit
- `RequestedAmount`
- `CommittedAmount`
- `Kind`
- `IsCommitted`

Это важно для сценариев, где бизнес-решение зависит не только от "какой item лежит в слоте", но и от смысла операции:

- покупка у торговца
- продажа предмета
- перенос между faction/container/authority boundaries
- проверка внешних ограничений перед commit

### Что сюда стоит класть

- проверку валюты
- проверку прав доступа
- валидацию у сервера
- side effects, которые не являются обычным sync данных

### Что сюда класть не стоит

- slot compatibility
- типовые inventory restrictions
- обычный sync `AddToData` / `RemoveFromData`
- UI preview-логику

Если вопрос звучит как "можно ли вообще класть такой предмет в этот слот?", это почти всегда rules.
Если вопрос звучит как "можно ли именно сейчас коммитить уже спланированную операцию?", это кандидат для `ITransferDomainHandler`.

---

## IAsyncTransferDomainHandler

`IAsyncTransferDomainHandler` дополняет `ITransferDomainHandler`, если ответ нельзя получить мгновенно.

```csharp
public class ServerInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>,
      ITransferDomainHandler,
      IAsyncTransferDomainHandler
{
    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return ValidateLocalState(context);
    }

    public async Task<RuleResult> CanCommitTransferAsync(
        TransferDomainContext context,
        CancellationToken cancellationToken)
    {
        return await _serverApi.ValidateTransferAsync(context, cancellationToken);
    }
}
```

Используйте его, когда нужно дождаться:

- ответа сервера
- файла или save-data
- базы данных
- внешнего профиля или authority layer

Важно:

- `CanCommitTransferAsync` не заменяет sync-версию, а идёт после неё
- если sync-проверка уже вернула отказ, async-стадия не запускается
- async-проверка вызывается перед локальным commit
- отказ на async-стадии отменяет перенос без мутации инвентарей

Если проверка чисто локальная и быстрая, достаточно обычного `CanCommitTransfer`.

---

## IStackSizeLimitable

`IStackSizeLimitable` позволяет item adapter'у задать собственный лимит стака:

```csharp
public class AmmoAdapter : IItemAdapter, IStackSizeLimitable
{
    public int MaxStackSize => 120;
}
```

Обычно это нужно для систем в стиле:

- Craft: разные типы предметов имеют разные stack caps
- RPG: зелья стакаются по 20, стрелы по 999, оружие по 1
- survival/crafting: контейнеры и инструменты не стакаются, ресурсы стакаются

### Где это реально учитывается

Интерфейс читает стратегия инвентаря при расчёте вместимости стака:

- `StackableItemStrategy`
- `SeparableStacksStrategy`
- planner через `UniversalInventory.GetMaxStackSizeForItem(...)`

### Важное уточнение про _allowItemStackOverride

В текущей реализации `IStackSizeLimitable` применяется только если в `UniversalInventory` включён `_allowItemStackOverride`.

Поведение сейчас такое:

- если `_allowItemStackOverride == false`, используется только inventory `_maxStackSize`
- если `_allowItemStackOverride == true` и item реализует `IStackSizeLimitable`, `MaxStackSize` предмета полностью заменяет inventory `_maxStackSize`

То есть в текущем коде это не "item может только поднять потолок".
Это полная подмена лимита предметом, и он может быть:

- меньше inventory лимита
- равен inventory лимиту
- больше inventory лимита

Примеры:

- inventory `_maxStackSize = 20`, `_allowItemStackOverride = false`, item `MaxStackSize = 99` -> фактический лимит всё равно `20`
- inventory `_maxStackSize = 20`, `_allowItemStackOverride = true`, item `MaxStackSize = 99` -> фактический лимит `99`
- inventory `_maxStackSize = 20`, `_allowItemStackOverride = true`, item `MaxStackSize = 5` -> фактический лимит `5`

Если вам нужна другая семантика, например "item может только уменьшать лимит" или "item может превышать потолок только вверх", это уже отдельная кастомизация стратегии.

---

## IDescribable

`IDescribable` не влияет на core transfer pipeline.
Это простой пример того, как можно доопределить `IItemAdapter` дополнительными данными для UI.

```csharp
public class ItemAdapter : IItemAdapter, IDescribable
{
    public string Description => _item.Description;
}
```

Стандартный use case в ассете сейчас один:

- `DefaultTooltipView` показывает `Description`, если adapter реализует `IDescribable`

Но смысл интерфейса шире:

- custom tooltip
- inspect panel
- hover card
- context menu details
- любая другая optional UI-система

То есть `IDescribable` стоит воспринимать не как "специальный обязательный интерфейс для тултипов",
а как паттерн расширения adapter'а маленькими дополнительными интерфейсами.

Если базового `IItemAdapter` уже недостаточно, вы можете добавлять такие интерфейсы под свой UI:

- `IDescribable`
- `IFilterable`
- `ISortable`
- любые свои `IItemStatsProvider`, `IRarityProvider`, `IFlavorTextProvider` и т.д.

Core inventory от них не зависит.
Их читают только те системы, которым они действительно нужны.
