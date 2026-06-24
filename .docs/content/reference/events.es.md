# Referencia de eventos

Los eventos globales de drag / drop / auto-transfer / swap están en `UDNDEvents`. Los eventos de cambio de contenido de un inventario concreto están en `UniversalInventory`.

## Versión corta

| Si necesitas... | Escucha |
|---|---|
| Saber cuándo el jugador empieza o termina un drag | `UDNDEvents.OnDragStarted`, `UDNDEvents.OnDragEnded` |
| Reaccionar a un drop exitoso | `UDNDEvents.OnDropCompleted` |
| Mostrar una razón de cancelación | `UDNDEvents.OnDragCancelled` |
| Seguir una transferencia rápida por click | `UDNDEvents.OnAutoTransferCompleted`, `UDNDEvents.OnAutoTransferFailed` |
| Seguir cambios de contenido del inventario | `UniversalInventory.OnItemAdded`, `UniversalInventory.OnItemRemoved` |
| Bloquear swap antes de ejecutarlo | `UDNDEvents.OnSwapAttempting` |

## Drag / drop normal

Orden típico cuando todo sale bien:

1. `OnDragAttempting`
2. `OnDragStarted`
3. `OnDragEnterSlot` / `OnDragExitSlot` durante hover
4. `OnDropAttempting`
5. `OnDropCompleted`
6. `OnDragEnded`

Si el drag o el drop se cancela, se ejecuta `OnDragCancelled` en vez de `OnDropCompleted`, y después igualmente se ejecuta `OnDragEnded`.

`OnDragEnded` es el lugar más seguro para limpiar UI temporal porque se ejecuta al final tanto de flujos exitosos como fallidos.

## Eventos globales (`UDNDEvents`)

Son eventos `static`. Suscríbete con `UDNDEvents.OnX += handler`; cancela la suscripción en `OnDisable` u otro método de teardown.

`DragAndDropManager` limpia suscriptores de la Play session anterior al empezar una nueva Play session. Esto importa para Fast Enter Play Mode.

### Drag

| Evento | Cuándo se ejecuta | Nota |
|---|---|---|
| `OnDragAttempting` | Antes de empezar drag | Las rules pueden cancelar la operación |
| `OnDragStarted` | Drag confirmado | `DragContext` ya está disponible |
| `OnDragEnterSlot` | El puntero entra en un slot | Útil para hover UI |
| `OnDragExitSlot` | El puntero sale de un slot | Útil para limpiar hover UI |
| `OnDropAttempting` | Antes de procesar drop | El destino ya se conoce |
| `OnDropCompleted` | Drop exitoso | Los cambios ya están aplicados |
| `OnDragCancelled` | Drag o drop cancelado | Útil para mensajes de error o limpieza de UI |
| `OnDragStackChanged` | Cambió la cantidad del drag stack | Por ejemplo después de split drop |
| `OnDragEnded` | Terminó el ciclo de drag | Siempre se ejecuta al final |

### Auto-transfer

| Evento | Cuándo se ejecuta |
|---|---|
| `OnAutoTransferAttempting` | Antes de la transferencia rápida por click |
| `OnAutoTransferCompleted` | Transferencia rápida exitosa |
| `OnAutoTransferFailed` | Transferencia rápida fallida |

### Swap

| Evento | Cuándo se ejecuta | Nota |
|---|---|---|
| `OnSwapAttempting` | Antes del swap | Pon `Cancel = true` para bloquear el swap |
| `OnSwapCompleted` | Swap exitoso | Los slots ya contienen los stacks resultantes |

## Eventos de inventario

`UniversalInventory` envía eventos cuando su contenido realmente cambió.

| Evento | Cuándo se ejecuta | Contexto |
|---|---|---|
| `OnItemAdded` | Se añadió un objeto al inventario | `InventoryItemEventContext` |
| `OnItemRemoved` | Se quitó un objeto del inventario | `InventoryItemEventContext` |

### Campos de `InventoryItemEventContext`

| Campo | Tipo | Descripción |
|---|---|---|
| `Stack` | `ItemStack` | Stack añadido o quitado |
| `SlotIndex` | `int` | Índice del slot |
| `SourceInventory` | `IInventory` | De dónde vino el objeto, si se conoce |
| `TargetInventory` | `IInventory` | A dónde fue el objeto, si se conoce |
| `SourceSlot` | `BaseSlot` | Slot de origen, si se conoce |
| `TargetSlot` | `BaseSlot` | Slot de destino, si se conoce |

En la práctica:

- `context.Stack.PrimaryAdapter` da el adapter principal del objeto
- `context.Stack.Count` da la cantidad
- en transferencias entre tipos distintos de inventario, remove normalmente contiene el objeto antes de conversión, y add contiene el objeto después de conversión

## Contexto de swap

### Campos de `InventorySwapContext`

| Campo | Tipo | Descripción |
|---|---|---|
| `SourceStack` | `ItemStack` | Stack en el slot de origen antes del swap |
| `TargetStack` | `ItemStack` | Stack en el slot de destino antes del swap |
| `SourceSlot` | `BaseSlot` | Slot desde el que empezó el drag |
| `TargetSlot` | `BaseSlot` | Slot donde se soltó el objeto |
| `SourceInventory` | `IInventory` | Inventario de origen |
| `TargetInventory` | `IInventory` | Inventario de destino |
| `Cancel` | `bool` | Pon `true` para cancelar el swap |

En swap entre tipos distintos de inventario, los adapters finales en los slots pueden ser distintos de los que había en `SourceStack` y `TargetStack` antes de ejecutar. Es lo esperado: los objetos se convierten para el inventario de destino.

## Cuándo se envían eventos

Los eventos se envían solo después de que el inventario cambie con éxito.

Si la validación de transferencia falla, no hay espacio o la operación se cancela, no se envían eventos add/remove. Los suscriptores no ven un estado temporal que el sistema después revertiría.

Para transferencia de grupo, cada objeto se procesa por separado:

- un objeto exitoso envía sus propios eventos
- un objeto fallido se queda donde estaba y no envía eventos
- el fallo de un objeto no cancela los eventos de objetos que ya se transfirieron con éxito

Ver también:

- [Pipeline de transferencia](../architecture/transfer-pipeline.md)
- [Logs y depuración](logs-and-debugging.md)
