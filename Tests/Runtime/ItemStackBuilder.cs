using NUnit.Framework;
using UDND.Core;

namespace UDND.Tests
{
    /// <summary>
    /// Thin wrapper over ItemStack.TryCreate. Keeps tests readable and asserts
    /// the stack was constructed successfully so failures surface early.
    /// </summary>
    public static class ItemStackBuilder
    {
        public static ItemStack Of(params IItemAdapter[] adapters)
        {
            Assert.IsTrue(ItemStack.TryCreate(adapters, out var stack),
                "ItemStackBuilder.Of failed to create stack from adapters");
            return stack;
        }

        public static ItemStack Unique(int count, string itemId = "fake")
            => Of(FakeItemAdapter.Many(count, itemId));
    }
}
