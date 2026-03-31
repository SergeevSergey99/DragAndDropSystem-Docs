using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Базовый тип предмета.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/BaseItemSO")]
    public class BaseItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(72f)] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }
    }
}
