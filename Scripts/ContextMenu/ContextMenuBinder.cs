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

        [SerializeField, Tooltip("Переопределить сценовые пункты для пустого слота. Если выключено — используются обычные сценовые пункты.")]
        private bool _overrideEmptySlotSceneEntries = false;

        [SerializeField, Tooltip("Сценовые пункты меню для пустого слота. Работает только если включён Override Empty Slot Scene Entries.")]
        private List<ContextMenuSceneEntryBase> _emptySlotSceneEntries = new();

        public UniversalInventory Inventory => _inventory;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        /// <summary>
        /// Вернуть записи пресета для данного состояния слота.
        /// Возвращает новый список — безопасно хранить ссылку.
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
