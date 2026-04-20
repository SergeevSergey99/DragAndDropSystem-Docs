using UnityEngine;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Tests
{
    /// <summary>
    /// Minimal IItemAdapter for tests. Each instance is a distinct adapter reference;
    /// stackability is controlled by ItemId + runtime type, matching ItemStack.CanStack.
    /// </summary>
    public sealed class FakeItemAdapter : IItemAdapter
    {
        public string ItemId { get; }
        public string DisplayName { get; }
        public Sprite Icon => null;

        public FakeItemAdapter(string itemId = "fake", string displayName = null)
        {
            ItemId = itemId;
            DisplayName = displayName ?? itemId;
        }

        public static FakeItemAdapter[] Many(int count, string itemId = "fake")
        {
            var arr = new FakeItemAdapter[count];
            for (int i = 0; i < count; i++)
                arr[i] = new FakeItemAdapter(itemId, $"{itemId}#{i}");
            return arr;
        }
    }
}
