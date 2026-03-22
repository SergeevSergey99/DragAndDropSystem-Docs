# Торговля

Этот пример показывает сценарий, где два инвентаря используют разные типы данных и одна операция переноса ещё и меняет золото.

---

## Что участвует в операции

```mermaid
flowchart TB
    Merchant["Инвентарь торговца<br/>ScriptableObject items"]
    Player["Инвентарь игрока<br/>Runtime model items"]
    Equipment["Слоты экипировки"]
    Economy["Экономика<br/>золото и цены"]

    Merchant <-->|покупка / продажа| Player
    Player <-->|экипировка| Equipment
    Economy --- Merchant
    Economy --- Player
```

---

## Что здесь важно понять

В торговом примере есть три разных слоя логики:

1. механика слота и инвентаря
2. конвертация предмета между двумя моделями данных
3. бизнес-логика операции: хватает ли золота и что делать после покупки/продажи

Их лучше не смешивать.

---

## Покупка: правильная последовательность

```mermaid
sequenceDiagram
    participant U as Игрок
    participant M as Merchant Inventory
    participant P as Player Inventory
    participant D as Domain Hook
    participant Data as Player / Merchant Data

    U->>P: Бросает товар торговца
    P->>P: CanDrop (механика, совместимость)
    M->>P: Конвертация предмета в формат игрока
    P->>D: CanCommitTransfer (хватает ли золота?)
    P->>P: Выполнить перенос
    P->>D: OnTransferSucceeded (обновить золото)
    P->>Data: AddToData / RemoveFromData
```

---

## Где писать какую логику

| Задача | Где писать |
|---|---|
| Только подходящий тип в слот экипировки | `canAccept` или `CanDrop` |
| Проверка, хватает ли денег | `CanCommitTransfer` |
| Списание и начисление золота | `OnTransferSucceeded` |
| Конвертация `SO <-> Model` | `CreateItemConverter()` |
| Синхронизация списков и полей данных | `AddToData` / `RemoveFromData` |

Это и есть главная идея примера.

---

## Почему это лучше, чем всё делать в CanDrop

Потому что `CanDrop` вызывается как часть preview/planning.

А денежная логика и другие доменные проверки часто должны:

- выполняться один раз перед commit
- видеть целую операцию, а не только hover-preview
- иметь post-success hook после реального успеха

Именно поэтому торговля опирается на `CanCommitTransfer` и `OnTransferSucceeded`.

---

## Конвертация предметов

```mermaid
flowchart LR
    SO["Товар торговца<br/>ScriptableObject"] --> Conv["Item Converter"]
    Conv --> Model["Предмет игрока<br/>Runtime model"]
    Model --> Conv
    Conv --> SO
```

- торговец хранит более статичное представление предметов
- игрок хранит runtime-модель
- при переносе через границу инвентаря предмет конвертируется автоматически

---

## Ограничения примера

| Ограничение | Где выражается |
|---|---|
| Недостаточно золота у игрока | `CanCommitTransfer` |
| Недостаточно золота у торговца | `CanCommitTransfer` |
| Нельзя положить броню в слот оружия | `canAccept` / `CanDrop` |
| Нельзя продать неподходящий предмет торговцу | target-side mechanical validation |

---

## Что смотреть в коде

| Файл | Роль |
|---|---|
| `TradingHelper.cs` | торговые проверки и side effects |
| `PlayerInventoryDataBinding.cs` | sync игрока и player-side domain hooks |
| `MerchantInventoryDataBinding.cs` | sync торговца и merchant-side domain hooks |
| `EquipmentInventoryDataBinding.cs` | fixed-slot экипировка |
| `ModelInventoryItemConverter.cs` | converter в формат игрока |
| `MerchantInventoryItemConverter.cs` | converter в формат торговца |

---

## Куда идти дальше

- [Привязка данных](../architecture/data-binding.md) — полный lifecycle hooks
- [Экипировка](equipment.md) — если нужны только fixed slots без торговли
