using DragAndDropSystem.Core;
using Plugins.DragAndDropSystem.Examples;
using UnityEngine;

namespace DragAndDropSystem.Examples.Demo2Loot
{
    /// <summary>
    /// Компонент для предметов, выброшенных в 3D мир
    /// Хранит ссылку на оригинальный IItemAdapter
    /// Опционально может быть подобран обратно в инвентарь
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        public ItemExampleWith3DSO Item { get; private set; }

        /// <summary>
        /// Инициализировать предмет с данными
        /// </summary>
        public void Initialize(ItemExampleWith3DSO item)
        {
            Item = item;
        }
    }
}
