using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Tests
{
    /// <summary>
    /// Minimal concrete BaseSlot for EditMode tests.
    /// No visuals, no UI — relies entirely on BaseSlot defaults.
    /// Lives in a runtime test assembly so AddComponent works.
    /// </summary>
    public sealed class TestSlot : BaseSlot
    {
    }
}
