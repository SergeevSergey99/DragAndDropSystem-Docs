using System.Collections.Generic;
using DragAndDropSystem.Inventories;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Binds context menu presets to a specific inventory.
    /// Add it to the same GameObject as <see cref="UniversalInventory"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ContextMenuBinder : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;

        [SerializeField, Tooltip("Menu entries for a non-empty slot.")]
        private ContextMenuPreset _preset;

        [SerializeField, Tooltip("Menu entries for an empty slot. If not set, the main preset is used.")]
        private ContextMenuPreset _emptySlotPreset;

        [SerializeField, Tooltip("Scene menu entries for a non-empty slot.")]
        private List<ContextMenuSceneEntryBase> _sceneEntries = new();

        [SerializeField, Tooltip("Override scene entries for an empty slot. If disabled, regular scene entries are used.")]
        private bool _overrideEmptySlotSceneEntries = false;

        [SerializeField, Tooltip("Scene menu entries for an empty slot. Works only if Override Empty Slot Scene Entries is enabled.")]
        private List<ContextMenuSceneEntryBase> _emptySlotSceneEntries = new();

        public UniversalInventory Inventory => _inventory;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        /// <summary>
        /// Return preset entries for the current slot state.
        /// Returns a new list, so storing the reference is safe.
        /// </summary>
        public List<IContextMenuEntry> GetEntries(bool slotIsEmpty)
        {
            var result = new List<IContextMenuEntry>();

            var preset = (slotIsEmpty && _emptySlotPreset != null) ? _emptySlotPreset : _preset;
            if (preset != null && preset.Entries != null)
            {
                for (int i = 0; i < preset.Entries.Count; i++)
                {
                    var entry = preset.Entries[i];
                    if (entry != null)
                        result.Add(entry);
                }
            }

            var sceneEntries = (slotIsEmpty && _overrideEmptySlotSceneEntries)
                ? _emptySlotSceneEntries
                : _sceneEntries;

            for (int i = 0; i < sceneEntries.Count; i++)
            {
                var entry = sceneEntries[i];
                if (entry != null)
                    result.Add(entry);
            }

            return result;
        }
    }
}
