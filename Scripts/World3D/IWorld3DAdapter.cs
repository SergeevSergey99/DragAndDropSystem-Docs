using DragAndDropSystem.Examples.Demo3Loot;
using UnityEngine;

namespace DragAndDropSystem.World3D
{
    /// <summary>
    /// Интерфейс для связи IInventoryItem с 3D префабами
    /// Реализуйте этот интерфейс в ваших ScriptableObject предметах,
    /// чтобы они могли быть выброшены в мир
    /// </summary>
    public interface IWorld3DAdapter
    {
        /// <summary>
        /// Префаб 3D объекта для создания в мире
        /// </summary>
        GameObject WorldPrefab { get; }
    }
}
