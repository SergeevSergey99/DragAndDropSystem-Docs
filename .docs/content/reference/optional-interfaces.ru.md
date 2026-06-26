# Опциональные интерфейсы

Эти интерфейсы не нужны для базового drag & drop. Они подключаются, когда вам нужно добавить поведение поверх обычного переноса.

## Что выбрать

| Если нужно... | Используйте |
|---|---|
| Запретить перенос из-за денег, прав доступа, владельца предмета или состояния магазина | `ITransferDomainHandler` |
| Спросить сервер или другую внешнюю систему перед переносом | `IAsyncTransferDomainHandler` |
| Сделать особое поведение при drop на занятый слот: вложить предмет в контейнер, надеть предмет, открыть предмет | `IPreRuleOccupiedSlotDropHandler` или `IPostRuleOccupiedSlotDropHandler` |
| Задать разный лимит стека для разных предметов | `IStackSizeLimitable` |
| Показать описание предмета в tooltip или другом UI | `IDescribable` |

## ITransferDomainHandler

Используйте `ITransferDomainHandler`, когда решение зависит не только от слота и предмета, а от игровой логики.

Примеры:

- хватает ли золота для покупки
- можно ли продавать этот предмет
- принадлежит ли предмет игроку
- открыт ли магазин
- можно ли переносить предмет между этими контейнерами

Обычно интерфейс реализуют на DataBinding:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
    {
        return _shopIsOpen ? RuleResult.Success() : RuleResult.Failure("Shop is closed");
    }

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

Методы:

| Метод | Когда вызывается | Для чего |
|---|---|---|
| `CanStartTransfer` | один раз перед переносом | запретить всю операцию целиком |
| `CanCommitTransfer` | перед фиксацией конкретного размещения | проверить деньги, права, владельца и похожую логику |
| `OnTransferSucceeded` | после успешного размещения | списать деньги, отправить аналитику, обновить внешнюю систему |

Не кладите сюда обычную проверку “можно ли этот тип предмета в этот слот”. Для этого лучше подходят rules, `CanDrop` или fixed-slot настройки.

## IAsyncTransferDomainHandler

`IAsyncTransferDomainHandler` нужен, когда перед переносом нужно дождаться внешнего ответа.

Например:

- сервер подтверждает перенос
- save-data проверяется с диска
- внешняя система проверяет права доступа

```csharp
public async Task<RuleResult> CanStartTransferAsync(
    DragContext context,
    IInventory targetInventory,
    CancellationToken cancellationToken)
{
    return await _serverApi.ValidateTransferAsync(context, cancellationToken);
}
```

Важные правила:

- проверка относится ко всему переносу
- она выполняется до изменения инвентарей
- если проверка отказала, перенос не начинается
- если проверка быстрая и локальная, обычно достаточно `ITransferDomainHandler`

## IOccupiedSlotDropHandler

`IOccupiedSlotDropHandler` нужен, когда drop на занятый слот должен означать не обычный swap или поиск другого места, а ваше действие.

Примеры:

- бросить предмет на сумку, чтобы положить его внутрь
- бросить предмет на экипированный слот, чтобы выполнить особую замену
- бросить ключ на контейнер, чтобы открыть его

Реализуйте не базовый `IOccupiedSlotDropHandler`, а один из timing-интерфейсов:

| Интерфейс | Когда вызывается | Для чего |
|---|---|---|
| `IPreRuleOccupiedSlotDropHandler` | до drop-правил целевого слота | drop фактически адресован объекту внутри слота, например контейнеру |
| `IPostRuleOccupiedSlotDropHandler` | после drop-правил целевого слота | обычные правила цели должны сначала разрешить drop |

Обычно интерфейс реализуют на DataBinding целевого инвентаря:

```csharp
public class ContainerInventoryBinding
    : SlotIndexedInventoryDataBinding<ItemModel, ItemModelAdapter>,
      IPreRuleOccupiedSlotDropHandler
{
    public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        return occupiedSlot.Stack?.PrimaryAdapter is ContainerAdapter;
    }

    public OccupiedSlotDropResult ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        return TryPutIntoContainer(entry, occupiedSlot)
            ? OccupiedSlotDropResult.Handled
            : OccupiedSlotDropResult.Rejected;
    }
}
```

`ExecuteOccupiedSlotDrop` возвращает:

| Результат | Что делает pipeline |
|---|---|
| `Handled` | handler сам выполнил действие; обычный drop, swap и поиск другого слота не запускаются |
| `Rejected` | handler запретил действие; перенос откатывается |
| `Fallthrough` | handler отказался вмешиваться; перенос продолжается как обычный drop на занятый слот |

Если handler вернул `Handled` или `Rejected`, он завершает эту попытку переноса. После этого система не запускает swap или alternative placement.

## IStackSizeLimitable

`IStackSizeLimitable` позволяет конкретному предмету задать свой лимит стека.

```csharp
public class AmmoAdapter : IItemAdapter, IStackSizeLimitable
{
    public int MaxStackSize => 120;
}
```

Примеры:

- зелья стакаются по 20
- стрелы по 999
- оружие по 1
- ресурсы и инструменты имеют разные лимиты

Чтобы этот лимит учитывался, в `UniversalInventory` должна быть включена настройка `_allowItemStackOverride`.

Если `_allowItemStackOverride` выключен, используется общий `_maxStackSize` инвентаря.
Если включён и item реализует `IStackSizeLimitable`, лимит предмета заменяет общий лимит.

Примеры:

| Настройки | Итог |
|---|---|
| inventory `_maxStackSize = 20`, override выключен, item `MaxStackSize = 99` | лимит `20` |
| inventory `_maxStackSize = 20`, override включён, item `MaxStackSize = 99` | лимит `99` |
| inventory `_maxStackSize = 20`, override включён, item `MaxStackSize = 5` | лимит `5` |

## IDescribable

`IDescribable` нужен, чтобы adapter мог отдать описание предмета для UI.

```csharp
public class ItemAdapter : IItemAdapter, IDescribable
{
    public string Description => _item.Description;
}
```

В стандартном примере `DefaultTooltipView` показывает `Description`, если adapter реализует `IDescribable`.

Этот интерфейс можно использовать и для своего UI:

- tooltip
- inspect panel
- hover card
- context menu details

## Собственные маленькие интерфейсы

Если базового `IItemAdapter` не хватает, можно добавить свои интерфейсы под нужды проекта.

Например:

- `IItemStatsProvider`
- `IRarityProvider`
- `IFlavorTextProvider`

Core inventory от них не зависит. Их должны читать только те UI- или gameplay-системы, которым эти данные нужны.
