# Inicio rapido

Esta guia crea dos inventarios simples que permiten drag and drop entre si.

## Requisitos

- obligatorio: `Unity.ugui`
- opcional: `com.unity.inputsystem`

El flujo basico con raton funciona sin el nuevo Input System.

## Paso 1. Preparar la escena

1. Crea un `Canvas`.
2. Anade `DragAndDropManager` a la escena o usa `Prefabs/DragCanvas.prefab`.
3. Comprueba que existe un `EventSystem`.

## Paso 2. Crear dos inventarios

1. Crea un objeto `Backpack` dentro del `Canvas`.
2. Anade `UniversalInventory`.
3. Configura:

| Campo | Valor recomendado |
|---|---|
| `Item Behavior` | `Unique` para el caso mas simple |
| `Slot Management` | `Fixed` |
| `Initial Slot Count` | por ejemplo `10` |
| `Slot Prefab` | `Prefabs/Slot.prefab` |
| `Slot Container` | un objeto con `GridLayout` u otro layout |

4. Duplica el objeto para crear un segundo inventario, por ejemplo `Chest`.

## Paso 3. Definir el tipo de item

Necesitas un `IItemAdapter` que exponga al menos:

- `ItemId`
- `Icon`
- `DisplayName`

`ItemId` controla la semantica de stacking. Si esta mal definido, los items se uniran o separaran de forma incorrecta.

## Paso 4. Crear un binding

Para un inventario basado en lista, normalmente se usa `ListInventoryDataBinding<TData, TAdapter>`.

Debes implementar:

- `GetItems()`
- `CreateAdapter()`
- `AddToData()`
- `RemoveFromData()`

El binding es el puente entre el UI y tus datos.

## Paso 5. Cargar datos iniciales

1. Crea algunos items.
2. Ponlos en la fuente de datos del binding.
3. Ejecuta la escena.

Si todo esta correcto:

- ambos inventarios muestran items
- puedes arrastrarlos entre inventarios
- la fuente de datos se actualiza

## Errores comunes

- no hay `DragAndDropManager` en la escena
- no hay `EventSystem`
- el binding apunta al inventario equivocado
- el `ItemId` no coincide con la logica de stacking
- los datos cambian fuera del pipeline y no llamas a `ReloadUI()`

## Siguiente paso

- [Examples](../examples/index.md)
- [Data Binding](../architecture/data-binding.md)
- [Troubleshooting](../reference/troubleshooting.md)

