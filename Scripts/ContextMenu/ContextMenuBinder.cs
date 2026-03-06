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

        public UniversalInventory Inventory => _inventory;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        /// <summary>
        /// Вернуть записи пресета для данного состояния слота.
        /// </summary>
        public IReadOnlyList<ContextMenuEntryDefinitionSO> GetEntries(bool slotIsEmpty)
        {
            var preset = (slotIsEmpty && _emptySlotPreset != null) ? _emptySlotPreset : _preset;
            return preset != null
                ? preset.Entries
                : System.Array.Empty<ContextMenuEntryDefinitionSO>();
        }
    }
}
