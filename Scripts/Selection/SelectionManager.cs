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
    [DisallowMultipleComponent]
    public class SelectionManager : MonoSingleton<SelectionManager>
    {
        // Внутреннее мутабельное состояние
        private readonly HashSet<BaseSlot> _selected = new HashSet<BaseSlot>();
        private readonly Dictionary<IInventory, List<BaseSlot>> _byInventory = new Dictionary<IInventory, List<BaseSlot>>();

        // Последний выделенный слот — нужен для диапазонного выделения (Shift+Click)
        private BaseSlot _lastSelectedBaseSlot;

        /// <summary>
        /// Текущий неизменяемый снимок выделения.
        /// Пересоздаётся при каждом изменении.
        /// </summary>
        public SelectionContext CurrentContext { get; private set; } = SelectionContext.Empty;

        /// <summary>
        /// Вызывается при любом изменении выделения
        /// </summary>
        public static event Action<SelectionContext> OnSelectionChanged;

        // ===== Публичное API =====

        public bool IsSelected(BaseSlot baseSlot) => baseSlot != null && _selected.Contains(baseSlot);

        /// <summary>
        /// Добавить слот к выделению
        /// </summary>
        public void Select(BaseSlot baseSlot)
        {
            if (baseSlot == null || _selected.Contains(baseSlot)) return;
            AddInternal(baseSlot);
            _lastSelectedBaseSlot = baseSlot;
            RebuildContext();
        }

        /// <summary>
        /// Убрать слот из выделения
        /// </summary>
        public void Deselect(BaseSlot baseSlot)
        {
            if (baseSlot == null || !_selected.Contains(baseSlot)) return;
            RemoveInternal(baseSlot);
            RebuildContext();
        }

        /// <summary>
        /// Переключить состояние выделения слота
        /// </summary>
        public void Toggle(BaseSlot baseSlot)
        {
            if (baseSlot == null) return;
            if (_selected.Contains(baseSlot)) Deselect(baseSlot);
            else Select(baseSlot);
        }

        /// <summary>
        /// Выделить диапазон слотов от последнего выделенного до указанного (в одном инвентаре).
        /// Если нет последнего выделенного или он из другого инвентаря — просто выделяет указанный слот.
        /// </summary>
        public void SelectRange(BaseSlot toBaseSlot)
        {
            if (toBaseSlot == null) return;

            if (_lastSelectedBaseSlot == null || _lastSelectedBaseSlot.Inventory != toBaseSlot.Inventory)
            {
                Select(toBaseSlot);
                return;
            }

            int from = _lastSelectedBaseSlot.Index;
            int to   = toBaseSlot.Index;
            int min  = Mathf.Min(from, to);
            int max  = Mathf.Max(from, to);

            var inventory = toBaseSlot.Inventory;
            for (int i = min; i <= max; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !_selected.Contains(slot))
                    AddInternal(slot);
            }

            _lastSelectedBaseSlot = toBaseSlot;
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
            _lastSelectedBaseSlot = null;
            RebuildContext();
        }

        // ===== Приватные методы =====

        private void AddInternal(BaseSlot baseSlot)
        {
            _selected.Add(baseSlot);

            if (!_byInventory.TryGetValue(baseSlot.Inventory, out var list))
            {
                list = new List<BaseSlot>();
                _byInventory[baseSlot.Inventory] = list;
            }
            list.Add(baseSlot);
        }

        private void RemoveInternal(BaseSlot baseSlot)
        {
            _selected.Remove(baseSlot);

            if (!_byInventory.TryGetValue(baseSlot.Inventory, out var list)) return;
            list.Remove(baseSlot);
            if (list.Count == 0)
                _byInventory.Remove(baseSlot.Inventory);
        }

        private void RebuildContext()
        {
            // Строим плоский список из _byInventory чтобы сохранить порядок по инвентарям
            var allSlots = new List<BaseSlot>(_selected.Count);
            foreach (var slots in _byInventory.Values)
                allSlots.AddRange(slots);

            CurrentContext = new SelectionContext(_byInventory, allSlots, _selected);
            OnSelectionChanged?.Invoke(CurrentContext);
        }
    }
}
