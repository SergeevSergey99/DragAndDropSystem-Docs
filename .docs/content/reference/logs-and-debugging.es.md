# Logs y depuración

Esta página ayuda a encontrar dónde mirar cuando una transferencia no funciona.

Empieza por lo que ves en el juego, no por el nombre de una fase interna:

- el objeto no se puede tomar
- el objeto no se puede soltar
- el objeto vuelve atrás
- los datos no cambiaron después de la transferencia
- swap se comporta mal
- Console muestra muchos warnings

## Cómo activar los logs

!!! warning Por defecto no hay logs
    Los logs del asset están desactivados en tiempo de compilación. Hasta que añadas el define
    `UDND_LOG`, la Console estará vacía aunque internamente algo se esté rechazando.

Para activarlos:

1. `Edit → Project Settings → Player`
2. Despliega `Other Settings` y busca `Scripting Define Symbols`
3. Añade `UDND_LOG` y pulsa `Apply`
4. Espera a que recompile

Conviene saber:

- los logs funcionan **solo en el Editor** — el código está envuelto en `#if UNITY_EDITOR && UDND_LOG`, así que no llegan a la build
- `Scripting Define Symbols` se define por plataforma, así que añádelo en la plataforma en la que estés trabajando
- todos los mensajes del asset llevan el prefijo `[UDND]`, lo que facilita filtrar la Console
- las reglas rechazadas se imprimen en una línea aparte: `[RuleResult] Validation failed: <motivo>`
- los mensajes se imprimen en inglés, independientemente del idioma de la documentación

Cuando termines de depurar puedes quitar el define — solo afecta a la salida en Console,
no al comportamiento del sistema.

## Mapa rápido

| Qué ves | Empieza por |
|---|---|
| El objeto no se puede tomar | `CanStartDrag`, reglas del slot origen, carga de datos en UI |
| El objeto no se puede soltar | `CanDrop`, reglas del slot destino, `DropPolicySettings` |
| El objeto va a otro slot | `FindAlternative`, `PlacementCandidateOrderer`, drop sobre área de inventario |
| La UI cambió, los datos no | `AddToData`, `RemoveFromData`, binding correcto |
| Error de tipo de objeto | `CreateItemConverter()`, tipo de adapter en origen y destino |
| Swap se rompe después de la primera vez | conversión en ambas direcciones y tipo de adapter en los slots tras swap |

## Qué suelen significar los logs

| Log o clase | Normalmente significa |
|---|---|
| `RuleResult` | Una regla bloqueó drag o drop. |
| `CanStartDrag` | El binding bloqueó el inicio del drag. |
| `CanDrop` | El binding bloqueó drop en el inventario o slot destino. |
| `InventoryDropProcessor` | Drop fue rechazado antes de la transferencia real. A menudo la causa es policy o target slot. |
| `InventoryTransferService` | El problema ocurrió durante la transferencia: placement, swap, conversión o rollback. |
| `GetAcceptableCount` | El sistema está iterando slots y buscando dónde poner el objeto. |
| `CanCommitTransfer` | La lógica de negocio bloqueó la transferencia justo antes del commit, por ejemplo por falta de oro. |

## Si los warnings mencionan otros slots

Esto a menudo es normal. El sistema puede comprobar más que el slot seleccionado cuando:

- el objeto se soltó sobre un área de inventario
- `FindAlternative` está activado
- un stack grande necesita espacio
- se está comprobando swap

Si esperabas que solo se comprobara un slot, revisa:

- que el pointer realmente llegue al slot y no a `InventoryDropArea`
- que la policy no permita `FindAlternative`
- que el slot seleccionado no esté cubierto por otro elemento UI

## Si el log menciona una regla

Un log de regla no siempre significa un bug. A veces el sistema comprueba varias opciones
y una de ellas se rechaza correctamente.

Mira el contexto:

- qué slot se comprobó
- qué objeto se comprobó
- si era el slot seleccionado o una alternativa
- si la transferencia terminó funcionando después

## Si el problema son los datos

Cuando UI y datos del juego difieren tras una transferencia, revisa el binding:

- `GetItems()` / `GetOccupiedSlots()` cargan los datos correctos?
- `AddToData(...)` añade a la lista correcta?
- `RemoveFromData(...)` elimina de la lista correcta?
- los cambios externos de datos llaman a `ReloadUI()`?

## Si el problema aparece entre inventarios distintos

Por ejemplo: comerciante, jugador, equipo o contenedor usan modelos de datos distintos.

Revisa:

- si el binding correcto tiene `CreateItemConverter()`
- qué adapter hay en el slot origen
- qué adapter debería quedar en el slot destino
- si el converter conserva los datos únicos del objeto

## Si el problema está en comercio o validación externa

Revisa el domain handler:

- `CanStartTransfer` — si la transferencia puede empezar
- `CanStartTransferAsync` — si servidor u otra comprobación externa rechazó la transferencia
- `CanCommitTransfer` — si una colocación concreta puede confirmarse
- `OnTransferSucceeded` — si un efecto secundario rompe datos tras una transferencia exitosa

## Orden útil de comprobación

1. Asegúrate de que el objeto realmente se cargó en UI.
2. Revisa rules y `CanStartDrag`.
3. Revisa target slot, `CanDrop` y drop policy.
4. Si la transferencia va entre modelos distintos, revisa converter.
5. Si hay dinero, servidor, permisos o ownership, revisa domain handler.
6. Si UI y datos se separaron, revisa métodos add/remove del binding.

Ver también:

- [Troubleshooting](troubleshooting.md)
- [Drop Policy](../architecture/drop-policy-matrix.md)
- [Conversión de objetos](../architecture/item-conversion-cookbook.md)
