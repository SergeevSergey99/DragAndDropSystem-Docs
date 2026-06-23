# Demo2 Loot

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/Q1B4ef6LyTU"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo2 Loot/LootDemo.unity`

Este es un ejemplo de inventario indexado por slot y de conexión entre el mundo del
juego, eventos y UI.

## Qué muestra la demo

- cofres como objetos interactivos del mundo
- bindings separados para jugador y cofre
- cambio de distintas fuentes de datos para DataBinding
- escenarios de pickup/drop conectados con el mundo
- el mundo no conoce directamente la UI

## Cómo está estructurada

El punto clave es que `Chest` y `PlayerInteraction` viven en el mundo del juego y no
conocen paneles de UI concretos. La apertura y vinculación de UI la gestiona
`LootUIController`.

## Cómo funciona

Apertura de cofre:

1. `PlayerInteraction` encuentra un `IInteractable`.
2. La acción del jugador llama a `Interact(...)`.
3. `Chest` cambia de estado y publica un evento.
4. `LootUIController` recibe el evento.
5. El controller activa el panel y llama a `BindToChest(...)`.
6. `ChestInventoryDataBinding` carga el contenido del cofre en la UI.

Movimiento de objetos:

1. El jugador arrastra objetos entre el inventario del jugador y el inventario del cofre.
2. El transfer pipeline estándar realiza la transferencia.
3. Tras el commit exitoso, los bindings actualizan `Chest` y `PlayerInventoryData`.

## Cuándo usar este ejemplo como base

- necesitas cofres que se abren mediante interacción
- necesitas un ejemplo de separación entre lógica del mundo y UI de inventario
