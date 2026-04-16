using System.Collections.Generic;
using UnityEngine;

namespace UniversalDragAndDrop.ContextMenu
{
    /// <summary>
    /// Context menu preset: a reusable list of entries.
    /// Created in Assets and assigned to <see cref="ContextMenuBinder"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ContextMenuPreset", menuName = "DragAndDrop/ContextMenu/Preset", order = 100)]
    public class ContextMenuPreset : ScriptableObject
    {
        [SerializeField] private List<ContextMenuEntryDefinitionSO> _entries = new();

        public IReadOnlyList<ContextMenuEntryDefinitionSO> Entries => _entries;
    }
}