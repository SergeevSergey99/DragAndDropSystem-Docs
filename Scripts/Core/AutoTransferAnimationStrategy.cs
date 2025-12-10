using System;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Базовый класс для стратегий анимации автопереноса
    /// Используется через [SerializedReference] для выбора разных типов анимации
    /// </summary>
    [Serializable]
    public abstract class AutoTransferAnimationStrategy
    {
        /// <summary>
        /// Анимировать перенос предмета из источника в цель
        /// </summary>
        /// <param name="stack">Стак предметов для отображения</param>
        /// <param name="sourceSlot">Слот-источник</param>
        /// <param name="targetSlot">Слот-цель</param>
        /// <param name="visualPrefab">Префаб визуала для анимации</param>
        /// <param name="visualContainer">Контейнер для создания визуала</param>
        /// <param name="canvas">Canvas для расчета координат</param>
        /// <param name="onComplete">Колбэк по завершении анимации</param>
        /// <returns>GameObject визуала для отслеживания в DragAndDropManager</returns>
        public abstract GameObject AnimateTransfer(
            ItemStack stack,
            ISlot sourceSlot,
            ISlot targetSlot,
            MonoBehaviour visualPrefab,
            Transform visualContainer,
            Canvas canvas,
            Action onComplete
        );
    }
}
