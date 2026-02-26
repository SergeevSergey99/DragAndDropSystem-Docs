---
name: dragdrop-architecture
description: Quick architectural reference for DragAndDrop system. Design decisions, component overview, and links to technical documentation. Use for understanding system internals. Check actual code files for implementation.
---
# DragAndDrop System Architecture - Quick Reference

**Last Updated**: 2025-01-20
**Version**: 1.0+

> 💡 **Tip**: Quick architectural overview. See linked files for details. **Check actual code for implementation.**

---

## Project Context

**CRITICAL**: This is a **Unity Asset Store product** - designed to work in ANY Unity project with ZERO assumptions.

### Design Goals

1. **Universal Compatibility**: Works with or without DI frameworks
2. **Zero Configuration**: Works out-of-box with sensible defaults
3. **Flexible Integration**: Supports multiple architectural patterns
4. **Minimal Dependencies**: Only Unity built-ins (+ optional integrations)
5. **Easy to Extend**: Clear extension points without modifying source

### Singleton + DI Hybrid Pattern

**Why both?**
- **Singleton**: Works in any project, no setup
- **DI**: Opt-in for projects using Reflex/Zenject/VContainer
- **Conditional Compilation**: User chooses via scripting define

See `Scripts/DragAndDropManager.cs` for implementation.

---

## Core Components Overview

### 1. DragAndDropManager (Orchestrator)

**File**: `Scripts/DragAndDropManager.cs`

**Responsibility**: Orchestrates all drag operations

**Key State**:
- `DragContext _currentContext` - Single source of truth
- `InventoryTransferService _transferService` - Atomic transactions (NEW!)
- `GlobalRuleValidator _globalRules` - Global validation
- `List<IDropTarget> _dropTargetStack` - Nested drop zones

**Key Methods**: StartDrag(), CompleteDrag(), TryAutoTransfer()

### 2. DragContext

**File**: `Scripts/Core/DragContext.cs`

**Single source of truth** for drag operations. Contains dragged stack, source/target slots and inventories.

**Golden Rule**: Always check DragContext before adding new state!

### 3. InventoryTransferService (NEW!)

**File**: `Scripts/Inventories/InventoryTransferService.cs`

**Responsibility**: Atomic transaction handling

**How**: Snapshot → Remove → Add → Rollback on failure

**Benefits**: Atomicity, Isolation, automatic rollback

### 4. UniversalInventory

**File**: `Scripts/Inventories/UniversalInventory.cs`

**Manages slots** using strategy pattern:
- `IInventoryStrategy _strategy` - Behavior (Unique/Stackable/Separable)
- `List<ISlot> _slots` - Slot collection
- `InventoryRuleValidator _ruleValidator` - Validation
- `InventoryDataBindingBase _dataBinding` - Optional external sync

### 5. ISlot (Abstract Class) ⚠️

**File**: `Scripts/Slots/ISlot.cs`

**IMPORTANT**: Abstract class, NOT interface!

**Why**: Needs MonoBehaviour for Inspector, provides virtual Transform property

**New Methods**: ReplaceItem() - for trading/crafting

📖 **See**: [COMPONENTS.md](./COMPONENTS.md) for detailed component documentation

---

## Strategy Pattern

**File**: `Scripts/Inventories/InventoryStrategy.cs`

```
IInventoryStrategy
  ├─ UniqueItemStrategy        (1 item per slot)
  ├─ StackableItemStrategy     (Auto-merge)
  ├─ SeparableStacksStrategy   (Manual merge) NEW!
  └─ DynamicSlotDecorator      (Wraps any strategy)
```

**Selection** via ItemBehaviorType enum: Unique, Stackable, SeparableStacks

📖 **See**: [STRATEGIES.md](./STRATEGIES.md) for strategy deep dive

---

## Three-Level Rule Validation

**Flow**:
```
Global Rules (DragAndDropManager)
    ↓
Inventory Rules (UniversalInventory)
    ↓
Slot Rules (UniversalSlot)
    ↓
Execute
```

**Files**:
- `Scripts/Rules/IDragRule.cs` - Rule interface
- `Scripts/Rules/RuleValidator.cs` - Validation logic

**Execution**: Early exit on first failure (performance)

---

## Data Flow

### Manual Drag & Drop

```
User Click → StartDrag() → Validate → Hover → Validate Drop
→ CompleteDrag() → InventoryTransferService.TryExecuteTransfer()
→ Events
```

### Auto-Transfer

```
Trigger → TryAutoTransfer() → Find Valid Slot
→ InventoryTransferService.TryExecuteTransfer()
→ Optional Animation → Events
```

📖 **See**: [DATA_FLOW.md](./DATA_FLOW.md) for detailed flow diagrams

---

## Performance Considerations

### Hot Paths

- **UpdateAllVisuals()** - O(n), needs dirty flags (TODO)
- **TryRelocateAndRetry** - O(n²), limit attempts for 100+ slots
- **FindValidAutoTransferSlot** - O(n × rules), needs caching (TODO)

### Optimization Strategies

1. **Slot Caching** (TODO) - Cache by ItemId, empty slots
2. **Dirty Flags** (TODO) - Only update changed slots
3. **Rule Caching** (Implemented) - Cache sorted rules

📖 **See**: [PERFORMANCE.md](./PERFORMANCE.md) for detailed analysis

---

## Extension Points

### Custom Strategy

Inherit from InventoryStrategyBase and override TryAdd/TryRemove methods.

Example: WeightLimitStrategy - check `Scripts/Inventories/InventoryStrategy.cs` for base class.

### Custom Rule

Implement IDragRule interface with Priority and CanDrop/CanStartDrag methods.

Example: LevelRequirementRule - check `Scripts/Rules/IDragRule.cs` for interface.

---

## Asset Store Compatibility

### Required Support

✅ Singleton + DI hybrid
✅ Optional dependencies with `#if`
✅ Unity 2019.4+ support
✅ C# 7.3 compatible

### Anti-Patterns

❌ Forcing DI-only
❌ Requiring external packages
❌ Using C# 9+ features
❌ Hard-coupling to specific architectures

---

## Key Architectural Changes (2025)

1. ✅ **InventoryTransferService** - Atomic transactions
2. ✅ **ISlot** - Changed to abstract class
3. ✅ **SeparableStacksStrategy** - Third strategy type
4. ✅ **ReplaceItem** - Method for trading/crafting
5. ✅ **SlotOperationContext** - Event suppression
6. ✅ **InventorySnapshot** - Rollback system

---

## Quick Reference

### File Locations

```
Scripts/
├── DragAndDropManager.cs
├── Core/DragContext.cs
├── Inventories/
│   ├── UniversalInventory.cs
│   ├── InventoryStrategy.cs
│   └── InventoryTransferService.cs (NEW!)
├── Slots/ISlot.cs (abstract class)
└── Rules/IDragRule.cs
```

### Common Patterns

**Singleton/DI Access**: Check `Scripts/DragAndDropManager.cs` for conditional compilation pattern

---

## 📖 Detailed Documentation

- **[COMPONENTS.md](./COMPONENTS.md)** - Full component documentation
- **[STRATEGIES.md](./STRATEGIES.md)** - Strategy pattern implementation details
- **[DATA_FLOW.md](./DATA_FLOW.md)** - Operation pipelines and flow diagrams
- **[PERFORMANCE.md](./PERFORMANCE.md)** - Performance analysis & optimizations

---

**Remember**: This is a product for everyone - compatibility over purity!
