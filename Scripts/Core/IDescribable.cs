using DragAndDropSystem.UI;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Extended interface for items with detailed information
    /// </summary>
    public interface IDescribable
    {
        string Description { get; }
    }

}
