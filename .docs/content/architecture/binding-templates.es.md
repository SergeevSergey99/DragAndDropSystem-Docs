# Plantillas de binding

El asset incluye bases reutilizables para no tener que escribir cada binding desde cero.

## `ListInventoryDataBinding`

Util para:

- backpacks
- cofres
- inventarios lineales

Normalmente implementas:

- lectura de la lista
- creacion del adapter
- anadir item a los datos
- quitar item de los datos

## `SlotIndexedInventoryDataBinding`

Util para:

- hotbars
- crafting grids
- arrays fijos

Es apropiado cuando la posicion del slot importa.

## `MappedSlotInventoryDataBinding`

Util para:

- equipamiento
- slots con nombre
- layouts con reglas por slot

## Recomendacion

Elige la plantilla que mejor refleje la estructura real de tus datos. Si fuerzas el tipo equivocado, el binding se vuelve mas complejo de lo necesario.

