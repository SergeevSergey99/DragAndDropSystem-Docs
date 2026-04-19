using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class MergeOnlyAlternativePlacementStrategy : IAlternativePlacementStrategy
    {
        public AlternativePlacementMode Mode => AlternativePlacementMode.MergeOnly;
    }
}
