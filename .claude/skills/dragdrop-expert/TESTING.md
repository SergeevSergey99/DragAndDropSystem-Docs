# Testing - Complete Scenarios

Comprehensive testing checklist and scenarios.

## Manual Testing Checklist

### Basic Drag & Drop
- [ ] Drag item from slot A to empty slot B
- [ ] Drag item from slot A to occupied slot B (same item, stackable)
- [ ] Drag item from slot A to occupied slot B (different item)
- [ ] Drag item from slot A to same slot A (should block - SameSlotRule)
- [ ] Press ESC during drag (should cancel)
- [ ] Drag item outside inventory (should cancel)

### Auto-Transfer
- [ ] Auto-transfer item (Shift+Click or quick click)
- [ ] Auto-transfer while dragging different item (should work)
- [ ] Auto-transfer while dragging SAME item (should block)
- [ ] Auto-transfer with no valid target (should fail gracefully)

### Concurrent Operations
- [ ] Drag slot 5 → try auto-transfer slot 5 → should BLOCK
- [ ] Drag slot 5 → try auto-transfer slot 7 → should WORK

### Dynamic Inventory
- [ ] Add items → slots auto-create
- [ ] Remove items → excess slots removed
- [ ] Verify slots never go below `_initialSlotCount`

### Rule Validation
- [ ] Drag to slot that fails SlotRule → should not highlight
- [ ] Drop to slot that fails validation → should show error message
- [ ] Drag item that fails InventoryRule → should block

### Nested Drop Zones
- [ ] Drop to InventoryDropArea (should find best slot)
- [ ] Drop to slot inside inventory area (should prioritize slot)

### Swap
- [ ] Drag item A to slot with item B (if `_autoSwapOnOccupiedSlot = true`)
- [ ] Verify both items swapped correctly

---

## Edge Cases

### Empty State
- [ ] Empty inventory (no slots created yet)
- [ ] Try to drag from empty slot (should block)
- [ ] Auto-transfer with no target inventory

### Full State
- [ ] Inventory at max capacity
- [ ] Try to add item to full inventory
- [ ] Relocation with complex rules

### Animation
- [ ] Multiple animations simultaneously
- [ ] Cancel drag during animation
- [ ] Auto-transfer spam (rapid clicks)

### DataBinding
- [ ] Circular updates (verify `_isSyncing` works)
- [ ] External data changes → UI updates
- [ ] UI changes → external data updates

### Prefab/Editor
- [ ] Prefab instantiation in edit mode
- [ ] Slot deleted while dragging
- [ ] Inventory destroyed during drag

---

## Snapshots & Rollback Testing

### Transfer Failures
- [ ] Transfer fails validation → source state restored ✅
- [ ] Transfer fails on target full → source state restored ✅
- [ ] Complex relocation fails → entire inventory restored ✅

### Verify Rollback

**Test procedure**:
1. Capture initial state
2. Attempt failing transfer
3. Verify state matches initial exactly

**Location**: Check `Scripts/Inventories/InventoryTransferService.cs` for rollback implementation.

---

## Performance Testing

### Small Inventories (< 50 slots)
- [ ] Drag & Drop: < 1ms
- [ ] Auto-Transfer: < 2ms
- [ ] UpdateAllVisuals: < 2ms

### Large Inventories (100+ slots)
- [ ] Drag & Drop: < 2ms
- [ ] Auto-Transfer: < 5ms
- [ ] Relocation: < 5ms (with attempt limit)
- [ ] UpdateAllVisuals: < 1ms (with dirty flags)

### Stress Test
- [ ] 10 simultaneous transfers → smooth execution
- [ ] Rapid spam clicks → no crashes
- [ ] 1000 items in inventory → no lag

---

## Integration Testing

### Demo1: Basic Inventory
- [ ] Drag item to weapon slot → only weapons allowed
- [ ] Drag potion to armor slot → should fail with message
- [ ] Dynamic inventory: add 20 items → slots auto-create

### Demo2: Trading System
- [ ] Buy item from merchant → money deducted
- [ ] Sell item to merchant → money added
- [ ] Try to buy with insufficient funds → blocked
- [ ] Drag from merchant A to merchant B → blocked

### Demo3: Loot System
- [ ] Walk to chest, press E → loot UI opens
- [ ] Loot items from chest → items removed
- [ ] Close loot UI → player can move again
- [ ] Open multiple chests → binding switches

---

## Regression Testing

After each change, verify:

### Core Operations
- [ ] Manual drag & drop still works
- [ ] Auto-transfer still works
- [ ] Swap still works
- [ ] Validation still works

### No New Bugs
- [ ] No console errors
- [ ] No NullReferenceExceptions
- [ ] No infinite loops
- [ ] No visual glitches

---

## Automated Testing (Optional)

Unity Test Framework can be used for unit tests.

**Example Test Structure**:

**Test Location**: `Tests/` directory (create if needed)

**Sample Test Cases**:
- `TryAdd_WithValidStack_ReturnsTrue()` - Test adding valid item
- `TryAutoTransfer_WhileDraggingSameSlot_ReturnsFalse()` - Test concurrent operation blocking
- `CaptureSnapshot_ThenRestore_MatchesOriginal()` - Test rollback system

**Key Components to Test**:
- `InventoryTransferService` - Transaction atomicity
- `InventoryStrategy` implementations - Behavior correctness
- `RuleValidator` - Validation logic
- `ItemStack` - Mutation methods

**Check**: Unity Test Framework documentation for setup and best practices.

---

**[Back to SKILL.md](./SKILL.md)**
