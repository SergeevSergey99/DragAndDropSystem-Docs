---
name: dragdrop-system
description: Quick reference for Unity Drag & Drop Inventory System. Overview of architecture, core concepts, and operations. Links to detailed docs. Use for quick lookups; check actual code files for implementation details.
---
# Unity Drag & Drop Inventory System - Quick Reference

**Version**: 1.0+
**Last Updated**: 2025-01-20

> 💡 **Tip**: This is a quick reference. See linked files below for details. **Check actual script files for code.**

---

## 🎯 System Overview

**Production-ready** Unity Asset Store plugin for drag & drop inventory systems.

**Key Features**:
- ✅ Zero configuration - works out of the box
- ✅ Singleton + DI hybrid - works in ANY Unity project
- ✅ Strategy Pattern - Unique/Stackable/SeparableStacks behaviors
- ✅ Three-level rule validation (Global → Inventory → Slot)
- ✅ DataBinding for external data integration
- ✅ Atomic transactions with rollback

**Dependencies**:
- Required: Unity UI, TextMeshPro
- Optional: DOTween, Odin Inspector, Reflex DI

---

## 🏗️ Core Architecture

### Main Components

**DragAndDropManager** → `Scripts/DragAndDropManager.cs`
- Orchestrates all drag & drop operations
- Singleton/DI hybrid pattern
- Contains: DragContext, InventoryTransferService, GlobalRules

**UniversalInventory** → `Scripts/Inventories/UniversalInventory.cs`
- Manages slot collection
- Uses Strategy Pattern for item behavior
- Contains: IInventoryStrategy, RuleValidator, DataBinding

**ISlot** → `Scripts/Slots/ISlot.cs`
- Abstract class (NOT interface!)
- Base class for all slots
- Inherits from MonoBehaviour

**InventoryTransferService** → `Scripts/Inventories/InventoryTransferService.cs` ⭐ NEW!
- Atomic transactions with rollback
- Snapshot-based error recovery

📖 **See**: [CORE_CONCEPTS.md](./CORE_CONCEPTS.md) for detailed architecture

---

## 🔑 Essential Concepts

### 1. DragContext - Single Source of Truth

**What**: Single source of truth for current drag operation
**Where**: `Scripts/Core/DragContext.cs`

**Contains**:
- DraggedStack - what's being dragged
- SourceSlot/SourceInventory - where from
- TargetSlot/TargetInventory - where to (can be null!)

**Golden Rule**: Always check DragContext before adding new state!

### 2. Three-Level Rule Validation

**Validation Flow**:
```
User Action
  ↓
Global Rules (DragAndDropManager)
  ↓
Inventory Rules (UniversalInventory)
  ↓
Slot Rules (UniversalSlot)
  ↓
Execute
```

**Files**:
- `Scripts/Rules/IDragRule.cs` - rule interface
- `Scripts/Rules/RuleValidator.cs` - validator
- `Scripts/Rules/BuiltInRules.cs` - built-in rules

**Priority**: 0 = highest (executes first)

### 3. Strategy Pattern

**Where**: `Scripts/Inventories/InventoryStrategy.cs`

**Behavior Types**:
- **UniqueItemStrategy** - 1 item per slot (RPG equipment)
- **StackableItemStrategy** - Auto-merge (Minecraft style)
- **SeparableStacksStrategy** - Manual merge (Heroes of M&M) ⭐ NEW!

**Decorator**: DynamicSlotDecorator - wraps any strategy, adds dynamic slot creation

### 4. InventoryTransferService ⭐ NEW!

**What**: Service for atomic item transfers
**Where**: `Scripts/Inventories/InventoryTransferService.cs`

**How it Works**:
1. Creates inventory snapshots
2. Removes from source
3. Tries to add to target
4. On error - rollback via snapshots

**Benefits**: Atomicity, Isolation, automatic rollback

### 5. ISlot - Abstract Class ⚠️

**Important Change**: ISlot is now abstract class, not interface!

**Why**:
- Needs MonoBehaviour for Inspector
- Provides virtual Transform property
- Simplifies prefab workflow

**Where**: `Scripts/Slots/ISlot.cs`

**New Methods**: ReplaceItem() - for trading/crafting scenarios

📖 **See**: [CORE_CONCEPTS.md](./CORE_CONCEPTS.md) for full details

---

## 🎮 Core Operations

### Manual Drag & Drop
1. User Click → StartDrag()
2. Hover → Validate rules, highlight
3. Release → CompleteDrag() → InventoryTransferService
4. Events dispatched

### Auto-Transfer (Shift+Click)
1. Trigger → TryAutoTransfer()
2. FindValidSlot
3. InventoryTransferService
4. Optional animation

### Swap Operation
1. Validate both directions
2. Atomic swap
3. Events

📖 **See**: [OPERATIONS.md](./OPERATIONS.md) for detailed flow diagrams

---

## 🔧 Advanced Features

**Quick Click Auto-Transfer** - LMB quick click for Diablo-style transfers
**Dynamic Slot Management** - Auto-create/remove slots
**Animation System** - DOTween-based transfer animations
**3D World Integration** - WorldDropZone, WorldItem components
**Tooltip System** - ITooltipProvider interface
**DataBinding** - Sync with external data (GameManager, SaveData)

📖 **See**: [ADVANCED_FEATURES.md](./ADVANCED_FEATURES.md) for implementation details

---

## 📚 Examples & Demos

**Demo1**: Basic Inventory - Core features, slot rules
**Demo2**: Trading System - Economy, money validation
**Demo3**: Loot System - Event-driven architecture

📖 **See**: [EXAMPLES.md](./EXAMPLES.md) for detailed breakdowns

---

## ⚡ Quick Reference

### Key Files

```
Scripts/
├── DragAndDropManager.cs           - Main manager
├── Core/
│   ├── DragContext.cs              - Single source of truth
│   ├── ItemStack.cs                - Item+count wrapper
│   └── AutoTransferAnimationStrategy.cs
├── Inventories/
│   ├── UniversalInventory.cs       - Main inventory
│   ├── InventoryStrategy.cs        - Strategy implementations
│   └── InventoryTransferService.cs - Transactions (NEW!)
├── Slots/
│   ├── ISlot.cs                    - Abstract class (NEW!)
│   └── UniversalSlot.cs
└── Rules/
    ├── IDragRule.cs
    └── RuleValidator.cs
```

### Main Events

**DragAndDropManager**:
- OnDragStarting/Started
- OnDropAttempting/Completed
- OnAutoTransferCompleted
- OnSwapCompleted

**UniversalInventory**:
- OnItemAdded
- OnItemRemoved

### Access Pattern

**Singleton/DI Hybrid** - Check `Scripts/DragAndDropManager.cs` for implementation

---

## 🐛 Common Pitfalls

❌ **Don't**:
- Add `_isLocked` flags (use DragContext!)
- Modify data in rules (validation only)
- Forget null check for `context.TargetSlot`
- Create circular DataBinding updates

✅ **Do**:
- Check `DragContext.SourceSlot` before operations
- Use `InventoryTransferService` for transfers
- Validate via rules, execute via strategies
- Use snapshots for rollback protection

📖 **See**: [../dragdrop-expert/ANTIPATTERNS.md](../dragdrop-expert/ANTIPATTERNS.md)

---

## 🎓 Best Practices

**Asset Store Product**:
- Works out of box without configuration
- Supports both Singleton AND DI
- Compatible with Unity 2019.4+

**Architecture**:
- DragContext = single source of truth
- Rules = read-only validation
- Strategies = item behavior
- TransferService = atomicity

📖 **See**: [../dragdrop-expert/SKILL.md](../dragdrop-expert/SKILL.md)

---

## 📖 Detailed Documentation

- **[CORE_CONCEPTS.md](./CORE_CONCEPTS.md)** - DragContext, Rules, Strategies, DataBinding explained
- **[OPERATIONS.md](./OPERATIONS.md)** - Detailed operation flow diagrams
- **[ADVANCED_FEATURES.md](./ADVANCED_FEATURES.md)** - Animations, 3D integration, tooltips
- **[EXAMPLES.md](./EXAMPLES.md)** - Demo scene breakdowns

---

**Remember**: This is an Asset Store product - universal compatibility over architectural purity!
