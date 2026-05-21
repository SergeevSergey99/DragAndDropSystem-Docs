using UDND.UI;
using UnityEngine;

namespace UDND.Core
{
    /// <summary>
    /// Extended interface for items with detailed information
    /// </summary>
    public interface IDescribable
    {
        string Description { get; }
    }
}