using Sirenix.OdinInspector;
using UnityEngine;

namespace Plugins.DragAndDropSystem.Examples
{
    /// <summary>
    /// Пример предмета с поддержкой 3D представления
    /// Расширяет ItemExampleSO добавляя поле для 3D префаба
    /// Для работы с системой используйте ItemSOWith3DAdapter
    /// </summary>
    [CreateAssetMenu(fileName = "ItemExampleWith3DSO", menuName = "DragAndDropSystem/Examples/ItemExampleWith3DSO", order = 2)]
    public class ItemExampleWith3DSO : ItemExampleSO
    {
        [BoxGroup("3D World Representation")]
        [SerializeField, Tooltip("Префаб для создания в 3D мире при выбрасывании")]
        [PreviewField(100)]
        private GameObject _worldPrefab;

        // Публичное свойство для доступа из адаптера
        public GameObject WorldPrefab => _worldPrefab;
        public bool HasWorldRepresentation => _worldPrefab != null;
    }
}
