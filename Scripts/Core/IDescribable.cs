using DragAndDropSystem.UI;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Расширенный интерфейс для предметов с детальной информацией
    /// </summary>
    public interface IDescribable
    {
        string Description { get; }
    }

}
