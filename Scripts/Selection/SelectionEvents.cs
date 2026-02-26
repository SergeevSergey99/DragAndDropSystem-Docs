using System;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Аргументы события изменения выделения
    /// </summary>
    public class SelectionChangedEventArgs : EventArgs
    {
        public SelectionContext Context { get; }

        public SelectionChangedEventArgs(SelectionContext context)
        {
            Context = context;
        }
    }
}
