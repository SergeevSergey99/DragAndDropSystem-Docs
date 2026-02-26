using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Базовый класс для операций выделения.
    /// Используется через [SerializeReference] — не требует MonoBehaviour или ScriptableObject.
    ///
    /// Создайте наследника чтобы реализовать любую логику выделения:
    ///   public class SelectByRarityOperation : SelectByConditionOperation { ... }
    /// </summary>
    [System.Serializable]
    public abstract class SelectionOperationBase
    {
        public virtual string DisplayName => GetType().Name.Replace("Operation", "");

        /// <summary>
        /// Выполнить операцию выделения.
        /// </summary>
        /// <param name="manager">Менеджер выделения</param>
        /// <param name="contextSlot">Слот, инициировавший операцию (может быть null для кнопок/хоткеев)</param>
        public abstract void Execute(SelectionManager manager, ISlot contextSlot = null);

        /// <summary>
        /// Можно ли выполнить операцию прямо сейчас
        /// </summary>
        public virtual bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => manager != null;
    }
}
