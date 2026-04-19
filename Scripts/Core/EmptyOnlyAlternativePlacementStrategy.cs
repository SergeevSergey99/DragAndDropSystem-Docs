using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class EmptyOnlyAlternativePlacementStrategy : IAlternativePlacementStrategy
    {
        public AlternativePlacementMode Mode => AlternativePlacementMode.EmptyOnly;
    }
}
