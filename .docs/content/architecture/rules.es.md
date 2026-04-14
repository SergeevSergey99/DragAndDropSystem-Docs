# Rules

Las rules validan si un drag puede empezar o si un drop puede completarse.

## Ambitos

- `IGlobalRule`
- `IInventoryRule`
- `ISlotRule`

## Base recomendada

Hereda de `DragRuleBase` para crear reglas nuevas de forma simple.

## Rules incluidas

- `SameSlotRule`
- `SameInventoryRule`
- `ItemIdFilterRule`
- `UniqueItemLimitRule`

## Composite rules

Puedes combinar reglas usando `CompositeRule` con modo `AND` u `OR`.

## Uso practico

Usa rules para restricciones de UI y validacion de transferencia. Usa domain handlers para logica de negocio que depende del estado del juego.

