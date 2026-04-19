using System;

namespace UniversalDragAndDrop.Core
{
    [Serializable]
    public sealed class EmptyFirstAlternativePlacementStrategy : IAlternativePlacementStrategy
    {
        public AlternativePlacementMode Mode => AlternativePlacementMode.EmptyFirst;
    }
}
