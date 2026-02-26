---
name: dragdrop-expert
description: Expert-level quick reference for DragAndDrop system. KISS principles, key anti-patterns, decision trees, and code review checklist. Use for code review or implementing features. Check actual code files for implementation.
---
# DragAndDrop System Expert Guide - Quick Reference

**Last Updated**: 2025-01-20
**Version**: 1.0+

> 💡 **Tip**: Expert reference. See linked files for details. **Check actual code for implementation.**

---

## Core Philosophy: KISS Principle

**THE GOLDEN RULE**: Check if existing state/context can solve the problem BEFORE adding new flags, locks, or states.

### Most Common Anti-Pattern

❌ **Adding locks when DragContext exists**

The system already has DragContext tracking current drag state. Don't add `_isLocked` or `_isProcessing` flags.

✅ **Use existing DragContext** - Check `Scripts/Core/DragContext.cs` to see what's already tracked

📖 **See**: [ANTIPATTERNS.md](./ANTIPATTERNS.md) for complete anti-pattern catalog

---

## Context: Asset Store Product

**CRITICAL**: This is a Unity Asset Store product. Every decision must consider:
- ✅ Works out-of-the-box (zero config)
- ✅ Supports Singleton AND DI patterns
- ✅ Compatible with Unity 2019.4+
- ✅ Minimal dependencies
- ✅ Easy for beginners, powerful for experts

**Singleton is NOT bad here** - it's a feature for universal compatibility!

---

## Key Architectural Changes (2025)

Know these when implementing features:

1. ✅ **InventoryTransferService** → `Scripts/Inventories/InventoryTransferService.cs` - All transfers go through this service
2. ✅ **ISlot** → `Scripts/Slots/ISlot.cs` - Abstract class now (was interface)
3. ✅ **SeparableStacksStrategy** → `Scripts/Inventories/InventoryStrategy.cs` - Third strategy type
4. ✅ **ReplaceItem** → See ISlot - Method for trading/crafting
5. ✅ **SlotOperationContext** → Event suppression
6. ✅ **InventorySnapshot** → Rollback system

---

## State Management: Golden Rule

**DragContext** is the single source of truth.

**File**: `Scripts/Core/DragContext.cs`

### Common Scenarios

**1. Prevent concurrent operations**:
- Check if `IsDragging && CurrentContext.SourceSlot == sourceSlot`
- ONE-LINE CHECK - no need for complex locking

**2. Allow parallel operations on different slots**:
- Dragging slot 5, auto-transfer slot 7 → ALLOWED ✅
- Different slots = no conflict

**3. Exclude dragged slot from operations**:
- Filter slots excluding `CurrentContext.SourceSlot`

---

## Decision Tree

```
Need to prevent concurrent operations?
  └─> Does DragContext already track this?
       ├─> YES: Use it! Don't add new state.
       └─> NO: Document why DragContext can't work

Need validation?
  └─> Which level?
       ├─> Global: Add to GlobalRuleValidator
       ├─> Inventory: Add to InventoryRuleValidator
       └─> Slot: Add to SlotRuleValidator

Need to change item behavior?
  └─> Can existing strategies handle it?
       ├─> YES: Use configuration
       └─> NO: Create custom strategy

Need transaction data?
  └─> Does InventoryTransferResult contain it?
       ├─> YES: Use result properties
       └─> NO: Extend SlotOperationContext
```

---

## Code Review Checklist

When reviewing code:

1. ✅ Works without setup?
2. ✅ Supports both Singleton and DI?
3. ✅ Optional dependencies wrapped in `#if`?
4. ✅ Public API has XML documentation?
5. ✅ Uses DragContext instead of new state?
6. ✅ Uses InventoryTransferService for transfers?
7. ✅ Transactions are atomic (rollback on failure)?
8. ✅ ISlot treated as abstract class?
9. ✅ Follows Asset Store compatibility?
10. ✅ Will run on mobile at 60 FPS?

### Red Flags

🚩 `_isLocked`, `_isProcessing` flags (check DragContext!)
🚩 Direct slot manipulation without strategies
🚩 Validation that modifies data (rules = read-only)
🚩 Unity 2020+ specific features
🚩 Required external packages without `#if`
🚩 Breaking existing public API

---

## Common Mistakes

### ❌ Mistake #1: Checking IsEmpty Without Null Check
Use `slot.IsEmpty` property (handles null) instead of `slot.Stack.IsEmpty` (crashes if Stack is null)

### ❌ Mistake #2: Forgetting TargetSlot Null Check
`context.TargetSlot` can be null for area drops! Always check before accessing.

### ❌ Mistake #3: Modifying Data in Rules
Rules are read-only validation. Never modify DraggedStack or any data during validation.

📖 **See**: [ANTIPATTERNS.md](./ANTIPATTERNS.md) for complete mistake catalog

---

## Performance Quick Tips

1. **UpdateAllVisuals** - O(n), needs dirty flags (TODO)
2. **TryRelocateAndRetry** - O(n²), limit attempts for 100+ slots
3. **FindValidAutoTransferSlot** - O(n × rules), needs caching (TODO)

📖 **See**: [BEST_PRACTICES.md](./BEST_PRACTICES.md) for optimization strategies

---

## Asset Store Recommendations

### ✅ GOOD Recommendations

- Performance optimizations (caching, dirty flags)
- Optional features with `[SerializeField]` toggle
- Extension points via interfaces/abstract classes
- Both Singleton and DI support
- XML documentation for public API

### ❌ BAD Recommendations

- "Replace Singleton with pure DI" → NO, keep both
- "Use async/await everywhere" → NO, Unity 2019.4 compatibility
- "Require UniRx/UniTask" → NO, optional only
- "Use C# 9 records" → NO, C# 7.3 compatible
- Breaking API changes → NO, maintain compatibility

---

## Extension Points

### Custom Rule

Implement `IDragRule` interface → `Scripts/Rules/IDragRule.cs`

Override Priority (0=highest) and CanDrop/CanStartDrag methods.

### Custom Strategy

Inherit from `InventoryStrategyBase` → `Scripts/Inventories/InventoryStrategy.cs`

Override TryAdd/TryRemove methods, use PassesRules() for validation.

📖 **See**: [BEST_PRACTICES.md](./BEST_PRACTICES.md) for complete extension examples

---

## Testing Checklist

### Drag & Drop
- [ ] Drag slot 5 → auto-transfer slot 5 → BLOCK
- [ ] Drag slot 5 → auto-transfer slot 7 → WORK
- [ ] Drag to same slot → BLOCK (SameSlotRule)
- [ ] Drag to occupied slot (same item, Stackable) → MERGE

### Snapshots & Rollback
- [ ] Transfer fails validation → source restored ✅
- [ ] Transfer fails on full target → source restored ✅
- [ ] Complex relocation fails → inventory restored ✅

📖 **See**: [TESTING.md](./TESTING.md) for complete test scenarios

---

## 📖 Detailed Documentation

- **[ANTIPATTERNS.md](./ANTIPATTERNS.md)** - Complete anti-pattern catalog with explanations
- **[BEST_PRACTICES.md](./BEST_PRACTICES.md)** - Extension points and optimization strategies
- **[TESTING.md](./TESTING.md)** - Comprehensive testing scenarios

---

## Summary

**Before adding ANY new state/flag/lock, ask yourself**:

1. Does `DragContext` already have this information?
2. Can I solve this with a simple check?
3. Am I overthinking this?
4. **Does this break compatibility with beginner projects?**
5. **Will this work on mobile at 60 FPS?**

**The answer is usually**: Yes, the context already knows. Just check it.

**Remember**:
- This is a drag-and-drop system for a game, not a distributed database
- This is an Asset Store product for EVERYONE
- Singleton here is a feature, not a bug
- All transfers go through `InventoryTransferService`
- Use snapshots for rollback protection

---

**Remember**: Keep it simple! Compatibility over purity!
