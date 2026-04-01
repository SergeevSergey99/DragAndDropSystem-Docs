# Demo1 Inventories

`Examples/Demo1 Inventaries/InventariesDemo.unity`

Это самый базовый пример пакета. Он показывает inventory без отдельной игровой логики, экономики или world-интеграции.

## Что показывает демо

- простой список `ItemExampleSO`
- `ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>`
- загрузку списка в `UniversalInventory`
- обратную синхронизацию UI -> данные
- локальные переопределения `CanStartDrag` и `CanDrop`

## Как устроено

Основные части:

- `ItemExampleSO.cs` — данные предмета
- `Adapters/ItemAdapterSoAdapter.cs` — адаптер UI-слоя
- `DataBindings/ItemsSOInventoryDataBinding.cs` — binding между списком и inventory
- `SO/Rules/*` — пример rule preset

Архитектурно это самая короткая цепочка в проекте:

```mermaid
flowchart LR
    Data["List<ItemExampleSO>"] <--> Binding["ItemsSOInventoryDataBinding"] <--> UI["UniversalInventory"]
```

Здесь нет отдельного доменного сервиса. Сам binding читает список, создаёт адаптеры и синхронизирует изменения обратно в тот же список.

## Как работает

1. `GetItems()` отдаёт список `items`.
2. `ReloadUI()` строит UI-стеки через `CreateAdapter(...)`.
3. Drag and drop выполняется стандартным pipeline.
4. После успешного переноса binding получает add/remove callbacks.
5. `AddToData(...)` и `RemoveFromData(...)` обновляют исходный список.

Дополнительно пример показывает, где удобно добавлять простые локальные ограничения:

- `CanStartDrag(...)` — запретить drag из конкретного inventory
- `CanDrop(...)` — запретить drop в конкретный inventory

## Что смотреть в коде

| Файл | Роль |
|---|---|
| `Examples/Demo1 Inventaries/DataBindings/ItemsSOInventoryDataBinding.cs` | основной binding примера |
| `Examples/Demo1 Inventaries/Adapters/ItemAdapterSoAdapter.cs` | адаптер для `ItemExampleSO` |
| `Examples/Demo1 Inventaries/ItemExampleSO.cs` | модель данных предмета |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | базовый класс list-based binding |

## Когда брать этот пример за основу

- нужен первый inventory без сложной доменной модели
- нужно понять lifecycle `ListInventoryDataBinding`
- нужно быстро проверить slot rules, filters или visual setup
