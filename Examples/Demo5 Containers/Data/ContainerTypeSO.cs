using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Конфигурация типа контейнера: ёмкость, фильтр категорий, глубина вложенности.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/Container Type")]
    public class ContainerTypeSO : ScriptableObject
    {
        [SerializeField, Range(1, 32), Tooltip("Количество слотов в контейнере")]
        private int _capacity = 6;

        [SerializeField, Tooltip("Разрешённые категории предметов (пусто = все)")]
        private string[] _allowedCategories;

        [SerializeField, Range(0, 5), Tooltip("Макс. глубина вложенности контейнеров. 0 = не может содержать контейнеры")]
        private int _maxNestingDepth = 1;

        public int Capacity => _capacity;
        public string[] AllowedCategories => _allowedCategories;
        public int MaxNestingDepth => _maxNestingDepth;
    }
}
