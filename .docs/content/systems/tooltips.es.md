# Tooltips

Los tooltips muestran informacion adicional del item sin acoplar el UI al modelo de datos.

## Piezas principales

- `TooltipManager`
- `BaseTooltipView`
- `DefaultTooltipView`
- `IDescribable`

## Patron recomendado

Haz que el adapter implemente `IDescribable` si necesitas contenido enriquecido. El tooltip deberia consumir interfaces, no tipos concretos del dominio.

