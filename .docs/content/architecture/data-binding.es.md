# Data Binding

`DataBinding` es la capa que conecta el inventario visual con tus datos reales.

## Responsabilidades

- leer los datos iniciales
- crear adapters para mostrarlos en slots
- escribir cambios de vuelta al modelo de datos
- recargar el UI cuando la fuente de datos cambia fuera del pipeline

## Tipos principales

- `InventoryDataBindingBase`: base comun
- `ListInventoryDataBinding`: listas
- `SlotIndexedInventoryDataBinding`: estructuras indexadas por slot
- `MappedSlotInventoryDataBinding`: slots semanticos o fijos

## Regla practica

Si el drag and drop funciona pero tus datos no cambian correctamente, casi siempre el problema esta en el binding y no en el planner.

## Donde empezar

- usa `ListInventoryDataBinding` para listas simples
- usa `SlotIndexedInventoryDataBinding` para hotbars y crafting grids
- usa `MappedSlotInventoryDataBinding` para equipamiento

