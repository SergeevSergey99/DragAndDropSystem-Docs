using DragAndDropSystem.Examples.Demo3Loot;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples
{
    /// <summary>
    /// Пример предмета с поддержкой 3D представления
    /// Расширяет ItemExampleSO добавляя поле для 3D префаба
    /// Для работы с системой используйте ItemAdapterSoWith3DAdapter
    /// </summary>
    [CreateAssetMenu(fileName = "ItemExampleWith3DSO", menuName = "DragAndDrop/Examples/ItemExampleWith3DSO", order = 2)]
    public class ItemExampleWith3DSO : ScriptableObject
    {
        [field: SerializeField] 
        public string ItemName { get; private set; }

        public Sprite Icon => _worldPrefab.spriteRenderer.sprite;
        
        [field: SerializeField]
        public string itemType { get; private set; }
        
        [SerializeField, Tooltip("Префаб для создания в 3D мире при выбрасывании")]
        private ItemController _worldPrefab;

        // Публичное свойство для доступа из адаптера
        public ItemController WorldPrefab => _worldPrefab;
    }
}
