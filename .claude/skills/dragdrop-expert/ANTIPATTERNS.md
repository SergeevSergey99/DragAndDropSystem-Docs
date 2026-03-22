# Anti-Patterns - Complete Catalog

Comprehensive catalog of anti-patterns and how to avoid them.

**Last Updated**: 2026-03-22

## Anti-Pattern #1: Adding Locks Instead of Checking DragContext

**Problem**: Adding `_isLocked` or `_isProcessing` flags when DragContext already tracks drag state.

**Why It's Bad**: Duplicates information that already exists in DragContext. Adds complexity, potential for bugs with flag management.

**Solution**: Use existing `DragContext.SourceSlot` to check if slot is in use.

**Check**: `Scripts/Core/DragContext.cs` for what's already tracked.

---

## Anti-Pattern #2: Complex Locking Mechanisms

**Problem**: Building queue systems with `_isProcessing` flags and pending operation queues.

**Why It's Bad**:
- This is single-threaded Unity, not distributed system
- Adds unnecessary complexity
- Hard to debug
- Can introduce deadlocks

**Solution**: Simple check against current drag context. If slot is being dragged, block operation on that slot. Allow parallel operations on different slots.

**Remember**: Only "concurrency" is manual drag + hotkey auto-transfer at same time. Simple slot check solves this.

---

## Anti-Pattern #3: Overthinking Concurrency

**Problem**: Building thread-safe queues, mutexes, locks for Unity game.

**Why It's Bad**: Unity is single-threaded! No need for thread synchronization.

**The Only "Concurrency"**:
- User dragging slot A while pressing hotkey to auto-transfer slot A
- Solution: Check `if (IsDragging && CurrentContext.SourceSlot == sourceSlot) return false;`

**Don't Build**: Thread-safe queues, mutexes, semaphores, locks
**Do Build**: Simple state checks

---

## Anti-Pattern #4: Modifying ItemStack During Validation

**Problem**: Calling `RemoveFromStack()` or other mutation methods inside rule validation.

**Why It's Bad**:
- Rules are read-only validation
- Should not have side effects
- Can corrupt state if validation fails later
- Violates single responsibility principle

**Solution**: Rules only validate and return RuleResult. Let strategies handle mutations.

**Check**: `Scripts/Rules/IDragRule.cs` for rule interface.

---

## Anti-Pattern #5: Mixing Slot-Specific Validation With Slot-Less Preview

**Problem**: Writing feature validation that assumes `context.TargetSlot` always exists, even during area-drop or inventory-level preview.

**Why It's Bad**:
- `TargetSlot` can be null during some preview stages
- generic preview should be driven by `InventoryAcceptanceRequest`, not ad-hoc binding guards
- feature code becomes brittle and starts duplicating infrastructure concerns

**Solution**:
- if logic is truly slot-specific, validate it only when a concrete slot is known
- for shared preview paths, rely on acceptance infrastructure (`InventoryAcceptanceRequest`, strategy slot iteration)
- use helper methods like `TryGetTargetBinding()` in mapped-slot bindings instead of raw dictionary lookup

**Common Places**:
- mapped-slot data bindings
- custom drop handlers
- manual preview code in area targets

---

## Anti-Pattern #6: Circular DataBinding Updates

**Problem**: `OnItemAddedToUI()` → Update external data → External data fires UI update → `TryAddItem()` → `OnItemAddedToUI()` → Infinite loop!

**Why It's Bad**: Crashes or freezes game.

**Solution**: Use `BeginSync()` scope in `ReloadUI()`:
- Wrap Data→UI sync code with `using (BeginSync()) { ... }` — exception-safe and supports nesting
- Base class `HandleItemAdded()`/`HandleItemRemoved()` automatically skips when `IsSyncing == true`
- Use `AddToUIQuiet()` helper which internally uses `BeginSync()`
- Do NOT check `IsSyncing` manually in subclass overrides — base class already handles this

**Note**: DataBinding is notified via direct calls from UniversalInventory (not events), but the
re-entrancy guard via `IsSyncing` still applies. The `HandleItemAdded()`/`HandleItemRemoved()` internal
methods check `IsSyncing` before calling virtual `OnItemAddedToUI()`/`OnItemRemovedFromUI()`.

**Check**: `Scripts/DataBinding/InventoryDataBindingBase.cs` for base class implementation.

---

## Anti-Pattern #7: Rule Priority Confusion

**Problem**: Setting `Priority = 100` thinking it's highest priority.

**Why It's Wrong**: 0 = highest priority (executes first), 100 = lowest priority (executes last).

**Solution**:
- Critical rules (empty slot check) → Priority = 0
- Important rules (max stack) → Priority = 10-50
- Optional rules (visual feedback) → Priority = 90-100

**Check**: `Scripts/Rules/IDragRule.cs` for priority system.

---

## Anti-Pattern #8: Checking IsEmpty Without Null Check

**Problem**: Accessing `slot.Stack.IsEmpty` directly.

**Why It Crashes**: `Stack` property can be null if slot never initialized.

**Solution**: Use `slot.IsEmpty` property instead (handles null check internally).

**Right Way**: `if (slot.IsEmpty) { ... }`
**Wrong Way**: `if (slot.Stack.IsEmpty) { ... }` ← Crashes if Stack is null!

---

## Anti-Pattern #9: Modifying Collection During Iteration

**Problem**: Calling `_slots.Remove(slot)` inside `foreach (var slot in _slots)` loop.

**Why It Crashes**: Collection modified during enumeration throws InvalidOperationException.

**Solutions**:
- **Option 1**: Collect slots to remove first, then remove
- **Option 2**: Use reverse for loop `for (int i = _slots.Count - 1; i >= 0; i--)`

**Check**: `Scripts/Inventories/UniversalInventory.cs` for proper iteration patterns.

---

## Anti-Pattern #10: Not Invalidating Cache

**Problem**: Modifying slot contents with `SetStack()` but forgetting to invalidate cached data.

**Why It's Bad**: Cache becomes stale, searches return wrong results, operations fail unexpectedly.

**Solution**:
- Call `InvalidateCache()` after modifying slots
- Or implement `MarkDirty(slot)` system for granular updates

**Note**: Currently no caching implemented (TODO), but important for future optimizations.

---

## Anti-Pattern #11: Breaking Asset Store Compatibility

**Problem**: Using C# 9+ features, requiring specific Unity version, forcing DI framework.

**Why It's Bad**: This is Asset Store product - must work in ANY Unity project!

**Don't Do**:
- Force pure DI (remove Singleton)
- Require external packages without `#if`
- Use C# 9+ language features
- Break existing public API

**Do Instead**:
- Support both Singleton AND DI
- Wrap optional features in `#if PACKAGE_NAME`
- Use C# 7.3 compatible code
- Maintain backward compatibility

**Check**: `Scripts/DragAndDropManager.cs` for conditional compilation pattern.

---

## Anti-Pattern #12: Direct Slot Manipulation Without Strategies

**Problem**: Calling `slot.SetStack()` directly instead of using strategy.

**Why It's Bad**:
- Bypasses validation rules
- Breaks strategy pattern
- Can corrupt inventory state
- Skips events and DataBinding

**Solution**: Use `inventory.TryAddItem()` or `strategy.TryAdd()` instead.

**Exception**: Internal strategy code can manipulate slots directly (it's the owner).

---

## Summary: Quick Check Before Coding

**Ask Yourself**:
1. Does DragContext already have this information?
2. Am I validating or mutating in rules? (should only validate!)
3. Did I check for null on `TargetSlot`?
4. Am I using `slot.IsEmpty` instead of `slot.Stack.IsEmpty`?
5. Will this work in any Unity project with any architecture?

**If uncertain** → Check actual script files for implementation patterns.

---

**[Back to SKILL.md](./SKILL.md)**
