using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Tests
{
    /// <summary>
    /// Fluent helper that assembles a DragContext from an already-populated inventory,
    /// mirroring the production pattern used by AutoTransferService.
    ///
    /// The executor only consumes the stack Count per entry (the source slot's own stack
    /// is split by the executor), so we can build entries by copying adapters straight
    /// from each non-empty source slot.
    ///
    /// Usage:
    ///   var ctx = DragContextBuilder.FromAllSlots(source).ToTarget(target).Build();
    ///   var ctx = DragContextBuilder.FromSlots(source, 0, 2).ToTargetSlot(slot, target).Build();
    /// </summary>
    public sealed class DragContextBuilder
    {
        private readonly IInventory _source;
        private readonly List<DragEntry> _entries = new List<DragEntry>();
        private IInventory _targetInventory;
        private UniversalDragAndDrop.Slots.BaseSlot _targetSlot;

        private DragContextBuilder(IInventory source)
        {
            _source = source;
        }

        /// <summary>
        /// Entry point: build a context by pulling specific slot indices from <paramref name="source"/>.
        /// Empty slots are skipped (mirrors real drag behavior — you can't drag nothing).
        /// </summary>
        public static DragContextBuilder FromSlots(IInventory source, params int[] slotIndices)
        {
            Assert.IsNotNull(source, "DragContextBuilder.FromSlots: source is null");

            var builder = new DragContextBuilder(source);
            foreach (var index in slotIndices)
                builder.AddSlot(index);
            return builder;
        }

        /// <summary>
        /// Entry point: pull every non-empty slot from <paramref name="source"/>.
        /// </summary>
        public static DragContextBuilder FromAllSlots(IInventory source)
        {
            Assert.IsNotNull(source, "DragContextBuilder.FromAllSlots: source is null");

            var builder = new DragContextBuilder(source);
            for (int i = 0; i < source.SlotCount; i++)
                builder.AddSlot(i);
            return builder;
        }

        /// <summary>
        /// Area drop: no specific target slot, only a target inventory.
        /// </summary>
        public DragContextBuilder ToTarget(IInventory target)
        {
            _targetInventory = target;
            _targetSlot = null;
            return this;
        }

        /// <summary>
        /// Slot-specific drop: target slot inside target inventory.
        /// </summary>
        public DragContextBuilder ToTargetSlot(UniversalDragAndDrop.Slots.BaseSlot targetSlot, IInventory targetInventory)
        {
            _targetSlot = targetSlot;
            _targetInventory = targetInventory;
            return this;
        }

        public DragContext Build()
        {
            Assert.IsTrue(_entries.Count > 0,
                "DragContextBuilder.Build: no non-empty source slots produced any drag entries");

            var context = new DragContext(_entries);
            if (_targetSlot != null || _targetInventory != null)
                context.SetTarget(_targetSlot, _targetInventory);
            return context;
        }

        private void AddSlot(int index)
        {
            Assert.IsTrue(index >= 0 && index < _source.SlotCount,
                $"DragContextBuilder: slot index {index} out of range [0, {_source.SlotCount})");

            var slot = _source.GetSlot(index);
            if (slot == null || slot.IsEmpty)
                return;

            Assert.IsTrue(ItemStack.TryCreate(slot.Stack.Adapters.ToArray(), out var entryStack),
                $"DragContextBuilder: could not clone stack from source slot {index}");

            _entries.Add(new DragEntry(entryStack, slot, _source));
        }
    }
}
