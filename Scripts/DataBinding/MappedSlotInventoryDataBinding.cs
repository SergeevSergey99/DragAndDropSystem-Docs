using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Rules;
using UDND.Slots;

namespace UDND.DataBinding
{
    /// <summary>
    /// Slot-to-data binding with support for both single items and stacks.
    /// Internally it always works with lists (GetAll/Add/Remove/Clear).
    /// For single items, use the simple constructor and wrappers will be generated automatically.
    /// </summary>
    public readonly struct SlotBinding<TData, TAdapter>
        where TAdapter : class, IItemAdapter
    {
        /// <summary>All data items currently stored in the slot (for ReloadUI).</summary>
        public readonly Func<IReadOnlyList<TData>> GetAll;

        /// <summary>Add adapters to the slot (called from OnItemAddedToUI).</summary>
        public readonly Action<IReadOnlyList<TAdapter>> Add;

        /// <summary>Remove adapters from the slot (called from OnItemRemovedFromUI).</summary>
        public readonly Action<IReadOnlyList<TAdapter>> Remove;

        /// <summary>Optional adapter validation for CanDrop.</summary>
        public readonly Func<TAdapter, RuleResult> CanDrop;

        /// <summary>Optional adapter validation for CanStartDrag.</summary>
        public readonly Func<TAdapter, RuleResult> CanStartDrag;

        /// <summary>
        /// Constructor for single-item slots (one item per slot).
        /// Get/Set/Clear are automatically wrapped into a list-based API.
        /// </summary>
        public SlotBinding(
            Func<TData> get,
            Action<TAdapter> set,
            Action clear,
            Func<TAdapter, RuleResult> canDrop = null,
            Func<TAdapter, RuleResult> canStartDrag = null)
        {
            GetAll = () =>
            {
                var item = get();
                return item != null ? new[] { item } : Array.Empty<TData>();
            };
            Add = items => { if (items != null && items.Count > 0) set(items[0]); };
            Remove = _ => clear();
            CanDrop = canDrop;
            CanStartDrag = canStartDrag;
        }

        /// <summary>
        /// Constructor for stackable slots (multiple identical items in the slot).
        /// Allows controlling addition and removal of each instance individually.
        /// </summary>
        public SlotBinding(
            Func<IReadOnlyList<TData>> getAll,
            Action<IReadOnlyList<TAdapter>> add,
            Action<IReadOnlyList<TAdapter>> remove,
            Action clear = null,
            Func<TAdapter, RuleResult> canDrop = null,
            Func<TAdapter, RuleResult> canStartDrag = null)
        {
            GetAll = getAll;
            Add = add;
            Remove = remove;
            CanDrop = canDrop;
            CanStartDrag = canStartDrag;
        }
    }

    /// <summary>
    /// Template DataBinding for inventories with fixed named slots.
    /// Each slot is declaratively bound to data through SlotBinding in a dictionary.
    /// Supports both single items and stacks in the same BindingMap.
    /// Automatically handles ReloadUI, OnItemAdded, OnItemRemoved, CanDrop, and CanStartDrag.
    /// Derived classes only need to define CreateBindingMap() and CreateAdapter().
    ///
    /// TData is the data item type (for example, ItemModel)
    /// TAdapter is the adapter type implementing IItemAdapter (for example, ItemModelAdapter)
    ///
    /// Usage example:
    /// <code>
    /// public class EquipmentBinding : MappedSlotInventoryDataBinding&lt;ItemModel, ItemModelAdapter&gt;
    /// {
    ///     [SerializeField] private UniversalSlot _weaponSlot, _potionSlot;
    ///
    ///     protected override Dictionary&lt;ISlot, SlotBinding&lt;ItemModel, ItemModelAdapter&gt;&gt; CreateBindingMap() =&gt; new()
    ///     {
    ///         // Single item (simple constructor)
    ///         [_weaponSlot] = new(
    ///             get: () =&gt; _data.Weapon,
    ///             set: adapter =&gt; _data.Weapon = adapter.Model,
    ///             clear: () =&gt; _data.Weapon = null,
    ///             canDrop: adapter =&gt; adapter.Model.Type == ItemType.Weapon
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Only weapons are allowed")),
    ///
    ///         // Item stack (list constructor)
    ///         [_potionSlot] = new(
    ///             getAll: () =&gt; _data.Potions,
    ///             add: adapters =&gt; _data.AddPotions(adapters),
    ///             remove: adapters =&gt; _data.RemovePotions(adapters),
    ///             clear: () =&gt; _data.ClearPotions(),
    ///             canDrop: adapter =&gt; adapter.Model.Type == ItemType.Potion
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Only potions are allowed")),
    ///     };
    ///
    ///     protected override ItemModelAdapter CreateAdapter(ItemModel item) =&gt; new(item);
    /// }
    /// </code>
    /// </summary>
    public abstract class MappedSlotInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        private Dictionary<BaseSlot, SlotBinding<TData, TAdapter>> _bindingMap;

        /// <summary>
        /// Slot binding dictionary. Built once from CreateBindingMap().
        /// </summary>
        protected Dictionary<BaseSlot, SlotBinding<TData, TAdapter>> BindingMap
            => _bindingMap ??= CreateBindingMap();

        /// <summary>
        /// Create the binding dictionary: key is a slot, value is a SlotBinding with getter, setter,
        /// clear action, and optional validation.
        /// </summary>
        protected abstract Dictionary<BaseSlot, SlotBinding<TData, TAdapter>> CreateBindingMap();

        /// <summary>
        /// Create an adapter (IItemAdapter) from a data item.
        /// Called when data is loaded into the UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        protected bool TryGetTargetBinding(BaseSlot targetBaseSlot, out SlotBinding<TData, TAdapter> binding)
        {
            binding = default;
            return targetBaseSlot != null && BindingMap.TryGetValue(targetBaseSlot, out binding);
        }

        protected bool TryGetSourceBinding(BaseSlot sourceBaseSlot, out SlotBinding<TData, TAdapter> binding)
        {
            binding = default;
            return sourceBaseSlot != null && BindingMap.TryGetValue(sourceBaseSlot, out binding);
        }

        protected override void OnReloadUI()
        {
            foreach (var (slot, binding) in BindingMap)
            {
                var items = binding.GetAll();
                if (items == null || items.Count == 0) continue;

                var adapters = new List<IItemAdapter>(items.Count);
                foreach (var item in items)
                {
                    if (item != null)
                        adapters.Add(CreateAdapter(item));
                }

                if (adapters.Count == 0) continue;

                if (ItemStack.TryCreate(adapters, out var stack))
                {
                    var uiSlot = _inventory.GetSlot(slot.Index);
                    uiSlot?.SetStack(stack);
                }
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (!TryGetTargetBinding(context.ResolvedTargetBaseSlot, out var binding)) return;

            var added = FilterAdapters(context.Stack);
            if (added.Count > 0)
                binding.Add(added);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (!TryGetSourceBinding(context.ResolvedSourceBaseSlot, out var binding)) return;

            var removed = FilterAdapters(context.Stack);
            if (removed.Count > 0)
                binding.Remove(removed);
        }

        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is not TAdapter adapter)
                return RuleResult.Failure("Invalid item type");

            if (!TryGetSourceBinding(entry.SourceBaseSlot, out var binding))
                return RuleResult.Failure("Unknown slot");

            if (binding.CanStartDrag == null)
                return RuleResult.Success();

            return binding.CanStartDrag(adapter);
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is not TAdapter adapter)
                return RuleResult.Failure("Invalid item type");

            if (!TryGetTargetBinding(context?.TargetBaseSlot, out var binding))
                return RuleResult.Failure("Unknown slot");

            if (binding.CanDrop == null)
                return RuleResult.Success();

            return binding.CanDrop(adapter);
        }

        /// <summary>
        /// Filter adapters of the required type from the stack.
        /// </summary>
        private List<TAdapter> FilterAdapters(ItemStack stack)
        {
            var result = new List<TAdapter>(stack.Count);
            foreach (var adapter in stack.Adapters)
            {
                if (adapter is TAdapter typed)
                    result.Add(typed);
            }
            return result;
        }
    }
}
