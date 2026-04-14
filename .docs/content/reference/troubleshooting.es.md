# Troubleshooting

## El drag no empieza

Suele significar:

- slot vacio
- `CanStartDrag` fallo
- adapter incorrecto
- el binding no cargo datos

## El preview pasa pero el commit falla

Suele significar:

- veto de `ITransferDomainHandler`
- veto de `IAsyncTransferDomainHandler`
- conversion fallida
- problema de colocacion durante execution

## Los datos no se sincronizan

Revisa:

- `AddToData`
- `RemoveFromData`
- `ReloadUI()`
- referencia del binding al inventario correcto

## `CanDrop` se llama muchas veces

Puede ser normal si hay:

- busqueda de targets alternativos
- area drop
- validacion de swap

## Donde mirar primero

- `TransferPlanner`
- `TransferPlanExecutor`
- converter del binding
- rule validator correspondiente

