using UnityEngine;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Selection
{
    /// <summary>
    /// Base class for selection operations.
    /// Used through [SerializeReference] and does not require MonoBehaviour or ScriptableObject.
    ///
    /// Create a derived class to implement any selection logic:
    ///   public class SelectByRarityOperation : SelectByConditionOperation { ... }
    /// </summary>
    [System.Serializable]
    public abstract class SelectionOperationBase
    {
        public virtual string DisplayName => GetType().Name.Replace("Operation", "");

        /// <summary>
        /// Execute the selection operation.
        /// </summary>
        /// <param name="manager">Selection manager</param>
        /// <param name="contextBaseSlot">Slot that initiated the operation (can be null for buttons/hotkeys)</param>
        public abstract void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null);

        /// <summary>
        /// Whether the operation can be executed right now
        /// </summary>
        public virtual bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => manager != null;
    }
}