using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// Привязка слота к данным с поддержкой как единичных предметов, так и стеков.
    /// Внутренне всегда работает со списками (GetAll/Add/Remove/Clear).
    /// Для единичных предметов используйте простой конструктор — обёртки генерируются автоматически.
    /// </summary>
    public readonly struct SlotBinding<TData, TAdapter>
        where TAdapter : class, IItemAdapter
    {
        /// <summary>Все элементы данных, находящиеся в слоте (для ReloadUI).</summary>
        public readonly Func<IReadOnlyList<TData>> GetAll;

        /// <summary>Добавить адаптеры в слот (вызывается при OnItemAddedToUI).</summary>
        public readonly Action<IReadOnlyList<TAdapter>> Add;

        /// <summary>Удалить адаптеры из слота (вызывается при OnItemRemovedFromUI).</summary>
        public readonly Action<IReadOnlyList<TAdapter>> Remove;

        /// <summary>Опциональная валидация адаптера для CanDrop.</summary>
        public readonly Func<TAdapter, RuleResult> CanDrop;

        /// <summary>Опциональная валидация адаптера для CanStartDrag.</summary>
        public readonly Func<TAdapter, RuleResult> CanStartDrag;

        /// <summary>
        /// Конструктор для единичных предметов (один предмет на слот).
        /// Get/Set/Clear автоматически оборачиваются в list-based API.
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
        /// Конструктор для стекаемых слотов (несколько одинаковых предметов в слоте).
        /// Позволяет контролировать добавление/удаление каждого экземпляра индивидуально.
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
    /// Шаблонный DataBinding для инвентарей с фиксированными именованными слотами.
    /// Каждый слот декларативно привязывается к данным через SlotBinding в словаре.
    /// Поддерживает как единичные предметы, так и стеки в одном BindingMap.
    /// Автоматически обрабатывает ReloadUI, OnItemAdded, OnItemRemoved, CanDrop и CanStartDrag —
    /// наследнику достаточно определить CreateBindingMap() и CreateAdapter().
    ///
    /// TData — тип элемента данных (например, ItemModel)
    /// TAdapter — тип адаптера, реализующий IItemAdapter (например, ItemModelAdapter)
    ///
    /// Пример использования:
    /// <code>
    /// public class EquipmentBinding : MappedSlotInventoryDataBinding&lt;ItemModel, ItemModelAdapter&gt;
    /// {
    ///     [SerializeField] private UniversalSlot _weaponSlot, _potionSlot;
    ///
    ///     protected override Dictionary&lt;ISlot, SlotBinding&lt;ItemModel, ItemModelAdapter&gt;&gt; CreateBindingMap() =&gt; new()
    ///     {
    ///         // Единичный предмет (простой конструктор)
    ///         [_weaponSlot] = new(
    ///             get: () =&gt; _data.Weapon,
    ///             set: adapter =&gt; _data.Weapon = adapter.Model,
    ///             clear: () =&gt; _data.Weapon = null,
    ///             canDrop: adapter =&gt; adapter.Model.Type == ItemType.Weapon
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Only weapons are allowed")),
    ///
    ///         // Стек предметов (list-конструктор)
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
        /// Словарь привязок слотов. Строится один раз из CreateBindingMap().
        /// </summary>
        protected Dictionary<BaseSlot, SlotBinding<TData, TAdapter>> BindingMap
            => _bindingMap ??= CreateBindingMap();

        /// <summary>
        /// Создать словарь привязок: ключ — слот, значение — SlotBinding с геттером, сеттером,
        /// очисткой и опциональной валидацией.
        /// </summary>
        protected abstract Dictionary<BaseSlot, SlotBinding<TData, TAdapter>> CreateBindingMap();

        /// <summary>
        /// Создать адаптер (IItemAdapter) из элемента данных.
        /// Вызывается при загрузке данных в UI (ReloadUI).
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
            if (!TryGetTargetBinding(context.TargetBaseSlot, out var binding)) return;

            var added = FilterAdapters(context.Stack);
            if (added.Count > 0)
                binding.Add(added);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (!TryGetSourceBinding(context.SourceBaseSlot, out var binding)) return;

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
        /// Отфильтровать адаптеры нужного типа из стека.
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
