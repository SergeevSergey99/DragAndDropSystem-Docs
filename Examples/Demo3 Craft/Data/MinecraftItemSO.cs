using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Examples.Craft
{
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Craft/CraftItemSO")]
    public class CraftItemSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(120)] public Sprite Icon { get; private set; }
    }
}
