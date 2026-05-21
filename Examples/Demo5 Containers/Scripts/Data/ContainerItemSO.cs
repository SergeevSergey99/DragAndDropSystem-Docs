using UnityEngine;

namespace UDND.Examples.Containers
{
    /// <summary>
    /// Container item: extends the base item type with capacity, filter, and nesting depth.
    /// </summary>
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Containers/ContainerItemSO")]
    public class ContainerItemSO : BaseItemSO
    {
        [Header("Container Settings")]
        [SerializeField, Range(1, 32), Tooltip("Number of slots")]
        private int _capacity = 6;

        public int Capacity => _capacity;
    }
}