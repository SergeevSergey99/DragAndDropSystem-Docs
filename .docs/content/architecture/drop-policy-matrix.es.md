# Matriz de Drop Policy

El flujo completo está en [Transfer Pipeline](transfer-pipeline.md).

## Campos de policy

- `BlockedTargetResolutionKind`: `Reject`, `FindAlternative` o `Swap`
- `AlternativeOrderer`: orden usado solo para colocación automática
- `AllowSameInventoryAlternativePlacement`: permite otro placement para un drop
  bloqueado dentro del mismo inventory
- `PartialTransferMode`: permite una parte del entry o exige el amount completo

## Matriz

| Escenario | `Reject` | `FindAlternative` | `Swap` |
|---|---|---|---|
| target explícito válido | usar target | usar target | usar target |
| target explícito bloqueado | rechazar entry | buscar ordered candidates | intentar swap |
| area drop sin target | automatic candidates | automatic candidates | automatic candidates |
| occupied handler acepta | ejecutar handler | ejecutar handler | ejecutar handler |
| batch con varios entries | sequential best-effort | sequential best-effort | rechazar antes de mutar |

## Candidate orderers

La colocación automática puede usar orderers `Natural`, `MergeFirst`,
`EmptyFirst`, `MergeOnly` y `EmptyOnly`.

Un slot explícito se valida directamente y nunca se reordena.

## Transferencia parcial

`Allow` mueve la cantidad que cabe y deja el resto en source. `RequireFull`
restaura el entry si no cabe el amount completo.

Es comportamiento por entry. No existe modo batch `Atomic`.

## Alternativa dentro del mismo inventory

Con `AllowSameInventoryAlternativePlacement` desactivado, un explicit drop
bloqueado no busca otro slot del mismo inventory. Area drop y targets válidos no
cambian.

## Restricciones de swap

Swap requiere un único entry completo, validación bidireccional y rollback si
falla cualquier dirección. El batch swap de conjuntos shaped no está soportado.
