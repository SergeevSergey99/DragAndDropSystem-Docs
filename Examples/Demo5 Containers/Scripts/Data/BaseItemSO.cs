using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Examples.Containers
{
    /// <summary>
    /// Base item type.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/BaseItemSO")]
    public class BaseItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(72f)] public Sprite Icon { get; private set; }
    }
}
