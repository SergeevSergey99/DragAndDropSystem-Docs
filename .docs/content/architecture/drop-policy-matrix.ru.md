# Матрица Drop Policy

Полный порядок описан в [Конвейере переноса](transfer-pipeline.md).

## Поля policy

- `BlockedTargetResolutionKind`: `Reject`, `FindAlternative` или `Swap`
- `AlternativeOrderer`: сортировка только для автоматического размещения
- `AllowSameInventoryAlternativePlacement`: можно ли искать другой placement при
  blocked drop внутри того же inventory
- `PartialTransferMode`: разрешить часть entry или требовать весь amount

## Матрица поведения

| Сценарий | `Reject` | `FindAlternative` | `Swap` |
|---|---|---|---|
| выбранный target валиден | использовать target | использовать target | использовать target |
| выбранный target заблокирован | отклонить entry | искать ordered candidates | попытаться swap |
| area drop без target slot | automatic candidates | automatic candidates | automatic candidates |
| occupied handler принимает target | выполнить handler | выполнить handler | выполнить handler |
| batch из нескольких entries | sequential best-effort | sequential best-effort | отклонить до мутаций |

## Candidate orderers

Для автоматического размещения доступны `Natural`, `MergeFirst`, `EmptyFirst`,
`MergeOnly` и `EmptyOnly` реализации `PlacementCandidateOrderer`.

Выбранный slot проверяется напрямую и никогда не сортируется.

## Partial transfer

`Allow` переносит вместившееся количество и оставляет остаток в source.
`RequireFull` восстанавливает entry, если весь amount разместить нельзя.

Это поведение одного entry. Режима batch `Atomic` нет.

## Same-inventory alternative

Если `AllowSameInventoryAlternativePlacement` выключен, blocked explicit drop
внутри одного inventory не уходит в другой slot. Area drop и валидный explicit
target не меняются.

## Ограничения swap

Swap требует один полный drag entry, двустороннюю валидность rules/placement и
успешный rollback при ошибке любой стороны. Batch swap shaped-наборов пока не
поддерживается.
