# Logs and Debugging

Esta página te ayuda a identificar rápidamente qué fase del pipeline ha fallado.

Idea central:

- `planner` elige un plan válido
- `executor` maneja commit, conversion y rollback
- `rules` manejan restricciones mecánicas
- `domain hooks` manejan el veto de negocio justo antes del commit

---

## Mapa corto de logs

| Dónde aparece el log | Qué suele significar |
|---|---|
| `RuleResult` | una comprobación concreta de una rule fue rechazada |
| `InventoryTransferService` | problema de planning o de selección del target |
| `InventoryDropProcessor` | el planner no pudo construir un plan válido |
| `InventoryTransferService` | problema de commit, conversión, swap o rollback |
| `GetAcceptableCount` | búsqueda de slots a nivel de inventario |
| `CanCommitTransfer` / domain validation | la lógica de negocio vetó el commit |

---

## Cómo leer logs comunes

### `[RuleResult] Validation failed: ...`

Esto es el rechazo de una rama concreta de rules.

Importante:

- por sí solo no siempre significa un bug
- a veces es un rechazo normal de un slot candidato de prueba
- la call stack importa: mira quién inició la comprobación

Si el log viene de:

- `MappedSlotInventoryDataBinding.CanDrop()` -> normalmente tipo de adapter o compatibilidad del slot
- `CanStartDrag()` -> tipo de adapter incorrecto en el source slot o veto de drag del lado de origen

### `[InventoryDropProcessor] plan failed: ...`

El planner no pudo producir un plan válido.
La execution todavía no ha empezado.

Causas comunes:

- el target slot es inválido
- la policy no permite fallback
- no existe ningún candidate slot válido

### `[InventoryTransferService] ...`

Esto es logging de la fase de execution.
El planning ya tuvo éxito, y el problema ocurrió durante:

- validación de dominio
- split/remove
- outgoing/incoming conversion
- colocación en el inventario objetivo
- commit de swap
- rollback

### `[InventoryName] GetAcceptableCount: ...`

Esto es una búsqueda de aceptación a nivel de inventario.

Si esperabas un direct slot drop y aun así ves este log, normalmente revisa:

- si realmente existía un `targetSlot` concreto
- si la operación cayó en `FindAlternative`
- si este era un area-drop path

---

## Diagnóstico rápido por fase

### 1. Inicio del drag

Mira:

- `OnDragAttempting`
- `ValidateStartDrag`
- el `CanStartDrag` del binding

Causas típicas:

- el source slot está vacío
- el slot contiene el tipo de adapter incorrecto
- el source binding prohíbe el drag

### 2. Preview / planning

Mira:

- `InventoryTransferService`
- `ValidateDrop`
- `InventoryAcceptanceRequest`
- `GetAcceptableCount`

Causas típicas:

- falló la conversión del lado objetivo
- las slot rules rechazan el target adapter
- el planner busca candidatos de forma más amplia de lo esperado

### 3. Validación de dominio

Mira:

- `CanCommitTransfer`
- `CanCommitTransferAsync`
- `ValidateDomainHandlers`

Causas típicas:

- dinero
- permisos de acceso
- veto del servidor
- validación externa síncrona/asíncrona

### 4. Execution

Mira:

- `InventoryTransferService`
- utility de conversión
- `TryAddToSlot` / `TryAddStack`

Causas típicas:

- la conversión falló durante el commit
- la colocación falló
- el rollback restauró el estado previo

### 5. Swap

Mira:

- `RequiresSwap`
- `ValidateSwapRules`
- `OnSwapAttempting`
- `OnSwapCompleted`

Causas típicas:

- una dirección del swap no supera las rules
- el swap se implementó como raw exchange en lugar de como conversion-aware commit
- después del primer swap, un slot sigue conteniendo un adapter extranjero

---

## Patrones prácticos

### El preview pasó, pero el commit falló

Eso suele significar que el problema no está en las rules, sino en la execution o en los domain hooks.

Comprueba:

- `CanCommitTransfer`
- `CanCommitTransferAsync`
- conversión del executor
- placement / rollback

### Aparecen warnings para otros slots

Eso suele significar que alguna ruta disparó una búsqueda a nivel de inventario.

Comprueba:

- `GetAcceptableCount`
- `FindAlternative`
- area-drop
- routing incorrecto para un direct slot drop

### El primer swap tiene éxito y el segundo falla

Esto casi siempre significa que el slot almacena el tipo de adapter incorrecto después del primer swap.

---

## Léelo junto con

- [Troubleshooting](troubleshooting.md) — síntoma -> causa -> dónde mirar
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — orden de fases
- [Cookbook: Item Conversion](../architecture/item-conversion-cookbook.md) — problemas en límites de adapters

