# Components - Detailed Documentation

Full technical documentation of all core components.

## DragAndDropManager

**Responsibility**: Orchestrates all drag & drop operations.

**Location**: → `Scripts/DragAndDropManager.cs`

**Key State**:
- `_currentContext` (DragContext) - Single source of truth
- `_transferService` (InventoryTransferService) - Atomic transactions
- `_globalRules` (GlobalRuleValidator) - Global validation
- `_dropTargetStack` (List<IDropTarget>) - Nested drop zones

**Key Methods**:
- `StartDrag(ISlot sourceSlot)` - Begin drag operation
- `CompleteDrag()` - Finish drag (drop)
- `TryAutoTransfer(...)` - Quick transfer with hotkey
- `PerformTransfer()` - Uses InventoryTransferService for atomic operations

**Events**:
- OnDragStarting, OnDragStarted
- OnDropAttempting, OnDropCompleted
- OnSwapAttempting, OnSwapCompleted
- OnAutoTransferCompleted

**Singleton + DI Support**: Uses conditional compilation for universal compatibility.

Check implementation in `Scripts/DragAndDropManager.cs`.

---

## InventoryTransferService (NEW!)

**Responsibility**: Atomic transaction handling for item transfers.

**Location**: → `Scripts/Inventories/InventoryTransferService.cs`

**Purpose**: Centralize transfer logic with automatic rollback on failure.

**How It Works**:
1. **Prepare** - Extract source/target from request, determine transfer amount
2. **Snapshot** - Capture state of both inventories
3. **Remove** - Remove from source slot
4. **Add** - Try add to target inventory
5. **Rollback** - On failure, restore snapshots
6. **Result** - Return InventoryTransferResult with all transaction data

**Request Struct**: `InventoryTransferRequest`
- Contains source/target inventories and slots
- `TargetSlot` can be null for area drops
- `AllowAlternativeSlots` bool for fallback behavior

**Result Struct**: `InventoryTransferResult`
- Contains all information about completed transfer
- Used for event dispatching
- Includes resolved target slot, item, amount, whether target was empty before

**Benefits**:
- ✅ **Atomicity**: Either all succeeds or all rolls back
- ✅ **Isolation**: Transfer logic in one place
- ✅ **Automatic Rollback**: No manual cleanup needed
- ✅ **Event Data**: Result contains everything for event subscribers

Check implementation in `Scripts/Inventories/InventoryTransferService.cs`.

---

## InventorySnapshot (NEW!)

**Responsibility**: Capture and restore inventory state for rollback.

**Location**: → `Scripts/Inventories/InventorySnapshot.cs`

**Struct**: `InventorySnapshot` (readonly)
- `SlotStacks` - ReadOnlyList of ItemStack for each slot
- `SlotCount` - Number of slots

**Interface**: `IInventorySnapshotProvider`
- `CaptureSnapshot()` - Create snapshot of current state
- `RestoreSnapshot(snapshot)` - Restore to captured state

**How UniversalInventory Implements It**:
- Creates deep copies of all ItemStacks
- Restores by calling `SetStack()` on each slot
- Used by InventoryTransferService for rollback

Check implementation in `Scripts/Inventories/InventorySnapshot.cs`.

---

## UniversalInventory

**Responsibility**: Manages slot collection using strategy pattern.

**Location**: → `Scripts/Inventories/UniversalInventory.cs`

**Key State**:
- `_slots` (List<ISlot>) - Slot collection
- `_strategy` (IInventoryStrategy) - Behavior (Unique/Stackable/Separable)
- `_ruleValidator` (InventoryRuleValidator) - Validation
- `_dataBinding` (InventoryDataBindingBase) - Optional external sync

**Configuration Enums**:
- `ItemBehaviorType`: Unique, Stackable, SeparableStacks
- `SlotManagementType`: Fixed, Dynamic

**Key Methods**:
- `TryAddItem(item, count)` - Add item (uses strategy)
- `TryRemoveItem(item, count)` - Remove item (uses strategy)
- `FindValidAutoTransferSlot(stack)` - Find slot for auto-transfer
- `CaptureSnapshot()` / `RestoreSnapshot()` - Rollback support

**Snapshot Support**: Implements `IInventorySnapshotProvider` for transaction rollback.

Check implementation in `Scripts/Inventories/UniversalInventory.cs`.

---

## ISlot (Abstract Class) ⚠️

**IMPORTANT**: Abstract class, NOT interface!

**Location**: → `Scripts/Slots/ISlot.cs`

**Why Abstract Class?**:
- Needs `MonoBehaviour` for Inspector references
- Provides default `Transform` property (virtual)
- Simplifies prefab assignment
- Allows virtual methods with default implementation

**Abstract Properties**:
- `Stack` - Current ItemStack (can be null!)
- `Index` - Slot index in inventory
- `IsEmpty` - Helper property (handles null check!)
- `Inventory` - Parent inventory reference
- `SlotRuleValidator` - Optional slot-specific rules

**Virtual Property**:
- `Transform` - Defaults to `MonoBehaviour.transform`, can override

**Abstract Methods**:
- `Initialize(index, inventory)` - Setup
- `SetStack(stack)` - Set contents
- `ReplaceItem(newItem)` - NEW! For trading/crafting
- `Clear()` - Remove contents
- `UpdateVisuals()` - Refresh UI

**UniversalSlot Implementation**:
**Location**: → `Scripts/Slots/UniversalSlot.cs`

Default implementation with Icon (Image) and Count (TMP_Text) visuals. Includes hover events, rule validation, and DataBinding integration.

Check implementations in `Scripts/Slots/`.

---

## SlotOperationContext (NEW!)

**Responsibility**: Pass context during slot operations for event suppression.

**Location**: → `Scripts/Inventories/SlotOperationContext.cs`

**Properties**:
- `SuppressEvents` - Flag to prevent duplicate events
- `ResolvedSlot` - Which slot was actually used
- `TargetWasEmptyBefore` - State before operation
- `TransferredAmount` - How many items moved

**Methods**:
- `ResetResult()` - Clear recorded data
- `RecordResult(slot, wasEmpty, amount)` - Store operation result

**Use Case**: Allows InventoryTransferService to suppress duplicate events. Transfer service fires its own events, so inventory shouldn't fire duplicates.

Check implementation in `Scripts/Inventories/SlotOperationContext.cs`.

---

## ItemStack (Mutable Wrapper)

**Responsibility**: Container for item + count with mutation methods.

**Location**: → `Scripts/Core/ItemStack.cs`

**Properties**:
- `Item` (IInventoryItem) - The item
- `Count` (int) - Quantity
- `IsEmpty` - Helper property (Item == null || Count <= 0)

**Key Methods**:
- `AddToStack(amount)` - Increase count (respects MaxStackSize)
- `RemoveFromStack(amount)` - Decrease count, returns actually removed
- `Split(amount)` - Create new stack with amount
- `ReplaceItem(newItem)` - NEW! Change item, keep count
- `CanStack(otherItem)` - Check if items can stack together

**Usage**: Mutable wrapper used throughout system. Be careful with references - changes affect all holders of the same instance.

Check implementation in `Scripts/Core/ItemStack.cs`.

---

**[Back to SKILL.md](./SKILL.md)**
