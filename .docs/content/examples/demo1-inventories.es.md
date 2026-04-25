# Demo1 Inventories

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/96VOcrIeLUk"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo1 Inventories/InventoriesDemo.unity`

Esta es la muestra más básica del asset. Muestra un inventario sin lógica de juego separada, economía o integración con el mundo.

## Qué muestra la demo

- una simple `List<ItemExampleSO>`
- `ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>`
- cargar una lista en `UniversalInventory`
- sincronizar los cambios de la UI de vuelta a los datos
- overrides locales de `CanStartDrag` y `CanDrop`

## Cómo está estructurada

Piezas principales:

- `ItemExampleSO.cs` — datos del item
- `Adapters/ItemAdapterSoAdapter.cs` — adapter de la capa UI
- `DataBindings/ItemsSOInventoryDataBinding.cs` — binding entre la lista de datos y el inventario
- `SO/Rules/*` — preset de reglas de ejemplo

Arquitectónicamente, esta es la cadena más corta del proyecto:

```mermaid
flowchart LR
    Data["List<ItemExampleSO>"] <--> Binding["ItemsSOInventoryDataBinding"] <--> UI["UniversalInventory"]
```

Aquí no existe un servicio de dominio separado. El binding lee la lista, crea adapters y sincroniza los cambios de vuelta a la misma lista.

## Cómo funciona

1. `GetItems()` devuelve la lista `items`.
2. `ReloadUI()` construye stacks de UI mediante `CreateAdapter(...)`.
3. El drag and drop pasa por el pipeline estándar.
4. Tras una transferencia exitosa, el binding recibe callbacks de add/remove.
5. `AddToData(...)` y `RemoveFromData(...)` actualizan la lista de origen.

La demo también muestra dónde encajan mejor las restricciones locales sencillas:

- `CanStartDrag(...)` — bloquear el drag desde un inventario concreto
- `CanDrop(...)` — bloquear el drop en un inventario concreto

## Archivos para inspeccionar

| Archivo | Rol |
|---|---|
| `DataBindings/ItemsSOInventoryDataBinding.cs` | binding principal del ejemplo |
| `Adapters/ItemAdapterSoAdapter.cs` | adapter para `ItemExampleSO` |
| `ItemExampleSO.cs` | modelo de datos del item |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | clase base para bindings basados en listas |

## Cuándo usar esto como punto de partida

- necesitas un primer inventario sin un modelo de dominio complejo
- quieres entender el lifecycle de `ListInventoryDataBinding`
- quieres un lugar rápido donde probar reglas o configuración visual

