# Introduccion

Universal Drag & Drop System es un asset para Unity pensado para integrar inventarios con drag and drop sin obligarte a reescribir tu modelo de datos.

Sirve para:

- mostrar datos en UI de inventario
- mover objetos entre inventarios
- stacking, split, merge y swap
- sincronizar cambios del UI con tus datos de juego
- aplicar reglas y validaciones de transferencia
- añadir acciones, menu contextual, tooltips y seleccion multiple

## Que es este asset

No es solo un conjunto de slots visuales. La idea central es separar la capa visual del inventario de tus datos de juego.

La arquitectura gira alrededor de tres piezas:

- `UniversalInventory`: controla slots, transferencias y estado visual
- `IItemAdapter`: representa tus datos dentro del inventario
- `DataBinding`: sincroniza el inventario con tus datos reales

Gracias a esto el sistema puede trabajar con:

- `ScriptableObject`
- modelos runtime
- listas y estructuras indexadas
- slots fijos de equipamiento
- representaciones distintas del mismo item en inventarios distintos

## Coste de la flexibilidad

Para tipos de datos propios normalmente tendras que escribir una pequena capa de integracion:

- un adapter para exponer el item al sistema
- un binding para leer y escribir tus datos

Ese trabajo suele ser pequeno, pero es importante entender que este asset esta orientado a proyectos donde importa mas la extensibilidad que el zero-code setup.

## Subsystems incluidos

- inventarios list-based e indexados
- pipeline de transferencia con planning y ejecucion
- reglas globales, por inventario y por slot
- context menu
- tooltips
- seleccion multiple y multi-drag
- quick transfer
- filtros y ordenacion
- conversion de items entre inventarios
- ejemplos de world loot, trading, crafting y nested containers

## Leer despues

- [Quick Start](getting-started/quick-start.md)
- [Examples](examples/index.md)
- [Data Binding](architecture/data-binding.md)
- [Feedback](more/feedback.md)

