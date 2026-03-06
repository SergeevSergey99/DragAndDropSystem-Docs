using System.Collections.Generic;
using DragAndDropSystem.Inventories;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Привязывает пресеты контекстного меню к конкретному инвентарю.
    /// Добавьте на тот же GameObject что и <see cref="UniversalInventory"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ContextMenuBinder : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;

        [SerializeField, Tooltip("Пункты меню для непустого слота.")]
        private ContextMenuPreset _preset;

        [SerializeField, Tooltip("Пункты меню для пустого слота. Если не задан — используется основной пресет.")]
        private ContextMenuPreset _emptySlotPreset;

        [SerializeField, Tooltip("Сценовые пункты меню для непустого слота.")]
        private List<ContextMenuSceneEntryBase> _sceneEntries = new();

        [SerializeField, Tooltip("Сценовые пункты меню для пустого слота. Если не заданы — используются обычные сценовые пункты.")]
        private List<ContextMenuSceneEntryBase> _emptySlotSceneEntries = new();

        private readonly List<IContextMenuEntry> _resolvedEntries = new();

        public UniversalInventory Inventory => _inventory;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        /// <summary>
        /// Вернуть записи пресета для данного состояния слота.
        /// </summary>
        public IReadOnlyList<IContextMenuEntry> GetEntries(bool slotIsEmpty)
        {
            _resolvedEntries.Clear();

            var preset = (slotIsEmpty && _emptySlotPreset != null) ? _emptySlotPreset : _preset;
            if (preset != null && preset.Entries != null)
            {
                for (int i = 0; i < preset.Entries.Count; i++)
                {
                    var entry = preset.Entries[i];
                    if (entry != null)
                        _resolvedEntries.Add(entry);
                }
            }

            var sceneEntries = slotIsEmpty && _emptySlotSceneEntries.Count > 0
                ? _emptySlotSceneEntries
                : _sceneEntries;

            for (int i = 0; i < sceneEntries.Count; i++)
            {
                var entry = sceneEntries[i];
                if (entry != null)
                    _resolvedEntries.Add(entry);
            }

            return _resolvedEntries;
        }
    }
}
