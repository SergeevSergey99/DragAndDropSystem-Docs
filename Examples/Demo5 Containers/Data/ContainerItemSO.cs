using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Тип предмета. Может быть контейнером (если указан ContainerType).
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/Item")]
    public class ContainerItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(72f)] public Sprite Icon { get; private set; }
        [field: SerializeField] public string Category { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [SerializeField, Tooltip("Null для обычных предметов. Задать для контейнеров.")]
        private ContainerTypeSO _containerType;

        public ContainerTypeSO ContainerType => _containerType;
        public bool IsContainer => _containerType != null;
    }
}
