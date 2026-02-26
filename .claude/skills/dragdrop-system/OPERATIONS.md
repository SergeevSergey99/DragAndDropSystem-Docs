# Operations - Detailed Flow Diagrams

Detailed breakdowns of all drag & drop operations.

## Manual Drag & Drop Flow

```
1. USER CLICK
   DragDropEventListener.OnBeginDrag()
   ↓
2. START DRAG
   DragAndDropManager.StartDrag(sourceSlot)
   ├─ Create DragContext(stack, sourceSlot, sourceInventory)
   ├─ Event: OnDragStarting (cancelable)
   ├─ Validate: GlobalRules.ValidateStartDrag()
   ├─ Validate: InventoryRules.ValidateStartDrag()
   ├─ Create visual: GetDragVisual()
   └─ Event: OnDragStarted

3. HOVER TARGET
   DragDropEventListener.OnPointerEnter()
   ↓
   DragAndDropManager.PushDropTarget(dropTarget)
   ├─ Add to _dropTargetStack
   └─ SetHoveredSlot(slot, inventory)
       ├─ Update DragContext.TargetSlot/TargetInventory
       ├─ Validate: CanDropToSlot()
       │   ├─ GlobalRules.ValidateDrop()
       │   ├─ InventoryRules.ValidateDrop()
       │   └─ SlotRules.ValidateDrop()
       └─ If valid → Highlight(true)

4. DROP
   Input.GetMouseButtonUp(0)
   ↓
   DragAndDropManager.CompleteDrag()
   ├─ Event: OnDropAttempting (cancelable)
   ├─ Call: PerformTransfer()
   │   ├─ Create InventoryTransferRequest
   │   ├─ _transferService.TryExecuteTransfer(request, out result)
   │   ├─ If success:
   │   │   ├─ DispatchTransferEvents(result)
   │   │   └─ sourceInventory.HandleSlotEmptied(sourceSlot)
   │   └─ If failed: OnDragCancelled
   └─ EndDrag()
```

**Check implementation**: → `Scripts/DragAndDropManager.cs`, `Scripts/Slots/DragDropEventListener.cs`

---

## Auto-Transfer Flow (Shift+Click, Quick Click)

```
1. TRIGGER
   User presses hotkey OR quick click
   ↓
   AutoTransferAction.Execute(inventory, slot)
   ↓
2. VALIDATION
   DragAndDropManager.TryAutoTransfer(sourceSlot, sourceInv, targetInv)
   ├─ Check: IsDragging && CurrentContext.SourceSlot == sourceSlot? → BLOCK
   ├─ Create DragContext
   ├─ Event: OnAutoTransferAttempting (cancelable)
   ├─ Validate: GlobalRules.ValidateStartDrag()
   ├─ Validate: SourceRules.ValidateStartDrag()
   └─ Validate: TargetRules.ValidateDrop()

3. FIND SLOT
   FindValidAutoTransferSlot(targetInventory, transferStack)
   ├─ Try stacking in existing slots (if not Unique mode)
   └─ Try empty slots that pass SlotRules

4. TRANSFER
   _transferService.TryExecuteTransfer(request, out result)
   ├─ If animation strategy exists:
   │   ├─ Hide targetSlot icon
   │   ├─ Animate visual from source to target
   │   └─ On complete:
   │       ├─ Show targetSlot icon
   │       ├─ Event: OnDropCompleted
   │       └─ Event: OnAutoTransferCompleted
   └─ Else (no animation):
       ├─ Event: OnDropCompleted
       └─ Event: OnAutoTransferCompleted
```

**Check implementation**: → `Scripts/Actions/Inheritors/AutoTransferAction.cs`, `Scripts/DragAndDropManager.cs`

---

## Swap Operation

```
1. VALIDATION
   ValidateSwap(dragContext, targetSlot, out reverseContext)
   ├─ Check: Can drag from targetSlot? (ValidateStartDrag)
   ├─ Check: Can drop target item to source? (ValidateDrop reverseContext)
   └─ Check: Can drop source item to target? (ValidateDrop dragContext)

2. EVENT
   OnSwapAttempting.Invoke(swapEventArgs)
   ├─ Cancel? → return false

3. EXECUTE
   targetInventory.TrySwapSlots(targetSlot, sourceSlot, out swapResult)
   ├─ Backup stacks
   ├─ Swap: targetSlot.SetStack(sourceStack), sourceSlot.SetStack(targetStack)
   └─ UpdateVisuals()

4. EVENTS
   DispatchSwapEvents(swapResult)
   ├─ OnItemRemoved for both slots
   └─ OnItemAdded for both slots
```

**Check implementation**: → `Scripts/DragAndDropManager.cs`, `Scripts/Inventories/UniversalInventory.cs`

---

## Quick Click Auto-Transfer Recognition

**Problem**: Distinguish between click (auto-transfer) and drag (manual drag).

**Solution**: Delayed drag start with timeout and distance threshold.

**Algorithm**:
1. User presses mouse button down
2. Start coroutine with timer
3. While time < `quickClickTimeThreshold`:
   - If mouse moves > `quickClickDistanceThreshold` pixels → Start drag
   - Continue waiting
4. If timeout reached without significant movement → Trigger auto-transfer

**Configuration**:
- `_quickClickTimeThreshold`: Default 0.2 seconds
- `_quickClickDistanceThreshold`: Default 5 pixels
- `_enableQuickClickAutoTransfer`: Bool to enable/disable

**Location**: → `Scripts/Slots/DragDropEventListener.cs` (DelayedDragStart coroutine)

---

## Relocation System (TryRelocateAndRetry)

**Purpose**: Reorganize inventory to make space for new item when target slot is occupied.

**Algorithm**:
1. Capture snapshot of inventory (rollback protection)
2. Build list of candidate slots (target slot + other valid slots)
3. Priority order:
   - Target slot (if specified)
   - Slots that accept incoming item (by rules)
   - All other occupied slots
4. For each candidate:
   - Restore snapshot (reset before each attempt)
   - Try to relocate occupant to another slot
   - If successful, try add incoming item
   - If item added successfully → return true
5. Final rollback if all attempts fail

**Complexity**: O(n² × attempts) - for 100+ slots needs optimization!

**Location**: → `Scripts/Inventories/UniversalInventory.cs`

---

**[Back to SKILL.md](./SKILL.md)**
