# Troubleshooting

Esta página ayuda a diagnosticar problemas por síntoma: qué ves, qué suele significar y qué comprobar.

## El objeto no se puede tomar

Causas comunes:

- el slot origen está vacío
- `CanStartDrag` devolvió fallo
- los datos no se cargaron en UI
- el slot contiene un adapter de tipo incorrecto

Comprueba:

- si `DataBinding` está asignado al `UniversalInventory` correcto
- si se llama a `ReloadUI()`
- qué devuelve `GetItems()` / `GetOccupiedSlots()`
- si alguna regla bloquea drag desde este slot o inventario

## El objeto no se puede soltar en un slot

Causas comunes:

- el slot prohíbe este tipo de objeto
- el stack destino ya está lleno
- `CanDrop` devolvió fallo
- `DropPolicySettings` está configurado como `Reject`
- el objeto se convirtió al tipo de adapter incorrecto

Comprueba:

- reglas del slot e inventario
- `CanDrop` en tu binding
- `DropPolicySettings`
- converter, si la transferencia va entre tipos de inventario distintos

## El objeto se coloca en otro slot

Normalmente no es un bug, sino comportamiento de policy.

Comprueba:

- si `FindAlternative` está activado
- si está activada la búsqueda de alternativas dentro del mismo inventario
- si el objeto se soltó sobre el área del inventario en lugar de un slot concreto
- qué `PlacementCandidateOrderer` está seleccionado

Si necesitas comportamiento estricto de “solo este slot”, usa `Reject` para destino bloqueado.

## Los datos no se actualizaron tras la transferencia

Si la UI cambió pero tus datos del juego no, el problema casi siempre está en el binding.

Comprueba:

- si el binding está asignado al inventario correcto
- si `AddToData` / `RemoveFromData` están implementados
- si esos métodos modifican exactamente la lista u objeto que esperas
- si cambias datos directamente y olvidas llamar a `ReloadUI()`

## Aparece `Invalid item type`

Todos los mensajes del sistema se muestran en inglés, así que busca en la Console exactamente esta cadena:

```text
[RuleResult] Validation failed: Invalid item type
```

!!! info ¿No ves el mensaje?
    Los logs están desactivados por defecto. Añade el define `UDND_LOG` para activarlos —
    consulta [Logs y depuración](logs-and-debugging.md).

Normalmente significa que un inventario recibió un adapter que pertenece a otro modelo de datos.

Causas comunes:

- `CreateItemConverter()` no está configurado
- converter devuelve el adapter incorrecto
- swap dejó un adapter de otro inventario en el slot
- los datos tras la transferencia se sincronizan en un formato, mientras el slot guarda otro

Comprueba:

- converter en bindings de origen y destino
- `CanDrop` / `CanStartDrag` en binding de slots fijos o equipamiento
- qué adapter está realmente guardado en el slot tras la transferencia

## El primer swap funciona, el segundo se rompe

Casi siempre, tras el primer swap uno de los slots guarda un objeto con tipo de adapter incorrecto.

Comprueba:

- si existe conversión en ambas direcciones
- si estás intercambiando dos stacks directamente sin conversión
- si datos y UI se actualizan con el mismo tipo de adapter

## `CanDrop` se llama muchas veces

Esto es normal cuando el sistema busca un lugar adecuado:

- drop fue sobre un área de inventario
- `FindAlternative` está activado
- se está comprobando swap
- el sistema itera slots para auto placement

Es sospechoso si definitivamente sueltas sobre un slot concreto y la policy no permite búsqueda alternativa.

En ese caso, comprueba:

- si el pointer llega al slot
- si `InventoryDropArea` está encima de los slots
- qué `BlockedTargetResolutionKind` se usa

## El stack se comporta como el mismo objeto

Esto ocurre cuando varias instancias del stack reutilizan el mismo adapter, aunque deberían ser objetos distintos.

Comprueba:

- si se crea un adapter separado para cada instancia única
- si converter conserva el estado runtime del objeto
- si `ItemId` coincide con tu lógica de stacking

## Por dónde empezar a depurar

1. Nombra el síntoma: no se puede tomar, no se puede soltar, datos no actualizados, swap roto.
2. Comprueba ajustes en Inspector: strategy, slot management, drop policy, rules.
3. Comprueba binding: carga de datos, `CanStartDrag`, `CanDrop`, métodos add/remove.
4. Si los inventarios usan modelos de datos distintos, comprueba converter.
5. Si el problema aparece solo con comercio, validación de servidor u oro, comprueba domain handler.

Ver también:

- [Logs y depuración](logs-and-debugging.md)
- [Drop Policy](../architecture/drop-policy-matrix.md)
- [Conversión de objetos](../architecture/item-conversion-cookbook.md)
- [Pipeline de transferencia](../architecture/transfer-pipeline.md)
