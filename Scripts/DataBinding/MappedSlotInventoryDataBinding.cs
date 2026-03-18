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
        public readonly UniversalSlot Slot;
        public readonly Func<TData> Get;
        public readonly Action<TData> Set;
        public readonly Action Clear;
        public readonly Func<TData, RuleResult> CanAccept;

        public SlotBinding(
            UniversalSlot slot,
            Func<TData> get,
            Action<TData> set,
            Action clear,
            Func<TData, RuleResult> canAccept = null)
        {
            Slot = slot;
            Get = get;
            Set = set;
            Clear = clear;
            CanAccept = canAccept;
        }
    }

    /// <summary>
    /// Шаблонный DataBinding для инвентарей с фиксированными именованными слотами.
    /// Каждый слот декларативно привязывается к данным через SlotBinding.
    /// Автоматически обрабатывает ReloadUI, OnItemAdded, OnItemRemoved и CanDrop —
    /// наследнику достаточно определить GetSlotBindings(), CreateAdapter() и ExtractData().
    ///
    /// TData — тип элемента данных (например, ItemModel)
    /// TAdapter — тип адаптера, реализующий IInventoryItem (например, ItemModelAdapter)
    ///
    /// Пример использования:
    /// <code>
    /// public class EquipmentBinding : MappedSlotInventoryDataBinding&lt;ItemModel, ItemModelAdapter&gt;
    /// {
    ///     [SerializeField] private UniversalSlot _weaponSlot, _armorSlot;
    ///
    ///     protected override IEnumerable&lt;SlotBinding&lt;ItemModel&gt;&gt; GetSlotBindings()
    ///     {
    ///         yield return new(_weaponSlot,
    ///             get: () =&gt; _data.Weapon,
    ///             set: item =&gt; _data.Weapon = item,
    ///             clear: () =&gt; _data.Weapon = null,
    ///             canAccept: item =&gt; item.Type == ItemType.Weapon
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Только оружие"));
    ///         yield return new(_armorSlot,
    ///             get: () =&gt; _data.Armor,
    ///             set: item =&gt; _data.Armor = item,
    ///             clear: () =&gt; _data.Armor = null);
    ///     }
    ///
    ///     protected override ItemModelAdapter CreateAdapter(ItemModel item) =&gt; new(item);
    ///     protected override ItemModel ExtractData(ItemModelAdapter a) =&gt; a.Item;
    /// }
    /// </code>
    /// </summary>
    public abstract class MappedSlotInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IInventoryItem
    {
        /// <summary>
        /// Декларативно описать привязки слотов к данным.
        /// Каждый yield return — один именованный слот.
        /// </summary>
        protected abstract IEnumerable<SlotBinding<TData>> GetSlotBindings();

        /// <summary>
        /// Создать адаптер (IInventoryItem) из элемента данных.
        /// Вызывается при загрузке данных в UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Извлечь элемент данных из адаптера.
        /// Вызывается при добавлении/удалении предмета через drag&amp;drop.
        /// Может вернуть default если адаптер не содержит нужных данных.
        /// </summary>
        protected abstract TData ExtractData(TAdapter adapter);

        protected override void OnReloadUI()
        {
            foreach (var binding in GetSlotBindings())
            {
                var data = binding.Get();
                if (data == null) continue;

                AddToUIQuiet(CreateAdapter(data), 1, binding.Slot.Index);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Item is not TAdapter adapter) return;

            var data = ExtractData(adapter);
            if (data == null) return;

            foreach (var binding in GetSlotBindings())
            {
                if (!ReferenceEquals(context.TargetSlot, binding.Slot)) continue;

                binding.Set(data);
                return;
            }
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            foreach (var binding in GetSlotBindings())
            {
                if (!ReferenceEquals(context.SourceSlot, binding.Slot)) continue;

                binding.Clear();
                return;
            }
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.Item is not TAdapter adapter)
                return RuleResult.Failure("Неверный тип предмета");

            foreach (var binding in GetSlotBindings())
            {
                if (!ReferenceEquals(context.TargetSlot, binding.Slot)) continue;

                if (binding.CanAccept == null)
                    return RuleResult.Success();

                var data = ExtractData(adapter);
                return data != null
                    ? binding.CanAccept(data)
                    : RuleResult.Failure("Нет данных");
            }

            return RuleResult.Failure("Неизвестный слот");
        }
    }
}
