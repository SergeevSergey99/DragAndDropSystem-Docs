using System.Collections.Generic;
using System.Linq;
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

        [SerializeField] private bool _useGlobalPresets = true;
        [SerializeField, Tooltip("Menu entries for a non-empty slot.")]
        private ContextMenuPreset _preset;

        [SerializeField, Tooltip("Menu entries for an empty slot. If not set, the main preset is used.")]
        private ContextMenuPreset _emptySlotPreset;

        [SerializeField, Tooltip("Scene menu entries for a non-empty slot.")]
        private List<ContextMenuSceneEntryBase> _sceneEntries = new();

        [SerializeField, Tooltip("Scene menu entries for an empty slot. Works only if Override Empty Slot Scene Entries is enabled.")]
        private List<ContextMenuSceneEntryBase> _emptySlotSceneEntries = new();

        public UniversalInventory Inventory => _inventory;

        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
            
            ContextMenuManager.Instance.RegisterBinder(this);
        }
        
        private void OnDisable()
        {
            if (ContextMenuManager.IsInstanceExist)
                ContextMenuManager.Instance.UnregisterBinder(this);
        }

        /// <summary>
        /// Return preset entries for the current slot state.
        /// Returns a new list, so storing the reference is safe.
        /// </summary>
        public List<IContextMenuEntry> GetEntries(ContextMenuContext context)
        {
            var result = new List<IContextMenuEntry>();

            var preset = context.BaseSlot.IsEmpty 
                ? _emptySlotPreset 
                : _preset;
            
            if (_useGlobalPresets)
            {
                var globalPreset = context.BaseSlot.IsEmpty
                    ? ContextMenuManager.AutoCreateInstance.DefaultEmptySlotPreset
                    : ContextMenuManager.AutoCreateInstance.DefaultPreset;

                if (globalPreset != null && globalPreset.Entries != null)
                {
                    result.AddRange(globalPreset.Entries.Where(entry => entry != null));
                }
            }
            
            if (preset != null && preset.Entries != null)
            {
                result.AddRange(preset.Entries.Where(entry => entry != null));
            }

            var sceneEntries = context.BaseSlot.IsEmpty
                ? _emptySlotSceneEntries
                : _sceneEntries;

            result.AddRange(sceneEntries.Where(entry => entry != null));

            return result;
        }
    }
}
