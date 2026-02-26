# Core Concepts - Detailed Guide

This file contains in-depth documentation of core system concepts.

## Table of Contents

1. [DragContext - Single Source of Truth](#dragcontext---single-source-of-truth)
2. [Three-Level Rule Validation](#three-level-rule-validation)
3. [Strategy Pattern - Item Behavior](#strategy-pattern---item-behavior)
4. [InventoryTransferService](#inventorytransferservice)
5. [ISlot - Abstract Class](#islot---abstract-class)
6. [Dynamic Slot Management](#dynamic-slot-management)
7. [DataBinding System](#databinding-system)

---

## DragContext - Single Source of Truth

**THE GOLDEN RULE**: Always check DragContext before adding new state!

### What It Contains

**Location**: → `Scripts/Core/DragContext.cs`

**Properties**:
- `DraggedStack` - What's being dragged
- `SourceSlot` - Where from
- `SourceInventory` - Source inventory
- `TargetSlot` - Where to (can be null!)
- `TargetInventory` - Target inventory (can be null!)
- `IsSameSlot` - Helper property
- `IsSameInventory` - Helper property
- `HasTarget` - Helper property

### Common Usage Patterns

**Check if slot is being dragged**:
- Use `_dragManager.IsDragging && _dragManager.CurrentContext.SourceSlot == slot`
- Returns false if slot is already in use

**Get current drag info**:
- Check `IsDragging` first
- Access `CurrentContext.DraggedStack.Item` and `.Count`
- Access `CurrentContext.SourceInventory`

**Null check for TargetSlot**:
- ALWAYS check `if (context.TargetSlot != null)` before accessing
- `TargetSlot` can be null for area drops!

### Why This Is Better Than Adding Locks

Instead of adding `_isLocked` or `_isProcessing` flags, use existing DragContext state. The system already tracks what slot is being dragged - don't duplicate this information.

Check `Scripts/Core/DragContext.cs` for full implementation.

---

## Three-Level Rule Validation

**Validation Pipeline**:
```
User Action
    ↓
Global Rules (DragAndDropManager)
    ↓ (if valid)
Inventory Rules (UniversalInventory)
    ↓ (if valid)
Slot Rules (UniversalSlot)
    ↓ (if all valid)
Execute Operation
```

**Location**: → `Scripts/Rules/IDragRule.cs`, `Scripts/Rules/RuleValidator.cs`

### How Rules Work

**Rule Interface**:
- `Priority` property (0 = highest, 100 = lowest)
- `CanStartDrag(DragContext context)` method
- `CanDrop(DragContext context)` method
- Return `RuleResult.Success()` or `RuleResult.Failure(reason)`

**Built-in Rules**: → `Scripts/Rules/BuiltInRules.cs`

### Creating Custom Rules

1. Implement `IDragRule` interface
2. Set Priority (lower number = executes first)
3. Return RuleResult from validation methods
4. **IMPORTANT**: Rules are read-only! Never modify data during validation

**Priority System**:
- **0** = highest priority (executes first)
- **100** = lowest priority
- Early exit on first failure for performance

Check `Scripts/Rules/` directory for rule examples.

---

## Strategy Pattern - Item Behavior

**Location**: → `Scripts/Inventories/InventoryStrategy.cs`

### Strategy Types

**UniversalInventory uses composition**:
- `ItemBehaviorType` enum: Unique, Stackable, SeparableStacks
- Strategy created automatically based on enum selection
- Can wrap in `DynamicSlotDecorator` for dynamic slot management

### 1. UniqueItemStrategy

**Behavior**: One item per slot, count always 1

**Use Case**: Equipment slots, unique items, character inventories

**How it works**: Always creates single-item stacks, finds first empty slot

### 2. StackableItemStrategy

**Behavior**: Automatic merging of same items

**Use Case**: Resources, consumables, Minecraft-style inventories

**How it works**:
1. Fill existing stacks first
2. Create new stacks in empty slots if needed
3. Respects max stack size

### 3. SeparableStacksStrategy (NEW!)

**Behavior**: Heroes of M&M style - multiple stacks allowed, merge only on explicit drop

**Key Difference**: Does NOT auto-merge with `TryAddItem()`, only on explicit drop

**Use Case**: Strategy games, tactical RPGs, when player wants control over stack separation

**Configuration**: `_allowMergeOnDrop` bool controls whether merge on drop is allowed

Check `Scripts/Inventories/InventoryStrategy.cs` for implementation details.

---

## InventoryTransferService

**Problem**: Transfer logic was scattered between Manager and Inventory.
**Solution**: Centralized service for atomic transfers.

**Location**: → `Scripts/Inventories/InventoryTransferService.cs`

### How It Works

**Transaction Pipeline**:
1. Capture snapshots of both inventories (rollback protection)
2. Remove from source
3. Try add to target
4. On failure - rollback via snapshots
5. Return result for event dispatching

### Request/Result Structs

**InventoryTransferRequest**:
- Contains source/target inventories and slots
- `TargetSlot` can be null for area drops
- `AllowAlternativeSlots` bool for fallback behavior

**InventoryTransferResult**:
- Contains all information about completed transfer
- Used for event dispatching
- Includes resolved target slot, item, amount, etc.

### Benefits

- ✅ **Atomicity**: Either all succeeds or all rolls back
- ✅ **Isolation**: Transfer logic in one place
- ✅ **Snapshots**: Automatic rollback on error
- ✅ **Events**: Result contains all data for event subscribers

Check `Scripts/Inventories/InventoryTransferService.cs` for full implementation.

---

## ISlot - Abstract Class

**IMPORTANT CHANGE**: `ISlot` is now **abstract class**, not interface!

**Location**: → `Scripts/Slots/ISlot.cs`

### Why Abstract Class?

- Needs `MonoBehaviour` for Inspector references
- Provides default `Transform` property (virtual)
- Simplifies prefab assignment in Unity Inspector
- Allows virtual methods with default implementation

### Abstract Members

**Properties**:
- `Stack` - Current ItemStack
- `Index` - Slot index in inventory
- `IsEmpty` - Helper property (handles null check!)
- `Transform` - Virtual property, defaults to MonoBehaviour.transform
- `Inventory` - Parent inventory reference
- `SlotRuleValidator` - Optional slot-specific rules

**Methods**:
- `Initialize(index, inventory)` - Setup
- `SetStack(stack)` - Set contents
- `ReplaceItem(newItem)` - NEW! For trading/crafting
- `Clear()` - Remove contents
- `UpdateVisuals()` - Refresh UI

### UniversalSlot Implementation

**Location**: → `Scripts/Slots/UniversalSlot.cs`

Default implementation with Icon (Image) and Count (TMP_Text) visuals.

### ReplaceItem Use Case

**Trading scenario**: Replace item in slot while preserving count. Useful when different players/merchants have different prices or stats for same base item.

Check `Scripts/Slots/ISlot.cs` and `Scripts/Slots/UniversalSlot.cs` for implementation.

---

## Dynamic Slot Management

**Location**: → `Scripts/Inventories/InventoryStrategy.cs` (DynamicSlotDecorator)

### Two Creation Modes

**MODE 1: Explicit drag to specific slot**:
- Creates slots UP TO targetIndex only if `_maxFreeSlots > 0`
- Prevents infinite slot growth from spam clicking

**MODE 2: TryAddItem (programmatic addition)**:
- ALWAYS creates slots when needed
- Ensures programmatic additions never fail

### EnsureFreeSlots

Called after adding items. Ensures minimum number of free slots (`_maxFreeSlots`) are available for drag operations.

### TrimExcessFreeSlots

Called after removing items:
- Removes excess empty slots
- NEVER removes below `_initialSlotCount`
- Removes from end of list

**Configuration**:
- `_initialSlotCount` - Minimum slots to keep
- `_maxDynamicSlots` - Maximum total slots
- `_maxFreeSlots` - Target number of empty slots

Check `Scripts/Inventories/InventoryStrategy.cs` for DynamicSlotDecorator implementation.

---

## DataBinding System

**Purpose**: Link UI inventory with external data (GameManager, SaveData, Trading, etc)

**Location**: → `Scripts/DataBinding/InventoryDataBindingBase.cs`

### Architecture

```
External Data (GameManager, SaveData, etc)
    ↕ (bidirectional sync)
DataBinding (Adapter)
    ↓
UniversalInventory (UI)
```

**Flow**:
1. External data changes → `DataBinding.SyncToUI()` → `inventory.TryAddItem()` with `_isSyncing=true`
2. User drags item in UI → `inventory.OnItemAdded` event → If `!_isSyncing` → `DataBinding.OnItemAddedToUI()` → Update external data

### Base Class

**Abstract methods to implement**:
- `OnItemAddedToUI(item, count)` - User added item via UI
- `OnItemRemovedFromUI(item, count)` - User removed item via UI
- `SyncToUI()` - Load external data into UI

**Optional validation**:
- `CanStartDragInternal(context)` - Custom drag validation
- `CanDropInternal(context)` - Custom drop validation

### Preventing Circular Updates

**The Problem**: External data update → UI update → External data update → infinite loop!

**The Solution**: Use `_isSyncing` flag:
- Set `_isSyncing = true` before calling `inventory.TryAddItem()` in `SyncToUI()`
- Check `if (_isSyncing) return;` in `OnItemAddedToUI()` and `OnItemRemovedFromUI()`

### Example Implementations

Check examples:
- `Examples/Demo1/DataBindings/ItemsSOInventoryDataBinding.cs` - Simple binding to List
- `Examples/Demo2 Trading/DataBindings/PlayerInventoryDataBinding.cs` - With money validation
- `Examples/Demo3 Loot/DataBindings/ChestInventoryDataBinding.cs` - Dynamic binding

---

**[Back to SKILL.md](./SKILL.md)**
