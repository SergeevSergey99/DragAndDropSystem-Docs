using System;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Base class for auto-transfer animation strategies
    /// Used through [SerializedReference] to select different animation types
    /// </summary>
    [Serializable]
    public abstract class AutoTransferAnimationStrategy
    {
        /// <summary>
        /// Animate item transfer from source to target
        /// </summary>
        /// <param name="stack">Item stack to display</param>
        /// <param name="sourceBaseSlot">Source slot</param>
        /// <param name="targetBaseSlot">Target slot</param>
        /// <param name="visualPrefab">Animation visual prefab</param>
        /// <param name="visualContainer">Container where the visual is created</param>
        /// <param name="canvas">Canvas used for coordinate calculations</param>
        /// <param name="onComplete">Callback invoked when animation completes</param>
        /// <returns>Visual GameObject tracked by DragAndDropManager</returns>
        public abstract GameObject AnimateTransfer(
            ItemStack stack,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            MonoBehaviour visualPrefab,
            Transform visualContainer,
            Canvas canvas,
            Action onComplete
        );
    }
}