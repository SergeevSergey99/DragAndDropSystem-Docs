# Drop Policy

Drop policy responde una pregunta simple: **qué debe pasar cuando el jugador suelta un objeto en un lugar donde no se puede colocar directamente**.

Por ejemplo:

- el slot está ocupado
- el slot está bloqueado por reglas
- el stack destino ya está lleno
- el objeto se soltó sobre un área de inventario en lugar de un slot

## Opciones principales

| Ajuste | Qué hace |
|---|---|
| `Reject` | Rechaza la transferencia. El objeto vuelve a donde estaba. |
| `FindAlternative` | Busca otro slot adecuado. |
| `Swap` | Intenta intercambiar el objeto arrastrado con el objeto del slot ocupado. |

## Si el slot está ocupado

| Policy | Comportamiento |
|---|---|
| `Reject` | No se mueve nada. |
| `FindAlternative` | El sistema busca otro slot donde el objeto pueda entrar. |
| `Swap` | El sistema intenta intercambiar los dos objetos. |

Si el slot elegido es válido, la policy no interfiere: el objeto se coloca en ese slot.

## Si el objeto se soltó sobre un área de inventario

Cuando el jugador suelta un objeto sobre el área del inventario en lugar de un slot concreto, el sistema busca un lugar adecuado.

Para eso se usa `PlacementCandidateOrderer`:

| Orderer | Comportamiento |
|---|---|
| `Natural` | Usa el orden natural de slots. |
| `MergeFirst` | Primero intenta unir con stacks existentes. |
| `EmptyFirst` | Primero intenta slots vacíos. |
| `MergeOnly` | Solo intenta unir con stacks. |
| `EmptyOnly` | Solo intenta slots vacíos. |

Si el jugador suelta directamente sobre un slot concreto, no se usa ordenación: se comprueba primero el slot seleccionado.

## Transferencia parcial

`PartialTransferMode` controla si puede transferirse solo parte de un stack.

| Valor | Comportamiento |
|---|---|
| `Allow` | Transfiere lo que cabe. El resto vuelve atrás. |
| `RequireFull` | Cancela la transferencia si no cabe todo el stack. |

Esto se aplica a una entrada de transferencia. Si el jugador arrastra varios objetos seleccionados, el fallo de un objeto no cancela los objetos que ya se transfirieron antes.

## Slot alternativo dentro del mismo inventario

`AllowSameInventoryAlternativePlacement` importa al mover un objeto dentro del mismo inventario.

Si está activado, soltar sobre un slot ocupado puede hacer que el sistema busque otro slot libre en el mismo inventario.

Si está desactivado, ese drop no se convierte en “ponlo en otro sitio”. El objeto se queda donde estaba si el slot seleccionado no es válido.

## Swap

`Swap` intercambia dos objetos.

Funciona solo con un objeto arrastrado y un slot ocupado concreto. Si el jugador arrastra varios objetos sobre un solo slot ocupado, batch swap se rechaza antes de cualquier cambio.

## Qué elegir

| Comportamiento deseado | Elige |
|---|---|
| Colocar solo en el slot elegido | `Reject` |
| Permitir “ponlo en algún lugar adecuado” | `FindAlternative` |
| Permitir intercambio con un slot ocupado | `Swap` |
| Permitir mover parte de un stack | `PartialTransferMode.Allow` |
| Prohibir transferencias parciales | `PartialTransferMode.RequireFull` |

Ver también:

- [Pipeline de transferencia](transfer-pipeline.md)
- [Estrategias de colocación](strategies.md)
