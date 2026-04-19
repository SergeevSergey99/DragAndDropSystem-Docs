using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class MergeFirstAlternativePlacementStrategy : IAlternativePlacementStrategy
    {
        public AlternativePlacementMode Mode => AlternativePlacementMode.MergeFirst;
    }
}
