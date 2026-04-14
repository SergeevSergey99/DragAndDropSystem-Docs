using DragAndDropSystem.Examples.Loot;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples
{
    /// <summary>
    /// Example item with support for a 3D representation
    /// Extends ItemExampleSO by adding a field for a 3D prefab
    /// Use ItemAdapterSoWith3DAdapter to work with the system
    /// </summary>
    [CreateAssetMenu(fileName = "ItemExampleWith3DSO", menuName = "DragAndDrop/Examples/ItemExampleWith3DSO", order = 2)]
    public class ItemExampleWith3DSO : ScriptableObject
    {
        [field: SerializeField] 
        public string ItemName { get; private set; }

        public Sprite Icon => _worldPrefab.spriteRenderer.sprite;
        
        [field: SerializeField]
        public string itemType { get; private set; }
        
        [SerializeField, Tooltip("Prefab to spawn in the 3D world when dropped")]
        private ItemController _worldPrefab;

        // Public property for adapter access
        public ItemController WorldPrefab => _worldPrefab;
    }
}