# Performance Analysis & Optimizations

Performance considerations and optimization strategies.

## Hot Paths

### UpdateAllVisuals() - O(n)

**Location**: → `Scripts/Inventories/UniversalInventory.cs`

**Problem**: Updates ALL slots every time, even if only 1 changed.

**Current Complexity**: O(n) where n = number of slots

**Solution (TODO)**: Dirty flags system
- Mark only changed slots as dirty
- Update only dirty slots
- Clear dirty flags after update
- Reduces updates from O(n) to O(changed slots)

Check `UpdateAllVisuals()` method in `Scripts/Inventories/UniversalInventory.cs`.

---

### TryRelocateAndRetry - O(n²)

**Location**: → `Scripts/Inventories/UniversalInventory.cs`

**Problem**: For each slot, tries to relocate its occupant to every other slot.

**Current Complexity**: O(n²) where n = number of slots

**Performance Impact**:
- 10-50 slots: acceptable (<5ms)
- 100+ slots: noticeable lag (10-50ms)

**Solution (TODO)**: Limit attempts
- Add `_maxRelocationAttempts` configuration
- Try only first N candidates instead of all
- Prioritize candidates by rule compatibility
- Reduces worst-case from O(n²) to O(n × attempts)

Check `TryRelocateAndRetry()` method in `Scripts/Inventories/UniversalInventory.cs`.

---

### FindValidAutoTransferSlot - O(n × rules)

**Location**: → `Scripts/Inventories/UniversalInventory.cs`

**Problem**: Linear search with rule validation for every slot.

**Current Complexity**: O(n × r) where n = slots, r = number of rules

**Algorithm**:
1. If not Unique mode → Try stacking in existing slots with same item
2. Try empty slots that pass rules
3. Returns first valid slot or null

**Solution (TODO)**: Cache by ItemId
- Maintain `Dictionary<itemId, List<slots>>` cache
- Maintain `List<emptySlots>` cache
- Invalidate cache on slot changes
- Reduces search from O(n) to O(1) lookup + O(matching slots × rules)

Check `FindValidAutoTransferSlot()` method in `Scripts/Inventories/UniversalInventory.cs`.

---

## Optimization Strategies

### 1. Slot Caching (TODO)

**Benefit**: O(1) lookups instead of O(n) searches.

**Implementation Strategy**:
- `Dictionary<itemId, List<slots>>` - Fast lookup by item
- `List<emptySlots>` - Fast empty slot access
- `int _cacheVersion` - Invalidation tracking
- `OnSlotChanged(slot)` → `InvalidateCache()`
- `RebuildCacheIfNeeded()` → Rebuild on first access after invalidation

**Trade-offs**:
- Memory overhead: ~O(unique items) for dictionary
- Rebuild cost: O(n) when cache invalidated
- Best for: Large inventories with frequent searches

Check `Scripts/Inventories/UniversalInventory.cs` for potential implementation.

---

### 2. Dirty Flags (TODO)

**Benefit**: Only update changed visuals.

**Implementation Strategy**:
- `HashSet<ISlot> _dirtySlots` - Track changed slots
- `MarkDirty(slot)` → Add to set
- `UpdateDirtyVisuals()` → Update only marked slots, clear set
- Call from `LateUpdate()` to batch updates

**Trade-offs**:
- Memory overhead: Minimal (single HashSet)
- Best for: Frequent visual updates with few actual changes

**Expected Improvement**:
- Current: O(total slots) every update
- With dirty flags: O(changed slots) per frame

Check `Scripts/Inventories/UniversalInventory.cs` for potential implementation.

---

### 3. Rule Caching (Implemented)

**Benefit**: Avoids LINQ queries on every validation.

**Location**: → `Scripts/Rules/RuleValidator.cs`

**How It Works**:
- Caches sorted rule list (`_cachedRules`)
- Marks dirty when rules added/removed (`_rulesDirty = true`)
- Rebuilds cache on first access after dirty
- Cache contains flattened + sorted rules from all sources

**Performance Gain**:
- Current: O(1) cache hit
- Without cache: O(rules × log(rules)) for LINQ sorting each time

Check `RuleValidator.GetRules()` method in `Scripts/Rules/RuleValidator.cs`.

---

## Benchmark Targets

### Target Performance (60 FPS = 16.67ms per frame)

**Small Inventories (< 50 slots)**:
- Drag & Drop: < 1ms
- Auto-Transfer: < 2ms
- UpdateAllVisuals: < 2ms
- Total inventory operations: < 5ms per frame

**Large Inventories (100+ slots)**:
- Drag & Drop: < 2ms
- Auto-Transfer: < 5ms (with caching)
- UpdateAllVisuals: < 1ms (with dirty flags)
- Relocation: < 5ms (with attempt limit)
- Total inventory operations: < 10ms per frame

---

## Profiling Recommendations

**Unity Profiler Markers**:
Unity provides `ProfilerMarker` API for deep profiling. Consider adding markers to hot paths.

**What to Profile**:
1. `UpdateAllVisuals()` - Most frequent call
2. `TryRelocateAndRetry()` - Highest complexity
3. Rule validation - Called on every hover
4. `Strategy.TryAdd()` - Called on every drop

**Profiling Workflow**:
1. Run Unity Profiler
2. Perform operations (drag, auto-transfer, etc.)
3. Identify slowest methods
4. Implement optimizations for those methods
5. Re-profile to verify improvement

**Check Locations**:
- `Scripts/Inventories/UniversalInventory.cs` - Main hot paths
- `Scripts/Inventories/InventoryStrategy.cs` - Strategy methods
- `Scripts/Rules/RuleValidator.cs` - Validation overhead

---

## Performance Best Practices

### General Guidelines

1. **Avoid LINQ in hot paths** - Use for loops instead
2. **Cache frequently accessed data** - Especially searches
3. **Batch visual updates** - Use dirty flags
4. **Limit algorithm complexity** - Add attempt limits for O(n²) operations
5. **Profile before optimizing** - Focus on actual bottlenecks

### Mobile Considerations

- Test on low-end devices (old Android/iOS)
- Target 30 FPS minimum on mobile
- Consider reducing max slots for mobile builds
- Disable expensive features (relocation) on mobile if needed

---

**[Back to SKILL.md](./SKILL.md)**
