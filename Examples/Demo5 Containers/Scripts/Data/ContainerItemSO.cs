using UnityEngine;

namespace DragAndDropSystem.Examples.Containers
{
    /// <summary>
    /// Предмет-контейнер: наследует базовый тип + ёмкость, фильтр, глубина вложенности.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/ContainerItemSO")]
    public class ContainerItemSO : BaseItemSO
    {
        [Header("Container Settings")]
        [SerializeField, Range(1, 32), Tooltip("Количество слотов")]
        private int _capacity = 6;

        public int Capacity => _capacity;
    }
}