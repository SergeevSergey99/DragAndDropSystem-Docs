# Equipment

La forma habitual de modelar equipamiento es usar `MappedSlotInventoryDataBinding`.

## Idea general

- cada slot tiene un significado fijo
- el dominio decide que tipos son validos para cada slot
- el UI solo refleja esa estructura

## Puntos de extension

- `MappedSlotInventoryDataBinding`
- reglas por slot
- converters si el equipamiento usa otra representacion del item

## Ejemplo recomendado

Consulta `Demo4 Trading`, donde el inventario de equipamiento convive con comercio y conversion entre modelos.

