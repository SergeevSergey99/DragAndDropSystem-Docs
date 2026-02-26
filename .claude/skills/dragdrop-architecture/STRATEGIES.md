# Strategy Pattern - Deep Dive

Detailed documentation of all inventory strategies.

## Strategy Hierarchy

```
IInventoryStrategy
  ├─ UniqueItemStrategy
  ├─ StackableItemStrategy
  ├─ SeparableStacksStrategy (NEW!)
  └─ DynamicSlotDecorator (wraps any of above)
```

**Location**: → `Scripts/Inventories/InventoryStrategy.cs`

---

## UniqueItemStrategy

**Behavior**: One item per slot, count always 1.

**Location**: → `Scripts/Inventories/InventoryStrategy.cs`

**How It Works**:
- Always creates single-item stacks (count = 1)
- Each item gets its own slot
- If targetIndex specified → tries that slot
- Otherwise → finds first empty slot
- Removes 1 from incoming stack per placement

**Use Cases**:
- Equipment slots (weapon, armor, accessory)
- Unique items (quest items, artifacts)
- Character inventories where each item is distinct

Check `UniqueItemStrategy` class in `Scripts/Inventories/InventoryStrategy.cs`.

---

## StackableItemStrategy

**Behavior**: Automatic merging of same items.

**Location**: → `Scripts/Inventories/InventoryStrategy.cs`

**How It Works**:
1. **Fill existing stacks first** - Finds slots with same item, adds until full
2. **Create new stacks** - If remaining items, creates new stacks in empty slots
3. **Respects MaxStackSize** - Never exceeds item's max stack limit

**Two-Phase Algorithm**:
- **Phase 1**: Loop through occupied slots, try to stack with matching items
- **Phase 2**: Loop through empty slots, create new stacks

**Use Cases**:
- Resources (wood, stone, ore)
- Consumables (potions, food)
- Minecraft-style inventories
- Crafting materials

Check `StackableItemStrategy` class in `Scripts/Inventories/InventoryStrategy.cs`.

---

## SeparableStacksStrategy (NEW!)

**Behavior**: Heroes of Might & Magic style - multiple stacks allowed, merge only on explicit drop.

**Location**: → `Scripts/Inventories/InventoryStrategy.cs`

**Configuration**:
- `_allowMergeOnDrop` (bool) - Controls whether merge on drop is allowed

**How It Works**:

**MODE 1: Explicit drag to slot** (targetIndex >= 0):
- If target slot empty → Place as new stack
- If target slot occupied + `_allowMergeOnDrop` = true + same item → Merge stacks
- If target slot occupied but can't merge → Fail

**MODE 2: TryAddItem** (no target):
- Finds first empty slot
- Creates new stack
- Does NOT auto-merge with existing stacks

**Key Difference**: Does NOT auto-merge with `TryAddItem()`, only on explicit drop to occupied slot (if allowed).

**Use Cases**:
- Strategy games (Heroes of M&M, King's Bounty)
- Tactical RPGs (Fire Emblem style)
- When player needs fine control over stack separation
- Army unit management systems

Check `SeparableStacksStrategy` class in `Scripts/Inventories/InventoryStrategy.cs`.

---

## DynamicSlotDecorator

**Behavior**: Wraps any strategy, adds dynamic slot creation/removal.

**Location**: → `Scripts/Inventories/InventoryStrategy.cs`

**Configuration**:
- `_maxSlots` - Maximum total slots
- `_maxFreeSlots` - Target number of empty slots
- `_initialSlotCount` - Minimum slots (never removed below this)

**How It Works**:

### During TryAdd

**MODE 1: Explicit drag to slot** (targetIndex >= 0):
- Creates slots UP TO targetIndex only if `_maxFreeSlots > 0`
- Prevents infinite growth from spam clicking
- Respects `_maxSlots` limit

**MODE 2: TryAddItem** (targetIndex < 0):
- ALWAYS creates slots as needed
- Ensures programmatic additions never fail
- Continues until item fully added or `_maxSlots` reached

### After TryAdd: EnsureFreeSlots

- Counts current free slots
- Creates additional slots to reach `_maxFreeSlots` target
- Respects `_maxSlots` limit

### After TryRemove: TrimExcessFreeSlots

- Counts free slots
- Removes excess empty slots beyond `_maxFreeSlots`
- NEVER removes below `_initialSlotCount`
- Removes from end of list

**Wrapping Example**:
UniversalInventory wraps base strategy in decorator when `SlotManagementType.Dynamic` is selected.

Check `DynamicSlotDecorator` class in `Scripts/Inventories/InventoryStrategy.cs`.

---

## Strategy Selection in UniversalInventory

**Location**: → `Scripts/Inventories/UniversalInventory.cs`

**Configuration Enum**: `ItemBehaviorType`
- Unique
- Stackable
- SeparableStacks

**Strategy Creation**:
1. Based on `_itemBehavior` enum, creates base strategy
2. If `_slotManagement = Dynamic`, wraps in `DynamicSlotDecorator`
3. Assigns to `_strategy` field

**Benefits of Strategy Pattern**:
- Behavior change without modifying UniversalInventory
- Easy to add new strategies
- Testable in isolation
- Composable with decorators

---

## Custom Strategy Development

**Steps**:
1. Inherit from `InventoryStrategyBase`
2. Override `TryAdd(slots, stack, targetIndex)` method
3. Override `TryRemove(slots, itemId, count)` method
4. Use `PassesRules(slot, item, count)` for validation
5. Return true if operation succeeds, false otherwise

**Example Use Cases**:
- Weight limit strategy
- Volume-based inventory
- Durability tracking
- Custom stacking rules

Check base class in `Scripts/Inventories/InventoryStrategy.cs`.

---

**[Back to SKILL.md](./SKILL.md)**
