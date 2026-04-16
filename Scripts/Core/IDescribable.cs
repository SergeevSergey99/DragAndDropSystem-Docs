using UniversalDragAndDrop.UI;
using UnityEngine;

namespace UniversalDragAndDrop.Core
{
    /// <summary>
    /// Extended interface for items with detailed information
    /// </summary>
    public interface IDescribable
    {
        string Description { get; }
    }
}