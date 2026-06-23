# Demo2 Loot

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/Q1B4ef6LyTU"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo2 Loot/LootDemo.unity`

Это пример slot-indexed инвентаря и связки игрового мира, событий и UI.

## Что показывает демо

- сундуки как интерактивные world-объекты
- отдельные bindings для игрока и сундука
- переключения разных источников данных для DataBinding
- подбор/выбрасывание сценарии, связанные с миром
- мир не знает о UI напрямую

## Как устроено

Ключевой момент примера: `Chest` и `PlayerInteraction` живут в игровом мире и не знают о конкретных UI-панелях. UI открывает и связывает только `LootUIController`.

## Как работает

Открытие сундука:

1. `PlayerInteraction` находит `IInteractable`.
2. По действию игрока вызывается `Interact(...)`.
3. `Chest` меняет состояние и публикует событие.
4. `LootUIController` получает это событие.
5. Контроллер активирует панель и вызывает `BindToChest(...)`.
6. `ChestInventoryDataBinding` загружает содержимое сундука в UI.

Перенос предметов:

1. Игрок таскает предмет между inventory игрока и сундука.
2. Стандартный transfer pipeline выполняет перенос.
3. После успешного commit binding-и обновляют `Chest` и `PlayerInventoryData`.

## Когда брать этот пример за основу

- нужны сундуки, открывающиеся по взаимодействию
- нужен пример разделения логики игрового мира и UI инвентаря