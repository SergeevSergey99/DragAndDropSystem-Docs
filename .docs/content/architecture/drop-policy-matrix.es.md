# Matriz de drop policy

Las drop policies controlan como intenta colocarse una transferencia.

## Parametros importantes

- `BlockedTargetBehavior`
- `DragAmount`
- `BatchMode`
- `AlternativePlacementMode`

## Preguntas que responde la policy

- que pasa si el slot objetivo esta ocupado
- si se permite partial transfer
- si el sistema puede buscar otro slot
- si una operacion multiple debe ser atomica

## Uso practico

Si el comportamiento de drop parece "demasiado inteligente" o "demasiado estricto", normalmente debes revisar la policy antes de tocar reglas o bindings.

