using System;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Аргументы событий системы drag-and-drop
    /// </summary>
    public class DragEventArgs : EventArgs
    {
        public DragContext Context { get; }
        public bool Cancel { get; set; }

        public DragEventArgs(DragContext context)
        {
            Context = context;
            Cancel = false;
        }
    }

    /// <summary>
    /// Аргументы событий автопереноса
    /// </summary>
    public class AutoTransferEventArgs : EventArgs
    {
        public DragContext Context { get; }
        public bool Cancel { get; set; }

        public AutoTransferEventArgs(DragContext context)
        {
            Context = context;
            Cancel = false;
        }
    }

    /// <summary>
    /// Интерфейс для объектов, генерирующих события drag-and-drop
    /// </summary>
    public interface IDragEvents
    {
        /// <summary>
        /// Перед началом перетаскивания (можно отменить)
        /// </summary>
        event EventHandler<DragEventArgs> OnDragStarting;

        /// <summary>
        /// Перетаскивание началось
        /// </summary>
        event EventHandler<DragEventArgs> OnDragStarted;

        /// <summary>
        /// Курсор вошел в слот
        /// </summary>
        event EventHandler<DragEventArgs> OnDragEnterSlot;

        /// <summary>
        /// Курсор вышел из слота
        /// </summary>
        event EventHandler<DragEventArgs> OnDragExitSlot;

        /// <summary>
        /// Перед попыткой бросить предмет (можно отменить)
        /// </summary>
        event EventHandler<DragEventArgs> OnDropAttempting;

        /// <summary>
        /// Предмет успешно перемещен
        /// </summary>
        event EventHandler<DragEventArgs> OnDropCompleted;

        /// <summary>
        /// Перетаскивание отменено
        /// </summary>
        event EventHandler<DragEventArgs> OnDragCancelled;
    }
}
