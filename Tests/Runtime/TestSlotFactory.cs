using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests
{
    /// <summary>
    /// Creates lists of BaseSlot-backed GameObjects for strategy-level tests.
    /// Always call Dispose in [TearDown] to avoid leaking GameObjects between tests.
    /// By default slots are backed by a minimal IInventory stack store so
    /// strategy tests exercise the same mutation boundary as runtime inventories.
    /// </summary>
    public static class TestSlotFactory
    {
        public static List<BaseSlot> CreateSlots(int count, IInventory inventory = null)
        {
            var list = new List<BaseSlot>(count);
            var owner = inventory ?? new TestInventoryStore(list);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"TestSlot_{i}");
                var slot = go.AddComponent<TestSlot>();
                slot.Initialize(i, owner);
                list.Add(slot);
            }
            return list;
        }

        public static BaseSlot CreatePrefab(string name = "TestSlotPrefab")
        {
            var go = new GameObject(name);
            return go.AddComponent<TestSlot>();
        }

        public static void Dispose(List<BaseSlot> slots)
        {
            if (slots == null) return;
            foreach (var s in slots)
            {
                if (s != null && s.gameObject != null)
                    Object.DestroyImmediate(s.gameObject);
            }
            slots.Clear();
        }

        public static void Dispose(BaseSlot slot)
        {
            if (slot != null && slot.gameObject != null)
                Object.DestroyImmediate(slot.gameObject);
        }

        private sealed class TestInventoryStore : IInventory
        {
            private readonly List<BaseSlot> _slots;
            private readonly List<ItemStack> _stacks = new List<ItemStack>();

            public TestInventoryStore(List<BaseSlot> slots)
            {
                _slots = slots;
            }

            public IReadOnlyList<BaseSlot> Slots => _slots;
            public int SlotCount => _slots.Count;
            public InventoryDataBindingBase DataBinding => null;

            public BaseSlot GetSlot(int index)
                => index >= 0 && index < _slots.Count ? _slots[index] : null;

            public bool TryAddStack(ItemStack stack, int targetSlotIndex = -1)
            {
                if (stack == null || stack.IsEmpty)
                    return false;

                if (targetSlotIndex >= 0 && targetSlotIndex < _slots.Count)
                    return TrySetStackForSlot(_slots[targetSlotIndex], stack);

                foreach (var slot in _slots)
                {
                    EnsureStackCapacity(slot.Index);
                    if (!_stacks[slot.Index].IsEmpty)
                        continue;

                    return TrySetStackForSlot(slot, stack);
                }

                return false;
            }

            public bool TryAddStackQuiet(ItemStack stack, int targetSlotIndex = -1)
            {
                // For testing purposes, treat quiet add the same as normal add since we don't have rules or events to worry about}
                return TryAddStack(stack, targetSlotIndex);
            }

            public bool Contains(IItemAdapter itemAdapter)
            {
                if (itemAdapter == null)
                    return false;

                foreach (var stack in _stacks)
                {
                    if (stack != null && !stack.IsEmpty && stack.CanStack(itemAdapter))
                        return true;
                }

                return false;
            }

            public void UpdateAllVisuals()
            {
                foreach (var slot in _slots)
                    slot?.UpdateVisuals();
            }

            public int GetDragAmount(BaseSlot baseSlot, DragAmount? overrideAmount = null, int? overrideCustom = null)
                => baseSlot?.Stack?.Count ?? 0;

            public bool TryAddToSlot(
                ItemStack stack,
                BaseSlot targetBaseSlot,
                IInventory sourceInventory = null,
                int sourceSlotIndex = -1,
                SlotOperationContext operationContext = null)
                => TrySetStackForSlot(targetBaseSlot, stack);

            public int RemoveItemsFromSlot(
                BaseSlot sourceBaseSlot,
                ItemStack stackToRemove,
                IInventory targetInventory = null,
                BaseSlot targetBaseSlot = null)
            {
                if (stackToRemove == null || stackToRemove.IsEmpty)
                    return 0;

                return TryRemoveFromSlot(sourceBaseSlot, stackToRemove.Adapters, out int removed)
                    ? removed
                    : 0;
            }

            public int GetAcceptableCount(InventoryAcceptanceRequest request)
                => request?.DesiredCount ?? 0;

            public bool TryGetStackForSlot(BaseSlot baseSlot, out IReadOnlyItemStack stack)
            {
                stack = ItemStack.Empty();
                if (!TryResolveSlotIndex(baseSlot, out int index))
                    return false;

                EnsureStackCapacity(index);
                stack = _stacks[index] ?? ItemStack.Empty();
                return true;
            }

            public bool TrySetStackForSlot(BaseSlot baseSlot, ItemStack stack)
            {
                if (!TryResolveSlotIndex(baseSlot, out int index))
                    return false;

                EnsureStackCapacity(index);
                _stacks[index] = stack ?? ItemStack.Empty();
                return true;
            }

            public bool TryClearSlot(BaseSlot baseSlot)
                => TrySetStackForSlot(baseSlot, ItemStack.Empty());

            public bool TryGetPlacementAt(BaseSlot baseSlot, out Placement placement)
            {
                placement = null;
                return false;
            }

            public Vector2Int GetGrabOffset(Placement placement, BaseSlot baseSlot)
                => Vector2Int.zero;

            public bool TrySplitFromSlot(BaseSlot baseSlot, int amount, out ItemStack splitStack)
            {
                splitStack = ItemStack.Empty();
                if (amount <= 0 || !TryResolveSlotIndex(baseSlot, out int index))
                    return false;

                EnsureStackCapacity(index);
                var stack = _stacks[index];
                if (stack == null || stack.IsEmpty)
                    return false;

                splitStack = stack.Split(amount);
                if (splitStack == null || splitStack.IsEmpty)
                    return false;

                if (stack.IsEmpty)
                    _stacks[index] = ItemStack.Empty();

                baseSlot.UpdateVisuals();
                return true;
            }

            public bool TryAddToSlotStack(BaseSlot baseSlot, ItemStack stack)
            {
                if (stack == null || stack.IsEmpty || !TryResolveSlotIndex(baseSlot, out int index))
                    return false;

                EnsureStackCapacity(index);
                var existingStack = _stacks[index];
                if (existingStack == null || existingStack.IsEmpty)
                {
                    _stacks[index] = stack;
                    baseSlot.UpdateVisuals();
                    return true;
                }

                bool added = existingStack.TryAddToStack(stack);
                if (added)
                    baseSlot.UpdateVisuals();

                return added;
            }

            public bool TryRemoveFromSlot(BaseSlot baseSlot, IReadOnlyList<IItemAdapter> adapters, out int removed)
            {
                removed = 0;
                if (adapters == null || adapters.Count == 0 || !TryResolveSlotIndex(baseSlot, out int index))
                    return false;

                EnsureStackCapacity(index);
                var stack = _stacks[index];
                if (stack == null || stack.IsEmpty)
                    return false;

                removed = stack.RemoveAdapters(adapters);
                if (removed <= 0)
                    return false;

                if (stack.IsEmpty)
                    _stacks[index] = ItemStack.Empty();

                baseSlot.UpdateVisuals();
                return true;
            }

            private bool TryResolveSlotIndex(BaseSlot baseSlot, out int index)
            {
                index = -1;
                if (baseSlot == null || !ReferenceEquals(baseSlot.Inventory, this))
                    return false;

                index = baseSlot.Index;
                return index >= 0 && index < _slots.Count;
            }

            private void EnsureStackCapacity(int index)
            {
                while (_stacks.Count <= index)
                    _stacks.Add(ItemStack.Empty());
            }
        }
    }
}
