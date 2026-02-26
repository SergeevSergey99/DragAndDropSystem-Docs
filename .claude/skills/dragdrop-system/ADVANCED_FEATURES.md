# Advanced Features

Detailed documentation of advanced system features.

## Quick Click Auto-Transfer (LMB)

**Problem**: Users want fast item transfer without dragging (Diablo-style).

**Solution**: Recognize quick click vs drag using time and distance thresholds.

**Location**: → `Scripts/DragAndDropManager.cs`, `Scripts/Slots/DragDropEventListener.cs`

**How It Works**:
1. User presses mouse button
2. System starts coroutine with timer
3. While timer runs, tracks mouse movement
4. If mouse moves beyond threshold → This is a drag!
5. If timer expires without movement → This is a click! Trigger auto-transfer

**Configuration Settings**:
- `_enableQuickClickAutoTransfer` - Enable/disable feature
- `_quickClickTimeThreshold` - Max click duration (default 0.2s)
- `_quickClickDistanceThreshold` - Max mouse movement (default 5 pixels)

Check `DragDropEventListener.DelayedDragStart()` coroutine for implementation.

---

## Animation System

**Purpose**: Animate items during auto-transfer for better visual feedback.

**Location**: → `Scripts/Core/AutoTransferAnimationStrategy.cs`

### Strategy Pattern

**Base Class**: `AutoTransferAnimationStrategy` (abstract)
- `AnimateTransfer()` method
- Returns GameObject for tracking
- Calls `onComplete` callback when animation finishes

### DOTween Implementation

**Class**: `TweenAutoTransferAnimation`
**Location**: → `Scripts/Core/TweenAutoTransferAnimation.cs`

**How It Works**:
1. Instantiate visual prefab
2. Position at source slot
3. Show item stack on visual
4. Tween position to target slot
5. On complete: invoke callback, destroy visual

**Configuration**:
- `_duration` - Animation duration
- `_ease` - Easing curve (OutQuad, InOutQuad, etc.)

**Conditional Compilation**: Uses `#if DOTWEEN` to work without DOTween installed.

Check implementation in `Scripts/Core/TweenAutoTransferAnimation.cs`.

---

## 3D World Integration

**Purpose**: Allow dropping items from inventory into 3D world and picking up 3D items.

**Location**: → `Scripts/World3D/`

### WorldDropZone

**Purpose**: 3D collider that acts as drop target.

**Location**: → `Scripts/World3D/WorldDropZone.cs`

**How It Works**:
- Implements `IDropTarget` interface
- Uses Physics raycast for drop detection
- Checks if mouse is over collider during drag
- Pushes itself as drop target to DragAndDropManager
- Returns target inventory (item goes into that inventory)

**Configuration**:
- `_targetInventory` - Where dropped items go
- `_collider` - Collider for detection

### WorldItem

**Purpose**: Item existing in 3D world that can be picked up.

**Location**: → `Scripts/World3D/WorldItem.cs`

**How It Works**:
- Holds reference to item (ItemSO)
- `OnPickup(inventory)` method
- Tries to add item to inventory
- Destroys GameObject if successful

**Usage**: Call `worldItem.OnPickup(playerInventory)` from interaction system.

### IWorld3DAdapter

**Interface for items that have 3D representation**.

**Location**: → `Scripts/World3D/IWorld3DAdapter.cs`

**Methods**:
- `GetWorldPrefab()` - Returns GameObject to spawn
- `GetWorldSpawnOffset()` - Offset from drop position

Check `Examples/Demo1/Adapters/ItemSOWith3DAdapter.cs` for example.

---

## Tooltip System

**Purpose**: Show item information on hover.

**Location**: → `Scripts/UI/TooltipManager.cs`, `Scripts/Core/ITooltipProvider.cs`

### ITooltipProvider Interface

**Items implement this** to provide tooltip data.

**Location**: → `Scripts/Core/ITooltipProvider.cs`

**Methods**:
- `GetTooltipTitle()` - Item name
- `GetTooltipDescription()` - Item description
- `GetTooltipIcon()` - Item icon

### TooltipManager Singleton

**Manages tooltip display**.

**Location**: → `Scripts/UI/TooltipManager.cs`

**Key Methods**:
- `Show(provider, position)` - Show tooltip for item
- `Hide()` - Hide tooltip

**Usage**: Call from hover listeners.

### SlotHoverEventListener

**Listens to slot hover events**.

**Location**: → `Scripts/Slots/SlotHoverEventListener.cs`

**How It Works**:
- Attached to slot GameObject
- OnPointerEnter → Check if item implements ITooltipProvider → Show tooltip
- OnPointerExit → Hide tooltip

Check `Examples/` for usage examples.

---

## Extension Points

### Custom Strategy

**Purpose**: Create custom inventory behavior (e.g., weight limits).

**How**:
1. Inherit from existing strategy (e.g., `StackableItemStrategy`)
2. Override `TryAdd()` and/or `TryRemove()`
3. Add custom validation logic
4. Call `base.TryAdd()` if validation passes

**Example Use Cases**:
- Weight limit system
- Volume-based inventory
- Custom stacking rules
- Durability tracking

Check `Scripts/Inventories/InventoryStrategy.cs` for base classes.

### Custom Rule

**Purpose**: Create custom validation rules (e.g., level requirements).

**How**:
1. Create class implementing `IDragRule`
2. Set `Priority` property (0 = highest)
3. Implement `CanStartDrag()` and `CanDrop()` methods
4. Return `RuleResult.Success()` or `RuleResult.Failure(reason)`
5. Add `[Serializable]` attribute for Inspector

**Example Use Cases**:
- Level requirements
- Class restrictions
- Quest requirements
- Zone restrictions

Check `Examples/Demo1/ItemTypeExampleFilterRule.cs` for example.

### Custom Action

**Purpose**: Create custom inventory operations (e.g., sell all, sort inventory).

**How**:
1. Inherit from `InventoryActionBase`
2. Override `Execute(inventory, activeSlot, logWarnings)` method
3. Check `if (dragManager.IsDragging)` before executing
4. Use `dragManager.TryAutoTransfer()` for item transfers
5. Return true if successful, false otherwise

**Example Use Cases**:
- Sell all items
- Sort inventory
- Quick stash
- Auto-organize

Check `Scripts/Actions/Inheritors/` for examples.

### Custom Visual

**Purpose**: Create custom drag visual appearance.

**How**:
1. Create MonoBehaviour implementing `IDragVisual`
2. Implement `Show(stack)`, `Hide()`, `UpdatePosition(position)` methods
3. Add visual elements (Image, Text, Particles, etc.)
4. Assign in DragAndDropManager inspector

**Example Use Cases**:
- Particle effects
- Custom animations
- Rarity highlighting
- Count badges

Check `Scripts/UI/` for visual examples.

---

**[Back to SKILL.md](./SKILL.md)**
