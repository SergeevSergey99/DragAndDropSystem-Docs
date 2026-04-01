using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.DataBinding
{
    /// <summary>
    /// Привязка слота к данным: геттер, сеттер, очистка и опциональная валидация.
    /// </summary>
    public readonly struct SlotBinding<TData>
    {
        public readonly Func<TData> Get;
        public readonly Action<TData> Set;
        public readonly Action Clear;
        public readonly Func<TData, RuleResult> CanAccept;

        public SlotBinding(
            Func<TData> get,
            Action<TData> set,
            Action clear,
            Func<TData, RuleResult> canAccept = null)
        {
            Get = get;
            Set = set;
            Clear = clear;
            CanAccept = canAccept;
        }
    }

    /// <summary>
    /// Шаблонный DataBinding для инвентарей с фиксированными именованными слотами.
    /// Каждый слот декларативно привязывается к данным через SlotBinding в словаре.
    /// Автоматически обрабатывает ReloadUI, OnItemAdded, OnItemRemoved и CanDrop —
    /// наследнику достаточно определить CreateBindingMap(), CreateAdapter() и ExtractData().
    ///
    /// TData — тип элемента данных (например, ItemModel)
    /// TAdapter — тип адаптера, реализующий IItemAdapter (например, ItemModelAdapter)
    ///
    /// Пример использования:
    /// <code>
    /// public class EquipmentBinding : MappedSlotInventoryDataBinding&lt;ItemModel, ItemModelAdapter&gt;
    /// {
    ///     [SerializeField] private UniversalSlot _weaponSlot, _armorSlot;
    ///
    ///     protected override Dictionary&lt;ISlot, SlotBinding&lt;ItemModel&gt;&gt; CreateBindingMap() =&gt; new()
    ///     {
    ///         [_weaponSlot] = new(
    ///             get: () =&gt; _data.Weapon,
    ///             set: itemAdapter =&gt; _data.Weapon = itemAdapter,
    ///             clear: () =&gt; _data.Weapon = null,
    ///             canAccept: itemAdapter =&gt; itemAdapter.Type == ItemType.Weapon
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Только оружие")),
    ///         [_armorSlot] = new(
    ///             get: () =&gt; _data.Armor,
    ///             set: itemAdapter =&gt; _data.Armor = itemAdapter,
    ///             clear: () =&gt; _data.Armor = null),
    ///     };
    ///
    ///     protected override ItemModelAdapter CreateAdapter(ItemModel itemAdapter) =&gt; new(itemAdapter);
    ///     protected override ItemModel ExtractData(ItemModelAdapter a) =&gt; a.PrimaryAdapter;
    /// }
    /// </code>
    /// </summary>
    public abstract class MappedSlotInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        private Dictionary<ISlot, SlotBinding<TData>> _bindingMap;

        /// <summary>
        /// Словарь привязок слотов. Строится один раз из CreateBindingMap().
        /// </summary>
        protected Dictionary<ISlot, SlotBinding<TData>> BindingMap
            => _bindingMap ??= CreateBindingMap();

        /// <summary>
        /// Создать словарь привязок: ключ — слот, значение — SlotBinding с геттером, сеттером,
        /// очисткой и опциональной валидацией.
        /// </summary>
        protected abstract Dictionary<ISlot, SlotBinding<TData>> CreateBindingMap();

        /// <summary>
        /// Создать адаптер (IItemAdapter) из элемента данных.
        /// Вызывается при загрузке данных в UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Извлечь элемент данных из адаптера.
        /// Вызывается при добавлении/удалении предмета через drag&amp;drop.
        /// Может вернуть default если адаптер не содержит нужных данных.
        /// </summary>
        protected abstract TData ExtractData(TAdapter adapter);

        protected bool TryGetTargetBinding(ISlot targetSlot, out SlotBinding<TData> binding)
        {
            binding = default;
            return targetSlot != null && BindingMap.TryGetValue(targetSlot, out binding);
        }

        protected bool TryGetSourceBinding(ISlot sourceSlot, out SlotBinding<TData> binding)
        {
            binding = default;
            return sourceSlot != null && BindingMap.TryGetValue(sourceSlot, out binding);
        }

        protected override void OnReloadUI()
        {
            foreach (var (slot, binding) in BindingMap)
            {
                var data = binding.Get();
                if (data == null) continue;

                AddToUIQuiet(CreateAdapter(data), 1, slot.Index);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter adapter) return;

            var data = ExtractData(adapter);
            if (data == null) return;

            if (TryGetTargetBinding(context.TargetSlot, out var binding))
                binding.Set(data);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (TryGetSourceBinding(context.SourceSlot, out var binding))
                binding.Clear();
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is not TAdapter adapter)
                return RuleResult.Failure("Неверный тип предмета");

            if (!TryGetTargetBinding(context?.TargetSlot, out var binding))
                return RuleResult.Failure("Неизвестный слот");

            if (binding.CanAccept == null)
                return RuleResult.Success();

            var data = ExtractData(adapter);
            return data != null
                ? binding.CanAccept(data)
                : RuleResult.Failure("Нет данных");
        }
    }
}
