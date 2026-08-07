# Anti-Patterns - Complete Catalog

Comprehensive catalog of anti-patterns and how to avoid them.

**Last Updated**: 2026-08-06

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

## Anti-Pattern #13: Using CanSwap to Intercept Occupied Slot Drops

**Problem**: Overriding `CanSwap()` in DataBinding to detect an occupied-slot drop, perform the actual mutation (e.g. insert into container), then return `RuleResult.Failure` to suppress the swap.

**Why It's Bad**:
- `CanSwap` is a validation hook — it must never mutate state
- returning Failure after mutation leaves source slot cleared and target unchanged → inconsistent state
- suppresses the swap entirely, so changing `BlockedTargetBehavior` to `FindAlternative` or `Reject` later has no effect — the behavior is hardcoded inside the validation path
- the transfer pipeline cannot roll back mutations done inside a rule check

**Solution**: Implement the proper occupied-slot handler timing interface on the target DataBinding:

```csharp
public class ContainerBinding : SlotIndexedInventoryDataBinding<ItemModel, ItemModelAdapter>,
    IPreRuleOccupiedSlotDropHandler
{
    // Read-only eligibility check
    public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        // check capacity, type compatibility, etc.
        return true; // or false to fall through to normal blocked-target behavior
    }

    // Custom mutation, owned by DataBinding
    public bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        // add to container, clear source slot, fire events
        return true;
    }
}
```

Use `IPreRuleOccupiedSlotDropHandler` when the real target is the object inside the occupied slot. Use `IPostRuleOccupiedSlotDropHandler` when normal target drop rules must pass first. If `CheckOccupiedSlotDrop` returns false, the pipeline continues normally (swap, findAlternative, reject) — no behavior is lost.

**Check**: `Scripts/DataBinding/InventoryDataBindingBase.cs` and `Scripts/Inventories/InventoryTransferEngine.cs`.

---

## Anti-Pattern #14: Making Drop Targets Depend on Layouts

**Problem**: Making `InventoryDropArea` branch on a concrete layout component such as `FreeFormSlotLayout`.

**Why It's Bad**:
- couples transfer target resolution to visual layout
- makes future layouts require drop-area edits
- encourages layout code to split stacks, create items, or emit inventory events
- bypasses transfer-service rollback and event ordering guarantees

**Solution**:
- layout components move UI transforms only
- transfer semantics stay in `InventoryTransferService`
- dynamic target slot creation goes through generic runtime capabilities such as `IDynamicSlotLifecycle`
- `FreeFormSlotLayout` reacts to `OnSlotCreated` and positions the created slot at the pending drop point

**Check**: `Scripts/UI/InventoryDropArea.cs`, `Scripts/UI/FreeFormSlotLayout.cs`, `Scripts/Inventories/InventoryTransferEngine.cs`.

---

## Anti-Pattern #15: Converting Adapters Inside a Rule

❌ **BAD**:
```csharp
protected override RuleResult CanDrop(DragContext context, DragEntry entry)
{
    // Reaching for the converter because entry.Stack "has the wrong type"
    var mine = ItemConverter.TryConvertIncoming(entry.Stack.PrimaryAdapter) as MyAdapter;
    return mine != null ? RuleResult.Success() : RuleResult.Failure("Wrong type");
}
```

✅ **GOOD**:
```csharp
protected override RuleResult CanDrop(DragContext context, DragEntry entry)
{
    // Drop rules already receive the target-domain entry.
    if (entry.Stack.PrimaryAdapter is not MyAdapter mine)
        return RuleResult.Failure("Wrong type");

    return mine.Kind == ItemKind.Weapon
        ? RuleResult.Success()
        : RuleResult.Failure("Only weapons");
}
```

**Why**: `RuleEvaluationService.ValidateEntryDrop` converts the entry before any drop rule runs, and
routes it through the drag's conversion session. A hand-rolled conversion allocates a second object
that is *not* the one the transfer will commit, and it re-runs on every hover frame.

**Check**: `Scripts/Rules/RuleEvaluationService.cs`.

---

## Anti-Pattern #16: A Feedback Slot That Probes on Its Own

❌ **BAD**:
```csharp
public override void Highlight(bool highlight)
{
    base.Highlight(highlight);
    var probe = new InventoryTransferService().Probe(manager.CurrentContext, Inventory, this, policy);
    _redCross.SetActive(highlight && !probe.CanAttempt);
}
```

✅ **GOOD**:
```csharp
public override void Highlight(bool highlight)
{
    base.Highlight(highlight);

    if (_redCross != null)
        _redCross.SetActive(highlight && IsCurrentDropRefused());
}

private bool IsCurrentDropRefused()
    => Inventory is IInventoryInteraction interaction &&
       interaction.TryGetActiveDropVerdict(this, out var verdict) &&
       verdict.IsRejected;
```

**Why**: only `InventoryDropProcessor` knows the effective policy (it merges the bound override), so
a locally resolved probe can green-light a drop that then refuses, or vice versa. It also doubles
the probe cost per covered slot. Read the `DropVerdict` the preview already produced.

Do not store the verdict on the slot either: it is drag state, and a copy has to be invalidated.

**Check**: `Scripts/Slots/CrossFeedbackSlot.cs`, `Scripts/Inventories/DropPreviewController.cs`.

---

## Anti-Pattern #17: Predicting a Transfer From the Head of a Stack

❌ **BAD**:
```csharp
ItemStack.TryCreate(slot.Stack.Adapters.Take(dragAmount), out var entryStack);
```

✅ **GOOD**:
```csharp
var entryStack = slot.Stack.CreateCopy(dragAmount);
```

**Why**: `ItemStack.Split` and `ItemStack.CreateCopy` consume a stack from the **tail**. Anything
that predicts which instances will move — drag start, auto-transfer, preview slices — must slice the
same way, otherwise rules and conversion talk about instances that execution never touches. It looks
correct for full-stack moves and breaks only on partial ones.

For an entry spread over several placements, the remaining instances are the **head** of the entry
stack (length `DesiredCount`), and the next split takes the tail of that remainder.

**Check**: `Scripts/Inventories/TransferItemConversionUtility.cs`,
`Scripts/Inventories/AutoTransferService.cs`.

---

## Anti-Pattern #18: A Converter With Side Effects

❌ **BAD**:
```csharp
public IItemAdapter TryConvertIncoming(IItemAdapter adapter)
{
    var model = new ItemModel(adapter) { Uid = _registry.NextUid() };  // burns an id
    _registry.Register(model);                                        // and registers it
    return new MyAdapter(model);
}
```

✅ **GOOD**:
```csharp
public IItemAdapter TryConvertIncoming(IItemAdapter adapter)
    => adapter is ITradable t ? new MyAdapter(new ItemModel(t.OriginalSO)) : null;
```

**Why**: conversion runs during previews that may never become a drop. The session limits it to one
object per drag instead of one per hover frame, but a converter that touches the outside world still
leaves debris for a hover the player abandoned. Register in `ITransferDomainHandler.OnTransferSucceeded`,
which only runs on commit.

**Check**: `Scripts/Inventories/TransferConversionSession.cs`.

---

## Summary: Quick Check Before Coding

**Ask Yourself**:
1. Does DragContext already have this information?
2. Am I validating or mutating in rules? (should only validate!)
3. Did I check for null on `TargetSlot`?
4. Am I using `slot.IsEmpty` instead of `slot.Stack.IsEmpty`?
5. Am I converting by hand where the pipeline already converted for me?
6. Am I slicing a stack from the head where `Split` takes the tail?
7. Will this work in any Unity project with any architecture?

**If uncertain** → Check actual script files for implementation patterns.

---

**[Back to SKILL.md](./SKILL.md)**
