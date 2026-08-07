# Performance Analysis & Optimizations

Performance considerations and optimization strategies.

**Last Updated**: 2026-08-06

## Hot Paths

### UpdateAllVisuals() - O(n)

Location:
- `Scripts/Inventories/UniversalInventory.cs`

Problem:
- updates all slots every time, even if only one changed

Potential future improvement:
- dirty flag system for slot visuals

### TryRelocateAndRetry() - O(n²)

Location:
- `Scripts/Inventories/SlotRelocationService.cs`

Problem:
- relocation may try many slot combinations when recovering from failed placement

Potential future improvement:
- limit relocation attempts
- prioritize candidates by rule compatibility

### Acceptance Preview - O(n × rules)

Locations:
- `Scripts/Inventories/UniversalInventory.cs`
- `Scripts/Inventories/Strategies/*.cs`

Problem:
- acceptance preview now validates real candidate slots via `InventoryAcceptanceRequest`
- this is correct behavior, but area-drop hover and planning preview may scan many slots

Current flow:
1. resolve target-side preview item
2. build `InventoryAcceptanceRequest`
3. strategy iterates slots
4. each candidate goes through rule checks

Potential future improvement:
- cache likely-acceptable slots per item type when safe
- cache empty slot lists

## Existing Optimization

### Drag-Scoped Conversion Memo

Location:
- `Scripts/Inventories/TransferConversionSession.cs`

Cross-inventory conversion allocates (a converter typically builds a new adapter, sometimes a new
domain model with it). Without memoization that happened on every probe, every preview and again at
mutation.

Current behavior:
- converted adapters are resolved once per `(source inventory, target inventory, source adapter)`
  and reused for the whole drag
- entries are consumed on commit, so a converted object is never offered twice
- failures are not cached

Correctness first, allocation second: reusing one object is what makes the previewed item and the
committed item the same instance.

### One Probe Per Hover

Location:
- `Scripts/Interaction/SlotInputAdapter.cs`, `Scripts/Inventories/DropPreviewController.cs`

`ShowDropPreview` used to probe, and each covered slot rendering drop feedback probed again — twice
per slot per hover, multiplied by the footprint of a shaped item. The processor's probe is now
resolved once and passed down; feedback visuals read the stored `DropVerdict`.

### Rule Caching

Location:
- `Scripts/Rules/RuleValidator.cs`

Current behavior:
- sorted rule list is cached
- cache is invalidated only when rules change

This avoids repeated sorting work during validation-heavy operations.

## Profiling Recommendations

Focus on:
1. `UpdateAllVisuals()`
2. `SlotRelocationService.TryRelocateAndRetry()`
3. acceptance preview (`InventoryAcceptanceRequest` path)
4. strategy placement methods (`TryAdd`, `TryAddToSlot`)

Check locations:
- `Scripts/Inventories/SlotRelocationService.cs`
- `Scripts/Inventories/UniversalInventory.cs`
- `Scripts/Inventories/Strategies/*.cs`
- `Scripts/Rules/RuleValidator.cs`

## General Guidance

- avoid LINQ in hot paths
- cache only after profiling proves a real bottleneck
- keep preview validation correct first, then optimize
- prefer bounded algorithms for relocation and recovery logic
