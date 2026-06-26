# Demo5 Containers

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/3BFn4jt9xhM"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo5 Containers/Containers Demo.unity`

Este es un ejemplo de items-inventario que contienen su propio conjunto de items anidado.

## Qué muestra la demo

- item instances en lugar de simples filas de lista
- un contenedor como item normal en el inventario del jugador
- un panel de UI separado para el contenido del contenedor activo
- acción de context menu para abrir un contenedor
- soltar un item directamente sobre el icono de un contenedor para meterlo dentro
- protección contra ciclos al anidar contenedores

## Cómo está estructurada

Datos:

- `Scripts/Data/ItemInstance.cs`
- `Scripts/Data/ContainerItemInstance.cs`
- `Scripts/Data/IContainerizeItemInstance.cs`
- `Scripts/ContainerDemoManager.cs`

Bindings y UI:

- `Scripts/Bindings/PlayerContainerInventoryDataBinding.cs`
- `Scripts/Bindings/ContainerInventoryDataBinding.cs`
- `Scripts/UI/ContainerUIController.cs`
- `Scripts/ContextMenu/OpenContainerMenuEntrySO.cs`

## Cómo funciona

Apertura de contenedor:

1. `ContainerItemInstance` está en el inventario del jugador.
2. El context menu llama a `OpenContainerMenuEntrySO`.
3. Mediante `Events.OnOpenClick`, el contenedor seleccionado se pasa a `ContainerUIController`.
4. El controller establece el contenedor activo.
5. `ContainerInventoryDataBinding` reconstruye el tamaño del inventario y carga el contenido del contenedor.

Movimiento de objetos:

1. Las operaciones drag/drop normales funcionan entre el inventario del jugador y el del contenedor.
2. Los bindings sincronizan la transferencia con la lista del jugador o `ContainerItemInstance.Items`.
3. Si se suelta un item sobre un slot que contiene un contenedor, `IPreRuleOccupiedSlotDropHandler` intercepta el intento antes de las reglas normales de drop.
4. El binding comprueba que el contenedor tenga capacidad y que el movimiento no coloque un contenedor dentro de sí mismo ni dentro de uno de sus hijos.
5. Si la comprobación pasa, `ContainerViewRegistry.InsertIntoContainer(...)` mete el item en el contenedor y devuelve `OccupiedSlotDropResult.Handled`.
6. Si la comprobación falla, el handler devuelve `Rejected` y el item se queda en su lugar original.

## Cuándo usar este ejemplo como base

- un item debe contener un inventario anidado
- necesitas context menu para operaciones sobre items
- soltar sobre un slot ya ocupado debe significar una acción aparte, por ejemplo "meter esto dentro del contenedor"
