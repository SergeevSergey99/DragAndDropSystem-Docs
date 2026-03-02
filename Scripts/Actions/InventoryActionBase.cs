using System;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Базовый класс для действий инвентаря, которые можно привязать к клавишам через Input System
    /// </summary>
    [Serializable]
    public abstract class InventoryActionBase : MonoBehaviour
    {
        /// <summary>
        /// Имя типа действия для отображения в инспекторе
        /// </summary>
        public virtual string DisplayName => GetType().Name.Replace("Action", "");

        /// <summary>
        /// Выполнить действие
        /// </summary>
        /// <param name="inventory">Инвентарь, на котором выполняется действие</param>
        /// <param name="activeSlot">Активный слот (под курсором или последний взаимодействовавший)</param>
        /// <param name="logWarnings">Писать ли предупреждения в консоль</param>
        /// <returns>True если действие выполнено успешно</returns>
        public abstract bool Execute(UniversalInventory inventory, UniversalSlot activeSlot);

        /// <summary>
        /// Можно ли выполнить действие (проверка перед выполнением)
        /// </summary>
        public virtual bool CanExecute(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            return inventory != null;
        }
    }
}
