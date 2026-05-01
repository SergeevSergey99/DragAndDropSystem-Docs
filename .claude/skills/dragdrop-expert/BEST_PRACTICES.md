# Best Practices & Extensions

Complete guide for extending the system and optimization strategies.

**Last Updated**: 2026-05-01

## Extension Points

### Custom Strategy

**Purpose**: Create custom inventory behavior (e.g., weight limits, volume-based, durability).

**Locations**:
- `Scripts/Inventories/Strategies/IPlacementStrategy.cs`
- `Scripts/Inventories/Strategies/IAcceptanceStrategy.cs`
- `Scripts/Inventories/Strategies/IDragPolicy.cs`
- `Scripts/Inventories/Strategies/IInventoryQueryStrategy.cs`
- `Scripts/Inventories/Strategies/IInventoryStrategy.cs`
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
- `Scripts/Inventories/Strategies/*.cs`

**How to Create**:
1. Inherit from `InventoryStrategyBase` or an existing concrete strategy
2. Override `TryAdd(slots, stack, targetIndex)` method
3. Override `TryRemove(slots, item, count, sourceIndex)` method (optional)
4. Override `TryAddToSlot(...)` if slot-target semantics differ
5. Override `CanAcceptItem(...)` / `GetAcceptableCount(...)` if preview logic differs
6. Override `ResolveDragAmount(...)` only if drag semantics differ
7. Override `Contains(...)` / `GetItemCount(...)` only if query semantics differ
8. Use `PassesRules(slot, item, count, request)` for rule validation
9. Return true if operation succeeds, false otherwise

**Example Use Cases**:
- Weight limit system (check total weight before adding)
- Volume-based inventory (items have size, inventory has capacity)
- Custom stacking rules (max 5 potions per slot)
- Durability tracking (items degrade on use)
- Class-specific inventories (warrior vs mage items)

**Check Implementation**: `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` and concrete strategy files in `Scripts/Inventories/Strategies/`.

---

### Custom Rule

**Purpose**: Create custom validation rules (e.g., level requirements, class restrictions).

**Location**: → `Scripts/Rules/IDragRule.cs`

**How to Create**:
1. Create class implementing `IDragRule`
2. Add `[Serializable]` attribute for Inspector
3. Set `Priority` property (0 = highest, 100 = lowest)
4. Implement `CanStartDrag(context)` method
5. Implement `CanDrop(context)` method
6. Return `RuleResult.Success()` or `RuleResult.Failure(reason)`
7. **NEVER** modify data in rules - read-only validation only!

**Example Use Cases**:
- Level requirements (character level 10 to use)
- Class restrictions (only warriors can use swords)
- Quest requirements (must complete quest first)
- Zone restrictions (can't drag items in combat)
- Cooldown validation (can't use item on cooldown)

**Check Implementation**: `Examples/Demo1 Inventories/ItemTypeExampleFilterRule.cs` for simple example.

---

### Custom Action

**Purpose**: Create custom inventory operations (e.g., sell all, sort, quick stash).

**Location**: → `Scripts/Actions/InventoryActionBase.cs`

**How to Create**:
1. Inherit from `InventoryActionBase`
2. Override `Execute(inventory, activeSlot, logWarnings)` method
3. Check `if (dragManager.IsDragging)` before executing
4. Use `dragManager.TryAutoTransfer()` for item transfers
5. Return true if successful, false otherwise
6. Add `[SerializeField]` fields for configuration

**Example Use Cases**:
- Sell all items (transfer all to merchant)
- Sort inventory (arrange by type, rarity, etc.)
- Quick stash (transfer all to storage)
- Auto-organize (group same items together)
- Repair all (consume repair kits for damaged items)

**Check Implementation**: `Scripts/Actions/Inheritors/AutoTransferAction.cs`, `Scripts/Actions/Inheritors/SortInventoryAction.cs` for examples.

---

### Custom Visual

**Purpose**: Create custom drag visual appearance.

**Location**: → `Scripts/UI/IDragVisual.cs`

**How to Create**:
1. Create MonoBehaviour implementing `IDragVisual`
2. Implement `Show(stack)` method
3. Implement `Hide()` method
4. Implement `UpdatePosition(position)` method
5. Add visual elements (Image, Text, Particles, Animations)
6. Assign in DragAndDropManager Inspector

**Example Use Cases**:
- Particle effects (glowing items, trails)
- Custom animations (wobble, scale, rotation)
- Rarity highlighting (legendary = golden glow)
- Count badges (large numbers for big stacks)
- Item preview (3D model preview)

**Check Implementation**: `Scripts/UI/DefaultDragVisual.cs`, `Scripts/UI/FancyDragVisual.cs` for examples.

---

## Performance Optimizations

### Dirty Flags (TODO)

**Purpose**: Only update visuals for changed slots.

**Location**: → `Scripts/Inventories/UniversalInventory.cs` (potential implementation)

**How It Works**:
- Maintain `HashSet<BaseSlot>` of dirty slots
- `MarkDirty(slot)` adds slot to set
- `UpdateDirtyVisuals()` updates only marked slots, then clears set
- Call from `LateUpdate()` to batch all frame updates

**Benefits**:
- Reduces visual updates from O(n) to O(changed slots)
- Significant improvement for large inventories
- Minimal memory overhead

---

### Slot Caching (TODO)

**Purpose**: Fast lookups by ItemId instead of linear search.

**Location**: → `Scripts/Inventories/UniversalInventory.cs` (potential implementation)

**How It Works**:
- `Dictionary<itemId, List<slots>>` for fast item lookup
- `List<emptySlots>` for fast empty slot access
- `int _cacheVersion` for invalidation tracking
- `InvalidateCache()` increments version
- `RebuildCacheIfNeeded()` rebuilds if version changed

**Benefits**:
- O(1) lookup instead of O(n) search
- Major improvement for acceptance preview and slot search paths
- Trade-off: Memory overhead for large inventories

---

### Limit Relocation Attempts

**Purpose**: Prevent O(n²) performance for large inventories.

**Location**: → `Scripts/Inventories/SlotRelocationService.cs` (TryRelocateAndRetry)

**How It Works**:
- Add `[SerializeField] int _maxRelocationAttempts = 3;` config
- Limit candidate slot tries to first N instead of all
- Prioritize candidates by rule compatibility
- Skip relocation if `_maxRelocationAttempts <= 0`

**Benefits**:
- Reduces worst-case from O(n²) to O(n × attempts)
- Essential for 100+ slot inventories
- Configurable per inventory

---

## Asset Store Best Practices

### Code Style

**Namespace Convention**:
- `UniversalDragAndDrop` - Root namespace
- `UniversalDragAndDrop.Core` - Core classes
- `UniversalDragAndDrop.Rules` - Rule system
- etc.

**XML Documentation**:
Add XML comments to ALL public API:
- `/// <summary>` - Method/class description
- `/// <param>` - Parameter description
- `/// <returns>` - Return value description
- `/// <example>` - Usage example (optional)

**Conditional Compilation**:
Wrap optional features:
- `#if ODIN_INSPECTOR` - Odin attributes
- `#if DOTWEEN` - DOTween animations
- `#if ENABLE_REFLEX_DI` - DI framework support

**Check**: `Scripts/DragAndDropManager.cs` for examples.

---

### Compatibility Guidelines

**✅ DO**:
- Support both Singleton and DI patterns
- Provide sensible defaults (zero config)
- Use interfaces for extension points
- Keep backwards compatibility
- Test on Unity 2019.4+

**❌ DON'T**:
- Require specific architecture (MVC, ECS)
- Force external dependencies
- Use C# 9+ language features (stick to C# 7.3)
- Break API between versions
- Assume user's project structure

---

## Pattern for Improvements

**Bad Recommendation**: "You should remove Singleton and use DI"

**Good Recommendation**: "To support both patterns, wrap access like this:"
- Use conditional compilation (`#if ENABLE_REFLEX_DI`)
- Provide `[Inject]` property for DI
- Provide `Instance` property for Singleton
- Let user choose via scripting define

This keeps backward compatibility while supporting modern architectures.

---

## Extension Development Checklist

When creating extension:

1. ✅ Works without configuration?
2. ✅ Compatible with Unity 2019.4+?
3. ✅ Has XML documentation?
4. ✅ Uses conditional compilation for optional features?
5. ✅ Doesn't break existing API?
6. ✅ Follows namespace convention?
7. ✅ Tested with both Singleton and DI?
8. ✅ Works on mobile (60 FPS)?

---

## Common Extension Patterns

### Validation Extension

**Pattern**: Add custom rule
**When**: Need to validate based on game logic
**Example**: Level requirement, quest completion, zone restriction

### Behavior Extension

**Pattern**: Create custom strategy
**When**: Need different item behavior
**Example**: Weight limits, durability, custom stacking

### Operation Extension

**Pattern**: Create custom action
**When**: Need new inventory operation
**Example**: Sort, sell all, quick transfer

### Visual Extension

**Pattern**: Implement IDragVisual
**When**: Need custom drag appearance
**Example**: Particle effects, animations, rarity glow

---

**[Back to SKILL.md](./SKILL.md)**
