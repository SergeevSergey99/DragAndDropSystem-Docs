using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples
{
    [System.Serializable]
    public class ItemModelExample
    {
        [field: SerializeField]
        public ItemExampleSO itemSO { get; private set; }
        
        public ItemModelExample(ItemExampleSO itemSo)
        {
            this.itemSO = itemSo;
        }
    }
}