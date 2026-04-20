# Troubleshooting

Esta página está organizada por síntoma.

Formato:

- lo que ves
- lo que suele significar
- dónde mirar en el código y en la configuración

---

## `Wrong item type`

Esto normalmente significa que un binding recibió un adapter del límite de inventario equivocado.

Causas típicas:

- no ocurrió la conversión del lado objetivo
- el swap se confirmó como raw exchange
- después de una operación previa, el slot aún almacena un tipo de adapter extranjero

Dónde mirar:

- `CreateItemConverter()` en el binding
- `TransferItemConversionUtility`
- `MappedSlotInventoryDataBinding.CanDrop()`
- `MappedSlotInventoryDataBinding.CanStartDrag()`

---

## Warnings para otros slots al soltar en un único slot

Esto normalmente significa que el sistema cayó en una búsqueda a nivel de inventario cuando esperabas una ruta de slot directo.

Causas típicas:

- la operación usó `FindAlternative`
- se activó area-drop en lugar de slot-drop
- el executor volvió a llamar a `GetAcceptableCount()`
- el planner no recibió un `targetSlotHint` concreto

Dónde mirar:

- `DropPolicySettings`
- blocked-target resolver activo
- `InventoryDropProcessor`
- `TransferPlanner`
- logs de `GetAcceptableCount`

---

## El preview pasa, pero el commit falla

Normalmente el problema no está en las rules, sino en la execution o en los domain hooks.

Causas típicas:

- veto de `CanCommitTransfer`
- veto de `CanCommitTransferAsync`
- la conversión falló durante la execution
- el placement falló después del split

Dónde mirar:

- `ITransferDomainHandler`
- `IAsyncTransferDomainHandler`
- `TransferPlanExecutor`

---

## El primer swap funciona y el segundo se rompe

Esto casi siempre significa que el slot almacena el tipo de adapter incorrecto después del primer swap.

Causas típicas:

- el swap fue un raw exchange
- la conversión se aplicó en preview pero no en commit
- los eventos add/remove sincronizaron un formato mientras que el slot almacenaba físicamente otro distinto

Dónde mirar:

- ruta de ejecución del swap
- `ValidateSwapRules`
- conversión en ambas direcciones

---

## El drag no empieza en absoluto

Causas típicas:

- el source slot está vacío
- `CanStartDrag` devolvió fallo
- el slot almacena el tipo de adapter incorrecto
- el binding no cargó los datos en la UI

Dónde mirar:

- `OnDragAttempting`
- `ValidateStartDrag`
- `ReloadUI()`
- `GetItems()`

---

## Los datos no se sincronizaron después de una transferencia exitosa

Causas típicas:

- la execution nunca llegó a los deferred events
- el binding está adjunto al inventario equivocado
- `AddToData` / `RemoveFromData` trabajan contra la backing source equivocada

Dónde mirar:

- `DispatchTransferEvents`
- `DispatchSwapEvents`
- `OnItemAdded` / `OnItemRemoved`
- el binding concreto

---

## Un stack se comporta como un item repetido, pero las instancias deberían ser distintas

Causas típicas:

- se reutiliza una sola instancia de adapter como representante de todo el stack
- la conversión no preserva el state de instancia
- `ItemId` no coincide con la semántica real de stacking

Dónde mirar:

- implementación del adapter
- conversion cookbook
- `ItemId`

---

## `CanDrop` se llama muchas veces

Esto puede ser normal cuando:

- se está ejecutando una búsqueda de candidatos durante el preview
- `FindAlternative` está activo
- area-drop está activo
- el swap valida ambas direcciones

Esto no es normal cuando:

- estás haciendo un direct slot drop sobre un slot concreto sin `FindAlternative`
- y aun así los logs muestran comprobaciones sobre slots vecinos

En ese caso, busca un problema de ruta/policy.

---

## Por dónde empezar a depurar

1. Identifica la fase: inicio del drag, preview/planning, validación de dominio, execution o swap.
2. Mira el primer log significativo de la stack, no el último.
3. Comprueba si existe un `targetSlot` concreto.
4. Comprueba qué tipo de adapter está almacenado físicamente en el slot después de la operación.

---

## Páginas relacionadas

- [Logs and Debugging](logs-and-debugging.md)
- [Transfer Pipeline](../architecture/transfer-pipeline.md)
- [Cookbook: Item Conversion](../architecture/item-conversion-cookbook.md)
- [Drop Policy Matrix](../architecture/drop-policy-matrix.md)

