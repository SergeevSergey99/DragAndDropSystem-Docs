# Conversion de items

La conversion se usa cuando dos inventarios no comparten la misma representacion del item.

## Casos tipicos

- comerciante usa `ScriptableObject`
- jugador usa modelo runtime
- equipo usa otra forma del mismo item

## Punto de extension

Implementa `IItemAdapterConverter`.

## Recomendaciones

- define claramente la direccion de conversion
- conserva los datos necesarios del dominio
- usa la misma logica tanto en preview como en commit

## Ejemplo

Revisa `Demo4 Trading`, donde hay conversion entre items del comerciante y del jugador.

