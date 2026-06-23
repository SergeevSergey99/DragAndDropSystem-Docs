# Demo3 Craft

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/lbcTdQGJTIs"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo3 Craft/CraftDemo.unity`

Este es un ejemplo de inventario indexado por slot y crafting donde la UI se sincroniza
con arrays de datos fijos, no con una lista.

## Qué muestra la demo

- `SlotIndexedInventoryDataBinding`
- mesa de crafting y slot de resultado
- límite máximo de stack por slot
- `CraftResultDataBinding` como tipo de inventario personalizado para tomar el resultado de crafting

## Cómo está estructurada

El punto central de la lógica es:

- `Crafting/CraftingManager.cs`

Almacena:

- `_hotbarItems`
- `_inventoryItems`
- `_craftTableItems`
- lista de recetas
- resultado actual y multiplicador de crafting

La UI está dividida en cuatro bindings independientes:

- `MainInventoryDataBinding`
- `HotbarDataBinding`
- `CraftTableDataBinding`
- `CraftResultDataBinding`

## Cómo funciona

Slots normales:

1. El binding enumera los índices ocupados mediante `GetOccupiedSlots()`.
2. `AddToSlotData(...)` y `RemoveFromSlotData(...)` llaman a métodos de `CraftingManager`.
3. En `Awake()`, los inventarios llaman a `SetMaxStackSize(CraftingManager.MaxItemsPerSlot)` para limitar la cantidad de objetos por slot.

Crafting:

1. Cambiar la craft table actualiza `_craftTableItems`.
2. `CraftingManager.RefreshCraftResult()` busca una receta adecuada.
3. `CraftResultDataBinding.OnReloadUI()` muestra el resultado y configura el paso de drag.
4. Cuando el usuario toma el resultado, `OnItemRemovedFromUI(...)` llama a `ConsumeCraftIngredients(...)`.

Es un buen ejemplo de slot de solo lectura que no acepta drop entrante, pero crea un
objeto según lógica externa que se puede extraer.

## Cuándo usar este ejemplo como base

- necesitas un inventario con índices fijos
- necesitas crafting
- necesitas un slot de salida con un efecto después de extraer
