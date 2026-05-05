using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Tests
{
    /// <summary>
    /// Minimal concrete BaseSlot for EditMode tests.
    /// No visuals, no UI — relies entirely on BaseSlot defaults.
    /// Lives in a runtime test assembly so AddComponent works.
    /// </summary>
    public sealed class TestSlot : BaseSlot
    {
        private ItemStack _testStack = ItemStack.Empty();

        public override ItemStack Stack => Inventory != null ? base.Stack : _testStack;

        public override void SetStack(ItemStack stack)
        {
            if (Inventory != null)
            {
                base.SetStack(stack);
                return;
            }

            _testStack = stack ?? ItemStack.Empty();
            UpdateVisuals();
        }

        public override void Clear()
        {
            if (Inventory != null)
            {
                base.Clear();
                return;
            }

            _testStack = ItemStack.Empty();
            UpdateVisuals();
        }
    }
}
