# Strategies

Las strategies controlan como se comporta el inventario al aceptar, consultar y colocar items.

## Strategies incluidas

- `UniqueItemStrategy`
- `StackableItemStrategy`
- `SeparableStacksStrategy`

## Cuando elegir cada una

- `Unique`: cada item vive separado
- `Stackable`: stacking clasico
- `SeparableStacks`: stacks con split y movimientos parciales mas ricos

## Nota

La strategy define comportamiento de colocacion y capacidad. No reemplaza bindings ni reglas.

