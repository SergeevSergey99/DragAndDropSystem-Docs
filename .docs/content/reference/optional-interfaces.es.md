# Interfaces opcionales

Ademas de `IItemAdapter`, el sistema puede usar interfaces opcionales para enriquecer el comportamiento.

## Interfaces mas importantes

- `IDescribable`: informacion ampliada para tooltip o vistas de detalle
- `IFilterable`: datos para filtros
- `ISortable`: datos para ordenacion
- `IStackSizeLimitable`: limite de stack a nivel de item

## Regla practica

Implementa solo las interfaces que realmente necesitas. No intentes meter toda la logica del dominio dentro del adapter.

