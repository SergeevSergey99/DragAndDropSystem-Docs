# Transfer Pipeline

El sistema separa planning y execution.

## Planning

`TransferPlanner` construye un plan sin mutar el estado real.

Durante esta fase se comprueba:

- reglas
- capacidad
- conversiones
- swaps posibles
- colocacion alternativa

## Execution

`TransferPlanExecutor` aplica el plan validado.

Durante esta fase se manejan:

- commit real
- rollback
- hooks de dominio
- eventos diferidos

## Por que importa

Esta separacion hace que el preview de drag sea mas fiable y permite detectar fallos antes de tocar los datos reales.

## Archivos clave

- `TransferPlanner.cs`
- `TransferPlanExecutor.cs`
- `InventoryDropProcessor.cs`

