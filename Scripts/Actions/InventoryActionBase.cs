using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Base class for inventory actions that can be bound to keys through the Input System
    /// </summary>
    [Serializable]
    public abstract class InventoryActionBase : MonoBehaviour
    {
        /// <summary>
        /// Action type name shown in the Inspector
        /// </summary>
        public virtual string DisplayName => GetType().Name.Replace("Action", "");

        /// <summary>
        /// Execute the action
        /// </summary>
        /// <param name="inventory">Inventory the action is executed on</param>
        /// <param name="activeBaseSlot">Active slot (under the cursor or the last interacted one)</param>
        /// <param name="logWarnings">Whether to write warnings to the console</param>
        /// <returns>Action execution result</returns>
        public abstract ActionResult Execute(UniversalInventory inventory, BaseSlot activeBaseSlot);

        /// <summary>
        /// Whether the action can be executed (pre-check before execution)
        /// </summary>
        public virtual bool CanExecute(UniversalInventory inventory, BaseSlot activeBaseSlot)
        {
            return inventory != null;
        }
    }
}