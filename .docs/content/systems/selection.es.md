# Selección

El sistema de selección permite elegir varios objetos y ejecutar una acción sobre ellos: arrastrar un grupo, limpiar la selección, seleccionar un rango o seleccionar todos los objetos de un inventario.

## Operaciones de selección

| Acción | Qué ocurre |
|---|---|
| Click | Limpia la selección anterior y selecciona un objeto |
| Ctrl + click | Añade el objeto a la selección o lo quita |
| Shift + click | Selecciona el rango desde el último slot seleccionado hasta el actual |
| Ctrl + A | Selecciona todos los slots no vacíos del inventario actual |
| Escape | Limpia la selección |

Los slots vacíos normalmente se ignoran. Así se evita seleccionar celdas vacías por accidente en vez de objetos.

## Arrastre de grupo

Si el jugador arrastra un objeto seleccionado, el sistema transfiere el grupo seleccionado.

Comportamiento importante:

- cada slot seleccionado se transfiere por separado
- el slot de destino se usa como punto inicial para buscar colocación
- los objetos que encuentran una posición válida se transfieren
- los objetos que no caben o no pasan validación se quedan donde estaban
- los objetos ya transferidos no se revierten porque falle un objeto posterior

Si la transferencia parcial está activada, de un stack puede moverse solo la cantidad que quepa.

Swap no se usa para transferencias de grupo. El grupo se coloca en slots libres o compatibles.

Después de una transferencia de grupo exitosa, la selección se limpia automáticamente.

## Varios inventarios

Por defecto se pueden seleccionar objetos de varios inventarios si tus operaciones de selección lo permiten.

Si no necesitas este escenario, activa `_restrictToSameInventory`. Entonces el grupo se forma solo desde un inventario.

## Configuración

1. Añade `SelectionManager` a la escena. Normalmente basta con una instancia por escena.
2. Añade `SlotSelectionView` al prefab del slot. Muestra el resaltado del estado seleccionado.
3. Configura las operaciones de selección mediante input:
   - `SelectionSlotAction` en `InteractionBindingsProfile` para Ctrl + click y Shift + click.
   - `InputActionSelectionTrigger` para hotkeys como Ctrl + A y Escape.
   - `ButtonSelectionTrigger` para botones UI como "Seleccionar todo" y "Limpiar selección".

## Qué comprobar

| Síntoma | Comprueba |
|---|---|
| El objeto no se añade a la selección | Si hay un `SelectionManager` en la escena |
| No se ve el resaltado | Si `SlotSelectionView` está en el prefab del slot |
| Ctrl/Shift no funcionan | Si las entradas `SelectionSlotAction` necesarias están configuradas en el perfil de input |
| Ctrl + A o Escape no funcionan | Si `InputActionSelectionTrigger` u otro trigger está conectado |
| No se transfiere todo el grupo | Si hay espacio en el inventario de destino y las rules pasan para cada objeto |

## Referencia de clases

| Clase | Rol |
|---|---|
| `SelectionManager` | Guarda la selección actual |
| `SelectionContext` | Estado de selección en el momento de lectura |
| `SlotSelectionView` | Resalta un slot cuando está seleccionado |
| `SelectionOperationBase` | Clase base para operaciones de selección |
| `ClearAndSelectOperation` | Click normal: limpiar todo y seleccionar un slot |
| `ToggleFilledSlotOperation` | Ctrl + click: alternar un slot no vacío |
| `RangeSelectOperation` | Shift + click: seleccionar un rango |
| `SelectAllOperation` | Ctrl + A: seleccionar todos los slots no vacíos |
| `ClearSelectionOperation` | Escape: limpiar selección |
| `SelectByConditionOperation` | Seleccionar por predicado |
| `SelectionSlotAction` | Acción de selección para pointer binding |
| `StartMultiDragAction` | Empieza el arrastre del grupo seleccionado |
| `ButtonSelectionTrigger` | Ejecuta una operación de selección desde un UI Button |
| `InputActionSelectionTrigger` | Ejecuta una operación de selección desde una Input System Action |
