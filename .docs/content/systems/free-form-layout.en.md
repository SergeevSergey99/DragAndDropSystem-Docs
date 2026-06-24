# Free-Form Slot Layout

`FreeFormSlotLayout` shows how to build an inventory where an item appears near the drop point instead of the next grid cell.

This is a UI layout example on top of normal transfer behavior. Item transfer does not change: the inventory still decides whether it can accept the item, creates a slot, and places the stack. `FreeFormSlotLayout` only chooses the screen position for the created slot.

## What It Does

- the player drops an item on the inventory area
- the inventory creates a dynamic slot
- the slot is placed near the drop point
- if the position is occupied, the slot is moved to the nearest free position
- on load or `ReloadUI`, all slots are arranged without overlap

## How It Works

Drop coordinates are not passed into transfer logic. They stay in the UI layer.

The component uses two events:

| Event | Purpose |
|---|---|
| `UDNDEvents.OnDropAttempting` | Remember the mouse position before drop processing |
| `UniversalInventory.OnSlotCreated` | Place the new slot at the remembered position |

If a slot is created outside a drop flow, the component arranges it with the default layout pass.

## Components

| Component | Purpose |
|---|---|
| `FreeFormSlotLayout` | Positions dynamically created slots and resolves overlap |
| `InventoryDropArea` | Accepts drops on the inventory area |
| `UniversalInventory` | Runs in `Dynamic` mode and creates slots when needed |

## Setup

### 1. Configure `UniversalInventory`

Set:

- **Slot Management** = `Dynamic`
- **Max Free Slots** = `0` if slots should appear only after drop
- **Max Dynamic Slots** = maximum item count for this inventory

### 2. Remove `LayoutGroup`

The slot container (`_slotContainer`) must not have `HorizontalLayoutGroup`, `VerticalLayoutGroup`, or `GridLayoutGroup`.

Unity `LayoutGroup` overwrites child positions, so it conflicts with free-form layout.

### 3. Add `FreeFormSlotLayout`

Add the component to the same GameObject as `UniversalInventory`.

Settings:

| Field | What it does |
|---|---|
| **UI Camera** | UI camera. Leave empty for Screen Space - Overlay |
| **Slot Spacing** | Minimum spacing between slots |
| **Bounds Override** | RectTransform that bounds slot positions. If empty, the slot container is used |

### 4. Add `InventoryDropArea`

The standard `InventoryDropArea` is enough. You do not need a custom drop target for this scenario.

## Saving Positions

`FreeFormSlotLayout` can convert positions to normalized `0..1` coordinates. These are convenient to store in your data.

```csharp
// Save: local position -> normalized 0..1
Vector2 normalized = layout.GetNormalizedPosition(slot);
myModel.SavePosition(slot.Index, normalized);

// Load: normalized 0..1 -> local position
Vector2 local = layout.NormalizedToLocal(savedNormalized);
layout.SetSlotPosition(slot, local);
```

Normalized coordinates survive container size changes: the slot stays in roughly the same place relative to the inventory area.

## Overlap Resolution

If the drop position is occupied, the component checks nearby positions around it and chooses the nearest free position inside the allowed bounds.

This behavior is suitable for the example and small inventories. If you need another algorithm, use the same events and replace the position calculation.

## Building Your Own Layout

A custom layout usually follows this pattern:

1. Create a component next to `UniversalInventory`.
2. Subscribe to `UniversalInventory.OnSlotCreated`.
3. Optionally subscribe to `UDNDEvents.OnDropAttempting` to remember the drop position.
4. Recalculate all slot positions in `ArrangeAllSlots()` after load or `ReloadUI`.

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

## Ideas For Other Layouts

| Option | Idea |
|---|---|
| Circular | Place slots around a circle |
| Snap Grid | Drop freely, but snap the final position to the nearest cell |
| Physics | Give slots physics behavior |
| Radial Menu | Place slots in a fan from the center |

## Reference

| Class | Role |
|---|---|
| `FreeFormSlotLayout` | Example component for free-form slot UI layout |
| `UniversalInventory.OnSlotCreated` | Main hook for positioning a new slot |
| `UniversalInventory.SlotContainer` | Container used for coordinate calculations |
| `InventoryDropArea` | Standard inventory drop area |
| `DynamicSlotManagementSettings` | Mode that creates slots when needed |
