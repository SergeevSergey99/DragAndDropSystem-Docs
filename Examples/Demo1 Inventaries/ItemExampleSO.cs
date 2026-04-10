using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples.General
{
    [CreateAssetMenu(fileName = "ItemExampleSO", menuName = "DragAndDrop/Examples/ItemExampleSO", order = 1)]
    public class ItemExampleSO : ScriptableObject
    {
        [field: SerializeField] 
        public string ItemName { get; private set; }
        [field: SerializeField, PreviewField(200)] 
        public Sprite Icon { get; private set; }
        
        [field: SerializeField]
        public string itemType { get; private set; }
    }
}
