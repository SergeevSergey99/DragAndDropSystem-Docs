# Demo5 Containers

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/3BFn4jt9xhM"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo5 Containers/Containers Demo.unity`

Это пример предметов-инвентарей, которые сами содержат вложенный набор предметов.

## Что показывает демо

- item instances вместо простых записей списка
- контейнер как обычный предмет в инвентаре игрока
- отдельную UI-панель для содержимого активного контейнера
- context menu action для открытия контейнера
- drop предмета прямо на иконку контейнера, чтобы положить предмет внутрь
- защиту от циклов при вложении контейнеров

## Как устроено

Данные:

- `Scripts/Data/ItemInstance.cs`
- `Scripts/Data/ContainerItemInstance.cs`
- `Scripts/Data/IContainerizeItemInstance.cs`
- `Scripts/ContainerDemoManager.cs`

Bindings и UI:

- `Scripts/Bindings/PlayerContainerInventoryDataBinding.cs`
- `Scripts/Bindings/ContainerInventoryDataBinding.cs`
- `Scripts/UI/ContainerUIController.cs`
- `Scripts/ContextMenu/OpenContainerMenuEntrySO.cs`

## Как работает

Открытие контейнера:

1. В инвентаре игрока лежит `ContainerItemInstance`.
2. Контекстное меню вызывает `OpenContainerMenuEntrySO`.
3. Через `Events.OnOpenClick` выбранный контейнер передаётся в `ContainerUIController`.
4. Контроллер выставляет активный контейнер.
5. `ContainerInventoryDataBinding` перестраивает размер inventory и загружает содержимое контейнера.

Перенос предметов:

1. Между инвентарем игрока и контейнера работают обычные drag/drop операции.
2. Binding-и синхронизируют перенос в player list или в `ContainerItemInstance.Items`.
3. Если предмет бросают на слот, где лежит контейнер, `IPreRuleOccupiedSlotDropHandler` перехватывает эту попытку до обычных drop-правил.
4. Binding проверяет, что в контейнере есть место и что перенос не создаст вложение контейнера в самого себя или своего потомка.
5. Если проверка прошла, `ContainerViewRegistry.InsertIntoContainer(...)` кладет предмет внутрь контейнера и возвращает `OccupiedSlotDropResult.Handled`.
6. Если проверка не прошла, handler возвращает `Rejected`, и предмет остается на исходном месте.

## Когда брать этот пример за основу

- предмет должен содержать вложенный инвентарь
- нужен context menu для операций над предметом
- нужен drop на уже занятый слот как отдельное действие, например “положить внутрь контейнера”
