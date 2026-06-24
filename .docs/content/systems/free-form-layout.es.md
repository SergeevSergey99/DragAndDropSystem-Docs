# Layout libre de slots

`FreeFormSlotLayout` muestra cómo crear un inventario donde el objeto aparece cerca del punto de drop, no en la siguiente celda de una cuadrícula.

Es un ejemplo de layout UI encima del comportamiento normal de transferencia. La transferencia de objetos no cambia: el inventario sigue decidiendo si puede aceptar el objeto, crea un slot y coloca el stack. `FreeFormSlotLayout` solo elige la posición en pantalla del slot creado.

## Qué hace

- el jugador suelta un objeto sobre el área del inventario
- el inventario crea un slot dinámico
- el slot se coloca cerca del punto de drop
- si la posición está ocupada, el slot se mueve a la posición libre más cercana
- al cargar o ejecutar `ReloadUI`, todos los slots se ordenan sin solaparse

## Cómo funciona

Las coordenadas del drop no se pasan a la lógica de transferencia. Se quedan en la capa UI.

El componente usa dos eventos:

| Evento | Para qué sirve |
|---|---|
| `UDNDEvents.OnDropAttempting` | Recordar la posición del mouse antes de procesar el drop |
| `UniversalInventory.OnSlotCreated` | Colocar el nuevo slot en la posición recordada |

Si un slot se crea fuera de un flujo de drop, el componente lo coloca con el pase de layout por defecto.

## Componentes

| Componente | Propósito |
|---|---|
| `FreeFormSlotLayout` | Posiciona slots creados dinámicamente y resuelve solapamientos |
| `InventoryDropArea` | Acepta drops sobre el área del inventario |
| `UniversalInventory` | Funciona en modo `Dynamic` y crea slots cuando hace falta |

## Configuración

### 1. Configura `UniversalInventory`

Define:

- **Slot Management** = `Dynamic`
- **Max Free Slots** = `0` si los slots deben aparecer solo después de drop
- **Max Dynamic Slots** = cantidad máxima de objetos para este inventario

### 2. Quita `LayoutGroup`

El contenedor de slots (`_slotContainer`) no debe tener `HorizontalLayoutGroup`, `VerticalLayoutGroup` ni `GridLayoutGroup`.

Unity `LayoutGroup` sobrescribe las posiciones de los hijos, así que entra en conflicto con un layout libre.

### 3. Añade `FreeFormSlotLayout`

Añade el componente al mismo GameObject que `UniversalInventory`.

Opciones:

| Campo | Qué hace |
|---|---|
| **UI Camera** | Cámara UI. Déjala vacía para Screen Space - Overlay |
| **Slot Spacing** | Separación mínima entre slots |
| **Bounds Override** | RectTransform que limita las posiciones. Si está vacío, se usa el contenedor de slots |

### 4. Añade `InventoryDropArea`

El `InventoryDropArea` estándar es suficiente. No necesitas un drop target propio para este escenario.

## Guardar posiciones

`FreeFormSlotLayout` puede convertir posiciones a coordenadas normalizadas `0..1`. Son cómodas para guardarlas en tus datos.

```csharp
// Save: local position -> normalized 0..1
Vector2 normalized = layout.GetNormalizedPosition(slot);
myModel.SavePosition(slot.Index, normalized);

// Load: normalized 0..1 -> local position
Vector2 local = layout.NormalizedToLocal(savedNormalized);
layout.SetSlotPosition(slot, local);
```

Las coordenadas normalizadas sobreviven a cambios de tamaño del contenedor: el slot se mantiene aproximadamente en el mismo lugar relativo al área del inventario.

## Resolver solapamientos

Si la posición de drop está ocupada, el componente revisa posiciones cercanas alrededor y elige la posición libre más cercana dentro de los límites permitidos.

Este comportamiento es suficiente para el ejemplo y para inventarios pequeños. Si necesitas otro algoritmo, usa los mismos eventos y cambia el cálculo de posición.

## Crear tu propio layout

Un layout propio normalmente sigue este patrón:

1. Crear un componente junto a `UniversalInventory`.
2. Suscribirse a `UniversalInventory.OnSlotCreated`.
3. Opcionalmente suscribirse a `UDNDEvents.OnDropAttempting` para recordar la posición de drop.
4. Recalcular todas las posiciones en `ArrangeAllSlots()` después de cargar o ejecutar `ReloadUI`.

```csharp
[RequireComponent(typeof(UniversalInventory))]
public class MyCustomLayout : MonoBehaviour
{
    private UniversalInventory _inventory;

    void Awake() => _inventory = GetComponent<UniversalInventory>();

    void OnEnable()
    {
        _inventory.OnSlotCreated += HandleSlotCreated;
    }

    void OnDisable()
    {
        _inventory.OnSlotCreated -= HandleSlotCreated;
    }

    void HandleSlotCreated(BaseSlot slot)
    {
        var rectTransform = slot.Transform as RectTransform;
        rectTransform.anchoredPosition = CalculatePosition(slot);
    }

    public void ArrangeAllSlots()
    {
        foreach (var slot in _inventory.Slots)
        {
            var rectTransform = slot.Transform as RectTransform;
            rectTransform.anchoredPosition = CalculatePosition(slot);
        }
    }

    Vector2 CalculatePosition(BaseSlot slot)
    {
        return Vector2.zero;
    }
}
```

## Ideas para otros layouts

| Opción | Idea |
|---|---|
| Circular | Colocar slots alrededor de un círculo |
| Snap Grid | Soltar libremente, pero ajustar la posición final a la celda más cercana |
| Physics | Dar comportamiento físico a los slots |
| Radial Menu | Colocar slots en abanico desde el centro |

## Referencia

| Clase | Rol |
|---|---|
| `FreeFormSlotLayout` | Componente de ejemplo para layout UI libre de slots |
| `UniversalInventory.OnSlotCreated` | Hook principal para posicionar un slot nuevo |
| `UniversalInventory.SlotContainer` | Contenedor usado para cálculos de coordenadas |
| `InventoryDropArea` | Área de drop estándar del inventario |
| `DynamicSlotManagementSettings` | Modo que crea slots cuando hace falta |
