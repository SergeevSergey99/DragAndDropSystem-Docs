using System;
using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Синглтон, управляющий состоянием выделения слотов.
    /// Внутри работает с мутабельными структурами, наружу отдаёт только неизменяемый SelectionContext.
    /// </summary>
    public class SelectionManager : MonoSingleton<SelectionManager>
    {
        // Внутреннее мутабельное состояние
        private readonly HashSet<ISlot> _selected = new HashSet<ISlot>();
        private readonly Dictionary<IInventory, List<ISlot>> _byInventory = new Dictionary<IInventory, List<ISlot>>();

        // Последний выделенный слот — нужен для диапазонного выделения (Shift+Click)
        private ISlot _lastSelectedSlot;

        /// <summary>
        /// Текущий неизменяемый снимок выделения.
        /// Пересоздаётся при каждом изменении.
        /// </summary>
        public SelectionContext CurrentContext { get; private set; } = SelectionContext.Empty;

        /// <summary>
        /// Вызывается при любом изменении выделения
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs> OnSelectionChanged;

        // ===== Публичное API =====

        public bool IsSelected(ISlot slot) => slot != null && _selected.Contains(slot);

        /// <summary>
        /// Добавить слот к выделению
        /// </summary>
        public void Select(ISlot slot)
        {
            if (slot == null || _selected.Contains(slot)) return;
            AddInternal(slot);
            _lastSelectedSlot = slot;
            RebuildContext();
        }

        /// <summary>
        /// Убрать слот из выделения
        /// </summary>
        public void Deselect(ISlot slot)
        {
            if (slot == null || !_selected.Contains(slot)) return;
            RemoveInternal(slot);
            RebuildContext();
        }

        /// <summary>
        /// Переключить состояние выделения слота
        /// </summary>
        public void Toggle(ISlot slot)
        {
            if (slot == null) return;
            if (_selected.Contains(slot)) Deselect(slot);
            else Select(slot);
        }

        /// <summary>
        /// Выделить диапазон слотов от последнего выделенного до указанного (в одном инвентаре).
        /// Если нет последнего выделенного или он из другого инвентаря — просто выделяет указанный слот.
        /// </summary>
        public void SelectRange(ISlot toSlot)
        {
            if (toSlot == null) return;

            if (_lastSelectedSlot == null || _lastSelectedSlot.Inventory != toSlot.Inventory)
            {
                Select(toSlot);
                return;
            }

            int from = _lastSelectedSlot.Index;
            int to   = toSlot.Index;
            int min  = Mathf.Min(from, to);
            int max  = Mathf.Max(from, to);

            var inventory = toSlot.Inventory;
            for (int i = min; i <= max; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !_selected.Contains(slot))
                    AddInternal(slot);
            }

            _lastSelectedSlot = toSlot;
            RebuildContext();
        }

        /// <summary>
        /// Выделить все слоты инвентаря
        /// </summary>
        public void SelectAll(IInventory inventory)
        {
            if (inventory == null) return;

            foreach (var slot in inventory.Slots)
            {
                if (slot != null && !_selected.Contains(slot))
                    AddInternal(slot);
            }

            RebuildContext();
        }

        /// <summary>
        /// Снять все выделения
        /// </summary>
        public void Clear()
        {
            if (_selected.Count == 0) return;
            _selected.Clear();
            _byInventory.Clear();
            _lastSelectedSlot = null;
            RebuildContext();
        }

        // ===== Приватные методы =====

        private void AddInternal(ISlot slot)
        {
            _selected.Add(slot);

            if (!_byInventory.TryGetValue(slot.Inventory, out var list))
            {
                list = new List<ISlot>();
                _byInventory[slot.Inventory] = list;
            }
            list.Add(slot);
        }

        private void RemoveInternal(ISlot slot)
        {
            _selected.Remove(slot);

            if (!_byInventory.TryGetValue(slot.Inventory, out var list)) return;
            list.Remove(slot);
            if (list.Count == 0)
                _byInventory.Remove(slot.Inventory);
        }

        private void RebuildContext()
        {
            // Строим плоский список из _byInventory чтобы сохранить порядок по инвентарям
            var allSlots = new List<ISlot>(_selected.Count);
            foreach (var slots in _byInventory.Values)
                allSlots.AddRange(slots);

            CurrentContext = new SelectionContext(_byInventory, allSlots, _selected);
            OnSelectionChanged?.Invoke(this, new SelectionChangedEventArgs(CurrentContext));
        }
    }
}
