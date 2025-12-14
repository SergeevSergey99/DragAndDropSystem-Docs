using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.World3D
{
    /// <summary>
    /// Компонент для предметов, выброшенных в 3D мир
    /// Хранит ссылку на оригинальный IInventoryItem
    /// Опционально может быть подобран обратно в инвентарь
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        public IInventoryItem itemData { get; private set; }
        public int count { get; private set; }

        /// <summary>
        /// Инициализировать предмет с данными
        /// </summary>
        public void Initialize(IInventoryItem itemData, int count = 1)
        {
            this.itemData = itemData;
            this.count = Mathf.Max(1, count);
        }
    }
}
