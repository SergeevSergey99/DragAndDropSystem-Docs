# Конвейер переноса

Эта страница описывает текущий just-in-time pipeline переноса.

См. также:

- [Матрица Drop Policy](drop-policy-matrix.md)
- [Cookbook: конвертация предметов](item-conversion-cookbook.md)
- [Логи и отладка](../reference/logs-and-debugging.md)

## Короткая версия

```mermaid
flowchart LR
    A["Запрос drop"] --> B["Общий veto transfer"]
    B --> C["Последовательная обработка entries"]
    C --> D["Проверка текущего состояния target"]
    D --> E["Мутация одного entry"]
    E --> F["Events и sync после commit"]
```

Материализованного `TransferPlan`, виртуального состояния слотов и общего atomic
rollback больше нет. Каждый entry проверяется по реальному состоянию, оставшемуся
после предыдущего entry.

## Порядок обработки entry

Для каждого `DragEntry` сервис:

1. проверяет drag/drop rules;
2. без мутации source получает target-side preview adapter;
3. создаёт `InventoryAcceptanceRequest`;
4. проверяет выбранный target через `IStrategy.TryGetCandidate(...)`;
5. при автоматическом размещении перечисляет `IStrategy.GetCandidates(...)` и
   применяет `PlacementCandidateOrderer`;
6. выполняет conversion, split, merge, placement или swap;
7. восстанавливает snapshots текущего entry при ошибке;
8. отправляет events и DataBinding notifications только после commit этого entry.

## Выбранный target и автоматическое размещение

Конкретный target slot всегда проверяется первым и не проходит через orderer.

Если target заблокирован:

- `Reject` отклоняет entry;
- `Swap` пытается выполнить swap одного entry;
- `AlternativeSlots` перечисляет автоматические candidates и сортирует их
  настроенным `PlacementCandidateOrderer`.

Area drop и auto-transfer сразу используют автоматические candidates.

## Strategy и topology

`IStrategy` отвечает за item semantics: unique, one-per-ID, separable stacks,
capacity, merge/create и перечисление candidates.

`IPlacementGeometry` и topology отвечают за anchor, oriented footprint, bounds,
occupancy и covered slots. Single-cell и shaped items используют один placement
pipeline.

## Batch semantics

Batch работает как sequential best-effort:

- entries идут в порядке `DragContext`;
- ошибка текущего entry не откатывает ранее завершённые entries;
- следующие entries видят уже внесённые изменения;
- при `PartialTransferMode.Allow` переносится вместившаяся часть, остаток остаётся
  в source;
- batch swap отклоняется до мутаций. Swap требует один полный entry.

## Точки расширения

`ITransferDomainHandler.CanStartTransfer(...)` вызывается до обработки entries и
может отменить весь transfer. Это общий veto hook. В нём можно реализовать свою
симуляцию, но pipeline не требует обязательной симуляции.

Также доступны inventory/global rules, `IOccupiedSlotDropHandler`,
`PlacementCandidateOrderer`, converters и success hooks.

## Гарантии при ошибке

Batch-wide atomicity не обещается. Ошибка одного entry восстанавливает snapshots
source и target этого entry, а notifications отправляются только после его commit.
