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
    public readonly struct SlotBinding<TData>
    {
        /// <summary>Все элементы данных, находящиеся в слоте.</summary>
        public readonly Func<IReadOnlyList<TData>> GetAll;

        /// <summary>Добавить элементы в слот (вызывается при OnItemAddedToUI).</summary>
        public readonly Action<IReadOnlyList<TData>> Add;

        /// <summary>Удалить элементы из слота (вызывается при OnItemRemovedFromUI).</summary>
        public readonly Action<IReadOnlyList<TData>> Remove;

        /// <summary>Полная очистка слота.</summary>
        public readonly Action Clear;

        /// <summary>Опциональная валидация одного элемента для CanDrop.</summary>
        public readonly Func<TData, RuleResult> CanDrop;
        
        /// <summary> Опциональная валидация одного элемента для CanStartDrag. </summary>
        public readonly Func<TData, RuleResult> CanStartDrag;

        /// <summary>
        /// Конструктор для единичных предметов (один предмет на слот).
        /// Get/Set/Clear автоматически оборачиваются в list-based API.
        /// </summary>
        public SlotBinding(
            Func<TData> get,
            Action<TData> set,
            Action clear,
            Func<TData, RuleResult> canDrop = null,
            Func<TData, RuleResult> canStartDrag = null)
        {
            GetAll = () =>
            {
                var item = get();
                return item != null ? new[] { item } : Array.Empty<TData>();
            };
            Add = items => { if (items != null && items.Count > 0) set(items[0]); };
            Remove = _ => clear();
            Clear = clear;
            CanDrop = canDrop;
            CanStartDrag = canStartDrag;
        }

        /// <summary>
        /// Конструктор для стекаемых слотов (несколько одинаковых предметов в слоте).
        /// Позволяет контролировать добавление/удаление каждого экземпляра индивидуально.
        /// </summary>
        public SlotBinding(
            Func<IReadOnlyList<TData>> getAll,
            Action<IReadOnlyList<TData>> add,
            Action<IReadOnlyList<TData>> remove,
            Action clear,
            Func<TData, RuleResult> canDrop = null,
            Func<TData, RuleResult> canStartDrag = null)
        {
            GetAll = getAll;
            Add = add;
            Remove = remove;
            Clear = clear;
            CanDrop = canDrop;
            CanStartDrag = canStartDrag;
        }
    }

    /// <summary>
    /// Шаблонный DataBinding для инвентарей с фиксированными именованными слотами.
    /// Каждый слот декларативно привязывается к данным через SlotBinding в словаре.
    /// Поддерживает как единичные предметы, так и стеки в одном BindingMap.
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
    ///     [SerializeField] private UniversalSlot _weaponSlot, _potionSlot;
    ///
    ///     protected override Dictionary&lt;ISlot, SlotBinding&lt;ItemModel&gt;&gt; CreateBindingMap() =&gt; new()
    ///     {
    ///         // Единичный предмет (простой конструктор)
    ///         [_weaponSlot] = new(
    ///             get: () =&gt; _data.Weapon,
    ///             set: item =&gt; _data.Weapon = item,
    ///             clear: () =&gt; _data.Weapon = null,
    ///             canAccept: item =&gt; item.Type == ItemType.Weapon
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Только оружие")),
    ///
    ///         // Стек предметов (list-конструктор)
    ///         [_potionSlot] = new(
    ///             getAll: () =&gt; _data.Potions,
    ///             add: items =&gt; _data.AddPotions(items),
    ///             remove: items =&gt; _data.RemovePotions(items),
    ///             clear: () =&gt; _data.ClearPotions(),
    ///             canAccept: item =&gt; item.Type == ItemType.Potion
    ///                 ? RuleResult.Success()
    ///                 : RuleResult.Failure("Только зелья")),
    ///     };
    ///
    ///     protected override ItemModelAdapter CreateAdapter(ItemModel item) =&gt; new(item);
    ///     protected override ItemModel ExtractData(ItemModelAdapter a) =&gt; a.Model;
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
            if (!TryGetTargetBinding(context.TargetSlot, out var binding)) return;

            var added = ExtractDataList(context.Stack);
            if (added.Count > 0)
                binding.Add(added);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (!TryGetSourceBinding(context.SourceSlot, out var binding)) return;

            var removed = ExtractDataList(context.Stack);
            if (removed.Count > 0)
                binding.Remove(removed);
        }

        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is not TAdapter adapter)
                return RuleResult.Failure("Неверный тип предмета");
            
            if (!TryGetTargetBinding(context?.TargetSlot, out var binding))
                return RuleResult.Failure("Неизвестный слот");
            
            if (binding.CanStartDrag == null)
                return RuleResult.Success();
            
            var data = ExtractData(adapter);
            return data != null
                ? binding.CanStartDrag(data)
                : RuleResult.Failure("Нет данных");
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.PrimaryAdapter is not TAdapter adapter)
                return RuleResult.Failure("Неверный тип предмета");

            if (!TryGetTargetBinding(context?.TargetSlot, out var binding))
                return RuleResult.Failure("Неизвестный слот");

            if (binding.CanDrop == null)
                return RuleResult.Success();

            var data = ExtractData(adapter);
            return data != null
                ? binding.CanDrop(data)
                : RuleResult.Failure("Нет данных");
        }

        /// <summary>
        /// Извлечь список TData из всех адаптеров в стеке.
        /// </summary>
        private List<TData> ExtractDataList(ItemStack stack)
        {
            var result = new List<TData>(stack.Count);
            foreach (var adapter in stack.Adapters)
            {
                if (adapter is TAdapter typed)
                {
                    var data = ExtractData(typed);
                    if (data != null)
                        result.Add(data);
                }
            }
            return result;
        }
    }
}
