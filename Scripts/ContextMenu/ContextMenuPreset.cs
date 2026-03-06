using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Пресет контекстного меню — переиспользуемый список пунктов.
    /// Создаётся в Assets и назначается на <see cref="ContextMenuBinder"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ContextMenuPreset", menuName = "DragAndDrop/ContextMenu/Preset", order = 100)]
    public class ContextMenuPreset : ScriptableObject
    {
        [SerializeField] private List<ContextMenuEntryDefinitionSO> _entries = new();

        public IReadOnlyList<ContextMenuEntryDefinitionSO> Entries => _entries;
    }
}
