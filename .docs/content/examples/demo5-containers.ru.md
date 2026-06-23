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

Это пример предметов-инвентарей, которые сами содержат вложенный набор предмтов.

## Что показывает демо

- item instances вместо простых записей списка
- контейнер как обычный предмет в инвентаре игрока
- отдельную UI-панель для содержимого активного контейнера
- context menu action для открытия контейнера
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
3. При drop в занятый слот `PlayerContainerInventoryDataBinding` может выполнить дополнительное поведение.
4. Перед помещением контейнера внутрь другого контейнера проверяется `WouldCreateCycle(...)`.

## Когда брать этот пример за основу

- предмет должен содержать вложенный инвентарь
- нужен context menu для операций над предметом
- нужны свои drop-правила поверх стандартного процесса переноса
