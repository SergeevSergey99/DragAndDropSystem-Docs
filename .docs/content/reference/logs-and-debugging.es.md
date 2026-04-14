# Logs y debugging

Cuando una transferencia falla, separa el problema por fases:

1. inicio del drag
2. preview y planning
3. validacion de dominio
4. execution y commit
5. eventos posteriores

## Recomendacion

Busca primero el primer log significativo, no el ultimo.

## Archivos donde mirar

- `TransferPlanner.cs`
- `TransferPlanExecutor.cs`
- `InventoryDropProcessor.cs`
- bindings concretos
- converters
- rules

