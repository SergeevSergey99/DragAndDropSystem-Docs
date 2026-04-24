# Matriz de Drop Policy

Esta página responde una pregunta práctica:
"¿qué hará exactamente el sistema para mi combinación de target slot, policy y estado del slot?"

La arquitectura completa está descrita en [Transfer Pipeline](transfer-pipeline.md).
Esta página es una matriz compacta de comportamiento.

---

## Idea corta

Hay tres interruptores principales:

- si existe un `target slot` concreto
- si ese slot está vacío u ocupado
- qué blocked-target resolver está seleccionado

Modificadores adicionales:

- `AllowPartial`
- `BatchMode`
- transferencia dentro del mismo inventario vs entre inventarios

---

## Matriz básica

| Escenario | `Reject` | `Swap` | `FindAlternative` |
|---|---|---|---|
| `target slot` concreto, slot vacío y válido | colocar en ese slot | colocar en ese slot | colocar en ese slot |
| `target slot` concreto, slot ocupado pero merge/placement es posible | colocar en ese slot | colocar en ese slot | colocar en ese slot |
| `target slot` concreto, slot ocupado y placement normal imposible | rechazar si el `occupied handler` no intercepta | probar `occupied handler`, luego swap | probar `occupied handler`, luego buscar otros slots |
| `target slot` concreto, slot falla rules | rechazar | rechazar o swap si es un target ocupado y ambas direcciones del swap pasan rules | puede buscar otro slot |
| area-drop sin slot concreto | búsqueda en todo el inventario | normalmente se comporta como búsqueda normal, no como slot-to-slot swap | búsqueda en todo el inventario |

---

## Direct slot drop: regla importante

Cuando la operación ya tiene un `target slot` concreto:

- con `Reject` y `Swap`, el planner no debe escanear el resto del inventario
- la búsqueda en todo el inventario solo es válida para `FindAlternative`
- el executor para un `targetSlot` concreto no debe repetir `GetAcceptableCount()` a nivel de inventario

Esto es especialmente importante para inventories de fixed slots, como equipment.
De lo contrario aparecen logs de validación engañosos para slots vecinos.

---

## Occupied slot: orden real

Cuando el target slot está ocupado y el placement normal en ese slot falló:

1. el planner primero comprueba `DataBinding.CanHandleOccupiedSlotDrop(...)`
2. si el binding dice "lo manejo yo" -> se construye `RequiresOccupiedHandler`
3. si el binding no intercepta:
   - `Reject` -> fallo
   - `Swap` -> se construye `RequiresSwap`
   - `FindAlternative` -> se enumeran otros candidate slots

El `occupied handler` tiene prioridad sobre swap y sobre alternative placement.

---

## Partial transfer

`AllowPartial` solo cambia la cantidad, no la rama elegida:

- si todo cabe -> éxito normal
- si solo cabe una parte y `AllowPartial = false` -> fallo
- si solo cabe una parte y `AllowPartial = true`:
  - con `FindAlternative`, el resto puede buscar otros slots
  - con `Reject` y `Swap`, el resto no debe activar búsqueda global

---

## Same-inventory vs cross-inventory

### Same-inventory

- `FindAlternative` no es un reshuffle/sort global
- por defecto, `FindAlternative` puede mover el item a otro slot válido si el hinted target está bloqueado
- desactiva `AllowSameInventoryAlternativePlacement` en `FindAlternativeBlockedTargetResolver` si un blocked drop dentro del mismo inventario debe dejar el item en su slot original
- swap solo está soportado para una sola entry y un source stack completo

### Cross-inventory

- conversion puede cambiar el tipo de adapter al cruzar inventarios
- swap no debe ser un intercambio raw de stacks
- ambas direcciones deben pasar por conversion de forma independiente

---

## Batch drag

Para batch drag, mantén separadas dos cosas:

- blocked-target resolver
- `BatchMode`

`BatchMode` responde "qué pasa si una entry falla":

- `Atomic` -> todo o nada
- `BestEffort` -> mover lo que se pueda

Swap no es un modo general de batch.
La implementación actual de swap está orientada a una sola entry y un source stack completo.

